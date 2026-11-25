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
        private readonly List<Vector2> _mapSites = new();
        private Node[] _sceneNodes;
        private static readonly Rect FullWorld = new(0, 0, YanorraMap.WorldSize, YanorraMap.WorldSize);
        private Texture2D _terrain, _zoomTerrain;
        private Rect _zoomBounds;
        private float _span = YanorraMap.WorldSize;
        private GUIStyle _label, _title, _centered;

        public bool IsOpen { get; private set; }

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

        public void Draw(TerrainStream stream, Vector3 player)
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

            Vector2 playerMarker = Project(playerPosition, map, world);
            Fill(new Rect(playerMarker.x - 7, playerMarker.y - 7, 14, 14), Panel, 7);
            Fill(new Rect(playerMarker.x - 5, playerMarker.y - 5, 10, 10), PlayerColor, 5);

            stream.DrawMapWaterBodies(map, world);

            DrawNode(new Vector2(map.x + 6, map.yMax + 23));
            GUI.Label(new Rect(map.x + 16, map.yMax + 10, 144, 26), "Horizon Chain node", _label);
            if (size >= 300)
            {
                Fill(new Rect(map.x + 156, map.yMax + 19, 8, 8), PlayerColor, 4);
                GUI.Label(new Rect(map.x + 168, map.yMax + 10, 75, 26), "You", _label);
            }
            if (size >= 420)
                GUI.Label(new Rect(map.xMax - 175, map.yMax + 10, 95, 26), "= / - zoom", _label);
            GUI.Label(new Rect(map.xMax - 78, map.yMax + 10, 78, 26), "M / Esc close", _label);
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

        private static Texture2D CreateTerrainTexture(string name) =>
            new(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

        private static void UpdateTerrain(Texture2D texture, YanorraMap.Elevation elevation, Rect world)
        {
            var heights = new float[Resolution * Resolution];
            float step = world.width / Resolution;
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
                heights[z * Resolution + x] = elevation.Sample(world.xMin + (x + .5) * step, world.yMin + (z + .5) * step);

            var pixels = new Color32[heights.Length];
            var lowland = new Color(.21f, .38f, .31f);
            var upland = new Color(.60f, .59f, .37f);
            var peaks = new Color(.87f, .80f, .66f);
            var light = new Vector3(-.55f, .73f, .40f).normalized;
            var range = Mathf.Max(1, elevation.Max - elevation.Min);
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
            {
                int i = z * Resolution + x;
                float height = Mathf.Clamp01((heights[i] - elevation.Min) / range);
                float east = heights[z * Resolution + Mathf.Min(x + 1, Resolution - 1)] -
                    heights[z * Resolution + Mathf.Max(x - 1, 0)];
                float north = heights[Mathf.Min(z + 1, Resolution - 1) * Resolution + x] -
                    heights[Mathf.Max(z - 1, 0) * Resolution + x];
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
        }
    }
}
