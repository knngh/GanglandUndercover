using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机地图坐标服务：集中管理地图尺寸、坐标转换、出生点、任务点、
    /// 暗线节点、会议座位和房间区域定义等所有地图坐标相关逻辑。
    /// 地图为2D（XY 平面），坐标统一为 Vector3（z 分量恒为 0）。
    ///
    /// M8.1: 支持多地图（港区/警署），通过 ActiveMapType 切换。
    /// </summary>
    public sealed class OnlineMapService : MonoBehaviour
    {
        // ---- 地图类型枚举 ----

        /// <summary>联机地图类型</summary>
        public enum OnlineMapType
        {
            HarbourDistrict,  // 港区（12 房间，默认）
            PoliceStation,    // 警署（6 房间）
        }

        [Header("地图类型")]
        [SerializeField, Tooltip("当前使用的地图类型")]
        private OnlineMapType activeMapType = OnlineMapType.HarbourDistrict;

        /// <summary>当前活跃的地图类型（运行时可在开局前修改）</summary>
        public OnlineMapType ActiveMapType
        {
            get => activeMapType;
            set
            {
                activeMapType = value;
                ApplyMapConfig();
            }
        }

        [Header("地图尺寸（世界坐标）")]
        [SerializeField, Tooltip("地图半宽（世界单位）")]
        private float mapHalfWidth = 24.0f;

        [SerializeField, Tooltip("地图半高（世界单位）")]
        private float mapHalfHeight = 14.0f;

        [Header("设计坐标缩放")]
        [SerializeField, Tooltip("设计坐标 X 轴缩放因子")]
        private float designScaleX = 2.0f;

        [SerializeField, Tooltip("设计坐标 Y 轴缩放因子")]
        private float designScaleY = 1.85f;

        [Header("警署地图专用设置")]
        [SerializeField, Tooltip("警署地图缩放因子（警署比港区小，需更大缩放）")]
        private float policeStationScale = 3.0f;

        // ---- 运行时配置缓存 ----
        private float _runtimeScaleX;
        private float _runtimeScaleY;
        private float _runtimeHalfWidth;
        private float _runtimeHalfHeight;

        private void Awake()
        {
            ApplyMapConfig();
        }

        /// <summary>根据当前地图类型应用对应的尺寸和缩放配置</summary>
        private void ApplyMapConfig()
        {
            if (activeMapType == OnlineMapType.PoliceStation)
            {
                float psHalfW = Map.PoliceStationMapLayout.DesignHalfWidth;
                float psHalfH = Map.PoliceStationMapLayout.DesignHalfHeight;
                _runtimeScaleX = policeStationScale;
                _runtimeScaleY = policeStationScale;
                _runtimeHalfWidth = psHalfW * policeStationScale;
                _runtimeHalfHeight = psHalfH * policeStationScale;
            }
            else
            {
                _runtimeScaleX = designScaleX;
                _runtimeScaleY = designScaleY;
                _runtimeHalfWidth = mapHalfWidth;
                _runtimeHalfHeight = mapHalfHeight;
            }
        }

        // ---- 公开属性 ----

        public float MapHalfWidth => _runtimeHalfWidth;
        public float MapHalfHeight => _runtimeHalfHeight;
        public float DesignScaleX => _runtimeScaleX;
        public float DesignScaleY => _runtimeScaleY;
        public float DesignMapHalfWidth => _runtimeHalfWidth / _runtimeScaleX;
        public float DesignMapHalfHeight => _runtimeHalfHeight / _runtimeScaleY;

        // ---- 枚举与结构 ----

        public enum MapEntrance
        {
            North,
            South,
            East,
            West
        }

        public readonly struct ShipRoomSpec
        {
            public ShipRoomSpec(string name, string label, Vector3 center, Vector3 size, Color floor, MapEntrance entrance)
            {
                Name = name;
                Label = label;
                Center = center;
                Size = size;
                Floor = floor;
                Entrance = entrance;
            }

            public readonly string Name;
            public readonly string Label;
            public readonly Vector3 Center;
            public readonly Vector3 Size;
            public readonly Color Floor;
            public readonly MapEntrance Entrance;
        }

        // ---- 坐标转换 ----

        /// <summary>设计坐标 → 世界坐标</summary>
        public Vector3 ScaleMapPosition(Vector3 designPosition)
        {
            return new Vector3(designPosition.x * designScaleX, designPosition.y * designScaleY, designPosition.z);
        }

        /// <summary>设计尺寸 → 世界尺寸</summary>
        public Vector3 ScaleMapSize(Vector3 designSize)
        {
            return new Vector3(designSize.x * designScaleX, designSize.y * designScaleY, designSize.z);
        }

        /// <summary>世界坐标钳制到地图边界</summary>
        public Vector3 ClampToOnlineMap(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, -mapHalfWidth, mapHalfWidth),
                Mathf.Clamp(position.y, -mapHalfHeight, mapHalfHeight),
                0f);
        }

        /// <summary>设计坐标钳制到设计地图边界</summary>
        public Vector3 ClampToDesignMap(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, -DesignMapHalfWidth, DesignMapHalfWidth),
                Mathf.Clamp(position.y, -DesignMapHalfHeight, DesignMapHalfHeight),
                0f);
        }

        // ---- 出生点 ----

        /// <summary>根据索引获取出生点世界坐标（最多10个预设位置，循环使用）</summary>
        public Vector3 SpawnPosition(int index)
        {
            Vector3[] spawns =
            {
                ScaleMapPosition(new Vector3(-9.45f, 4.95f, 0f)),
                ScaleMapPosition(new Vector3(-5.05f, 1.82f, 0f)),
                ScaleMapPosition(new Vector3(-1.08f, 2.62f, 0f)),
                ScaleMapPosition(new Vector3(4.72f, 2.68f, 0f)),
                ScaleMapPosition(new Vector3(8.82f, 4.45f, 0f)),
                ScaleMapPosition(new Vector3(0f, -4.55f, 0f)),
                ScaleMapPosition(new Vector3(-8.55f, -4.42f, 0f)),
                ScaleMapPosition(new Vector3(5.52f, -1.32f, 0f)),
                ScaleMapPosition(new Vector3(6.05f, -4.42f, 0f)),
                ScaleMapPosition(new Vector3(-9.32f, 1.18f, 0f))
            };

            return spawns[index % spawns.Length];
        }

        // ---- 任务点 ----

        /// <summary>根据任务ID获取任务点世界坐标（28个预设位置，默认返回原点）</summary>
        public Vector3 TaskPositionFor(int id)
        {
            switch (id)
            {
                case 0:  return ScaleMapPosition(new Vector3(-9.45f, 2.2f, 0f));
                case 1:  return ScaleMapPosition(new Vector3(-8.75f, 5.72f, 0f));
                case 2:  return ScaleMapPosition(new Vector3(8.7f, 5.42f, 0f));
                case 3:  return ScaleMapPosition(new Vector3(-8.65f, -4.95f, 0f));
                case 4:  return ScaleMapPosition(new Vector3(0.2f, -5.32f, 0f));
                case 5:  return ScaleMapPosition(new Vector3(-4.82f, 1.38f, 0f));
                case 6:  return ScaleMapPosition(new Vector3(-0.25f, 3.25f, 0f));
                case 7:  return ScaleMapPosition(new Vector3(3.98f, 2.9f, 0f));
                case 8:  return ScaleMapPosition(new Vector3(8.82f, 1.72f, 0f));
                case 9:  return ScaleMapPosition(new Vector3(6.12f, -5.12f, 0f));
                case 10: return ScaleMapPosition(new Vector3(-10.55f, 4.45f, 0f));
                case 11: return ScaleMapPosition(new Vector3(5.65f, 3.22f, 0f));
                case 12: return ScaleMapPosition(new Vector3(5.38f, -1.02f, 0f));
                case 13: return ScaleMapPosition(new Vector3(-1.42f, -0.18f, 0f));
                case 14: return ScaleMapPosition(new Vector3(9.62f, 4.72f, 0f));
                case 15: return ScaleMapPosition(new Vector3(-7.62f, -5.68f, 0f));
                case 16: return ScaleMapPosition(new Vector3(6.35f, 2.18f, 0f));
                case 17: return ScaleMapPosition(new Vector3(-6.86f, 0.16f, 0f));
                case 18: return ScaleMapPosition(new Vector3(1.74f, -3.62f, 0f));
                case 19: return ScaleMapPosition(new Vector3(7.25f, -4.45f, 0f));
                case 20: return ScaleMapPosition(new Vector3(-1.95f, 2.22f, 0f));
                case 21: return ScaleMapPosition(new Vector3(-9.88f, 1.25f, 0f));
                case 22: return ScaleMapPosition(new Vector3(5.65f, 2.52f, 0f));
                case 23: return ScaleMapPosition(new Vector3(-10.35f, 5.05f, 0f));
                case 24: return ScaleMapPosition(new Vector3(0.95f, -4.62f, 0f));
                case 25: return ScaleMapPosition(new Vector3(6.74f, -1.95f, 0f));
                case 26: return ScaleMapPosition(new Vector3(-6.85f, -2.82f, 0f));
                case 27: return ScaleMapPosition(new Vector3(8.95f, 1.12f, 0f));
                default: return Vector3.zero;
            }
        }

        // ---- 暗线/通风管节点 ----

        /// <summary>暗线节点设计坐标（最多4个预设位置）</summary>
        public Vector3 UnderworldPassageDesignPosition(int index, int passageCount)
        {
            int wrapped = passageCount > 0 ? index % passageCount : index % 4;
            switch (wrapped)
            {
                case 0:  return new Vector3(-6.85f, 0.72f, 0f);
                case 1:  return new Vector3(1.65f, 3.65f, 0f);
                case 2:  return new Vector3(6.95f, -2.25f, 0f);
                default: return new Vector3(-7.25f, -4.15f, 0f);
            }
        }

        /// <summary>暗线节点世界坐标</summary>
        public Vector3 UnderworldPassagePosition(int index, int passageCount)
        {
            return ScaleMapPosition(UnderworldPassageDesignPosition(index, passageCount));
        }

        // ---- 会议座位 ----

        /// <summary>
        /// 会议座位设计坐标（圆形排列，顺时针从顶部开始）。
        /// seatIndex: 0-based 座位序号；seatCount: 总座位数。
        /// </summary>
        public Vector3 MeetingSeatDesignPosition(int seatIndex, int seatCount)
        {
            float angle = seatIndex / (float)seatCount * Mathf.PI * 2f + Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(angle) * 1.18f, -0.35f + Mathf.Sin(angle) * 0.78f, 0f);
        }

        /// <summary>会议座位世界坐标</summary>
        public Vector3 MeetingSeatWorldPosition(int seatIndex, int seatCount)
        {
            return ScaleMapPosition(MeetingSeatDesignPosition(seatIndex, seatCount));
        }

        // ---- 地图区域定义（房间） ----

        /// <summary>获取所有房间/区域定义（设计坐标），根据 ActiveMapType 返回对应地图的房间</summary>
        public ShipRoomSpec[] ShipRooms()
        {
            if (activeMapType == OnlineMapType.PoliceStation)
                return PoliceStationRooms();
            return HarbourDistrictRooms();
        }

        /// <summary>港区（Harbour District）12 个房间定义（设计坐标）</summary>
        public ShipRoomSpec[] HarbourDistrictRooms()
        {
            return new[]
            {
                new ShipRoomSpec("西码头货柜场", "货柜舱", new Vector3(-9.3f, 5.35f, 0f), new Vector3(4.35f, 2.12f, 0.16f), new Color(0.13f, 0.24f, 0.21f, 1f), MapEntrance.South),
                new ShipRoomSpec("海关查验区", "海关查验", new Vector3(-5.0f, 5.35f, 0f), new Vector3(3.05f, 2.08f, 0.16f), new Color(0.18f, 0.25f, 0.19f, 1f), MapEntrance.South),
                new ShipRoomSpec("监控室", "监控中心", new Vector3(-9.35f, 1.85f, 0f), new Vector3(2.95f, 1.9f, 0.16f), new Color(0.1f, 0.19f, 0.27f, 1f), MapEntrance.East),
                new ShipRoomSpec("茶餐厅", "茶餐厅", new Vector3(-4.8f, 1.65f, 0f), new Vector3(2.95f, 1.86f, 0.16f), new Color(0.32f, 0.18f, 0.1f, 1f), MapEntrance.West),
                new ShipRoomSpec("夜市主街", "情报夜市", new Vector3(-1.0f, 2.75f, 0f), new Vector3(4.08f, 2.12f, 0.16f), new Color(0.27f, 0.14f, 0.09f, 1f), MapEntrance.South),
                new ShipRoomSpec("金融楼", "洗钱账房", new Vector3(4.75f, 2.75f, 0f), new Vector3(3.42f, 2.12f, 0.16f), new Color(0.16f, 0.18f, 0.28f, 1f), MapEntrance.West),
                new ShipRoomSpec("电房", "电力机房", new Vector3(8.85f, 5.25f, 0f), new Vector3(2.8f, 2.12f, 0.16f), new Color(0.12f, 0.19f, 0.27f, 1f), MapEntrance.South),
                new ShipRoomSpec("天台通道", "天台观测", new Vector3(8.95f, 1.65f, 0f), new Vector3(2.76f, 1.86f, 0.16f), new Color(0.18f, 0.19f, 0.3f, 1f), MapEntrance.West),
                new ShipRoomSpec("指挥车广场", "指挥广场", new Vector3(0f, -5.35f, 0f), new Vector3(4.35f, 1.92f, 0.16f), new Color(0.11f, 0.2f, 0.29f, 1f), MapEntrance.North),
                new ShipRoomSpec("证物库", "证物冷藏", new Vector3(-8.6f, -5.05f, 0f), new Vector3(3.35f, 1.98f, 0.16f), new Color(0.2f, 0.16f, 0.27f, 1f), MapEntrance.East),
                new ShipRoomSpec("后巷排档", "黑市排档", new Vector3(5.6f, -1.55f, 0f), new Vector3(3.55f, 2.14f, 0.16f), new Color(0.25f, 0.14f, 0.09f, 1f), MapEntrance.West),
                new ShipRoomSpec("地下诊所", "地下诊疗", new Vector3(6.15f, -5.05f, 0f), new Vector3(3.45f, 1.98f, 0.16f), new Color(0.13f, 0.25f, 0.2f, 1f), MapEntrance.North)
            };
        }

        /// <summary>警署（Police Station）6 个房间定义（设计坐标）</summary>
        public ShipRoomSpec[] PoliceStationRooms()
        {
            return new[]
            {
                new ShipRoomSpec("大厅",       "Lobby",      new Vector3(0f,   0f,   0f),  new Vector3(3.2f, 2.4f, 0.16f), new Color(0.18f, 0.22f, 0.32f, 1f), MapEntrance.South),
                new ShipRoomSpec("审讯室",     "IntRoom",    new Vector3(-3.2f, 1.6f, 0f),  new Vector3(2.4f, 1.8f, 0.16f), new Color(0.28f, 0.18f, 0.18f, 1f), MapEntrance.East),
                new ShipRoomSpec("证物室",     "Evidence",   new Vector3(-3.0f, -1.8f, 0f), new Vector3(2.6f, 1.8f, 0.16f), new Color(0.14f, 0.22f, 0.16f, 1f), MapEntrance.East),
                new ShipRoomSpec("监控室",     "Armory",     new Vector3(3.1f, -1.6f, 0f),  new Vector3(2.2f, 1.6f, 0.16f), new Color(0.22f, 0.20f, 0.14f, 1f), MapEntrance.West),
                new ShipRoomSpec("拘留室",     "Cells",      new Vector3(-0.8f, 2.4f, 0f),  new Vector3(2.0f, 1.8f, 0.16f), new Color(0.16f, 0.16f, 0.22f, 1f), MapEntrance.South),
                new ShipRoomSpec("简报室",     "Briefing",   new Vector3(2.8f, 1.5f, 0f),   new Vector3(2.4f, 1.8f, 0.16f), new Color(0.20f, 0.24f, 0.30f, 1f), MapEntrance.West),
            };
        }

        // ══════════════════════════════════════════════════════
        // M6.1 监控摄像头布点
        // ══════════════════════════════════════════════════════

        /// <summary>监控摄像头区域定义</summary>
        public readonly struct SurveillanceZoneSpec
        {
            public SurveillanceZoneSpec(string label, Vector3 center, Vector3 size, int roomIndex)
            {
                Label = label;
                Center = center;
                Size = size;
                RoomIndex = roomIndex;
            }

            public readonly string Label;
            public readonly Vector3 Center;   // 设计坐标
            public readonly Vector3 Size;     // 设计坐标
            public readonly int RoomIndex;    // -1 = 走廊/公共区域
        }

        /// <summary>
        /// 获取所有监控摄像头布点（设计坐标），根据 ActiveMapType 返回对应地图的监控配置。
        /// </summary>
        public SurveillanceZoneSpec[] SurveillanceZones()
        {
            if (activeMapType == OnlineMapType.PoliceStation)
                return PoliceStationSurveillanceZones();
            return HarbourDistrictSurveillanceZones();
        }

        /// <summary>港区 6 个监控摄像头布点（设计坐标）</summary>
        public SurveillanceZoneSpec[] HarbourDistrictSurveillanceZones()
        {
            return new[]
            {
                new SurveillanceZoneSpec("监控室主机",          new Vector3(-9.35f, 1.85f, 0f),  new Vector3(3.5f, 2.2f, 0f),   2),   // 监控室
                new SurveillanceZoneSpec("西码头公共走廊",       new Vector3(-7.0f, 3.6f, 0f),   new Vector3(3.0f, 2.5f, 0f),   -1),  // 货柜场-海关走廊
                new SurveillanceZoneSpec("夜市情报街",           new Vector3(-1.0f, 2.75f, 0f),  new Vector3(5.0f, 2.5f, 0f),   4),   // 夜市主街
                new SurveillanceZoneSpec("金融楼大厅",           new Vector3(4.75f, 2.75f, 0f),  new Vector3(4.0f, 2.5f, 0f),   5),   // 金融楼
                new SurveillanceZoneSpec("指挥广场中枢",         new Vector3(0f, -3.8f, 0f),     new Vector3(5.5f, 2.5f, 0f),   -1),  // 广场+会议区
                new SurveillanceZoneSpec("后巷暗角",             new Vector3(5.6f, -1.55f, 0f),  new Vector3(4.0f, 2.5f, 0f),   10),  // 后巷排档
            };
        }

        /// <summary>警署 4 个监控摄像头布点（设计坐标）</summary>
        public SurveillanceZoneSpec[] PoliceStationSurveillanceZones()
        {
            return new[]
            {
                new SurveillanceZoneSpec("大厅监控",     new Vector3(0f,   0f,   0f),   new Vector3(4.0f, 3.0f, 0f), 0),  // 大厅（房间0）
                new SurveillanceZoneSpec("审讯室监控",   new Vector3(-3.2f, 1.6f, 0f),  new Vector3(2.8f, 2.2f, 0f), 1),  // 审讯室（房间1）
                new SurveillanceZoneSpec("证物室监控",   new Vector3(-3.0f, -1.8f, 0f), new Vector3(3.0f, 2.2f, 0f), 2),  // 证物室（房间2）
                new SurveillanceZoneSpec("监控室监控",   new Vector3(3.1f, -1.6f, 0f),  new Vector3(2.6f, 2.0f, 0f), 3),  // 监控室（房间3）
            };
        }
    }
}
