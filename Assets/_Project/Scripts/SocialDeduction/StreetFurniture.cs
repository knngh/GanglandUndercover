using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 街景物品生成器（v1）：GTA / Watch Dogs 级别街头写实风格。
    /// 生成路灯、交通信号灯、围栏/栏杆、垃圾桶、长椅、消防栓、报刊亭等。
    /// 所有对象使用 MaterialFactory 管理材质。
    /// </summary>
    public static class StreetFurniture
    {
        // ─── 常量 ────────────────────────────────
        private const float SidewalkY   = 0.055f;  // 人行道高度偏移
        private const float RoadY       = 0.02f;   // 道路面偏移
        private const float GroundZ     = -0.08f;  // 地面层 Z（略低于建筑基底）

        // ─── 颜色常量 ────────────────────────────
        private static readonly Color PoleGray   = new Color(0.22f, 0.22f, 0.24f, 1f);
        private static readonly Color LampHousing = new Color(0.18f, 0.18f, 0.20f, 1f);
        private static readonly Color SignYellow = new Color(0.88f, 0.82f, 0.15f, 1f);
        private static readonly Color FireHydrantRed = new Color(0.72f, 0.15f, 0.08f, 1f);

        // ─── 公开接口 — 单个物件 ──────────────────

        /// <summary>
        /// 路灯：Cylinder 杆（3m 高）+ 弯臂 + 灯具 Box + PointLight 暖黄。
        /// </summary>
        public static GameObject PlaceStreetLight(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"StreetLight_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            // 灯杆主体 — Cylinder
            GameObject pole = CreatePrimitive("Pole", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + 0.15f),
                Quaternion.identity,
                new Vector3(0.06f, 0.3f, 0.06f),
                generatedObjects);
            ApplyMaterial(pole, MaterialFactory.MaterialPreset.IronSheet);

            // 弯臂（略向前倾的横杆 + 下弯连接）
            GameObject armHoriz = CreatePrimitive("Arm_H", PrimitiveType.Cylinder, root.transform,
                new Vector3(0.12f, 0f, GroundZ + 0.29f),
                Quaternion.Euler(90f, 0f, 90f),
                new Vector3(0.025f, 0.18f, 0.025f),
                generatedObjects);
            ApplyMaterial(armHoriz, MaterialFactory.MaterialPreset.IronSheet);

            GameObject armDown = CreatePrimitive("Arm_Down", PrimitiveType.Cylinder, root.transform,
                new Vector3(0.28f, 0f, GroundZ + 0.28f),
                Quaternion.identity,
                new Vector3(0.025f, 0.04f, 0.025f),
                generatedObjects);
            ApplyMaterial(armDown, MaterialFactory.MaterialPreset.IronSheet);

            // 灯罩
            GameObject housing = CreatePrimitive("LampHousing", PrimitiveType.Cube, root.transform,
                new Vector3(0.28f, 0f, GroundZ + 0.265f),
                Quaternion.identity,
                new Vector3(0.10f, 0.14f, 0.05f),
                generatedObjects);
            ApplyMaterial(housing, MaterialFactory.MaterialPreset.IronSheet);

            // 发光面板（暖黄）
            GameObject glowPanel = CreatePrimitive("LampGlow", PrimitiveType.Cube, root.transform,
                new Vector3(0.28f, 0f, GroundZ + 0.24f),
                Quaternion.identity,
                new Vector3(0.08f, 0.12f, 0.01f),
                generatedObjects);
            Material glowMat = MaterialFactory.GetNeonMaterial(
                new Color(1f, 0.78f, 0.42f, 1f), 3f);
            glowPanel.GetComponent<MeshRenderer>().sharedMaterial = glowMat;

            // Point Light 暖黄
            GameObject lightObj = new GameObject("PointLight");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0.28f, 0f, GroundZ + 0.24f);
            generatedObjects.Add(lightObj);

            Light pt = lightObj.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(1f, 0.78f, 0.42f, 1f);
            pt.intensity = 1.8f;
            pt.range = 1.5f;
            pt.shadows = LightShadows.None;
            pt.renderMode = LightRenderMode.ForcePixel;

            return root;
        }

        /// <summary>
        /// 交通信号灯：Cylinder 杆 + 横臂 + 3 个 Sphere（红/黄/绿）。
        /// </summary>
        public static GameObject PlaceTrafficLight(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"TrafficLight_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            // 杆
            GameObject pole = CreatePrimitive("Pole", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + 0.125f),
                Quaternion.identity,
                new Vector3(0.05f, 0.25f, 0.05f),
                generatedObjects);
            ApplyMaterial(pole, MaterialFactory.MaterialPreset.IronSheet);

            // 横臂
            GameObject arm = CreatePrimitive("Arm", PrimitiveType.Cylinder, root.transform,
                new Vector3(0.08f, 0f, GroundZ + 0.245f),
                Quaternion.Euler(90f, 0f, 90f),
                new Vector3(0.028f, 0.2f, 0.028f),
                generatedObjects);
            ApplyMaterial(arm, MaterialFactory.MaterialPreset.IronSheet);

            // 信号灯盒
            GameObject box = CreatePrimitive("SignalBox", PrimitiveType.Cube, root.transform,
                new Vector3(0.24f, 0f, GroundZ + 0.23f),
                Quaternion.identity,
                new Vector3(0.06f, 0.05f, 0.14f),
                generatedObjects);
            ApplyMaterial(box, MaterialFactory.MaterialPreset.IronSheet);

            // 三盏灯（Sphere）
            float lightSpacing = 0.045f;
            Color[] colors = { Color.red, new Color(1f, 0.85f, 0.1f, 1f), Color.green };
            string[] labels = { "Red", "Yellow", "Green" };

            for (int i = 0; i < 3; i++)
            {
                float zOffset = GroundZ + 0.18f + i * lightSpacing;
                GameObject sphere = CreatePrimitive($"Signal_{labels[i]}", PrimitiveType.Sphere,
                    root.transform,
                    new Vector3(0.27f, 0f, zOffset),
                    Quaternion.identity,
                    new Vector3(0.035f, 0.035f, 0.035f),
                    generatedObjects);
                Material m = MaterialFactory.GetNeonMaterial(colors[i], 1.5f);
                sphere.GetComponent<MeshRenderer>().sharedMaterial = m;
            }

            return root;
        }

        /// <summary>
        /// 围栏/栏杆段：两侧细 Cylinder 立柱 + 2 层 Cube 横梁。
        /// </summary>
        public static GameObject PlaceRailingSegment(
            Vector3 startPos,
            float length,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"Railing_{startPos.x:F1}_{startPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = startPos;
            generatedObjects.Add(root);

            float postHeight = 0.18f;
            float postGap = 0.15f;
            float halfLen = length * 0.5f;

            // 立柱（每 postGap 放置一根）
            int postCount = Mathf.Max(2, Mathf.FloorToInt(length / postGap) + 1);
            for (int i = 0; i < postCount; i++)
            {
                float x = -halfLen + i * length / (postCount - 1);
                GameObject post = CreatePrimitive($"Post_{i}", PrimitiveType.Cylinder,
                    root.transform,
                    new Vector3(x, 0f, GroundZ + postHeight * 0.5f),
                    Quaternion.identity,
                    new Vector3(0.015f, 0.015f, postHeight),
                    generatedObjects);
                ApplyMaterial(post, MaterialFactory.MaterialPreset.IronSheet);
            }

            // 横梁（上下两排）
            float[] railHeights = { 0.045f, 0.13f };
            foreach (float rh in railHeights)
            {
                GameObject rail = CreatePrimitive($"Rail_H{rh:F2}", PrimitiveType.Cube,
                    root.transform,
                    new Vector3(0f, 0f, GroundZ + rh),
                    Quaternion.identity,
                    new Vector3(length, 0.012f, 0.012f),
                    generatedObjects);
                ApplyMaterial(rail, MaterialFactory.MaterialPreset.IronSheet);
            }

            return root;
        }

        /// <summary>
        /// 垃圾桶：Cylinder 桶身 + 略大的桶盖。
        /// </summary>
        public static GameObject PlaceTrashBin(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"TrashBin_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            float bodyH = 0.14f;
            float radius = 0.04f;

            // 桶身
            GameObject body = CreatePrimitive("Body", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + bodyH * 0.5f),
                Quaternion.identity,
                new Vector3(radius, radius, bodyH),
                generatedObjects);
            // 随机颜色：深绿/深灰
            Color binColor = Random.value > 0.5f
                ? new Color(0.08f, 0.22f, 0.12f, 1f)
                : new Color(0.18f, 0.18f, 0.2f, 1f);
            ApplySimpleMaterial(body, binColor);

            // 桶盖
            GameObject lid = CreatePrimitive("Lid", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + bodyH + 0.01f),
                Quaternion.identity,
                new Vector3(radius + 0.01f, radius + 0.01f, 0.015f),
                generatedObjects);
            ApplySimpleMaterial(lid, binColor * 0.85f);

            return root;
        }

        /// <summary>
        /// 长椅：3 根横梁（座面/靠背）+ 4 根短支柱。
        /// </summary>
        public static GameObject PlaceBench(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"Bench_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            float benchLen  = 0.3f;
            float seatWidth = 0.06f;
            float seatZ     = GroundZ + 0.06f;
            float backZ     = GroundZ + 0.11f;

            // 座面（3 根木板条）
            for (int i = 0; i < 3; i++)
            {
                float yOff = (i - 1) * 0.03f;
                GameObject slat = CreatePrimitive($"Seat_{i}", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, yOff, seatZ),
                    Quaternion.identity,
                    new Vector3(benchLen, seatWidth * 0.3f, 0.012f),
                    generatedObjects);
                ApplyMaterial(slat, MaterialFactory.MaterialPreset.Wood);
            }

            // 靠背横梁
            GameObject back = CreatePrimitive("Back", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -seatWidth * 0.45f, backZ),
                Quaternion.identity,
                new Vector3(benchLen, 0.010f, 0.025f),
                generatedObjects);
            ApplyMaterial(back, MaterialFactory.MaterialPreset.Wood);

            // 支柱（4 根）
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    GameObject leg = CreatePrimitive($"Leg_{sx}_{sy}", PrimitiveType.Cylinder,
                        root.transform,
                        new Vector3(sx * benchLen * 0.42f, sy * seatWidth * 0.4f, GroundZ + 0.025f),
                        Quaternion.identity,
                        new Vector3(0.012f, 0.012f, 0.05f),
                        generatedObjects);
                    ApplyMaterial(leg, MaterialFactory.MaterialPreset.IronSheet);
                }
            }

            return root;
        }

        /// <summary>
        /// 消防栓：Cylinder 主体 + 侧方小圆柱出水口。
        /// </summary>
        public static GameObject PlaceFireHydrant(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"FireHydrant_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            float bodyH = 0.10f;
            float bodyR = 0.025f;

            // 主体
            GameObject body = CreatePrimitive("Body", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + bodyH * 0.5f),
                Quaternion.identity,
                new Vector3(bodyR, bodyR, bodyH),
                generatedObjects);
            ApplySimpleMaterial(body, FireHydrantRed);

            // 顶盖（稍宽）
            GameObject dome = CreatePrimitive("Dome", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + bodyH + 0.01f),
                Quaternion.identity,
                new Vector3(bodyR + 0.008f, bodyR + 0.008f, 0.02f),
                generatedObjects);
            ApplySimpleMaterial(dome, FireHydrantRed * 0.85f);

            // 侧出水口
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject nozzle = CreatePrimitive($"Nozzle_{i}", PrimitiveType.Cylinder,
                    root.transform,
                    new Vector3(i * (bodyR + 0.015f), 0f, GroundZ + bodyH * 0.5f),
                    Quaternion.Euler(0f, 90f, 0f),
                    new Vector3(0.012f, 0.012f, 0.03f),
                    generatedObjects);
                ApplySimpleMaterial(nozzle, new Color(0.15f, 0.15f, 0.17f, 1f));
            }

            return root;
        }

        /// <summary>
        /// 报刊亭：Cube 主体 + 顶棚 + 遮阳板。
        /// </summary>
        public static GameObject PlaceNewsStand(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"NewsStand_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            float kioskW = 0.22f;
            float kioskD = 0.18f;
            float kioskH = 0.16f;

            // 主体
            GameObject body = CreatePrimitive("Body", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0f, GroundZ + kioskH * 0.5f),
                Quaternion.identity,
                new Vector3(kioskW, kioskD, kioskH),
                generatedObjects);
            ApplyMaterial(body, MaterialFactory.MaterialPreset.Wood);

            // 前面玻璃橱窗
            GameObject window = CreatePrimitive("Window", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -kioskD * 0.52f, GroundZ + kioskH * 0.55f),
                Quaternion.identity,
                new Vector3(kioskW - 0.02f, 0.008f, kioskH * 0.55f),
                generatedObjects);
            ApplyMaterial(window, MaterialFactory.MaterialPreset.Glass);

            // 遮阳棚
            GameObject awning = CreatePrimitive("Awning", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -kioskD * 0.55f, GroundZ + kioskH + 0.01f),
                Quaternion.identity,
                new Vector3(kioskW + 0.04f, 0.06f, 0.015f),
                generatedObjects);
            Color awningColor = new Color(0.8f, 0.25f, 0.22f, 1f); // 红白条纹色
            ApplySimpleMaterial(awning, awningColor);

            return root;
        }

        // ─── 公开接口 — 批量生成 ──────────────────

        /// <summary>
        /// 沿街道生成路灯。每间隔 distance 放置一盏，从 start 到 end（世界空间 Y 轴方向）。
        /// </summary>
        public static void PlaceStreetLightsAlong(
            Vector3 start, Vector3 end, float distance,
            Transform parent, List<GameObject> generatedObjects)
        {
            float totalLen = Vector3.Distance(start, end);
            int count = Mathf.FloorToInt(totalLen / distance) + 1;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                Vector3 pos = Vector3.Lerp(start, end, t);
                PlaceStreetLight(pos, parent, generatedObjects);
            }
        }

        /// <summary>
        /// 沿人行道连续放置围栏。
        /// </summary>
        public static void PlaceRailingAlong(
            Vector3 start, Vector3 end,
            Transform parent, List<GameObject> generatedObjects)
        {
            float totalLen = Vector3.Distance(start, end);
            float segmentLen = 0.3f;
            int segments = Mathf.Max(1, Mathf.FloorToInt(totalLen / segmentLen));
            Vector3 dir = (end - start).normalized;

            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector3 pos = Vector3.Lerp(start, end, t) + dir * segmentLen * 0.5f;

                // 旋转使围栏对齐到街道方向
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);

                // 围栏段手动创建（不能用 root 的 rotation 因为 PlaceRailingSegment 内部已是本地坐标）
                PlaceRailingSegment(pos, segmentLen, parent, generatedObjects);
            }
        }

        // ─── 内部辅助 ──────────────────────────────

        private static GameObject CreatePrimitive(
            string name, PrimitiveType type, Transform parent,
            Vector3 localPos, Quaternion localRot, Vector3 localScale,
            List<GameObject> gen)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            gen.Add(obj);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = localRot;
            obj.transform.localScale   = localScale;
            return obj;
        }

        private static void ApplyMaterial(GameObject obj, MaterialFactory.MaterialPreset preset)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.sharedMaterial = MaterialFactory.GetMaterial(preset);
        }

        private static void ApplySimpleMaterial(GameObject obj, Color color,
            float metallic = 0f, float smoothness = 0.25f)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.sharedMaterial = MaterialFactory.GetSimpleMaterial(color, metallic, smoothness);
        }
    }
}