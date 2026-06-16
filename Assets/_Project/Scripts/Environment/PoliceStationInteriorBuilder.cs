using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.World;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.Environment
{
    /// <summary>
    /// 警察局室内装饰程序化生成器。
    /// 为 PoliceStationMap.cs 定义的 6 个区域生成室内场景物品，
    /// 使用程序化几何体 + MaterialFactory 材质系统，支持 LOD 分组。
    ///
    /// 区域映射：
    ///   Lobby         → 大厅（接待台、长椅、公告栏、饮水机、贩卖机）
    ///   Interrogation → 审讯室（单向玻璃、金属桌椅、台灯、录音设备）
    ///   Evidence       → 证物室（铁架、证物箱、保险柜）
    ///   Armory         → 监控室（监视器墙、控制台、通讯设备）
    ///   Cells           → 拘留室（铁栅栏、简易床铺、马桶、监控摄像头）
    ///   Briefing       → 办公室（办公桌、档案柜、电脑、咖啡机、白板）
    /// </summary>
    public static class PoliceStationInteriorBuilder
    {
        // ─── 常量 ────────────────────────────────
        private const float BaseZ = 0.05f;
        private const float FurnitureZ = BaseZ + 0.01f;

        // ─── 通用色板 ────────────────────────────
        private static readonly Color DeskWood = new Color(0.36f, 0.25f, 0.16f, 1f);       // 深木色
        private static readonly Color MetalDark = new Color(0.22f, 0.23f, 0.25f, 1f);       // 暗金属
        private static readonly Color MetalLight = new Color(0.45f, 0.47f, 0.50f, 1f);      // 浅金属
        private static readonly Color GlassBlue = new Color(0.35f, 0.48f, 0.62f, 0.55f);    // 蓝色半透玻璃
        private static readonly Color PlasticBeige = new Color(0.82f, 0.78f, 0.70f, 1f);    // 米白塑料
        private static readonly Color PlasticWhite = new Color(0.90f, 0.88f, 0.85f, 1f);    // 白色
        private static readonly Color RubberBlack = new Color(0.10f, 0.10f, 0.11f, 1f);     // 黑色橡胶
        private static readonly Color ScreenGlow = new Color(0.15f, 0.65f, 0.85f, 1f);      // 屏幕蓝光
        private static readonly Color ScreenDark = new Color(0.04f, 0.05f, 0.08f, 0.92f);   // 关机屏幕
        private static readonly Color WallGray = new Color(0.55f, 0.54f, 0.52f, 1f);        // 墙面灰
        private static readonly Color BoardWhite = new Color(0.88f, 0.87f, 0.84f, 1f);      // 白板白
        private static readonly Color RedAccent = new Color(0.75f, 0.12f, 0.08f, 1f);       // 红色强调
        private static readonly Color GreenAccent = new Color(0.12f, 0.55f, 0.18f, 1f);     // 绿色强调
        private static readonly Color LabelYellow = new Color(0.82f, 0.75f, 0.25f, 1f);     // 标签黄
        private static readonly Color BarGray = new Color(0.30f, 0.31f, 0.33f, 1f);         // 铁栅栏灰
        private static readonly Color MattressBlue = new Color(0.28f, 0.32f, 0.45f, 1f);    // 床垫蓝
        private static readonly Color ToiletWhite = new Color(0.85f, 0.84f, 0.82f, 1f);     // 马桶白
        private static readonly Color CameraBlack = new Color(0.08f, 0.08f, 0.09f, 1f);     // 摄像头黑
        private static readonly Color SafeGreen = new Color(0.18f, 0.32f, 0.22f, 1f);       // 保险柜绿
        private static readonly Color CoffeeBrown = new Color(0.22f, 0.16f, 0.11f, 1f);     // 咖啡机棕

        // ─── 主入口 ──────────────────────────────

        /// <summary>
        /// 为警察局全部 6 个区域生成室内装饰。
        /// </summary>
        public static void BuildAllInteriors(Transform parent, List<GameObject> generatedObjects)
        {
            // 按区域构建，每个区域一个独立父节点 + LOD 分组
            BuildLobby(parent, generatedObjects);
            BuildInterrogationRoom(parent, generatedObjects);
            BuildEvidenceRoom(parent, generatedObjects);
            BuildMonitorRoom(parent, generatedObjects);
            BuildCellBlock(parent, generatedObjects);
            BuildOfficeRoom(parent, generatedObjects);
        }

        // ─── 大厅（Lobby）─────────────────────────

        private static void BuildLobby(Transform parent, List<GameObject> gen)
        {
            PoliceStationMap.Area area = PoliceStationMap.Area.Lobby;
            Vector3 center = PoliceStationMap.GetAreaCenter(area);
            Vector2 size = PoliceStationMap.GetAreaSize(area);

            GameObject root = CreateAreaRoot("Lobby_Interior", center, parent, gen);
            GameObject lod0 = CreateLodGroup(root, "LOD0", gen);

            // 接待台（靠后方居中）
            BuildReceptionDesk(lod0.transform, new Vector3(0f, size.y * 0.3f, FurnitureZ), gen);

            // 长椅 ×2（左右靠墙）
            BuildBench(lod0.transform, "Bench_L", new Vector3(-size.x * 0.35f, -size.y * 0.3f, FurnitureZ), gen);
            BuildBench(lod0.transform, "Bench_R", new Vector3(size.x * 0.35f, -size.y * 0.3f, FurnitureZ), gen);

            // 公告栏（后墙）
            BuildBulletinBoard(lod0.transform, new Vector3(-size.x * 0.28f, size.y * 0.35f, FurnitureZ + 0.1f), gen);

            // 饮水机（一侧角落）
            BuildWaterDispenser(lod0.transform, new Vector3(-size.x * 0.38f, -size.y * 0.38f, FurnitureZ), gen);

            // 贩卖机（另一侧角落）
            BuildVendingMachine(lod0.transform, new Vector3(size.x * 0.38f, -size.y * 0.38f, FurnitureZ), gen);

            // LOD1 简化版（仅保留接待台轮廓）
            CreateLodGroup(root, "LOD1", gen);
        }

        // ─── 审讯室（Interrogation）───────────────

        private static void BuildInterrogationRoom(Transform parent, List<GameObject> gen)
        {
            PoliceStationMap.Area area = PoliceStationMap.Area.Interrogation;
            Vector3 center = PoliceStationMap.GetAreaCenter(area);
            Vector2 size = PoliceStationMap.GetAreaSize(area);

            GameObject root = CreateAreaRoot("Interrogation_Interior", center, parent, gen);
            GameObject lod0 = CreateLodGroup(root, "LOD0", gen);

            // 单向玻璃（前墙观察面）
            BuildOneWayMirror(lod0.transform, new Vector3(0f, -size.y * 0.4f, FurnitureZ + 0.08f), size.x * 0.7f, gen);

            // 金属桌（中央略偏后）
            BuildMetalTable(lod0.transform, new Vector3(0f, size.y * 0.12f, FurnitureZ), gen);

            // 金属椅 ×2（桌两侧）
            BuildMetalChair(lod0.transform, "Chair_Suspect", new Vector3(0f, -size.y * 0.15f, FurnitureZ), gen);
            BuildMetalChair(lod0.transform, "Chair_Officer", new Vector3(0f, size.y * 0.3f, FurnitureZ), gen);

            // 台灯（桌上）
            BuildDeskLamp(lod0.transform, new Vector3(0.08f, size.y * 0.15f, FurnitureZ + 0.07f), gen);

            // 录音设备（桌角）
            BuildRecordingDevice(lod0.transform, new Vector3(-0.09f, size.y * 0.18f, FurnitureZ + 0.04f), gen);

            CreateLodGroup(root, "LOD1", gen);
        }

        // ─── 证物室（Evidence）─────────────────────

        private static void BuildEvidenceRoom(Transform parent, List<GameObject> gen)
        {
            PoliceStationMap.Area area = PoliceStationMap.Area.Evidence;
            Vector3 center = PoliceStationMap.GetAreaCenter(area);
            Vector2 size = PoliceStationMap.GetAreaSize(area);

            GameObject root = CreateAreaRoot("Evidence_Interior", center, parent, gen);
            GameObject lod0 = CreateLodGroup(root, "LOD0", gen);

            // 铁架 ×2（靠后墙两侧）
            BuildMetalShelving(lod0.transform, "Shelf_L", new Vector3(-size.x * 0.3f, size.y * 0.28f, FurnitureZ), gen);
            BuildMetalShelving(lod0.transform, "Shelf_R", new Vector3(size.x * 0.3f, size.y * 0.28f, FurnitureZ), gen);

            // 证物箱（铁架上及地面）
            int boxId = 1;
            BuildEvidenceBox(lod0.transform, boxId++, new Vector3(-size.x * 0.25f, size.y * 0.25f, FurnitureZ + 0.12f), gen);
            BuildEvidenceBox(lod0.transform, boxId++, new Vector3(-size.x * 0.32f, size.y * 0.2f, FurnitureZ + 0.06f), gen);
            BuildEvidenceBox(lod0.transform, boxId++, new Vector3(size.x * 0.28f, size.y * 0.22f, FurnitureZ + 0.12f), gen);
            BuildEvidenceBox(lod0.transform, boxId++, new Vector3(size.x * 0.34f, size.y * 0.18f, FurnitureZ + 0.06f), gen);
            BuildEvidenceBox(lod0.transform, boxId++, new Vector3(-size.x * 0.15f, -size.y * 0.15f, FurnitureZ), gen);

            // 保险柜（角落）
            BuildSafe(lod0.transform, new Vector3(size.x * 0.35f, -size.y * 0.35f, FurnitureZ), gen);

            CreateLodGroup(root, "LOD1", gen);
        }

        // ─── 监控室 ─────────────────────────────

        private static void BuildMonitorRoom(Transform parent, List<GameObject> gen)
        {
            PoliceStationMap.Area area = PoliceStationMap.Area.Surveillance;
            Vector3 center = PoliceStationMap.GetAreaCenter(area);
            Vector2 size = PoliceStationMap.GetAreaSize(area);

            GameObject root = CreateAreaRoot("Monitor_Interior", center, parent, gen);
            GameObject lod0 = CreateLodGroup(root, "LOD0", gen);

            // 监视器墙（多屏幕，后墙）
            BuildMonitorWall(lod0.transform, new Vector3(0f, size.y * 0.35f, FurnitureZ + 0.06f),
                size.x * 0.75f, gen);

            // 控制台（监视器墙前）
            BuildControlConsole(lod0.transform, new Vector3(0f, size.y * 0.12f, FurnitureZ), gen);

            // 通讯设备（控制台旁）
            BuildCommEquipment(lod0.transform, new Vector3(size.x * 0.28f, size.y * 0.05f, FurnitureZ + 0.05f), gen);

            CreateLodGroup(root, "LOD1", gen);
        }

        // ─── 拘留室（Cells）───────────────────────

        private static void BuildCellBlock(Transform parent, List<GameObject> gen)
        {
            PoliceStationMap.Area area = PoliceStationMap.Area.Cells;
            Vector3 center = PoliceStationMap.GetAreaCenter(area);
            Vector2 size = PoliceStationMap.GetAreaSize(area);

            GameObject root = CreateAreaRoot("Cells_Interior", center, parent, gen);
            GameObject lod0 = CreateLodGroup(root, "LOD0", gen);

            // 铁栅栏（前侧，分隔拘留室与走廊）
            BuildIronBars(lod0.transform, new Vector3(0f, -size.y * 0.35f, FurnitureZ),
                size.x * 0.8f, gen);

            // 简易床铺（靠后墙）
            BuildSimpleBed(lod0.transform, new Vector3(-size.x * 0.2f, size.y * 0.22f, FurnitureZ), gen);

            // 马桶（角落）
            BuildToilet(lod0.transform, new Vector3(size.x * 0.32f, size.y * 0.3f, FurnitureZ), gen);

            // 监控摄像头（天花板角落）
            BuildSurveillanceCamera(lod0.transform, new Vector3(size.x * 0.3f, -size.y * 0.3f, FurnitureZ + 0.18f), gen);

            CreateLodGroup(root, "LOD1", gen);
        }

        // ─── 办公室（Briefing → 办公室）───────────

        private static void BuildOfficeRoom(Transform parent, List<GameObject> gen)
        {
            PoliceStationMap.Area area = PoliceStationMap.Area.Briefing;
            Vector3 center = PoliceStationMap.GetAreaCenter(area);
            Vector2 size = PoliceStationMap.GetAreaSize(area);

            GameObject root = CreateAreaRoot("Office_Interior", center, parent, gen);
            GameObject lod0 = CreateLodGroup(root, "LOD0", gen);

            // 办公桌（居中偏后）
            BuildOfficeDesk(lod0.transform, new Vector3(0f, size.y * 0.15f, FurnitureZ), gen);

            // 档案柜（靠墙）
            BuildFilingCabinet(lod0.transform, "Cabinet_L", new Vector3(-size.x * 0.35f, size.y * 0.3f, FurnitureZ), gen);
            BuildFilingCabinet(lod0.transform, "Cabinet_R", new Vector3(size.x * 0.35f, size.y * 0.3f, FurnitureZ), gen);

            // 电脑（办公桌上）
            BuildComputer(lod0.transform, new Vector3(0.06f, size.y * 0.18f, FurnitureZ + 0.07f), gen);

            // 咖啡机（边桌/档案柜旁）
            BuildCoffeeMachine(lod0.transform, new Vector3(-size.x * 0.25f, -size.y * 0.28f, FurnitureZ), gen);

            // 白板（侧墙）
            BuildWhiteboard(lod0.transform, new Vector3(size.x * 0.38f, -size.y * 0.05f, FurnitureZ + 0.08f), gen);

            CreateLodGroup(root, "LOD1", gen);
        }

        // ══════════════════════════════════════════
        //  家具构建方法
        // ══════════════════════════════════════════

        // ─── 接待台 ──────────────────────────────
        private static void BuildReceptionDesk(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float deskW = 0.55f;
            float deskD = 0.22f;
            float deskH = 0.16f;
            float counterH = 0.19f;

            // 主体桌面
            CreateFurniture("Desk_Top", parent, gen,
                pos + new Vector3(0f, 0f, deskH * 0.5f),
                new Vector3(deskW, deskD, 0.02f), DeskWood);

            // 前挡板
            CreateFurniture("Desk_Front", parent, gen,
                pos + new Vector3(0f, deskD * 0.5f, deskH * 0.35f),
                new Vector3(deskW, 0.015f, deskH * 0.7f), DeskWood);

            // 两侧立板
            for (int side = -1; side <= 1; side += 2)
            {
                CreateFurniture($"Desk_Side_{side}", parent, gen,
                    pos + new Vector3(side * deskW * 0.48f, 0f, deskH * 0.35f),
                    new Vector3(0.015f, deskD - 0.02f, deskH * 0.7f), DeskWood);
            }

            // 台面升高部分（接待人员侧）
            CreateFurniture("Counter_Riser", parent, gen,
                pos + new Vector3(0f, -deskD * 0.2f, counterH - 0.015f),
                new Vector3(deskW * 0.9f, deskD * 0.3f, 0.02f), DeskWood);
        }

        // ─── 长椅 ────────────────────────────────
        private static void BuildBench(Transform parent, string name, Vector3 pos, List<GameObject> gen)
        {
            float benchW = 0.40f;
            float benchD = 0.12f;
            float seatH = 0.07f;
            float legH = 0.06f;

            // 座面
            CreateFurniture($"{name}_Seat", parent, gen,
                pos + new Vector3(0f, 0f, seatH),
                new Vector3(benchW, benchD, 0.02f), DeskWood);

            // 四条腿
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            {
                CreateFurniture($"{name}_Leg_{ix}_{iy}", parent, gen,
                    pos + new Vector3(ix * benchW * 0.42f, iy * benchD * 0.38f, legH * 0.5f),
                    new Vector3(0.015f, 0.015f, legH), MetalDark);
            }

            // 靠背（后侧竖板）
            CreateFurniture($"{name}_Back", parent, gen,
                pos + new Vector3(0f, benchD * 0.5f, seatH + 0.06f),
                new Vector3(benchW, 0.012f, 0.1f), DeskWood);
        }

        // ─── 公告栏 ──────────────────────────────
        private static void BuildBulletinBoard(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float boardW = 0.22f;
            float boardH = 0.16f;

            // 底板（软木色）
            CreateFurniture("Board", parent, gen, pos,
                new Vector3(boardW, 0.008f, boardH), new Color(0.68f, 0.54f, 0.35f, 1f));

            // 边框
            float fw = boardW * 0.5f;
            float fh = boardH * 0.5f;
            for (int dx = -1; dx <= 1; dx += 2)
            {
                CreateFurniture($"Board_Frame_V_{dx}", parent, gen,
                    pos + new Vector3(dx * fw, 0.006f, 0f),
                    new Vector3(0.012f, 0.006f, boardH), MetalDark);
            }
            for (int dz = -1; dz <= 1; dz += 2)
            {
                CreateFurniture($"Board_Frame_H_{dz}", parent, gen,
                    pos + new Vector3(0f, 0.006f, dz * fh),
                    new Vector3(boardW, 0.006f, 0.012f), MetalDark);
            }

            // 若干通知纸张（彩色小方块）
            Color[] paperColors = { Color.white, new Color(1f, 0.92f, 0.7f, 1f), new Color(0.78f, 0.9f, 1f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                float px = Random.Range(-fw * 0.6f, fw * 0.6f);
                float pz = Random.Range(-fh * 0.5f, fh * 0.5f);
                CreateFurniture($"Paper_{i}", parent, gen,
                    pos + new Vector3(px, 0.005f, pz),
                    new Vector3(0.04f, 0.001f, 0.05f), paperColors[i % paperColors.Length]);
            }
        }

        // ─── 饮水机 ──────────────────────────────
        private static void BuildWaterDispenser(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 机身
            CreateFurniture("Dispenser_Body", parent, gen,
                pos + new Vector3(0f, 0f, 0.09f),
                new Vector3(0.10f, 0.09f, 0.16f), PlasticWhite);

            // 水桶（顶部圆形→用扁Cube近似）
            CreateFurniture("Dispenser_Bottle", parent, gen,
                pos + new Vector3(0f, 0f, 0.18f),
                new Vector3(0.09f, 0.08f, 0.06f), GlassBlue);

            // 出水口
            CreateFurniture("Dispenser_Tap", parent, gen,
                pos + new Vector3(0f, 0.06f, 0.14f),
                new Vector3(0.02f, 0.02f, 0.015f), MetalLight);

            // 红色/蓝色龙头按钮
            CreateFurniture("Dispenser_Btn_Red", parent, gen,
                pos + new Vector3(-0.025f, 0.06f, 0.145f),
                new Vector3(0.012f, 0.012f, 0.006f), RedAccent);
            CreateFurniture("Dispenser_Btn_Blue", parent, gen,
                pos + new Vector3(0.025f, 0.06f, 0.145f),
                new Vector3(0.012f, 0.012f, 0.006f), ScreenGlow);
        }

        // ─── 贩卖机 ──────────────────────────────
        private static void BuildVendingMachine(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 机身
            CreateFurniture("Vending_Body", parent, gen,
                pos + new Vector3(0f, 0f, 0.1f),
                new Vector3(0.11f, 0.10f, 0.18f), new Color(0.85f, 0.15f, 0.18f, 1f));

            // 玻璃面板
            CreateFurniture("Vending_Glass", parent, gen,
                pos + new Vector3(0f, 0.055f, 0.13f),
                new Vector3(0.08f, 0.005f, 0.1f), GlassBlue);

            // 出货口
            CreateFurniture("Vending_Chute", parent, gen,
                pos + new Vector3(0f, 0.055f, 0.04f),
                new Vector3(0.07f, 0.03f, 0.02f), RubberBlack);

            // 按钮面板
            CreateFurniture("Vending_Panel", parent, gen,
                pos + new Vector3(0.04f, 0.055f, 0.08f),
                new Vector3(0.02f, 0.005f, 0.06f), MetalDark);
        }

        // ─── 单向玻璃 ─────────────────────────────
        private static void BuildOneWayMirror(Transform parent, Vector3 pos, float width, List<GameObject> gen)
        {
            float mirrorH = 0.18f;

            // 玻璃面板
            CreateFurniture("Mirror_Glass", parent, gen, pos,
                new Vector3(width, 0.006f, mirrorH), new Color(0.25f, 0.35f, 0.45f, 0.35f));

            // 金属边框
            float fw = width * 0.5f;
            float fh = mirrorH * 0.5f;
            for (int dx = -1; dx <= 1; dx += 2)
            {
                CreateFurniture($"Mirror_Frame_V_{dx}", parent, gen,
                    pos + new Vector3(dx * fw, 0.004f, 0f),
                    new Vector3(0.015f, 0.004f, mirrorH), MetalDark);
            }
            for (int dz = -1; dz <= 1; dz += 2)
            {
                CreateFurniture($"Mirror_Frame_H_{dz}", parent, gen,
                    pos + new Vector3(0f, 0.004f, dz * fh),
                    new Vector3(width, 0.004f, 0.015f), MetalDark);
            }
        }

        // ─── 金属桌 ──────────────────────────────
        private static void BuildMetalTable(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float tw = 0.28f;
            float td = 0.18f;

            CreateFurniture("MetalTable_Top", parent, gen,
                pos + new Vector3(0f, 0f, 0.12f),
                new Vector3(tw, td, 0.015f), MetalLight);

            // 四条金属腿
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            {
                CreateFurniture($"MetalTable_Leg_{ix}_{iy}", parent, gen,
                    pos + new Vector3(ix * tw * 0.42f, iy * td * 0.4f, 0.06f),
                    new Vector3(0.012f, 0.012f, 0.11f), MetalDark);
            }
        }

        // ─── 金属椅 ──────────────────────────────
        private static void BuildMetalChair(Transform parent, string name, Vector3 pos, List<GameObject> gen)
        {
            CreateFurniture($"{name}_Seat", parent, gen,
                pos + new Vector3(0f, 0f, 0.07f),
                new Vector3(0.10f, 0.10f, 0.012f), MetalLight);

            // 四条腿
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            {
                CreateFurniture($"{name}_Leg_{ix}_{iy}", parent, gen,
                    pos + new Vector3(ix * 0.04f, iy * 0.04f, 0.035f),
                    new Vector3(0.008f, 0.008f, 0.06f), MetalDark);
            }

            // 靠背
            CreateFurniture($"{name}_Back", parent, gen,
                pos + new Vector3(0f, 0.05f, 0.11f),
                new Vector3(0.10f, 0.006f, 0.08f), MetalLight);
        }

        // ─── 台灯 ────────────────────────────────
        private static void BuildDeskLamp(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            CreateFurniture("Lamp_Base", parent, gen,
                pos + new Vector3(0f, 0f, 0.01f),
                new Vector3(0.03f, 0.03f, 0.008f), MetalDark);

            CreateFurniture("Lamp_Stem", parent, gen,
                pos + new Vector3(0f, 0f, 0.04f),
                new Vector3(0.006f, 0.006f, 0.05f), MetalLight);

            CreateFurniture("Lamp_Shade", parent, gen,
                pos + new Vector3(0.02f, 0f, 0.07f),
                new Vector3(0.04f, 0.04f, 0.025f), new Color(1f, 0.92f, 0.68f, 0.85f));

            // 发光灯泡
            CreateFurniture("Lamp_Bulb", parent, gen,
                pos + new Vector3(0.02f, 0f, 0.06f),
                new Vector3(0.012f, 0.012f, 0.012f), new Color(1f, 0.95f, 0.8f, 1f));
            // 自发光材质
            GameObject bulb = parent.Find("Lamp_Bulb")?.gameObject;
            if (bulb != null)
            {
                bulb.GetComponent<MeshRenderer>().sharedMaterial =
                    MaterialFactory.GetNeonMaterial(new Color(1f, 0.95f, 0.8f, 1f), 1.2f);
            }
        }

        // ─── 录音设备 ─────────────────────────────
        private static void BuildRecordingDevice(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 录音机主体
            CreateFurniture("Recorder_Body", parent, gen, pos,
                new Vector3(0.06f, 0.04f, 0.025f), RubberBlack);

            // 磁带仓
            CreateFurniture("Recorder_Tape", parent, gen,
                pos + new Vector3(0f, 0f, 0.015f),
                new Vector3(0.035f, 0.025f, 0.008f), new Color(0.3f, 0.3f, 0.3f, 1f));

            // 红色录制指示灯
            CreateFurniture("Recorder_LED", parent, gen,
                pos + new Vector3(0.02f, 0.022f, 0.013f),
                new Vector3(0.006f, 0.004f, 0.003f), RedAccent);
            // 发光材质
            GameObject led = parent.Find("Recorder_LED")?.gameObject;
            if (led != null)
            {
                led.GetComponent<MeshRenderer>().sharedMaterial =
                    MaterialFactory.GetNeonMaterial(RedAccent, 0.8f);
            }

            // 麦克风（小圆柱→Cube近似）
            CreateFurniture("Recorder_Mic", parent, gen,
                pos + new Vector3(-0.03f, 0f, 0.02f),
                new Vector3(0.01f, 0.01f, 0.03f), MetalDark);
        }

        // ─── 铁架 ────────────────────────────────
        private static void BuildMetalShelving(Transform parent, string name, Vector3 pos, List<GameObject> gen)
        {
            float shelfW = 0.2f;
            float shelfD = 0.14f;
            float shelfH = 0.2f;
            int shelfCount = 3;

            // 四根立柱
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            {
                CreateFurniture($"{name}_Post_{ix}_{iy}", parent, gen,
                    pos + new Vector3(ix * shelfW * 0.48f, iy * shelfD * 0.46f, shelfH * 0.5f),
                    new Vector3(0.008f, 0.008f, shelfH), MetalDark);
            }

            // 层板
            for (int s = 0; s < shelfCount; s++)
            {
                float sz = 0.03f + s * (shelfH - 0.06f) / (shelfCount - 1);
                CreateFurniture($"{name}_Shelf_{s}", parent, gen,
                    pos + new Vector3(0f, 0f, sz),
                    new Vector3(shelfW, shelfD, 0.01f), MetalLight);
            }
        }

        // ─── 证物箱（带编号标签）──────────────────
        private static void BuildEvidenceBox(Transform parent, int boxId, Vector3 pos, List<GameObject> gen)
        {
            float bw = Random.Range(0.05f, 0.08f);
            float bd = Random.Range(0.04f, 0.07f);
            float bh = Random.Range(0.04f, 0.06f);

            CreateFurniture($"EvidenceBox_{boxId}", parent, gen,
                pos + new Vector3(0f, 0f, bh * 0.5f),
                new Vector3(bw, bd, bh), new Color(0.55f, 0.42f, 0.25f, 1f)); // 纸箱色

            // 编号标签（黄色小条）
            CreateFurniture($"EvidenceBox_{boxId}_Label", parent, gen,
                pos + new Vector3(0f, bd * 0.52f, bh),
                new Vector3(bw * 0.6f, 0.002f, bh * 0.2f), LabelYellow);
        }

        // ─── 保险柜 ──────────────────────────────
        private static void BuildSafe(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float safeW = 0.13f;
            float safeD = 0.11f;
            float safeH = 0.14f;

            // 柜体
            CreateFurniture("Safe_Body", parent, gen,
                pos + new Vector3(0f, 0f, safeH * 0.5f),
                new Vector3(safeW, safeD, safeH), SafeGreen);

            // 门（正面稍小）
            CreateFurniture("Safe_Door", parent, gen,
                pos + new Vector3(0f, safeD * 0.52f, safeH * 0.5f),
                new Vector3(safeW * 0.85f, 0.006f, safeH * 0.85f), new Color(0.25f, 0.42f, 0.3f, 1f));

            // 转盘锁
            CreateFurniture("Safe_Dial", parent, gen,
                pos + new Vector3(0f, safeD * 0.56f, safeH * 0.55f),
                new Vector3(0.025f, 0.004f, 0.025f), MetalLight);

            // 把手
            CreateFurniture("Safe_Handle", parent, gen,
                pos + new Vector3(0.025f, safeD * 0.57f, safeH * 0.4f),
                new Vector3(0.006f, 0.015f, 0.006f), MetalDark);
        }

        // ─── 监视器墙 ─────────────────────────────
        private static void BuildMonitorWall(Transform parent, Vector3 pos, float totalWidth, List<GameObject> gen)
        {
            int screenCols = 3;
            int screenRows = 2;
            float screenW = 0.1f;
            float screenH = 0.08f;
            float gapX = 0.015f;
            float gapZ = 0.012f;

            float gridW = screenCols * screenW + (screenCols - 1) * gapX;
            float gridH = screenRows * screenH + (screenRows - 1) * gapZ;

            // 背板
            CreateFurniture("MonitorWall_Back", parent, gen, pos,
                new Vector3(totalWidth, 0.008f, gridH + 0.02f), MetalDark);

            // 屏幕网格
            for (int row = 0; row < screenRows; row++)
            {
                for (int col = 0; col < screenCols; col++)
                {
                    float sx = -gridW * 0.5f + col * (screenW + gapX) + screenW * 0.5f;
                    float sz = -gridH * 0.5f + row * (screenH + gapZ) + screenH * 0.5f;

                    // 屏幕边框
                    CreateFurniture($"Screen_{row}_{col}_Frame", parent, gen,
                        pos + new Vector3(sx, 0.005f, sz),
                        new Vector3(screenW + 0.01f, 0.003f, screenH + 0.01f), RubberBlack);

                    // 屏幕面板（部分亮部分暗）
                    bool isOn = (row + col) % 3 != 0; // 2/3 亮
                    CreateFurniture($"Screen_{row}_{col}_Panel", parent, gen,
                        pos + new Vector3(sx, 0.009f, sz),
                        new Vector3(screenW - 0.004f, 0.002f, screenH - 0.004f),
                        isOn ? ScreenGlow : ScreenDark);

                    if (isOn)
                    {
                        GameObject panel = parent.Find($"Screen_{row}_{col}_Panel")?.gameObject;
                        if (panel != null)
                        {
                            panel.GetComponent<MeshRenderer>().sharedMaterial =
                                MaterialFactory.GetNeonMaterial(ScreenGlow, 0.5f);
                        }
                    }
                }
            }
        }

        // ─── 控制台 ──────────────────────────────
        private static void BuildControlConsole(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float consoleW = 0.35f;
            float consoleD = 0.16f;
            float consoleH = 0.12f;

            // 桌面
            CreateFurniture("Console_Top", parent, gen,
                pos + new Vector3(0f, 0f, consoleH),
                new Vector3(consoleW, consoleD, 0.012f), MetalDark);

            // 前挡板（倾斜感：上部窄于桌面）
            CreateFurniture("Console_Front", parent, gen,
                pos + new Vector3(0f, consoleD * 0.5f, consoleH * 0.5f),
                new Vector3(consoleW, 0.01f, consoleH), MetalDark);

            // 按钮面板
            CreateFurniture("Console_Panel", parent, gen,
                pos + new Vector3(-consoleW * 0.2f, consoleD * 0.55f, consoleH + 0.006f),
                new Vector3(consoleW * 0.25f, 0.004f, 0.04f), RubberBlack);

            // 若干小按钮
            Color[] btnColors = { RedAccent, GreenAccent, ScreenGlow, new Color(1f, 0.72f, 0.08f, 1f) };
            for (int i = 0; i < 5; i++)
            {
                float bx = -consoleW * 0.2f + i * 0.03f;
                CreateFurniture($"Console_Btn_{i}", parent, gen,
                    pos + new Vector3(bx, consoleD * 0.56f, consoleH + 0.024f),
                    new Vector3(0.01f, 0.003f, 0.01f), btnColors[i % btnColors.Length]);
            }

            // 键盘区域
            CreateFurniture("Console_Keyboard", parent, gen,
                pos + new Vector3(consoleW * 0.15f, consoleD * 0.53f, consoleH + 0.006f),
                new Vector3(consoleW * 0.35f, 0.004f, 0.03f), RubberBlack);
        }

        // ─── 通讯设备 ─────────────────────────────
        private static void BuildCommEquipment(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 无线电底座
            CreateFurniture("Comm_Base", parent, gen,
                pos + new Vector3(0f, 0f, 0.02f),
                new Vector3(0.08f, 0.06f, 0.03f), RubberBlack);

            // 机身
            CreateFurniture("Comm_Body", parent, gen,
                pos + new Vector3(0f, 0f, 0.05f),
                new Vector3(0.06f, 0.05f, 0.035f), MetalDark);

            // 天线
            CreateFurniture("Comm_Antenna", parent, gen,
                pos + new Vector3(0.02f, 0f, 0.09f),
                new Vector3(0.004f, 0.004f, 0.06f), MetalLight);

            // 频率显示屏
            CreateFurniture("Comm_Display", parent, gen,
                pos + new Vector3(-0.015f, 0.028f, 0.06f),
                new Vector3(0.025f, 0.003f, 0.012f), ScreenGlow);

            // 扬声器格栅
            CreateFurniture("Comm_Speaker", parent, gen,
                pos + new Vector3(0.02f, 0.028f, 0.04f),
                new Vector3(0.03f, 0.003f, 0.015f), MetalLight);
        }

        // ─── 铁栅栏 ──────────────────────────────
        private static void BuildIronBars(Transform parent, Vector3 pos, float totalWidth, List<GameObject> gen)
        {
            int barCount = 7;
            float barHeight = 0.18f;
            float spacing = totalWidth / (barCount - 1);

            // 上横梁
            CreateFurniture("Bars_TopRail", parent, gen,
                pos + new Vector3(0f, 0f, barHeight - 0.01f),
                new Vector3(totalWidth, 0.015f, 0.015f), BarGray);

            // 下横梁
            CreateFurniture("Bars_BottomRail", parent, gen,
                pos + new Vector3(0f, 0f, 0.01f),
                new Vector3(totalWidth, 0.015f, 0.015f), BarGray);

            // 竖杆
            for (int i = 0; i < barCount; i++)
            {
                float bx = -totalWidth * 0.5f + i * spacing;
                CreateFurniture($"Bars_Vertical_{i}", parent, gen,
                    pos + new Vector3(bx, 0f, barHeight * 0.5f),
                    new Vector3(0.008f, 0.008f, barHeight), BarGray);
            }
        }

        // ─── 简易床铺 ─────────────────────────────
        private static void BuildSimpleBed(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float bedW = 0.2f;
            float bedD = 0.25f;

            // 床架
            CreateFurniture("Bed_Frame", parent, gen,
                pos + new Vector3(0f, 0f, 0.03f),
                new Vector3(bedW, bedD, 0.04f), MetalDark);

            // 床垫
            CreateFurniture("Bed_Mattress", parent, gen,
                pos + new Vector3(0f, 0f, 0.06f),
                new Vector3(bedW - 0.02f, bedD - 0.02f, 0.025f), MattressBlue);

            // 枕头
            CreateFurniture("Bed_Pillow", parent, gen,
                pos + new Vector3(0f, bedD * 0.4f, 0.07f),
                new Vector3(bedW * 0.7f, bedD * 0.2f, 0.015f), PlasticWhite);
        }

        // ─── 马桶 ────────────────────────────────
        private static void BuildToilet(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 水箱
            CreateFurniture("Toilet_Tank", parent, gen,
                pos + new Vector3(0f, 0.06f, 0.07f),
                new Vector3(0.06f, 0.05f, 0.08f), ToiletWhite);

            // 座体
            CreateFurniture("Toilet_Bowl", parent, gen,
                pos + new Vector3(0f, -0.02f, 0.03f),
                new Vector3(0.07f, 0.08f, 0.04f), ToiletWhite);

            // 座圈
            CreateFurniture("Toilet_Seat", parent, gen,
                pos + new Vector3(0f, -0.02f, 0.055f),
                new Vector3(0.065f, 0.07f, 0.01f), PlasticWhite);

            // 冲水按钮
            CreateFurniture("Toilet_Flush", parent, gen,
                pos + new Vector3(0f, 0.06f, 0.12f),
                new Vector3(0.018f, 0.018f, 0.006f), MetalLight);
        }

        // ─── 监控摄像头 ────────────────────────────
        private static void BuildSurveillanceCamera(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 支架
            CreateFurniture("Cam_Bracket", parent, gen,
                pos + new Vector3(0f, 0f, -0.025f),
                new Vector3(0.015f, 0.015f, 0.04f), MetalDark);

            // 机身（半球形 → Cube 近似）
            CreateFurniture("Cam_Body", parent, gen, pos,
                new Vector3(0.03f, 0.03f, 0.025f), CameraBlack);

            // 镜头
            CreateFurniture("Cam_Lens", parent, gen,
                pos + new Vector3(0f, 0f, 0.015f),
                new Vector3(0.012f, 0.012f, 0.006f), new Color(0.04f, 0.04f, 0.05f, 1f));

            // 红色指示灯
            CreateFurniture("Cam_LED", parent, gen,
                pos + new Vector3(0.01f, 0.012f, 0.013f),
                new Vector3(0.004f, 0.004f, 0.003f), RedAccent);
        }

        // ─── 办公桌 ──────────────────────────────
        private static void BuildOfficeDesk(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float dw = 0.35f;
            float dd = 0.2f;
            float dh = 0.13f;

            // 桌面
            CreateFurniture("OfficeDesk_Top", parent, gen,
                pos + new Vector3(0f, 0f, dh),
                new Vector3(dw, dd, 0.015f), DeskWood);

            // 抽屉组（右侧）
            CreateFurniture("OfficeDesk_Drawers", parent, gen,
                pos + new Vector3(dw * 0.35f, dd * 0.45f, dh * 0.55f),
                new Vector3(dw * 0.22f, dd * 0.08f, dh * 0.9f), DeskWood);

            // 抽屉把手
            for (int d = 0; d < 2; d++)
            {
                CreateFurniture($"OfficeDesk_Handle_{d}", parent, gen,
                    pos + new Vector3(dw * 0.35f, dd * 0.5f, dh * (0.3f + d * 0.35f)),
                    new Vector3(0.04f, 0.006f, 0.006f), MetalLight);
            }

            // 横梁/裙板（前侧）
            CreateFurniture("OfficeDesk_Apron", parent, gen,
                pos + new Vector3(0f, dd * 0.48f, dh * 0.5f),
                new Vector3(dw, 0.012f, dh * 0.85f), DeskWood);

            // 四条腿
            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            {
                CreateFurniture($"OfficeDesk_Leg_{ix}_{iy}", parent, gen,
                    pos + new Vector3(ix * dw * 0.44f, iy * dd * 0.44f, dh * 0.45f),
                    new Vector3(0.014f, 0.014f, dh * 0.88f), DeskWood);
            }
        }

        // ─── 档案柜 ──────────────────────────────
        private static void BuildFilingCabinet(Transform parent, string name, Vector3 pos, List<GameObject> gen)
        {
            float cabW = 0.1f;
            float cabD = 0.12f;
            float cabH = 0.18f;

            // 柜体
            CreateFurniture($"{name}_Body", parent, gen,
                pos + new Vector3(0f, 0f, cabH * 0.5f),
                new Vector3(cabW, cabD, cabH), MetalDark);

            // 抽屉（3层）
            for (int d = 0; d < 3; d++)
            {
                float dz = cabH * (0.15f + d * 0.28f);
                // 抽屉面板
                CreateFurniture($"{name}_Drawer_{d}", parent, gen,
                    pos + new Vector3(0f, cabD * 0.52f, dz),
                    new Vector3(cabW * 0.88f, 0.005f, cabH * 0.22f), MetalLight);

                // 把手
                CreateFurniture($"{name}_Handle_{d}", parent, gen,
                    pos + new Vector3(0f, cabD * 0.55f, dz),
                    new Vector3(cabW * 0.4f, 0.008f, 0.006f), MetalLight);
            }
        }

        // ─── 电脑 ────────────────────────────────
        private static void BuildComputer(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 显示器底座
            CreateFurniture("PC_Base", parent, gen,
                pos + new Vector3(0f, 0f, 0.01f),
                new Vector3(0.04f, 0.03f, 0.01f), RubberBlack);

            // 显示器支架
            CreateFurniture("PC_Stand", parent, gen,
                pos + new Vector3(0f, 0f, 0.04f),
                new Vector3(0.012f, 0.008f, 0.05f), MetalDark);

            // 屏幕面板
            CreateFurniture("PC_Screen", parent, gen,
                pos + new Vector3(0f, 0f, 0.08f),
                new Vector3(0.08f, 0.006f, 0.06f), ScreenGlow);

            // 屏幕边框
            CreateFurniture("PC_Bezel", parent, gen,
                pos + new Vector3(0f, 0.002f, 0.08f),
                new Vector3(0.09f, 0.004f, 0.07f), RubberBlack);

            // 键盘
            CreateFurniture("PC_Keyboard", parent, gen,
                pos + new Vector3(0f, -0.05f, 0.025f),
                new Vector3(0.06f, 0.03f, 0.006f), RubberBlack);
        }

        // ─── 咖啡机 ──────────────────────────────
        private static void BuildCoffeeMachine(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            // 机身
            CreateFurniture("Coffee_Body", parent, gen,
                pos + new Vector3(0f, 0f, 0.05f),
                new Vector3(0.06f, 0.05f, 0.08f), CoffeeBrown);

            // 水箱（后侧透明）
            CreateFurniture("Coffee_Tank", parent, gen,
                pos + new Vector3(0f, 0f, 0.09f),
                new Vector3(0.05f, 0.04f, 0.03f), GlassBlue);

            // 咖啡壶
            CreateFurniture("Coffee_Pot", parent, gen,
                pos + new Vector3(0.015f, 0.035f, 0.035f),
                new Vector3(0.025f, 0.02f, 0.03f), new Color(0.2f, 0.18f, 0.15f, 0.7f));

            // 按钮面板
            CreateFurniture("Coffee_Panel", parent, gen,
                pos + new Vector3(0.018f, 0.028f, 0.085f),
                new Vector3(0.02f, 0.003f, 0.015f), RubberBlack);

            // 电源指示灯
            CreateFurniture("Coffee_LED", parent, gen,
                pos + new Vector3(0.018f, 0.031f, 0.08f),
                new Vector3(0.005f, 0.002f, 0.005f), GreenAccent);
        }

        // ─── 白板 ────────────────────────────────
        private static void BuildWhiteboard(Transform parent, Vector3 pos, List<GameObject> gen)
        {
            float wbW = 0.18f;
            float wbH = 0.14f;

            // 板面
            CreateFurniture("Whiteboard_Surface", parent, gen, pos,
                new Vector3(wbW, 0.006f, wbH), BoardWhite);

            // 边框
            float fw = wbW * 0.5f;
            float fh = wbH * 0.5f;
            for (int dx = -1; dx <= 1; dx += 2)
            {
                CreateFurniture($"Whiteboard_Frame_V_{dx}", parent, gen,
                    pos + new Vector3(dx * fw, 0.003f, 0f),
                    new Vector3(0.012f, 0.003f, wbH), MetalLight);
            }
            for (int dz = -1; dz <= 1; dz += 2)
            {
                CreateFurniture($"Whiteboard_Frame_H_{dz}", parent, gen,
                    pos + new Vector3(0f, 0.003f, dz * fh),
                    new Vector3(wbW, 0.003f, 0.012f), MetalLight);
            }

            // 笔槽（底部）
            CreateFurniture("Whiteboard_Tray", parent, gen,
                pos + new Vector3(0f, 0.015f, -fh + 0.01f),
                new Vector3(wbW * 0.8f, 0.025f, 0.01f), MetalLight);
        }

        // ══════════════════════════════════════════
        //  内部辅助方法
        // ══════════════════════════════════════════

        private static GameObject CreateAreaRoot(string name, Vector3 worldPos, Transform parent, List<GameObject> gen)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            gen.Add(root);
            return root;
        }

        private static GameObject CreateLodGroup(GameObject areaRoot, string lodName, List<GameObject> gen)
        {
            GameObject lod = new GameObject(lodName);
            lod.transform.SetParent(areaRoot.transform, false);
            lod.transform.localPosition = Vector3.zero;
            gen.Add(lod);
            return lod;
        }

        /// <summary>创建家具 Cube 并设置材质。</summary>
        private static GameObject CreateFurniture(string name, Transform parent, List<GameObject> gen,
            Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            gen.Add(obj);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = scale;
            SetMaterial(obj, color);
            return obj;
        }

        private static void SetMaterial(GameObject obj, Color color)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.sharedMaterial = MaterialFactory.GetSimpleMaterial(color);
        }
    }
}
