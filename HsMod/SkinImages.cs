using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using UnityEngine;

namespace HsMod
{
    //皮肤缩略图服务：WebServer 后台线程调用，加载任务经主线程调度执行（Unity API 必须主线程）。
    //DefLoader/CardBackManager 的回调也发生在主线程，因此请求线程不能同步等待，改为轮询缓存：
    //主线程完成加载与 PNG 编码后写入缓存，请求线程轮询取回（前端一个请求等到最终结果）。
    //Unity 裁剪版无 EncodeToPNG，PNG 手动编码（zlib + CRC32），超宽纹理自动降采样。
    public static class SkinImages
    {
        private sealed class CacheEntry
        {
            public byte[] Bytes;    //null 表示已尝试但加载失败
            public DateTime FetchedAt;
        }

        private static readonly object s_lock = new object();
        private static readonly Dictionary<string, CacheEntry> s_cache = new Dictionary<string, CacheEntry>();
        private static readonly HashSet<string> s_pending = new HashSet<string>();
        private const int CacheMax = 400;
        private const int MaxWidth = 256;    //缩略图最大宽度，超宽降采样
        private const int FailRetrySeconds = 300;    //失败条目 5 分钟后允许重试

        //磁盘缓存：图片获取成功后落盘，后续请求先读本地文件，避免重复从游戏加载
        private static string CacheDir => Path.Combine(BepInEx.Paths.ConfigPath, "skinCache");
        private static string CachePath(string key) => Path.Combine(CacheDir, key + ".png");

        public static byte[] GetImage(string type, int id)
        {
            if (id <= 0) return null;
            string key = type + "_" + id;

            byte[] disk = ReadDiskCache(key);
            if (disk != null) return disk;

            lock (s_lock)
            {
                if (s_cache.TryGetValue(key, out CacheEntry entry))
                {
                    if (entry.Bytes != null) return entry.Bytes;
                    if ((DateTime.UtcNow - entry.FetchedAt).TotalSeconds < FailRetrySeconds) return null;
                    s_cache.Remove(key);    //过期失败条目，允许重试
                }
                if (s_pending.Contains(key)) return null;    //正在加载，跳过
                s_pending.Add(key);
            }

            ModSettingsUI.RunOnMainThread(() => LoadImageAsync(key, type, id));

            //轮询缓存（主线程异步完成后写入）
            DateTime deadline = DateTime.UtcNow.AddSeconds(12);
            while (DateTime.UtcNow < deadline)
            {
                lock (s_lock)
                {
                    if (s_cache.TryGetValue(key, out CacheEntry entry))
                    {
                        s_pending.Remove(key);
                        return entry.Bytes;
                    }
                }
                Thread.Sleep(150);
            }
            lock (s_lock) s_pending.Remove(key);
            return null;
        }

        private static void LoadImageAsync(string key, string type, int id)
        {
            try
            {
                byte[] bytes = null;
                switch (type)
                {
                    case "hero":
                    case "bgsHero":
                    case "bob":
                    case "coin":
                    case "pet":
                        bytes = LoadCardImage(id);
                        break;
                    case "cardBack":
                        bytes = LoadCardBackImage(id);
                        break;
                    case "bgsBoard":
                        bytes = LoadBoardImage(id);
                        break;
                }
                lock (s_lock)
                {
                    if (s_cache.Count >= CacheMax) s_cache.Clear();
                    s_cache[key] = new CacheEntry { Bytes = bytes, FetchedAt = DateTime.UtcNow };
                }
                if (bytes != null) WriteDiskCache(key, bytes);    //成功后落盘
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"SkinImages.LoadImageAsync({key}): {ex.Message}");
                lock (s_lock)
                {
                    if (s_cache.Count >= CacheMax) s_cache.Clear();
                    s_cache[key] = new CacheEntry { Bytes = null, FetchedAt = DateTime.UtcNow };
                }
            }
        }

