using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeepDriving
{
    internal sealed class Minimap
    {
        const int Resolution = 128;
        const float Range = 300f;
        const float PanStep = 16f;
        static readonly Color Frame = new(.05f, .09f, .085f, .96f);
        static readonly Color Rule = new(.32f, .43f, .38f);
        static readonly Color Text = new(.75f, .83f, .79f);
        static readonly Color Amber = new(1f, .71f, .31f);
        static readonly Color Mint = new(.64f, 1f, .8f);
        static readonly Color Lowland = new(.13f, .22f, .19f);
        static readonly Color Highland = new(.43f, .52f, .40f);
        static readonly Color Unmapped = new(.08f, .13f, .12f);

        readonly Color32[] pixels = new Color32[Resolution * Resolution];
        Texture2D relief, arrow, diamond;
        GUIStyle left, right;
        readonly List<Vector3> mapSites = new();
        Node[] nodes = System.Array.Empty<Node>();
        Vector2 center;
        float nextNodeScan, nextReliefRefresh;

        public void Draw(Scene scene, Vector3 position, float heading, TerrainStream terrainStream)
        {
            if (!relief) Initialize();
            if (Time.unscaledTime >= nextNodeScan)
            {
                nodes = Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
                nextNodeScan = Time.unscaledTime + 1f;
            }

            var newCenter = new Vector2(Mathf.Round(position.x / PanStep), Mathf.Round(position.z / PanStep)) * PanStep;
            if (newCenter != center || Time.unscaledTime >= nextReliefRefresh)
            {
                center = newCenter;
                UpdateRelief(scene);
                nextReliefRefresh = Time.unscaledTime + 1f;
            }

            float size = Mathf.Clamp(Screen.height * .29f, 120, 220);
            var map = new Rect(Screen.width - size - 24, Screen.height - size - 42, size, size);
            var frame = Rect.MinMaxRect(map.xMin - 8, map.yMin - 26, map.xMax + 8, map.yMax + 28);
            Fill(frame, Rule);
            Fill(new Rect(frame.x + 1, frame.y + 1, frame.width - 2, frame.height - 2), Frame);
            GUI.Label(new Rect(map.x, map.y - 23, size, 19), "LOCAL TERRAIN", left);
            GUI.Label(new Rect(map.x, map.y - 23, size, 19), "N ↑", right);
            GUI.DrawTexture(map, relief);
            DrawGrid(map);
            if (terrainStream && terrainStream.HasSource)
            {
                Vector2 mapCenter = terrainStream.ToMapPosition(new Vector3(center.x, 0, center.y));
                terrainStream.DrawMapRoads(map, new Rect(mapCenter.x - Range * .5f, mapCenter.y - Range * .5f, Range, Range));
            }
            DrawNodes(scene, map, terrainStream);
            DrawIcon(arrow, Project(position, map), 18, heading, Mint);

            DrawIcon(diamond, new Vector2(map.x + 5, map.yMax + 13), 9, 0, Amber);
            GUI.Label(new Rect(map.x + 14, map.yMax + 4, size - 14, 18), "SATELLITE NODE", left);
            GUI.Label(new Rect(map.x, map.yMax + 4, size, 18), "300 m", right);
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

        void DrawNodes(Scene scene, Rect map, TerrainStream terrainStream)
        {
            if (terrainStream)
            {
                terrainStream.CollectMapSitePositions(mapSites);
                foreach (var position in mapSites)
                    DrawNode(position, map);
            }

            foreach (Node node in nodes)
            {
                if (!node || !node.gameObject.activeInHierarchy || node.gameObject.scene != scene) continue;
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
            float scale = map.width / Range;
            return map.center + new Vector2(point.x - center.x, center.y - point.z) * scale;
        }

        void Initialize()
        {
            relief = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "Minimap relief", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            arrow = CreateIcon("Minimap arrow", true);
            diamond = CreateIcon("Minimap node", false);
            left = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            left.normal.textColor = Text;
            right = new GUIStyle(left) { alignment = TextAnchor.MiddleRight };
        }

        void UpdateRelief(Scene scene)
        {
            System.Array.Fill(pixels, (Color32)Unmapped);
            float west = center.x - Range * .5f;
            float south = center.y - Range * .5f;
            float step = Range / Resolution;
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain.gameObject.scene != scene || !terrain.terrainData) continue;
                var data = terrain.terrainData;
                Vector3 origin = terrain.GetPosition();
                Vector3 size = data.size;
                // Only sample the rectangle occupied by each loaded terrain tile.
                int xMin = Mathf.Max(0, Mathf.CeilToInt((origin.x - west) / step - .5f));
                int zMin = Mathf.Max(0, Mathf.CeilToInt((origin.z - south) / step - .5f));
                int xMax = Mathf.Min(Resolution, Mathf.CeilToInt((origin.x + size.x - west) / step - .5f));
                int zMax = Mathf.Min(Resolution, Mathf.CeilToInt((origin.z + size.z - south) / step - .5f));
                for (int z = zMin; z < zMax; z++)
                for (int x = xMin; x < xMax; x++)
                {
                    float u = (west + (x + .5f) * step - origin.x) / size.x;
                    float v = (south + (z + .5f) * step - origin.z) / size.z;
                    float height = origin.y + data.GetInterpolatedHeight(u, v);
                    Color color = Color.Lerp(Lowland, Highland, Mathf.InverseLerp(-3, 22, height));
                    float band = Mathf.Repeat(height, 3f);
                    float contour = 1 - Mathf.SmoothStep(0f, 1f, Mathf.Min(band, 3f - band) / .24f);
                    pixels[z * Resolution + x] = Color.Lerp(color, Frame, contour * .38f);
                }
            }
            relief.SetPixels32(pixels);
            relief.Apply(false);
        }

        static Texture2D CreateIcon(string name, bool isArrow)
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

        static void DrawIcon(Texture2D texture, Vector2 point, float size, float angle, Color tint)
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

        public void Dispose()
        {
            if (relief) Object.Destroy(relief);
            if (arrow) Object.Destroy(arrow);
            if (diamond) Object.Destroy(diamond);
        }
    }
}
