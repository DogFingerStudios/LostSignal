using System.Collections.Generic;
using UnityEngine;

namespace KeepDriving
{
    internal sealed class WorldMap
    {
        private const int Resolution = 512;
        private const float MinSpan = 10f;
        private static readonly Color Panel = new(.06f, .10f, .10f);
        private static readonly Color Border = new(.35f, .46f, .43f);
        private static readonly Color NodeColor = new(1f, .72f, .29f);
        private static readonly Color PlayerColor = new(.42f, 1f, .92f);
        private static readonly Color TileOutline = new(1f, .91f, .2f);
        private static readonly Color TileGrid = new(.15f, .17f, .18f, .55f);
        private readonly HashSet<Vector2Int> _loadedTiles = new();
        private readonly List<Vector2> _mapSites = new();
        private Node[] _sceneNodes;
        private static readonly Rect FullWorld = new(0, 0, YanorraMap.WorldSize, YanorraMap.WorldSize);
        private Texture2D _terrain, _zoomTerrain, _playerArrow;
        private Rect _zoomBounds;
        private float _span = YanorraMap.WorldSize;
        private GUIStyle _label, _title, _centered;

        public bool IsOpen { get; private set; }
        public float WidthKm => _span / 1000f;

        public void Open(TerrainStream stream)
        {
            stream.CollectWorldMapSites(_mapSites);
            _sceneNodes = Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
            if (!_terrain)
            {
                _terrain = CreateTerrainTexture("Yanorra world map");
                UpdateTerrain(_terrain, stream.MapElevation, FullWorld);
            }
            IsOpen = true;
        }

        public void Close() => IsOpen = false;

        public void Zoom(int direction) =>
            _span = Mathf.Clamp(direction > 0 ? _span * .5f : _span * 2f, MinSpan, YanorraMap.WorldSize);

        private Rect GetView(Vector2 player)
        {
            if (_span == YanorraMap.WorldSize) return FullWorld;
            float maxOrigin = YanorraMap.WorldSize - _span;
            return new Rect(Mathf.Clamp(player.x - _span * .5f, 0f, maxOrigin),
                Mathf.Clamp(player.y - _span * .5f, 0f, maxOrigin), _span, _span);
        }

        private Texture2D GetTerrain(YanorraMap.Elevation elevation, Rect world)
        {
            if (world == FullWorld) return _terrain;
            if (!_zoomTerrain) _zoomTerrain = CreateTerrainTexture("Yanorra zoomed terrain");
            if (_zoomBounds != world)
            {
                UpdateTerrain(_zoomTerrain, elevation, world);
                _zoomBounds = world;
            }
            return _zoomTerrain;
        }

