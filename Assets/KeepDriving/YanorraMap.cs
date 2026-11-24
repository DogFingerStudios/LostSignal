using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeepDriving
{
    internal static class YanorraMap
    {
        public const int TileSize = 500;
        public const int TileCount = 400;
        public const int WorldSize = TileSize * TileCount;
        public const int RasterSize = 2000;
        private const int CellSize = 100;

        [Serializable]
        internal sealed class FeatureCollection
        {
            public string type;
            public Feature[] features;
        }

        [Serializable]
        internal sealed class Feature
        {
            public Properties properties;
            public Geometry geometry;
        }

        [Serializable]
        internal sealed class Properties
        {
            public string id;
            public string name;
            public string site_type;
            public float heading_deg;
        }

        [Serializable]
        internal sealed class Geometry
        {
            public string type;
            public double[] coordinates;
        }

        internal readonly struct Point
        {
            public readonly double East;
            public readonly double North;
            public readonly string Id;
            public readonly string Name;
            public readonly float Heading;

            public Point(Feature feature)
            {
                var coordinates = feature?.geometry?.coordinates;
                if (feature?.geometry?.type != "Point" || coordinates == null || coordinates.Length < 2 ||
                    !WithinMap(coordinates[0]) || !WithinMap(coordinates[1]))
                    throw new FormatException("Expected a Point in Yanorra grid meters within the world boundary.");

                East = coordinates[0];
                North = coordinates[1];
                Id = feature.properties?.id;
                Name = feature.properties?.name;
                Heading = feature.properties?.heading_deg ?? 0f;
            }

            public Vector2Int Tile => new Vector2Int(TileIndex(East), TileIndex(North));
        }

        public static int TileIndex(double value) => value == WorldSize ? TileCount - 1 : (int)Math.Floor(value / TileSize);

        private static bool WithinMap(double value) => value >= 0 && value <= WorldSize;

        private static Feature[] ReadFeatures(TextAsset asset)
        {
            if (!asset) throw new InvalidOperationException("A Yanorra GeoJSON asset is missing.");
            var collection = JsonUtility.FromJson<FeatureCollection>(asset.text);
            if (collection?.type != "FeatureCollection" || collection.features == null)
                throw new FormatException($"Expected a GeoJSON FeatureCollection in {asset.name}.");
            return collection.features;
        }

        public static Point ReadSpawn(TextAsset asset)
        {
            var features = ReadFeatures(asset);
            if (features.Length != 1) throw new FormatException("spawn_point.geojson must contain exactly one point.");
            return new Point(features[0]);
        }

        public static Dictionary<Vector2Int, List<Point>> ReadSites(TextAsset asset)
        {
            var result = new Dictionary<Vector2Int, List<Point>>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var assets = ReadFeatures(asset);
            foreach (var feature in assets)
            {
                if (feature?.properties?.site_type != "Horizon Chain node") continue;
                var point = new Point(feature);
                if (string.IsNullOrWhiteSpace(point.Id) || string.IsNullOrWhiteSpace(point.Name) || !ids.Add(point.Id))
                    throw new FormatException("Each Horizon Chain node needs a unique id and a name.");
                if (!result.TryGetValue(point.Tile, out var points))
                    result.Add(point.Tile, points = new List<Point>());
                points.Add(point);
            }
            return result;
        }

        internal sealed class Elevation
        {
            private readonly float[] _pixels;
            private readonly ControlPointSurface _surface;
            public readonly float Min;
            public readonly float Max;

            public Elevation(TextAsset asset)
            {
                _pixels = new float[RasterSize * RasterSize];
                var bytes = asset ? asset.bytes : null;
                if (bytes == null || bytes.Length != _pixels.Length * sizeof(float) || !BitConverter.IsLittleEndian)
                    throw new FormatException("Expected the Yanorra 2000 x 2000 little-endian Float32 elevation asset.");
                Buffer.BlockCopy(bytes, 0, _pixels, 0, bytes.Length);
                Min = float.PositiveInfinity;
                Max = float.NegativeInfinity;
                foreach (float value in _pixels)
                {
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new FormatException("The Yanorra elevation asset contains missing or invalid heights.");
                    Min = Math.Min(Min, value);
                    Max = Math.Max(Max, value);
                }
            }

            public Elevation(TextAsset controlPoints, TextAsset fallback)
            {
                _surface = new ControlPointSurface(controlPoints, fallback);
                Min = _surface.Min;
                Max = _surface.Max;
            }

            public float Sample(double east, double north)
            {
                if (_surface != null) return _surface.SampleHeight(east, north);
                // GeoTIFF rows run from north to south. Values are stored at pixel centers.
                double x = Math.Max(0, Math.Min(RasterSize - 1, east / CellSize - 0.5));
                double z = Math.Max(0, Math.Min(RasterSize - 1, (WorldSize - north) / CellSize - 0.5));
                int x0 = (int)x, z0 = (int)z;
                int x1 = Math.Min(x0 + 1, RasterSize - 1), z1 = Math.Min(z0 + 1, RasterSize - 1);
                float a = Mathf.Lerp(_pixels[z0 * RasterSize + x0], _pixels[z0 * RasterSize + x1], (float)(x - x0));
                float b = Mathf.Lerp(_pixels[z1 * RasterSize + x0], _pixels[z1 * RasterSize + x1], (float)(x - x0));
                return Mathf.Lerp(a, b, (float)(z - z0));
            }
        }
    }
}
