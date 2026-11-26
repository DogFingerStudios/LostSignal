using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace KeepDriving
{
    internal sealed class RouteSurface
    {
        private const int TileSize = YanorraMap.TileSize;
        private readonly float _spacing, _heightOffset, _channelDepth, _treeBuffer;
        private readonly Dictionary<Vector2Int, List<Channel>> _channels = new();

        private readonly struct Channel
        {
            public readonly Vector2 Start, End;
            public readonly float Radius;

            public Channel(Vector2 start, Vector2 end, float radius)
            {
                Start = start;
                End = end;
                Radius = radius;
            }
        }

        private readonly List<List<Vector2>> _centerlines = new();
        private readonly Dictionary<Vector2Int, List<Ribbon>> _tiles = new();

        private readonly struct Ribbon
        {
            public readonly Vector2 LeftStart, LeftEnd, RightEnd, RightStart;

            public Ribbon(Vector2 start, Vector2 end, Vector2 startSide, Vector2 endSide)
            {
                LeftStart = start + startSide;
                LeftEnd = end + endSide;
                RightEnd = end - endSide;
                RightStart = start - startSide;
            }
        }

        public RouteSurface(TextAsset geoJson, string field, string type, float defaultWidth, float spacing, float heightOffset, float channelDepth = 0f, float treeBuffer = 0f)
        {
            _spacing = spacing;
            _heightOffset = heightOffset;
            _channelDepth = channelDepth;
            _treeBuffer = treeBuffer;
            using var reader = JsonReaderWriterFactory.CreateJsonReader(geoJson.bytes, XmlDictionaryReaderQuotas.Max);
            var collection = XElement.Load(reader);
            if ((string)collection.Element("type") != "FeatureCollection" || collection.Element("features") == null)
                throw new FormatException("Expected a GeoJSON FeatureCollection for routes.");

            foreach (var feature in collection.Element("features").Elements("item"))
            {
                var properties = feature.Element("properties");
                if ((string)properties?.Element(field) != type) continue;
                float width = float.TryParse((string)properties.Element("width_m"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) && value > 0 && !float.IsInfinity(value)
                    ? value : defaultWidth;
                var geometry = feature.Element("geometry");
                var coordinates = geometry?.Element("coordinates");
                if (coordinates == null) continue;

                switch ((string)geometry.Element("type"))
                {
                    case "LineString":
                        AddLine(coordinates, width);
                        break;
                    case "MultiLineString":
                        foreach (var line in coordinates.Elements("item"))
                            AddLine(line, width);
                        break;
                }
            }
        }

        private void AddLine(XElement coordinates, float width)
        {
            var points = new List<Vector2>();
            foreach (var coordinate in coordinates.Elements("item"))
            {
                using var values = coordinate.Elements("item").GetEnumerator();
                if (!values.MoveNext() || !TryCoordinate(values.Current, out float east) ||
                    !values.MoveNext() || !TryCoordinate(values.Current, out float north))
                    return;
                var point = new Vector2(east, north);
                if (points.Count == 0 || (point - points[points.Count - 1]).sqrMagnitude > .0001f)
                    points.Add(point);
            }

            if (points.Count < 2) return;
            _centerlines.Add(points);
            var sides = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 before = i > 0 ? LeftOf(points[i] - points[i - 1]) : LeftOf(points[1] - points[0]);
                Vector2 after = i + 1 < points.Count ? LeftOf(points[i + 1] - points[i]) : before;
                Vector2 join = (before + after).normalized;
                if (join == Vector2.zero) join = after;
                sides[i] = join * (width * .5f / Mathf.Max(.5f, Vector2.Dot(join, after)));
            }

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 start = points[i - 1], end = points[i];
                if (_channelDepth > 0)
                {
                    int parts = Mathf.CeilToInt(Vector2.Distance(start, end) / (TileSize * .5f));
                    for (int part = 0; part < parts; part++)
                        IndexChannel(new Channel(Vector2.Lerp(start, end, part / (float)parts),
                            Vector2.Lerp(start, end, (part + 1) / (float)parts), width * .5f));
                }
                Vector2 startSide = sides[i - 1], endSide = sides[i];
                int steps = Mathf.CeilToInt(Vector2.Distance(start, end) / _spacing);
                for (int j = 0; j < steps; j++)
                {
                    float a = j / (float)steps, b = (j + 1) / (float)steps;
                    Vector2 first = Vector2.Lerp(start, end, a), last = Vector2.Lerp(start, end, b);
                    Vector2 firstSide = Vector2.Lerp(startSide, endSide, a), lastSide = Vector2.Lerp(startSide, endSide, b);
                    int strips = Mathf.Max(1, Mathf.CeilToInt(2 * Mathf.Max(firstSide.magnitude, lastSide.magnitude) / _spacing));
                    for (int strip = 0; strip < strips; strip++)
                    {
                        float center = 1f - (2f * strip + 1f) / strips;
                        Index(new Ribbon(first + firstSide * center, last + lastSide * center,
                            firstSide / strips, lastSide / strips));
                    }
                }
            }
        }

        private static bool TryCoordinate(XElement element, out float value) =>
            float.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static Vector2 LeftOf(Vector2 direction) => new Vector2(-direction.y, direction.x).normalized;

        private void Index(Ribbon ribbon)
        {
            Vector2 min = Vector2.Min(Vector2.Min(ribbon.LeftStart, ribbon.LeftEnd), Vector2.Min(ribbon.RightStart, ribbon.RightEnd));
            Vector2 max = Vector2.Max(Vector2.Max(ribbon.LeftStart, ribbon.LeftEnd), Vector2.Max(ribbon.RightStart, ribbon.RightEnd));
            min -= Vector2.one * _treeBuffer;
            max += Vector2.one * _treeBuffer;
            if (max.x <= 0 || max.y <= 0 || min.x >= YanorraMap.WorldSize || min.y >= YanorraMap.WorldSize) return;
            for (int z = TileAt(min.y); z <= TileAt(max.y); z++)
            for (int x = TileAt(min.x); x <= TileAt(max.x); x++)
            {
                var key = new Vector2Int(x, z);
                if (!_tiles.TryGetValue(key, out var ribbons))
                    _tiles.Add(key, ribbons = new List<Ribbon>());
                ribbons.Add(ribbon);
            }
        }

        public bool ContainsRoute(Vector2Int key, Vector2 point, bool includeBuffer = false)
        {
            if (!_tiles.TryGetValue(key, out var ribbons)) return false;
            float bufferSquared = _treeBuffer * _treeBuffer;
            foreach (var ribbon in ribbons)
            {
                var a = ribbon.LeftStart;
                var b = ribbon.LeftEnd;
                var c = ribbon.RightEnd;
                var d = ribbon.RightStart;
                // Match the mesh footprint, including its joins, then measure from the actual edges.
                if (InsideTriangle(point, a, b, c) || InsideTriangle(point, a, c, d) ||
                    includeBuffer && (SegmentDistanceSquared(point, a, b) <= bufferSquared ||
                    SegmentDistanceSquared(point, b, c) <= bufferSquared ||
                    SegmentDistanceSquared(point, c, d) <= bufferSquared ||
                    SegmentDistanceSquared(point, d, a) <= bufferSquared))
                    return true;
            }

            return false;
        }

        private static bool InsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = Cross(b - a, c - a);
            if (area == 0f) return false;
            float ab = Cross(b - a, point - a);
            float bc = Cross(c - b, point - b);
            float ca = Cross(a - c, point - c);
            return area > 0f ? ab >= 0f && bc >= 0f && ca >= 0f : ab <= 0f && bc <= 0f && ca <= 0f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static float SegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            Vector2 offset = point - a;
            float lengthSquared = edge.sqrMagnitude;
            if (lengthSquared == 0f) return offset.sqrMagnitude;
            float along = Mathf.Clamp01(Vector2.Dot(offset, edge) / lengthSquared);
            return (offset - edge * along).sqrMagnitude;
        }

        private void IndexChannel(Channel channel)
        {
            var radius = Vector2.one * channel.Radius;
            Vector2 min = Vector2.Min(channel.Start, channel.End) - radius;
            Vector2 max = Vector2.Max(channel.Start, channel.End) + radius;
            if (max.x < 0 || max.y < 0 || min.x > YanorraMap.WorldSize || min.y > YanorraMap.WorldSize) return;
            for (int z = TileAt(min.y); z <= TileAt(max.y); z++)
            for (int x = TileAt(min.x); x <= TileAt(max.x); x++)
            {
                var key = new Vector2Int(x, z);
                if (!_channels.TryGetValue(key, out var channels))
                    _channels.Add(key, channels = new List<Channel>());
                channels.Add(channel);
            }
        }

        public bool HasChannel(Vector2Int key) => _channels.ContainsKey(key);

        public bool ContainsChannel(Vector2Int key, Vector2 point)
        {
            if (!_channels.TryGetValue(key, out var channels)) return false;
            foreach (var channel in channels)
            {
                Vector2 delta = channel.End - channel.Start;
                float lengthSquared = delta.sqrMagnitude;
                if (lengthSquared <= 0f) continue;
                Vector2 offset = point - channel.Start;
                float along = Mathf.Clamp01(Vector2.Dot(offset, delta) / lengthSquared);
                if ((offset - delta * along).sqrMagnitude < channel.Radius * channel.Radius)
                    return true;
            }
            return false;
        }

        public void AccumulateDepth(Vector2Int key, float[,] cut)
        {
            if (!_channels.TryGetValue(key, out var channels)) return;
            int last = cut.GetLength(0) - 1;
            float cell = TileSize / (float)last;
            var origin = new Vector2(key.x * TileSize, key.y * TileSize);
            foreach (var channel in channels)
            {
                var radius = Vector2.one * channel.Radius;
                Vector2 start = channel.Start - origin, end = channel.End - origin;
                Vector2 min = (Vector2.Min(start, end) - radius) / cell;
                Vector2 max = (Vector2.Max(start, end) + radius) / cell;
                Vector2 delta = end - start;
                float lengthSquared = delta.sqrMagnitude;
                if (lengthSquared <= 0f) continue;

                for (int z = Mathf.Max(0, Mathf.CeilToInt(min.y)); z <= Mathf.Min(last, Mathf.FloorToInt(max.y)); z++)
                for (int x = Mathf.Max(0, Mathf.CeilToInt(min.x)); x <= Mathf.Min(last, Mathf.FloorToInt(max.x)); x++)
                {
                    var offset = new Vector2(x * cell, z * cell) - start;
                    float along = Mathf.Clamp01(Vector2.Dot(offset, delta) / lengthSquared);
                    float distance = (offset - delta * along).magnitude;
                    if (distance >= channel.Radius) continue;
                    float depth = _channelDepth * Mathf.SmoothStep(1f, 0f, distance / channel.Radius);
                    cut[z, x] = Mathf.Max(cut[z, x], depth);
                }
            }
        }

        private static int TileAt(float value) =>
            Mathf.FloorToInt(Mathf.Clamp(value / TileSize, 0, YanorraMap.TileCount - 1));

        public void DrawMap(Rect map, Rect world, Color color, float width)
        {
            foreach (var line in _centerlines)
            for (int i = 1; i < line.Count; i++)
            {
                Vector2 start = line[i - 1];
                Vector2 delta = line[i] - start;
                float from = 0, to = 1;
                if (!ClipAxis(start.x, delta.x, world.xMin, world.xMax, ref from, ref to) ||
                    !ClipAxis(start.y, delta.y, world.yMin, world.yMax, ref from, ref to))
                    continue;

                Vector2 a = Project(start + delta * from, world, map);
                Vector2 b = Project(start + delta * to, world, map);
                Vector2 direction = b - a;
                if (direction.sqrMagnitude == 0f) continue;

                Matrix4x4 original = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, a);
                GUI.DrawTexture(new Rect(a.x, a.y - width * .5f, Mathf.Max(direction.magnitude, 1f), width),
                    Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
                GUI.matrix = original;
            }
        }

        private static bool ClipAxis(float start, float delta, float min, float max, ref float from, ref float to)
        {
            if (delta == 0) return start >= min && start <= max;
            float a = (min - start) / delta, b = (max - start) / delta;
            from = Mathf.Max(from, Mathf.Min(a, b));
            to = Mathf.Min(to, Mathf.Max(a, b));
            return from <= to;
        }

        private static Vector2 Project(Vector2 point, Rect world, Rect map) => new Vector2(
            map.xMin + (point.x - world.xMin) * map.width / world.width,
            map.yMax - (point.y - world.yMin) * map.height / world.height);

        public Mesh BuildTile(Vector2Int key, TerrainData terrain)
        {
            if (!_tiles.TryGetValue(key, out var ribbons)) return null;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var polygon = new List<Vector2>(8);
            var clipped = new List<Vector2>(8);
            var origin = new Vector2(key.x * TileSize, key.y * TileSize);
            foreach (var ribbon in ribbons)
            {
                polygon.Clear();
                polygon.Add(ribbon.LeftStart - origin);
                polygon.Add(ribbon.LeftEnd - origin);
                polygon.Add(ribbon.RightEnd - origin);
                polygon.Add(ribbon.RightStart - origin);

                Clip(polygon, clipped, 0, 0, true);
                Clip(clipped, polygon, 0, TileSize, false);
                Clip(polygon, clipped, 1, 0, true);
                Clip(clipped, polygon, 1, TileSize, false);
                if (polygon.Count < 3) continue;

                int start = vertices.Count;
                foreach (var point in polygon)
                    vertices.Add(new Vector3(point.x,
                        terrain.GetInterpolatedHeight(point.x / TileSize, point.y / TileSize) + _heightOffset, point.y));
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    indices.Add(start);
                    indices.Add(start + i);
                    indices.Add(start + i + 1);
                }
            }

            if (indices.Count == 0) return null;
            var mesh = new Mesh { name = "Route surface" };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            return mesh;
        }

        internal static void Clip(List<Vector2> input, List<Vector2> output, int axis, float edge, bool keepAbove)
        {
            output.Clear();
            if (input.Count == 0) return;
            Vector2 previous = input[input.Count - 1];
            bool previousInside = keepAbove ? previous[axis] >= edge : previous[axis] <= edge;
            foreach (var current in input)
            {
                bool inside = keepAbove ? current[axis] >= edge : current[axis] <= edge;
                if (inside != previousInside)
                {
                    var intersection = Vector2.Lerp(previous, current, (edge - previous[axis]) / (current[axis] - previous[axis]));
                    intersection[axis] = edge;
                    output.Add(intersection);
                }

                if (inside) output.Add(current);
                previous = current;
                previousInside = inside;
            }
        }
    }
}
