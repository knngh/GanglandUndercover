using GanglandUndercover.Online.Map;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// E3 地图瓦片调色板。
    /// 为每张地图提供专属 tile 配色方案，替换灰盒的单一灰色地面。
    /// 通过 GreyboxMapBuilder 的房间索引查询对应颜色。
    /// </summary>
    public static class MapTilePalette
    {
        /// <summary>港区 12 房间地板色</summary>
        public static readonly Color[] HarbourFloors = {
            new Color(0.15f, 0.20f, 0.18f, 1f),  // 货柜场：铁锈绿
            new Color(0.20f, 0.22f, 0.25f, 1f),  // 海关：深蓝灰
            new Color(0.12f, 0.18f, 0.28f, 1f),  // 监控室：暗蓝
            new Color(0.30f, 0.18f, 0.12f, 1f),  // 茶餐厅：木色
            new Color(0.25f, 0.14f, 0.10f, 1f),  // 夜市：暗红棕
            new Color(0.18f, 0.20f, 0.30f, 1f),  // 金融楼：冷蓝
            new Color(0.14f, 0.20f, 0.26f, 1f),  // 电房：金属蓝
            new Color(0.20f, 0.22f, 0.32f, 1f),  // 天台：灰蓝
            new Color(0.12f, 0.20f, 0.28f, 1f),  // 指挥广场：警蓝
            new Color(0.22f, 0.18f, 0.28f, 1f),  // 证物库：紫灰
            new Color(0.26f, 0.15f, 0.10f, 1f),  // 后巷排档：暗红
            new Color(0.14f, 0.24f, 0.22f, 1f),  // 地下诊所：青绿
        };

        /// <summary>警署 6 房间地板色</summary>
        public static readonly Color[] PoliceFloors = {
            new Color(0.22f, 0.24f, 0.30f, 1f),  // 大厅：蓝灰
            new Color(0.26f, 0.20f, 0.20f, 1f),  // 审讯室：暗红
            new Color(0.16f, 0.24f, 0.18f, 1f),  // 证物室：档案绿
            new Color(0.20f, 0.22f, 0.18f, 1f),  // 监控室：暗黄绿
            new Color(0.18f, 0.18f, 0.24f, 1f),  // 拘留室：铁灰
            new Color(0.22f, 0.24f, 0.28f, 1f),  // 简报室：白灰
        };

        /// <summary>九龙城寨 8 房间地板色</summary>
        public static readonly Color[] KowloonFloors = {
            new Color(0.30f, 0.18f, 0.12f, 1f),  // 茶餐厅：暖木
            new Color(0.18f, 0.28f, 0.16f, 1f),  // 药材铺：草药绿
            new Color(0.28f, 0.22f, 0.14f, 1f),  // 麻将馆：檀木
            new Color(0.16f, 0.18f, 0.22f, 1f),  // 天井：石板灰
            new Color(0.24f, 0.16f, 0.10f, 1f),  // 后巷：砖红
            new Color(0.20f, 0.20f, 0.30f, 1f),  // 天台：夜空蓝
            new Color(0.22f, 0.18f, 0.12f, 1f),  // 地下钱庄：金棕
            new Color(0.12f, 0.15f, 0.20f, 1f),  // 暗渠：深蓝灰
        };

        /// <summary>墙壁颜色</summary>
        public static readonly Color WallLight  = new Color(0.30f, 0.28f, 0.25f, 1f);
        public static readonly Color WallDark   = new Color(0.18f, 0.17f, 0.15f, 1f);
        public static readonly Color WallPolice = new Color(0.22f, 0.24f, 0.28f, 1f);
        public static readonly Color WallKowloon= new Color(0.28f, 0.18f, 0.12f, 1f);

        /// <summary>获取房间地板颜色（按地图类型+房间索引）</summary>
        public static Color FloorColor(OnlineMapService.OnlineMapType mapType, int roomIndex)
        {
            switch (mapType)
            {
                case OnlineMapService.OnlineMapType.PoliceStation:
                    return roomIndex >= 0 && roomIndex < PoliceFloors.Length
                        ? PoliceFloors[roomIndex] : new Color(0.15f, 0.17f, 0.20f, 1f);
                case OnlineMapService.OnlineMapType.KowloonWalledCity:
                    return roomIndex >= 0 && roomIndex < KowloonFloors.Length
                        ? KowloonFloors[roomIndex] : new Color(0.18f, 0.16f, 0.14f, 1f);
                default:
                    return roomIndex >= 0 && roomIndex < HarbourFloors.Length
                        ? HarbourFloors[roomIndex] : new Color(0.14f, 0.16f, 0.18f, 1f);
            }
        }
    }
}
