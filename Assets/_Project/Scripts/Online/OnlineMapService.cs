using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机地图坐标服务：集中管理地图尺寸、坐标转换、出生点、任务点、
    /// 暗线节点、会议座位和房间区域定义等所有地图坐标相关逻辑。
    /// 地图为2D（XY 平面），坐标统一为 Vector3（z 分量恒为 0）。
    /// </summary>
    public sealed class OnlineMapService : MonoBehaviour
    {
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

        // ---- 公开属性 ----

        public float MapHalfWidth => mapHalfWidth;
        public float MapHalfHeight => mapHalfHeight;
        public float DesignScaleX => designScaleX;
        public float DesignScaleY => designScaleY;
        public float DesignMapHalfWidth => mapHalfWidth / designScaleX;
        public float DesignMapHalfHeight => mapHalfHeight / designScaleY;

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

        /// <summary>获取所有12个港区房间/区域定义（设计坐标）</summary>
        public ShipRoomSpec[] ShipRooms()
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
    }
}
