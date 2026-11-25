using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace KeepDriving
{
    internal sealed class WoodlandBiomes
    {
        private const int CellsPerTile = 80;
        private const float BaseCellDensity = .025f;
        private readonly List<Woodland> _areas = new();
        private readonly int _prefabCount;

        private sealed class Woodland
        {
            public readonly List<Vector2[]> Rings = new();
            public Rect Bounds;
            public float Chance;

            public bool Contains(Vector2 point)
            {
                if (!Bounds.Contains(point)) return false;
                bool inside = false;
                foreach (var ring in Rings)
                for (int i = 0, previous = ring.Length - 1; i < ring.Length; previous = i++)
                {
                    Vector2 a = ring[previous], b = ring[i];
                    if ((a.y > point.y) != (b.y > point.y) &&
                        point.x < a.x + (b.x - a.x) * ((point.y - a.y) / (b.y - a.y)))
                        inside = !inside;
                }
                return inside;
            }
        }

        public WoodlandBiomes(TextAsset asset, int prefabCount)
        {
            _prefabCount = prefabCount;
            using var reader = JsonReaderWriterFactory.CreateJsonReader(asset.bytes, XmlDictionaryReaderQuotas.Max);
            var collection = XElement.Load(reader);
            if ((string)collection.Element("type") != "FeatureCollection" || collection.Element("features") == null)
                throw new FormatException("Expected a GeoJSON FeatureCollection for biomes.");

            foreach (var feature in collection.Element("features").Elements("item"))
            {
                var properties = feature.Element("properties");
                if (!string.Equals((string)properties?.Element("biome_type"), "woodland", StringComparison.OrdinalIgnoreCase))
                    continue;

                string raw = (string)properties.Element("vegetation_density");
                float density = float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    && float.IsFinite(value) ? Mathf.Max(0, value) : 1f;
                // Increasing multipliers approach the placement grid's physical capacity.
                float chance = 1f - Mathf.Exp(-BaseCellDensity * density);
                var geometry = feature.Element("geometry");
                var coordinates = geometry?.Element("coordinates");
                if (coordinates == null) continue;
                if ((string)geometry.Element("type") == "Polygon") AddPolygon(coordinates, chance);
                else if ((string)geometry.Element("type") == "MultiPolygon")
                    foreach (var polygon in coordinates.Elements("item")) AddPolygon(polygon, chance);
            }
        }

        private void AddPolygon(XElement polygon, float chance)
        {
            var area = new Woodland { Chance = chance };
            foreach (var ring in polygon.Elements("item"))
            {
                var points = new List<Vector2>();
                foreach (var coordinate in ring.Elements("item"))
                {
                    using var values = coordinate.Elements("item").GetEnumerator();
                    if (!values.MoveNext()) return;
                    float x = (float)values.Current;
                    if (!values.MoveNext()) return;
                    float z = (float)values.Current;
                    if (!float.IsFinite(x) || !float.IsFinite(z)) return;
                    points.Add(new Vector2(x, z));
                }
                if (points.Count < 4 || !points[0].Equals(points[points.Count - 1])) return;
                area.Rings.Add(points.ToArray());
            }
            if (area.Rings.Count == 0) return;

            Vector2 min = area.Rings[0][0], max = min;
            foreach (var point in area.Rings[0])
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            area.Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            _areas.Add(area);
        }

        private static Rect TileBounds(Vector2Int tile) => new(
            tile.x * YanorraMap.TileSize, tile.y * YanorraMap.TileSize, YanorraMap.TileSize, YanorraMap.TileSize);

        public bool OverlapsTile(Vector2Int tile)
        {
            Rect bounds = TileBounds(tile);
            foreach (var area in _areas)
                if (area.Chance > 0f && area.Bounds.Overlaps(bounds)) return true;
            return false;
        }

        public TreeInstance[] GenerateTile(Vector2Int tile, Func<Vector2, bool> excluded, CancellationToken token)
        {
            Rect bounds = TileBounds(tile);
            var candidates = new List<Woodland>();
            foreach (var area in _areas)
                if (area.Chance > 0f && area.Bounds.Overlaps(bounds)) candidates.Add(area);
            if (candidates.Count == 0) return Array.Empty<TreeInstance>();

            var trees = new List<TreeInstance>();
            for (int z = 0; z < CellsPerTile; z++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < CellsPerTile; x++)
                {
                    int east = tile.x * CellsPerTile + x, north = tile.y * CellsPerTile + z;
                    float u = (x + Mathf.Lerp(.15f, .85f, Random01(east, north, 1))) / CellsPerTile;
                    float v = (z + Mathf.Lerp(.15f, .85f, Random01(east, north, 2))) / CellsPerTile;
                    Vector2 point = bounds.min + new Vector2(u * bounds.width, v * bounds.height);
                    float roll = Random01(east, north, 3);
                    bool plant = false;
                    foreach (var area in candidates)
                    {
                        if (roll >= area.Chance || !area.Contains(point)) continue;
                        plant = true;
                        break;
                    }
                    if (!plant || excluded(point)) continue;

                    float scale = Mathf.Lerp(.8f, 1.25f, Random01(east, north, 4));
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(u, 0f, v),
                        prototypeIndex = (int)(Random01(east, north, 5) * _prefabCount),
                        widthScale = scale,
                        heightScale = scale,
                        rotation = Random01(east, north, 6) * 2f * Mathf.PI,
                        color = Color.white,
                        lightmapColor = Color.white
                    });
                }
            }
            return trees.ToArray();
        }

        public void ShadeMap(Color32[] pixels, int resolution, Rect view)
        {
            var crossings = new List<float>();
            var tint = new Color(.07f, .29f, .14f);
            float step = view.width / resolution;
            bool[] shaded = null;
            foreach (var area in _areas)
            {
                if (!area.Bounds.Overlaps(view)) continue;
                shaded ??= new bool[pixels.Length];
                int firstRow = Mathf.Max(0, Mathf.CeilToInt((area.Bounds.yMin - view.yMin) / step - .5f));
                int lastRow = Mathf.Min(resolution, Mathf.CeilToInt((area.Bounds.yMax - view.yMin) / step - .5f));
                for (int row = firstRow; row < lastRow; row++)
                {
                    float north = view.yMin + (row + .5f) * step;
                    if (north < 0 || north >= YanorraMap.WorldSize) continue;
                    crossings.Clear();
                    foreach (var ring in area.Rings)
                    for (int i = 1; i < ring.Length; i++)
                    {
                        Vector2 a = ring[i - 1], b = ring[i];
                        if ((a.y > north) != (b.y > north))
                            crossings.Add(a.x + (b.x - a.x) * ((north - a.y) / (b.y - a.y)));
                    }
                    crossings.Sort();
                    for (int i = 0; i + 1 < crossings.Count; i += 2)
                    {
                        float west = Mathf.Max(0, crossings[i]);
                        float east = Mathf.Min(YanorraMap.WorldSize, crossings[i + 1]);
                        int first = Mathf.Max(0, Mathf.CeilToInt((west - view.xMin) / step - .5f));
                        int last = Mathf.Min(resolution, Mathf.CeilToInt((east - view.xMin) / step - .5f));
                        for (int column = first; column < last; column++)
                        {
                            int index = row * resolution + column;
                            if (shaded[index]) continue;
                            shaded[index] = true;
                            pixels[index] = Color.Lerp(pixels[index], tint, .56f);
                        }
                    }
                }
            }
        }

        private static float Random01(int x, int z, uint salt)
        {
            unchecked
            {
                uint hash = (uint)x * 0x9e3779b9u ^ (uint)z * 0x85ebca6bu ^ salt * 0xc2b2ae35u;
                hash = (hash ^ (hash >> 16)) * 0x7feb352du;
                hash = (hash ^ (hash >> 15)) * 0x846ca68bu;
                return ((hash ^ (hash >> 16)) >> 8) / 16777216f;
            }
        }
    }
}
