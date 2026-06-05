using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.Art;

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// M6.1+E3 灰盒地图建造器。
    ///
    /// 从 MapLayoutData 生成地图：地面、墙壁、碰撞、可步行区域。
    /// E3 增强：每个房间建造后调用 RoomDecorator 添加美术道具和装饰。
    ///
    /// 坐标系：内部使用设计坐标，建造时通过 OnlineMapService 转换为世界坐标。
    /// </summary>
    public sealed class GreyboxMapBuilder
    {
        private readonly OnlineMapService _mapService;
        private readonly OnlineWorldBuilder _worldBuilder;
        private readonly MapLayoutData _layout;
        private readonly GameObject _worldRoot;
        private RoomDecorator _decorator;

        // 运行中收集的 POI 引用
        public readonly List<GameObject> BuiltWalls = new List<GameObject>();
        public readonly List<GameObject> BuiltRooms = new List<GameObject>();
        public readonly List<GameObject> BuiltCorridors = new List<GameObject>();
        public readonly List<GameObject> BuiltVents = new List<GameObject>();
        public readonly List<GameObject> BuiltCameras = new List<GameObject>();
        public readonly List<Rect> WalkableRects = new List<Rect>();

        // 颜色常量
        private static readonly Color FloorColor = new Color(0.12f, 0.13f, 0.14f, 1f);
        private static readonly Color CorridorColor = new Color(0.10f, 0.11f, 0.12f, 1f);
        private static readonly Color VentColor = new Color(0.25f, 0.20f, 0.30f, 1f);
        private static readonly Color MeetingColor = new Color(0.15f, 0.18f, 0.22f, 1f);
        private static readonly Color CameraColor = new Color(0.08f, 0.45f, 0.65f, 0.6f);
        private static readonly Color SeatColor = new Color(0.25f, 0.28f, 0.32f, 1f);
        private static readonly Color TaskSpotColor = new Color(0.92f, 0.72f, 0.12f, 0.5f);

        /// <summary>按地图类型获取墙壁颜色</summary>
        private Color WallColorForMap(OnlineMapService.OnlineMapType mapType)
        {
            switch (mapType)
            {
                case OnlineMapService.OnlineMapType.PoliceStation:
                    return MapTilePalette.WallPolice;
                case OnlineMapService.OnlineMapType.KowloonWalledCity:
                    return MapTilePalette.WallKowloon;
                default:
                    return MapTilePalette.WallLight;
            }
        }

        public GreyboxMapBuilder(OnlineMapService mapService, OnlineWorldBuilder worldBuilder,
            MapLayoutData layout, GameObject worldRoot)
        {
            _mapService = mapService;
            _worldBuilder = worldBuilder;
            _layout = layout;
            _worldRoot = worldRoot;
        }

        // ══════════════════════════════════════════════════════
        // 主建造流程
        // ══════════════════════════════════════════════════════

        public void BuildAll()
        {
            if (_layout == null)
            {
                Debug.LogError("[GreyboxMapBuilder] MapLayoutData is null — cannot build.");
                return;
            }

            _worldBuilder.EnsureRuntimeSprites();

            BuildGroundPlane();
            BuildCorridorNetwork();
            BuildRooms();
            BuildTaskSpots();
            BuildVentNodes();
            BuildSurveillanceZones();
            BuildMeetingPoint();
            BuildSightBlockers();
            BuildMapBoundary();

            Debug.Log($"[GreyboxMapBuilder] Built greybox map: " +
                      $"{BuiltRooms.Count} rooms, {BuiltCorridors.Count} corridors, " +
                      $"{BuiltWalls.Count} walls, {BuiltVents.Count} vents, " +
                      $"{BuiltCameras.Count} cameras, {WalkableRects.Count} walkable zones.");
        }

        // ══════════════════════════════════════════════════════
        // 各层建造
        // ══════════════════════════════════════════════════════

        private void BuildGroundPlane()
        {
            float hw = _layout.DesignHalfWidth + 1f;
            float hh = _layout.DesignHalfHeight + 1f;
            _worldBuilder.CreateShapeProp("Ground Plane",
                GanglandUndercover.Art.Sprite2DAssetCache.FloorConcrete,
                new Vector3(0f, 0f, -0.5f),
                new Vector3(hw * 2f, hh * 2f, 0.1f),
                FloorColor);
        }

        private void BuildCorridorNetwork()
        {
            if (_layout.Corridors == null) return;

            foreach (var corridor in _layout.Corridors)
            {
                Vector3 center = new Vector3(corridor.Center.x, corridor.Center.y, -0.24f);

                if (corridor.IsRoundNode)
                {
                    var obj = _worldBuilder.CreateShapeProp(
                        $"Greybox Node {corridor.Name}",
                        _worldBuilder.CircleSprite,
                        center,
                        new Vector3(corridor.NodeRadius * 2f, corridor.NodeRadius * 2f, 0.08f),
                        CorridorColor);
                    obj.name = $"Greybox Node {corridor.Name}";
                    BuiltCorridors.Add(obj);
                }
                else
                {
                    var obj = _worldBuilder.CreateProp(
                        $"Greybox Corridor {corridor.Name}",
                        center,
                        new Vector3(corridor.Size.x, corridor.Size.y, 0.08f),
                        CorridorColor);
                    BuiltCorridors.Add(obj);
                }

                if (corridor.Walkable)
                {
                    Vector2 c = corridor.Center;
                    Vector2 s = corridor.IsRoundNode
                        ? new Vector2(corridor.NodeRadius * 1.8f, corridor.NodeRadius * 1.8f)
                        : corridor.Size * 0.9f;
                    WalkableRects.Add(new Rect(c.x - s.x * 0.5f, c.y - s.y * 0.5f, s.x, s.y));
                }
            }
        }

        private void BuildRooms()
        {
            if (_layout.Rooms == null) return;

            _decorator = new RoomDecorator(_worldBuilder, _mapService, _worldRoot);
            Color wallColor = WallColorForMap(_mapService.ActiveMapType);

            for (int i = 0; i < _layout.Rooms.Length; i++)
            {
                var room = _layout.Rooms[i];
                Vector3 center = new Vector3(room.Center.x, room.Center.y, 0.06f);
                Vector3 size = new Vector3(room.Size.x, room.Size.y, room.Size.z);

                // E3: 使用 MapTilePalette 按地图类型+房间索引查地板色
                Color floorColor = MapTilePalette.FloorColor(
                    _mapService.ActiveMapType, i);
                // 如果布局数据中已有自定义颜色，优先使用
                if (room.FloorColor.a > 0.1f) floorColor = room.FloorColor;

                GameObject floor = _worldBuilder.CreateShapeProp(
                    $"Room {room.Name}",
                    Sprite2DAssetCache.FloorTileAlt,
                    center + new Vector3(0f, 0f, -0.05f),
                    size,
                    floorColor);
                floor.name = $"Room {room.Name}";
                BuiltRooms.Add(floor);

                // 墙壁（四边框，入口方向留缺口）—— E3: 使用地图专属墙壁颜色
                BuildRoomWalls(center, size, room.Entrance, wallColor);

                // 可步行区域
                WalkableRects.Add(new Rect(
                    center.x - size.x * 0.4f,
                    center.y - size.y * 0.45f,
                    size.x * 0.8f,
                    size.y * 0.9f));

                // E3: 房间美术装饰
                _decorator.DecorateRoom(room, i);
            }

            Debug.Log($"[GreyboxMapBuilder] E3 decorated {_layout.Rooms.Length} rooms, " +
                      $"{_decorator.DecoratedProps.Count} props, " +
                      $"{_decorator.EntranceMarkers.Count} entrance markers.");
        }

        private void BuildRoomWalls(Vector3 center, Vector3 size, OnlineMapService.MapEntrance entrance, Color wallColor)
        {
            float hw = size.x * 0.5f;
            float hh = size.y * 0.5f;
            float wallThick = 0.12f;
            float wallDepth = 0.22f;

            if (entrance != OnlineMapService.MapEntrance.North)
            {
                var w = CreateSolidGreyboxWall(
                    $"Wall {center.x:F1},{center.y:F1} N",
                    center + new Vector3(0f, hh, 0f),
                    new Vector3(size.x, wallThick, wallDepth), wallColor);
                BuiltWalls.Add(w);
            }

            if (entrance != OnlineMapService.MapEntrance.South)
            {
                var w = CreateSolidGreyboxWall(
                    $"Wall {center.x:F1},{center.y:F1} S",
                    center + new Vector3(0f, -hh, 0f),
                    new Vector3(size.x, wallThick, wallDepth), wallColor);
                BuiltWalls.Add(w);
            }

            if (entrance != OnlineMapService.MapEntrance.West)
            {
                var w = CreateSolidGreyboxWall(
                    $"Wall {center.x:F1},{center.y:F1} W",
                    center + new Vector3(-hw, 0f, 0f),
                    new Vector3(wallThick, size.y, wallDepth), wallColor);
                BuiltWalls.Add(w);
            }

            if (entrance != OnlineMapService.MapEntrance.East)
            {
                var w = CreateSolidGreyboxWall(
                    $"Wall {center.x:F1},{center.y:F1} E",
                    center + new Vector3(hw, 0f, 0f),
                    new Vector3(wallThick, size.y, wallDepth), wallColor);
                BuiltWalls.Add(w);
            }
        }

        private GameObject CreateSolidGreyboxWall(string name, Vector3 center, Vector3 size, Color wallColor)
        {
            return _worldBuilder.CreateShapeProp(name,
                Sprite2DAssetCache.WallBrick,
                center, size, wallColor);
        }

        private void BuildTaskSpots()
        {
            if (_layout.TaskAssignments == null) return;

            foreach (var task in _layout.TaskAssignments)
            {
                Vector3 pos = new Vector3(task.Position.x, task.Position.y, 0.12f);
                _worldBuilder.CreateShapeProp(
                    $"Greybox TaskSpot {task.TaskId}",
                    _worldBuilder.SoftCircleSprite,
                    pos,
                    new Vector3(0.35f, 0.35f, 0.04f),
                    TaskSpotColor);
            }
        }

        private void BuildVentNodes()
        {
            if (_layout.VentNodes == null) return;

            foreach (var vent in _layout.VentNodes)
            {
                Vector3 pos = new Vector3(vent.Position.x, vent.Position.y, 0.14f);
                var obj = _worldBuilder.CreateShapeProp(
                    $"Greybox Vent {vent.Name}",
                    _worldBuilder.CircleSprite,
                    pos,
                    new Vector3(0.4f, 0.4f, 0.08f),
                    VentColor);
                obj.name = $"Greybox Vent {vent.Name}";
                BuiltVents.Add(obj);
            }
        }

        private void BuildSurveillanceZones()
        {
            if (_layout.SurveillanceZones == null) return;

            foreach (var zone in _layout.SurveillanceZones)
            {
                Vector3 center = new Vector3(zone.Center.x, zone.Center.y, 0.1f);
                Vector3 size = new Vector3(zone.Size.x, zone.Size.y, 0.04f);

                var obj = _worldBuilder.CreateShapeProp(
                    $"Greybox CCTV {zone.Label}",
                    _worldBuilder.SoftCircleSprite,
                    center,
                    size,
                    CameraColor);
                obj.name = $"Greybox CCTV {zone.Label}";
                BuiltCameras.Add(obj);
            }
        }

        private void BuildMeetingPoint()
        {
            Vector3 center = new Vector3(_layout.MeetingCenter.x, _layout.MeetingCenter.y, 0.12f);

            // 会议桌
            _worldBuilder.CreateShapeProp(
                "Greybox Meeting Table",
                _worldBuilder.RoundedRectSprite,
                center,
                new Vector3(2.5f, 1.8f, 0.08f),
                MeetingColor);

            // 座位标记
            for (int i = 0; i < _layout.MaxMeetingSeats; i++)
            {
                float angle = i / (float)_layout.MaxMeetingSeats * Mathf.PI * 2f + Mathf.PI * 0.5f;
                Vector3 seat = center + new Vector3(
                    Mathf.Cos(angle) * 1.4f,
                    Mathf.Sin(angle) * 0.9f,
                    0.06f);
                _worldBuilder.CreateShapeProp(
                    $"Greybox Seat {i}",
                    _worldBuilder.CircleSprite,
                    seat,
                    new Vector3(0.18f, 0.18f, 0.06f),
                    SeatColor);
            }

            WalkableRects.Add(new Rect(
                center.x - 1.8f, center.y - 1.3f, 3.6f, 2.6f));
        }

        private void BuildSightBlockers()
        {
            if (_layout.SightBlockers == null) return;

            Color wallColor = WallColorForMap(_mapService.ActiveMapType);
            foreach (var blocker in _layout.SightBlockers)
            {
                Vector3 center = new Vector3(blocker.Center.x, blocker.Center.y, 0.18f);
                Vector3 size = new Vector3(blocker.Size.x, blocker.Size.y, 0.32f);
                CreateSolidGreyboxWall($"Blocker {blocker.Name}", center, size, wallColor);
            }
        }

        private void BuildMapBoundary()
        {
            float hw = _layout.DesignHalfWidth;
            float hh = _layout.DesignHalfHeight;
            float thick = 0.2f;
            float tall = 0.5f;
            Color wallColor = WallColorForMap(_mapService.ActiveMapType);

            CreateSolidGreyboxWall("Boundary N", new Vector3(0f, hh, 0f), new Vector3(hw * 2f + 1f, thick, tall), wallColor);
            CreateSolidGreyboxWall("Boundary S", new Vector3(0f, -hh, 0f), new Vector3(hw * 2f + 1f, thick, tall), wallColor);
            CreateSolidGreyboxWall("Boundary W", new Vector3(-hw, 0f, 0f), new Vector3(thick, hh * 2f + 1f, tall), wallColor);
            CreateSolidGreyboxWall("Boundary E", new Vector3(hw, 0f, 0f), new Vector3(thick, hh * 2f + 1f, tall), wallColor);
        }
    }
}
