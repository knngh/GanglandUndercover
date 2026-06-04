using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 散落道具生成器（v1）：街头/巷尾散落物品。
    /// 生成纸箱堆、油桶堆、轮胎堆、木托盘、锥筒等。
    /// 所有对象使用 MaterialFactory 管理材质。
    /// </summary>
    public static class StreetProps
    {
        private const float GroundZ = -0.08f;

        // ─── 颜色常量 ────────────────────────────
        private static readonly Color CardboardBrown = new Color(0.55f, 0.42f, 0.28f, 1f);
        private static readonly Color DrumBlue      = new Color(0.08f, 0.18f, 0.38f, 1f);
        private static readonly Color DrumRed       = new Color(0.48f, 0.12f, 0.10f, 1f);
        private static readonly Color TireBlack     = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color PalletBrown   = new Color(0.42f, 0.32f, 0.18f, 1f);
        private static readonly Color ConeOrange    = new Color(1f, 0.45f, 0.08f, 1f);
        private static readonly Color ConeWhite     = new Color(0.92f, 0.90f, 0.85f, 1f);

        // ─── 纸箱堆 ──────────────────────────────

        /// <summary>
        /// 纸箱堆：2-4 个略微旋转/偏移叠放的 Cube，模拟废弃快递箱/货物箱。
        /// </summary>
        public static GameObject PlaceCardboardStack(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"CardboardStack_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            int boxCount = Random.Range(2, 5);
            float baseScaleW = Random.Range(0.06f, 0.10f);
            float baseScaleD = Random.Range(0.06f, 0.10f);
            float baseScaleH = Random.Range(0.06f, 0.12f);

            for (int i = 0; i < boxCount; i++)
            {
                float zStack = i * baseScaleH * 0.8f;
                float jitterX = Random.Range(-0.02f, 0.02f);
                float jitterY = Random.Range(-0.02f, 0.02f);
                float rotZ    = Random.Range(-8f, 8f);

                float wScale = baseScaleW * Random.Range(0.85f, 1.15f);
                float dScale = baseScaleD * Random.Range(0.85f, 1.15f);
                float hScale = baseScaleH * Random.Range(0.85f, 1.15f);

                GameObject box = CreatePrimitive($"Box_{i}", PrimitiveType.Cube,
                    root.transform,
                    new Vector3(jitterX, jitterY, GroundZ + zStack + hScale * 0.5f),
                    Quaternion.Euler(0f, 0f, rotZ),
                    new Vector3(wScale, dScale, hScale),
                    generatedObjects);

                // 随机棕色变化
                Color boxColor = Color.Lerp(CardboardBrown,
                    new Color(0.62f, 0.48f, 0.32f, 1f), Random.value);
                ApplySimpleMaterial(box, boxColor);

                // 箱子上封条（细条）
                if (Random.value > 0.5f)
                {
                    GameObject tape = CreatePrimitive($"Tape_{i}", PrimitiveType.Cube,
                        box.transform,
                        new Vector3(0f, 0f, 0.48f),
                        Quaternion.identity,
                        new Vector3(0.9f, 0.05f, 0.02f),
                        generatedObjects);
                    ApplySimpleMaterial(tape, new Color(0.78f, 0.72f, 0.48f, 1f));
                }
            }

            return root;
        }

        // ─── 油桶堆 ──────────────────────────────

        /// <summary>
        /// 油桶堆：2-3 个 Cylinder 桶，略微倾斜叠放。
        /// </summary>
        public static GameObject PlaceOilDrumStack(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"OilDrumStack_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            int count = Random.Range(2, 4);
            float drumR = 0.04f;
            float drumH = 0.10f;

            Color[] drumColors = { DrumBlue, DrumRed, new Color(0.22f, 0.22f, 0.24f, 1f) };

            for (int i = 0; i < count; i++)
            {
                float zStack = i * drumH * 0.75f;
                float jitterX = Random.Range(-0.03f, 0.03f);
                float jitterY = Random.Range(-0.03f, 0.03f);
                float tiltDeg = Random.Range(-4f, 4f);

                GameObject drum = CreatePrimitive($"Drum_{i}", PrimitiveType.Cylinder,
                    root.transform,
                    new Vector3(jitterX, jitterY, GroundZ + zStack + drumH * 0.5f),
                    Quaternion.Euler(Random.Range(0f, 5f), 0f, Random.Range(0f, 5f)),
                    new Vector3(drumR, drumR, drumH),
                    generatedObjects);

                Color drumColor = drumColors[Random.Range(0, drumColors.Length)];
                ApplySimpleMaterial(drum, drumColor, 0.7f, 0.2f);

                // 油桶加强筋（环形凸起模拟）
                for (int r = 0; r < 2; r++)
                {
                    float rz = (r == 0 ? 0.3f : -0.3f);
                    GameObject rib = CreatePrimitive($"Rib_{i}_{r}", PrimitiveType.Cylinder,
                        drum.transform,
                        new Vector3(0f, 0f, rz),
                        Quaternion.identity,
                        new Vector3(drumR + 0.003f, drumR + 0.003f, 0.012f),
                        generatedObjects);
                    ApplySimpleMaterial(rib, drumColor * 0.85f, 0.7f, 0.2f);
                }
            }

            return root;
        }

        // ─── 轮胎堆 ──────────────────────────────

        /// <summary>
        /// 轮胎堆：3-5 个扁平 Cylinder 叠放，略偏移错落。
        /// </summary>
        public static GameObject PlaceTireStack(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"TireStack_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            int count = Random.Range(3, 6);
            float outerR = 0.06f;
            float tireH  = 0.02f;

            for (int i = 0; i < count; i++)
            {
                float zStack = i * tireH;
                float tiltX = Random.Range(-3f, 3f);
                float tiltY = Random.Range(-3f, 3f);

                // 轮胎外壳（扁圆柱）
                GameObject tire = CreatePrimitive($"Tire_{i}", PrimitiveType.Cylinder,
                    root.transform,
                    new Vector3(0f, 0f, GroundZ + zStack + tireH * 0.5f),
                    Quaternion.Euler(tiltX, tiltY, 0f),
                    new Vector3(outerR, outerR, tireH),
                    generatedObjects);
                ApplySimpleMaterial(tire, TireBlack, 0.05f, 0.03f);

                // 轮毂孔（小的深色 cylinder 模拟）
                GameObject hub = CreatePrimitive($"Hub_{i}", PrimitiveType.Cylinder,
                    tire.transform,
                    new Vector3(0f, 0f, 0.1f),
                    Quaternion.identity,
                    new Vector3(outerR * 0.55f, outerR * 0.55f, tireH * 0.5f),
                    generatedObjects);
                ApplySimpleMaterial(hub, new Color(0.05f, 0.05f, 0.05f, 1f));
            }

            return root;
        }

        // ─── 木托盘 ──────────────────────────────

        /// <summary>
        /// 木托盘：底板 3 根横梁 + 上面 5 根板条。
        /// </summary>
        public static GameObject PlaceWoodenPallet(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"Pallet_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            float palletW = 0.16f;
            float palletD = 0.12f;
            float plankThickness = 0.008f;
            float beamThickness  = 0.012f;

            // 底板横梁（3 根，沿 Y 方向）
            for (int i = 0; i < 3; i++)
            {
                float yOffset = (i - 1) * palletD * 0.38f;
                GameObject beam = CreatePrimitive($"Beam_{i}", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, yOffset, GroundZ + beamThickness * 0.5f),
                    Quaternion.identity,
                    new Vector3(palletW, beamThickness, beamThickness),
                    generatedObjects);
                ApplyMaterial(beam, MaterialFactory.MaterialPreset.Wood);
            }

            // 上面板条（5 根，沿 X 方向）
            for (int i = 0; i < 5; i++)
            {
                float xOffset = (i - 2) * palletW * 0.22f;
                GameObject plank = CreatePrimitive($"Plank_{i}", PrimitiveType.Cube, root.transform,
                    new Vector3(xOffset, 0f, GroundZ + beamThickness + plankThickness * 0.5f),
                    Quaternion.identity,
                    new Vector3(plankThickness, palletD, plankThickness),
                    generatedObjects);
                ApplyMaterial(plank, MaterialFactory.MaterialPreset.Wood);
            }

            return root;
        }

        // ─── 锥筒 ──────────────────────────────

        /// <summary>
        /// 锥筒：上小下大的锥形（用 2 段 Cylinder 近似）+ 反光条。
        /// </summary>
        public static GameObject PlaceTrafficCone(
            Vector3 worldPos,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject root = new GameObject($"Cone_{worldPos.x:F1}_{worldPos.y:F1}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            generatedObjects.Add(root);

            float coneH = 0.09f;

            // 底座（大圆盘）
            GameObject baseDisc = CreatePrimitive("Base", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + 0.01f),
                Quaternion.identity,
                new Vector3(0.04f, 0.04f, 0.02f),
                generatedObjects);
            ApplySimpleMaterial(baseDisc, ConeOrange);

            // 下部锥段（较宽）
            GameObject lowerCone = CreatePrimitive("Lower", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + coneH * 0.35f),
                Quaternion.identity,
                new Vector3(0.03f, 0.03f, coneH * 0.55f),
                generatedObjects);
            ApplySimpleMaterial(lowerCone, ConeOrange);

            // 上部锥段（较窄）
            GameObject upperCone = CreatePrimitive("Upper", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, GroundZ + coneH * 0.75f),
                Quaternion.identity,
                new Vector3(0.015f, 0.015f, coneH * 0.4f),
                generatedObjects);
            ApplySimpleMaterial(upperCone, ConeOrange);

            // 反光条（白色环带）
            GameObject reflectiveStrip = CreatePrimitive("Reflect", PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, 0f, GroundZ + coneH * 0.55f),
                Quaternion.identity,
                new Vector3(0.032f, 0.032f, 0.012f),
                generatedObjects);
            ApplySimpleMaterial(reflectiveStrip, ConeWhite);

            return root;
        }

        // ─── 随机散落（批量）──────────────────────

        /// <summary>
        /// 在指定矩形区域内随机放置若干散落道具。
        /// </summary>
        public static void ScatterProps(
            Vector3 regionCenter, Vector2 regionSize,
            int propCount,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            float halfW = regionSize.x * 0.5f;
            float halfH = regionSize.y * 0.5f;

            for (int i = 0; i < propCount; i++)
            {
                float rx = Random.Range(-halfW, halfW);
                float ry = Random.Range(-halfH, halfH);
                Vector3 pos = regionCenter + new Vector3(rx, ry, 0f);

                float roll = Random.value;
                if (roll < 0.30f)
                    PlaceCardboardStack(pos, parent, generatedObjects);
                else if (roll < 0.55f)
                    PlaceOilDrumStack(pos, parent, generatedObjects);
                else if (roll < 0.75f)
                    PlaceTireStack(pos, parent, generatedObjects);
                else if (roll < 0.90f)
                    PlaceWoodenPallet(pos, parent, generatedObjects);
                else
                    PlaceTrafficCone(pos, parent, generatedObjects);
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