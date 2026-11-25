using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
    public sealed class TerrainStream : MonoBehaviour
    {
        [SerializeField] private TextAsset spawnGeoJson;
        [SerializeField] private TextAsset sitesGeoJson;
        [SerializeField] private TextAsset elevation;
        [SerializeField] private TextAsset elevationControlPoints;
        [SerializeField] private Node satelliteDish;
        [SerializeField] private TextAsset roadsGeoJson;
        [SerializeField] private Shader dirtRoadShader;
        [SerializeField] private TextAsset waterwaysGeoJson;
        [SerializeField] private TextAsset waterBodiesGeoJson;

        private const int WindowRadius = 1;
        private const float OriginShiftDistance = 1600f;
        private const float RiverDepth = 10f, StreamDepth = 5f, SeasonalStreamDepth = 1f;
        private const int TileSize = YanorraMap.TileSize;

        private readonly Dictionary<Vector2Int, Terrain> _tiles = new();
        private readonly Dictionary<string, Node> _dishes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Terrain, List<MeshFilter>> _routeMeshes = new();
        private readonly List<(RouteSurface Surface, Material Material, string Name, Color MapColor, float MapWidth)> _waterways = new();
        private readonly List<(RouteSurface Surface, Material Material, string Name, Color MapColor, float MapWidth)> _landRoutes = new();
        private readonly List<WaterBody> _waterBodies = new();
        private readonly List<Rect> _waterMapMarkers = new();
        private readonly List<GameObject> _roots = new();
        private readonly List<Vector2Int> _expired = new();
        private readonly CancellationTokenSource _shutdown = new();

        private Terrain _source, _startingTerrain;
        private TerrainCollider _startingCollider;
        private TerrainCollider _sourceCollider;
        private TerrainData _sourceData;
        private Vector3 _origin;
        private Vector2Int _originTile;
        private YanorraMap.Point _spawn;
        private YanorraMap.Elevation _elevation;
        private Dictionary<Vector2Int, List<YanorraMap.Point>> _sites;
        private float[,] _sourceHeights;
        private float _datumOffset, _baseY, _heightRange, _sourceHeightSize;
        private GameObject _sitesRoot;
        private Task<float[,]> _pendingHeights;
        private Vector2Int _pendingKey;
        private float _retryAfter;

        public bool HasSource => isActiveAndEnabled && _sourceData && _elevation != null;
        public bool InitialWindowReady { get; private set; }
        public float SpawnHeading => _spawn.Heading;
        public float RecoveryHeight => _baseY - 20f;

        private void Awake()
        {
            _source = GetComponent<Terrain>();
            _sourceCollider = GetComponent<TerrainCollider>();
            _startingTerrain = _source;
            _startingCollider = _sourceCollider;
            _sourceData = _source.terrainData;
            if (!_sourceData) return;

            try
            {
                if (_sourceData.size.x != TileSize || _sourceData.size.z != TileSize)
                    throw new InvalidOperationException("The authored starting terrain must be 500 m × 500 m.");

                _spawn = YanorraMap.ReadSpawn(spawnGeoJson);
                _sites = YanorraMap.ReadSites(sitesGeoJson);
                if (_sites.Count > 0 && !satelliteDish)
                    throw new InvalidOperationException("Assign the SatelliteDish prefab's Node to the terrain streamer.");
                _elevation = elevationControlPoints
                    ? new YanorraMap.Elevation(elevationControlPoints, elevation)
                    : new YanorraMap.Elevation(elevation);
            }
            catch (Exception error)
            {
                Debug.LogError($"Could not load the Yanorra map: {error.Message}", this);
                return;
            }

            _origin = _source.GetPosition();
            _originTile = _spawn.Tile;
            _sourceHeightSize = _sourceData.size.y;
            int resolution = _sourceData.heightmapResolution;
            _sourceHeights = _sourceData.GetHeights(0, 0, resolution, resolution);
            // Keep the authored scene in place and align its local vertical datum at the spawn.
            _datumOffset = GetSpawnPosition(Vector2.zero, 0f).y - _elevation.Sample(_spawn.East, _spawn.North);
            _baseY = Mathf.Min(_origin.y, _elevation.Min + _datumOffset) - RiverDepth;
            _heightRange = Mathf.Max(_origin.y + _sourceData.size.y, _elevation.Max + _datumOffset) - _baseY;
            _source.allowAutoConnect = false;
            if (roadsGeoJson && dirtRoadShader)
            {
                AddLandRoute("dirt road", 5f, new Color(.42f, .26f, .15f), new Color(.91f, .72f, .48f), 2f);
                AddLandRoute("service track", 3f, new Color(.43f, .42f, .36f), new Color(.82f, .78f, .65f), 1.7f);
                AddLandRoute("footpath", 1.2f, new Color(.56f, .39f, .24f), new Color(.94f, .66f, .40f), 1.3f);
            }

            if (waterwaysGeoJson || waterBodiesGeoJson)
            {
                var shader = Resources.Load<Shader>("ProceduralWater");
                if (shader) LoadWaterBodies(shader);
                if (shader && waterwaysGeoJson)
                {
                    AddWaterway(shader, "river", 30f, RiverDepth, new Color(.82f, 1f, 1.12f), 1f, 1f, new Color(.32f, .74f, 1f), 2.7f);
                    AddWaterway(shader, "stream", 8f, StreamDepth, new Color(.95f, 1.15f, 1.03f), 1.15f, 1.35f, new Color(.36f, .9f, .87f), 2.1f);
                    AddWaterway(shader, "seasonal stream", 3f, SeasonalStreamDepth, new Color(1.38f, 1.1f, .72f), .62f, .7f, new Color(.66f, .84f, .94f), 1.5f);
                }
                if (!shader)
                    Debug.LogWarning("The procedural water shader is missing.", this);
            }
            if (HasWater(_spawn.Tile)) CreateStartingTerrain();
            _tiles.Add(_spawn.Tile, _startingTerrain);
            AttachRoutes(_spawn.Tile, _startingTerrain);
            InstallSites(_spawn.Tile, _startingTerrain);
        }

        public Vector3 SpawnPosition => GetSpawnPosition(Vector2.zero, 1f);

        public Vector3 VehicleSpawnPosition
        {
            get
            {
                float heading = SpawnHeading * Mathf.Deg2Rad;
                return GetSpawnPosition(new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)) * 5f, .75f);
            }
        }

        private Vector3 GetSpawnPosition(Vector2 offset, float heightOffset)
        {
            Vector2Int key = _spawn.Tile;
            float x = Mathf.Clamp((float)(_spawn.East - key.x * TileSize) + offset.x, 0, TileSize);
            float z = Mathf.Clamp((float)(_spawn.North - key.y * TileSize) + offset.y, 0, TileSize);
            float height = _startingTerrain.GetPosition().y - _origin.y + _startingTerrain.terrainData.GetInterpolatedHeight(x / TileSize, z / TileSize);
            return _origin + TileOffset(key - _originTile) + new Vector3(x, height + heightOffset, z);
        }

        private static Vector3 TileOffset(Vector2Int tile) => new Vector3(tile.x * TileSize, 0, tile.y * TileSize);

        public Vector2Int? GetTile(Vector3 point) => HasSource ? WorldTile(point) : null;

        internal void DrawMapRoutes(Rect map, Rect world)
        {
            foreach (var route in _landRoutes)
                route.Surface.DrawMap(map, world, route.MapColor, route.MapWidth);
            foreach (var water in _waterways)
                water.Surface.DrawMap(map, world, water.MapColor, water.MapWidth);
        }

        internal void DrawMapWaterBodies(Rect map, Rect world)
        {
            _waterMapMarkers.Clear();
            foreach (var body in _waterBodies) body.DrawMap(map, world, _waterMapMarkers);
        }

        internal YanorraMap.Elevation MapElevation => _elevation;

        internal Vector2 ToMapPosition(Vector3 position) => new Vector2(
            _originTile.x * TileSize + (position.x - _origin.x),
            _originTile.y * TileSize + (position.z - _origin.z));

        internal void CollectWorldMapSites(List<Vector2> positions)
        {
            positions.Clear();
            if (!HasSource) return;
            foreach (var sites in _sites.Values)
            foreach (var site in sites)
                positions.Add(new Vector2((float)site.East, (float)site.North));
        }

        internal void CollectMapSitePositions(List<Vector3> positions)
        {
            positions.Clear();
            if (!HasSource) return;
            foreach (var sites in _sites.Values)
            foreach (var site in sites)
                positions.Add(new Vector3(
                    _origin.x + (float)(site.East - _originTile.x * TileSize),
                    0f,
                    _origin.z + (float)(site.North - _originTile.y * TileSize)));
        }

        internal bool IsSpawnedMapSite(Node node) =>
            HasSource && _sitesRoot && node.transform.IsChildOf(_sitesRoot.transform);

        private Vector2Int WorldTile(Vector3 point)
        {
            double east = _originTile.x * TileSize + (double)point.x - _origin.x;
            double north = _originTile.y * TileSize + (double)point.z - _origin.z;
            return new Vector2Int(YanorraMap.TileIndex(east), YanorraMap.TileIndex(north));
        }

        public bool ShiftOriginIfNeeded(Vector3 focus)
        {
            if (!HasSource || (Mathf.Abs(focus.x) <= OriginShiftDistance && Mathf.Abs(focus.z) <= OriginShiftDistance))
                return false;
            Vector2Int center = WorldTile(focus);
            Vector3 shift = TileOffset(center - _originTile);
            if (shift == Vector3.zero) return false;

            gameObject.scene.GetRootGameObjects(_roots);
            foreach (GameObject root in _roots)
                root.transform.position -= shift;

            _originTile = center;
            Physics.SyncTransforms();
            return true;
        }

        public void Pump(Vector3 focus)
        {
            if (!HasSource) return;
            Vector2Int center = WorldTile(focus);
            var min = Vector2Int.Max(Vector2Int.zero, center - Vector2Int.one * WindowRadius);
            var max = Vector2Int.Min(Vector2Int.one * (YanorraMap.TileCount - 1), center + Vector2Int.one * WindowRadius);

            _expired.Clear();
            foreach (var entry in _tiles)
                if (!Contains(entry.Key, min, max))
                    _expired.Add(entry.Key);
            foreach (var key in _expired)
                UnloadTile(key);
            if (_expired.Count > 0) RefreshNeighbors();

            if (_pendingHeights?.IsCompleted == true)
            {
                var completed = _pendingHeights;
                _pendingHeights = null;
                if (completed.IsFaulted)
                {
                    Debug.LogException(completed.Exception, this);
                    _retryAfter = Time.unscaledTime + 1f;
                }
                else if (!completed.IsCanceled && Contains(_pendingKey, min, max))
                {
                    Terrain terrain = CreateTile(_pendingKey, completed.Result);
                    _tiles.Add(_pendingKey, terrain);

                    AttachRoutes(_pendingKey, terrain);
                    InstallSites(_pendingKey, terrain);
                    RefreshNeighbors();
                }
            }

            Vector2Int? next = null;
            int distance = int.MaxValue;
            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                var key = new Vector2Int(x, z);
                if (_tiles.ContainsKey(key)) continue;
                int priority = (key - center).sqrMagnitude;
                if (priority < distance)
                {
                    next = key;
                    distance = priority;
                }
            }

            if (next == null)
            {
                InitialWindowReady = true;
                return;
            }
            if (_pendingHeights != null || Time.unscaledTime < _retryAfter) return;
            Vector2Int requested = next.Value;
            if (requested == _spawn.Tile)
            {
                RestoreSourceTile();
                return;
            }

            _pendingKey = requested;
            CancellationToken token = _shutdown.Token;
            _pendingHeights = Task.Run(() => SampleTile(requested, token), token);
        }

        public void RestoreSourceTile()
        {
            if (!HasSource || _tiles.ContainsKey(_spawn.Tile)) return;
            SetSourceTileLoaded(true);
            _tiles.Add(_spawn.Tile, _startingTerrain);
            RefreshNeighbors();
        }

        private static bool Contains(Vector2Int key, Vector2Int min, Vector2Int max) =>
            key.x >= min.x && key.x <= max.x && key.y >= min.y && key.y <= max.y;

        private void UnloadTile(Vector2Int key)
        {
            var terrain = _tiles[key];
            _tiles.Remove(key);
            if (terrain == _startingTerrain)
            {
                _startingTerrain.SetNeighbors(null, null, null, null);
                SetSourceTileLoaded(false);
            }
            else
            {
                DestroyTile(terrain);
            }
        }

        private void SetSourceTileLoaded(bool loaded)
        {
            // Keep the scene GameObject and any authored children alive.
            _startingTerrain.enabled = loaded;
            _startingCollider.enabled = loaded;
            if (_routeMeshes.TryGetValue(_startingTerrain, out var routes))
                foreach (var route in routes) route.gameObject.SetActive(loaded);
        }

        private void RefreshNeighbors()
        {
            foreach (var entry in _tiles)
            {
                var key = entry.Key;
                entry.Value.SetNeighbors(Find(key + Vector2Int.left), Find(key + Vector2Int.up),
                    Find(key + Vector2Int.right), Find(key + Vector2Int.down));
            }
            Physics.SyncTransforms();
        }

        private Terrain Find(Vector2Int key) => _tiles.TryGetValue(key, out var terrain) ? terrain : null;

        private float[,] SampleTile(Vector2Int key, CancellationToken token)
        {
            int resolution = _sourceHeights.GetLength(0);
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                token.ThrowIfCancellationRequested();
                double v = z / (resolution - 1.0);
                for (int x = 0; x < resolution; x++)
                {
                    double u = x / (resolution - 1.0);
                    float height = SampleGround((key.x + u) * TileSize, (key.y + v) * TileSize);
                    heights[z, x] = Mathf.Clamp01((height - _baseY) / _heightRange);
                }
            }
            return heights;
        }

        private float SampleGround(double east, double north)
        {
            float localX = (float)(east / TileSize - _spawn.Tile.x);
            float localZ = (float)(north / TileSize - _spawn.Tile.y);
            float edgeX = Mathf.Clamp01(localX), edgeZ = Mathf.Clamp01(localZ);
            float distance = Mathf.Max(Mathf.Abs(localX - edgeX), Mathf.Abs(localZ - edgeZ));
            if (distance == 0f) return SampleSourceEdge(edgeX, edgeZ);

            float height = _elevation.Sample(east, north) + _datumOffset;
            if (distance >= 1f) return height;
            return Mathf.Lerp(SampleSourceEdge(edgeX, edgeZ), height, Mathf.SmoothStep(0, 1, distance));
        }

        private float SampleSourceEdge(float u, float v)
        {
            int last = _sourceHeights.GetLength(0) - 1;
            float x = u * last, z = v * last;
            int x0 = (int)x, z0 = (int)z;
            int x1 = Mathf.Min(x0 + 1, last), z1 = Mathf.Min(z0 + 1, last);
            float a = Mathf.Lerp(_sourceHeights[z0, x0], _sourceHeights[z0, x1], x - x0);
            float b = Mathf.Lerp(_sourceHeights[z1, x0], _sourceHeights[z1, x1], x - x0);
            return _origin.y + Mathf.Lerp(a, b, z - z0) * _sourceHeightSize;
        }

        private Terrain CreateTile(Vector2Int key, float[,] heights)
        {
            var layers = _sourceData.terrainLayers;
            var data = new TerrainData
            {
                heightmapResolution = heights.GetLength(0),
                size = new Vector3(TileSize, _heightRange, TileSize),
                terrainLayers = layers
            };
            data.SetHeights(0, 0, heights);
            if (layers.Length > 0)
            {
                data.alphamapResolution = 16;
                var paint = new float[16, 16, layers.Length];
                for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                    paint[z, x, 0] = 1;
                data.SetAlphamaps(0, 0, paint);
            }

            GameObject tile = Terrain.CreateTerrainGameObject(data);
            SceneManager.MoveGameObjectToScene(tile, gameObject.scene);
            tile.name = $"Terrain ({key.x}, {key.y})";
            tile.layer = _source.gameObject.layer;
            Vector3 position = _origin + TileOffset(key - _originTile);
            position.y = _baseY;
            tile.transform.position = position;
            var terrain = tile.GetComponent<Terrain>();
            ConfigureTerrain(terrain);
            return terrain;
        }

        private bool HasWater(Vector2Int key)
        {
            foreach (var body in _waterBodies)
                if (body.OverlapsTile(key)) return true;
            foreach (var water in _waterways)
                if (water.Surface.HasChannel(key)) return true;
            return false;
        }

        private void CarveWater(Vector2Int key, Terrain terrain)
        {
            if (!HasWater(key)) return;
            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            var cuts = new float[resolution, resolution];
            foreach (var water in _waterways)
                water.Surface.AccumulateDepth(key, cuts);

            var heights = data.GetHeights(0, 0, resolution, resolution);
            foreach (var body in _waterBodies)
                body.AccumulateCut(key, data, terrain.GetPosition().y, heights, cuts);
            float range = data.size.y;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                heights[z, x] = Mathf.Clamp01(heights[z, x] - cuts[z, x] / range);
            data.SetHeights(0, 0, heights);
        }

        private void CreateStartingTerrain()
        {
            // A child terrain reserves space beneath zero while the scene's authored objects stay in place.
            var data = Instantiate(_sourceData);
            data.name = _sourceData.name + " (Runtime water)";
            data.size = new Vector3(TileSize, _sourceHeightSize + RiverDepth, TileSize);
            int resolution = _sourceData.heightmapResolution;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                heights[z, x] = (_sourceHeights[z, x] * _sourceHeightSize + RiverDepth) / data.size.y;
            data.SetHeights(0, 0, heights);

            GameObject tile = Terrain.CreateTerrainGameObject(data);
            SceneManager.MoveGameObjectToScene(tile, gameObject.scene);
            tile.name = "Starting terrain (Runtime water)";
            tile.layer = _source.gameObject.layer;
            tile.transform.SetParent(_source.transform, false);
            tile.transform.localPosition = Vector3.down * RiverDepth;
            _startingTerrain = tile.GetComponent<Terrain>();
            _startingCollider = tile.GetComponent<TerrainCollider>();
            ConfigureTerrain(_startingTerrain);
            _startingTerrain.drawTreesAndFoliage = _source.drawTreesAndFoliage;
            _source.enabled = false;
            _sourceCollider.enabled = false;
        }

        private void ConfigureTerrain(Terrain terrain)
        {
            terrain.allowAutoConnect = false;
            terrain.drawInstanced = _source.drawInstanced;
            terrain.heightmapPixelError = _source.heightmapPixelError;
            terrain.materialTemplate = _source.materialTemplate;
        }

        private void InstallSites(Vector2Int key, Terrain terrain)
        {
            if (!_sites.TryGetValue(key, out var points)) return;
            foreach (var point in points)
            {
                if (_dishes.ContainsKey(point.Id)) continue;
                if (!_sitesRoot)
                {
                    _sitesRoot = new GameObject("Authored map sites");
                    SceneManager.MoveGameObjectToScene(_sitesRoot, gameObject.scene);
                }

                float x = (float)(point.East - key.x * TileSize), z = (float)(point.North - key.y * TileSize);
                float height = terrain.terrainData.GetInterpolatedHeight(x / TileSize, z / TileSize);
                Vector3 position = terrain.GetPosition() + new Vector3(x, height, z);
                Node dish = Instantiate(satelliteDish, position, Quaternion.Euler(0, point.Heading, 0), _sitesRoot.transform);
                Console.WriteLine($"Spawned map site {point.Name} at {position} with heading {point.Heading}");
                dish.NodeName = point.Name;
                dish.name = point.Name;
                _dishes.Add(point.Id, dish);
            }
        }

        private void AddLandRoute(string type, float width, Color color, Color mapColor, float mapWidth)
        {
            try
            {
                var surface = new RouteSurface(roadsGeoJson, "route_type", type, width, 4f, .05f);
                var material = new Material(dirtRoadShader) { name = type, color = color };
                _landRoutes.Add((surface, material, type, mapColor, mapWidth));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not load {type} features: {error.Message}", this);
            }
        }

        private void LoadWaterBodies(Shader shader)
        {
            if (!waterBodiesGeoJson) return;
            try
            {
                foreach (var body in WaterBody.Read(waterBodiesGeoJson, shader, point => SampleGround(point.x, point.y)))
                    _waterBodies.Add(body);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not load water bodies: {error.Message}", this);
            }
        }

        private void AddWaterway(Shader shader, string type, float width, float depth, Color tint, float ripples, float speed, Color mapColor, float mapWidth)
        {
            try
            {
                var surface = new RouteSurface(waterwaysGeoJson, "waterway_type", type, width, 2f, .14f, depth);
                var material = new Material(shader) { name = type };
                material.SetColor("_WaterTint", tint);
                material.SetFloat("_RippleStrength", ripples);
                material.SetFloat("_RippleSpeed", speed);
                _waterways.Add((surface, material, type, mapColor, mapWidth));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not load {type} features: {error.Message}", this);
            }
        }

        private void AttachRoutes(Vector2Int key, Terrain terrain)
        {
            foreach (var body in _waterBodies)
                AttachSurface(body.BuildTile(key, terrain.GetPosition().y), body.Material, body.Name, terrain);
            foreach (var water in _waterways)
                AttachSurface(water.Surface.BuildTile(key, terrain.terrainData), water.Material, water.Name, terrain);
            // Sample the water surface on the original ground, then lower the bed beneath it.
            CarveWater(key, terrain);
            if (terrain == _startingTerrain && terrain != _source && terrain.terrainData.treeInstanceCount > 0)
                terrain.terrainData.SetTreeInstances(terrain.terrainData.treeInstances, true);
            foreach (var route in _landRoutes)
                AttachSurface(route.Surface.BuildTile(key, terrain.terrainData), route.Material, route.Name, terrain);
        }

        private void AttachSurface(Mesh mesh, Material material, string name, Terrain terrain)
        {
            if (!mesh) return;
            var surface = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(terrain.transform, false);
            surface.layer = terrain.gameObject.layer;
            var filter = surface.GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!_routeMeshes.TryGetValue(terrain, out var routes))
                _routeMeshes.Add(terrain, routes = new List<MeshFilter>());
            routes.Add(filter);
        }

        private void RemoveRoutes(Terrain terrain)
        {
            if (!_routeMeshes.Remove(terrain, out var routes)) return;
            foreach (var route in routes)
            {
                if (!route) continue;
                Destroy(route.sharedMesh);
                Destroy(route.gameObject);
            }
        }

        private void DestroyTile(Terrain terrain)
        {
            if (!terrain) return;
            RemoveRoutes(terrain);
            terrain.gameObject.SetActive(false);
            Destroy(terrain.terrainData);
            Destroy(terrain.gameObject);
        }

        private void OnDestroy()
        {
            _shutdown.Cancel();
            _shutdown.Dispose();
            if (_startingTerrain) _startingTerrain.SetNeighbors(null, null, null, null);
            foreach (var terrain in _tiles.Values)
                if (terrain != _startingTerrain) DestroyTile(terrain);
            if (_startingTerrain && _startingTerrain != _source) DestroyTile(_startingTerrain);
            else if (_source) RemoveRoutes(_source);
            if (_source && _sourceCollider && gameObject.scene.isLoaded)
            {
                _source.enabled = true;
                _sourceCollider.enabled = true;
            }
            foreach (var route in _landRoutes) Destroy(route.Material);
            foreach (var water in _waterways) Destroy(water.Material);
            foreach (var body in _waterBodies) body.Dispose();
            if (_sitesRoot) Destroy(_sitesRoot);
        }
    }
}
