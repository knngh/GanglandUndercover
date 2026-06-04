using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 程序化豆子人角色构建器 — 不依赖外部 .fbx 模型，用 Unity Primitive 组合创建 Among Us 风格角色。
    /// 调用 Build() 返回带有完整视觉的 GameObject，可直接挂载为 SocialCharacter 载体。
    /// </summary>
    public sealed class BeanCharacterBuilder
    {
        // ── 几何参数 ──────────────────────────────────
        private const float BodyRadius        = 0.35f;
        private const float BodyHeight        = 1.2f;
        private const float HeadRadius        = 0.30f;
        private const float HeadEmbedOffset   = 0.08f;  // 头部略嵌入身体顶部的量
        private const float BackpackRadius    = 0.12f;
        private const float BackpackHeight    = 0.30f;
        private const float LegRadius         = 0.10f;
        private const float LegHeight         = 0.40f;
        private const float VisorRadius       = 0.18f;
        private const float VisorHeight       = 0.20f;

        private const float BodyTopY     = BodyHeight * 0.5f;           // 0.6
        private const float HeadCenterY  = BodyTopY - HeadEmbedOffset;  // 0.52
        private const float LegTopY      = -(BodyHeight * 0.5f);        // -0.6
        private const float LegCenterY   = LegTopY - LegHeight * 0.5f;  // -0.8
        private const float BackpackZ    = -BodyRadius - 0.06f;         // 背部后方
        private const float VisorZ       = BodyRadius * 0.6f;           // 面罩在头部前方

        // ── 颜色常量 ──────────────────────────────────
        private static readonly Color GangBodyColor       = HexColor("#c0392b");
        private static readonly Color GangBackpackColor   = HexColor("#96281b");
        private static readonly Color UndercoverBodyColor = HexColor("#f39c12");
        private static readonly Color UndercoverBackpackColor = HexColor("#c27a0a");
        private static readonly Color PoliceBodyColor     = HexColor("#2980b9");
        private static readonly Color PoliceBackpackColor = HexColor("#1f5d8a");
        private static readonly Color MoleBodyColor       = HexColor("#00aaaa");
        private static readonly Color MoleBackpackColor   = HexColor("#007777");
        private static readonly Color VisorColor          = new Color(0.533f, 0.8f, 1f, 0.35f);  // #88ccff 半透明
        private static readonly Color VisorFrameColor     = HexColor("#446688");
        private static readonly Color LegColor            = new Color(0.08f, 0.08f, 0.1f, 1f);
        private static readonly Color ShadowColor         = new Color(0f, 0f, 0f, 0.25f);

        // ── 构建 ──────────────────────────────────────

        private readonly SocialRole role;
        private readonly string characterName;
        private readonly bool isPlayer;

        private readonly List<GameObject> parts = new List<GameObject>();

        public BeanCharacterBuilder(string characterName, SocialRole role, bool isPlayer)
        {
            this.characterName = characterName;
            this.role = role;
            this.isPlayer = isPlayer;
        }

        /// <summary>
        /// 构建完整的豆子人 GameObject，返回根节点。
        /// 调用方可继续挂载 SocialCharacter、BeanAnimator 等组件。
        /// </summary>
        public GameObject Build()
        {
            GameObject root = new GameObject(characterName);
            root.transform.position = Vector3.zero;

            Color bodyColor = GetBodyColor();
            Color backpackColor = GetBackpackColor();

            // 身体（Capsule）
            GameObject body = CreatePrimitive(PrimitiveType.Capsule, "Body", root.transform,
                Vector3.zero,
                new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f));
            SetMaterialColor(body, bodyColor);

            // 头部（Sphere）—— 略嵌入身体顶部
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, "Head", root.transform,
                new Vector3(0f, HeadCenterY, 0f),
                Vector3.one * HeadRadius * 2f);
            SetMaterialColor(head, bodyColor);

            // 面罩玻璃（半透明 Cylinder，前方）
            GameObject visor = CreatePrimitive(PrimitiveType.Cylinder, "Visor", root.transform,
                new Vector3(0f, HeadCenterY + 0.02f, VisorZ),
                new Vector3(VisorRadius * 2f, VisorHeight * 0.5f, VisorRadius * 2f));
            visor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetMaterialColor(visor, VisorColor, true);

            // 面罩边框
            GameObject visorFrame = CreatePrimitive(PrimitiveType.Cylinder, "VisorFrame", root.transform,
                new Vector3(0f, HeadCenterY + 0.02f, VisorZ - 0.005f),
                new Vector3(VisorRadius * 2.1f, VisorHeight * 0.52f, VisorRadius * 2.1f));
            visorFrame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetMaterialColor(visorFrame, VisorFrameColor);

            // 背包（小 Cylinder，背部）
            GameObject backpack = CreatePrimitive(PrimitiveType.Cylinder, "Backpack", root.transform,
                new Vector3(0f, BodyTopY * 0.4f, BackpackZ),
                new Vector3(BackpackRadius * 2f, BackpackHeight * 0.5f, BackpackRadius * 2f));
            backpack.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetMaterialColor(backpack, backpackColor);

            // 左腿
            GameObject leftLeg = CreatePrimitive(PrimitiveType.Capsule, "LeftLeg", root.transform,
                new Vector3(-BodyRadius * 0.5f, LegCenterY, 0f),
                new Vector3(LegRadius * 2f, LegHeight * 0.5f, LegRadius * 2f));
            SetMaterialColor(leftLeg, LegColor);

            // 右腿
            GameObject rightLeg = CreatePrimitive(PrimitiveType.Capsule, "RightLeg", root.transform,
                new Vector3(BodyRadius * 0.5f, LegCenterY, 0f),
                new Vector3(LegRadius * 2f, LegHeight * 0.5f, LegRadius * 2f));
            SetMaterialColor(rightLeg, LegColor);

            // 脚下阴影
            CreatePrimitive(PrimitiveType.Cylinder, "Shadow", root.transform,
                new Vector3(0f, LegCenterY - LegHeight * 0.5f - 0.04f, 0f),
                new Vector3(BodyRadius * 1.6f, 0.015f, BodyRadius * 0.8f));
            // 阴影用半透明黑色

            parts.AddRange(new[] { body, head, visor, visorFrame, backpack, leftLeg, rightLeg });

            return root;
        }

        /// <summary>
        /// 返回构建的所有部件列表，供 CharacterOutliner 等组件遍历。
        /// </summary>
        public IReadOnlyList<GameObject> Parts => parts;

        // ── 颜色辅助 ──────────────────────────────────

        private Color GetBodyColor()
        {
            return role switch
            {
                SocialRole.Gang       => GangBodyColor,
                SocialRole.Undercover => UndercoverBodyColor,
                SocialRole.Police     => PoliceBodyColor,
                SocialRole.Mole       => MoleBodyColor,
                _                     => PoliceBodyColor,
            };
        }

        private Color GetBackpackColor()
        {
            return role switch
            {
                SocialRole.Gang       => GangBackpackColor,
                SocialRole.Undercover => UndercoverBackpackColor,
                SocialRole.Police     => PoliceBackpackColor,
                SocialRole.Mole       => MoleBackpackColor,
                _                     => PoliceBackpackColor,
            };
        }

        // ── 静态工具 ──────────────────────────────────

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            return go;
        }

        private static void SetMaterialColor(GameObject go, Color color, bool transparent = false)
        {
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            Material mat = new Material(FindShader(transparent));
            mat.color = color;
            if (transparent)
            {
                // 使用透明渲染模式
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
            renderer.sharedMaterial = mat;
        }

        private static Shader FindShader(bool transparent)
        {
            if (transparent)
            {
                return Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Unlit/Transparent")
                    ?? Shader.Find("Sprites/Default");
            }
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            return Color.magenta;
        }
    }
}