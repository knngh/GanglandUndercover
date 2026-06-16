using UnityEngine;

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// M8.1 警察局地图布局工厂。
    ///
    /// 基于 World/PoliceStationMap.cs 的 6 区域数据，
    /// 生成 MapLayoutData 兼容的定义数组，供 GreyboxMapBuilder 使用。
    ///
    /// 6 区域：大厅 / 审讯室 / 证物室 / 武器库(监控室) / 拘留室 / 简报室(办公室)
    /// 走廊网络基于 PoliceStationMap 邻接表生成。
    /// </summary>
    public static class PoliceStationMapLayout
    {
        // ─── 地图边界 ──────────────────────────────
        public const float DesignHalfWidth = 6.0f;
        public const float DesignHalfHeight = 4.5f;

        /// <summary>会议点（大厅中央靠下）</summary>
        public static Vector2 MeetingCenter => new Vector2(0f, -0.5f);
        public const int MaxMeetingSeats = 10;

        // ─── 房间 ──────────────────────────────────

        /// <summary>6 个警察局房间定义（设计坐标）</summary>
        public static RoomDefinition[] Rooms()
        {
            return new[]
            {
                new RoomDefinition
                {
                    Name = "大厅",
                    Label = "Lobby",
                    Center = new Vector2(0f, 0f),
                    Size = new Vector3(3.2f, 2.4f, 0.16f),
                    FloorColor = new Color(0.18f, 0.22f, 0.32f, 1f),
                    Entrance = OnlineMapService.MapEntrance.South,
                },
                new RoomDefinition
                {
                    Name = "审讯室",
                    Label = "IntRoom",
                    Center = new Vector2(-3.2f, 1.6f),
                    Size = new Vector3(2.4f, 1.8f, 0.16f),
                    FloorColor = new Color(0.28f, 0.18f, 0.18f, 1f),
                    Entrance = OnlineMapService.MapEntrance.East,
                },
                new RoomDefinition
                {
                    Name = "证物室",
                    Label = "Evidence",
                    Center = new Vector2(-3.0f, -1.8f),
                    Size = new Vector3(2.6f, 1.8f, 0.16f),
                    FloorColor = new Color(0.14f, 0.22f, 0.16f, 1f),
                    Entrance = OnlineMapService.MapEntrance.East,
                },
                new RoomDefinition
                {
                    Name = "监控室",
                    Label = "Surveillance",
                    Center = new Vector2(3.1f, -1.6f),
                    Size = new Vector3(2.2f, 1.6f, 0.16f),
                    FloorColor = new Color(0.22f, 0.20f, 0.14f, 1f),
                    Entrance = OnlineMapService.MapEntrance.West,
                },
                new RoomDefinition
                {
                    Name = "拘留室",
                    Label = "Cells",
                    Center = new Vector2(-0.8f, 2.4f),
                    Size = new Vector3(2.0f, 1.8f, 0.16f),
                    FloorColor = new Color(0.16f, 0.16f, 0.22f, 1f),
                    Entrance = OnlineMapService.MapEntrance.South,
                },
                new RoomDefinition
                {
                    Name = "简报室",
                    Label = "Briefing",
                    Center = new Vector2(2.8f, 1.5f),
                    Size = new Vector3(2.4f, 1.8f, 0.16f),
                    FloorColor = new Color(0.20f, 0.24f, 0.30f, 1f),
                    Entrance = OnlineMapService.MapEntrance.West,
                },
            };
        }

        // ─── 走廊 ──────────────────────────────────
        // 邻接: Lobby↔IntRoom, Lobby↔Evidence, Lobby↔Briefing, IntRoom↔Cells, Evidence↔Armory, Armory↔Briefing

        public static CorridorDefinition[] Corridors()
        {
            return new[]
            {
                // Lobby(0,0) ↔ Interrogation(-3.2,1.6)
                new CorridorDefinition
                {
                    Name = "大厅↔审讯室",
                    Center = new Vector2(-1.6f, 0.8f),
                    Size = new Vector2(3.5f, 1.0f),
                    Walkable = true,
                    IsRoundNode = false,
                    NodeRadius = 0f,
                },
                // Lobby(0,0) ↔ Evidence(-3.0,-1.8)
                new CorridorDefinition
                {
                    Name = "大厅↔证物室",
                    Center = new Vector2(-1.5f, -0.9f),
                    Size = new Vector2(3.2f, 1.0f),
                    Walkable = true,
                    IsRoundNode = false,
                    NodeRadius = 0f,
                },
                // Lobby(0,0) ↔ Briefing(2.8,1.5)
                new CorridorDefinition
                {
                    Name = "大厅↔简报室",
                    Center = new Vector2(1.4f, 0.75f),
                    Size = new Vector2(3.0f, 1.0f),
                    Walkable = true,
                    IsRoundNode = false,
                    NodeRadius = 0f,
                },
                // Interrogation(-3.2,1.6) ↔ Cells(-0.8,2.4)
                new CorridorDefinition
                {
                    Name = "审讯室↔拘留室",
                    Center = new Vector2(-2.0f, 2.0f),
                    Size = new Vector2(2.6f, 0.9f),
                    Walkable = true,
                    IsRoundNode = false,
                    NodeRadius = 0f,
                },
                // Evidence(-3.0,-1.8) ↔ Armory(3.1,-1.6)
                new CorridorDefinition
                {
                    Name = "证物室↔监控室",
                    Center = new Vector2(0.05f, -1.7f),
                    Size = new Vector2(6.3f, 0.9f),
                    Walkable = true,
                    IsRoundNode = false,
                    NodeRadius = 0f,
                },
                // Armory(3.1,-1.6) ↔ Briefing(2.8,1.5)
                new CorridorDefinition
                {
                    Name = "监控室↔简报室",
                    Center = new Vector2(2.95f, -0.05f),
                    Size = new Vector2(0.9f, 3.3f),
                    Walkable = true,
                    IsRoundNode = false,
                    NodeRadius = 0f,
                },
                // 走廊交叉节点（Lobby 附近）
                new CorridorDefinition
                {
                    Name = "中央枢纽",
                    Center = new Vector2(0f, -0.5f),
                    Size = new Vector2(0f, 0f),
                    Walkable = true,
                    IsRoundNode = true,
                    NodeRadius = 0.9f,
                },
            };
        }

        // ─── 任务点 ────────────────────────────────
        // 每房间 1-2 个任务，共 8 个任务点

        public static TaskAssignment[] Tasks()
        {
            return new[]
            {
                // 0: 大厅 — 整理档案
                new TaskAssignment { TaskId = 0, RoomIndex = 0, Position = new Vector2(0f, 0.3f) },
                // 1: 审讯室 — 审讯记录
                new TaskAssignment { TaskId = 1, RoomIndex = 1, Position = new Vector2(-3.5f, 2.0f) },
                // 2: 证物室 — 证据归档
                new TaskAssignment { TaskId = 2, RoomIndex = 2, Position = new Vector2(-3.3f, -2.2f) },
                // 3: 监控室 — 监控巡查
                new TaskAssignment { TaskId = 3, RoomIndex = 3, Position = new Vector2(3.5f, -1.9f) },
                // 4: 简报室 — 调取监控
                new TaskAssignment { TaskId = 4, RoomIndex = 5, Position = new Vector2(3.1f, 1.9f) },
                // 5: 大厅 — 辅助任务（公告栏）
                new TaskAssignment { TaskId = 5, RoomIndex = 0, Position = new Vector2(0.6f, -0.4f) },
                // 6: 拘留室 — 拘留记录
                new TaskAssignment { TaskId = 6, RoomIndex = 4, Position = new Vector2(-1.1f, 2.8f) },
                // 7: 证物室 — 证物清点
                new TaskAssignment { TaskId = 7, RoomIndex = 2, Position = new Vector2(-2.6f, -1.4f) },
            };
        }

        // ─── 暗线/通风管 ────────────────────────────
        // 4 个节点（索引 0-3），基于 PoliceStationMap.VentConfigs
        // 拓扑：大厅(0) 为中心枢纽，连接其余三个节点；
        //       审讯室(1) ↔ 证物室(2)；证物室(2) ↔ 监控室(3)

        public static VentNodeDefinition[] Vents()
        {
            return new[]
            {
                new VentNodeDefinition
                {
                    Name = "大厅通风管",
                    Position = new Vector2(0f, 0f),
                    ConnectedIndices = new[] { 1, 2, 3 },
                },
                new VentNodeDefinition
                {
                    Name = "审讯室通风管",
                    Position = new Vector2(-3.0f, 1.3f),
                    ConnectedIndices = new[] { 0, 2 },
                },
                new VentNodeDefinition
                {
                    Name = "证物室通风管",
                    Position = new Vector2(-2.7f, -1.3f),
                    ConnectedIndices = new[] { 0, 1, 3 },
                },
                new VentNodeDefinition
                {
                    Name = "监控室通风管",
                    Position = new Vector2(2.8f, -1.2f),
                    ConnectedIndices = new[] { 0, 2 },
                },
            };
        }

        // ─── 监控摄像头 ────────────────────────────
        // 4 个摄像头覆盖关键区域

        public static SurveillanceZoneDefinition[] Surveillance()
        {
            return new[]
            {
                new SurveillanceZoneDefinition
                {
                    Label = "大厅监控",
                    Center = new Vector2(0f, 0f),
                    Size = new Vector2(4.0f, 3.0f),
                    RoomIndex = 0,
                },
                new SurveillanceZoneDefinition
                {
                    Label = "审讯室监控",
                    Center = new Vector2(-3.2f, 1.6f),
                    Size = new Vector2(2.8f, 2.2f),
                    RoomIndex = 1,
                },
                new SurveillanceZoneDefinition
                {
                    Label = "证物室监控",
                    Center = new Vector2(-3.0f, -1.8f),
                    Size = new Vector2(3.0f, 2.2f),
                    RoomIndex = 2,
                },
                new SurveillanceZoneDefinition
                {
                    Label = "监控室监控",
                    Center = new Vector2(3.1f, -1.6f),
                    Size = new Vector2(2.6f, 2.0f),
                    RoomIndex = 3,
                },
            };
        }

        // ─── 视线遮挡体 ────────────────────────────

        public static BlockerVolume[] Blockers()
        {
            return new[]
            {
                // 大厅中央柱
                new BlockerVolume
                {
                    Name = "大厅柱",
                    Center = new Vector2(0f, 0.2f),
                    Size = new Vector2(0.3f, 0.3f),
                },
                // 证物室货架
                new BlockerVolume
                {
                    Name = "证物架",
                    Center = new Vector2(-3.0f, -1.3f),
                    Size = new Vector2(1.6f, 0.2f),
                },
                // 监控室控制台
                new BlockerVolume
                {
                    Name = "监控台",
                    Center = new Vector2(3.1f, -1.1f),
                    Size = new Vector2(1.4f, 0.3f),
                },
                // 简报室白板
                new BlockerVolume
                {
                    Name = "白板",
                    Center = new Vector2(2.8f, 2.0f),
                    Size = new Vector2(1.2f, 0.15f),
                },
                // 拘留室栅栏
                new BlockerVolume
                {
                    Name = "拘留栅栏",
                    Center = new Vector2(-0.8f, 2.4f),
                    Size = new Vector2(1.6f, 0.15f),
                },
            };
        }

        // ─── 出生点 ────────────────────────────────

        public static Vector2[] Spawns()
        {
            return new[]
            {
                new Vector2(0f, -0.5f),       // 大厅
                new Vector2(-2.2f, 1.2f),     // 审讯室附近
                new Vector2(-2.0f, -1.2f),    // 证物室附近
                new Vector2(2.0f, -1.0f),     // 监控室附近
                new Vector2(-0.3f, 1.8f),     // 拘留室附近
                new Vector2(2.5f, 1.0f),      // 简报室附近
                new Vector2(0.5f, -1.5f),     // 南走廊
                new Vector2(-1.5f, -0.3f),    // 西走廊
                new Vector2(1.5f, 0.5f),      // 东走廊
                new Vector2(-2.5f, 2.5f),     // 北区
            };
        }

        // ══════════════════════════════════════════════════════
        // 工厂方法：生成完整的 MapLayoutData（编辑器/运行时均可）
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 创建一个填充好警察局数据的 MapLayoutData ScriptableObject。
        /// 可在运行时用于 BuildGreyboxMap，也可在编辑器中保存为 .asset 文件。
        /// </summary>
        public static MapLayoutData CreateMapLayoutAsset()
        {
            var layout = ScriptableObject.CreateInstance<MapLayoutData>();
            layout.name = "MapLayout_PoliceStation";
            PopulateLayout(layout);
            return layout;
        }

        /// <summary>
        /// 将一个已存在的 MapLayoutData 实例填充为警察局数据。
        /// </summary>
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
