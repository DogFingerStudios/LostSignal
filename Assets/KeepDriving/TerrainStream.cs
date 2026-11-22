using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeepDriving
{
    internal sealed class TerrainStream : System.IDisposable
    {
        private const int WindowRadius = 2;
        private const float OriginShiftDistance = 1600f;

        private readonly Scene _scene;
        private readonly Terrain _source;
        private readonly TerrainData _sourceData;
        private readonly Vector3 _origin;
        private readonly Vector3 _size;
        private readonly Dictionary<Vector2Int, Terrain> _tiles = new Dictionary<Vector2Int, Terrain>();
        private readonly List<Vector2Int> _expired = new List<Vector2Int>();
        private readonly List<GameObject> _roots = new List<GameObject>();
        private Vector2Int _originTile;
        private Vector2Int? _windowCenter;

        public TerrainStream(Scene scene, Vector3 start)
        {
            this._scene = scene;
            foreach (Terrain candidate in Terrain.activeTerrains)
            {
                if (candidate.gameObject.scene != scene || !candidate.terrainData)
                {
                    continue;
                }

                Vector3 offset = start - candidate.GetPosition();
                Vector3 bounds = candidate.terrainData.size;
                if (offset.x < 0 || offset.z < 0 || offset.x >= bounds.x || offset.z >= bounds.z)
                {
                    continue;
                }

                _source = candidate;
                _sourceData = _source.terrainData;
                _origin = _source.GetPosition();
                _size = _sourceData.size;
                _tiles.Add(Vector2Int.zero, _source);
                break;
            }
        }

        public bool HasSource => _source != null;

        public Vector3 SpawnPosition => _origin + new Vector3(_size.x / 2,
            _sourceData.GetInterpolatedHeight(.5f, .5f) + 1f, _size.z / 2) - TileOffset(_originTile);

        private Vector3 TileOffset(Vector2Int tile) => new Vector3(tile.x * _size.x, 0, tile.y * _size.z);

        public Vector2Int? GetTile(Vector3 point) => _source ? WorldTile(point) : null;

        private Vector2Int WorldTile(Vector3 point)
        {
            return _originTile + new Vector2Int(Mathf.FloorToInt((point.x - _origin.x) / _size.x),
                Mathf.FloorToInt((point.z - _origin.z) / _size.z));
        }

        // Returns true when the gameplay _scene was shifted to keep physics near the _origin.
        public bool Update(Vector3 focus)
        {
            if (!_source)
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
                    _scene.GetRootGameObjects(_roots);
                    foreach (GameObject root in _roots)
                    {
                        root.transform.position -= shift;
                    }

                    _originTile = center;
                }
            }

            if (_windowCenter != center)
            {
                LoadWindow(center);
                _windowCenter = center;
            }
            else if (shifted)
            {
                Physics.SyncTransforms();
            }
            return shifted;
        }

        private void LoadWindow(Vector2Int center)
        {
            for (int z = center.y - WindowRadius; z <= center.y + WindowRadius; z++)
                for (int x = center.x - WindowRadius; x <= center.x + WindowRadius; x++)
                {
                    var key = new Vector2Int(x, z);
                    if (_tiles.ContainsKey(key)) continue;
                    if (key == Vector2Int.zero)
                    {
                        _source.gameObject.SetActive(true);
                        _tiles.Add(key, _source);
                    }
                    else
                    {
                        _tiles.Add(key, CreateTile(key));
                    }
                }

            _expired.Clear();
            foreach (var entry in _tiles)
                if (Mathf.Abs(entry.Key.x - center.x) > WindowRadius
                    || Mathf.Abs(entry.Key.y - center.y) > WindowRadius)
                {
                    _expired.Add(entry.Key);
                }

            foreach (Vector2Int key in _expired)
            {
                // Keep the authored asset and scene object available for the return trip.
                if (key == Vector2Int.zero)
                {
                    _source.SetNeighbors(null, null, null, null);
                    _source.gameObject.SetActive(false);
                }
                else
                {
                    DestroyTile(_tiles[key]);
                }
                _tiles.Remove(key);
            }

            foreach (var entry in _tiles)
            {
                Vector2Int key = entry.Key;
                entry.Value.SetNeighbors(Find(key + Vector2Int.left), Find(key + Vector2Int.up),
                    Find(key + Vector2Int.right), Find(key + Vector2Int.down));
            }
            Physics.SyncTransforms();
        }

        private Terrain Find(Vector2Int key) => _tiles.TryGetValue(key, out Terrain terrain) ? terrain : null;

        private Terrain CreateTile(Vector2Int key)
        {
            int resolution = _sourceData.heightmapResolution;
            var data = new TerrainData
            {
                heightmapResolution = resolution,
                size = _size,
                terrainLayers = _sourceData.terrainLayers
            };

            var heights = new float[resolution, resolution];
            bool nearSource = Mathf.Abs(key.x) <= 1 && Mathf.Abs(key.y) <= 1;
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = key.x + x / (resolution - 1f);
                    float worldZ = key.y + z / (resolution - 1f);
                    float edgeX = Mathf.Clamp01(worldX);
                    float edgeZ = Mathf.Clamp01(worldZ);
                    float distance = Mathf.Max(Mathf.Abs(worldX - edgeX), Mathf.Abs(worldZ - edgeZ));
                    float blend = Mathf.SmoothStep(0f, 1f, distance * 2f);
                    float hills = Mathf.PerlinNoise(worldX * 3.2f + 37.5f, worldZ * 3.2f + 71.5f)
                        * Mathf.Min(18f / _size.y, 1f);
                    float edgeHeight = nearSource && blend < 1f
                        ? _sourceData.GetInterpolatedHeight(edgeX, edgeZ) / _size.y : 0f;
                    heights[z, x] = Mathf.Lerp(edgeHeight, hills, blend);
                }
            data.SetHeights(0, 0, heights);

            if (data.terrainLayers.Length > 0)
            {
                data.alphamapResolution = 16;
                var paint = new float[16, 16, data.terrainLayers.Length];
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
            SceneManager.MoveGameObjectToScene(tile, _scene);
            tile.name = $"Terrain ({key.x}, {key.y})";
            tile.layer = _source.gameObject.layer;
            tile.transform.position = _origin + TileOffset(key - _originTile);
            var terrain = tile.GetComponent<Terrain>();
            terrain.allowAutoConnect = false;
            terrain.drawInstanced = _source.drawInstanced;
            terrain.heightmapPixelError = _source.heightmapPixelError;
            terrain.materialTemplate = _source.materialTemplate;
            return terrain;
        }

        private static void DestroyTile(Terrain tile)
        {
            if (!tile)
            {
                return;
            }

            tile.gameObject.SetActive(false);
            Object.Destroy(tile.terrainData);
            Object.Destroy(tile.gameObject);
        }

        public void Dispose()
        {
            if (_source)
            {
                _source.SetNeighbors(null, null, null, null);
                if (_scene.isLoaded) _source.gameObject.SetActive(true);
            }
            foreach (var entry in _tiles)
            {
                if (entry.Key != Vector2Int.zero)
                {
                    DestroyTile(entry.Value);
                }
            }
            _tiles.Clear();
        }
    }
}
