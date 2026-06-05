using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// E3 房间装饰器。
    /// 为灰盒地图的每个房间生成可辨识的美术装饰——道具、入口标记、地板细节、墙壁纹理。
    /// 与 GreyboxMapBuilder 协作：BuildRooms 后调用 DecorateRoom。
    ///
    /// 设计原则：装饰只增不减，不改变碰撞/坐标/联机同步。
    /// </summary>
    public class RoomDecorator
    {
        private readonly Online.OnlineWorldBuilder _worldBuilder;
        private readonly Online.OnlineMapService _mapService;
        private readonly Online.OnlineMapService.OnlineMapType _mapType;
        private readonly GameObject _worldRoot;

        public readonly List<GameObject> DecoratedProps = new();
        public readonly List<GameObject> EntranceMarkers = new();

        // ── 调色板引用 ──
        private static readonly Color DoorOpen   = new Color(0.2f, 0.8f, 0.2f, 0.7f);
        private static readonly Color DoorClosed = new Color(0.8f, 0.2f, 0.2f, 0.7f);
        private static readonly Color PropWood   = new Color(0.35f, 0.20f, 0.10f, 1f);
        private static readonly Color PropMetal  = new Color(0.30f, 0.32f, 0.35f, 1f);
        private static readonly Color PropGreen  = new Color(0.15f, 0.45f, 0.20f, 1f);
        private static readonly Color PropWhite  = new Color(0.85f, 0.85f, 0.88f, 1f);
        private static readonly Color PropBlue   = new Color(0.15f, 0.25f, 0.55f, 1f);
        private static readonly Color PropRed    = new Color(0.70f, 0.15f, 0.10f, 1f);
        private static readonly Color NeonPink   = new Color(0.95f, 0.20f, 0.55f, 0.8f);
        private static readonly Color NeonCyan   = new Color(0.15f, 0.85f, 0.85f, 0.7f);
        private static readonly Color NeonYellow = new Color(0.95f, 0.85f, 0.15f, 0.7f);

        public RoomDecorator(Online.OnlineWorldBuilder worldBuilder,
            Online.OnlineMapService mapService, GameObject worldRoot)
        {
            _worldBuilder = worldBuilder;
            _mapService = mapService;
            _mapType = mapService.ActiveMapType;
            _worldRoot = worldRoot;
        }

        // ══════════════════════════════════════════════════════
        // 公共入口：装饰一个房间
        // ══════════════════════════════════════════════════════
        public void DecorateRoom(Online.Map.RoomDefinition room, int roomIndex)
        {
            // 入口标记（门/通道指示）
            DecorateEntrance(room);

            // 地板细节（纹理线、地毯、斑块）
            DecorateFloor(room, roomIndex);

            // 墙壁细节（砖纹线、海报、管道）
            DecorateWalls(room, roomIndex);

            // 房间专属道具
            DecorateProps(room, roomIndex);
        }

        // ══════════════════════════════════════════════════════
        // 入口标记
        // ══════════════════════════════════════════════════════
        private void DecorateEntrance(Online.Map.RoomDefinition room)
        {
            float hw = room.Size.x * 0.5f;
            float hh = room.Size.y * 0.5f;

            Vector3 center = new Vector3(room.Center.x, room.Center.y, 0.15f);
            Vector3 dir;
            Vector3 perp;
            float length;

            switch (room.Entrance)
            {
                case Online.OnlineMapService.MapEntrance.North:
                    dir = Vector3.up;
                    perp = Vector3.right;
                    length = room.Size.x * 0.3f;
                    break;
                case Online.OnlineMapService.MapEntrance.South:
                    dir = -Vector3.up;
                    perp = Vector3.right;
                    length = room.Size.x * 0.3f;
                    break;
                case Online.OnlineMapService.MapEntrance.East:
                    dir = Vector3.right;
                    perp = Vector3.up;
                    length = room.Size.y * 0.3f;
                    break;
                case Online.OnlineMapService.MapEntrance.West:
                    dir = -Vector3.right;
                    perp = Vector3.up;
                    length = room.Size.y * 0.3f;
                    break;
                default: return;
            }

            Vector3 doorPos = center + dir * (dir.x != 0 ? hw + 0.05f : hh + 0.05f);

            // 门框（两侧立柱）
            var leftPillar = CreateProp($"EntrancePillar_{room.Name}_L",
                doorPos + perp * length * 0.5f,
                new Vector3(0.08f, 0.25f, 0.05f), PropMetal);
            EntranceMarkers.Add(leftPillar);

            var rightPillar = CreateProp($"EntrancePillar_{room.Name}_R",
                doorPos - perp * length * 0.5f,
                new Vector3(0.08f, 0.25f, 0.05f), PropMetal);
            EntranceMarkers.Add(rightPillar);

            // 门上横梁
            CreateProp($"EntranceBeam_{room.Name}",
                doorPos + new Vector3(0, 0.13f, 0),
                new Vector3(length, 0.05f, 0.05f), PropMetal);

            // 地垫/入口标记
            var mat = CreateProp($"EntranceMat_{room.Name}",
                doorPos + new Vector3(0, -0.12f, 0f),
                new Vector3(length * 0.9f, 0.1f, 0.02f), DoorOpen);
            EntranceMarkers.Add(mat);
        }

        // ══════════════════════════════════════════════════════
        // 地板细节
        // ══════════════════════════════════════════════════════
        private void DecorateFloor(Online.Map.RoomDefinition room, int roomIndex)
        {
            Vector3 center = new Vector3(room.Center.x, room.Center.y, 0.01f);
            float hw = room.Size.x * 0.4f;
            float hh = room.Size.y * 0.45f;

            // 地板纹理线（十字交叉）
            Color lineColor = new Color(0, 0, 0, 0.08f);
            CreateProp($"FloorLine_H_{room.Name}",
                center, new Vector3(hw * 1.6f, 0.03f, 0.01f), lineColor);
            CreateProp($"FloorLine_V_{room.Name}",
                center, new Vector3(0.03f, hh * 1.6f, 0.01f), lineColor);

            // X 型对角线点缀（仅大型房间）
            if (room.Size.x > 2f && room.Size.y > 1.5f)
            {
                CreateProp($"FloorDiag_LR_{room.Name}",
                    center, new Vector3(hw * 1.2f, 0.02f, 0.01f), lineColor);

                var diag2 = CreateProp($"FloorDiag_RL_{room.Name}",
                    center, new Vector3(0.02f, hh * 1.2f, 0.01f), lineColor);
                // 45度旋转
                diag2.transform.rotation = Quaternion.Euler(0, 0, 45);
            }
        }

        // ══════════════════════════════════════════════════════
        // 墙壁装饰
        // ══════════════════════════════════════════════════════
        private void DecorateWalls(Online.Map.RoomDefinition room, int roomIndex)
        {
            Vector3 center = new Vector3(room.Center.x, room.Center.y, 0.15f);
            float hw = room.Size.x * 0.48f;
            float hh = room.Size.y * 0.48f;
            bool isEntranceN = room.Entrance != Online.OnlineMapService.MapEntrance.North;
            bool isEntranceS = room.Entrance != Online.OnlineMapService.MapEntrance.South;
            bool isEntranceW = room.Entrance != Online.OnlineMapService.MapEntrance.West;
            bool isEntranceE = room.Entrance != Online.OnlineMapService.MapEntrance.East;

            // 每面墙内生装饰线（模拟踢脚线/墙裙）
            float wallThick = 0.04f;
            Color wallLine = new Color(0, 0, 0, 0.15f);

            if (isEntranceN)
                CreateProp($"WallTrim_N_{room.Name}",
                    center + new Vector3(0, hh - 0.05f, 0),
                    new Vector3(room.Size.x * 0.9f, wallThick, 0.02f), wallLine);

            if (isEntranceS)
                CreateProp($"WallTrim_S_{room.Name}",
                    center + new Vector3(0, -hh + 0.05f, 0),
                    new Vector3(room.Size.x * 0.9f, wallThick, 0.02f), wallLine);

            // 地图专属墙壁贴饰
            DrawWallPosters(room, roomIndex);
        }

        private void DrawWallPosters(Online.Map.RoomDefinition room, int roomIndex)
        {
            Vector3 c = new Vector3(room.Center.x, room.Center.y, 0.16f);
            float hw = room.Size.x * 0.48f;
            float hh = room.Size.y * 0.48f;

            // 根据地图类型选择装饰风格
            switch (_mapType)
            {
                case Online.OnlineMapService.OnlineMapType.HarbourDistrict:
                    // 港区：货柜铭牌、警示标志
                    if (roomIndex == 0) // 货柜场
                    {
                        CreateProp("Poster_Container_A", c + new Vector3(-0.5f, hh - 0.1f, 0),
                            new Vector3(0.3f, 0.2f, 0.02f), PropMetal);
                        CreateProp("Poster_Container_B", c + new Vector3(0.5f, -hh + 0.1f, 0),
                            new Vector3(0.3f, 0.2f, 0.02f), PropRed);
                    }
                    if (roomIndex == 4) // 夜市
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            CreateProp($"Neon_{i}", c + new Vector3(-0.8f + i * 0.8f, hh - 0.1f, 0),
                                new Vector3(0.15f, 0.3f, 0.02f), NeonPink);
                        }
                    }
                    break;

                case Online.OnlineMapService.OnlineMapType.PoliceStation:
                    // 警署：通告栏、编号牌
                    CreateProp($"Badge_{room.Name}", c + new Vector3(-hw + 0.15f, hh - 0.1f, 0),
                        new Vector3(0.15f, 0.2f, 0.02f), PropBlue);
                    CreateProp($"Board_{room.Name}", c + new Vector3(hw - 0.15f, 0, 0),
                        new Vector3(0.25f, 0.35f, 0.02f), PropWhite);
                    break;

                case Online.OnlineMapService.OnlineMapType.KowloonWalledCity:
                    // 九龙城寨：霓虹招牌、管线
                    if (roomIndex == 0 || roomIndex == 2 || roomIndex == 4) // 茶餐厅/麻将馆/后巷
                    {
                        CreateProp($"NeonSign_{room.Name}",
                            c + new Vector3(0, hh - 0.08f, 0),
                            new Vector3(0.6f, 0.12f, 0.02f), NeonCyan);
                    }
                    // 管道
                    if (roomIndex % 2 == 0)
                    {
                        CreateProp($"Pipe_{room.Name}", c + new Vector3(-hw + 0.12f, 0, 0),
                            new Vector3(0.04f, room.Size.y * 0.7f, 0.02f), PropMetal);
                    }
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 房间专属道具
        // ══════════════════════════════════════════════════════
        private void DecorateProps(Online.Map.RoomDefinition room, int roomIndex)
        {
            Vector3 c = new Vector3(room.Center.x, room.Center.y, 0.08f);

            switch (_mapType)
            {
                case Online.OnlineMapService.OnlineMapType.HarbourDistrict:
                    DecorateHarbourProps(room, roomIndex, c);
                    break;
                case Online.OnlineMapService.OnlineMapType.PoliceStation:
                    DecoratePoliceProps(room, roomIndex, c);
                    break;
                case Online.OnlineMapService.OnlineMapType.KowloonWalledCity:
                    DecorateKowloonProps(room, roomIndex, c);
                    break;
            }
        }

        // ── 港区专属道具 ──
        private void DecorateHarbourProps(Online.Map.RoomDefinition room, int roomIndex, Vector3 c)
        {
            switch (roomIndex)
            {
                case 0: // 货柜场
                    for (int i = 0; i < 3; i++)
                        CreateProp($"Crate_{i}", c + new Vector3(-0.6f + i * 0.6f, -0.3f, 0),
                            new Vector3(0.3f, 0.3f, 0.08f), PropMetal);
                    CreateProp("Crane", c + new Vector3(0, 0.5f, 0),
                        new Vector3(0.8f, 0.1f, 0.08f), PropMetal);
                    break;
                case 1: // 海关
                    CreateProp("Desk_A", c + new Vector3(-0.3f, 0.1f, 0),
                        new Vector3(0.5f, 0.3f, 0.08f), PropWood);
                    CreateProp("Desk_B", c + new Vector3(0.4f, -0.2f, 0),
                        new Vector3(0.4f, 0.25f, 0.08f), PropWood);
                    break;
                case 2: // 监控室
                    CreateProp("MonitorBank", c + new Vector3(0, -0.2f, 0),
                        new Vector3(0.8f, 0.4f, 0.08f), PropBlue);
                    break;
                case 3: // 茶餐厅
                    CreateProp("Booth_A", c + new Vector3(-0.4f, 0, 0),
                        new Vector3(0.5f, 0.35f, 0.08f), PropWood);
                    CreateProp("Booth_B", c + new Vector3(0.4f, 0, 0),
                        new Vector3(0.5f, 0.35f, 0.08f), PropWood);
                    break;
                case 4: // 夜市
                    for (int i = 0; i < 2; i++)
                        CreateProp($"Stall_{i}", c + new Vector3(-0.4f + i * 0.8f, -0.2f, 0),
                            new Vector3(0.35f, 0.35f, 0.08f), PropWood);
                    break;
                case 6: // 电房
                    CreateProp("Panel", c,
                        new Vector3(0.5f, 0.6f, 0.08f), PropMetal);
                    CreateProp("WarningSign", c + new Vector3(0, 0.35f, 0),
                        new Vector3(0.2f, 0.2f, 0.02f), PropRed);
                    break;
                case 7: // 天台
                    CreateProp("AC_Unit", c + new Vector3(0.3f, 0.2f, 0),
                        new Vector3(0.4f, 0.3f, 0.08f), PropMetal);
                    break;
                case 10: // 后巷排档
                    CreateProp("Trolley", c,
                        new Vector3(0.4f, 0.3f, 0.08f), PropMetal);
                    break;
                case 11: // 地下诊所
                    CreateProp("Bed", c + new Vector3(-0.3f, 0, 0),
                        new Vector3(0.6f, 0.35f, 0.08f), PropWhite);
                    CreateProp("Cabinet", c + new Vector3(0.4f, 0, 0),
                        new Vector3(0.25f, 0.45f, 0.08f), PropGreen);
                    break;
            }
        }

        // ── 警署专属道具 ──
        private void DecoratePoliceProps(Online.Map.RoomDefinition room, int roomIndex, Vector3 c)
        {
            switch (roomIndex)
            {
                case 0: // 大厅
                    CreateProp("Reception", c + new Vector3(-0.3f, 0, 0),
                        new Vector3(0.5f, 0.35f, 0.08f), PropWhite);
                    CreateProp("Bench", c + new Vector3(0.5f, -0.2f, 0),
                        new Vector3(0.6f, 0.15f, 0.08f), PropWood);
                    break;
                case 1: // 审讯室
                    CreateProp("Table", c,
                        new Vector3(0.6f, 0.4f, 0.08f), PropMetal);
                    CreateProp("Chair1", c + new Vector3(-0.3f, -0.3f, 0),
                        new Vector3(0.15f, 0.15f, 0.08f), PropMetal);
                    CreateProp("Chair2", c + new Vector3(0.3f, 0.3f, 0),
                        new Vector3(0.15f, 0.15f, 0.08f), PropMetal);
                    break;
                case 2: // 证物室
                    for (int i = 0; i < 3; i++)
                        CreateProp($"Shelf_{i}", c + new Vector3(-0.4f + i * 0.4f, 0, 0),
                            new Vector3(0.15f, 0.5f, 0.08f), PropWood);
                    break;
                case 3: // 监控室
                    CreateProp("MonitorWall", c,
                        new Vector3(0.7f, 0.45f, 0.08f), PropBlue);
                    break;
                case 4: // 拘留室
                    CreateProp("CellBars", c + new Vector3(0, 0.2f, 0),
                        new Vector3(0.7f, 0.05f, 0.12f), PropMetal);
                    break;
                case 5: // 简报室
                    CreateProp("Board", c + new Vector3(0, 0.25f, 0),
                        new Vector3(0.8f, 0.5f, 0.05f), PropWhite);
                    break;
            }
        }

        // ── 九龙城寨专属道具 ──
        private void DecorateKowloonProps(Online.Map.RoomDefinition room, int roomIndex, Vector3 c)
        {
            switch (roomIndex)
            {
                case 0: // 茶餐厅
                    CreateProp("RoundTable", c,
                        new Vector3(0.5f, 0.5f, 0.08f), PropWood);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * Mathf.PI * 0.5f;
                        CreateProp($"Stool_{i}", c + new Vector3(Mathf.Cos(ang) * 0.35f, Mathf.Sin(ang) * 0.35f, 0),
                            new Vector3(0.12f, 0.12f, 0.08f), PropWood);
                    }
                    break;
                case 1: // 药材铺
                    CreateProp("HerbCabinet", c + new Vector3(-0.3f, 0, 0),
                        new Vector3(0.3f, 0.55f, 0.08f), PropWood);
                    CreateProp("HerbShelf", c + new Vector3(0.4f, 0, 0),
                        new Vector3(0.2f, 0.5f, 0.08f), PropWood);
                    break;
                case 2: // 麻将馆
                    CreateProp("MJTable", c,
                        new Vector3(0.55f, 0.55f, 0.08f), PropWood);
                    CreateProp("MJTiles", c + new Vector3(0, 0, 0.05f),
                        new Vector3(0.35f, 0.35f, 0.02f), PropGreen);
                    break;
                case 3: // 天井
                    CreateProp("Fountain", c,
                        new Vector3(0.3f, 0.3f, 0.08f), NeonCyan);
                    CreateProp("Laundry_A", c + new Vector3(-0.5f, 0.3f, 0),
                        new Vector3(0.2f, 0.35f, 0.02f), PropWhite);
                    break;
                case 4: // 后巷
                    CreateProp("TrashBin", c + new Vector3(-0.3f, 0.1f, 0),
                        new Vector3(0.2f, 0.3f, 0.08f), PropMetal);
                    CreateProp("Cardboard", c + new Vector3(0.4f, -0.2f, 0),
                        new Vector3(0.3f, 0.2f, 0.05f), PropWood);
                    break;
                case 5: // 天台
                    CreateProp("Satellite", c + new Vector3(0.3f, 0, 0),
                        new Vector3(0.35f, 0.35f, 0.08f), PropMetal);
                    break;
                case 6: // 地下钱庄
                    CreateProp("VaultDoor", c + new Vector3(0, 0.3f, 0),
                        new Vector3(0.35f, 0.45f, 0.08f), PropMetal);
                    CreateProp("MoneyStack", c + new Vector3(-0.3f, -0.2f, 0),
                        new Vector3(0.2f, 0.2f, 0.08f), NeonYellow);
                    break;
                case 7: // 暗渠
                    CreateProp("Grate", c,
                        new Vector3(0.5f, 0.5f, 0.05f), PropMetal);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 辅助方法
        // ══════════════════════════════════════════════════════
        private GameObject CreateProp(string name, Vector3 pos, Vector3 size, Color color)
        {
            var obj = _worldBuilder.CreateProp(name, pos, size, color);
            DecoratedProps.Add(obj);
            return obj;
        }

        public void Clear()
        {
            foreach (var p in DecoratedProps)
                if (p != null) Object.Destroy(p);
            DecoratedProps.Clear();

            foreach (var e in EntranceMarkers)
                if (e != null) Object.Destroy(e);
            EntranceMarkers.Clear();
        }
    }
}