        public void Draw(TerrainStream stream, Vector3 player, bool showSpawnedTileOutline, bool showSpawnedTileGrid, float heading)
        {
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                _label.normal.textColor = new Color(.87f, .92f, .89f);
                _title = new GUIStyle(_label) { fontSize = 21, fontStyle = FontStyle.Bold };
                _centered = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            }

            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .87f));
            float size = Mathf.Min(Screen.width - 72f, Screen.height - 136f);
            if (size <= 0f) return;
            var map = new Rect((Screen.width - size) * .5f, (Screen.height - size) * .5f + 6f, size, size);
            Fill(new Rect(map.x - 16, map.y - 42, size + 32, size + 88), Panel);
            Fill(new Rect(map.x - 1, map.y - 1, size + 2, size + 2), Border);
            Vector2 playerPosition = stream.ToMapPosition(player);
            Rect world = GetView(playerPosition);
            GUI.DrawTexture(map, GetTerrain(stream.MapElevation, world));
            stream.DrawMapRoutes(map, world);
            GUI.Label(new Rect(map.x, map.y - 40, size, 35), "YANORRA", _title);
            string span = _span >= 1000f ? $"{_span / 1000f:0.##} km" : $"{_span:0.#} m";
            GUI.Label(new Rect(map.xMax - 104, map.y - 36, 104, 24), span + " wide", _label);
            GUI.Label(new Rect(map.xMax - 32, map.y + 8, 24, 20), "N ↑", _centered);

            foreach (var position in _mapSites)
                if (Contains(world, position)) DrawNode(Project(position, map, world));
            foreach (var node in _sceneNodes)
            {
                if (!node || node.gameObject.scene != stream.gameObject.scene || stream.IsSpawnedMapSite(node)) continue;
                Vector2 position = stream.ToMapPosition(node.transform.position);
                if (Contains(world, position)) DrawNode(Project(position, map, world));
            }

            stream.DrawMapWaterBodies(map, world);
            if (showSpawnedTileOutline || showSpawnedTileGrid)
            {
                HighlightSpawnedTiles(stream, map, world, showSpawnedTileOutline, showSpawnedTileGrid);
            }

            if (!_playerArrow)
            {
                _playerArrow = Minimap.CreateIcon("World map player arrow", true);
            }
            Minimap.DrawIcon(_playerArrow, Project(playerPosition, map, world), 18f, heading, PlayerColor);

            DrawNode(new Vector2(map.x + 6, map.yMax + 23));
            GUI.Label(new Rect(map.x + 16, map.yMax + 10, 144, 26), "Horizon Chain node", _label);
            if (size >= 300)
            {
                Minimap.DrawIcon(_playerArrow, new Vector2(map.x + 160, map.yMax + 23), 12f, 0f, PlayerColor);
                GUI.Label(new Rect(map.x + 168, map.yMax + 10, 75, 26), "You", _label);
            }
            if (size >= 420)
                GUI.Label(new Rect(map.xMax - 175, map.yMax + 10, 95, 26), "= / - zoom", _label);
            GUI.Label(new Rect(map.xMax - 78, map.yMax + 10, 78, 26), "M / Esc close", _label);
        }

        private void HighlightSpawnedTiles(TerrainStream stream, Rect map, Rect world, bool showOutline, bool showGrid)
        {
            stream.CollectLoadedTiles(_loadedTiles);
            const int size = YanorraMap.TileSize;
            foreach (var tile in _loadedTiles)
            {
                var sw = new Vector2(tile.x * size, tile.y * size);
                var ne = sw + Vector2.one * size;
                var nw = new Vector2(sw.x, ne.y);
                var se = new Vector2(ne.x, sw.y);
                bool eastLoaded = _loadedTiles.Contains(tile + Vector2Int.right);
                bool northLoaded = _loadedTiles.Contains(tile + Vector2Int.up);
                if (showGrid)
                {
                    if (eastLoaded) DrawTileEdge(se, ne, map, world, TileGrid, 1f);
                    if (northLoaded) DrawTileEdge(nw, ne, map, world, TileGrid, 1f);
                }
                if (showOutline)
                {
                    if (!_loadedTiles.Contains(tile + Vector2Int.left)) DrawTileEdge(sw, nw, map, world, TileOutline, 2f);
                    if (!eastLoaded) DrawTileEdge(se, ne, map, world, TileOutline, 2f);
                    if (!_loadedTiles.Contains(tile + Vector2Int.down)) DrawTileEdge(sw, se, map, world, TileOutline, 2f);
                    if (!northLoaded) DrawTileEdge(nw, ne, map, world, TileOutline, 2f);
                }
            }
        }

        private static void DrawTileEdge(Vector2 start, Vector2 end, Rect map, Rect world, Color color, float width)
        {
            if (end.x < world.xMin || start.x > world.xMax ||
                end.y < world.yMin || start.y > world.yMax) return;

            Vector2 a = Project(start, map, world);
            Vector2 b = Project(end, map, world);
            if (start.x == end.x && a.y > b.y)
            {
                float x = Mathf.Clamp(a.x - width * .5f, map.xMin, map.xMax - width);
                Fill(new Rect(x, b.y, width, a.y - b.y), color);
            }
            else if (start.y == end.y && b.x > a.x)
            {
                float y = Mathf.Clamp(a.y - width * .5f, map.yMin, map.yMax - width);
                Fill(new Rect(a.x, y, b.x - a.x, width), color);
            }
        }

        private static bool Contains(Rect world, Vector2 point) =>
            point.x >= world.xMin && point.x <= world.xMax && point.y >= world.yMin && point.y <= world.yMax;

        private static Vector2 Project(Vector2 position, Rect map, Rect world)
        {
            float east = Mathf.Clamp01((position.x - world.xMin) / world.width);
            float north = Mathf.Clamp01((position.y - world.yMin) / world.height);
            return new Vector2(map.x + east * map.width, map.yMax - north * map.height);
        }

        private static void DrawNode(Vector2 position)
        {
            Matrix4x4 original = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, position);
            Fill(new Rect(position.x - 4.5f, position.y - 4.5f, 9, 9), Panel);
            Fill(new Rect(position.x - 3f, position.y - 3f, 6, 6), NodeColor);
            GUI.matrix = original;
        }

        private static void Fill(Rect rect, Color color, float radius = 0f) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);

        internal static Texture2D CreateTerrainTexture(string name, int resolution = Resolution) =>
            new(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

        internal static void UpdateTerrain(Texture2D texture, YanorraMap.Elevation elevation, Rect world)
        {
            int resolution = texture.width;
            var heights = new float[resolution * resolution];
            float step = world.width / resolution;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                heights[z * resolution + x] = elevation.Sample(world.xMin + (x + .5) * step, world.yMin + (z + .5) * step);

            var pixels = new Color32[heights.Length];
            var lowland = new Color(.21f, .38f, .31f);
            var upland = new Color(.60f, .59f, .37f);
            var peaks = new Color(.87f, .80f, .66f);
            var light = new Vector3(-.55f, .73f, .40f).normalized;
            var range = Mathf.Max(1, elevation.Max - elevation.Min);
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int i = z * resolution + x;
                double mapX = world.xMin + (x + .5) * step;
                double mapZ = world.yMin + (z + .5) * step;
                if (mapX < 0 || mapX > YanorraMap.WorldSize || mapZ < 0 || mapZ > YanorraMap.WorldSize)
                {
                    pixels[i] = new Color32(20, 33, 31, 255);
                    continue;
                }
                float height = Mathf.Clamp01((heights[i] - elevation.Min) / range);
                float east = heights[z * resolution + Mathf.Min(x + 1, resolution - 1)] -
                    heights[z * resolution + Mathf.Max(x - 1, 0)];
                float north = heights[Mathf.Min(z + 1, resolution - 1) * resolution + x] -
                    heights[Mathf.Max(z - 1, 0) * resolution + x];
                var normal = new Vector3(-east * 18f, 2f * step, -north * 18f).normalized;
                float shade = .68f + .39f * Mathf.Clamp01(Vector3.Dot(normal, light));
                Color color = height < .55f ? Color.Lerp(lowland, upland, height / .55f)
                    : Color.Lerp(upland, peaks, (height - .55f) / .45f);
                pixels[i] = new Color(color.r * shade, color.g * shade, color.b * shade);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        public void Dispose()
        {
            if (_terrain) Object.Destroy(_terrain);
            if (_zoomTerrain) Object.Destroy(_zoomTerrain);
            if (_playerArrow) Object.Destroy(_playerArrow);
        }
    }
}
