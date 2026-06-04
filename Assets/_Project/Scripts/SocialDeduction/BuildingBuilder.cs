using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 程序化建筑生成器 (v1)：GTA / Watch Dogs 级别街头写实风格。
    /// 生成多层组合建筑：外墙、窗户、屋顶、入口、装饰细节。
    /// 每栋建筑随机外观变体，避免千篇一律。
    /// </summary>
    public static class BuildingBuilder
    {
        // ─── 常量 ────────────────────────────────
        private const float FloorHeight = 0.32f;
        private const float WallThickness = 0.06f;
        private const float WindowWidth = 0.16f;
        private const float WindowHeight = 0.18f;
        private const float ParapetHeight = 0.06f;
        private const float BaseZ = 0.08f;

        // ─── 材质色板 ────────────────────────────
        private static readonly Color StoneBase = new Color(0.353f, 0.314f, 0.275f, 1f);   // #5a5046
        private static readonly Color BrickUpper = new Color(0.545f, 0.435f, 0.369f, 1f);   // #8b6f5e
        private static readonly Color WindowGlow = new Color(1f, 0.91f, 0.75f, 0.72f);      // #ffe8c0
        private static readonly Color WindowDark = new Color(0.06f, 0.07f, 0.09f, 0.85f);
        private static readonly Color WindowFrame = new Color(0.18f, 0.17f, 0.15f, 1f);
        private static readonly Color RoofColor = new Color(0.15f, 0.14f, 0.13f, 1f);
        private static readonly Color ParapetColor = new Color(0.22f, 0.21f, 0.19f, 1f);
        private static readonly Color DoorFrameColor = new Color(0.2f, 0.18f, 0.15f, 1f);
        private static readonly Color StepColor = new Color(0.38f, 0.37f, 0.35f, 1f);
        private static readonly Color AwningColor = new Color(0.28f, 0.35f, 0.32f, 1f);

        // ─── 建筑类型默认配置 ─────────────────────

        public static BuildingConfig TenementConfig(Vector3 position, float width, float depth)
        {
            return new BuildingConfig
            {
                Type = BuildingType.Tenement,
                Position = position,
                Width = width,
                Depth = depth,
                Floors = Random.Range(4, 6),
                PrimaryColor = Color.Lerp(StoneBase, new Color(0.4f, 0.38f, 0.35f, 1f), Random.Range(-0.1f, 0.1f)),
                SecondaryColor = Color.Lerp(new Color(0.45f, 0.42f, 0.38f, 1f), BrickUpper, Random.Range(0f, 0.3f)),
                AccentColor = new Color(0.2f, 0.18f, 0.14f, 1f),
                FloorHeight = FloorHeight,
                WindowCountPerFloor = Random.Range(3, 6),
                HasFireEscape = true,
                HasRoofWaterTank = Random.value > 0.5f,
                HasACUnit = Random.value > 0.4f,
                RoofType = RoofStyle.Flat
            };
        }

        public static BuildingConfig WarehouseConfig(Vector3 position, float width, float depth)
        {
            return new BuildingConfig
            {
                Type = BuildingType.Warehouse,
                Position = position,
                Width = width,
                Depth = depth,
                Floors = 2,
                PrimaryColor = new Color(0.353f, 0.353f, 0.431f, 1f), // #5a5a6e
                SecondaryColor = new Color(0.29f, 0.29f, 0.38f, 1f),
                AccentColor = new Color(0.22f, 0.22f, 0.28f, 1f),
                FloorHeight = FloorHeight * 1.5f,
                WindowCountPerFloor = Random.Range(1, 3),
                HasFireEscape = false,
                HasRoofWaterTank = false,
                HasACUnit = Random.value > 0.5f,
                RoofType = RoofStyle.Flat
            };
        }

        public static BuildingConfig OfficeConfig(Vector3 position, float width, float depth)
        {
            return new BuildingConfig
            {
                Type = BuildingType.Office,
                Position = position,
                Width = width,
                Depth = depth,
                Floors = Random.Range(5, 7),
                PrimaryColor = new Color(0.2f, 0.25f, 0.35f, 0.45f), // 半透玻璃幕墙
                SecondaryColor = new Color(0.15f, 0.18f, 0.25f, 1f),
                AccentColor = new Color(0.08f, 0.12f, 0.2f, 1f),
                FloorHeight = FloorHeight,
                WindowCountPerFloor = Random.Range(4, 7),
                HasFireEscape = false,
                HasRoofWaterTank = Random.value > 0.6f,
                HasACUnit = true,
                RoofType = RoofStyle.Flat
            };
        }

        public static BuildingConfig ClinicConfig(Vector3 position, float width, float depth)
        {
            return new BuildingConfig
            {
                Type = BuildingType.Clinic,
                Position = position,
                Width = width,
                Depth = depth,
                Floors = 2,
                PrimaryColor = new Color(0.92f, 0.91f, 0.88f, 1f), // 白色外墙
                SecondaryColor = new Color(0.85f, 0.84f, 0.81f, 1f),
                AccentColor = new Color(0.78f, 0.08f, 0.06f, 1f),  // 红十字
                FloorHeight = FloorHeight,
                WindowCountPerFloor = Random.Range(2, 5),
                HasFireEscape = false,
                HasRoofWaterTank = false,
                HasACUnit = Random.value > 0.5f,
                RoofType = RoofStyle.Flat
            };
        }

        public static BuildingConfig NightMarketConfig(Vector3 position, float width, float depth)
        {
            return new BuildingConfig
            {
                Type = BuildingType.NightMarket,
                Position = position,
                Width = width,
                Depth = depth,
                Floors = Random.Range(1, 3),
                PrimaryColor = new Color(0.3f, 0.26f, 0.22f, 1f),
                SecondaryColor = new Color(0.25f, 0.22f, 0.18f, 1f),
                AccentColor = NeonColor(),
                FloorHeight = FloorHeight * 1.1f,
                WindowCountPerFloor = Random.Range(2, 4),
                HasFireEscape = false,
                HasRoofWaterTank = false,
                HasACUnit = false,
                RoofType = RoofStyle.Flat,
                HasRollerDoor = true,
                HasNeonSign = true
            };
        }

        private static Color NeonColor()
        {
            Color[] neonPalette =
            {
                new Color(1f, 0.15f, 0.42f, 1f),    // 霓虹粉
                new Color(0.12f, 0.85f, 0.95f, 1f),  // 霓虹青
                new Color(1f, 0.72f, 0.08f, 1f),     // 霓虹黄
                new Color(0.25f, 0.95f, 0.32f, 1f),  // 霓虹绿
                new Color(0.82f, 0.25f, 0.95f, 1f),  // 霓虹紫
            };
            return neonPalette[Random.Range(0, neonPalette.Length)];
        }

        // ─── 材质（委托给 MaterialFactory）────────
        // MaterialFactory 内部已缓存 Shader 和材质实例，不再需要 FindShader

        // ─── 建筑生成入口 ─────────────────────────

        /// <summary>
        /// 根据配置生成完整建筑 GameObject，添加到 generatedObjects 列表。
        /// 返回根 GameObject。
        /// </summary>
        public static GameObject GenerateBuilding(
            BuildingConfig config,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            GameObject buildingRoot = new GameObject(
                $"Building_{config.Type}_{config.Position.x:F1}_{config.Position.y:F1}");
            buildingRoot.transform.SetParent(parent, false);
            buildingRoot.transform.position = config.Position;
            generatedObjects.Add(buildingRoot);

            // 随机种子（保证同一建筑内部各组件一致但又与其他建筑不同）
            int seed = (int)(config.Position.x * 1000 + config.Position.y * 1000);
            Random.InitState(seed);

            // 1. 外墙体
            BuildExteriorWalls(buildingRoot, config, generatedObjects);

            // 2. 窗户（沿朝街面 y+ 和 x+ 方向）
            BuildWindows(buildingRoot, config, generatedObjects);

            // 3. 屋顶
            BuildRoof(buildingRoot, config, generatedObjects);

            // 4. 入口
            BuildEntrance(buildingRoot, config, generatedObjects);

            // 5. 特殊装饰
            switch (config.Type)
            {
                case BuildingType.Tenement when config.HasFireEscape:
                    BuildFireEscape(buildingRoot, config, generatedObjects);
                    break;
                case BuildingType.NightMarket when config.HasNeonSign:
                    BuildNeonSign(buildingRoot, config, generatedObjects);
                    break;
                case BuildingType.NightMarket when config.HasRollerDoor:
                    BuildRollerDoor(buildingRoot, config, generatedObjects);
                    break;
                case BuildingType.Clinic:
                    BuildRedCross(buildingRoot, config, generatedObjects);
                    break;
            }

            // 6. 天台设施
            if (config.HasRoofWaterTank)
                BuildRoofWaterTank(buildingRoot, config, generatedObjects);
            if (config.HasACUnit)
                BuildRoofACUnit(buildingRoot, config, generatedObjects);

            return buildingRoot;
        }

        // ─── 外墙体 ──────────────────────────────

        private static void BuildExteriorWalls(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfW = config.Width * 0.5f;
            float halfD = config.Depth * 0.5f;
            float floorH = config.FloorHeight;

            for (int floor = 0; floor < config.Floors; floor++)
            {
                float zCenter = BaseZ + floor * floorH + floorH * 0.5f;
                Color floorColor = (floor == 0 && config.Type != BuildingType.Office)
                    ? config.PrimaryColor  // 底层石材
                    : config.SecondaryColor; // 上层

                // Office 全玻璃幕墙
                if (config.Type == BuildingType.Office)
                {
                    floorColor = config.PrimaryColor; // 半透明蓝
                }

                // 北墙
                CreateWallCube(root, $"Wall_N_F{floor}", gen,
                    new Vector3(0f, halfD, zCenter),
                    new Vector3(config.Width, WallThickness, floorH),
                    floorColor);

                // 南墙
                CreateWallCube(root, $"Wall_S_F{floor}", gen,
                    new Vector3(0f, -halfD, zCenter),
                    new Vector3(config.Width, WallThickness, floorH),
                    floorColor);

                // 东墙
                CreateWallCube(root, $"Wall_E_F{floor}", gen,
                    new Vector3(halfW, 0f, zCenter),
                    new Vector3(WallThickness, config.Depth - WallThickness * 2, floorH),
                    floorColor);

                // 西墙
                CreateWallCube(root, $"Wall_W_F{floor}", gen,
                    new Vector3(-halfW, 0f, zCenter),
                    new Vector3(WallThickness, config.Depth - WallThickness * 2, floorH),
                    floorColor);
            }
        }

        // ─── 窗户 ────────────────────────────────

        private static void BuildWindows(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfW = config.Width * 0.5f;
            float halfD = config.Depth * 0.5f;
            float floorH = config.FloorHeight;
            int winCount = config.WindowCountPerFloor;

            // 窗户只在朝街面（y+）和侧街面（x+）生成
            for (int floor = 0; floor < config.Floors; floor++)
            {
                float zCenter = BaseZ + floor * floorH + floorH * 0.5f;

                // y+ 面窗户
                float availableY = config.Width - 1.2f; // 留出入口空间
                float spacingY = winCount > 1 ? availableY / (winCount - 1) : 0f;
                for (int w = 0; w < winCount; w++)
                {
                    float xOffset = -availableY * 0.5f + w * spacingY;
                    bool isLit = Random.value > 0.3f; // 70% 亮灯
                    BuildSingleWindow(root, $"Window_Y_F{floor}_{w}", gen,
                        new Vector3(xOffset, halfD + WallThickness * 0.5f, zCenter),
                        isLit);
                }

                // x+ 面窗户（侧街面，数量减半）
                int sideWinCount = Mathf.Max(1, winCount / 2);
                float availableX = config.Depth - 0.8f;
                float spacingX = sideWinCount > 1 ? availableX / (sideWinCount - 1) : 0f;
                for (int w = 0; w < sideWinCount; w++)
                {
                    float yOffset = -availableX * 0.5f + w * spacingX;
                    bool isLit = Random.value > 0.35f;
                    BuildSingleWindow(root, $"Window_X_F{floor}_{w}", gen,
                        new Vector3(halfW + WallThickness * 0.5f, yOffset, zCenter),
                        isLit);
                }
            }
        }

        private static void BuildSingleWindow(GameObject root, string name, List<GameObject> gen,
            Vector3 localPos, bool isLit)
        {
            // 外框
            GameObject frame = CreateCube(name + "_Frame", gen, root.transform,
                localPos, new Vector3(WindowWidth + 0.03f, 0.015f, WindowHeight + 0.03f));
            SetSimpleMaterial(frame, WindowFrame);

            // 内嵌玻璃
            GameObject glass = CreateCube(name + "_Glass", gen, root.transform,
                localPos, new Vector3(WindowWidth, 0.01f, WindowHeight));

            // 发光/暗 — 使用 MaterialFactory
            if (isLit)
            {
                glass.GetComponent<MeshRenderer>().sharedMaterial =
                    MaterialFactory.GetNeonMaterial(WindowGlow, 0.6f);
            }
            else
            {
                SetSimpleMaterial(glass, WindowDark);
            }
        }

        // ─── 屋顶 ────────────────────────────────

        private static void BuildRoof(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfW = config.Width * 0.5f;
            float halfD = config.Depth * 0.5f;
            float roofZ = BaseZ + config.Floors * config.FloorHeight + 0.03f;
            float parapetZ = roofZ + ParapetHeight * 0.5f;

            // 屋顶平板
            GameObject roof = CreateCube("Roof_Plate", gen, root.transform,
                new Vector3(0f, 0f, roofZ),
                new Vector3(config.Width + 0.08f, config.Depth + 0.08f, 0.05f));
            SetMaterial(roof, RoofColor);

            // 女儿墙（四个边框）
            float pw = config.Width + 0.08f;
            float pd = config.Depth + 0.08f;
            CreateParapetWall(root, "Parapet_N", gen,
                new Vector3(0f, halfD + 0.02f, parapetZ),
                new Vector3(pw, 0.04f, ParapetHeight));
            CreateParapetWall(root, "Parapet_S", gen,
                new Vector3(0f, -halfD - 0.02f, parapetZ),
                new Vector3(pw, 0.04f, ParapetHeight));
            CreateParapetWall(root, "Parapet_E", gen,
                new Vector3(halfW + 0.02f, 0f, parapetZ),
                new Vector3(0.04f, pd, ParapetHeight));
            CreateParapetWall(root, "Parapet_W", gen,
                new Vector3(-halfW - 0.02f, 0f, parapetZ),
                new Vector3(0.04f, pd, ParapetHeight));
        }

        private static void CreateParapetWall(GameObject root, string name, List<GameObject> gen,
            Vector3 localPos, Vector3 scale)
        {
            GameObject p = CreateCube(name, gen, root.transform, localPos, scale);
            SetMaterial(p, ParapetColor);
        }

        // ─── 入口 ────────────────────────────────

        private static void BuildEntrance(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfD = config.Depth * 0.5f;
            float doorWidth = config.Type == BuildingType.Warehouse ? 0.55f : 0.36f;
            float doorHeight = config.FloorHeight * 0.8f;
            float doorZ = BaseZ + doorHeight * 0.5f;
            float entranceY = -halfD - WallThickness * 0.5f; // 南面

            // 门框（两侧立柱 + 顶部横梁）
            GameObject frameL = CreateCube("DoorFrame_L", gen, root.transform,
                new Vector3(-doorWidth * 0.5f, entranceY, doorZ),
                new Vector3(0.04f, 0.04f, doorHeight));
            SetMaterial(frameL, DoorFrameColor);

            GameObject frameR = CreateCube("DoorFrame_R", gen, root.transform,
                new Vector3(doorWidth * 0.5f, entranceY, doorZ),
                new Vector3(0.04f, 0.04f, doorHeight));
            SetMaterial(frameR, DoorFrameColor);

            GameObject frameTop = CreateCube("DoorFrame_Top", gen, root.transform,
                new Vector3(0f, entranceY, BaseZ + doorHeight - 0.02f),
                new Vector3(doorWidth + 0.02f, 0.04f, 0.03f));
            SetMaterial(frameTop, DoorFrameColor);

            // 门板（半透明深色）
            GameObject door = CreateCube("Door", gen, root.transform,
                new Vector3(0f, entranceY - 0.015f, doorZ),
                new Vector3(doorWidth - 0.06f, 0.015f, doorHeight - 0.04f));
            Color doorColor = config.Type == BuildingType.Clinic
                ? new Color(0.65f, 0.75f, 0.82f, 0.55f)  // 玻璃门
                : new Color(0.12f, 0.1f, 0.08f, 0.85f);
            SetMaterial(door, doorColor);

            // 雨棚（门上方突出）
            float awningZ = BaseZ + doorHeight + 0.02f;
            GameObject awning = CreateCube("Awning", gen, root.transform,
                new Vector3(0f, entranceY - 0.12f, awningZ),
                new Vector3(doorWidth + 0.15f, 0.18f, 0.025f));
            SetMaterial(awning, config.Type == BuildingType.Clinic
                ? new Color(0.88f, 0.86f, 0.82f, 1f)
                : AwningColor);

            // 台阶（2-3层小Cube）
            int stepCount = Random.Range(2, 4);
            for (int s = 0; s < stepCount; s++)
            {
                float stepZ = BaseZ - 0.04f - s * 0.04f;
                float stepWidth = doorWidth + 0.08f - s * 0.04f;
                float stepY = entranceY - 0.06f - s * 0.05f;
                GameObject step = CreateCube($"Step_{s}", gen, root.transform,
                    new Vector3(0f, stepY, stepZ),
                    new Vector3(stepWidth, 0.06f, 0.04f));
                SetMaterial(step, StepColor);
            }
        }

        // ─── 消防梯（Tenement 专属）─────────────────

        private static void BuildFireEscape(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfW = config.Width * 0.5f;
            float xPos = -halfW - 0.08f;
            float floorH = config.FloorHeight;

            for (int floor = 0; floor < config.Floors; floor++)
            {
                float zCenter = BaseZ + floor * floorH + floorH * 0.5f;
                float platformDepth = 0.15f;
                float yStart = -config.Depth * 0.3f;
                float yLen = config.Depth * 0.4f;

                // 每层平台
                GameObject platform = CreateCube($"FireEscape_Platform_F{floor}", gen,
                    root.transform,
                    new Vector3(xPos, yStart + yLen * 0.5f, zCenter),
                    new Vector3(platformDepth, yLen, 0.02f));
                SetMaterial(platform, new Color(0.25f, 0.23f, 0.2f, 1f));

                // 护栏竖杆
                int railingCount = 3;
                for (int r = 0; r < railingCount; r++)
                {
                    float ry = yStart + r * yLen / (railingCount - 1);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject rail = CreateCube(
                            $"FireEscape_Rail_F{floor}_{r}_{side}", gen, root.transform,
                            new Vector3(xPos + side * platformDepth * 0.4f, ry, zCenter + 0.05f),
                            new Vector3(0.012f, 0.012f, 0.1f));
                        SetMaterial(rail, new Color(0.3f, 0.28f, 0.24f, 1f));
                    }
                }
            }

            // 梯子纵梁
            float totalHeight = config.Floors * floorH;
            float ladderX = xPos - 0.1f;
            float ladderY = -config.Depth * 0.1f;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject beam = CreateCube($"FireEscape_Beam_{side}", gen, root.transform,
                    new Vector3(ladderX + side * 0.025f, ladderY, BaseZ + totalHeight * 0.5f),
                    new Vector3(0.012f, 0.012f, totalHeight));
                SetMaterial(beam, new Color(0.22f, 0.2f, 0.17f, 1f));
            }

            // 横档
            int rungCount = config.Floors * 4;
            for (int i = 0; i < rungCount; i++)
            {
                float rz = BaseZ + i * totalHeight / rungCount;
                GameObject rung = CreateCube($"FireEscape_Rung_{i}", gen, root.transform,
                    new Vector3(ladderX, ladderY, rz),
                    new Vector3(0.06f, 0.008f, 0.008f));
                SetMaterial(rung, new Color(0.28f, 0.25f, 0.22f, 1f));
            }
        }

        // ─── 卷帘门（Warehouse / NightMarket）───────

        private static void BuildRollerDoor(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfD = config.Depth * 0.5f;
            float doorWidth = Mathf.Min(config.Width * 0.7f, 0.7f);
            float doorHeight = config.FloorHeight * 0.85f;
            float entranceY = -halfD - WallThickness * 0.5f;

            // 波纹卷帘门主体（用多个细Cube模拟波纹）
            int slatCount = 8;
            float slatHeight = doorHeight / slatCount;
            for (int i = 0; i < slatCount; i++)
            {
                float sz = BaseZ + i * slatHeight + slatHeight * 0.5f;
                GameObject slat = CreateCube($"RollerSlat_{i}", gen, root.transform,
                    new Vector3(0f, entranceY - 0.01f, sz),
                    new Vector3(doorWidth, 0.012f, slatHeight - 0.003f));
                SetMaterial(slat, new Color(0.3f, 0.3f, 0.38f, 1f));
            }

            // 顶部卷轴箱
            GameObject rollerBox = CreateCube("RollerBox", gen, root.transform,
                new Vector3(0f, entranceY - 0.06f, BaseZ + doorHeight + 0.02f),
                new Vector3(doorWidth + 0.06f, 0.1f, 0.05f));
            SetMaterial(rollerBox, new Color(0.22f, 0.22f, 0.28f, 1f));
        }

        // ─── 霓虹招牌（NightMarket）─────────────────

        private static void BuildNeonSign(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfD = config.Depth * 0.5f;
            float signY = halfD + 0.08f;
            float signZ = BaseZ + config.Floors * config.FloorHeight * 0.7f;
            float signWidth = config.Width * 0.55f;
            float signHeight = 0.12f;

            // 招牌底板
            GameObject signBoard = CreateCube("NeonSign_Board", gen, root.transform,
                new Vector3(0f, signY, signZ),
                new Vector3(signWidth, 0.02f, signHeight));
            SetSimpleMaterial(signBoard, new Color(0.08f, 0.08f, 0.08f, 1f));

            // 霓虹发光条 — 使用 MaterialFactory
            GameObject neonTube = CreateCube("NeonSign_Glow", gen, root.transform,
                new Vector3(0f, signY + 0.015f, signZ),
                new Vector3(signWidth - 0.04f, 0.005f, signHeight - 0.02f));
            Material neonMat = MaterialFactory.GetNeonMaterial(config.AccentColor, 2.5f);
            neonTube.GetComponent<MeshRenderer>().sharedMaterial = neonMat;

            // 支撑架
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject bracket = CreateCube($"NeonSign_Bracket_{side}", gen,
                    root.transform,
                    new Vector3(side * signWidth * 0.45f, signY - 0.04f, signZ - 0.04f),
                    new Vector3(0.015f, 0.08f, 0.06f));
                SetSimpleMaterial(bracket, DoorFrameColor);
            }
        }

        // ─── 红十字标志（Clinic）───────────────────

        private static void BuildRedCross(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float halfD = config.Depth * 0.5f;
            float signY = halfD + 0.06f;
            float signZ = BaseZ + config.Floors * config.FloorHeight * 0.6f;
            float size = 0.15f;
            float thickness = 0.04f;

            // 竖条
            GameObject vertical = CreateCube("RedCross_V", gen, root.transform,
                new Vector3(0f, signY, signZ),
                new Vector3(thickness, 0.01f, size));
            SetMaterial(vertical, config.AccentColor);

            // 横条
            GameObject horizontal = CreateCube("RedCross_H", gen, root.transform,
                new Vector3(0f, signY, signZ),
                new Vector3(size, 0.01f, thickness));
            SetMaterial(horizontal, config.AccentColor);
        }

        // ─── 天台水箱 ──────────────────────────────

        private static void BuildRoofWaterTank(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float tankZ = BaseZ + config.Floors * config.FloorHeight + 0.1f;
            float offsetX = -config.Width * 0.25f;
            float offsetY = config.Depth * 0.2f;
            float tankSize = Random.Range(0.15f, 0.22f);
            float tankHeight = Random.Range(0.18f, 0.25f);

            // 支架腿
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            {
                GameObject leg = CreateCube("Tank_Leg", gen, root.transform,
                    new Vector3(offsetX + ix * tankSize * 0.4f,
                                offsetY + iy * tankSize * 0.4f,
                                tankZ - tankHeight * 0.2f),
                    new Vector3(0.015f, 0.015f, tankHeight * 0.5f));
                SetMaterial(leg, new Color(0.22f, 0.2f, 0.18f, 1f));
            }

            // 水箱主体
            GameObject tank = CreateCube("WaterTank", gen, root.transform,
                new Vector3(offsetX, offsetY, tankZ),
                new Vector3(tankSize, tankSize, tankHeight));
            SetMaterial(tank, new Color(0.55f, 0.52f, 0.45f, 0.85f));

            // 顶盖
            GameObject lid = CreateCube("Tank_Lid", gen, root.transform,
                new Vector3(offsetX, offsetY, tankZ + tankHeight * 0.5f + 0.01f),
                new Vector3(tankSize + 0.02f, tankSize + 0.02f, 0.02f));
            SetMaterial(lid, new Color(0.3f, 0.28f, 0.24f, 1f));
        }

        // ─── 天台空调外机 ──────────────────────────

        private static void BuildRoofACUnit(GameObject root, BuildingConfig config, List<GameObject> gen)
        {
            float acZ = BaseZ + config.Floors * config.FloorHeight + 0.08f;
            float offsetX = config.Width * 0.3f;
            float offsetY = -config.Depth * 0.15f;
            float acW = Random.Range(0.14f, 0.2f);
            float acD = acW * 0.6f;
            float acH = Random.Range(0.12f, 0.16f);

            // 主体
            GameObject ac = CreateCube("AC_Unit", gen, root.transform,
                new Vector3(offsetX, offsetY, acZ),
                new Vector3(acW, acD, acH));
            SetMaterial(ac, new Color(0.75f, 0.73f, 0.68f, 1f));

            // 风扇格栅
            GameObject grille = CreateCube("AC_Grille", gen, root.transform,
                new Vector3(offsetX, offsetY + acD * 0.55f, acZ),
                new Vector3(acW - 0.02f, 0.005f, acH - 0.02f));
            SetMaterial(grille, new Color(0.28f, 0.28f, 0.3f, 1f));
        }

        // ─── 内部辅助 ──────────────────────────────

        private static void CreateWallCube(GameObject root, string name, List<GameObject> gen,
            Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject cube = CreateCube(name, gen, root.transform, localPos, scale);
            SetSimpleMaterial(cube, color);
        }

        /// <summary>
        /// 创建立方体并正确设置父子关系和本地坐标。
        /// 先设 parent，再设 localPosition，保证坐标相对于父节点。
        /// </summary>
        private static GameObject CreateCube(string name, List<GameObject> gen,
            Transform parent, Vector3 localPos, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            gen.Add(cube);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            return cube;
        }

        private static void SetMaterial(GameObject obj, Color color)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.sharedMaterial = MaterialFactory.GetSimpleMaterial(color);
        }

        private static void SetSimpleMaterial(GameObject obj, Color color)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.sharedMaterial = MaterialFactory.GetSimpleMaterial(color);
        }
    }

    // ─── 数据结构 ──────────────────────────────────

    public enum BuildingType
    {
        Tenement,
        Warehouse,
        Office,
        Clinic,
        NightMarket
    }

    public enum RoofStyle
    {
        Flat,
        Gable,
        Slanted
    }

    [Serializable]
    public struct BuildingConfig
    {
        public BuildingType Type;
        public Vector3 Position;
        public float Width;
        public float Depth;
        public int Floors;
        public Color PrimaryColor;
        public Color SecondaryColor;
        public Color AccentColor;
        public float FloorHeight;
        public int WindowCountPerFloor;
        public bool HasFireEscape;
        public bool HasRoofWaterTank;
        public bool HasACUnit;
        public bool HasRollerDoor;
        public bool HasNeonSign;
        public RoofStyle RoofType;
    }
}