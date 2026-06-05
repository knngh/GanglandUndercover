using UnityEngine;

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// D4 九龙城寨地图布局工厂。
    ///
    /// 九龙城寨主题：8 房间，密集的城市迷宫风格，与港区/警署节奏区分。
    ///   8 房间：茶餐厅 / 药材铺 / 麻将馆 / 天井 / 后巷 / 天台 / 地下钱庄 / 暗渠
    ///   走廊基于邻接表生成、12 任务点、5 暗线节点、5 监控摄像头、8 视线遮挡体、10 出生点。
    ///
    /// 设计理念：九龙城寨以狭窄、密集、多层著称——地图因此设计为小房间+长走廊，
    ///   视线遮挡体比另外两张图更多（8个），暗线也更密集（5节点→全连通），
    ///   适合高频接触/伏击玩法，与港区（开阔）和警署（对称规整）形成差异化。
    /// </summary>
    public static class KowloonWalledCityMapLayout
    {
        // ─── 地图边界 ──────────────────────────────
        public const float DesignHalfWidth = 7.0f;
        public const float DesignHalfHeight = 5.0f;

        /// <summary>会议点（天井中央）</summary>
        public static Vector2 MeetingCenter => new Vector2(0f, 0f);
        public const int MaxMeetingSeats = 10;

        // ─── 8 个房间 ──────────────────────────────────

        public static RoomDefinition[] Rooms()
        {
            return new[]
            {
                // 0: 茶餐厅 — 中央社交枢纽
                new RoomDefinition
                {
                    Name = "茶餐厅", Label = "Cafe",
                    Center = new Vector2(0f, 0.8f),
                    Size = new Vector3(2.2f, 1.8f, 0.16f),
                    FloorColor = new Color(0.32f, 0.18f, 0.10f, 1f),
                    Entrance = OnlineMapService.MapEntrance.South,
                },
                // 1: 药材铺 — 西北角
                new RoomDefinition
                {
                    Name = "药材铺", Label = "HerbShop",
                    Center = new Vector2(-4.5f, 2.5f),
                    Size = new Vector3(2.0f, 1.6f, 0.16f),
                    FloorColor = new Color(0.18f, 0.28f, 0.14f, 1f),
                    Entrance = OnlineMapService.MapEntrance.East,
                },
                // 2: 麻将馆 — 东北角
                new RoomDefinition
                {
                    Name = "麻将馆", Label = "Mahjong",
                    Center = new Vector2(4.2f, 2.5f),
                    Size = new Vector3(2.2f, 1.6f, 0.16f),
                    FloorColor = new Color(0.28f, 0.22f, 0.12f, 1f),
                    Entrance = OnlineMapService.MapEntrance.West,
                },
                // 3: 天井 — 中央
                new RoomDefinition
                {
                    Name = "天井", Label = "Courtyard",
                    Center = new Vector2(0f, -1.0f),
                    Size = new Vector3(2.8f, 2.0f, 0.16f),
                    FloorColor = new Color(0.15f, 0.17f, 0.22f, 1f),
                    Entrance = OnlineMapService.MapEntrance.North,
                },
                // 4: 后巷 — 西南
                new RoomDefinition
                {
                    Name = "后巷", Label = "BackAlley",
                    Center = new Vector2(-4.0f, -2.8f),
                    Size = new Vector3(2.4f, 1.6f, 0.16f),
                    FloorColor = new Color(0.25f, 0.14f, 0.09f, 1f),
                    Entrance = OnlineMapService.MapEntrance.East,
                },
                // 5: 天台 — 正北
                new RoomDefinition
                {
                    Name = "天台", Label = "Rooftop",
                    Center = new Vector2(-0.5f, 3.8f),
                    Size = new Vector3(2.6f, 1.4f, 0.16f),
                    FloorColor = new Color(0.18f, 0.19f, 0.30f, 1f),
                    Entrance = OnlineMapService.MapEntrance.South,
                },
                // 6: 地下钱庄 — 东南
                new RoomDefinition
                {
                    Name = "地下钱庄", Label = "Vault",
                    Center = new Vector2(4.0f, -2.8f),
                    Size = new Vector3(2.4f, 1.6f, 0.16f),
                    FloorColor = new Color(0.22f, 0.16f, 0.10f, 1f),
                    Entrance = OnlineMapService.MapEntrance.West,
                },
                // 7: 暗渠 — 极南
                new RoomDefinition
                {
                    Name = "暗渠", Label = "Drain",
                    Center = new Vector2(0f, -4.2f),
                    Size = new Vector3(2.6f, 1.4f, 0.16f),
                    FloorColor = new Color(0.10f, 0.15f, 0.18f, 1f),
                    Entrance = OnlineMapService.MapEntrance.North,
                },
            };
        }

        // ─── 走廊 — 密集网络（九龙城寨特色：狭窄长通道） ──

        public static CorridorDefinition[] Corridors()
        {
            return new[]
            {
                // 茶餐厅(0) ↔ 药材铺(1)
                new CorridorDefinition { Name = "西走廊", Center = new Vector2(-2.25f, 1.65f), Size = new Vector2(4.5f, 0.65f), Walkable = true },
                // 茶餐厅(0) ↔ 麻将馆(2)
                new CorridorDefinition { Name = "东走廊", Center = new Vector2(2.1f, 1.65f), Size = new Vector2(4.2f, 0.65f), Walkable = true },
                // 茶餐厅(0) ↔ 天井(3)
                new CorridorDefinition { Name = "中央通道", Center = new Vector2(0f, -0.1f), Size = new Vector2(1.2f, 1.8f), Walkable = true },
                // 茶餐厅(0) ↔ 天台(5)
                new CorridorDefinition { Name = "天井楼梯", Center = new Vector2(-0.25f, 2.3f), Size = new Vector2(0.8f, 3.0f), Walkable = true },
                // 药材铺(1) ↔ 后巷(4)
                new CorridorDefinition { Name = "西南暗廊", Center = new Vector2(-4.25f, -0.15f), Size = new Vector2(0.65f, 5.3f), Walkable = true },
                // 麻将馆(2) ↔ 地下钱庄(6)
                new CorridorDefinition { Name = "东南楼梯", Center = new Vector2(4.1f, -0.15f), Size = new Vector2(0.65f, 5.3f), Walkable = true },
                // 天井(3) ↔ 后巷(4)
                new CorridorDefinition { Name = "西下通道", Center = new Vector2(-2.0f, -1.9f), Size = new Vector2(4.0f, 0.6f), Walkable = true },
                // 天井(3) ↔ 地下钱庄(6)
                new CorridorDefinition { Name = "东下通道", Center = new Vector2(2.0f, -1.9f), Size = new Vector2(4.0f, 0.6f), Walkable = true },
                // 天井(3) ↔ 暗渠(7)
                new CorridorDefinition { Name = "暗渠通道", Center = new Vector2(0f, -2.6f), Size = new Vector2(1.0f, 3.2f), Walkable = true },
                // 天井(3) ↔ 茶餐厅(0)（第二个通道）
                new CorridorDefinition { Name = "东侧廊", Center = new Vector2(1.5f, -0.5f), Size = new Vector2(3.0f, 0.6f), Walkable = true },
                // 会议圆桌节点
                new CorridorDefinition { Name = "会议节点", Center = new Vector2(0f, -0.5f), Size = Vector2.zero, Walkable = true, IsRoundNode = true, NodeRadius = 0.9f },
            };
        }

        // ─── 12 个任务点 ──

        public static TaskAssignment[] Tasks()
        {
            return new[]
            {
                new TaskAssignment { TaskId = 0, RoomIndex = 0, Position = new Vector2(0f, 1.2f) },
                new TaskAssignment { TaskId = 1, RoomIndex = 0, Position = new Vector2(0.5f, 0.5f) },
                new TaskAssignment { TaskId = 2, RoomIndex = 1, Position = new Vector2(-4.8f, 2.8f) },
                new TaskAssignment { TaskId = 3, RoomIndex = 2, Position = new Vector2(4.5f, 2.8f) },
                new TaskAssignment { TaskId = 4, RoomIndex = 3, Position = new Vector2(0.5f, -0.5f) },
                new TaskAssignment { TaskId = 5, RoomIndex = 4, Position = new Vector2(-4.4f, -3.1f) },
                new TaskAssignment { TaskId = 6, RoomIndex = 5, Position = new Vector2(-0.8f, 4.1f) },
                new TaskAssignment { TaskId = 7, RoomIndex = 6, Position = new Vector2(4.4f, -3.1f) },
                new TaskAssignment { TaskId = 8, RoomIndex = 7, Position = new Vector2(0f, -4.5f) },
                new TaskAssignment { TaskId = 9, RoomIndex = 3, Position = new Vector2(-0.8f, -1.3f) },
                new TaskAssignment { TaskId = 10, RoomIndex = 5, Position = new Vector2(0.2f, 3.6f) },
                new TaskAssignment { TaskId = 11, RoomIndex = 4, Position = new Vector2(-3.6f, -2.5f) },
            };
        }

        // ─── 5 暗线/通风管节点（密集迷宫级） ──

        public static VentNodeDefinition[] Vents()
        {
            return new[]
            {
                new VentNodeDefinition { Name = "茶餐厅暗线", Position = new Vector2(0f, 0.8f), ConnectedIndices = new[] { 1, 2, 3, 4 } },
                new VentNodeDefinition { Name = "药材铺暗线", Position = new Vector2(-4.5f, 2.5f), ConnectedIndices = new[] { 0, 3, 5 } },
                new VentNodeDefinition { Name = "麻将馆暗线", Position = new Vector2(4.2f, 2.5f), ConnectedIndices = new[] { 0, 4, 6 } },
                new VentNodeDefinition { Name = "后巷暗线", Position = new Vector2(-4.0f, -2.8f), ConnectedIndices = new[] { 0, 1, 7 } },
                new VentNodeDefinition { Name = "钱庄暗线", Position = new Vector2(4.0f, -2.8f), ConnectedIndices = new[] { 0, 2, 7 } },
            };
        }

        // ─── 5 监控摄像头 ──

        public static SurveillanceZoneDefinition[] Surveillance()
        {
            return new[]
            {
                new SurveillanceZoneDefinition { Label = "茶餐厅监控", Center = new Vector2(0f, 0.8f), Size = new Vector2(2.6f, 2.2f), RoomIndex = 0 },
                new SurveillanceZoneDefinition { Label = "天井监控", Center = new Vector2(0f, -1.0f), Size = new Vector2(3.2f, 2.4f), RoomIndex = 3 },
                new SurveillanceZoneDefinition { Label = "西走廊监控", Center = new Vector2(-2.25f, 1.65f), Size = new Vector2(4.8f, 1.2f), RoomIndex = -1 },
                new SurveillanceZoneDefinition { Label = "东走廊监控", Center = new Vector2(2.1f, 1.65f), Size = new Vector2(4.6f, 1.2f), RoomIndex = -1 },
                new SurveillanceZoneDefinition { Label = "暗渠监控", Center = new Vector2(0f, -4.2f), Size = new Vector2(3.0f, 1.8f), RoomIndex = 7 },
            };
        }

        // ─── 8 视线遮挡体（九龙城寨特征：多遮挡 = 伏击玩法） ──

        public static BlockerVolume[] Blockers()
        {
            return new[]
            {
                new BlockerVolume { Name = "茶餐厅吧台", Center = new Vector2(-0.3f, 1.2f), Size = new Vector2(1.4f, 0.2f) },
                new BlockerVolume { Name = "药材铺柜台", Center = new Vector2(-4.5f, 2.8f), Size = new Vector2(1.2f, 0.2f) },
                new BlockerVolume { Name = "麻将桌", Center = new Vector2(4.2f, 2.8f), Size = new Vector2(1.4f, 0.8f) },
                new BlockerVolume { Name = "天井花坛", Center = new Vector2(0f, -0.8f), Size = new Vector2(0.8f, 0.8f) },
                new BlockerVolume { Name = "后巷货堆", Center = new Vector2(-4.0f, -3.1f), Size = new Vector2(1.6f, 0.3f) },
                new BlockerVolume { Name = "天台水箱", Center = new Vector2(-0.3f, 4.1f), Size = new Vector2(1.2f, 0.8f) },
                new BlockerVolume { Name = "钱庄保险柜", Center = new Vector2(4.4f, -3.1f), Size = new Vector2(1.0f, 0.7f) },
                new BlockerVolume { Name = "暗渠管柱", Center = new Vector2(0f, -4.5f), Size = new Vector2(0.5f, 0.5f) },
            };
        }

        // ─── 10 出生点 ──

        public static Vector2[] Spawns()
        {
            return new[]
            {
                new Vector2(0f, 0.5f),        // 茶餐厅
                new Vector2(-3.5f, 2.0f),     // 药材铺附近
                new Vector2(3.5f, 2.0f),      // 麻将馆附近
                new Vector2(0.8f, -0.5f),     // 天井
                new Vector2(-0.8f, -1.5f),    // 天井南
                new Vector2(-3.0f, -2.0f),    // 后巷附近
                new Vector2(0f, 3.0f),        // 天台附近
                new Vector2(3.0f, -2.0f),     // 钱庄附近
                new Vector2(0f, -3.5f),       // 暗渠附近
                new Vector2(-1.5f, 1.5f),     // 西走廊
            };
        }

        // ══════════════════════════════════════════════════════
        // 工厂方法
        // ══════════════════════════════════════════════════════

        public static MapLayoutData CreateMapLayoutAsset()
        {
            var layout = ScriptableObject.CreateInstance<MapLayoutData>();
            layout.name = "MapLayout_KowloonWalledCity";
            PopulateLayout(layout);
            return layout;
        }

        public static void PopulateLayout(MapLayoutData layout)
        {
            layout.DesignHalfWidth = DesignHalfWidth;
            layout.DesignHalfHeight = DesignHalfHeight;
            layout.MeetingCenter = MeetingCenter;
            layout.MaxMeetingSeats = MaxMeetingSeats;
            layout.Rooms = Rooms();
            layout.Corridors = Corridors();
            layout.TaskAssignments = Tasks();
            layout.VentNodes = Vents();
            layout.SurveillanceZones = Surveillance();
            layout.SightBlockers = Blockers();
            layout.SpawnPoints = Spawns();
        }
    }
}
