using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// 将烘焙好的 Kenney Sprite 应用到灰盒地图房间上。
    ///
    /// 不修改玩法逻辑——只在视觉层叠加 Sprite，替换纯色矩形。
    /// 灰盒碰撞/步行区/遮挡体不受影响。
    ///
    /// 使用方式：
    ///   decorator.DecorateAllRooms(mapService.ShipRooms(), worldBuilder, worldRoot);
    /// </summary>
    public class KenneySpriteDecorator
    {
        private readonly KenneySpriteCatalog _catalog;
        private readonly OnlineMapService _mapService;
        private readonly OnlineWorldBuilder _worldBuilder;
        private readonly GameObject _worldRoot;

        /// <summary>装饰生成的房间 SpriteRenderer 列表（用于后续回收或替换）</summary>
        public List<GameObject> DecoratedObjects { get; } = new List<GameObject>();

        public KenneySpriteDecorator(
            KenneySpriteCatalog catalog,
            OnlineMapService mapService,
            OnlineWorldBuilder worldBuilder,
            GameObject worldRoot)
        {
            _catalog = catalog;
            _mapService = mapService;
            _worldBuilder = worldBuilder;
            _worldRoot = worldRoot;
        }

        /// <summary>
        /// 为所有 12 个房间铺设 Kenney 建筑 Sprite。
        /// 动画参数控制淡入效果。
        /// </summary>
        public void DecorateAllRooms(OnlineMapService.ShipRoomSpec[] rooms)
        {
            if (_catalog == null)
            {
                Debug.LogWarning("[Kenney] No catalog assigned, skipping decoration.");
                return;
            }

            foreach (var room in rooms)
            {
                ApplyRoomSprite(room);
            }

            Debug.Log($"[Kenney] Decorated {DecoratedObjects.Count} rooms.");
        }

        /// <summary>
        /// 为单个房间匹配并铺设 Sprite。
        /// 主体 Sprite 填满房间区域，细节 Sprite（如有）放在房间入口附近。
        /// </summary>
        private void ApplyRoomSprite(OnlineMapService.ShipRoomSpec room)
        {
            var bag = _catalog.MatchRoom(room.Label);

            // ── 主体建筑 Sprite ──
            if (bag.Main != null)
            {
                var mainObj = _worldBuilder.CreateShapeProp(
                    $"Kenney_Main_{room.Label}",
                    bag.Main,
                    room.Center,        // 设计坐标，CreateShapeProp 内部会转换
                    room.Size,          // 设计坐标
                    Color.white);       // 不染色，保留原纹理

                DecoratedObjects.Add(mainObj);
            }

            // ── 细节装饰 ──
            if (bag.Detail != null)
            {
                // 细节放在房间入口附近（偏移到房间边缘）
                Vector3 detailPos = room.Center + GetEntranceOffset(room.Entrance, room.Size);
                Vector3 detailScale = new Vector3(
                    room.Size.x * 0.25f,
                    room.Size.y * 0.2f,
                    1f);

                var detailObj = _worldBuilder.CreateShapeProp(
                    $"Kenney_Detail_{room.Label}",
                    bag.Detail,
                    detailPos,
                    detailScale,
                    Color.white);

                DecoratedObjects.Add(detailObj);
            }
        }

        /// <summary>清理所有装饰对象</summary>
        public void ClearDecorations()
        {
            foreach (var obj in DecoratedObjects)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
            DecoratedObjects.Clear();
        }

        // ══════════════════════════════════════════════════════

        /// <summary>根据入口方向计算装饰偏移（设计坐标）</summary>
        private static Vector3 GetEntranceOffset(OnlineMapService.MapEntrance entrance, Vector3 roomSize)
        {
            switch (entrance)
            {
                case OnlineMapService.MapEntrance.North:
                    return new Vector3(0f, roomSize.y * 0.4f, 0f);
                case OnlineMapService.MapEntrance.South:
                    return new Vector3(0f, -roomSize.y * 0.4f, 0f);
                case OnlineMapService.MapEntrance.East:
                    return new Vector3(roomSize.x * 0.4f, 0f, 0f);
                case OnlineMapService.MapEntrance.West:
                    return new Vector3(-roomSize.x * 0.4f, 0f, 0f);
                default:
                    return Vector3.zero;
            }
        }
    }
}
