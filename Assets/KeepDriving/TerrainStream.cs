using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
    public sealed class TerrainStream : MonoBehaviour
    {
        [SerializeField, Tooltip("Seed for generated terrain. Set before entering Play Mode.")]
        private int worldSeed = 0;
        [SerializeField, Range(0f, 100f), Tooltip("Approximate generated land covered by rivers and lakes.")]
        private int waterCoverage = 5;
        [SerializeField, Range(0, 100), Tooltip("Tree density on generated tiles. Set before entering Play Mode.")]
        private int treeDensity = 25;
        [SerializeField, Tooltip("Drop tree prefab assets here. Empty slots are ignored.")]
        private GameObject[] treePrefabs = new GameObject[3];

        private const int WindowRadius = 2;
        private const float PrefetchDistance = 125f;
        private const float OriginShiftDistance = 1600f;
        private static readonly ProfilerMarker GenerateMarker = new ProfilerMarker("TerrainStream.GenerateHeights");
        private static readonly ProfilerMarker InstallMarker = new ProfilerMarker("TerrainStream.InstallTile");

        private Terrain _source;
        private TerrainCollider _sourceCollider;
        private TerrainData _sourceData;
        private Vector3 _origin;
        private Vector3 _size;
        private Vector2 _noiseOffset;
        private readonly Dictionary<Vector2Int, Terrain> _tiles = new Dictionary<Vector2Int, Terrain>();
        private readonly Dictionary<Terrain, (RiverGenerator.Tile Layout, Mesh Mesh)> _waterTiles = new();
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private float[,] _sourceHeights;
        private RiverGenerator _rivers;
        private TreeGenerator _trees;
        private TreePrototype[] _treePrototypes;
        private Material _waterMaterial;
        private Task<TileContents> _pendingHeights;
        private Vector2Int _pendingKey;
        private float _retryAfter;
        private Vector2Int _originTile;

        private readonly struct TileContents
        {
            public readonly float[,] Heights;
            public readonly RiverGenerator.Tile Water;
            public readonly Vector3[] Surface;
            public readonly TreeInstance[] Trees;

            public TileContents(float[,] heights, RiverGenerator.Tile water, Vector3[] surface, TreeInstance[] trees)
            {
                Heights = heights;
                Water = water;
                Surface = surface;
                Trees = trees;
            }
        }

        private void Awake()
        {
            _source = GetComponent<Terrain>();
            _sourceCollider = GetComponent<TerrainCollider>();
            _sourceData = _source.terrainData;
            if (!_sourceData) return;

            _noiseOffset = NoiseOffset(worldSeed);
            _origin = _source.GetPosition();
            _size = _sourceData.size;
            if (treeDensity > 0 && treePrefabs != null)
            {
                var prototypes = new List<TreePrototype>(treePrefabs.Length);
                foreach (var prefab in treePrefabs)
                    if (prefab && !prefab.scene.IsValid())
                        prototypes.Add(new TreePrototype { prefab = prefab });
                if (prototypes.Count > 0)
                {
                    _treePrototypes = prototypes.ToArray();
                    _trees = new TreeGenerator(worldSeed, treeDensity, prototypes.Count, _size);
                }
            }
            if (waterCoverage > 0)
            {
                var shader = Resources.Load<Shader>("ProceduralWater");
                if (shader)
                {
                    _waterMaterial = new Material(shader) { name = "Procedural River and Lake Water" };
                    _rivers = new RiverGenerator(worldSeed, waterCoverage, new Vector2(_size.x, _size.z));
                }
                else
                {
                    Debug.LogError("Procedural water shader is missing. Terrain will generate dry.", this);
                }
            }
            int resolution = _sourceData.heightmapResolution;
            _sourceHeights = _sourceData.GetHeights(0, 0, resolution, resolution);
            _tiles.Add(Vector2Int.zero, _source);
        }

        public bool HasSource => isActiveAndEnabled && _sourceData;
        public bool InitialWindowReady { get; private set; }

        public Vector3 SpawnPosition => _origin + new Vector3(_size.x / 2,
            _sourceData.GetInterpolatedHeight(.5f, .5f) + 1f, _size.z / 2) - TileOffset(_originTile);

        private Vector3 TileOffset(Vector2Int tile) => new Vector3(tile.x * _size.x, 0, tile.y * _size.z);

        public Vector2Int? GetTile(Vector3 point) => HasSource ? WorldTile(point) : null;

        internal bool HasWaterAt(Terrain terrain, float u, float v) =>
            _waterTiles.TryGetValue(terrain, out var water) && water.Layout.ContainsWater(u, v);

        private Vector2Int WorldTile(Vector3 point)
        {
            return _originTile + new Vector2Int(Mathf.FloorToInt((point.x - _origin.x) / _size.x),
                Mathf.FloorToInt((point.z - _origin.z) / _size.z));
        }

        // Returns true when the gameplay scene was shifted to keep physics near the origin.
        public bool ShiftOriginIfNeeded(Vector3 focus)
        {
            if (!HasSource)
            {
                return false;
            }

            Vector2Int center = WorldTile(focus);
            bool shifted = Mathf.Abs(focus.x) > OriginShiftDistance || Mathf.Abs(focus.z) > OriginShiftDistance;
            if (shifted)
            {
                Vector3 shift = TileOffset(center - _originTile);
                if (shift == Vector3.zero)
                {
                    shifted = false;
                }
                else
                {
                    gameObject.scene.GetRootGameObjects(_roots);
                    foreach (GameObject root in _roots)
                    {
                        root.transform.position -= shift;
                    }

                    _originTile = center;
                }
            }

            if (shifted) Physics.SyncTransforms();
            return shifted;
        }

        // Call once per rendered frame. Only this method installs streamed tiles.
        public void Pump(Vector3 focus)
        {
            if (!HasSource) return;
            Vector2Int center = WorldTile(focus);
            Vector3 withinTile = focus - (_origin + TileOffset(center - _originTile));
            var ahead = new Vector2Int(
                PrefetchDirection(withinTile.x, _size.x),
                PrefetchDirection(withinTile.z, _size.z));
            var min = center + Vector2Int.Min(ahead, Vector2Int.zero) - Vector2Int.one * WindowRadius;
            var max = center + Vector2Int.Max(ahead, Vector2Int.zero) + Vector2Int.one * WindowRadius;

            if (_pendingHeights?.IsCompleted == true)
            {
                var completed = _pendingHeights;
                _pendingHeights = null;
                if (completed.IsFaulted)
                {
                    Debug.LogException(completed.Exception);
                    _retryAfter = Time.unscaledTime + 1f;
                }
                else if (!completed.IsCanceled && Contains(_pendingKey, min, max))
                {
                    _tiles.Add(_pendingKey, CreateTile(_pendingKey, completed.Result));
                    RefreshNeighbors();
                    return;
                }
            }

            Vector2Int? next = null;
            int bestPriority = int.MaxValue;
            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                var key = new Vector2Int(x, z);
                if (_tiles.ContainsKey(key)) continue;
                var offset = key - center;
                bool prefetchOnly = Mathf.Abs(offset.x) > WindowRadius || Mathf.Abs(offset.y) > WindowRadius;
                int priority = offset.sqrMagnitude + (prefetchOnly ? 100 : 0);
                if (priority < bestPriority)
                {
                    next = key;
                    bestPriority = priority;
                }
            }

            if (bestPriority >= 100)
            {
                InitialWindowReady = true;
                // Keep existing colliders until all tiles in the new window are installed.
                Vector2Int? expired = null;
                foreach (var entry in _tiles)
                    if (!Contains(entry.Key, min, max))
                    {
                        expired = entry.Key;
                        break;
                    }
                if (expired is Vector2Int key)
                {
                    UnloadTile(key);
                    RefreshNeighbors();
                    return;
                }
            }

            if (_pendingHeights != null || Time.unscaledTime < _retryAfter || next is not Vector2Int requested)
                return;
            if (requested == Vector2Int.zero)
            {
                RestoreSourceTile();
                return;
            }

            _pendingKey = requested;
            float[,] sourceHeights = _sourceHeights;
            float amplitude = Mathf.Min(18f / _size.y, 1f);
            Vector2 noiseOffset = _noiseOffset;
            RiverGenerator rivers = _rivers;
            TreeGenerator trees = _trees;
            float heightScale = _size.y;
            CancellationToken token = _shutdown.Token;
            _pendingHeights = Task.Run(() =>
            {
                var water = rivers?.GenerateTile(requested, token);
                var heights = GenerateHeights(requested, sourceHeights, amplitude, noiseOffset, water, heightScale, token);
                return new TileContents(heights, water, water?.BuildSurface(token),
                    trees?.GenerateTile(requested, heights, water, token));
            }, token);
        }

        public void RestoreSourceTile()
        {
            if (_tiles.ContainsKey(Vector2Int.zero)) return;
            SetSourceTileLoaded(true);
            _tiles.Add(Vector2Int.zero, _source);
            RefreshNeighbors();
        }

        private static int PrefetchDirection(float position, float size)
        {
            float margin = Mathf.Min(PrefetchDistance, size * .25f);
            return position < margin ? -1 : position > size - margin ? 1 : 0;
        }

        private static bool Contains(Vector2Int key, Vector2Int min, Vector2Int max) =>
            key.x >= min.x && key.x <= max.x && key.y >= min.y && key.y <= max.y;

        private void UnloadTile(Vector2Int key)
        {
            if (key == Vector2Int.zero)
            {
                _source.SetNeighbors(null, null, null, null);
                SetSourceTileLoaded(false);
            }
            else
            {
                DestroyTile(_tiles[key]);
            }
            _tiles.Remove(key);
        }

        private void SetSourceTileLoaded(bool loaded)
        {
            // The streamer and atmosphere share this GameObject with the authored tile.
            _source.enabled = loaded;
            _sourceCollider.enabled = loaded;
        }

        private void RefreshNeighbors()
        {
            foreach (var entry in _tiles)
            {
                Vector2Int key = entry.Key;
                entry.Value.SetNeighbors(Find(key + Vector2Int.left), Find(key + Vector2Int.up),
                    Find(key + Vector2Int.right), Find(key + Vector2Int.down));
            }
            Physics.SyncTransforms();
        }

        private Terrain Find(Vector2Int key) => _tiles.TryGetValue(key, out Terrain terrain) ? terrain : null;

        private static Vector2 NoiseOffset(int seed)
        {
            // Seed zero keeps the terrain layout from before the seed was exposed.
            var originalOffset = new Vector2(37.5f, 71.5f);
            unchecked
            {
                uint hash = (uint)seed;
                hash = (hash ^ (hash >> 16)) * 0x7feb352du;
                hash = (hash ^ (hash >> 15)) * 0x846ca68bu;
                hash ^= hash >> 16;
                return originalOffset + new Vector2(hash & 0xffffu, hash >> 16) / 256f;
            }
        }

        private static float[,] GenerateHeights(Vector2Int key, float[,] sourceHeights,
            float amplitude, Vector2 noiseOffset, RiverGenerator.Tile water, float heightScale, CancellationToken token)
        {
            using var sample = GenerateMarker.Auto();
            int resolution = sourceHeights.GetLength(0);
            var heights = new float[resolution, resolution];
            bool nearSource = Mathf.Abs(key.x) <= 1 && Mathf.Abs(key.y) <= 1;
            for (int z = 0; z < resolution; z++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (resolution - 1f), v = z / (resolution - 1f);
                    float worldX = key.x + u;
                    float worldZ = key.y + v;
                    float edgeX = Mathf.Clamp01(worldX);
                    float edgeZ = Mathf.Clamp01(worldZ);
                    float distance = Mathf.Max(Mathf.Abs(worldX - edgeX), Mathf.Abs(worldZ - edgeZ));
                    float blend = Mathf.SmoothStep(0f, 1f, distance * 2f);
                    // Sample one continuous noise field in logical world coordinates across every tile.
                    float hills = Mathf.PerlinNoise(worldX * 3.2f + noiseOffset.x, worldZ * 3.2f + noiseOffset.y)
                        * amplitude;
                    float edgeHeight = nearSource && blend < 1f
                        ? SampleSourceEdge(sourceHeights, edgeX, edgeZ) : 0f;
                    float height = Mathf.Lerp(edgeHeight, hills, blend);
                    heights[z, x] = water == null ? height
                        : Mathf.Clamp01(water.ShapeGround(u, v, height * heightScale) / heightScale);
                }
            }
            return heights;
        }

        private static float SampleSourceEdge(float[,] heights, float u, float v)
        {
            int last = heights.GetLength(0) - 1;
            float x = u * last, z = v * last;
            int x0 = (int)x, z0 = (int)z;
            int x1 = Mathf.Min(x0 + 1, last), z1 = Mathf.Min(z0 + 1, last);
            float south = Mathf.Lerp(heights[z0, x0], heights[z0, x1], x - x0);
            float north = Mathf.Lerp(heights[z1, x0], heights[z1, x1], x - x0);
            return Mathf.Lerp(south, north, z - z0);
        }

        private Terrain CreateTile(Vector2Int key, TileContents contents)
        {
            using var sample = InstallMarker.Auto();
            var layers = _sourceData.terrainLayers;
            var data = new TerrainData
            {
                heightmapResolution = contents.Heights.GetLength(0),
                size = _size,
                terrainLayers = layers
            };
            data.SetHeights(0, 0, contents.Heights);
            if (contents.Trees is { Length: > 0 })
            {
                data.treePrototypes = _treePrototypes;
                data.SetTreeInstances(contents.Trees, true);
            }

            if (layers.Length > 0)
            {
                data.alphamapResolution = 16;
                var paint = new float[16, 16, layers.Length];
                for (int z = 0; z < 16; z++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        paint[z, x, 0] = 1;
                    }
                }
                data.SetAlphamaps(0, 0, paint);
            }

            GameObject tile = Terrain.CreateTerrainGameObject(data);
            SceneManager.MoveGameObjectToScene(tile, gameObject.scene);
            tile.name = $"Terrain ({key.x}, {key.y})";
            tile.layer = _source.gameObject.layer;
            tile.transform.position = _origin + TileOffset(key - _originTile);
            var terrain = tile.GetComponent<Terrain>();
            terrain.allowAutoConnect = false;
            terrain.drawInstanced = _source.drawInstanced;
            terrain.heightmapPixelError = _source.heightmapPixelError;
            terrain.materialTemplate = _source.materialTemplate;
            if (contents.Surface is { Length: > 0 })
                AttachWaterSurface(terrain, contents);
            return terrain;
        }

        private void AttachWaterSurface(Terrain terrain, TileContents contents)
        {
            var mesh = new Mesh { name = "Water Surface" };
            mesh.vertices = contents.Surface;
            var indices = new int[contents.Surface.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.triangles = indices;
            mesh.UploadMeshData(true);

            var water = new GameObject("Procedural Water");
            water.transform.SetParent(terrain.transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _waterMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _waterTiles.Add(terrain, (contents.Water, mesh));
        }

        private void DestroyTile(Terrain tile)
        {
            if (_waterTiles.Remove(tile, out var water)) Object.Destroy(water.Mesh);
            if (!tile)
            {
                return;
            }

            tile.gameObject.SetActive(false);
            Object.Destroy(tile.terrainData);
            Object.Destroy(tile.gameObject);
        }

        private void OnDestroy()
        {
            _shutdown.Cancel();
            _shutdown.Dispose();
            if (_source)
            {
                _source.SetNeighbors(null, null, null, null);
                if (_sourceCollider && gameObject.scene.isLoaded) SetSourceTileLoaded(true);
            }
            foreach (var entry in _tiles)
            {
                if (entry.Key != Vector2Int.zero)
                {
                    DestroyTile(entry.Value);
                }
            }
            _tiles.Clear();
            if (_waterMaterial) Object.Destroy(_waterMaterial);
        }
    }
}
