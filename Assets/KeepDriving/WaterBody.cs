using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace KeepDriving
{
    internal sealed class WaterBody : IDisposable
    {
        private const int TileSize = YanorraMap.TileSize;
        private readonly List<(Vector2 A, Vector2 B)> _edges = new();
        private readonly List<Vector2[]> _strips = new();
        private readonly Rect _bounds;
        private readonly float _level, _depth, _shoreSlopeWidth;
        private Texture2D _mapTexture;

        public Material Material { get; }
        public string Name { get; }
        private Color MapColor { get; }

        public static IEnumerable<WaterBody> Read(TextAsset asset, Shader shader, Func<Vector2, float> ground)
        {
            using var reader = JsonReaderWriterFactory.CreateJsonReader(asset.bytes, XmlDictionaryReaderQuotas.Max);
            var collection = XElement.Load(reader);
            if ((string)collection.Element("type") != "FeatureCollection")
                throw new FormatException("Expected a GeoJSON FeatureCollection for water bodies.");

            foreach (var feature in collection.Element("features").Elements("item"))
            {
                var properties = feature.Element("properties");
                string type = (string)properties?.Element("water_type");
                if (type != "lake" && type != "pond" && type != "reservoir") continue;
                string name = (string)properties.Element("name") ?? type;
                var geometry = feature.Element("geometry");
                var coordinates = geometry?.Element("coordinates");
                if (coordinates == null) continue;
                switch ((string)geometry.Element("type"))
                {
                    case "Polygon":
                        yield return new WaterBody(coordinates, shader, ground, name, type);
                        break;
                    case "MultiPolygon":
                        foreach (var polygon in coordinates.Elements("item"))
                            yield return new WaterBody(polygon, shader, ground, name, type);
                        break;
                }
            }
        }

        private WaterBody(XElement polygon, Shader shader, Func<Vector2, float> ground, string name, string type)
        {
            Name = name;
            _depth = type == "lake" ? 10f : 5f;
            _shoreSlopeWidth = type == "lake" ? 20f : 15f;
            var elevations = new SortedSet<float>();
            Vector2 min = Vector2.one * float.PositiveInfinity;
            Vector2 max = Vector2.one * float.NegativeInfinity;
            float level = float.PositiveInfinity;
            foreach (var ring in polygon.Elements("item"))
            {
                var points = new List<Vector2>();
                foreach (var coordinate in ring.Elements("item"))
                {
                    using var values = coordinate.Elements("item").GetEnumerator();
                    if (!values.MoveNext()) throw new FormatException("Missing water body easting.");
                    float east = (float)values.Current;
                    if (!values.MoveNext()) throw new FormatException("Missing water body northing.");
                    float north = (float)values.Current;
                    if (!float.IsFinite(east) || !float.IsFinite(north))
                        throw new FormatException("Water body coordinates must be finite.");
                    points.Add(new Vector2(east, north));
                }

                if (points.Count < 4 || !points[0].Equals(points[points.Count - 1]))
                    throw new FormatException($"Invalid polygon ring for {name}.");

                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 a = points[i - 1], b = points[i];
                    if (a.Equals(b)) continue;
                    _edges.Add((a, b));
                    elevations.Add(a.y);
                    min = Vector2.Min(min, a);
                    max = Vector2.Max(max, a);
                    // The lowest point along the shore sets the waterline for the entire body.
                    int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 4f));
                    for (int j = 0; j < samples; j++)
                        level = Mathf.Min(level, ground(Vector2.Lerp(a, b, j / (float)samples)));
                }
            }

            _bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            _level = level;
            BuildStrips(elevations);
            if (_strips.Count == 0) throw new FormatException($"Empty water polygon for {name}.");

            var style = type switch
            {
                "lake" => (new Color(.62f, .94f, 1.45f), 1.15f, .8f, new Color(.28f, .58f, 1f)),
                "pond" => (new Color(.96f, 1.26f, .56f), .38f, .42f, new Color(.60f, .81f, .34f)),
                _ => (new Color(.45f, 1.25f, 1.18f), .75f, 1.2f, new Color(.31f, .94f, .88f))
            };
            Material = new Material(shader) { name = name };
            Material.SetColor("_WaterTint", style.Item1);
            Material.SetFloat("_RippleStrength", style.Item2);
            Material.SetFloat("_RippleSpeed", style.Item3);
            MapColor = style.Item4;
        }

        private static bool Crosses(Vector2 a, Vector2 b, float north) => (a.y > north) != (b.y > north);

        private static float EastAt(Vector2 a, Vector2 b, float north) =>
            a.x + (b.x - a.x) * ((north - a.y) / (b.y - a.y));

        private void BuildStrips(SortedSet<float> elevations)
        {
            var levels = new List<float>(elevations);
            var crossings = new List<(Vector2 A, Vector2 B)>();
            // Between consecutive vertex northings, the edges keep their order.
            // Pairing crossings by parity fills concave polygons and preserves holes.
            for (int i = 1; i < levels.Count; i++)
            {
                float low = levels[i - 1], high = levels[i];
                float mid = low + (high - low) * .5f;
                crossings.Clear();
                foreach (var edge in _edges)
                    if (Crosses(edge.A, edge.B, mid)) crossings.Add(edge);
                crossings.Sort((a, b) => EastAt(a.A, a.B, mid).CompareTo(EastAt(b.A, b.B, mid)));
                for (int j = 1; j < crossings.Count; j += 2)
                {
                    var left = crossings[j - 1];
                    var right = crossings[j];
                    _strips.Add(new[]
                    {
                        new Vector2(EastAt(left.A, left.B, low), low),
                        new Vector2(EastAt(left.A, left.B, high), high),
                        new Vector2(EastAt(right.A, right.B, high), high),
                        new Vector2(EastAt(right.A, right.B, low), low)
                    });
                }
            }
        }

        private static Vector2 TileOrigin(Vector2Int key) => new Vector2(key.x * TileSize, key.y * TileSize);

        public bool OverlapsTile(Vector2Int key) => _bounds.Overlaps(new Rect(TileOrigin(key), Vector2.one * TileSize));

        public void AccumulateCut(Vector2Int key, float tileY, float range, float[,] heights, float[,] cuts)
        {
            if (!OverlapsTile(key)) return;
            int last = heights.GetLength(0) - 1;
            float cell = TileSize / (float)last;
            Vector2 origin = TileOrigin(key);
            int minX = Mathf.Max(0, Mathf.CeilToInt((_bounds.xMin - origin.x) / cell));
            int maxX = Mathf.Min(last, Mathf.FloorToInt((_bounds.xMax - origin.x) / cell));
            int minZ = Mathf.Max(0, Mathf.CeilToInt((_bounds.yMin - origin.y) / cell));
            int maxZ = Mathf.Min(last, Mathf.FloorToInt((_bounds.yMax - origin.y) / cell));
            if (minX > maxX || minZ > maxZ) return;

            int columns = maxX - minX + 1;
            float radius = _shoreSlopeWidth;
            float radiusSquared = radius * radius;
            var shore = new float[(maxZ - minZ + 1) * columns];

            // Only rasterize distances where a shoreline edge can affect the bank slope.
            foreach (var (a, b) in _edges)
            {
                int x0 = Mathf.Max(minX, Mathf.CeilToInt((Mathf.Min(a.x, b.x) - radius - origin.x) / cell));
                int x1 = Mathf.Min(maxX, Mathf.FloorToInt((Mathf.Max(a.x, b.x) + radius - origin.x) / cell));
                int z0 = Mathf.Max(minZ, Mathf.CeilToInt((Mathf.Min(a.y, b.y) - radius - origin.y) / cell));
                int z1 = Mathf.Min(maxZ, Mathf.FloorToInt((Mathf.Max(a.y, b.y) + radius - origin.y) / cell));
                Vector2 delta = b - a;
                float lengthSquared = delta.sqrMagnitude;
                for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var offset = origin + new Vector2(x * cell, z * cell) - a;
                    float along = Mathf.Clamp01(Vector2.Dot(offset, delta) / lengthSquared);
                    float influence = 1f - (offset - delta * along).sqrMagnitude / radiusSquared;
                    int i = (z - minZ) * columns + x - minX;
                    shore[i] = Mathf.Max(shore[i], influence);
                }
            }

            var crossings = new List<float>();
            for (int z = minZ; z <= maxZ; z++)
            {
                float north = origin.y + z * cell;
                crossings.Clear();
                foreach (var (a, b) in _edges)
                    if (Crosses(a, b, north)) crossings.Add(EastAt(a, b, north));
                crossings.Sort();

                // An inclusive left edge and exclusive right edge match the polygon parity test.
                int crossing = 0;
                for (int x = minX; x <= maxX && crossing < crossings.Count; x++)
                {
                    float east = origin.x + x * cell;
                    while (crossing < crossings.Count && east >= crossings[crossing]) crossing++;
                    if ((crossing & 1) == 0) continue;

                    float influence = shore[(z - minZ) * columns + x - minX];
                    float shoreFraction = Mathf.Sqrt(1f - influence);
                    float bed = _level + .04f - _depth * Mathf.SmoothStep(0f, 1f, shoreFraction);
                    float cut = tileY + heights[z, x] * range - bed;
                    cuts[z, x] = cuts[z, x] == 0f ? cut : Mathf.Max(cuts[z, x], cut);
                }
            }
        }

        public bool ContainsPoint(Vector2 point)
        {
            if (!_bounds.Contains(point)) return false;
            bool inside = false;
            foreach (var (a, b) in _edges)
                if (Crosses(a, b, point.y) && point.x < EastAt(a, b, point.y)) inside = !inside;
            return inside;
        }

        private bool Contains(Vector2 point, out float shoreDistance)
        {
            bool inside = false;
            float distanceSquared = float.PositiveInfinity;
            foreach (var (a, b) in _edges)
            {
                if (Crosses(a, b, point.y) && point.x < EastAt(a, b, point.y)) inside = !inside;
                Vector2 delta = b - a;
                Vector2 offset = point - a;
                float along = Mathf.Clamp01(Vector2.Dot(offset, delta) / delta.sqrMagnitude);
                distanceSquared = Mathf.Min(distanceSquared, (offset - delta * along).sqrMagnitude);
            }
            shoreDistance = Mathf.Sqrt(distanceSquared);
            return inside;
        }

        public Mesh BuildTile(Vector2Int key, float tileY)
        {
            if (!OverlapsTile(key)) return null;
            Vector2 origin = TileOrigin(key);
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var polygon = new List<Vector2>(8);
            var clipped = new List<Vector2>(8);
            foreach (var strip in _strips)
            {
                polygon.Clear();
                foreach (var point in strip) polygon.Add(point - origin);
                RouteSurface.Clip(polygon, clipped, 0, 0, true);
                RouteSurface.Clip(clipped, polygon, 0, TileSize, false);
                RouteSurface.Clip(polygon, clipped, 1, 0, true);
                RouteSurface.Clip(clipped, polygon, 1, TileSize, false);
                if (polygon.Count < 3) continue;

                int start = vertices.Count;
                foreach (var point in polygon)
                    vertices.Add(new Vector3(point.x, _level - tileY + .04f, point.y));
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    indices.Add(start);
                    indices.Add(start + i);
                    indices.Add(start + i + 1);
                }
            }

            if (indices.Count == 0) return null;
            var mesh = new Mesh { name = Name };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            return mesh;
        }

        public void DrawMap(Rect map, Rect world, List<Rect> markers)
        {
            if (Event.current.type != EventType.Repaint || !_bounds.Overlaps(world)) return;
            float scaleX = map.width / world.width, scaleY = map.height / world.height;
            float width = _bounds.width * scaleX, height = _bounds.height * scaleY;
            if (Mathf.Max(width, height) < 4f)
            {
                // Small bodies still need a visible mark on the 200 km world map.
                Vector2 center = _bounds.center;
                float x = Mathf.Clamp(map.xMin + (center.x - world.xMin) * scaleX, map.xMin + 2, map.xMax - 2);
                float y = Mathf.Clamp(map.yMax - (center.y - world.yMin) * scaleY, map.yMin + 2, map.yMax - 2);
                // Keep nearby bodies individually visible at a zoom where their footprints overlap.
                var marker = new Rect(x - 2, y - 2, 4, 4);
                for (int i = 0; i < markers.Count; i++)
                {
                    var occupied = markers[i];
                    var margin = Rect.MinMaxRect(occupied.xMin - 1, occupied.yMin - 1, occupied.xMax + 1, occupied.yMax + 1);
                    if (!marker.Overlaps(margin)) continue;
                    marker.x = x + 3 + 5 * i;
                }
                if (marker.xMax > map.xMax) marker.x = Mathf.Max(map.xMin, x - 2 - 5 * markers.Count);
                markers.Add(marker);
                GUI.DrawTexture(marker, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, MapColor, 0f, 2f);
                return;
            }

            if (!_mapTexture) CreateMapTexture();
            Rect visible = Rect.MinMaxRect(Mathf.Max(_bounds.xMin, world.xMin), Mathf.Max(_bounds.yMin, world.yMin),
                Mathf.Min(_bounds.xMax, world.xMax), Mathf.Min(_bounds.yMax, world.yMax));
            var screen = new Rect(map.xMin + (visible.xMin - world.xMin) * scaleX,
                map.yMax - (visible.yMax - world.yMin) * scaleY, visible.width * scaleX, visible.height * scaleY);
            var uv = new Rect((visible.xMin - _bounds.xMin) / _bounds.width,
                (visible.yMin - _bounds.yMin) / _bounds.height,
                visible.width / _bounds.width, visible.height / _bounds.height);
            GUI.DrawTextureWithTexCoords(screen, _mapTexture, uv, true);
        }

        private void CreateMapTexture()
        {
            const int resolution = 128;
            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                var point = new Vector2(_bounds.xMin + (x + .5f) * _bounds.width / resolution,
                    _bounds.yMin + (y + .5f) * _bounds.height / resolution);
                pixels[y * resolution + x] = Contains(point, out _) ? MapColor : new Color(MapColor.r, MapColor.g, MapColor.b, 0f);
            }
            _mapTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = Name,
                wrapMode = TextureWrapMode.Clamp
            };
            _mapTexture.SetPixels32(pixels);
            _mapTexture.Apply(false, true);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(Material);
            if (_mapTexture) UnityEngine.Object.Destroy(_mapTexture);
        }
    }
}