        private static byte[] ReadDiskCache(string key)
        {
            try
            {
                string path = CachePath(key);
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"SkinImages.ReadDiskCache({key}): {ex.Message}");
            }
            return null;
        }

        private static void WriteDiskCache(string key, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllBytes(CachePath(key), bytes);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"SkinImages.WriteDiskCache({key}): {ex.Message}");
            }
        }

        //卡牌肖像（对战英雄/酒馆英雄/鲍勃/幸运币/宠物均以卡牌形式存在）
        private static byte[] LoadCardImage(int cardId)
        {
            string cardStringId = DefLoader.Get()?.GetEntityDef(cardId)?.GetCardId();
            if (string.IsNullOrEmpty(cardStringId)) return null;
            DefLoader.DisposableCardDef cardDef = null;
            ManualResetEventSlim evt = new ManualResetEventSlim(false);
            DefLoader.Get().LoadCardDef(cardStringId, (string loadedCardId, DefLoader.DisposableCardDef def, object userData) =>
            {
                cardDef = def;
                evt.Set();
            }, null, CardPortraitQuality.GetDefault());
            if (!evt.Wait(10000)) return null;
            if (cardDef?.CardDef == null) return null;
            try
            {
                string path = cardDef.CardDef.PortraitTexturePath;
                if (string.IsNullOrEmpty(path)) return null;
                Texture2D tex = AssetLoader.Get()?.LoadAsset<Texture2D>(AssetReference.CreateFromAssetString(path), AssetLoadingOptions.None);
                return EncodePng(tex);
            }
            finally
            {
                cardDef.Dispose();
            }
        }

        //卡背：通过 CardBackManager 加载，取 m_CardBackTexture
        private static byte[] LoadCardBackImage(int cardBackId)
        {
            CardBack cardBack = null;
            ManualResetEventSlim evt = new ManualResetEventSlim(false);
            bool ok = CardBackManager.Get()?.LoadCardBackByIndex(cardBackId, (CardBackManager.LoadCardBackData data) =>
            {
                cardBack = data?.m_CardBack;
                evt.Set();
            }) ?? false;
            if (!ok || !evt.Wait(10000)) return null;
            return EncodePng(cardBack?.m_CardBackTexture);
        }

        //酒馆面板：直接加载 DetailsTexture 资源
        private static byte[] LoadBoardImage(int boardSkinId)
        {
            var record = GameDbf.BattlegroundsBoardSkin.GetRecord(boardSkinId);
            string path = record?.DetailsTexture;
            if (string.IsNullOrEmpty(path)) return null;
            Texture2D tex = AssetLoader.Get()?.LoadAsset<Texture2D>(AssetReference.CreateFromAssetString(path), AssetLoadingOptions.None);
            return EncodePng(tex);
        }

        //获取像素并编码 PNG（超宽降采样），纹理不可读/异常返回 null
        private static byte[] EncodePng(Texture2D tex)
        {
            if (tex == null) return null;
            try
            {
                Color32[] pixels = tex.GetPixels32();
                int w = tex.width;
                int h = tex.height;
                if (w > MaxWidth)
                {
                    int nw = MaxWidth;
                    int nh = Math.Max(1, (int)((long)h * nw / w));
                    pixels = Downsample(pixels, w, h, nw, nh);
                    w = nw;
                    h = nh;
                }
                return EncodePng(pixels, w, h);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"SkinImages.EncodePng: {ex.Message}");
                return null;
            }
        }

        private static Color32[] Downsample(Color32[] src, int sw, int sh, int dw, int dh)
        {
            Color32[] dst = new Color32[dw * dh];
            for (int y = 0; y < dh; y++)
            {
                int y0 = (int)((long)y * sh / dh);
                int y1 = Math.Min(sh - 1, (int)((long)(y + 1) * sh / dh));
                for (int x = 0; x < dw; x++)
                {
                    int x0 = (int)((long)x * sw / dw);
                    int x1 = Math.Min(sw - 1, (int)((long)(x + 1) * sw / dw));
                    long r = 0, g = 0, b = 0, a = 0;
                    int count = 0;
                    for (int yy = y0; yy <= y1; yy++)
                    {
                        int rowBase = yy * sw;
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            Color32 c = src[rowBase + xx];
                            r += c.r;
                            g += c.g;
                            b += c.b;
                            a += c.a;
                            count++;
                        }
                    }
                    dst[y * dw + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count));
                }
            }
            return dst;
        }

        //手动 PNG 编码（RGBA8）：Unity 裁剪版无 EncodeToPNG/EncodeToJPG
        private static byte[] EncodePng(Color32[] pixels, int w, int h)
        {
            byte[] raw = new byte[(w * 4 + 1) * h];
            int idx = 0;
            for (int y = 0; y < h; y++)
            {
                raw[idx++] = 0;    //filter: None
                int baseIdx = y * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = pixels[baseIdx + x];
                    raw[idx++] = c.r;
                    raw[idx++] = c.g;
                    raw[idx++] = c.b;
                    raw[idx++] = c.a;
                }
            }

            byte[] zlib = ZlibCompress(raw);

            MemoryStream ms = new MemoryStream();
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            ms.Write(signature, 0, signature.Length);
            byte[] ihdr = new byte[13];    //bit depth 8, color type 6 (RGBA)
            WriteInt32BE(ihdr, 0, w);
            WriteInt32BE(ihdr, 4, h);
            ihdr[8] = 8;
            ihdr[9] = 6;
            WriteChunk(ms, "IHDR", ihdr);
            WriteChunk(ms, "IDAT", zlib);
            WriteChunk(ms, "IEND", new byte[0]);
            return ms.ToArray();
        }

        private static byte[] ZlibCompress(byte[] raw)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (DeflateStream ds = new DeflateStream(ms, CompressionLevel.Fastest, true))
                {
                    ds.Write(raw, 0, raw.Length);
                }
                uint adler = Adler32(raw);
                ms.WriteByte((byte)(adler >> 24));
                ms.WriteByte((byte)(adler >> 16));
                ms.WriteByte((byte)(adler >> 8));
                ms.WriteByte((byte)adler);
                return ms.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte v in data)
            {
                a = (a + v) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private static void WriteInt32BE(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static readonly uint[] s_crcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        private static void WriteChunk(MemoryStream ms, string type, byte[] data)
        {
            byte[] len = new byte[4];
            WriteInt32BE(len, 0, data.Length);
            ms.Write(len, 0, 4);
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            ms.Write(typeBytes, 0, 4);
            ms.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < 4; i++)
                crc = s_crcTable[(crc ^ typeBytes[i]) & 0xFF] ^ (crc >> 8);
            foreach (byte v in data)
                crc = s_crcTable[(crc ^ v) & 0xFF] ^ (crc >> 8);
            byte[] crcBytes = new byte[4];
            WriteInt32BE(crcBytes, 0, (int)(crc ^ 0xFFFFFFFF));
            ms.Write(crcBytes, 0, 4);
        }
    }
}
