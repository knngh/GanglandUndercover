using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 材质工厂：统一管理建筑/街景的 PBR 材质，提供砖墙、混凝土、铁皮、玻璃、
    /// 木材、沥青、锈铁、霓虹共 8 类预设。支持可选程序化纹理绑定。
    /// 所有材质使用 URP Lit Shader，自动回退到 Standard。
    /// </summary>
    public static class MaterialFactory
    {
        // ─── Shader 缓存 ────────────────────────────

        private static Shader _cachedShader;
        private static Shader LitShader
        {
            get
            {
                if (_cachedShader == null)
                {
                    _cachedShader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard")
                        ?? Shader.Find("Unlit/Color")
                        ?? Shader.Find("Sprites/Default");
                }
                return _cachedShader;
            }
        }

        private static readonly Dictionary<MaterialPreset, Material> PresetCache
            = new Dictionary<MaterialPreset, Material>();

        private static readonly Dictionary<MaterialPreset, Texture2D> TextureCache
            = new Dictionary<MaterialPreset, Texture2D>();

        // ─── 预设枚举 ────────────────────────────────

        public enum MaterialPreset : byte
        {
            BrickWall,      // 砖墙 — 红棕色，低金属度，中粗糙
            Concrete,        // 混凝土 — 灰白，零金属，高粗糙
            IronSheet,       // 铁皮/金属板 — 暗灰，高金属度，中粗糙
            Glass,           // 玻璃幕墙 — 半透蓝灰，低金属，高光滑
            Wood,            // 木材 — 棕褐，零金属，中粗糙
            Asphalt,         // 沥青/柏油 — 深黑灰，零金属，极高粗糙
            RustedIron,      // 锈铁 — 橙棕，中金属度，极高粗糙
            Neon             // 霓虹发光 — 纯色 + 高自发光
        }

        // ─── 预设参数结构 ────────────────────────────

        public struct PresetParams
        {
            public Color BaseColor;
            public float Metallic;
            public float Smoothness;
            public Color EmissionColor;
            public float EmissionIntensity;
            public bool UseProceduralTexture;
        }

        /// <summary>获取指定预设的 PBR 参数。</summary>
        public static PresetParams GetPresetParams(MaterialPreset preset)
        {
            switch (preset)
            {
                case MaterialPreset.BrickWall:
                    return new PresetParams
                    {
                        BaseColor = new Color(0.588f, 0.302f, 0.224f, 1f), // #964d39
                        Metallic = 0.02f,
                        Smoothness = 0.25f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = true
                    };
                case MaterialPreset.Concrete:
                    return new PresetParams
                    {
                        BaseColor = new Color(0.482f, 0.475f, 0.459f, 1f), // #7b7975
                        Metallic = 0f,
                        Smoothness = 0.12f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = true
                    };
                case MaterialPreset.IronSheet:
                    return new PresetParams
                    {
                        BaseColor = new Color(0.294f, 0.302f, 0.310f, 1f), // #4b4d4f
                        Metallic = 0.82f,
                        Smoothness = 0.35f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = false
                    };
                case MaterialPreset.Glass:
                    return new PresetParams
                    {
                        BaseColor = new Color(0.62f, 0.71f, 0.78f, 0.45f),
                        Metallic = 0.1f,
                        Smoothness = 0.88f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = false
                    };
                case MaterialPreset.Wood:
                    return new PresetParams
                    {
                        BaseColor = new Color(0.392f, 0.267f, 0.169f, 1f), // #64442b
                        Metallic = 0f,
                        Smoothness = 0.28f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = true
                    };
                case MaterialPreset.Asphalt:
                    return new PresetParams
                    {
                        BaseColor = new Color(0.141f, 0.137f, 0.133f, 1f), // #242322
                        Metallic = 0f,
                        Smoothness = 0.06f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = true
                    };
                case MaterialPreset.RustedIron:
                    return new PresetParams
                    {
                        BaseColor = new Color(0.510f, 0.259f, 0.137f, 1f), // #824223
                        Metallic = 0.35f,
                        Smoothness = 0.08f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = false
                    };
                case MaterialPreset.Neon:
                    return new PresetParams
                    {
                        BaseColor = new Color(1f, 0.22f, 0.36f, 1f),
                        Metallic = 0f,
                        Smoothness = 0.15f,
                        EmissionColor = new Color(1f, 0.22f, 0.36f, 1f),
                        EmissionIntensity = 2.8f,
                        UseProceduralTexture = false
                    };
                default:
                    return new PresetParams
                    {
                        BaseColor = Color.gray,
                        Metallic = 0f,
                        Smoothness = 0.5f,
                        EmissionColor = Color.black,
                        EmissionIntensity = 0f,
                        UseProceduralTexture = false
                    };
            }
        }

        // ─── 主入口：获取或创建材质 ────────────────────

        /// <summary>
        /// 获取指定预设的材质实例（带缓存）。若 useProceduralTexture=true 且 ProceduralTexture
        /// 可用，自动绑定程序化纹理到 mainTexture。
        /// </summary>
        public static Material GetMaterial(MaterialPreset preset)
        {
            if (PresetCache.TryGetValue(preset, out Material cached))
            {
                return new Material(cached); // 返回副本避免共享污染
            }

            PresetParams p = GetPresetParams(preset);
            Material mat = CreatePbrMaterial(p);

            // 绑定程序化纹理
            if (p.UseProceduralTexture && ProceduralTexture.IsAvailable)
            {
                Texture2D tex = ProceduralTexture.GetTexture(preset);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                }
            }

            PresetCache[preset] = new Material(mat); // 缓存原型
            return mat;
        }

        /// <summary>
        /// 获取霓虹材质（可选自定义颜色）。
        /// </summary>
        public static Material GetNeonMaterial(Color glowColor, float intensity = 2.8f)
        {
            PresetParams p = GetPresetParams(MaterialPreset.Neon);
            p.BaseColor = glowColor;
            p.EmissionColor = glowColor;
            p.EmissionIntensity = intensity;
            return CreatePbrMaterial(p);
        }

        /// <summary>
        /// 获取纯色简易材质（用于小型装饰物，不占缓存）。
        /// </summary>
        public static Material GetSimpleMaterial(Color color, float metallic = 0f, float smoothness = 0.3f)
        {
            PresetParams p = new PresetParams
            {
                BaseColor = color,
                Metallic = metallic,
                Smoothness = smoothness,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                UseProceduralTexture = false
            };
            return CreatePbrMaterial(p);
        }

        // ─── 预生成所有纹理（场景启动时调用一次）──────────

        /// <summary>
        /// 预生成所有程序化纹理并缓存。建议在 BuildWorld 阶段早期调用。
        /// </summary>
        public static void PreWarmTextures(int texSize = 512)
        {
            MaterialPreset[] texturedPresets =
            {
                MaterialPreset.BrickWall,
                MaterialPreset.Concrete,
                MaterialPreset.Wood,
                MaterialPreset.Asphalt
            };

            foreach (MaterialPreset preset in texturedPresets)
            {
                if (!TextureCache.ContainsKey(preset))
                {
                    Texture2D tex = ProceduralTexture.Generate(preset, texSize, texSize);
                    if (tex != null)
                    {
                        TextureCache[preset] = tex;
                    }
                }
            }
        }

        // ─── 内部：创建 PBR 材质 ──────────────────────

        private static Material CreatePbrMaterial(PresetParams p)
        {
            Material mat = new Material(LitShader);
            mat.color = p.BaseColor;

            bool isUrpLit = LitShader.name.Contains("Lit");
            bool isStandard = LitShader.name.Contains("Standard");

            if (isUrpLit)
            {
                // URP Lit Shader 属性
                mat.SetColor("_BaseColor", p.BaseColor);
                mat.SetFloat("_Metallic", p.Metallic);
                mat.SetFloat("_Smoothness", p.Smoothness);

                if (p.EmissionIntensity > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", p.EmissionColor * p.EmissionIntensity);
                }

                // 玻璃特殊性：表面类型透明
                if (p.BaseColor.a < 1f)
                {
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.renderQueue = 3000;
                }
            }
            else if (isStandard)
            {
                mat.SetFloat("_Metallic", p.Metallic);
                mat.SetFloat("_Glossiness", p.Smoothness);

                if (p.EmissionIntensity > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", p.EmissionColor * p.EmissionIntensity);
                }

                if (p.BaseColor.a < 1f)
                {
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = 3000;
                }
            }

            return mat;
        }

        // ─── 清除缓存 ────────────────────────────────

        public static void ClearCache()
        {
            foreach (Material mat in PresetCache.Values)
            {
                if (mat != null) UnityEngine.Object.DestroyImmediate(mat);
            }
            PresetCache.Clear();

            foreach (Texture2D tex in TextureCache.Values)
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
            TextureCache.Clear();
        }
    }
}