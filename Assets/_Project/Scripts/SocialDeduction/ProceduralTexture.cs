using System;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 程序化纹理生成器：为砖墙、混凝土、木纹、沥青生成 Texture2D，
    /// 并自动绑定到 MaterialFactory 的对应材质。
    /// 所有纹理使用线性空间、无压缩，保证街景写实感。
    /// </summary>
    public static class ProceduralTexture
    {
        public static bool IsAvailable { get; private set; } = true;

        private static readonly System.Random Rng = new System.Random();

        private static readonly System.Collections.Generic.Dictionary<
            MaterialFactory.MaterialPreset, Texture2D> Cache =
            new System.Collections.Generic.Dictionary<
                MaterialFactory.MaterialPreset, Texture2D>();

        // ─── 公开接口 ────────────────────────────────

        /// <summary>
        /// 生成指定预设的程序化纹理（不缓存，每次返回新实例）。
        /// </summary>
        public static Texture2D Generate(
            MaterialFactory.MaterialPreset preset,
            int width = 512,
            int height = 512)
        {
            width  = Mathf.Clamp(width,  64, 2048);
            height = Mathf.Clamp(height, 64, 2048);

            switch (preset)
            {
                case MaterialFactory.MaterialPreset.BrickWall:
                    return GenerateBrickTexture(width, height);
                case MaterialFactory.MaterialPreset.Concrete:
                    return GenerateConcreteTexture(width, height);
                case MaterialFactory.MaterialPreset.Wood:
                    return GenerateWoodGrainTexture(width, height);
                case MaterialFactory.MaterialPreset.Asphalt:
                    return GenerateAsphaltTexture(width, height);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 获取缓存的纹理（无则生成并缓存）。
        /// </summary>
        public static Texture2D GetTexture(MaterialFactory.MaterialPreset preset)
        {
            if (Cache.TryGetValue(preset, out Texture2D cached) && cached != null)
                return cached;
            Texture2D tex = Generate(preset);
            if (tex != null) Cache[preset] = tex;
            return tex;
        }

        /// <summary>
        /// 预生成并缓存所有支持的程序化纹理。
        /// 建议在 BuildWorld 早期调用。
        /// </summary>
        public static void PreWarmAll(int size = 512)
        {
            MaterialFactory.MaterialPreset[] presets =
            {
                MaterialFactory.MaterialPreset.BrickWall,
                MaterialFactory.MaterialPreset.Concrete,
                MaterialFactory.MaterialPreset.Wood,
                MaterialFactory.MaterialPreset.Asphalt
            };
            foreach (MaterialFactory.MaterialPreset p in presets)
            {
                if (!Cache.ContainsKey(p)) GetTexture(p);
            }
        }

        /// <summary>清除所有缓存纹理。</summary>
        public static void ClearCache()
        {
            foreach (Texture2D t in Cache.Values)
                if (t != null) UnityEngine.Object.DestroyImmediate(t);
            Cache.Clear();
        }

        // ─── 砖墙纹理 ────────────────────────────────

        /// <summary>
        /// 砖墙纹理：交错排列的砖块 + 灰缝。
        /// 每块砖带轻微随机色差，模拟真实砖墙。
        /// </summary>
        private static Texture2D GenerateBrickTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            tex.filterMode   = FilterMode.Bilinear;
            tex.wrapMode     = TextureWrapMode.Repeat;

            // 砖块尺寸（像素）
            int brickW = w / 8;     // 每行 8 块砖
            int brickH = h / 12;    // 共 12 行砖
            int mortar = Math.Max(1, brickW / 8); // 灰缝宽度

            Color mortarColor = new Color(0.45f, 0.44f, 0.42f, 1f); // 灰缝

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 当前砖块行列（考虑交错）
                    int col = x / (brickW + mortar);
                    int row = y / (brickH + mortar);

                    // 奇数行偏移半块砖
                    int offsetX = (row % 2 == 1) ? (brickW + mortar) / 2 : 0;
                    int localX = (x + offsetX) % (brickW + mortar);
                    int localY = y % (brickH + mortar);

                    bool inMortar = (localX >= brickW) || (localY >= brickH);

                    if (inMortar)
                    {
                        tex.SetPixel(x, y, mortarColor);
                    }
                    else
                    {
                        // 砖块底色 + 随机色差
                        float r = 0.56f + (float)Rng.NextDouble() * 0.12f - 0.06f;
                        float g = 0.28f + (float)Rng.NextDouble() * 0.08f - 0.04f;
                        float b = 0.20f + (float)Rng.NextDouble() * 0.06f - 0.03f;
                        tex.SetPixel(x, y, new Color(r, g, b, 1f));
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        // ─── 混凝土纹理 ──────────────────────────────

        /// <summary>
        /// 混凝土纹理：多层 Perlin Noise 叠加，模拟粗糙灰白表面。
        /// 含细小气孔和颜色微变。
        /// </summary>
        private static Texture2D GenerateConcreteTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            tex.filterMode   = FilterMode.Bilinear;
            tex.wrapMode     = TextureWrapMode.Repeat;

            // 种子随机偏移，保证每次生成不同
            int seed = Rng.Next(10000);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)(x + seed) / w * 4f;
                    float ny = (float)(y + seed) / h * 4f;

                    // 多层噪声叠加
                    float n1 = Mathf.PerlinNoise(nx, ny) * 0.5f;
                    float n2 = Mathf.PerlinNoise(nx * 2.3f, ny * 2.3f) * 0.25f;
                    float n3 = Mathf.PerlinNoise(nx * 5.7f, ny * 5.7f) * 0.12f;
                    float noise = n1 + n2 + n3;

                    // 基础灰白 + 噪声偏移
                    float baseGray = 0.48f;
                    float gray = Mathf.Clamp01(baseGray + (noise - 0.42f) * 0.15f);

                    // 随机小气孔（暗点）
                    if (Rng.NextDouble() < 0.002)
                    {
                        gray *= 0.7f;
                    }

                    tex.SetPixel(x, y, new Color(gray, gray, gray * 0.97f, 1f));
                }
            }

            tex.Apply();
            return tex;
        }

        // ─── 木纹纹理 ────────────────────────────────

        /// <summary>
        /// 木纹纹理：沿 X 方向的条纹 + 结疤 + 颜色渐变，
        /// 模拟真实木材截面。
        /// </summary>
        private static Texture2D GenerateWoodGrainTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            tex.filterMode   = FilterMode.Bilinear;
            tex.wrapMode     = TextureWrapMode.Repeat;

            int seed = Rng.Next(10000);

            // 木纹周期（像素）
            float period = h * 0.08f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 沿 Y 方向的木纹正弦波
                    float wave = Mathf.Sin((y + seed) * Mathf.PI * 2f / period);
                    wave += Mathf.Sin((y + seed) * Mathf.PI * 2f / (period * 0.37f)) * 0.5f;
                    wave += (Mathf.PerlinNoise((x + seed) * 0.02f, (y + seed) * 0.02f) - 0.5f) * 0.3f;

                    // 映射到棕褐色范围
                    float wood = Mathf.Clamp01(0.38f + wave * 0.12f);

                    // 随机结疤（深色圆斑）
                    float dx = (x - w * 0.5f) / w;
                    float dy = (y - h * 0.5f) / h;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Rng.NextDouble() < 0.0003 && dist < 0.15f)
                    {
                        wood *= 0.55f;
                    }

                    float r = wood + 0.08f;
                    float g = wood - 0.04f;
                    float b = wood - 0.10f;
                    r = Mathf.Clamp01(r);
                    g = Mathf.Clamp01(g);
                    b = Mathf.Clamp01(b);

                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }

            tex.Apply();
            return tex;
        }

        // ─── 沥青纹理 ────────────────────────────────

        /// <summary>
        /// 沥青纹理：深色基底 + 高频噪声模拟石子颗粒 + 随机亮斑。
        /// </summary>
        private static Texture2D GenerateAsphaltTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            tex.filterMode   = FilterMode.Bilinear;
            tex.wrapMode     = TextureWrapMode.Repeat;

            int seed = Rng.Next(10000);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)(x + seed) / w * 6f;
                    float ny = (float)(y + seed) / h * 6f;

                    // 高频噪声 → 石子颗粒感
                    float n1 = Mathf.PerlinNoise(nx * 3f, ny * 3f);
                    float n2 = Mathf.PerlinNoise(nx * 7f, ny * 7f);
                    float grain = (n1 + n2) * 0.5f;

                    // 深色基底
                    float baseDark = 0.14f;
                    float dark = Mathf.Clamp01(baseDark + (grain - 0.5f) * 0.08f);

                    // 随机亮斑（小石子反光）
                    if (Rng.NextDouble() < 0.0015)
                    {
                        dark += 0.12f;
                    }

                    tex.SetPixel(x, y, new Color(dark, dark, dark * 0.96f, 1f));
                }
            }

            tex.Apply();
            return tex;
        }
    }
}