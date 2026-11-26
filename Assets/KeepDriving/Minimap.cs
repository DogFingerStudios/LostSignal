using System.Collections.Generic;
using UnityEngine;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TerrainStream))]
    public sealed class Minimap : MonoBehaviour
    {
        [SerializeField, Min(1f), Tooltip("Initial minimap width in meters.")]
        private float startingWidth = 300f;
        [SerializeField, Min(1f), Tooltip("Minimum minimap width in meters.")]
        private float minimumWidth = 10f;
        [SerializeField, Min(1f), Tooltip("Maximum minimap width in meters.")]
        private float maximumWidth = 50000f;

        [SerializeField] private List<MapMarker> trackedMarkers = new();

        private const int Resolution = 128;
        private const float PanStep = 16f;
        private TerrainStream terrainStream;

        static readonly Color Frame = new(.05f, .09f, .085f, .96f);
        static readonly Color Rule = new(.32f, .43f, .38f);
        static readonly Color Text = new(.75f, .83f, .79f);
        static readonly Color Amber = new(1f, .71f, .31f);
        static readonly Color Mint = new(.64f, 1f, .8f);
        static readonly Color Unmapped = new(.08f, .13f, .12f);

        Texture2D relief, arrow, diamond;
        GUIStyle left, right;
        readonly List<Vector3> mapSites = new();
        Node[] nodes = System.Array.Empty<Node>();
        Vector2 center;
        float range;
        float nextNodeScan;
        Rect reliefBounds;
        YanorraMap.Elevation reliefElevation;

        private void Awake()
        {
            terrainStream = GetComponent<TerrainStream>();
            range = ClampWidth(startingWidth);
        }

        private float ClampWidth(float width)
        {
            float min = Mathf.Max(1f, minimumWidth);
            return Mathf.Clamp(width, min, Mathf.Max(min, maximumWidth));
        }

        public void Zoom(int direction)
        {
            if (!isActiveAndEnabled || direction == 0) return;
            float nextRange = ClampWidth(direction > 0 ? range * .5f : range * 2f);
            if (nextRange == range) return;
            range = nextRange;
        }

        public void Draw(Vector3 position, float heading)
        {
            if (!isActiveAndEnabled) return;
            if (!relief) Initialize();
            if (Time.unscaledTime >= nextNodeScan)
            {
                nodes = Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
                nextNodeScan = Time.unscaledTime + 1f;
            }

            float panStep = Mathf.Min(PanStep, range / 20f);
            center = new Vector2(Mathf.Round(position.x / panStep), Mathf.Round(position.z / panStep)) * panStep;
            bool hasMap = terrainStream && terrainStream.HasSource;
            Rect world = default;
            if (hasMap)
            {
                Vector2 mapCenter = terrainStream.ToMapPosition(new Vector3(center.x, 0, center.y));
                world = new Rect(mapCenter.x - range * .5f, mapCenter.y - range * .5f, range, range);
                var elevation = terrainStream.MapElevation;
                if (world != reliefBounds || elevation != reliefElevation)
                {
                    WorldMap.UpdateTerrain(relief, elevation, world, terrainStream.MapWoodland);
                    reliefBounds = world;
                    reliefElevation = elevation;
                }
            }

            float size = Mathf.Clamp(Screen.height * .29f, 120, 220);
            var map = new Rect(Screen.width - size - 24, Screen.height - size - 42, size, size);
            var frame = Rect.MinMaxRect(map.xMin - 8, map.yMin - 26, map.xMax + 8, map.yMax + 28);
            Fill(frame, Rule);
            Fill(new Rect(frame.x + 1, frame.y + 1, frame.width - 2, frame.height - 2), Frame);
            GUI.Label(new Rect(map.x, map.y - 23, size, 19), "LOCAL TERRAIN", left);
            GUI.Label(new Rect(map.x, map.y - 23, size, 19), "N ↑", right);
            if (hasMap) GUI.DrawTexture(map, relief);
            else Fill(map, Unmapped);
            DrawGrid(map);
            if (hasMap)
            {
                terrainStream.DrawMapWaterBodies(map, world);
                terrainStream.DrawMapRoutes(map, world);
            }
            DrawNodes(map);
            foreach (var marker in trackedMarkers)
            {
                if (!marker || !marker.CanDraw) continue;
                Vector2 delta = Project(marker.Position, map) - map.center;
                float edge = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                float limit = Mathf.Max(0, (map.width - marker.Size) * .5f - 2f);
                if (edge > limit) delta *= limit / edge;
                marker.Draw(map.center + delta);
            }
            DrawIcon(arrow, Project(position, map), 18, heading, Mint);

            DrawIcon(diamond, new Vector2(map.x + 5, map.yMax + 13), 9, 0, Amber);
            GUI.Label(new Rect(map.x + 14, map.yMax + 4, size - 14, 18), "SATELLITE NODE", left);
            GUI.Label(new Rect(map.x, map.yMax + 4, size, 18), (range >= 1000f ? $"{range / 1000f:0.#} km" : $"{range:0.#} m"), right);
        }

        void DrawGrid(Rect map)
        {
            var grid = new Color(.66f, .8f, .72f, .17f);
            for (int i = 1; i < 4; i++)
            {
                float offset = i * map.width / 4;
                Fill(new Rect(map.x + offset, map.y, 1, map.height), grid);
                Fill(new Rect(map.x, map.y + offset, map.width, 1), grid);
            }
        }

        void DrawNodes(Rect map)
        {
            if (terrainStream)
            {
                terrainStream.CollectMapSitePositions(mapSites);
                foreach (var position in mapSites)
                    DrawNode(position, map);
            }

            foreach (Node node in nodes)
            {
                if (!node || !node.gameObject.activeInHierarchy || node.gameObject.scene != gameObject.scene) continue;
                if (terrainStream && terrainStream.IsSpawnedMapSite(node)) continue;
                DrawNode(node.transform.position, map);
            }
        }

        void DrawNode(Vector3 position, Rect map)
        {
            Vector2 delta = Project(position, map) - map.center;
            float limit = map.width * .5f - 8;
            float edge = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            if (edge > limit)
            {
                float angle = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg;
                DrawIcon(arrow, map.center + delta * (limit / edge), 12, angle, Amber);
            }
            else
            {
                DrawIcon(diamond, map.center + delta, 14, 0, Amber);
            }
        }

        Vector2 Project(Vector3 point, Rect map)
        {
            float scale = map.width / range;
            return map.center + new Vector2(point.x - center.x, center.y - point.z) * scale;
        }

        void Initialize()
        {
            relief = WorldMap.CreateTerrainTexture("Minimap relief", Resolution);
            arrow = CreateIcon("Minimap arrow", true);
            diamond = CreateIcon("Minimap node", false);
            left = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            left.normal.textColor = Text;
            right = new GUIStyle(left) { alignment = TextAnchor.MiddleRight };
        }

        internal static Texture2D CreateIcon(string name, bool isArrow)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float horizontal = Mathf.Abs(x - 15.5f);
                float distance = isArrow
                    ? Mathf.Max(horizontal - (29 - y) * .53f, 5 + (12 - horizontal) * .54f - y)
                    : horizontal + Mathf.Abs(y - 15.5f) - 13;
                texture.SetPixel(x, y, distance <= -3 ? Color.white
                    : distance <= 0 ? Color.black : Color.clear);
            }
            texture.Apply(false, true);
            return texture;
        }

        internal static void DrawIcon(Texture2D texture, Vector2 point, float size, float angle, Color tint)
        {
            var matrix = GUI.matrix;
            var color = GUI.color;
            GUIUtility.RotateAroundPivot(angle, point);
            GUI.color = tint;
            GUI.DrawTexture(new Rect(point.x - size / 2, point.y - size / 2, size, size), texture);
            GUI.matrix = matrix;
            GUI.color = color;
        }

        static void Fill(Rect rect, Color tint)
        {
            var previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (relief) Object.Destroy(relief);
            if (arrow) Object.Destroy(arrow);
            if (diamond) Object.Destroy(diamond);
        }
    }
}
