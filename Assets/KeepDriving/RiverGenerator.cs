using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace KeepDriving
{
    internal sealed class RiverGenerator
    {
        private const int Grid = 64;
        private const float RiverSpacing = 1200f;
        private const float LakeSpacing = 1800f;
        private const float BankWidth = 18f;
        private readonly int _seed;
        private readonly float _coverage;
        private readonly Vector2 _tileSize;
        private readonly float _lakeOffset;
        private readonly float _riverOrigin;
        private readonly float _dryRadius;

        public RiverGenerator(int seed, float coverage, Vector2 tileSize, float spawnClearingRadius)
        {
            _seed = seed;
            _coverage = Mathf.Clamp01(coverage / 100f);
            _tileSize = tileSize;
            _lakeOffset = Random01(0, 0, 1) * LakeSpacing;
            _riverOrigin = tileSize.y * .5f + (Random01(0, 0, 9) - .5f) * RiverSpacing;
            // Account for the bank blend and shoreline-grid interpolation around the clearing.
            _dryRadius = spawnClearingRadius + BankWidth + tileSize.magnitude / Grid;
        }

        public Tile GenerateTile(Vector2Int key, CancellationToken token)
        {
            if (_coverage == 0) return null;
            var samples = new float[Grid + 1, Grid + 1];
            var levels = new float[Grid + 1];
            float closest = float.NegativeInfinity;
            for (int x = 0; x <= Grid; x++)
                levels[x] = WaterLevel((key.x + x / (float)Grid) * _tileSize.x);

            for (int z = 0; z <= Grid; z++)
            {
                token.ThrowIfCancellationRequested();
                float worldZ = (key.y + z / (float)Grid) * _tileSize.y;
                for (int x = 0; x <= Grid; x++)
                {
                    float worldX = (key.x + x / (float)Grid) * _tileSize.x;
                    float distance = ShoreDistance(worldX, worldZ);
                    samples[z, x] = distance;
                    closest = Mathf.Max(closest, distance);
                }
            }
            if (closest <= -BankWidth) return null;
            return new Tile(samples, levels, _tileSize);
        }

        // Positive distance means water. Coordinates are measured from the starting tile's southwest corner.
        private float ShoreDistance(float x, float z)
        {
            float dx = x - _tileSize.x * .5f;
            float dz = z - _tileSize.y * .5f;
            float protectedShore = Mathf.Sqrt(dx * dx + dz * dz) - _dryRadius;
            if (_coverage >= 1f) return protectedShore;

            float relativeZ = z - _riverOrigin;
            int lane = Mathf.FloorToInt(relativeZ / RiverSpacing);
            float lakePosition = (x - _lakeOffset) / LakeSpacing;
            int lake = Mathf.RoundToInt(lakePosition);
            float lakeX = lake * LakeSpacing + _lakeOffset;
            float riverHalfWidth = RiverSpacing * .5f * _coverage * (.82f + .18f * _coverage);
            float lakeScale = Mathf.Sqrt(_coverage);
            float distance = float.NegativeInfinity;
            for (int river = lane - 1; river <= lane + 1; river++)
            {
                float riverZ = RiverCenter(river, x);
                distance = Mathf.Max(distance, riverHalfWidth - Mathf.Abs(z - riverZ));

                float branchStart = lakeX - 650f;
                float branchEnd = lakeX - 150f;
                if (x > branchStart - BankWidth && x < branchEnd + BankWidth
                    && Random01(river, lake, 7) < .55f)
                {
                    float progress = Mathf.InverseLerp(branchStart, branchEnd, x);
                    float side = Random01(river, lake, 8) < .5f ? -1f : 1f;
                    float branchZ = riverZ + side * Mathf.Lerp(245f, 0f, progress);
                    float width = riverHalfWidth * Mathf.Lerp(.15f, .34f, progress);
                    float branch = width - Mathf.Abs(z - branchZ);
                    distance = Mathf.Max(distance, Mathf.Min(branch, x - branchStart, branchEnd - x));
                }

                // Alternate between reaches with a broad lake and narrower river-only reaches.
                if (Random01(river, lake, 4) < .22f) continue;
                float radiusX = (480f + 220f * Random01(river, lake, 5)) * lakeScale;
                float longAxis = (x - lakeX) / radiusX;
                float radiusZ = (220f + 160f * Random01(river, lake, 6)) * lakeScale;
                float shortAxis = (z - RiverCenter(river, lakeX)) / radiusZ;
                float ellipse = Mathf.Sqrt(longAxis * longAxis + shortAxis * shortAxis);
                distance = Mathf.Max(distance, (1f - ellipse) * Mathf.Min(radiusX, radiusZ));
            }
            return Mathf.Min(distance, protectedShore);
        }

        private float RiverCenter(int lane, float x)
        {
            float phase = Random01(lane, 0, 2) * (2f * Mathf.PI);
            float bend = 110f * Mathf.Sin(x / 335f + phase)
                + 35f * Mathf.Sin(x / 126f - phase);
            return _riverOrigin + (lane + .5f) * RiverSpacing + bend;
        }

        private float WaterLevel(float x)
        {
            if (_coverage >= 1f) return 5f;
            float position = (x - _lakeOffset) / LakeSpacing;
            int reach = Mathf.FloorToInt(position);
            // Hold a flat elevation through each potential lake and drop between reaches.
            float transition = Mathf.Clamp01(((position - reach) * LakeSpacing - 710f) / (LakeSpacing - 1420f));
            return Mathf.SmoothStep(ReachLevel(reach), ReachLevel(reach + 1), transition);
        }

        private static float ReachLevel(int reach) => 5f - 1.5f * Mathf.Atan(reach * .3f);

        private float Random01(int x, int z, uint salt)
        {
            unchecked
            {
                uint hash = (uint)_seed ^ (uint)x * 0x9e3779b9u ^ (uint)z * 0x85ebca6bu ^ salt * 0xc2b2ae35u;
                hash = (hash ^ (hash >> 16)) * 0x7feb352du;
                hash = (hash ^ (hash >> 15)) * 0x846ca68bu;
                return ((hash ^ (hash >> 16)) >> 8) / 16777216f;
            }
        }

        internal sealed class Tile
        {
            private readonly float[,] _distances;
            private readonly float[] _levels;
            private readonly Vector2 _size;

            public Tile(float[,] distances, float[] levels, Vector2 size)
            {
                _distances = distances;
                _levels = levels;
                _size = size;
            }

            public bool ContainsWater(float u, float v) => Distance(u, v) > 0f;

            public float ShapeGround(float u, float v, float height)
            {
                float distance = Distance(u, v);
                if (distance <= -BankWidth) return height;
                float x = u * Grid;
                int column = Mathf.Min((int)x, Grid - 1);
                float level = Mathf.Lerp(_levels[column], _levels[column + 1], x - column);
                if (distance >= 0f)
                    return level - Mathf.Min(3f, .12f + distance * .22f);
                float bank = level - .12f + Mathf.Min(3f, -distance * .28f);
                return Mathf.Lerp(bank, height, Mathf.SmoothStep(0f, 1f, -distance / BankWidth));
            }

            private float Distance(float u, float v)
            {
                float x = Mathf.Clamp01(u) * Grid, z = Mathf.Clamp01(v) * Grid;
                int column = Mathf.Min((int)x, Grid - 1), row = Mathf.Min((int)z, Grid - 1);
                float fx = x - column, fz = z - row;
                float se = _distances[row, column + 1], nw = _distances[row + 1, column];
                // Use the same diagonal as the surface mesh.
                if (fx + fz <= 1f)
                    return _distances[row, column] * (1f - fx - fz) + se * fx + nw * fz;
                return _distances[row + 1, column + 1] * (fx + fz - 1f) + se * (1f - fz) + nw * (1f - fx);
            }

            public Vector3[] BuildSurface(CancellationToken token)
            {
                var vertices = new List<Vector3>();
                var polygon = new Vector3[4];
                for (int z = 0; z < Grid; z++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int x = 0; x < Grid; x++)
                    {
                        float sw = _distances[z, x], nw = _distances[z + 1, x];
                        float se = _distances[z, x + 1], ne = _distances[z + 1, x + 1];
                        if (sw <= 0 && nw <= 0 && se <= 0 && ne <= 0) continue;
                        var a = Vertex(x, z);
                        var b = Vertex(x, z + 1);
                        var c = Vertex(x + 1, z);
                        var d = Vertex(x + 1, z + 1);
                        AddTriangle(a, b, c, sw, nw, se, polygon, vertices);
                        AddTriangle(c, b, d, se, nw, ne, polygon, vertices);
                    }
                }
                return vertices.ToArray();
            }

            private Vector3 Vertex(int x, int z) => new Vector3(x * _size.x / Grid, _levels[x], z * _size.y / Grid);

            private static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, float da, float db, float dc,
                Vector3[] polygon, List<Vector3> output)
            {
                int count = 0;
                AddEdge(a, b, da, db, polygon, ref count);
                AddEdge(b, c, db, dc, polygon, ref count);
                AddEdge(c, a, dc, da, polygon, ref count);
                for (int index = 1; index + 1 < count; index++)
                {
                    output.Add(polygon[0]);
                    output.Add(polygon[index]);
                    output.Add(polygon[index + 1]);
                }
            }

            private static void AddEdge(Vector3 a, Vector3 b, float da, float db, Vector3[] polygon, ref int count)
            {
                if (da > 0) polygon[count++] = a;
                if ((da > 0) != (db > 0)) polygon[count++] = Vector3.LerpUnclamped(a, b, da / (da - db));
            }
        }
    }
}
