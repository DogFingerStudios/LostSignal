using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace KeepDriving
{
    internal sealed class TreeGenerator
    {
        private const float CellSize = 12.5f;
        private const float MaxGroundGrade = .625f; // Approximately 32 degrees.
        private readonly int _seed;
        private readonly float _density;
        private readonly int _prefabCount;
        private readonly Vector3 _tileSize;
        private readonly Vector2 _woodlandOffset;
        private readonly int _columns, _rows;

        public TreeGenerator(int seed, int density, int prefabCount, Vector3 tileSize)
        {
            _seed = seed;
            _density = Mathf.Clamp01(density / 100f);
            _prefabCount = prefabCount;
            _tileSize = tileSize;
            _columns = Mathf.CeilToInt(tileSize.x / CellSize);
            _rows = Mathf.CeilToInt(tileSize.z / CellSize);
            _woodlandOffset = new Vector2(Random01(0, 0, 6), Random01(0, 0, 7)) * 256f;
        }

        public TreeInstance[] GenerateTile(Vector2Int key, float[,] heights, RiverGenerator.Tile water,
            CancellationToken token)
        {
            if (key == Vector2Int.zero || _density <= 0 || _prefabCount == 0)
                return System.Array.Empty<TreeInstance>();

            var trees = new List<TreeInstance>();
            for (int z = 0; z < _rows; z++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < _columns; x++)
                {
                    int cellX = unchecked(key.x * _columns + x);
                    int cellZ = unchecked(key.y * _rows + z);
                    float u = (x + Mathf.Lerp(.15f, .85f, Random01(cellX, cellZ, 1))) / _columns;
                    float v = (z + Mathf.Lerp(.15f, .85f, Random01(cellX, cellZ, 2))) / _rows;
                    float woods = Mathf.PerlinNoise((key.x + u) * _tileSize.x / 155f + _woodlandOffset.x,
                        (key.y + v) * _tileSize.z / 155f + _woodlandOffset.y);
                    float chance = Mathf.Lerp(_density * Mathf.SmoothStep(.08f, 1f, woods), 1f,
                        _density * _density * _density);
                    if (Random01(cellX, cellZ, 3) >= chance || water?.ContainsWater(u, v) == true
                        || !TrySampleGround(heights, u, v, out float elevation))
                        continue;

                    float scale = Mathf.Lerp(.8f, 1.25f, Random01(cellX, cellZ, 4));
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(u, elevation, v),
                        prototypeIndex = (int)(Random01(cellX, cellZ, 5) * _prefabCount),
                        widthScale = scale,
                        heightScale = scale,
                        rotation = Random01(cellX, cellZ, 8) * 2f * Mathf.PI,
                        color = Color.white,
                        lightmapColor = Color.white
                    });
                }
            }
            return trees.ToArray();
        }

        private bool TrySampleGround(float[,] heights, float u, float v, out float elevation)
        {
            int columns = heights.GetLength(1) - 1, rows = heights.GetLength(0) - 1;
            float x = u * columns, z = v * rows;
            int left = Mathf.Min((int)x, columns - 1), bottom = Mathf.Min((int)z, rows - 1);
            float fx = x - left, fz = z - bottom;
            float sw = heights[bottom, left], se = heights[bottom, left + 1];
            float nw = heights[bottom + 1, left], ne = heights[bottom + 1, left + 1];
            float south = Mathf.Lerp(sw, se, fx), north = Mathf.Lerp(nw, ne, fx);
            elevation = Mathf.Lerp(south, north, fz);
            float dx = Mathf.Lerp(se - sw, ne - nw, fz) * _tileSize.y * columns / _tileSize.x;
            float dz = (north - south) * _tileSize.y * rows / _tileSize.z;
            return dx * dx + dz * dz <= MaxGroundGrade * MaxGroundGrade;
        }

        private float Random01(int x, int z, uint salt)
        {
            unchecked
            {
                uint hash = (uint)_seed ^ (uint)x * 0x9e3779b9u ^ (uint)z * 0x85ebca6bu ^ (salt + 100u) * 0xc2b2ae35u;
                hash = (hash ^ (hash >> 16)) * 0x7feb352du;
                hash = (hash ^ (hash >> 15)) * 0x846ca68bu;
                return ((hash ^ (hash >> 16)) >> 8) / 16777216f;
            }
        }
    }
}
