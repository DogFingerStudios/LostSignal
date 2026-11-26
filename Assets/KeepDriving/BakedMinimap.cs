using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;

namespace KeepDriving
{
    internal sealed class BakedMinimap : IDisposable
    {
        internal const string FileName = "YanorraMinimap.bytes";
        internal const int Magic = 0x594D5031;
        internal static readonly int[] Sides = { 1, 8, 64 };
        internal static readonly int[] Resolutions = { 512, 256, 128 };
        internal const int TileCount = 1 + 64 + 4096;
        private const int Capacity = 48;

        private sealed class Entry
        {
            public Texture2D Texture;
            public int LastUsed;
        }

        private readonly Dictionary<int, Entry> _cache = new();
        private readonly string _path = Path.Combine(Application.streamingAssetsPath, FileName);
        private Task<byte[]> _pending;
        private int _requestedId, _requestedResolution;
        private bool _failed;

        public BakedMinimap() => Request(0, Resolutions[0]);

        internal static int TileId(int level, int x, int z) =>
            (level == 0 ? 0 : level == 1 ? 1 : 65) + z * Sides[level] + x;

        internal static Rect TileBounds(int level, int x, int z)
        {
            float span = YanorraMap.WorldSize / (float)Sides[level];
            return new Rect(x * span, z * span, span, span);
        }

        public void Pump()
        {
            if (_pending?.IsCompleted != true) return;
            var finished = _pending;
            _pending = null;
            if (finished.IsFaulted)
            {
                _failed = true;
                Debug.LogWarning($"Could not load minimap relief. Bake it from KeepDriving > Bake Minimap Relief. {finished.Exception?.GetBaseException().Message}");
                return;
            }

            int size = _requestedResolution + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                name = $"Baked minimap tile {_requestedId}", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            texture.LoadRawTextureData(finished.Result);
            texture.Apply(false, true);
            _cache.Add(_requestedId, new Entry { Texture = texture, LastUsed = Time.frameCount });
            if (_cache.Count <= Capacity) return;

            int oldest = -1, frame = int.MaxValue;
            foreach (var (id, entry) in _cache)
            {
                if (id == 0 || id == _requestedId || entry.LastUsed >= frame) continue;
                oldest = id;
                frame = entry.LastUsed;
            }
            UnityEngine.Object.Destroy(_cache[oldest].Texture);
            _cache.Remove(oldest);
        }

        public void Draw(Rect map, Rect view)
        {
            if (!_cache.TryGetValue(0, out var overview)) return;
            DrawTile(overview.Texture, TileBounds(0, 0, 0), map, view);
            int level = SelectLevel(view.width);
            if (level == 0) return;

            // Keep regional imagery visible while finer tiles arrive.
            if (level == 2) DrawLevel(1, map, view, false);
            DrawLevel(level, map, view, true);
        }

        public bool Prefetch(Vector2 center, float width)
        {
            if (_failed) return true;
            if (!_cache.ContainsKey(0)) return false;
            int level = SelectLevel(width);
            if (level == 0) return true;

            int side = Sides[level];
            float span = YanorraMap.WorldSize / (float)side;
            int x = Mathf.Clamp(Mathf.FloorToInt(center.x / span), 0, side - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(center.y / span), 0, side - 1);
            Request(TileId(level, x, z), Resolutions[level]);
            return true;
        }

        private static int SelectLevel(float width) => width <= 6250f ? 2 : width <= 50000f ? 1 : 0;

        private void DrawLevel(int level, Rect map, Rect view, bool load)
        {
            int side = Sides[level];
            float span = YanorraMap.WorldSize / (float)side;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(view.xMin / span));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(view.yMin / span));
            int x1 = Mathf.Min(side - 1, Mathf.CeilToInt(view.xMax / span) - 1);
            int z1 = Mathf.Min(side - 1, Mathf.CeilToInt(view.yMax / span) - 1);
            int next = -1;
            float distance = float.PositiveInfinity;

            for (int z = Mathf.Max(0, z0 - 1); z <= Mathf.Min(side - 1, z1 + 1); z++)
            for (int x = Mathf.Max(0, x0 - 1); x <= Mathf.Min(side - 1, x1 + 1); x++)
            {
                bool visible = x >= x0 && x <= x1 && z >= z0 && z <= z1;
                int id = TileId(level, x, z);
                Rect tile = TileBounds(level, x, z);
                if (_cache.TryGetValue(id, out var entry))
                {
                    if (visible)
                    {
                        entry.LastUsed = Time.frameCount;
                        DrawTile(entry.Texture, tile, map, view);
                    }
                }
                else if (load)
                {
                    float priority = (tile.center - view.center).sqrMagnitude + (visible ? 0 : 16f * span * span);
                    if (priority >= distance) continue;
                    next = id;
                    distance = priority;
                }
            }
            if (next >= 0) Request(next, Resolutions[level]);
        }

        private static void DrawTile(Texture2D texture, Rect tile, Rect map, Rect view)
        {
            Rect clipped = Rect.MinMaxRect(Mathf.Max(tile.xMin, view.xMin), Mathf.Max(tile.yMin, view.yMin),
                Mathf.Min(tile.xMax, view.xMax), Mathf.Min(tile.yMax, view.yMax));
            if (clipped.width <= 0 || clipped.height <= 0) return;
            float scale = map.width / view.width;
            var target = new Rect(map.xMin + (clipped.xMin - view.xMin) * scale,
                map.yMax - (clipped.yMax - view.yMin) * scale, clipped.width * scale, clipped.height * scale);
            float pixel = 1f / texture.width;
            float usable = 1f - 2f * pixel;
            var uv = new Rect(pixel + (clipped.xMin - tile.xMin) / tile.width * usable,
                pixel + (clipped.yMin - tile.yMin) / tile.height * usable,
                clipped.width / tile.width * usable, clipped.height / tile.height * usable);
            GUI.DrawTextureWithTexCoords(target, texture, uv, false);
        }

        private void Request(int id, int resolution)
        {
            if (_failed || _pending != null || _cache.ContainsKey(id)) return;
            _requestedId = id;
            _requestedResolution = resolution;
            string path = _path;
            _pending = Task.Run(() => ReadTile(path, id, resolution + 2));
        }

        internal static byte[] ReadTile(string path, int id, int size)
        {
            using var file = File.OpenRead(path);
            using var reader = new BinaryReader(file);
            if (reader.ReadInt32() != Magic || reader.ReadInt32() != TileCount)
                throw new InvalidDataException("Unknown minimap archive format.");
            file.Position = 8 + id * sizeof(long);
            long offset = reader.ReadInt64();
            file.Position = offset;
            int length = reader.ReadInt32();
            if (length <= 0 || length > file.Length - file.Position)
                throw new InvalidDataException("Invalid minimap tile length.");
            using var compressed = new MemoryStream(reader.ReadBytes(length));
            using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
            var pixels = new byte[size * size * 3];
            for (int start = 0; start < pixels.Length;)
            {
                int read = deflate.Read(pixels, start, pixels.Length - start);
                if (read == 0) throw new InvalidDataException("Incomplete minimap tile.");
                start += read;
            }
            return pixels;
        }

        public void Dispose()
        {
            foreach (var entry in _cache.Values) UnityEngine.Object.Destroy(entry.Texture);
            _cache.Clear();
        }
    }
}
