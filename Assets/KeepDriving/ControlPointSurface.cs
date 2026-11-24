using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace KeepDriving
{
    internal sealed class ControlPointSurface
    {
        private const int WorldSize = YanorraMap.WorldSize;
        private const int BucketSize = 10000;
        private const int BucketCount = WorldSize / BucketSize;
        private readonly List<Triangle>[] _buckets = new List<Triangle>[BucketCount * BucketCount];

        public float Min { get; }
        public float Max { get; }

        private readonly struct Sample
        {
            public readonly double X, Z;
            public readonly float Height;

            public Sample(double x, double z, float height)
            {
                X = x;
                Z = z;
                Height = height;
            }
        }

        private readonly struct Triangle
        {
            public readonly int A, B, C;

            public Triangle(int a, int b, int c)
            {
                A = a;
                B = b;
                C = c;
            }

            public IEnumerable<(int, int)> Edges()
            {
                yield return A < B ? (A, B) : (B, A);
                yield return B < C ? (B, C) : (C, B);
                yield return C < A ? (C, A) : (A, C);
            }
        }

        private readonly List<Sample> _samples = new();

        public ControlPointSurface(TextAsset asset, TextAsset fallback)
        {
            using var reader = JsonReaderWriterFactory.CreateJsonReader(asset.bytes, XmlDictionaryReaderQuotas.Max);
            var collection = XElement.Load(reader);
            if ((string)collection.Element("type") != "FeatureCollection" || collection.Element("features") == null)
                throw new FormatException("Expected a GeoJSON FeatureCollection for elevation control points.");

            var positions = new Dictionary<(double, double), float>();
            foreach (var feature in collection.Element("features").Elements("item"))
            {
                var geometry = feature.Element("geometry");
                if ((string)geometry?.Element("type") != "Point" || geometry.Element("coordinates") == null)
                    continue;
                using var values = geometry.Element("coordinates").Elements("item").GetEnumerator();
                if (!values.MoveNext() || !TryNumber(values.Current.Value, out double east) ||
                    !values.MoveNext() || !TryNumber(values.Current.Value, out double north) ||
                    east < 0 || east > WorldSize || north < 0 || north > WorldSize ||
                    !TryNumber((string)feature.Element("properties")?.Element("height_m"), out double height) ||
                    height < -float.MaxValue || height > float.MaxValue)
                    continue;
                positions[(east, north)] = (float)height;
            }
            if (positions.Count == 0) throw new FormatException("No valid elevation control points were found.");

            YanorraMap.Elevation fallbackElevation = null;
            foreach (var corner in new[] { (0d, 0d), ((double)WorldSize, 0d), (0d, (double)WorldSize), ((double)WorldSize, (double)WorldSize) })
            {
                if (positions.ContainsKey(corner)) continue;
                if (fallback && fallbackElevation == null) fallbackElevation = new YanorraMap.Elevation(fallback);
                positions.Add(corner, fallbackElevation?.Sample(corner.Item1, corner.Item2) ?? 0f);
            }

            Min = float.PositiveInfinity;
            Max = float.NegativeInfinity;
            foreach (var point in positions)
            {
                _samples.Add(new Sample(point.Key.Item1, point.Key.Item2, point.Value));
                Min = Math.Min(Min, point.Value);
                Max = Math.Max(Max, point.Value);
            }
            Triangulate();
        }

        private static bool TryNumber(string value, out double number) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
            !double.IsNaN(number) && !double.IsInfinity(number);

        private void Triangulate()
        {
            int count = _samples.Count;
            // Bowyer–Watson starts with a triangle large enough to enclose the entire map.
            _samples.Add(new Sample(-WorldSize * 16, -WorldSize * 8, 0));
            _samples.Add(new Sample(WorldSize * 17, -WorldSize * 8, 0));
            _samples.Add(new Sample(WorldSize * .5, WorldSize * 17, 0));
            var triangles = new List<Triangle> { new Triangle(count, count + 1, count + 2) };
            var perimeter = new Dictionary<(int, int), int>();
            for (int index = 0; index < count; index++)
            {
                perimeter.Clear();
                for (int i = triangles.Count - 1; i >= 0; i--)
                {
                    Triangle triangle = triangles[i];
                    if (!InCircumcircle(triangle, _samples[index])) continue;
                    foreach (var edge in triangle.Edges())
                    {
                        perimeter.TryGetValue(edge, out int occurrences);
                        perimeter[edge] = occurrences + 1;
                    }
                    triangles.RemoveAt(i);
                }

                foreach (var edge in perimeter)
                {
                    if (edge.Value != 1) continue;
                    var (a, b) = edge.Key;
                    double turn = Cross(_samples[a], _samples[b], _samples[index]);
                    if (turn > 0) triangles.Add(new Triangle(a, b, index));
                    else if (turn < 0) triangles.Add(new Triangle(b, a, index));
                }
            }

            foreach (var triangle in triangles)
            {
                if (triangle.A >= count || triangle.B >= count || triangle.C >= count) continue;
                Sample a = _samples[triangle.A], b = _samples[triangle.B], c = _samples[triangle.C];
                int minX = Bucket(Math.Min(a.X, Math.Min(b.X, c.X))), maxX = Bucket(Math.Max(a.X, Math.Max(b.X, c.X)));
                int minZ = Bucket(Math.Min(a.Z, Math.Min(b.Z, c.Z))), maxZ = Bucket(Math.Max(a.Z, Math.Max(b.Z, c.Z)));
                for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    (_buckets[z * BucketCount + x] ??= new List<Triangle>()).Add(triangle);
            }
            _samples.RemoveRange(count, 3);
        }

        private bool InCircumcircle(Triangle triangle, Sample p)
        {
            Sample a = _samples[triangle.A], b = _samples[triangle.B], c = _samples[triangle.C];
            double ax = a.X - p.X, az = a.Z - p.Z;
            double bx = b.X - p.X, bz = b.Z - p.Z;
            double cx = c.X - p.X, cz = c.Z - p.Z;
            return (ax * ax + az * az) * (bx * cz - bz * cx)
                 - (bx * bx + bz * bz) * (ax * cz - az * cx)
                 + (cx * cx + cz * cz) * (ax * bz - az * bx) > 0;
        }

        private static double Cross(Sample a, Sample b, Sample c) =>
            (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

        private static int Bucket(double position) => Math.Min((int)(position / BucketSize), BucketCount - 1);

        public float SampleHeight(double east, double north)
        {
            var point = new Sample(Math.Clamp(east, 0, WorldSize), Math.Clamp(north, 0, WorldSize), 0);
            var candidates = _buckets[Bucket(point.Z) * BucketCount + Bucket(point.X)];
            if (candidates != null)
            {
                foreach (var triangle in candidates)
                {
                    Sample a = _samples[triangle.A], b = _samples[triangle.B], c = _samples[triangle.C];
                    double area = Cross(a, b, c);
                    double u = Cross(b, c, point) / area, v = Cross(c, a, point) / area, w = Cross(a, b, point) / area;
                    if (u >= -1e-10 && v >= -1e-10 && w >= -1e-10)
                        return (float)(u * a.Height + v * b.Height + w * c.Height);
                }
            }

            // This also handles numerical degeneracy at coincident or nearly collinear inputs.
            double nearest = double.PositiveInfinity;
            float height = 0;
            foreach (var sample in _samples)
            {
                double dx = sample.X - point.X, dz = sample.Z - point.Z;
                double distance = dx * dx + dz * dz;
                if (distance >= nearest) continue;
                nearest = distance;
                height = sample.Height;
            }
            return height;
        }
    }
}
