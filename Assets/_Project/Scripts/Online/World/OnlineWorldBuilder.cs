using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

using GanglandUndercover.Art;
using GanglandUndercover.Core;
using GanglandUndercover.Online.World;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// World / prop / task / body / player visual builder.
    /// Extracted from OnlineMatchController for M2 controller slimming.
    /// Holds all rendering layer state (sprites, materials, model caches, collision rects, labels).
    /// Pure class — no MonoBehaviour dependency.
    /// M3: Use2DBackend defaults to true — all world props render via SpriteRenderer.
    /// </summary>
    public class OnlineWorldBuilder : IWorldBuilder
    {
        // --- M3 Backend Toggle ---
        /// <summary>
        /// When true, all world building uses SpriteRenderer exclusively.
        /// When false, legacy 3D MeshRenderer path is preserved for comparison.
        /// Set before calling BuildWorld() / BuildPoliceStation().
        /// </summary>
        public bool Use2DBackend = true;

        // --- References (set at init) ---
        private GameObject _worldRoot;
        private OnlineMapService _mapService;
        private List<Rect> _solidObstacleRects;
        private List<Rect> _walkableRects;
        private List<TextMesh> _worldLabels;

        // --- Caches ---
        private readonly Dictionary<string, GameObject> _modelPrefabCache = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Material> _runtimeMeshMaterials = new Dictionary<string, Material>();

        // --- Runtime sprites ---
        private Sprite _roundedRectSprite;
        private Sprite _circleSprite;
        private Sprite _softCircleSprite;
        private Sprite _diamondSprite;
        private Sprite _capsuleSprite;

        // --- M2 refactoring: underworld passage count passed by controller ---
        private int _underworldPassageCount;

        // --- Constants migrated from controller ---
        private const string AssetStoreResourceRoot = "AssetStore/";

        // --- Task list (set from controller after Initialize) ---
        private IReadOnlyList<OnlineTaskState> _tasks;

        // --- Backward compat: migrated code uses worldRoot (no underscore) ---
        private GameObject worldRoot => _worldRoot;

        // --- Public accessors ---
        public GameObject WorldRoot => _worldRoot;
        public OnlineMapService MapService => _mapService;
        public IReadOnlyList<Rect> SolidObstacleRects => _solidObstacleRects;
        public IReadOnlyList<Rect> WalkableRects => _walkableRects;
        public IReadOnlyList<TextMesh> WorldLabels => _worldLabels;
        public Sprite RoundedRectSprite => _roundedRectSprite;
        public Sprite CircleSprite => _circleSprite;
        public Sprite SoftCircleSprite => _softCircleSprite;
        public Sprite DiamondSprite => _diamondSprite;
        public Sprite CapsuleSprite => _capsuleSprite;
        public int UnderworldPassageCount => _underworldPassageCount;
        public int ModelPrefabCacheCount => _modelPrefabCache.Count;
        public int RuntimeMeshMaterialCount => _runtimeMeshMaterials.Count;
        public int OperationalLightingElementCount => _operationalLightingElementCount;
        public int LimeZuFirstScreenSpriteElementCount => _limeZuFirstScreenSpriteElementCount;
        public int LimeZuTaskMiniGameSetPieceSpriteElementCount => _limeZuTaskMiniGameSetPieceSpriteElementCount;
        public int LimeZuTaskStationSpriteElementCount => _limeZuTaskStationSpriteElementCount;
        public int LimeZuLandmarkSpriteElementCount => _limeZuLandmarkSpriteElementCount;
        public int LimeZuTaskEventFeedbackSpriteElementCount => _limeZuTaskEventFeedbackSpriteElementCount;
        public int LimeZuRoomPropSpriteElementCount => _limeZuRoomPropSpriteElementCount;
        public int RuntimeMapPropSpriteElementCount => _runtimeMapPropSpriteElementCount;
        public string FloorTileResourcePath => FloorTileResourcePathInternal;
        public string WallTileResourcePath => WallTileResourcePathInternal;

        private int _operationalLightingElementCount;
        private int _limeZuFirstScreenSpriteElementCount;
        private int _limeZuTaskMiniGameSetPieceSpriteElementCount;
        private int _limeZuTaskStationSpriteElementCount;
        private int _limeZuLandmarkSpriteElementCount;
        private int _limeZuTaskEventFeedbackSpriteElementCount;
        private int _limeZuRoomPropSpriteElementCount;
        private int _runtimeMapPropSpriteElementCount;

        private enum LimeZuVisualCounter
        {
            FirstScreen,
            TaskMiniGameSetPiece,
            TaskStation,
            Landmark,
            TaskEventFeedback,
            RoomProp
        }

        // Tiled floor/wall CC0 sprites (loaded on demand)
        private const string LimeZuFloorTilePath = "Sprites/Tilesets/LimeZu/Exteriors/floors/asphalt-48-a";
        private const string LimeZuWallTilePath = "Sprites/Tilesets/LimeZu/Interiors/walls/room-builder-walls-16";
        private const string FallbackFloorTilePath = "Sprites/Tilesets/Harbour/floors/cargo-bay";
        private const string FallbackWallTilePath = "Sprites/Tilesets/Harbour/walls/indtech_000_000";
        private Sprite _floorTileSprite;
        private Sprite _wallTileSprite;
        private string _floorTileResourcePath;
        private string _wallTileResourcePath;
        private Sprite FloorTileSprite => _floorTileSprite ?? (_floorTileSprite = LoadFloorTile());
        private Sprite WallTileSprite  => _wallTileSprite  ?? (_wallTileSprite  = LoadWallTile());
        private string FloorTileResourcePathInternal => _floorTileResourcePath ?? string.Empty;
        private string WallTileResourcePathInternal => _wallTileResourcePath ?? string.Empty;

        private Sprite LoadFloorTile()
        {
            var sprite = LoadRuntimeTileSprite(LimeZuFloorTilePath, 48f, out _floorTileResourcePath);
            if (sprite != null) return sprite;
            sprite = LoadRuntimeTileSprite(FallbackFloorTilePath, 32f, out _floorTileResourcePath);
            if (sprite != null) return sprite;
            return _roundedRectSprite;
        }

        private Sprite LoadWallTile()
        {
            var sprite = LoadRuntimeTileSprite(LimeZuWallTilePath, 16f, out _wallTileResourcePath);
            if (sprite != null) return sprite;
            sprite = LoadRuntimeTileSprite(FallbackWallTilePath, 32f, out _wallTileResourcePath);
            if (sprite != null) return sprite;
            return _roundedRectSprite;
        }

        private static Sprite LoadRuntimeTileSprite(string resourcePath, float pixelsPerUnit, out string loadedResourcePath)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                loadedResourcePath = string.Empty;
                return null;
            }

            tex.filterMode = FilterMode.Point;
            loadedResourcePath = resourcePath;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        public void Initialize(GameObject worldRoot, OnlineMapService mapService,
            List<Rect> solidObstacleRects, List<Rect> walkableRects, List<TextMesh> worldLabels,
            int underworldPassageCount = 8)
        {
            _worldRoot = worldRoot;
            _mapService = mapService;
            _solidObstacleRects = solidObstacleRects;
            _walkableRects = walkableRects;
            _worldLabels = worldLabels;
            _underworldPassageCount = underworldPassageCount;
        }

        public void SetTasks(IReadOnlyList<OnlineTaskState> taskList)
        {
            _tasks = taskList;
        }

        private static int TaskTemplateMode(int taskId)
        {
            switch (taskId)
            {
                case 0: case 6: case 13: case 21: return 0;
                case 1: case 10: case 20: case 23: return 1;
                case 2: case 7: case 12: case 14: case 24: return 2;
                case 3: case 9: case 15: case 19: return 3;
                case 4: case 11: case 16: case 22: return 4;
                default: return 5;
            }
        }

        private void CreateVerticalSliceProductionLayer()
        {
            CreateVerticalSliceGroundPlan();
            CreateVerticalSliceRoomIdentityLayer();
            CreateVerticalSliceTaskMiniGameLayer();
            CreateVerticalSliceStageOneFirstScreenLayer();
            CreateVerticalSliceStageOneEntranceLayer();
            CreateVerticalSliceStageOneSightlineLayer();
            CreateVerticalSliceStageOneMeetingAndBlackoutLayer();
            CreateVerticalSliceStageOneGameplayAnchorLayer();
            CreateVerticalSliceStageOneCameraShotLayer();
            CreateVerticalSliceStageOneEditableAnchorLayer();
        }

        private void CreateVerticalSliceGroundPlan()
        {
            Sprite2DAssetCache.Ensure();

            Color asphalt = new Color(0.085f, 0.1f, 0.102f, 1f);
            Color sidewalk = new Color(0.18f, 0.19f, 0.18f, 1f);
            Color wetReflection = new Color(0.08f, 0.18f, 0.2f, 0.72f);
            Color guide = new Color(0.82f, 0.68f, 0.2f, 1f);

            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu central wet plaza", Sprite2DAssetCache.CorridorTile, Sprite2DAssetCache.CorridorTileResourcePath, new Vector3(-1.25f, -0.56f, -0.31f), new Vector3(5.4f, 2.58f, 0.08f), asphalt);
            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu meeting ring stone apron", Sprite2DAssetCache.FloorTileAlt, Sprite2DAssetCache.FloorTileAltResourcePath, new Vector3(-1.18f, -0.72f, -0.285f), new Vector3(1.62f, 1.18f, 0.08f), new Color(0.28f, 0.3f, 0.28f, 1f));
            CreateShapeProp("VerticalSlice Ground meeting ring wet core", CircleSprite, new Vector3(-1.18f, -0.72f, -0.27f), new Vector3(1.05f, 0.78f, 0.08f), wetReflection);
            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu cctv corridor", Sprite2DAssetCache.CorridorTile, Sprite2DAssetCache.CorridorTileResourcePath, new Vector3(-6.6f, 0.68f, -0.3f), new Vector3(3.5f, 1.02f, 0.08f), sidewalk);
            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu cha chaan teng threshold", Sprite2DAssetCache.FloorTileAlt, Sprite2DAssetCache.FloorTileAltResourcePath, new Vector3(-4.42f, 1.34f, -0.29f), new Vector3(2.55f, 1.24f, 0.08f), new Color(0.26f, 0.16f, 0.08f, 1f));
            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu night market bend", Sprite2DAssetCache.FloorTileAlt, Sprite2DAssetCache.FloorTileAltResourcePath, new Vector3(-0.4f, 2.56f, -0.3f), new Vector3(4.4f, 1.08f, 0.08f), new Color(0.19f, 0.12f, 0.1f, 1f));
            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu non-square diagonal market cut", Sprite2DAssetCache.FloorTileAlt, Sprite2DAssetCache.FloorTileAltResourcePath, new Vector3(1.72f, 2.05f, -0.295f), new Vector3(2.6f, 0.78f, 0.08f), new Color(0.16f, 0.12f, 0.1f, 1f), -18f);
            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu alley approach", Sprite2DAssetCache.CorridorTile, Sprite2DAssetCache.CorridorTileResourcePath, new Vector3(3.88f, -1.25f, -0.305f), new Vector3(3.7f, 0.96f, 0.08f), new Color(0.13f, 0.11f, 0.1f, 1f));
            CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu service lane diagonal", Sprite2DAssetCache.CorridorTile, Sprite2DAssetCache.CorridorTileResourcePath, new Vector3(5.45f, 0.42f, -0.302f), new Vector3(2.95f, 0.7f, 0.08f), new Color(0.11f, 0.13f, 0.13f, 1f), 22f);

            RegisterWalkableArea(new Vector3(-1.25f, -0.56f, 0f), new Vector3(5.7f, 2.78f, 0.08f));
            RegisterWalkableArea(new Vector3(-6.6f, 0.68f, 0f), new Vector3(3.8f, 1.18f, 0.08f));
            RegisterWalkableArea(new Vector3(-4.42f, 1.34f, 0f), new Vector3(2.85f, 1.44f, 0.08f));
            RegisterWalkableArea(new Vector3(-0.4f, 2.56f, 0f), new Vector3(4.65f, 1.24f, 0.08f));
            RegisterWalkableArea(new Vector3(3.88f, -1.25f, 0f), new Vector3(3.9f, 1.12f, 0.08f));
            RegisterWalkableArea(new Vector3(5.45f, 0.42f, 0f), new Vector3(3.2f, 0.86f, 0.08f));

            for (int i = 0; i < 13; i++)
            {
                float x = -3.8f + i * 0.43f;
                CreateProp("VerticalSlice Ground plaza paving joint " + i, new Vector3(x, -0.02f + Mathf.Sin(i * 0.8f) * 0.08f, -0.18f), new Vector3(0.22f, 0.025f, 0.04f), new Color(0.36f, 0.38f, 0.34f, 0.82f));
            }

            for (int i = 0; i < 10; i++)
            {
                CreateRotatedProp("VerticalSlice Ground yellow route paint " + i, new Vector3(-5.35f + i * 0.86f, 0.42f + Mathf.Sin(i * 0.65f) * 0.12f, -0.16f), new Vector3(0.42f, 0.035f, 0.04f), guide, i % 2 == 0 ? -6f : 8f);
            }
        }

        private void CreateVerticalSliceRoomIdentityLayer()
        {
            (string id, string label, Vector3 center, Vector3 size, Color color)[] rooms =
            {
                ("CCTV", "监控室", new Vector3(-8.88f, 1.72f, 0.06f), new Vector3(2.35f, 1.48f, 0.62f), new Color(0.08f, 0.15f, 0.2f, 1f)),
                ("Cafe", "茶餐厅", new Vector3(-4.64f, 1.58f, 0.06f), new Vector3(2.52f, 1.46f, 0.54f), new Color(0.32f, 0.18f, 0.08f, 1f)),
                ("Market", "夜市", new Vector3(-0.72f, 2.88f, 0.06f), new Vector3(3.64f, 1.3f, 0.46f), new Color(0.28f, 0.12f, 0.08f, 1f)),
                ("Alley", "后巷", new Vector3(4.92f, -1.48f, 0.06f), new Vector3(2.72f, 1.34f, 0.5f), new Color(0.17f, 0.12f, 0.09f, 1f)),
                ("Power", "电房", new Vector3(7.88f, 4.82f, 0.06f), new Vector3(2.28f, 1.52f, 0.66f), new Color(0.1f, 0.17f, 0.22f, 1f)),
                ("Meeting", "集合", new Vector3(-1.18f, -0.72f, 0.05f), new Vector3(2.15f, 1.42f, 0.38f), new Color(0.12f, 0.19f, 0.2f, 1f))
            };

            for (int i = 0; i < rooms.Length; i++)
            {
                CreateVerticalSliceRoomShell(rooms[i].id, rooms[i].label, rooms[i].center, rooms[i].size, rooms[i].color);
            }

            CreateAssetStoreProp("VerticalSlice Room 茶餐厅 free building front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Coffee Shop", new Vector3(-4.72f, 2.4f, 0.12f), new Vector3(1.1f, 0.58f, 0.72f), -3f, false);
            CreateAssetStoreProp("VerticalSlice Room 夜市 restaurant front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Restaurant", new Vector3(-1.02f, 3.82f, 0.12f), new Vector3(1.38f, 0.58f, 0.76f), 4f, false);
            CreateSolidAssetStoreProp("VerticalSlice Room 后巷 garage front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Auto Service", new Vector3(6.02f, -2.26f, 0.12f), new Vector3(1.18f, 0.62f, 0.78f), -8f, false);
            CreateSolidAssetStoreProp("VerticalSlice Room 电房 lowpoly utility front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Factory", new Vector3(8.5f, 5.85f, 0.14f), new Vector3(1.22f, 0.66f, 0.86f), 5f, false);
        }

        private void CreateVerticalSliceRoomShell(string id, string label, Vector3 center, Vector3 size, Color color)
        {
            Sprite2DAssetCache.Ensure();

            Color wall = new Color(0.045f, 0.052f, 0.055f, 1f);
            Color trim = new Color(0.64f, 0.58f, 0.42f, 1f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;

            CreateLimeZuFirstScreenProp("VerticalSlice Room " + id + " LimeZu playable shell floor", Sprite2DAssetCache.FloorTileAlt, Sprite2DAssetCache.FloorTileAltResourcePath, center + new Vector3(0f, 0f, -0.11f), size, Darken(color, 0.82f));
            CreateLimeZuFirstScreenProp("VerticalSlice Room " + id + " LimeZu back wall volume", Sprite2DAssetCache.WallBlock, Sprite2DAssetCache.WallBlockResourcePath, center + new Vector3(0f, halfHeight, 0.28f), new Vector3(size.x, 0.1f, size.z), wall);
            CreateLimeZuFirstScreenProp("VerticalSlice Room " + id + " LimeZu left return wall", Sprite2DAssetCache.WallBlock, Sprite2DAssetCache.WallBlockResourcePath, center + new Vector3(-halfWidth, 0f, 0.24f), new Vector3(0.1f, size.y, size.z * 0.86f), wall);
            CreateLimeZuFirstScreenProp("VerticalSlice Room " + id + " LimeZu right return wall", Sprite2DAssetCache.WallBlock, Sprite2DAssetCache.WallBlockResourcePath, center + new Vector3(halfWidth, 0f, 0.24f), new Vector3(0.1f, size.y * 0.72f, size.z * 0.8f), wall);
            CreateLimeZuFirstScreenProp("VerticalSlice Room " + id + " LimeZu gold trim back", Sprite2DAssetCache.PropCabinet, Sprite2DAssetCache.PropCabinetResourcePath, center + new Vector3(0f, halfHeight - 0.08f, 0.56f), new Vector3(size.x * 0.78f, 0.04f, 0.06f), trim);
            CreateMeshBoxProp("VerticalSlice Room " + id + " sign board " + label, center + new Vector3(0f, halfHeight - 0.15f, 0.72f), new Vector3(Mathf.Min(size.x * 0.55f, 1.2f), 0.045f, 0.18f), new Color(0.08f, 0.64f, 0.82f, 1f));
            RegisterWalkableArea(center, new Vector3(size.x * 0.82f, size.y * 0.78f, 0.08f));
            CreateWorldLabelAt(label, MapService.ScaleMapPosition(center + new Vector3(0f, halfHeight + 0.12f, -0.08f)), 0.065f);
        }

        private void CreateVerticalSliceTaskMiniGameLayer()
        {
            Sprite2DAssetCache.Ensure();

            (string id, Vector3 position, Color accent)[] tasks =
            {
                ("CCTV", new Vector3(-9.45f, 2.12f, 0.18f), new Color(0.08f, 0.62f, 0.86f, 1f)),
                ("Recorder", new Vector3(-4.8f, 1.32f, 0.18f), new Color(0.92f, 0.48f, 0.12f, 1f)),
                ("Breaker", new Vector3(8.72f, 5.18f, 0.18f), new Color(0.96f, 0.76f, 0.12f, 1f)),
                ("Plate", new Vector3(1.74f, -3.62f, 0.18f), new Color(0.36f, 0.72f, 0.95f, 1f))
            };

            for (int taskIndex = 0; taskIndex < tasks.Length; taskIndex++)
            {
                GameObject root = CreateVerticalSliceTaskRoot("VerticalSlice Task " + tasks[taskIndex].id + " minigame set", tasks[taskIndex].position, tasks[taskIndex].accent);

                for (int i = 0; i < 6; i++)
                {
                    Vector3 offset = new Vector3(-0.52f + i * 0.21f, i % 2 == 0 ? 0.24f : -0.24f, 0.1f);
                    int assetIndex = taskIndex + i;
                    CreateLimeZuTaskSetPieceChild(root.transform,
                        "VerticalSlice Task " + tasks[taskIndex].id + " LimeZu physical prop " + i,
                        LimeZuSetPieceSprite(assetIndex),
                        LimeZuSetPieceResourcePath(assetIndex),
                        offset,
                        new Vector3(0.22f, 0.18f, 0.24f),
                        i % 2 == 0 ? Color.white : new Color(0.82f, 0.9f, 0.92f, 1f),
                        i * 14f);
                }

                for (int i = 0; i < 5; i++)
                {
                    CreateMeshBoxChild(root.transform, "VerticalSlice Task " + tasks[taskIndex].id + " minigame feedback segment " + i, new Vector3(-0.38f + i * 0.19f, 0.38f, 0.48f), new Vector3(0.12f, 0.028f, 0.08f + i % 2 * 0.05f), i % 2 == 0 ? tasks[taskIndex].accent : Darken(tasks[taskIndex].accent, 0.55f));
                }
            }
        }

        private GameObject CreateVerticalSliceTaskRoot(string name, Vector3 position, Color accent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(_worldRoot.transform, false);
            root.transform.position = MapService.ScaleMapPosition(position);
            CreateLimeZuTaskSetPieceChild(root.transform, "VerticalSlice Task LimeZu physical base", Sprite2DAssetCache.PropDesk, Sprite2DAssetCache.PropDeskResourcePath, new Vector3(0f, 0f, 0.12f), new Vector3(0.96f, 0.62f, 0.18f), new Color(0.92f, 0.96f, 0.98f, 1f));
            CreateLimeZuTaskSetPieceChild(root.transform, "VerticalSlice Task LimeZu lit face", Sprite2DAssetCache.PropCabinet, Sprite2DAssetCache.PropCabinetResourcePath, new Vector3(0f, 0.29f, 0.3f), new Vector3(0.72f, 0.035f, 0.2f), accent);
            CreateMeshBoxChild(root.transform, "VerticalSlice Task interaction halo", new Vector3(0f, 0f, -0.02f), new Vector3(1.08f, 0.72f, 0.035f), new Color(accent.r, accent.g, accent.b, 0.22f));
            SetSortingFromZ(root);
            return root;
        }

        private void CreateVerticalSliceStageOneFirstScreenLayer()
        {
            Sprite2DAssetCache.Ensure();

            Color shadow = new Color(0.018f, 0.022f, 0.024f, 0.82f);
            Color wetBlue = new Color(0.05f, 0.2f, 0.24f, 0.68f);
            Color tileA = new Color(0.14f, 0.148f, 0.138f, 1f);
            Color tileB = new Color(0.1f, 0.115f, 0.11f, 1f);
            Color brass = new Color(0.82f, 0.58f, 0.14f, 1f);

            for (int i = 0; i < 18; i++)
            {
                float x = -6.2f + i * 0.72f;
                float y = -1.38f + Mathf.Sin(i * 0.74f) * 0.42f;
                CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu wet route reflection " + i, Sprite2DAssetCache.CorridorTile, Sprite2DAssetCache.CorridorTileResourcePath, new Vector3(x, y, -0.17f), new Vector3(0.46f, 0.03f, 0.035f), i % 2 == 0 ? wetBlue : Darken(wetBlue, 0.78f), i % 2 == 0 ? -11f : 17f);
            }

            for (int i = 0; i < 18; i++)
            {
                float x = -5.5f + i * 0.62f;
                float y = i % 2 == 0 ? 0.28f : -0.52f;
                CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu brass curb marker " + i, Sprite2DAssetCache.WallBlock, Sprite2DAssetCache.WallBlockResourcePath, new Vector3(x, y, -0.08f), new Vector3(0.38f, 0.026f, 0.032f), brass, i % 2 == 0 ? -9f : 12f);
            }

            for (int i = 0; i < 12; i++)
            {
                Vector3 position = new Vector3(-6.35f + i * 1.12f, 2.22f + Mathf.Sin(i * 0.48f) * 0.42f, 0.66f);
                CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu authored awning shadow " + i, Sprite2DAssetCache.PropCabinet, Sprite2DAssetCache.PropCabinetResourcePath, position, new Vector3(1.42f, 0.12f, 0.28f), shadow, i % 2 == 0 ? -6f : 9f);
            }

            for (int i = 0; i < 12; i++)
            {
                Vector3 position = new Vector3(-4.7f + i * 0.82f, 1.92f + Mathf.Sin(i * 0.55f) * 0.28f, 0.58f);
                Color color = i % 3 == 0 ? new Color(0.04f, 0.7f, 0.94f, 1f) : i % 3 == 1 ? new Color(0.92f, 0.18f, 0.42f, 1f) : brass;
                CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu hanging neon strip " + i, Sprite2DAssetCache.PropDesk, Sprite2DAssetCache.PropDeskResourcePath, position, new Vector3(0.42f, 0.034f, 0.12f), color, i % 2 == 0 ? -5f : 7f);
            }

            for (int i = 0; i < 8; i++)
            {
                CreateLimeZuFirstScreenProp("VerticalSlice Stage1 FirstScreen LimeZu non-square paving slab " + i, Sprite2DAssetCache.FloorTileAlt, Sprite2DAssetCache.FloorTileAltResourcePath, new Vector3(-3.85f + i * 1.15f, -1.02f + Mathf.Sin(i) * 0.56f, -0.245f), new Vector3(1.05f, 0.32f, 0.045f), i % 2 == 0 ? tileA : tileB, i % 2 == 0 ? -12f : 8f);
            }
        }

        private void CreateVerticalSliceStageOneEntranceLayer()
        {
            foreach (VerticalSliceStageOneAnchorSpec spec in VerticalSliceStageOneAnchorCatalog.Specs)
            {
                if (spec.Category != "Room") continue;
                CreateVerticalSliceStageOneEntrance(spec);
            }
        }

        private void CreateVerticalSliceStageOneEntrance(VerticalSliceStageOneAnchorSpec spec)
        {
            Color frame = new Color(0.032f, 0.04f, 0.042f, 1f);
            Color glass = new Color(0.08f, 0.18f, 0.22f, 0.72f);
            Vector3 center = spec.DesignPosition;
            Vector3 size = new Vector3(spec.Footprint.x, 0.34f, 0.56f);
            float sideOffset = Mathf.Max(0.22f, size.x * 0.48f);

            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + spec.Id + " threshold floor", center + new Vector3(0f, 0f, -0.14f), new Vector3(size.x * 1.05f, size.y * 0.72f, 0.05f), Darken(spec.DebugColor, 0.38f));
            CreateSolidMeshBoxProp("VerticalSlice Stage1 Entrance " + spec.Id + " left jamb", center + new Vector3(-sideOffset, 0f, 0.18f), new Vector3(0.12f, size.y * 0.86f, size.z), frame);
            CreateSolidMeshBoxProp("VerticalSlice Stage1 Entrance " + spec.Id + " right jamb", center + new Vector3(sideOffset, 0f, 0.18f), new Vector3(0.12f, size.y * 0.86f, size.z), frame);
            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + spec.Id + " header", center + new Vector3(0f, size.y * 0.42f, 0.48f), new Vector3(size.x, 0.065f, 0.14f), frame);
            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + spec.Id + " glass glow", center + new Vector3(0f, size.y * 0.12f, 0.36f), new Vector3(size.x * 0.72f, 0.035f, size.z * 0.34f), glass);
            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + spec.Id + " role color strip", center + new Vector3(0f, size.y * 0.49f, 0.66f), new Vector3(size.x * 0.64f, 0.034f, 0.055f), spec.DebugColor);
        }

        private void CreateVerticalSliceStageOneSightlineLayer()
        {
            Color steel = new Color(0.035f, 0.044f, 0.048f, 1f);
            Color blue = new Color(0.06f, 0.42f, 0.88f, 1f);
            Color red = new Color(0.82f, 0.08f, 0.06f, 1f);
            Color amber = new Color(0.9f, 0.62f, 0.12f, 1f);

            for (int i = 0; i < 14; i++)
            {
                float x = -7.6f + i * 1.18f;
                float y = i % 2 == 0 ? -0.1f + Mathf.Sin(i) * 0.7f : 2.6f + Mathf.Sin(i * 0.6f) * 0.4f;
                Vector3 center = new Vector3(x, y, 0.32f);
                Vector3 scale = i % 2 == 0 ? new Vector3(1.0f, 0.14f, 0.48f) : new Vector3(0.16f, 1.0f, 0.48f);
                float rotation = i % 2 == 0 ? -12f : 10f;
                CreateSolidMeshBoxProp("VerticalSlice Stage1 Sightline authored blocker " + i, center, scale, steel, rotation);
                CreateMeshBoxProp("VerticalSlice Stage1 Sightline blocker status light " + i, center + new Vector3(0f, 0.1f, 0.32f), new Vector3(Mathf.Max(0.18f, scale.x * 0.52f), 0.03f, 0.05f), i % 3 == 0 ? blue : i % 3 == 1 ? red : amber, rotation);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = -9.2f + i * 1.68f;
                CreateMeshBoxProp("VerticalSlice Stage1 Sightline parallax roof occluder " + i, new Vector3(x, 4.28f + Mathf.Sin(i * 0.6f) * 0.12f, 0.58f), new Vector3(0.88f, 0.08f, 0.18f), new Color(0.004f, 0.006f, 0.008f, 0.74f), i % 2 == 0 ? -4f : 6f);
            }
        }

        private void CreateVerticalSliceStageOneMeetingAndBlackoutLayer()
        {
            Color police = new Color(0.08f, 0.32f, 0.9f, 1f);
            Color gang = new Color(0.84f, 0.08f, 0.06f, 1f);
            Color cyan = new Color(0.08f, 0.74f, 0.86f, 1f);
            Color amber = new Color(0.94f, 0.72f, 0.12f, 1f);
            Color table = new Color(0.14f, 0.17f, 0.18f, 1f);

            Vector3 meetingCenter = new Vector3(-1.18f, -0.72f, 0.16f);
            CreateMeshPrimitiveProp("VerticalSlice Stage1 Meeting evidence round table", PrimitiveType.Cylinder, meetingCenter + new Vector3(0f, 0f, 0.1f), new Vector3(0.86f, 0.055f, 0.86f), table, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting voice channel blue strip", meetingCenter + new Vector3(-0.28f, 0.2f, 0.34f), new Vector3(0.78f, 0.035f, 0.06f), police, -8f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting suspicion red strip", meetingCenter + new Vector3(0.26f, -0.22f, 0.34f), new Vector3(0.72f, 0.035f, 0.06f), gang, 10f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting evidence wall panel", meetingCenter + new Vector3(-1.24f, 0.48f, 0.62f), new Vector3(0.92f, 0.065f, 0.52f), new Color(0.78f, 0.8f, 0.72f, 1f), -6f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting evidence wall red thread", meetingCenter + new Vector3(-1.34f, 0.52f, 0.86f), new Vector3(0.58f, 0.025f, 0.04f), gang, 13f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting evidence wall blue thread", meetingCenter + new Vector3(-1.08f, 0.52f, 0.74f), new Vector3(0.46f, 0.025f, 0.04f), police, -16f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting overhead shadow frame", meetingCenter + new Vector3(0.2f, 0.96f, 0.88f), new Vector3(2.45f, 0.16f, 0.32f), new Color(0f, 0f, 0f, 0.56f));

            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                Vector3 seat = meetingCenter + new Vector3(Mathf.Cos(angle) * 1.36f, Mathf.Sin(angle) * 0.86f, 0.04f);
                CreateMeshPrimitiveProp("VerticalSlice Stage1 Meeting player voice seat " + i, PrimitiveType.Cylinder, seat, new Vector3(0.16f, 0.035f, 0.16f), i % 3 == 0 ? police : i % 3 == 1 ? gang : cyan, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshBoxProp("VerticalSlice Stage1 Meeting vote card " + i, seat + new Vector3(0f, 0.12f, 0.2f), new Vector3(0.22f, 0.03f, 0.06f), amber, i * 13f);
            }

            Vector3 blackoutCore = new Vector3(8.72f, 5.18f, 0.22f);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout breaker silhouette wall", blackoutCore + new Vector3(0f, 0.38f, 0.56f), new Vector3(1.42f, 0.1f, 0.78f), new Color(0.018f, 0.022f, 0.026f, 1f));
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout red emergency strip", blackoutCore + new Vector3(0f, 0.48f, 0.98f), new Vector3(1.12f, 0.035f, 0.06f), gang);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout repair target amber pad", blackoutCore + new Vector3(0f, -0.58f, 0.06f), new Vector3(0.92f, 0.045f, 0.06f), amber);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout visible cable A", blackoutCore + new Vector3(-0.42f, 0.05f, 0.54f), new Vector3(0.56f, 0.028f, 0.05f), cyan, -14f);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout visible cable B", blackoutCore + new Vector3(0.34f, -0.06f, 0.5f), new Vector3(0.5f, 0.028f, 0.05f), gang, 16f);
            CreateMeshBoxProp("Blackout VFX emergency red wash", blackoutCore + new Vector3(0f, 0.08f, 1.08f), new Vector3(1.32f, 0.04f, 0.08f), new Color(1f, 0.04f, 0.02f, 0.8f));
            CreateMeshBoxProp("Blackout VFX breaker spark left", blackoutCore + new Vector3(-0.36f, 0.3f, 0.86f), new Vector3(0.24f, 0.026f, 0.05f), amber, -22f);
            CreateMeshBoxProp("Blackout VFX breaker spark right", blackoutCore + new Vector3(0.42f, 0.22f, 0.86f), new Vector3(0.22f, 0.026f, 0.05f), cyan, 20f);
            CreateMeshBoxProp("Blackout VFX cable short flash", blackoutCore + new Vector3(0.02f, -0.18f, 0.64f), new Vector3(0.64f, 0.035f, 0.05f), new Color(0.86f, 0.92f, 1f, 0.92f), -4f);
            CreateShapeProp("Blackout VFX dimmed vision pool", SoftCircleSprite, blackoutCore + new Vector3(0f, -0.18f, 0.04f), new Vector3(1.72f, 1.08f, 0.04f), new Color(0f, 0f, 0f, 0.28f));

            for (int i = 0; i < 8; i++)
            {
                float x = -0.9f + i * 0.26f;
                CreateMeshBoxProp("VerticalSlice Stage1 Blackout fuse slot " + i, blackoutCore + new Vector3(x, 0.52f, 0.78f), new Vector3(0.12f, 0.028f, 0.08f), i % 2 == 0 ? amber : gang);
            }

            for (int i = 0; i < 9; i++)
            {
                Vector3 route = new Vector3(7.42f - i * 1.06f, 4.72f - Mathf.Sin(i * 0.55f) * 0.3f, 0.08f);
                CreateMeshBoxProp("VerticalSlice Stage1 Blackout emergency floor arrow " + i, route, new Vector3(0.42f, 0.035f, 0.05f), i % 2 == 0 ? amber : cyan, i % 2 == 0 ? -13f : 12f);
            }
        }

        private void CreateVerticalSliceStageOneGameplayAnchorLayer()
        {
            foreach (VerticalSliceStageOneAnchorSpec spec in VerticalSliceStageOneAnchorCatalog.Specs)
            {
                if (spec.Category != "Gameplay") continue;

                CreateShapeProp("VerticalSlice Stage1 GameplayAnchor " + spec.Id + " footprint", SoftCircleSprite, spec.DesignPosition + new Vector3(0f, 0f, -0.02f), new Vector3(spec.Footprint.x, spec.Footprint.y, 0.04f), new Color(spec.DebugColor.r, spec.DebugColor.g, spec.DebugColor.b, 0.18f));
                CreateMeshBoxProp("VerticalSlice Stage1 GameplayAnchor " + spec.Id + " readable strip", spec.DesignPosition + new Vector3(0f, 0.12f, 0.18f), new Vector3(Mathf.Max(0.42f, spec.Footprint.x * 0.48f), 0.04f, 0.07f), spec.DebugColor, -8f);
                CreateMeshBoxProp("VerticalSlice Stage1 GameplayAnchor " + spec.Id + " action marker", spec.DesignPosition + new Vector3(0f, -0.12f, 0.22f), new Vector3(Mathf.Max(0.34f, spec.Footprint.x * 0.36f), 0.035f, 0.06f), Darken(spec.DebugColor, 0.62f), 10f);
            }

            Vector3[] voiceCenters =
            {
                new Vector3(-1.18f, -0.72f, 0.02f),
                new Vector3(-4.8f, 1.32f, 0.02f),
                new Vector3(4.92f, -0.82f, 0.02f)
            };

            for (int i = 0; i < voiceCenters.Length; i++)
            {
                CreateShapeProp("VerticalSlice Stage1 GameplayAnchor action voice radius " + i, SoftCircleSprite, voiceCenters[i], new Vector3(2.0f, 1.25f, 0.04f), new Color(0.12f, 0.78f, 0.66f, 0.52f));
            }
        }

        private void CreateVerticalSliceStageOneCameraShotLayer()
        {
            foreach (VerticalSliceStageOneAnchorSpec spec in VerticalSliceStageOneAnchorCatalog.Specs)
            {
                if (spec.Category != "Camera") continue;

                Vector3 center = spec.DesignPosition + new Vector3(0f, 0f, 0.04f);
                Color color = spec.DebugColor;
                CreateShapeProp("VerticalSlice Stage1 CameraShot " + spec.Id + " footprint", SoftCircleSprite, center, new Vector3(spec.Footprint.x, spec.Footprint.y, 0.04f), new Color(color.r, color.g, color.b, 0.12f));
                CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame top", center + new Vector3(0f, spec.Footprint.y * 0.5f, 0.16f), new Vector3(spec.Footprint.x, 0.035f, 0.05f), color);
                CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame bottom", center + new Vector3(0f, -spec.Footprint.y * 0.5f, 0.16f), new Vector3(spec.Footprint.x, 0.035f, 0.05f), color);
                CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame left", center + new Vector3(-spec.Footprint.x * 0.5f, 0f, 0.16f), new Vector3(0.035f, spec.Footprint.y, 0.05f), color);
                CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame right", center + new Vector3(spec.Footprint.x * 0.5f, 0f, 0.16f), new Vector3(0.035f, spec.Footprint.y, 0.05f), color);
            }
        }

        private void CreateVerticalSliceStageOneEditableAnchorLayer()
        {
            foreach (VerticalSliceStageOneAnchorSpec spec in VerticalSliceStageOneAnchorCatalog.Specs)
            {
                GameObject anchor = new GameObject("VerticalSlice Stage1 EditableAnchor " + spec.Id);
                anchor.transform.SetParent(_worldRoot.transform, false);
                anchor.transform.position = MapService.ScaleMapPosition(spec.DesignPosition);
                VerticalSliceStageOneAnchor component = anchor.AddComponent<VerticalSliceStageOneAnchor>();
                component.Configure(spec);

                CreateShapeProp("VerticalSlice Stage1 GameplayAnchor editable footprint " + spec.Id, SoftCircleSprite, spec.DesignPosition + new Vector3(0f, 0f, -0.02f), new Vector3(spec.Footprint.x, spec.Footprint.y, 0.04f), new Color(spec.DebugColor.r, spec.DebugColor.g, spec.DebugColor.b, 0.08f));
            }
        }

        // ====================================================================
        //  SPRITES
        // ====================================================================

        public void EnsureRuntimeSprites()
        {
            if (_roundedRectSprite != null && _circleSprite != null && _softCircleSprite != null &&
                _diamondSprite != null && _capsuleSprite != null)
                return;

            // E1: 使用美术增强版程序化 sprite 替代纯色矩形
            GanglandUndercover.Art.Sprite2DAssetCache.Ensure();

            _roundedRectSprite = GanglandUndercover.Art.Sprite2DAssetCache.WallBlock;
            _circleSprite       = GanglandUndercover.Art.Sprite2DAssetCache.FloorTile;
            _softCircleSprite   = GanglandUndercover.Art.Sprite2DAssetCache.TaskGlow;
            _diamondSprite      = GanglandUndercover.Art.Sprite2DAssetCache.CharDirectionArrow;
            _capsuleSprite      = GanglandUndercover.Art.Sprite2DAssetCache.CorridorTile;
        }

        private GameObject CreateSpriteObject(string objectName, Sprite sprite, Color color)
        {
            EnsureRuntimeSprites();
            GameObject spriteObject = new GameObject(objectName);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : _roundedRectSprite;
            renderer.color = color;
            renderer.sortingOrder = SortingOrderForZ(spriteObject.transform.position.z);
            return spriteObject;
        }

        private void ResetLimeZuVisualCounters()
        {
            _limeZuFirstScreenSpriteElementCount = 0;
            _limeZuTaskMiniGameSetPieceSpriteElementCount = 0;
            _limeZuTaskStationSpriteElementCount = 0;
            _limeZuLandmarkSpriteElementCount = 0;
            _limeZuTaskEventFeedbackSpriteElementCount = 0;
            _limeZuRoomPropSpriteElementCount = 0;
            _runtimeMapPropSpriteElementCount = 0;
        }

        private static bool IsRuntimeLimeZuResource(string resourcePath)
        {
            return !string.IsNullOrEmpty(resourcePath)
                && resourcePath.IndexOf("Sprites/Tilesets/LimeZu/", StringComparison.Ordinal) >= 0;
        }

        private static bool IsRuntimeMapPropResource(string resourcePath)
        {
            return !string.IsNullOrEmpty(resourcePath)
                && (resourcePath.IndexOf("Sprites/Tilesets/Harbour/props/", StringComparison.Ordinal) >= 0
                    || resourcePath.IndexOf("Sprites/Tilesets/KowloonWalledCity/props/", StringComparison.Ordinal) >= 0);
        }

        private void CountLimeZuSpriteUse(string resourcePath, LimeZuVisualCounter counter)
        {
            if (!IsRuntimeLimeZuResource(resourcePath)) return;

            switch (counter)
            {
                case LimeZuVisualCounter.FirstScreen:
                    _limeZuFirstScreenSpriteElementCount++;
                    break;
                case LimeZuVisualCounter.TaskMiniGameSetPiece:
                    _limeZuTaskMiniGameSetPieceSpriteElementCount++;
                    break;
                case LimeZuVisualCounter.TaskStation:
                    _limeZuTaskStationSpriteElementCount++;
                    break;
                case LimeZuVisualCounter.Landmark:
                    _limeZuLandmarkSpriteElementCount++;
                    break;
                case LimeZuVisualCounter.TaskEventFeedback:
                    _limeZuTaskEventFeedbackSpriteElementCount++;
                    break;
                case LimeZuVisualCounter.RoomProp:
                    _limeZuRoomPropSpriteElementCount++;
                    break;
            }
        }

        private void CountRuntimeMapPropUse(string resourcePath)
        {
            if (IsRuntimeMapPropResource(resourcePath))
            {
                _runtimeMapPropSpriteElementCount++;
            }
        }

        private GameObject CreateLimeZuProp(string propName, Sprite sprite, string resourcePath, Vector3 position,
            Vector3 scale, Color color, LimeZuVisualCounter counter, float rotationDegrees = 0f, bool solid = false)
        {
            GameObject prop = CreateShapeProp(propName, sprite, position, scale, color);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            SetSortingFromZ(prop);

            if (solid)
            {
                RegisterSolidObstacle(position, scale);
                AttachPhysicsCollider(prop, scale, false);
            }

            CountLimeZuSpriteUse(resourcePath, counter);
            return prop;
        }

        private GameObject CreateLimeZuChild(Transform parent, string objectName, Sprite sprite, string resourcePath,
            Vector3 localPosition, Vector3 scale, Color color, LimeZuVisualCounter counter,
            float rotationDegrees = 0f)
        {
            GameObject child = CreateSpriteChild(parent, objectName, sprite, localPosition, scale, color);
            child.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            SetSortingFromZ(child);
            CountLimeZuSpriteUse(resourcePath, counter);
            return child;
        }

        private GameObject CreateLimeZuFirstScreenProp(string propName, Sprite sprite, string resourcePath,
            Vector3 position, Vector3 scale, Color color, float rotationDegrees = 0f, bool solid = false)
        {
            return CreateLimeZuProp(propName, sprite, resourcePath, position, scale, color,
                LimeZuVisualCounter.FirstScreen, rotationDegrees, solid);
        }

        private GameObject CreateLimeZuTaskSetPieceChild(Transform parent, string objectName, Sprite sprite,
            string resourcePath, Vector3 localPosition, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            return CreateLimeZuChild(parent, objectName, sprite, resourcePath, localPosition, scale, color,
                LimeZuVisualCounter.TaskMiniGameSetPiece, rotationDegrees);
        }

        private GameObject CreateLimeZuTaskStationChild(Transform parent, string objectName, Sprite sprite,
            string resourcePath, Vector3 localPosition, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            return CreateLimeZuChild(parent, objectName, sprite, resourcePath, localPosition, scale, color,
                LimeZuVisualCounter.TaskStation, rotationDegrees);
        }

        private GameObject CreateLimeZuLandmarkProp(string propName, Sprite sprite, string resourcePath,
            Vector3 position, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            return CreateLimeZuProp(propName, sprite, resourcePath, position, scale, color,
                LimeZuVisualCounter.Landmark, rotationDegrees);
        }

        private GameObject CreateLimeZuTaskEventFeedbackChild(Transform parent, string objectName, Sprite sprite,
            string resourcePath, Vector3 localPosition, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            return CreateLimeZuChild(parent, objectName, sprite, resourcePath, localPosition, scale, color,
                LimeZuVisualCounter.TaskEventFeedback, rotationDegrees);
        }

        private GameObject CreateLimeZuRoomProp(string propName, Sprite sprite, string resourcePath,
            Vector3 position, Vector3 scale, Color color, float rotationDegrees = 0f, bool solid = false)
        {
            return CreateLimeZuProp(propName, sprite, resourcePath, position, scale, color,
                LimeZuVisualCounter.RoomProp, rotationDegrees, solid);
        }

        private GameObject CreateLimeZuRoomPropAt(string propName, Sprite sprite, string resourcePath,
            Vector3 basePosition, Vector3 offset, Vector3 scale, float rotationDegrees = 0f, bool solid = false)
        {
            return CreateLimeZuRoomProp(propName, sprite, resourcePath, basePosition + offset,
                scale, Color.white, rotationDegrees, solid);
        }

        private GameObject CreateRuntimeMapProp(string propName, Sprite sprite, string resourcePath, Vector3 position,
            Vector3 scale, Color color, float rotationDegrees = 0f, bool solid = false)
        {
            GameObject prop = CreateShapeProp(propName, sprite, position, scale, color);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            SetSortingFromZ(prop);

            if (solid)
            {
                RegisterSolidObstacle(position, scale);
                AttachPhysicsCollider(prop, scale, false);
            }

            CountRuntimeMapPropUse(resourcePath);
            return prop;
        }

        public static Sprite CreateRoundedRectSprite(string spriteName, int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color fill = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0, x - (size - radius - 1));
                    float dy = Mathf.Max(radius - y, 0, y - (size - radius - 1));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    texture.SetPixel(x, y, distance <= radius ? fill : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite CreateCircleSprite(string spriteName, int size, bool softEdge)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = softEdge
                        ? Mathf.Clamp01((radius - distance) / (radius * 0.18f))
                        : distance <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite CreateDiamondSprite(string spriteName, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            Color clear = new Color(1f, 1f, 1f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float manhattan = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    texture.SetPixel(x, y, manhattan <= center ? Color.white : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ====================================================================
        //  PRIMITIVE BUILDING BLOCKS
        // ====================================================================

        public GameObject CreatePrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color)
        {
            GameObject prop = primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere
                ? CreateSpriteObject(propName, _circleSprite, color)
                : CreateSpriteObject(propName, _roundedRectSprite, color);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSolidPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color)
        {
            GameObject prop = CreatePrimitiveProp(propName, primitiveType, position, scale, color);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale,
                primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere);
            return prop;
        }

        /// <summary>
        /// Create a tiled floor by repeating a sprite in a grid pattern.
        /// Avoids the 32px→24m stretch blur. Each tile is ~1m×1m.
        /// </summary>
        public void CreateTiledFloor(string name, Vector3 position, Vector2 size, Color tint)
        {
            Sprite tile = FloorTileSprite;
            float tileWorldSize = 1.0f;
            int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / tileWorldSize));
            int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / tileWorldSize));
            float stepX = size.x / cols;
            float stepY = size.y / rows;
            Vector3 start = position - new Vector3(size.x * 0.5f, size.y * 0.5f, 0f) + new Vector3(stepX * 0.5f, stepY * 0.5f, 0f);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    Vector3 pos = start + new Vector3(col * stepX, row * stepY, position.z);
                    Vector3 scale = new Vector3(stepX, stepY, 0.06f);
                    var t = CreateShapeProp($"{name}_tile_{col}_{row}", tile, pos, scale, tint);
                    t.transform.SetSiblingIndex(0);
                }
            }
        }

        public GameObject CreateProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateSpriteObject(propName, _roundedRectSprite, color);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSolidProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateProp(propName, position, scale, color);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale, false);
            return prop;
        }

        public GameObject CreateShapeProp(string propName, Sprite sprite, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateSpriteObject(propName, sprite, color);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateRotatedProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees)
        {
            GameObject prop = CreateProp(propName, position, scale, color);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees = 0f)
        {
            // M3: Delegate to sprite path when 2D backend is active
            if (Use2DBackend)
                return CreateRotatedProp(propName, position, scale, color, rotationDegrees);

            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSolidMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees = 0f)
        {
            // M3: Delegate to sprite path when 2D backend is active
            if (Use2DBackend)
                return CreateSolidProp(propName, position, scale, color);

            GameObject prop = CreateMeshBoxProp(propName, position, scale, color, rotationDegrees);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale, false);
            return prop;
        }

        public GameObject CreateMeshBoxChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale,
            Color color, float rotationDegrees = 0f)
        {
            // M3: Delegate to sprite child path when 2D backend is active
            if (Use2DBackend)
                return CreatePropChild(parent, propName, localPosition, scale, color, PrimitiveType.Cube);

            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            prop.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateMeshPrimitiveChild(Transform parent, string propName, PrimitiveType primitiveType,
            Vector3 localPosition, Vector3 scale, Color color, Quaternion localRotation)
        {
            // M3: Delegate to sprite child path when 2D backend is active
            if (Use2DBackend)
                return CreatePropChild(parent, propName, localPosition, scale, color, primitiveType);

            GameObject prop = GameObject.CreatePrimitive(primitiveType);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            prop.transform.localRotation = localRotation;
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateMeshPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color, Quaternion rotation)
        {
            // M3: Delegate to sprite path when 2D backend is active
            if (Use2DBackend)
                return CreatePrimitiveProp(propName, primitiveType, position, scale, color);

            GameObject prop = GameObject.CreatePrimitive(primitiveType);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            prop.transform.rotation = rotation;
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreatePropChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale,
            Color color, PrimitiveType primitiveType)
        {
            GameObject prop = primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere
                ? CreateSpriteObject(propName, _circleSprite, color)
                : CreateSpriteObject(propName, _roundedRectSprite, color);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSpriteChild(Transform parent, string objectName, Sprite sprite, Vector3 localPosition,
            Vector3 scale, Color color)
        {
            GameObject child = CreateSpriteObject(objectName, sprite, color);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = scale;
            SetSortingFromZ(child);
            return child;
        }

        // ====================================================================
        //  3D HELPER (shared)
        // ====================================================================

        public static void Remove3DCollider(GameObject prop)
        {
            Collider collider = prop.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) UnityEngine.Object.DestroyImmediate(collider);
            else UnityEngine.Object.DestroyImmediate(collider);
        }

        public void ConfigureRuntimeMesh(GameObject prop, Color color)
        {
            Renderer renderer = prop.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = RuntimeMeshMaterial(color);
        }

        public Material RuntimeMeshMaterial(Color color)
        {
            string key = Mathf.RoundToInt(color.r * 255f) + "-" +
                         Mathf.RoundToInt(color.g * 255f) + "-" +
                         Mathf.RoundToInt(color.b * 255f) + "-" +
                         Mathf.RoundToInt(color.a * 255f);
            if (_runtimeMeshMaterials.TryGetValue(key, out Material cached)) return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ??
                            Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (color.a < 0.99f) ConfigureTransparentMaterial(material);

            _runtimeMeshMaterials[key] = material;
            return material;
        }

        public static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null) return;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        }

        // ====================================================================
        //  COLLISION
        // ====================================================================

        public void RegisterSolidObstacle(Vector3 position, Vector3 scale)
        {
            Vector3 scaledPos = _mapService.ScaleMapPosition(position);
            Vector3 scaledScale = _mapService.ScaleMapSize(scale);
            float w = Mathf.Max(0.01f, Mathf.Abs(scaledScale.x));
            float h = Mathf.Max(0.01f, Mathf.Abs(scaledScale.y));
            _solidObstacleRects.Add(new Rect(scaledPos.x - w * 0.5f, scaledPos.y - h * 0.5f, w, h));
        }

        public void RegisterWalkableArea(Vector3 position, Vector3 scale)
        {
            Vector3 scaledPos = _mapService.ScaleMapPosition(position);
            Vector3 scaledScale = _mapService.ScaleMapSize(scale);
            float w = Mathf.Max(0.01f, Mathf.Abs(scaledScale.x));
            float h = Mathf.Max(0.01f, Mathf.Abs(scaledScale.y));
            _walkableRects.Add(new Rect(scaledPos.x - w * 0.5f, scaledPos.y - h * 0.5f, w, h));
        }

        public static void AttachPhysicsCollider(GameObject prop, Vector3 designScale, bool round)
        {
            if (prop == null) return;
            Remove3DCollider(prop);
            Rigidbody2D body = prop.GetComponent<Rigidbody2D>();
            if (body == null) body = prop.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            if (round)
            {
                CircleCollider2D circle = prop.GetComponent<CircleCollider2D>();
                if (circle == null) circle = prop.AddComponent<CircleCollider2D>();
                circle.radius = 0.5f;
                circle.isTrigger = false;
                return;
            }

            BoxCollider2D box = prop.GetComponent<BoxCollider2D>();
            if (box == null) box = prop.AddComponent<BoxCollider2D>();
            float width = Mathf.Abs(designScale.x) <= 0.18f ? 0.82f : 1f;
            float height = Mathf.Abs(designScale.y) <= 0.18f ? 0.82f : 1f;
            box.size = new Vector2(width, height);
            box.isTrigger = false;
        }

        // ====================================================================
        //  COLOR HELPERS
        // ====================================================================

        public static Color Darken(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        public static Color FallbackColorForModel(string relativeFbxPath)
        {
            if (relativeFbxPath.IndexOf("Light", StringComparison.Ordinal) >= 0)
                return new Color(0.08f, 0.78f, 0.92f, 1f);
            if (relativeFbxPath.IndexOf("Crate", StringComparison.Ordinal) >= 0 ||
                relativeFbxPath.IndexOf("Chest", StringComparison.Ordinal) >= 0)
                return new Color(0.72f, 0.52f, 0.14f, 1f);
            if (relativeFbxPath.IndexOf("Door", StringComparison.Ordinal) >= 0)
                return new Color(0.42f, 0.48f, 0.5f, 1f);
            if (relativeFbxPath.IndexOf("Computer", StringComparison.Ordinal) >= 0 ||
                relativeFbxPath.IndexOf("AccessPoint", StringComparison.Ordinal) >= 0)
                return new Color(0.08f, 0.32f, 0.42f, 1f);
            return new Color(0.24f, 0.28f, 0.3f, 1f);
        }

        // ====================================================================
        //  MODEL LOADING
        // ====================================================================

        private const string QuaterniusFbxRoot = "Assets/_Project/Art/ThirdParty/Quaternius/ModularSciFiMegaKit/FBX/";
        private const string RuntimeResourcesRoot = "Assets/_Project/Resources/";

        public GameObject CreateAssetStoreProp(string propName, string resourcePath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false,
            bool preserveMaterials = true)
        {
            // M3: Skip 3D model loading entirely when 2D backend is active
            if (Use2DBackend)
                return CreateModelFallbackProp(propName + " (2D)", position, footprint, rotationDegrees,
                    FallbackColorForModel(resourcePath));

            GameObject prefab = LoadResourcePrefab(resourcePath);
            if (prefab == null || _worldRoot == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(resourcePath));

            GameObject model = InstantiateModelPrefab(prefab);
            if (model == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(resourcePath));

            model.name = propName;
            model.transform.SetParent(_worldRoot.transform, false);
            model.transform.position = _mapService.ScaleMapPosition(position);
            model.transform.rotation =
                Quaternion.Euler(0f, 0f, rotationDegrees) * Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            FitModelToFootprint(model, _mapService.ScaleMapPosition(position), footprint, stretchToFootprint);
            ConfigureModelRenderers(model, preserveMaterials);
            SetSortingFromZ(model);
            return model;
        }

        public GameObject CreateSolidAssetStoreProp(string propName, string resourcePath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false,
            bool preserveMaterials = true)
        {
            GameObject model = CreateAssetStoreProp(propName, resourcePath, position, footprint, rotationDegrees,
                stretchToFootprint, preserveMaterials);
            if (model != null)
            {
                RegisterSolidObstacle(position, footprint);
                AttachPhysicsCollider(model, footprint, false);
            }

            return model;
        }

        public GameObject CreateModelProp(string propName, string relativeFbxPath, Vector3 position, Vector3 footprint,
            float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            // M3: Skip 3D model loading entirely when 2D backend is active
            if (Use2DBackend)
                return CreateModelFallbackProp(propName + " (2D)", position, footprint, rotationDegrees,
                    FallbackColorForModel(relativeFbxPath));

            GameObject prefab = LoadQuaterniusModel(relativeFbxPath);
            if (prefab == null || _worldRoot == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(relativeFbxPath));

            GameObject model = InstantiateModelPrefab(prefab);
            if (model == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(relativeFbxPath));

            model.name = propName;
            model.transform.SetParent(_worldRoot.transform, false);
            model.transform.position = _mapService.ScaleMapPosition(position);
            model.transform.rotation =
                Quaternion.Euler(0f, 0f, rotationDegrees) * Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            FitModelToFootprint(model, _mapService.ScaleMapPosition(position), footprint, stretchToFootprint);
            ConfigureModelRenderers(model, false);
            SetSortingFromZ(model);
            return model;
        }

        public GameObject CreateSolidModelProp(string propName, string relativeFbxPath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            GameObject model = CreateModelProp(propName, relativeFbxPath, position, footprint, rotationDegrees,
                stretchToFootprint);
            if (model != null)
            {
                RegisterSolidObstacle(position, footprint);
                AttachPhysicsCollider(model, footprint, false);
            }

            return model;
        }

        public void CreateWallModelOverlay(string wallName, Vector3 position, Vector3 scale)
        {
            if (Mathf.Abs(scale.x) < 0.2f && Mathf.Abs(scale.y) < 0.2f) return;
            bool horizontal = Mathf.Abs(scale.x) >= Mathf.Abs(scale.y);
            string modelPath = horizontal ? "Walls/ShortWall_Metal2_Straight.fbx" : "Walls/WallAstra_Straight.fbx";
            float rotation = horizontal ? 0f : 90f;
            Vector3 footprint = new Vector3(Mathf.Max(Mathf.Abs(scale.x), 0.18f), Mathf.Max(Mathf.Abs(scale.y), 0.18f),
                Mathf.Max(Mathf.Abs(scale.z), 0.16f));
            CreateModelProp(wallName + " CC0 Wall Module", modelPath,
                position + new Vector3(0f, 0f, 0.08f), footprint, rotation, true);
        }

        public void CreateDoorModelOverlay(string markerName, Vector3 position, Vector3 scale)
        {
            bool horizontal = Mathf.Abs(scale.x) >= Mathf.Abs(scale.y);
            string doorPath = markerName.Contains("黑市") || markerName.Contains("维修") || markerName.Contains("暗")
                ? "Platforms/Door_DarkMetal.fbx"
                : "Platforms/Door_Frame_A.fbx";
            float rotation = horizontal ? 0f : 90f;
            Vector3 footprint = horizontal
                ? new Vector3(Mathf.Max(0.72f, Mathf.Abs(scale.x)), 0.34f, 0.32f)
                : new Vector3(0.34f, Mathf.Max(0.72f, Mathf.Abs(scale.y)), 0.32f);
            CreateModelProp(markerName + " CC0 Door Frame", doorPath, position + new Vector3(0f, 0f, 0.1f), footprint,
                rotation, true);
        }

        public GameObject CreateModelFallbackProp(string propName, Vector3 position, Vector3 footprint,
            float rotationDegrees, Color color)
        {
            // M3: Use simple 2D sprite fallback instead of 3D mesh stack
            if (Use2DBackend)
                return CreateRotatedProp(propName, position, footprint, color, rotationDegrees);

            GameObject fallback = new GameObject(propName);
            fallback.transform.SetParent(_worldRoot.transform, false);
            fallback.transform.position = _mapService.ScaleMapPosition(position);
            fallback.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            Vector3 size = _mapService.ScaleMapSize(footprint);
            float width = Mathf.Max(0.08f, Mathf.Abs(size.x));
            float depth = Mathf.Max(0.08f, Mathf.Abs(size.y));
            float height = Mathf.Max(0.08f, Mathf.Abs(footprint.z));

            CreateMeshBoxChild(fallback.transform, "Fallback Base", new Vector3(0f, 0f, height * 0.35f),
                new Vector3(width, depth, height * 0.7f), Darken(color, 0.8f));
            CreateMeshBoxChild(fallback.transform, "Fallback Face",
                new Vector3(0f, depth * 0.38f, height * 0.76f),
                new Vector3(width * 0.72f, Mathf.Max(0.025f, depth * 0.08f), height * 0.26f), color);

            if (footprint.z > 0.18f)
            {
                CreateMeshBoxChild(fallback.transform, "Fallback Light Strip",
                    new Vector3(0f, -depth * 0.38f, height * 1.02f),
                    new Vector3(width * 0.52f, Mathf.Max(0.02f, depth * 0.08f), height * 0.12f),
                    new Color(0.08f, 0.78f, 0.92f, 1f));
            }

            SetSortingFromZ(fallback);
            return fallback;
        }

        public GameObject LoadQuaterniusModel(string relativeFbxPath)
        {
            string cacheKey = "Quaternius/" + relativeFbxPath;
            if (_modelPrefabCache.TryGetValue(cacheKey, out GameObject cached)) return cached;

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusFbxRoot + relativeFbxPath);
#endif
            if (prefab == null)
            {
                string resourcePath = "Quaternius/ModularSciFiMegaKit/FBX/" +
                                      relativeFbxPath.Replace(".fbx", string.Empty);
                prefab = Resources.Load<GameObject>(resourcePath);
            }

            _modelPrefabCache[cacheKey] = prefab;
            return prefab;
        }

        public GameObject LoadResourcePrefab(string resourcePath)
        {
            string normalized = NormalizeResourcePath(resourcePath);
            string cacheKey = "Resource/" + normalized;
            if (_modelPrefabCache.TryGetValue(cacheKey, out GameObject cached)) return cached;

            GameObject prefab = null;
#if UNITY_EDITOR
            string assetPath = RuntimeResourcesRoot + normalized + ".prefab";
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                assetPath = RuntimeResourcesRoot + normalized + ".fbx";
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
#endif
            if (prefab == null) prefab = Resources.Load<GameObject>(normalized);

            _modelPrefabCache[cacheKey] = prefab;
            return prefab;
        }

        public static string NormalizeResourcePath(string resourcePath)
        {
            string normalized = resourcePath.Replace('\\', '/').Trim();
            if (normalized.StartsWith("Assets/_Project/Resources/", StringComparison.Ordinal))
                normalized = normalized.Substring("Assets/_Project/Resources/".Length);
            if (normalized.StartsWith("/", StringComparison.Ordinal)) normalized = normalized.Substring(1);
            string extension = System.IO.Path.GetExtension(normalized);
            if (!string.IsNullOrEmpty(extension))
                normalized = normalized.Substring(0, normalized.Length - extension.Length);
            return normalized;
        }

        public static GameObject InstantiateModelPrefab(GameObject prefab)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
#endif
            return UnityEngine.Object.Instantiate(prefab);
        }

        public void FitModelToFootprint(GameObject model, Vector3 targetPosition, Vector3 footprint,
            bool stretchToFootprint)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds)) return;
            Vector3 desired = _mapService.ScaleMapSize(footprint);
            float desiredX = Mathf.Max(0.04f, Mathf.Abs(desired.x));
            float desiredY = Mathf.Max(0.04f, Mathf.Abs(desired.y));
            float desiredZ = Mathf.Max(0.05f, Mathf.Abs(footprint.z));

            if (stretchToFootprint)
            {
                Vector3 ls = model.transform.localScale;
                float xF = bounds.size.x > 0.001f ? desiredX / bounds.size.x : 1f;
                float yF = bounds.size.y > 0.001f ? desiredY / bounds.size.y : 1f;
                float zF = bounds.size.z > 0.001f ? desiredZ / bounds.size.z : Mathf.Min(xF, yF);
                model.transform.localScale = new Vector3(ls.x * xF, ls.y * zF, ls.z * yF);
            }
            else
            {
                float xF = bounds.size.x > 0.001f ? desiredX / bounds.size.x : 1f;
                float yF = bounds.size.y > 0.001f ? desiredY / bounds.size.y : 1f;
                float factor = Mathf.Clamp(Mathf.Min(xF, yF), 0.02f, 3.0f);
                model.transform.localScale *= factor;
            }
        }

        public static void AlignModelBounds(GameObject model, Vector3 targetPosition)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds)) return;
            Vector3 offset = new Vector3(targetPosition.x - bounds.center.x, targetPosition.y - bounds.center.y,
                targetPosition.z - bounds.min.z);
            model.transform.position += offset;
        }

        public static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(model.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            return hasBounds;
        }

        public static void ConfigureModelRenderers(GameObject model, bool preserveMaterials)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer.sharedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    renderer.sharedMaterial = new Material(shader);
                }

                Material material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
                if (material == null) continue;
                if (preserveMaterials)
                {
                    Color sourceColor = ReadMaterialColor(material, Color.white);
                    if (material.HasProperty("_BaseColor"))
                    {
                        Color bc = material.GetColor("_BaseColor");
                        material.SetColor("_BaseColor",
                            new Color(Mathf.Clamp01(bc.r * 1.04f), Mathf.Clamp01(bc.g * 1.04f),
                                Mathf.Clamp01(bc.b * 1.04f), bc.a));
                    }

                    SetMaterialColor(material,
                        new Color(Mathf.Clamp01(sourceColor.r * 1.04f), Mathf.Clamp01(sourceColor.g * 1.04f),
                            Mathf.Clamp01(sourceColor.b * 1.04f), sourceColor.a));
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    Color color = ReadMaterialColor(material, Color.white);
                    material.SetColor("_BaseColor",
                        new Color(Mathf.Clamp01(color.r * 1.1f), Mathf.Clamp01(color.g * 1.1f),
                            Mathf.Clamp01(color.b * 1.1f), color.a));
                }

                SetMaterialColor(material, new Color(0.92f, 0.94f, 0.98f, 1f));
            }
        }

        public static Color ReadMaterialColor(Material material, Color fallback)
        {
            if (material == null) return fallback;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return fallback;
        }

        public static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        // ====================================================================
        //  TASK VISUALS
        // ====================================================================

        private static Sprite LimeZuSetPieceSprite(int index)
        {
            Sprite2DAssetCache.Ensure();
            switch (Mathf.Abs(index) % 5)
            {
                case 0: return Sprite2DAssetCache.PropDesk;
                case 1: return Sprite2DAssetCache.PropCabinet;
                case 2: return Sprite2DAssetCache.PropEvidenceBox;
                case 3: return Sprite2DAssetCache.WallBlock;
                default: return Sprite2DAssetCache.FloorTileAlt;
            }
        }

        private static string LimeZuSetPieceResourcePath(int index)
        {
            Sprite2DAssetCache.Ensure();
            switch (Mathf.Abs(index) % 5)
            {
                case 0: return Sprite2DAssetCache.PropDeskResourcePath;
                case 1: return Sprite2DAssetCache.PropCabinetResourcePath;
                case 2: return Sprite2DAssetCache.PropEvidenceBoxResourcePath;
                case 3: return Sprite2DAssetCache.WallBlockResourcePath;
                default: return Sprite2DAssetCache.FloorTileAltResourcePath;
            }
        }

        private static Sprite TaskLimeZuSpriteForMode(int mode)
        {
            Sprite2DAssetCache.Ensure();
            switch (mode)
            {
                case 0: return Sprite2DAssetCache.PropCabinet;
                case 1: return Sprite2DAssetCache.PropDesk;
                case 2: return Sprite2DAssetCache.PropEvidenceBox;
                case 3: return Sprite2DAssetCache.PropDesk;
                case 4: return Sprite2DAssetCache.PropEvidenceBox;
                default: return Sprite2DAssetCache.PropCabinet;
            }
        }

        private static string TaskLimeZuResourcePathForMode(int mode)
        {
            Sprite2DAssetCache.Ensure();
            switch (mode)
            {
                case 0: return Sprite2DAssetCache.PropCabinetResourcePath;
                case 1: return Sprite2DAssetCache.PropDeskResourcePath;
                case 2: return Sprite2DAssetCache.PropEvidenceBoxResourcePath;
                case 3: return Sprite2DAssetCache.PropDeskResourcePath;
                case 4: return Sprite2DAssetCache.PropEvidenceBoxResourcePath;
                default: return Sprite2DAssetCache.PropCabinetResourcePath;
            }
        }

        /// <summary>RGB accent colour for each task-type template.</summary>
        public static Color TaskPanelAccent(int taskId)
        {
            int mode = taskId % 7;
            switch (mode)
            {
                case 0: return new Color(0.08f, 0.58f, 0.92f, 1f);
                case 1: return new Color(0.92f, 0.22f, 0.12f, 1f);
                case 2: return new Color(0.88f, 0.72f, 0.08f, 1f);
                case 3: return new Color(0.18f, 0.78f, 0.32f, 1f);
                case 4: return new Color(0.72f, 0.18f, 0.84f, 1f);
                case 5: return new Color(0.95f, 0.48f, 0.08f, 1f);
                default: return new Color(0.28f, 0.62f, 0.88f, 1f);
            }
        }

        public GameObject CreateTaskVisual(OnlineTaskState task, Transform parent)
        {
            if (parent == null) return null;
            string label = string.IsNullOrWhiteSpace(task.Name) ? TaskNameFor(task.Id) : task.Name;
            Sprite sprite = TaskVisualSprite(task.Id);
            Color color = TaskPanelAccent(task.Id);
            Vector3 pos = _mapService.ScaleMapPosition(task.Position);
            Vector3 scale = TaskScale(task.Id);

            GameObject root = new GameObject("任务视觉 " + label + " #" + task.Id);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.localScale = scale;

            string resourcePath = TaskVisualResourcePath(task.Id);
            GameObject body = CreateSpriteObject("LimeZu TaskStation body", sprite, Color.white);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one;
            CountLimeZuSpriteUse(resourcePath, LimeZuVisualCounter.TaskStation);
            SetSortingFromZ(root);

            CreateWorldLabel(root.transform, label, new Vector3(0f, 0.3f, 0.02f), 0.08f);
            CreateTaskEquipment(root, task);
            CreateTaskEventFeedbackLayer(root.transform, color);

            // M3: Interactive highlight halo — expands when player is near
            GameObject halo = CreateSpriteObject("交互光晕", _softCircleSprite, new Color(color.r, color.g, color.b, 0.12f));
            halo.transform.SetParent(root.transform, false);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
            SpriteRenderer haloRenderer = halo.GetComponent<SpriteRenderer>();
            if (haloRenderer != null) haloRenderer.sortingOrder = -1; // Behind task body

            return root;
        }

        private void CreateTaskEventFeedbackLayer(Transform parent, Color accent)
        {
            Sprite2DAssetCache.Ensure();

            GameObject damaged = new GameObject("破坏标记");
            damaged.transform.SetParent(parent, false);
            damaged.transform.localPosition = new Vector3(0f, 0f, 0.18f);
            damaged.transform.localScale = Vector3.one;

            CreateLimeZuTaskEventFeedbackChild(damaged.transform, "事件反馈 LimeZu 破坏设备盖板",
                Sprite2DAssetCache.LandmarkAirDuct, Sprite2DAssetCache.LandmarkAirDuctResourcePath,
                new Vector3(0f, 0.02f, 0.025f), new Vector3(0.42f, 0.22f, 0.035f),
                Color.white, 7f);
            CreateSpriteChild(damaged.transform, "事件反馈 破坏火花 A", _diamondSprite,
                new Vector3(-0.26f, 0.18f, 0.03f), new Vector3(0.16f, 0.16f, 0.04f),
                new Color(0.95f, 0.16f, 0.08f, 0.95f));
            CreateSpriteChild(damaged.transform, "事件反馈 破坏火花 B", _diamondSprite,
                new Vector3(0.24f, 0.14f, 0.04f), new Vector3(0.12f, 0.12f, 0.04f),
                new Color(1f, 0.64f, 0.08f, 0.9f));
            CreateSpriteChild(damaged.transform, "事件反馈 破坏烟雾", _softCircleSprite,
                new Vector3(0f, -0.08f, 0.02f), new Vector3(0.74f, 0.36f, 0.04f),
                new Color(0.08f, 0.08f, 0.08f, 0.46f));
            CreateLimeZuTaskEventFeedbackChild(parent, "事件反馈 LimeZu 现场证物包",
                Sprite2DAssetCache.LandmarkPackage, Sprite2DAssetCache.LandmarkPackageResourcePath,
                new Vector3(-0.36f, -0.25f, 0.055f), new Vector3(0.24f, 0.2f, 0.035f),
                Color.white, -8f);
            CreateSpriteChild(parent, "事件反馈 现场可疑足迹", _capsuleSprite,
                new Vector3(0.34f, -0.26f, 0.04f), new Vector3(0.22f, 0.055f, 0.03f),
                new Color(accent.r, accent.g, accent.b, 0.52f));

            damaged.SetActive(false);
        }

        public void SetTaskVisualState(GameObject visual, OnlineTaskState task)
        {
            if (visual == null) return;

            Color color = task.Completed
                ? new Color(0.12f, 0.38f, 0.14f, 0.7f)
                : task.Sabotaged
                    ? new Color(0.55f, 0.08f, 0.06f, 0.9f)
                    : TaskPanelAccent(task.Id);

            SpriteRenderer[] renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer r in renderers)
            {
                r.color = new Color(color.r, color.g, color.b, r.color.a);
            }

            Transform halo = visual.transform.Find("交互光晕");
            if (halo != null) halo.gameObject.SetActive(!task.Completed && !task.Sabotaged);

            Transform damaged = visual.transform.Find("破坏标记");
            if (damaged != null) damaged.gameObject.SetActive(task.Sabotaged);
        }

        public static Sprite TaskVisualSprite(int taskId)
        {
            return TaskLimeZuSpriteForMode(TaskTemplateMode(taskId));
        }

        public static string TaskVisualResourcePath(int taskId)
        {
            return TaskLimeZuResourcePathForMode(TaskTemplateMode(taskId));
        }

        private void CreateTaskEquipment(GameObject root, OnlineTaskState task)
        {
            if (root == null) return;
            CreateTaskEquipment(root.transform, task.Id);
        }

        public void CreateTaskEquipment(Transform parent, int taskId)
        {
            if (parent == null) return;
            Color accent = TaskPanelAccent(taskId);
            int mode = taskId % 7;
            string stationResourcePath = TaskVisualResourcePath(taskId);

            CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment asset base",
                TaskVisualSprite(taskId), stationResourcePath, new Vector3(0f, 0f, 0.035f),
                new Vector3(0.34f, 0.22f, 0.05f), Color.white);
            CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment asset trim",
                Sprite2DAssetCache.WallBlock, Sprite2DAssetCache.WallBlockResourcePath, new Vector3(0f, 0.12f, 0.055f),
                new Vector3(0.28f, 0.055f, 0.035f), new Color(accent.r, accent.g, accent.b, 0.9f));

            switch (mode)
            {
                case 0: // Wire / panel
                    CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment CCTV cabinet",
                        Sprite2DAssetCache.PropCabinet, Sprite2DAssetCache.PropCabinetResourcePath,
                        new Vector3(0f, 0.08f, 0.07f), new Vector3(0.2f, 0.11f, 0.035f), Color.white);
                    CreatePropChild(parent, "终端面板", new Vector3(0f, 0.06f, 0.04f),
                        new Vector3(0.28f, 0.14f, 0.08f), Darken(accent, 0.7f), PrimitiveType.Cube);
                    CreatePropChild(parent, "终端屏幕", new Vector3(0f, 0.08f, 0.06f),
                        new Vector3(0.18f, 0.08f, 0.04f), accent, PrimitiveType.Cube);
                    CreatePropChild(parent, "线缆L", new Vector3(-0.1f, -0.06f, 0.02f),
                        new Vector3(0.02f, 0.1f, 0.02f), new Color(0.82f, 0.22f, 0.22f, 1f), PrimitiveType.Cylinder);
                    CreatePropChild(parent, "线缆R", new Vector3(0.1f, -0.06f, 0.02f),
                        new Vector3(0.02f, 0.1f, 0.02f), new Color(0.22f, 0.22f, 0.82f, 1f), PrimitiveType.Cylinder);
                    break;
                case 1: // Keypad
                    CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment keypad desk",
                        Sprite2DAssetCache.PropDesk, Sprite2DAssetCache.PropDeskResourcePath,
                        new Vector3(0f, 0.02f, 0.07f), new Vector3(0.22f, 0.12f, 0.035f), Color.white);
                    CreatePropChild(parent, "键盘基座", new Vector3(0f, 0.02f, 0.04f),
                        new Vector3(0.22f, 0.12f, 0.06f), Darken(accent, 0.6f), PrimitiveType.Cube);
                    for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        CreatePropChild(parent, "键" + r + c,
                            new Vector3(-0.06f + c * 0.06f, 0.06f - r * 0.04f, 0.06f),
                            new Vector3(0.03f, 0.02f, 0.02f), accent, PrimitiveType.Cube);
                    break;
                case 2: // Scanner
                    CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment evidence scanner",
                        Sprite2DAssetCache.PropEvidenceBox, Sprite2DAssetCache.PropEvidenceBoxResourcePath,
                        new Vector3(0f, 0.03f, 0.07f), new Vector3(0.24f, 0.11f, 0.035f), Color.white);
                    CreatePropChild(parent, "扫描台", new Vector3(0f, 0.02f, 0.05f),
                        new Vector3(0.28f, 0.08f, 0.1f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "扫描线", new Vector3(0f, 0.06f, 0.08f),
                        new Vector3(0.04f, 0.04f, 0.02f), new Color(accent.r, accent.g, accent.b, 0.6f),
                        PrimitiveType.Cylinder);
                    break;
                case 3: // Download / screen
                    CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment office terminal",
                        Sprite2DAssetCache.PropDesk, Sprite2DAssetCache.PropDeskResourcePath,
                        new Vector3(0f, 0.03f, 0.07f), new Vector3(0.22f, 0.12f, 0.035f), Color.white);
                    CreatePropChild(parent, "显示器基座", new Vector3(0f, 0f, 0.04f),
                        new Vector3(0.12f, 0.06f, 0.08f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "显示器屏幕", new Vector3(0f, 0.02f, 0.08f),
                        new Vector3(0.18f, 0.1f, 0.02f), accent, PrimitiveType.Cube);
                    break;
                case 4: // Memory / card
                    CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment evidence tray",
                        Sprite2DAssetCache.PropEvidenceBox, Sprite2DAssetCache.PropEvidenceBoxResourcePath,
                        new Vector3(0f, 0.04f, 0.07f), new Vector3(0.18f, 0.12f, 0.035f), Color.white);
                    CreatePropChild(parent, "读卡器", new Vector3(0f, 0.02f, 0.05f),
                        new Vector3(0.1f, 0.06f, 0.04f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "卡片", new Vector3(0f, 0.06f, 0.06f),
                        new Vector3(0.06f, 0.04f, 0.01f), accent, PrimitiveType.Cube);
                    break;
                case 5: // Breaker / switch
                    CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment breaker cabinet",
                        Sprite2DAssetCache.PropCabinet, Sprite2DAssetCache.PropCabinetResourcePath,
                        new Vector3(0f, 0.05f, 0.07f), new Vector3(0.2f, 0.13f, 0.035f), Color.white);
                    CreatePropChild(parent, "开关面板", new Vector3(0f, 0.04f, 0.04f),
                        new Vector3(0.14f, 0.08f, 0.04f), Darken(accent, 0.6f), PrimitiveType.Cube);
                    CreatePropChild(parent, "开关杆", new Vector3(0f, 0.06f, 0.06f),
                        new Vector3(0.02f, 0.08f, 0.02f), accent, PrimitiveType.Cylinder);
                    break;
                default: // Evidence tray
                    CreateLimeZuTaskStationChild(parent, "LimeZu TaskStation equipment archive tray",
                        Sprite2DAssetCache.PropEvidenceBox, Sprite2DAssetCache.PropEvidenceBoxResourcePath,
                        new Vector3(0f, 0.04f, 0.07f), new Vector3(0.18f, 0.12f, 0.035f), Color.white);
                    CreatePropChild(parent, "证物盘", new Vector3(0f, 0.02f, 0.04f),
                        new Vector3(0.16f, 0.1f, 0.06f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "证物袋", new Vector3(0f, 0.04f, 0.05f),
                        new Vector3(0.08f, 0.06f, 0.03f), accent, PrimitiveType.Cube);
                    break;
            }
        }

        // ====================================================================
        //  PLAYER & BODY VISUALS
        // ====================================================================

        public GameObject CreatePlayerVisual(OnlinePlayerState state, bool isLocal)
        {
            string name = "玩家 " + state.DisplayName + " cid" + state.ClientId;
            GameObject root = new GameObject(name);
            root.transform.SetParent(_worldRoot.transform, false);
            root.transform.position = _mapService.ScaleMapPosition(state.Position);
            root.transform.localScale = Vector3.one * (state.Alive ? 1.12f : 1.04f);

            // B1: 角色脚下阴影 — anchors character to ground
            CreateSpriteChild(root.transform, "阴影", _softCircleSprite, new Vector3(0f, -0.12f, -0.06f),
                new Vector3(0.32f, 0.14f, 0.02f), new Color(0f, 0f, 0f, 0.35f));

            // Base body
            CreateSpriteChild(root.transform, "角色基座", _roundedRectSprite, Vector3.zero,
                new Vector3(0.28f, 0.24f, 0.12f), PlayerColor(state, isLocal));

            // Coat / uniform
            CreateSpriteChild(root.transform, "警服外套", _roundedRectSprite, new Vector3(0f, 0.04f, 0.02f),
                new Vector3(0.22f, 0.18f, 0.04f), Darken(PlayerColor(state, isLocal), 0.8f));

            // Helmet
            CreateSpriteChild(root.transform, "头盔", _circleSprite, new Vector3(0f, 0.12f, 0.04f),
                new Vector3(0.12f, 0.12f, 0.04f), PlayerAccentColor(state));

            CreateStageTwoCharacterRig(root, state);
            CreateStageTwoCharacterStateLayer(root);
            CreateProfessionAccessory(root, state);

            // Local ring
            CreateSpriteChild(root.transform, "本地指示圈", _circleSprite, new Vector3(0f, -0.04f, 0.01f),
                new Vector3(0.36f, 0.36f, 0.02f), new Color(0.08f, 0.72f, 0.95f, 0.62f));
            SetChildActive(root, "本地指示圈", isLocal && state.Alive);

            SetSortingFromZ(root);
            return root;
        }

        public GameObject CreateBodyVisual(OnlineBodyState body, Sprite characterSprite = null)
        {
            GameObject root = new GameObject("尸体 cid" + body.VictimClientId + " bid" + body.Id);
            root.transform.SetParent(_worldRoot.transform, false);
            root.transform.position = _mapService.ScaleMapPosition(body.Position);
            root.transform.localScale = new Vector3(1.04f, 0.52f, 0.08f);

            // Body base — use character sprite if available, otherwise red rectangle
            Sprite bodyBase = characterSprite ?? _roundedRectSprite;
            Color bodyColor = characterSprite != null ? Color.white : new Color(0.72f, 0.08f, 0.06f, 0.8f);
            CreateSpriteChild(root.transform, "尸体轮廓", bodyBase, Vector3.zero,
                new Vector3(0.26f, 0.16f, 0.06f), bodyColor);

            CreateSpriteChild(root.transform, "尸体标记", _diamondSprite, new Vector3(0f, 0.14f, 0.02f),
                new Vector3(0.08f, 0.08f, 0.02f), new Color(1f, 0.12f, 0.08f, 0.9f));

            CreateSpriteChild(root.transform, "Stage2 Downed body scene marker", _roundedRectSprite,
                new Vector3(0f, -0.02f, 0.03f), new Vector3(0.32f, 0.12f, 0.02f),
                new Color(1f, 1f, 1f, 0.34f));
            CreateSpriteChild(root.transform, "Stage2 Report body proximity prompt", _circleSprite,
                new Vector3(0f, 0.18f, 0.05f), new Vector3(0.16f, 0.16f, 0.02f),
                new Color(1f, 0.22f, 0.12f, 0.62f));
            CreateSpriteChild(root.transform, "Stage2 Forensic evidence tag", _diamondSprite,
                new Vector3(-0.12f, 0.08f, 0.04f), new Vector3(0.05f, 0.05f, 0.01f),
                new Color(1f, 0.86f, 0.18f, 0.9f));
            CreateSpriteChild(root.transform, "Stage2 Forensic chalk trace", _roundedRectSprite,
                new Vector3(0.1f, -0.07f, 0.04f), new Vector3(0.14f, 0.035f, 0.01f),
                new Color(1f, 1f, 1f, 0.42f));
            CreateSpriteChild(root.transform, "Stage2 Forensic sample vial", _circleSprite,
                new Vector3(0.15f, 0.08f, 0.04f), new Vector3(0.035f, 0.035f, 0.01f),
                new Color(0.16f, 0.72f, 0.95f, 0.86f));
            CreateSpriteChild(root.transform, "Stage2 Forensic scene boundary", _circleSprite,
                new Vector3(0f, 0f, 0.01f), new Vector3(0.46f, 0.32f, 0.01f),
                new Color(1f, 0.72f, 0.08f, 0.16f));
            CreateSpriteChild(root.transform, "Stage2 Kill VFX impact slash", _diamondSprite,
                new Vector3(-0.04f, 0.02f, 0.06f), new Vector3(0.18f, 0.05f, 0.01f),
                new Color(1f, 0.08f, 0.04f, 0.84f));
            CreateSpriteChild(root.transform, "Stage2 Kill VFX blood scatter", Sprite2DAssetCache.BloodSplatter,
                new Vector3(0.08f, -0.04f, 0.055f), new Vector3(0.18f, 0.18f, 0.01f),
                new Color(1f, 1f, 1f, 0.78f));
            CreateSpriteChild(root.transform, "Stage2 Kill VFX evidence package", Sprite2DAssetCache.LandmarkPackage,
                new Vector3(-0.18f, -0.02f, 0.055f), new Vector3(0.1f, 0.08f, 0.01f),
                Color.white);
            CreateSpriteChild(root.transform, "Stage2 Kill VFX witness light cone", _softCircleSprite,
                new Vector3(0f, 0.04f, 0.02f), new Vector3(0.38f, 0.18f, 0.01f),
                new Color(0.95f, 0.16f, 0.08f, 0.22f));

            SetSortingFromZ(root);
            return root;
        }

        public void CreateStageTwoCharacterRig(GameObject root)
        {
            if (root == null) return;
            StageTwoCharacterRig rig = root.GetComponent<StageTwoCharacterRig>();
            if (rig == null) rig = root.AddComponent<StageTwoCharacterRig>();
        }

        public void CreateStageTwoCharacterRig(GameObject root, OnlinePlayerState state)
        {
            if (root == null) return;

            StageTwoCharacterRig rig = root.GetComponent<StageTwoCharacterRig>();
            if (rig == null) rig = root.AddComponent<StageTwoCharacterRig>();

            rig.Configure("online-" + state.ClientId, "runtime/online-player/" + state.Profession);
            rig.BodyRoot = EnsureRigPart(root.transform, "BodyRoot", "警服外套",
                _roundedRectSprite, new Vector3(0f, 0.04f, 0.05f), new Vector3(0.22f, 0.18f, 0.04f),
                Darken(PlayerColor(state, false), 0.78f));
            rig.HeadRoot = EnsureRigPart(root.transform, "HeadRoot", "头盔",
                _circleSprite, new Vector3(0f, 0.2f, 0.07f), new Vector3(0.13f, 0.13f, 0.04f),
                PlayerAccentColor(state));
            rig.LeftArm = EnsureRigPart(root.transform, "LeftArm", null,
                _roundedRectSprite, new Vector3(-0.16f, 0.02f, 0.06f), new Vector3(0.05f, 0.14f, 0.03f),
                PlayerAccentColor(state));
            rig.RightArm = EnsureRigPart(root.transform, "RightArm", null,
                _roundedRectSprite, new Vector3(0.16f, 0.02f, 0.06f), new Vector3(0.05f, 0.14f, 0.03f),
                PlayerAccentColor(state));
            rig.LeftFoot = EnsureRigPart(root.transform, "LeftFoot", null,
                _circleSprite, new Vector3(-0.08f, -0.14f, 0.04f), new Vector3(0.05f, 0.04f, 0.02f),
                Darken(PlayerAccentColor(state), 0.62f));
            rig.RightFoot = EnsureRigPart(root.transform, "RightFoot", null,
                _circleSprite, new Vector3(0.08f, -0.14f, 0.04f), new Vector3(0.05f, 0.04f, 0.02f),
                Darken(PlayerAccentColor(state), 0.62f));
            rig.StateRoot = EnsureStateRoot(root.transform);
            rig.ApplyState(StageTwoCharacterVisualState.Idle);
        }

        private Transform EnsureRigPart(Transform parent, string rigName, string existingName, Sprite sprite,
            Vector3 localPosition, Vector3 scale, Color color)
        {
            Transform part = parent.Find(rigName);
            if (part == null && !string.IsNullOrEmpty(existingName))
            {
                part = parent.Find(existingName);
            }

            if (part == null)
            {
                part = CreateSpriteChild(parent, rigName, sprite, localPosition, scale, color).transform;
            }
            else
            {
                part.name = rigName;
            }

            return part;
        }

        private static Transform EnsureStateRoot(Transform parent)
        {
            Transform stateRoot = parent.Find("StateRoot");
            if (stateRoot != null)
            {
                return stateRoot;
            }

            GameObject stateRootObject = new GameObject("StateRoot");
            stateRootObject.transform.SetParent(parent, false);
            return stateRootObject.transform;
        }

        public void CreateStageTwoCharacterStateLayer(GameObject root)
        {
            if (root == null) return;
            CreateSpriteChild(root.transform, "Stage2 Character interaction radius", _circleSprite,
                new Vector3(0f, 0f, 0.02f), new Vector3(0.82f, 0.82f, 0.02f),
                new Color(0.2f, 0.8f, 0.2f, 0.15f));
            SetChildActive(root, "Stage2 Character interaction radius", false);

            CreateSpriteChild(root.transform, "Stage2 VoiceRadius action proximity", _circleSprite,
                new Vector3(0f, 0f, 0.01f), new Vector3(2.35f, 1.36f, 0.02f),
                new Color(0.3f, 0.3f, 0.9f, 0.08f));
            SetChildActive(root, "Stage2 VoiceRadius action proximity", false);

            CreateSpriteChild(root.transform, "Stage2 Downed chalk silhouette", _roundedRectSprite,
                new Vector3(0f, -0.02f, 0.01f), new Vector3(0.3f, 0.12f, 0.02f),
                new Color(1f, 1f, 1f, 0.25f));
            SetChildActive(root, "Stage2 Downed chalk silhouette", false);

            CreateSpriteChild(root.transform, "Stage2 Downed personal item", _circleSprite,
                new Vector3(0.06f, 0.02f, 0.02f), new Vector3(0.04f, 0.04f, 0.02f),
                new Color(0.8f, 0.3f, 0.1f, 0.6f));
            SetChildActive(root, "Stage2 Downed personal item", false);

            CreateSpriteChild(root.transform, "Stage2 Character facing wedge", _roundedRectSprite,
                new Vector3(0f, 0.12f, 0.03f), new Vector3(0.06f, 0.04f, 0.02f),
                new Color(1f, 1f, 1f, 0.3f));
            SetChildActive(root, "Stage2 Character facing wedge", true);

            CreateSpriteChild(root.transform, "Stage2 Character action hand prop", _circleSprite,
                new Vector3(0.08f, 0.04f, 0.04f), new Vector3(0.03f, 0.03f, 0.02f),
                new Color(0.9f, 0.7f, 0.1f, 0.8f));
            SetChildActive(root, "Stage2 Character action hand prop", false);

            CreateSpriteChild(root.transform, "Stage2 Character report beacon", _circleSprite,
                new Vector3(0f, 0.2f, 0.05f), new Vector3(0.08f, 0.08f, 0.02f),
                new Color(1f, 0.2f, 0.1f, 0.7f));
            SetChildActive(root, "Stage2 Character report beacon", false);

            CreateSpriteChild(root.transform, "Stage2 Report proximity ping", _circleSprite,
                new Vector3(0f, 0f, 0.03f), new Vector3(0.5f, 0.5f, 0.02f),
                new Color(1f, 0.2f, 0.1f, 0.12f));
            SetChildActive(root, "Stage2 Report proximity ping", false);

            CreateSpriteChild(root.transform, "Stage2 Meeting seated pad", _roundedRectSprite,
                new Vector3(0f, 0.04f, 0.01f), new Vector3(0.2f, 0.14f, 0.02f),
                new Color(0.2f, 0.3f, 0.4f, 0.5f));
            SetChildActive(root, "Stage2 Meeting seated pad", false);

            CreateSpriteChild(root.transform, "Stage2 Meeting vote tablet", _roundedRectSprite,
                new Vector3(0.06f, 0.08f, 0.03f), new Vector3(0.05f, 0.04f, 0.01f),
                new Color(0.1f, 0.6f, 0.9f, 0.8f));
            SetChildActive(root, "Stage2 Meeting vote tablet", false);

            CreateSpriteChild(root.transform, "Stage2 Meeting voice mic", _circleSprite,
                new Vector3(0.04f, 0.08f, 0.03f), new Vector3(0.02f, 0.02f, 0.01f),
                new Color(0.8f, 0.1f, 0.1f, 0.8f));
            SetChildActive(root, "Stage2 Meeting voice mic", false);

            CreateSpriteChild(root.transform, "Stage2 Vote locked marker", _circleSprite,
                new Vector3(0.08f, 0.08f, 0.04f), new Vector3(0.04f, 0.04f, 0.01f),
                new Color(1f, 0.6f, 0.1f, 0.9f));
            SetChildActive(root, "Stage2 Vote locked marker", false);

            CreateSpriteChild(root.transform, "Stage2 Character footstep L", _circleSprite,
                new Vector3(-0.06f, -0.12f, 0.01f), new Vector3(0.03f, 0.03f, 0.01f),
                new Color(0.3f, 0.3f, 0.3f, 0.3f));
            SetChildActive(root, "Stage2 Character footstep L", false);

            CreateSpriteChild(root.transform, "Stage2 Character footstep R", _circleSprite,
                new Vector3(0.06f, -0.12f, 0.01f), new Vector3(0.03f, 0.03f, 0.01f),
                new Color(0.3f, 0.3f, 0.3f, 0.3f));
            SetChildActive(root, "Stage2 Character footstep R", false);
        }

        public void CreateStageTwoCharacterStateLayer(Transform parent, OnlinePlayerState state)
        {
            CreateStageTwoCharacterStateLayer(parent == null ? null : parent.gameObject);
        }

        public void CreateProfessionAccessory(GameObject root, OnlinePlayerState state)
        {
            if (root == null) return;
            Color accent = PlayerAccentColor(state);

            switch (state.Profession)
            {
                case OnlineProfession.Inspector:
                    CreateSpriteChild(root.transform, "警徽", _diamondSprite, new Vector3(0.06f, 0.14f, 0.05f),
                        new Vector3(0.04f, 0.04f, 0.01f), new Color(1f, 0.84f, 0.1f, 1f));
                    break;
                case OnlineProfession.Forensics:
                    CreateSpriteChild(root.transform, "证物袋", _roundedRectSprite, new Vector3(0.08f, 0.06f, 0.04f),
                        new Vector3(0.04f, 0.03f, 0.01f), accent);
                    break;
                case OnlineProfession.Tech:
                    CreateSpriteChild(root.transform, "平板", _roundedRectSprite, new Vector3(0.08f, 0.06f, 0.04f),
                        new Vector3(0.05f, 0.03f, 0.01f), new Color(0.1f, 0.6f, 0.9f, 1f));
                    break;
                case OnlineProfession.Enforcer:
                    CreateSpriteChild(root.transform, "指虎", _circleSprite, new Vector3(0.08f, -0.04f, 0.04f),
                        new Vector3(0.03f, 0.03f, 0.01f), new Color(0.6f, 0.1f, 0.1f, 1f));
                    break;
                case OnlineProfession.Fixer:
                    CreateSpriteChild(root.transform, "工具包", _roundedRectSprite, new Vector3(0.08f, 0.04f, 0.04f),
                        new Vector3(0.04f, 0.03f, 0.01f), new Color(0.5f, 0.3f, 0.1f, 1f));
                    break;
                case OnlineProfession.UndercoverAgent:
                    CreateSpriteChild(root.transform, "线人笔记", _roundedRectSprite,
                        new Vector3(0.06f, 0.08f, 0.04f), new Vector3(0.03f, 0.04f, 0.01f),
                        new Color(0.4f, 0.2f, 0.6f, 1f));
                    break;
            }
        }

        public void CreateProfessionAccessory(Transform parent, OnlinePlayerState state)
        {
            CreateProfessionAccessory(parent == null ? null : parent.gameObject, state);
        }

        // ====================================================================
        //  PLAYER COLOR HELPERS
        // ====================================================================

        public static Color PlayerColor(OnlinePlayerState state, bool isLocal)
        {
            if (isLocal) return new Color(0.12f, 0.78f, 0.32f, 1f);
            return state.Profession switch
            {
                OnlineProfession.Inspector => new Color(0.15f, 0.35f, 0.7f, 1f),
                OnlineProfession.Forensics => new Color(0.1f, 0.5f, 0.5f, 1f),
                OnlineProfession.Tech => new Color(0.2f, 0.4f, 0.7f, 1f),
                OnlineProfession.Enforcer => new Color(0.7f, 0.15f, 0.15f, 1f),
                OnlineProfession.Fixer => new Color(0.6f, 0.4f, 0.15f, 1f),
                OnlineProfession.UndercoverAgent => new Color(0.5f, 0.3f, 0.6f, 1f),
                OnlineProfession.Driver => new Color(0.3f, 0.3f, 0.3f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
        }

        public static Color PlayerAccentColor(OnlinePlayerState state)
        {
            return state.Profession switch
            {
                OnlineProfession.Inspector => new Color(0.9f, 0.8f, 0.2f, 1f),
                OnlineProfession.Forensics => new Color(0.1f, 0.7f, 0.8f, 1f),
                OnlineProfession.Tech => new Color(0.2f, 0.6f, 0.9f, 1f),
                OnlineProfession.Enforcer => new Color(0.8f, 0.1f, 0.1f, 1f),
                OnlineProfession.Fixer => new Color(0.7f, 0.5f, 0.1f, 1f),
                OnlineProfession.UndercoverAgent => new Color(0.6f, 0.3f, 0.7f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
        }

        // ====================================================================
        //  LABELS
        // ====================================================================

        public TextMesh CreateWorldLabelAt(string text, Vector3 position, float characterSize)
        {
            GameObject labelObject = new GameObject("Label " + text);
            labelObject.transform.SetParent(_worldRoot.transform, false);
            labelObject.transform.position = position;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = new Color(0.72f, 0.78f, 0.72f, 0.96f);
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = SortingOrderForZ(position.z) + 20;
            if (Camera.main != null) BillboardLabel(labelObject.transform);
            _worldLabels.Add(label);
            return label;
        }

        public TextMesh CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float characterSize)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = new Color(0.88f, 0.92f, 0.88f, 1f);
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 900;
            if (Camera.main != null) BillboardLabel(labelObject.transform);
            return label;
        }

        public static void BillboardLabel(Transform labelTransform)
        {
            if (labelTransform == null || Camera.main == null) return;
            Vector3 direction = Camera.main.transform.position - labelTransform.position;
            if (direction.sqrMagnitude <= 0.0001f) return;
            labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Camera.main.transform.up);
        }

        public string BuildPlayerWorldLabel(OnlinePlayerState state, bool isLocal)
        {
            if (!state.Alive) return "出局";
            if (isLocal) return "你\n" + ProfessionName(state.Profession);
            return state.DisplayName.Length > 4 ? state.DisplayName.Substring(0, 4) : state.DisplayName;
        }

        public bool ShouldShowPlayerWorldLabel(OnlinePlayerState state, bool isLocal, OnlineMatchPhase phase,
            bool tacticalMapOpen)
        {
            if (phase == OnlineMatchPhase.Action)
            {
                if (tacticalMapOpen) return true;
                return isLocal || !state.Alive;
            }

            return true;
        }

        private static string ProfessionName(OnlineProfession p)
        {
            return p switch
            {
                OnlineProfession.Inspector => "督察",
                OnlineProfession.Forensics => "法证",
                OnlineProfession.Tech => "技术",
                OnlineProfession.Enforcer => "打手",
                OnlineProfession.Fixer => "善后",
                OnlineProfession.UndercoverAgent => "卧底",
                OnlineProfession.Driver => "车手",
                _ => "未知"
            };
        }

        // ====================================================================
        //  SORTING
        // ====================================================================

        public static void SetSortingFromZ(GameObject target)
        {
            int sortingOrder = SortingOrderForZ(target.transform.position.z);
            foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sortingOrder = sortingOrder;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
                if (renderer is not SpriteRenderer)
                    renderer.sortingOrder = sortingOrder;
        }

        public static int SortingOrderForZ(float z)
        {
            return Mathf.RoundToInt(-z * 1000f);
        }

        public static int SortingOrderForLocalZ(float localZ)
        {
            return Mathf.RoundToInt(-localZ * 1000f);
        }

        // ====================================================================
        //  SHARED HELPERS
        // ====================================================================

        public static void SetTextMeshVisible(TextMesh label, bool visible)
        {
            MeshRenderer renderer = label == null ? null : label.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = visible;
        }

        public static void SetColor(GameObject target, Color color)
        {
            SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) { spriteRenderer.color = color; return; }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
                if (material == null)
                {
                    material = new Material(
                        Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    if (Application.isPlaying) renderer.material = material;
                    else renderer.sharedMaterial = material;
                }

                material.color = color;
            }
        }

        public static void SetPlayerVisualColors(GameObject visual, OnlinePlayerState state, bool isLocal)
        {
            SpriteRenderer rootRenderer = visual.GetComponent<SpriteRenderer>();
            if (rootRenderer != null) rootRenderer.color = new Color(1f, 1f, 1f, 0f);

            Transform coat = visual.transform.Find("Coat");
            if (coat != null) SetColor(coat.gameObject, PlayerColor(state, isLocal));
            Transform bodyVol = visual.transform.Find("Body Volume");
            if (bodyVol != null) SetColor(bodyVol.gameObject, PlayerColor(state, isLocal));
            Transform helmet = visual.transform.Find("Helmet Volume");
            if (helmet != null) SetColor(helmet.gameObject, PlayerColor(state, isLocal));
            Transform pack = visual.transform.Find("Pack Volume");
            Transform localRing = visual.transform.Find("Local Ring");
            Transform localArrow = visual.transform.Find("Local Arrow");

            Color accent = PlayerAccentColor(state);
            Transform torso = visual.transform.Find("Torso");
            Transform armL = visual.transform.Find("Arm L");
            Transform armR = visual.transform.Find("Arm R");
            if (torso != null) SetColor(torso.gameObject, accent);
            if (armL != null) SetColor(armL.gameObject, accent);
            if (armR != null) SetColor(armR.gameObject, accent);
            if (pack != null) SetColor(pack.gameObject, Darken(accent, 0.7f));
            if (localRing != null)
            {
                localRing.gameObject.SetActive(isLocal && state.Alive);
                SetColor(localRing.gameObject, new Color(0.08f, 0.72f, 0.95f, 0.62f));
            }

            if (localArrow != null)
            {
                localArrow.gameObject.SetActive(isLocal && state.Alive);
                SetColor(localArrow.gameObject, new Color(0.95f, 0.82f, 0.12f, 1f));
            }
        }

        public static Transform FindChildTransform(Transform root, params string[] names)
        {
            if (root == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = root.Find(names[i]);
                if (found != null) return found;
            }

            return null;
        }

        public static void SetChildActive(GameObject root, string childName, bool active)
        {
            Transform child = root == null ? null : root.transform.Find(childName);
            if (child != null && child.gameObject.activeSelf != active) child.gameObject.SetActive(active);
        }

        // ====================================================================
        //  TASK SCALE / NAMES
        // ====================================================================

        public static Vector3 TaskScale(int taskId)
        {
            return (taskId % 7) switch
            {
                0 => new Vector3(0.42f, 0.24f, 0.22f),
                1 => new Vector3(0.32f, 0.26f, 0.18f),
                2 => new Vector3(0.46f, 0.18f, 0.24f),
                3 => new Vector3(0.38f, 0.3f, 0.16f),
                4 => new Vector3(0.28f, 0.2f, 0.2f),
                5 => new Vector3(0.34f, 0.22f, 0.18f),
                _ => new Vector3(0.36f, 0.24f, 0.2f),
            };
        }

        public static string TaskNameFor(int taskId)
        {
            return (taskId % 7) switch
            {
                0 => "线缆拼接",
                1 => "密码键盘",
                2 => "扫描验证",
                3 => "数据下载",
                4 => "记忆卡牌",
                5 => "断路器",
                _ => "证物整理",
            };
        }

        public static string TaskDistrictName(int taskId)
        {
            return (taskId / 7) switch
            {
                0 => "码头区",
                1 => "海关楼",
                2 => "监控室",
                3 => "茶餐厅",
                4 => "夜市",
                5 => "财务室",
                6 => "电力房",
                7 => "天台",
                8 => "指挥部",
                9 => "证物室",
                10 => "后巷",
                11 => "诊所",
                _ => "港区",
            };
        }

        // ====================================================================
        //  NEON LIGHT & SCENE LIGHTING
        // ====================================================================

        public void CreateNeonLight(string lightName, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(_worldRoot.transform, false);
            lightObject.transform.position = _mapService.ScaleMapPosition(position);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        public void ConfigureSceneLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.68f, 1f);

            GameObject lightObject = new GameObject("CC0 Model Fill Light");
            lightObject.transform.SetParent(_worldRoot.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.55f;
            light.color = new Color(0.8f, 0.88f, 1f, 1f);
        }

        public void CreateEmergencyBell()
        {
            GameObject bell =
                CreateSpriteObject("紧急铃", _circleSprite, new Color(0.72f, 0.08f, 0.06f, 1f));
            bell.transform.SetParent(_worldRoot.transform, false);
            bell.transform.position = _mapService.ScaleMapPosition(new Vector3(0f, 0f, 0.12f));
            bell.transform.localScale = new Vector3(0.58f, 0.58f, 0.34f);
            SetSortingFromZ(bell);

            // M4.3: 附挂 EmergencyButton 组件
            EmergencyButton emergencyBtn = bell.AddComponent<EmergencyButton>();
            emergencyBtn.InteractionRadius = 0.85f;

            CreatePropChild(bell.transform, "Bell Highlight", new Vector3(0f, 0f, 0.08f),
                new Vector3(0.52f, 0.52f, 0.08f), new Color(1f, 0.34f, 0.22f, 0.9f), PrimitiveType.Cylinder);
            CreateWorldLabelAt("紧急铃", _mapService.ScaleMapPosition(new Vector3(0f, 0.48f, -0.16f)), 0.075f);
        }

        // ====================================================================
        //  REMOVE STALE VISUALS (shared generic)
        // ====================================================================

        public static void RemoveStaleVisuals<T>(Dictionary<T, GameObject> visuals, HashSet<T> seen)
        {
            List<T> stale = new List<T>();
            foreach (KeyValuePair<T, GameObject> pair in visuals)
                if (!seen.Contains(pair.Key))
                    stale.Add(pair.Key);

            foreach (T key in stale)
            {
                if (visuals[key] != null) UnityEngine.Object.Destroy(visuals[key]);
                visuals.Remove(key);
            }
        }
        // ====================================================================
        //  M3: Corpse Marker (ground decal at death position)
        // ====================================================================

        /// <summary>
        /// Creates a blood-like ground decal at the death position.
        /// Semi-transparent soft circle, visible from top-down orthographic camera.
        /// </summary>
        public GameObject CreateCorpseMarker(Vector3 position)
        {
            EnsureRuntimeSprites();
            GameObject marker = new GameObject("CorpseMarker");
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = _softCircleSprite;
            renderer.color = new Color(0.55f, 0.04f, 0.04f, 0.35f);
            renderer.sortingOrder = 5; // On ground, below characters

            marker.transform.SetParent(_worldRoot != null ? _worldRoot.transform : null, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            return marker;
        }

        // ====================================================================
        //  M2: WORLD BUILDING (moved from OnlineMatchController)
        // ====================================================================

        /// <summary>
        /// For PoliceStation or KowloonWalledCity mode: creates only the floor background.
        /// Called by OnlineMatchController.CreateSocialDeductionShipMap().
        /// </summary>
        public void CreateFloorBackground()
        {
            CreateFloor();
        }

        /// <summary>
        /// Public entry for building the Hong Kong Port District map.
        /// Called by OnlineMatchController.CreateHongKongPortDistrictMap().
        /// </summary>
        public void BuildDistrictMap()
        {
            ResetLimeZuVisualCounters();
            CreateFloor();
            CreateRoadNetwork();
            CreateMapStructureLayer();
            CreateArchitecturalVolumeLayer();
            CreateShipRooms();
            CreateShipRoomFrames();
            CreateLargeMapProps();
            CreateShipAmbientDressing();
            CreateDenseMapMicroDressing();
            CreatePlayableScaleSetDressing();
            CreateQuaterniusModelDressing();
            CreateLargeScalePortSetPieces();
            CreateLargeRoomReadabilityLayer();
            CreateOfficialFreeAssetStoreLayer();
            CreateCommercialArtAdapterLayer();
            CreateVerticalSliceProductionLayer();

            CreateOperationalLightingLayer();
        }

        /// <summary>
        /// Public entry for building the legacy ship map.
        /// Called by OnlineMatchController.CreateLegacyShipMap().
        /// </summary>
        public void BuildLegacyShipMap()
        {
            ResetLimeZuVisualCounters();
            CreateShipFloor();
            CreateShipCorridors();
            CreateCorridorVolumeLayer();
            CreateShipRooms();
            CreateShipRoomFrames();
            CreateUnderworldPassageNodes();
            CreateShipAmbientDressing();
            CreateDenseMapMicroDressing();
            CreatePlayableScaleSetDressing();
            CreateQuaterniusModelDressing();
            CreateLargeScalePortSetPieces();
            CreateOfficialFreeAssetStoreLayer();
            CreateCommercialArtAdapterLayer();
            CreateVerticalSliceProductionLayer();
        }

        private void CreateOperationalLightingLayer()
        {
            _operationalLightingElementCount = 0;

            Color coldWash = new Color(0.16f, 0.28f, 0.32f, 0.22f);
            Color commandBlue = new Color(0.16f, 0.38f, 0.58f, 0.58f);
            Color amber = new Color(0.76f, 0.58f, 0.2f, 0.68f);
            Color restrictedRed = new Color(0.62f, 0.16f, 0.12f, 0.56f);
            Color lowWhite = new Color(0.72f, 0.78f, 0.72f, 0.36f);

            CreateOperationalLightWash("指挥车冷光洗地", new Vector3(0f, -4.58f, -0.11f), new Vector3(4.6f, 1.55f, 0.04f), commandBlue);
            CreateOperationalLightWash("监控室屏幕反光", new Vector3(-9.22f, 1.75f, -0.1f), new Vector3(2.05f, 1.28f, 0.04f), coldWash);
            CreateOperationalLightWash("海关查验顶灯", new Vector3(-5.02f, 5.18f, -0.1f), new Vector3(2.2f, 1.32f, 0.04f), lowWhite);
            CreateOperationalLightWash("证物库冷链光", new Vector3(-8.58f, -5.02f, -0.1f), new Vector3(2.15f, 1.12f, 0.04f), coldWash);
            CreateOperationalLightWash("地下诊所隔离光", new Vector3(6.12f, -5.04f, -0.1f), new Vector3(2.2f, 1.08f, 0.04f), new Color(0.22f, 0.42f, 0.34f, 0.24f));

            Vector3[] perimeterLights =
            {
                new Vector3(-10.28f, 4.28f, -0.05f),
                new Vector3(-8.42f, 4.28f, -0.05f),
                new Vector3(-5.48f, 4.28f, -0.05f),
                new Vector3(-4.48f, 4.28f, -0.05f),
                new Vector3(8.32f, 4.16f, -0.05f),
                new Vector3(9.42f, 4.16f, -0.05f),
                new Vector3(-7.18f, -5.04f, -0.05f),
                new Vector3(6.12f, -4.02f, -0.05f)
            };

            for (int i = 0; i < perimeterLights.Length; i++)
            {
                Color color = i < 4 ? amber : restrictedRed;
                CreateOperationalStrip("封控灯带 " + i, perimeterLights[i], new Vector3(0.52f, 0.035f, 0.05f), color);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -4.9f + i * 1.95f;
                CreateOperationalStrip("主走廊低位地灯 " + i, new Vector3(x, -0.48f, -0.04f), new Vector3(0.48f, 0.028f, 0.05f), lowWhite);
            }
        }

        private void CreateOperationalLightWash(string name, Vector3 position, Vector3 scale, Color color)
        {
            CreateShapeProp("行动照明 " + name, _softCircleSprite, position, scale, color);
            _operationalLightingElementCount++;
        }

        private void CreateOperationalStrip(string name, Vector3 position, Vector3 scale, Color color)
        {
            CreateShapeProp("行动照明 " + name, _roundedRectSprite, position, scale, color);
            _operationalLightingElementCount++;
        }

        // ── Private implementation ──

        private void CreateFloor()
        {
            // Deep background fill
            CreateProp("港区街区外暗区", new Vector3(0f, 0f, -0.34f), new Vector3(26.2f, 16.8f, 0.08f), new Color(0.025f, 0.032f, 0.034f, 1f));

            // Tiled floor — repeat tile sprite instead of single stretched sprite
            CreateTiledFloor("港区地板纹理", new Vector3(0f, 0f, -0.31f), new Vector2(23.6f, 14.3f), new Color(0.10f, 0.12f, 0.13f, 1f));

            CreateProp("港区主干道暗面", new Vector3(0f, -0.1f, -0.3f), new Vector3(24.0f, 8.6f, 0.08f), new Color(0.094f, 0.112f, 0.116f, 1f));
            CreateProp("港区北侧仓储街块", new Vector3(0f, 4.7f, -0.305f), new Vector3(22.5f, 4.7f, 0.08f), new Color(0.086f, 0.104f, 0.11f, 1f));
            CreateProp("港区南侧封控街块", new Vector3(0f, -5.2f, -0.305f), new Vector3(22.2f, 3.9f, 0.08f), new Color(0.086f, 0.104f, 0.11f, 1f));
            CreateProp("北侧港区围挡", new Vector3(0f, MapService.MapHalfHeight, 0.02f), new Vector3(24.0f, 0.24f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
            CreateProp("南侧港区围挡", new Vector3(0f, -MapService.MapHalfHeight, 0.02f), new Vector3(24.0f, 0.24f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
            CreateProp("西侧港区围挡", new Vector3(-MapService.MapHalfWidth, 0f, 0.02f), new Vector3(0.24f, 15.0f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
            CreateProp("东侧港区围挡", new Vector3(MapService.MapHalfWidth, 0f, 0.02f), new Vector3(0.24f, 15.0f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
        }

        private void CreateRoadNetwork()
        {
            Color mainCorridor = new Color(0.2f, 0.22f, 0.23f, 1f);
            Color branchCorridor = new Color(0.16f, 0.18f, 0.19f, 1f);
            Color serviceCorridor = new Color(0.13f, 0.16f, 0.17f, 1f);
            Color trim = new Color(0.42f, 0.48f, 0.5f, 1f);
            Color guide = new Color(0.74f, 0.65f, 0.24f, 1f);

            CreateCorridorSegment("会议中心圆舱", new Vector3(0f, -0.08f, -0.17f), new Vector3(2.15f, 1.45f, 0.08f), mainCorridor, true);
            CreateCorridorSegment("西中段弯廊", new Vector3(-3.85f, 0.08f, -0.18f), new Vector3(6.35f, 1.04f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("东中段弯廊", new Vector3(4.15f, -0.15f, -0.18f), new Vector3(6.75f, 1.04f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("西北弯廊", new Vector3(-6.95f, 3.78f, -0.18f), new Vector3(7.2f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("东上弯廊", new Vector3(5.15f, 3.98f, -0.18f), new Vector3(7.9f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("西南弯廊", new Vector3(-6.25f, -3.72f, -0.18f), new Vector3(7.4f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("东南弯廊", new Vector3(4.9f, -3.58f, -0.18f), new Vector3(7.2f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("西侧舱梯", new Vector3(-7.18f, 1.45f, -0.18f), new Vector3(1.02f, 5.2f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("西下舱梯", new Vector3(-7.0f, -3.25f, -0.18f), new Vector3(1.02f, 4.5f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("中心竖向短舱", new Vector3(-0.12f, 2.0f, -0.17f), new Vector3(1.12f, 4.65f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("中心南向短舱", new Vector3(0.18f, -3.16f, -0.17f), new Vector3(1.12f, 4.4f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("东侧舱梯", new Vector3(7.12f, 1.18f, -0.18f), new Vector3(1.02f, 5.5f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("东下舱梯", new Vector3(7.35f, -2.6f, -0.18f), new Vector3(1.02f, 4.3f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("金融偏置短廊", new Vector3(4.25f, 1.1f, -0.17f), new Vector3(0.78f, 4.85f, 0.08f), serviceCorridor, false);
            CreateCorridorSegment("指挥舱入口短廊", new Vector3(0.35f, -4.48f, -0.17f), new Vector3(3.75f, 0.78f, 0.08f), serviceCorridor, false);
            CreateCorridorSegment("电房转角舱", new Vector3(8.82f, 4.18f, -0.17f), new Vector3(1.64f, 0.86f, 0.08f), serviceCorridor, true);
            CreateCorridorSegment("证物库转角舱", new Vector3(-7.0f, -4.42f, -0.17f), new Vector3(1.15f, 1.62f, 0.08f), serviceCorridor, true);

            CreateRotatedProp("西上斜向连接舱", new Vector3(-3.82f, 2.38f, -0.16f), new Vector3(4.2f, 0.64f, 0.08f), branchCorridor, 13f);
            CreateRotatedProp("东上斜向连接舱", new Vector3(2.7f, 2.4f, -0.16f), new Vector3(4.1f, 0.64f, 0.08f), branchCorridor, -11f);
            CreateRotatedProp("西下斜向连接舱", new Vector3(-3.6f, -2.1f, -0.16f), new Vector3(4.35f, 0.64f, 0.08f), branchCorridor, -10f);
            CreateRotatedProp("东下斜向连接舱", new Vector3(3.1f, -2.02f, -0.16f), new Vector3(4.15f, 0.64f, 0.08f), branchCorridor, 12f);

            CreateCorridorNode("中央圆节点", new Vector3(0f, 0f, -0.08f), 0.72f, trim);
            CreateCorridorNode("西北圆节点", new Vector3(-7f, 4.15f, -0.08f), 0.54f, trim);
            CreateCorridorNode("东北圆节点", new Vector3(7.25f, 4.15f, -0.08f), 0.54f, trim);
            CreateCorridorNode("西南圆节点", new Vector3(-7f, -3.65f, -0.08f), 0.54f, trim);
            CreateCorridorNode("东南圆节点", new Vector3(7.25f, -3.65f, -0.08f), 0.54f, trim);
            CreateCorridorNode("金融岔口圆节点", new Vector3(4.45f, 0.18f, -0.08f), 0.42f, trim);
            CreateCorridorNode("指挥入口圆节点", new Vector3(0.18f, -4.38f, -0.08f), 0.46f, trim);

            CreateRotatedProp("主走廊导向线 A", new Vector3(-4.8f, 0.08f, -0.07f), new Vector3(4.7f, 0.055f, 0.09f), guide, 2f);
            CreateRotatedProp("主走廊导向线 B", new Vector3(4.85f, -0.14f, -0.07f), new Vector3(5.1f, 0.055f, 0.09f), guide, -2f);
            CreateRotatedProp("北走廊导向线 A", new Vector3(-6.1f, 3.78f, -0.07f), new Vector3(4.8f, 0.05f, 0.09f), guide, 1f);
            CreateRotatedProp("北走廊导向线 B", new Vector3(5.9f, 3.98f, -0.07f), new Vector3(5.4f, 0.05f, 0.09f), guide, -1f);
            CreateRotatedProp("南走廊导向线 A", new Vector3(-5.8f, -3.72f, -0.07f), new Vector3(5.1f, 0.05f, 0.09f), guide, -1.5f);
            CreateRotatedProp("南走廊导向线 B", new Vector3(5.25f, -3.58f, -0.07f), new Vector3(5.0f, 0.05f, 0.09f), guide, 1.5f);
        }

        private void CreateCorridorSegment(string corridorName, Vector3 center, Vector3 size, Color color, bool roundNode)
        {
            // Tiled floor texture (multiple small tiles, not one stretched blob)
            CreateTiledFloor(corridorName + "_floor", center, new Vector2(size.x - 0.15f, size.y - 0.15f), color);

            // Wall borders — thin dark strips giving the corridor thickness/definition
            Color wallBorder = new Color(color.r * 0.35f, color.g * 0.38f, color.b * 0.42f, 1f);
            CreateProp(corridorName + "_wallT", center + new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, 0.1f, 0.09f), wallBorder);
            CreateProp(corridorName + "_wallB", center - new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, 0.1f, 0.09f), wallBorder);
            if (size.x > size.y * 0.6f) // only add left/right on horizontal corridors
            {
                CreateProp(corridorName + "_wallL", center - new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(0.1f, size.y, 0.09f), wallBorder);
                CreateProp(corridorName + "_wallR", center + new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(0.1f, size.y, 0.09f), wallBorder);
            }
            RegisterWalkableArea(center, size);
        }

        private void CreateCorridorNode(string nodeName, Vector3 center, float radius, Color color)
        {
            GameObject node = CreateShapeProp(nodeName, CircleSprite, center, new Vector3(radius, radius, 0.08f), color);
            node.transform.SetAsFirstSibling();
            CreateShapeProp(nodeName + " 内圈", CircleSprite, center + new Vector3(0f, 0f, 0.02f), new Vector3(radius * 0.62f, radius * 0.62f, 0.08f), new Color(0.18f, 0.22f, 0.23f, 1f));
            RegisterWalkableArea(center, new Vector3(radius * 1.9f, radius * 1.9f, 0.08f));
        }

        private void CreateCorridorTrim(string corridorName, Vector3 center, Vector3 size, Color trimColor, bool horizontal)
        {
            if (horizontal)
            {
                CreateRoad(corridorName + " 上沿", center + new Vector3(0f, size.y * 0.5f - 0.06f, 0.02f), new Vector3(size.x, 0.05f, 0.08f), trimColor);
                CreateRoad(corridorName + " 下沿", center + new Vector3(0f, -size.y * 0.5f + 0.06f, 0.02f), new Vector3(size.x, 0.05f, 0.08f), trimColor);
                return;
            }

            CreateRoad(corridorName + " 左沿", center + new Vector3(-size.x * 0.5f + 0.06f, 0f, 0.02f), new Vector3(0.05f, size.y, 0.08f), trimColor);
            CreateRoad(corridorName + " 右沿", center + new Vector3(size.x * 0.5f - 0.06f, 0f, 0.02f), new Vector3(0.05f, size.y, 0.08f), trimColor);
        }

        private void CreateMapStructureLayer()
        {
            Color wall = new Color(0.055f, 0.065f, 0.068f, 1f);
            Color trim = new Color(0.48f, 0.48f, 0.42f, 1f);
            Color door = new Color(0.88f, 0.66f, 0.12f, 1f);

            CreateRoomFrame("西码头货柜场", new Vector3(-9.3f, 5.35f, 0.09f), new Vector3(4.25f, 2.05f, 0.24f), wall, trim, OnlineMapService.MapEntrance.South);
            CreateRoomFrame("海关查验区", new Vector3(-5.0f, 5.35f, 0.09f), new Vector3(2.95f, 2.05f, 0.24f), wall, trim, OnlineMapService.MapEntrance.South);
            CreateRoomFrame("监控室", new Vector3(-9.35f, 1.85f, 0.09f), new Vector3(2.85f, 1.85f, 0.24f), wall, trim, OnlineMapService.MapEntrance.East);
            CreateRoomFrame("茶餐厅", new Vector3(-4.8f, 1.65f, 0.09f), new Vector3(2.85f, 1.8f, 0.24f), wall, trim, OnlineMapService.MapEntrance.East);
            CreateRoomFrame("夜市主街", new Vector3(-1.0f, 2.75f, 0.09f), new Vector3(4.0f, 2.05f, 0.24f), wall, trim, OnlineMapService.MapEntrance.South);
            CreateRoomFrame("金融楼", new Vector3(4.75f, 2.75f, 0.09f), new Vector3(3.3f, 2.05f, 0.24f), wall, trim, OnlineMapService.MapEntrance.West);
            CreateRoomFrame("电房", new Vector3(8.85f, 5.25f, 0.09f), new Vector3(2.7f, 2.05f, 0.24f), wall, trim, OnlineMapService.MapEntrance.South);
            CreateRoomFrame("天台通道", new Vector3(8.95f, 1.65f, 0.09f), new Vector3(2.65f, 1.8f, 0.24f), wall, trim, OnlineMapService.MapEntrance.West);
            CreateRoomFrame("指挥车广场", new Vector3(0f, -5.35f, 0.09f), new Vector3(4.25f, 1.85f, 0.24f), wall, trim, OnlineMapService.MapEntrance.North);
            CreateRoomFrame("证物库", new Vector3(-8.6f, -5.05f, 0.09f), new Vector3(3.25f, 1.9f, 0.24f), wall, trim, OnlineMapService.MapEntrance.East);
            CreateRoomFrame("后巷排档", new Vector3(5.6f, -1.55f, 0.09f), new Vector3(3.45f, 2.1f, 0.24f), wall, trim, OnlineMapService.MapEntrance.West);
            CreateRoomFrame("地下诊所", new Vector3(6.15f, -5.05f, 0.09f), new Vector3(3.35f, 1.9f, 0.24f), wall, trim, OnlineMapService.MapEntrance.North);

            CreateRoadDetailLayer();
            CreateSharedCityProps();

            CreateDoorMarker("码头门禁黄线", new Vector3(-9.3f, 4.28f, 0.13f), new Vector3(1.0f, 0.07f, 0.08f), door);
            CreateDoorMarker("海关排队黄线", new Vector3(-5.0f, 4.28f, 0.13f), new Vector3(0.9f, 0.07f, 0.08f), door);
            CreateDoorMarker("监控室门灯", new Vector3(-7.82f, 1.85f, 0.13f), new Vector3(0.08f, 0.72f, 0.08f), door);
            CreateDoorMarker("茶餐厅门灯", new Vector3(-3.28f, 1.65f, 0.13f), new Vector3(0.08f, 0.72f, 0.08f), door);
            CreateDoorMarker("夜市入口灯带", new Vector3(-1.0f, 1.62f, 0.13f), new Vector3(1.2f, 0.07f, 0.08f), new Color(0.96f, 0.22f, 0.36f, 1f));
            CreateDoorMarker("金融楼门灯", new Vector3(3.02f, 2.75f, 0.13f), new Vector3(0.08f, 0.82f, 0.08f), new Color(0.32f, 0.72f, 1f, 1f));
            CreateDoorMarker("电房警戒门", new Vector3(8.85f, 4.16f, 0.13f), new Vector3(0.92f, 0.07f, 0.08f), door);
            CreateDoorMarker("天台铁门", new Vector3(7.55f, 1.65f, 0.13f), new Vector3(0.08f, 0.72f, 0.08f), trim);
            CreateDoorMarker("指挥广场入口", new Vector3(0f, -4.38f, 0.13f), new Vector3(1.0f, 0.07f, 0.08f), new Color(0.28f, 0.52f, 1f, 1f));
            CreateDoorMarker("证物库门禁", new Vector3(-6.88f, -5.05f, 0.13f), new Vector3(0.08f, 0.78f, 0.08f), door);
            CreateDoorMarker("后巷入口灯", new Vector3(3.82f, -1.55f, 0.13f), new Vector3(0.08f, 0.78f, 0.08f), new Color(0.88f, 0.36f, 0.12f, 1f));
            CreateDoorMarker("诊所卷闸门", new Vector3(6.15f, -4.02f, 0.13f), new Vector3(0.92f, 0.07f, 0.08f), new Color(0.52f, 0.78f, 0.72f, 1f));
        }

        private void CreateRoomFrame(string roomName, Vector3 center, Vector3 size, Color wallColor, Color trimColor, OnlineMapService.MapEntrance entrance)
        {
            float wallThickness = 0.08f;
            float doorGap = Mathf.Min(1.45f, size.x * 0.42f);
            float verticalDoorGap = Mathf.Min(1.2f, size.y * 0.5f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float horizontalSegment = Mathf.Max(0.1f, (size.x - doorGap) * 0.5f);
            float verticalSegment = Mathf.Max(0.1f, (size.y - verticalDoorGap) * 0.5f);

            if (entrance == OnlineMapService.MapEntrance.North)
            {
                CreateWallSegment(roomName + " 北墙左", center + new Vector3(-(doorGap + horizontalSegment) * 0.5f, halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
                CreateWallSegment(roomName + " 北墙右", center + new Vector3((doorGap + horizontalSegment) * 0.5f, halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 北墙", center + new Vector3(0f, halfHeight, 0f), new Vector3(size.x, wallThickness, size.z), wallColor);
            }

            if (entrance == OnlineMapService.MapEntrance.South)
            {
                CreateWallSegment(roomName + " 南墙左", center + new Vector3(-(doorGap + horizontalSegment) * 0.5f, -halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
                CreateWallSegment(roomName + " 南墙右", center + new Vector3((doorGap + horizontalSegment) * 0.5f, -halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 南墙", center + new Vector3(0f, -halfHeight, 0f), new Vector3(size.x, wallThickness, size.z), wallColor);
            }

            if (entrance == OnlineMapService.MapEntrance.East)
            {
                CreateWallSegment(roomName + " 东墙上", center + new Vector3(halfWidth, (verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
                CreateWallSegment(roomName + " 东墙下", center + new Vector3(halfWidth, -(verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 东墙", center + new Vector3(halfWidth, 0f, 0f), new Vector3(wallThickness, size.y, size.z), wallColor);
            }

            if (entrance == OnlineMapService.MapEntrance.West)
            {
                CreateWallSegment(roomName + " 西墙上", center + new Vector3(-halfWidth, (verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
                CreateWallSegment(roomName + " 西墙下", center + new Vector3(-halfWidth, -(verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 西墙", center + new Vector3(-halfWidth, 0f, 0f), new Vector3(wallThickness, size.y, size.z), wallColor);
            }

            CreateProp(roomName + " 顶部细线", center + new Vector3(0f, halfHeight - 0.16f, 0.03f), new Vector3(size.x - 0.34f, 0.035f, 0.05f), trimColor);
            CreateProp(roomName + " 底部细线", center + new Vector3(0f, -halfHeight + 0.16f, 0.03f), new Vector3(size.x - 0.34f, 0.035f, 0.05f), trimColor);
            CreateProp(roomName + " 左侧细线", center + new Vector3(-halfWidth + 0.16f, 0f, 0.03f), new Vector3(0.035f, size.y - 0.34f, 0.05f), trimColor);
            CreateProp(roomName + " 右侧细线", center + new Vector3(halfWidth - 0.16f, 0f, 0.03f), new Vector3(0.035f, size.y - 0.34f, 0.05f), trimColor);
            CreateRoomRoundedCaps(roomName, center, size, trimColor);
            CreateRoomCornerCutouts(roomName, center, size);
            CreateRoomAirlockBulges(roomName, center, size, trimColor, entrance);
        }

        private void CreateRoomRoundedCaps(string roomName, Vector3 center, Vector3 size, Color trimColor)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float cap = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.18f, 0.2f, 0.42f);

            CreateShapeProp(roomName + " 圆角舱壁 NW", CircleSprite, center + new Vector3(-halfWidth + cap * 0.45f, halfHeight - cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
            CreateShapeProp(roomName + " 圆角舱壁 NE", CircleSprite, center + new Vector3(halfWidth - cap * 0.45f, halfHeight - cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
            CreateShapeProp(roomName + " 圆角舱壁 SW", CircleSprite, center + new Vector3(-halfWidth + cap * 0.45f, -halfHeight + cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
            CreateShapeProp(roomName + " 圆角舱壁 SE", CircleSprite, center + new Vector3(halfWidth - cap * 0.45f, -halfHeight + cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
        }

        private void CreateRoomCornerCutouts(string roomName, Vector3 center, Vector3 size)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float cut = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.34f, 0.38f, 0.72f);
            Color voidColor = new Color(0.045f, 0.055f, 0.055f, 1f);

            CreateShapeProp(roomName + " 舱室剪影 NW", CircleSprite, center + new Vector3(-halfWidth - cut * 0.08f, halfHeight + cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);
            CreateShapeProp(roomName + " 舱室剪影 NE", CircleSprite, center + new Vector3(halfWidth + cut * 0.08f, halfHeight + cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);
            CreateShapeProp(roomName + " 舱室剪影 SW", CircleSprite, center + new Vector3(-halfWidth - cut * 0.08f, -halfHeight - cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);
            CreateShapeProp(roomName + " 舱室剪影 SE", CircleSprite, center + new Vector3(halfWidth + cut * 0.08f, -halfHeight - cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);

            if (size.x > 3.2f)
            {
                CreateRotatedProp(roomName + " 斜切暗角 A", center + new Vector3(-halfWidth * 0.6f, halfHeight + 0.02f, 0.23f), new Vector3(size.x * 0.28f, 0.1f, 0.05f), voidColor, -14f);
                CreateRotatedProp(roomName + " 斜切暗角 B", center + new Vector3(halfWidth * 0.55f, -halfHeight - 0.02f, 0.23f), new Vector3(size.x * 0.26f, 0.1f, 0.05f), voidColor, 12f);
            }
        }

        private void CreateRoomAirlockBulges(string roomName, Vector3 center, Vector3 size, Color trimColor, OnlineMapService.MapEntrance entrance)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Color glass = new Color(0.15f, 0.24f, 0.25f, 1f);

            switch (entrance)
            {
                case OnlineMapService.MapEntrance.North:
                    CreateShapeProp(roomName + " 外凸气闸", CircleSprite, center + new Vector3(0f, halfHeight + 0.08f, 0.12f), new Vector3(0.62f, 0.38f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(0f, halfHeight + 0.1f, 0.18f), new Vector3(0.38f, 0.06f, 0.05f), glass);
                    break;
                case OnlineMapService.MapEntrance.South:
                    CreateShapeProp(roomName + " 外凸气闸", CircleSprite, center + new Vector3(0f, -halfHeight - 0.08f, 0.12f), new Vector3(0.62f, 0.38f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(0f, -halfHeight - 0.1f, 0.18f), new Vector3(0.38f, 0.06f, 0.05f), glass);
                    break;
                case OnlineMapService.MapEntrance.East:
                    CreateShapeProp(roomName + " 外凸气闸", CircleSprite, center + new Vector3(halfWidth + 0.08f, 0f, 0.12f), new Vector3(0.38f, 0.62f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(halfWidth + 0.1f, 0f, 0.18f), new Vector3(0.06f, 0.38f, 0.05f), glass);
                    break;
                case OnlineMapService.MapEntrance.West:
                    CreateShapeProp(roomName + " 外凸气闸", CircleSprite, center + new Vector3(-halfWidth - 0.08f, 0f, 0.12f), new Vector3(0.38f, 0.62f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(-halfWidth - 0.1f, 0f, 0.18f), new Vector3(0.06f, 0.38f, 0.05f), glass);
                    break;
            }
        }

        private void CreateWallSegment(string wallName, Vector3 position, Vector3 scale, Color color)
        {
            CreateSolidProp(wallName, position, scale, color);
        }

        private void CreateDoorMarker(string markerName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject marker = CreateProp(markerName, position, scale, color);
            marker.name = markerName + " Door Marker";
            CreateDoorModelOverlay(markerName, position, scale);
        }

        private void CreateArchitecturalVolumeLayer()
        {
            CreateBuildingVolume("西码头仓库", new Vector3(-9.3f, 5.35f, 0f), new Vector3(4.25f, 2.05f, 0.16f), 0.9f, new Color(0.12f, 0.18f, 0.17f, 1f), new Color(0.06f, 0.09f, 0.1f, 1f), "WHARF");
            CreateBuildingVolume("海关查验楼", new Vector3(-5.0f, 5.35f, 0f), new Vector3(2.95f, 2.05f, 0.16f), 1.05f, new Color(0.16f, 0.2f, 0.16f, 1f), new Color(0.08f, 0.1f, 0.09f, 1f), "CUSTOMS");
            CreateBuildingVolume("监控中心", new Vector3(-9.35f, 1.85f, 0f), new Vector3(2.85f, 1.85f, 0.16f), 1.15f, new Color(0.1f, 0.16f, 0.22f, 1f), new Color(0.05f, 0.08f, 0.1f, 1f), "CCTV");
            CreateBuildingVolume("茶餐厅骑楼", new Vector3(-4.8f, 1.65f, 0f), new Vector3(2.85f, 1.8f, 0.16f), 0.78f, new Color(0.34f, 0.19f, 0.1f, 1f), new Color(0.16f, 0.08f, 0.05f, 1f), "茶餐厅");
            CreateBuildingVolume("庙街夜市棚群", new Vector3(-1.0f, 2.75f, 0f), new Vector3(4.0f, 2.05f, 0.16f), 0.62f, new Color(0.3f, 0.12f, 0.08f, 1f), new Color(0.12f, 0.05f, 0.04f, 1f), "NIGHT");
            CreateBuildingVolume("黑钱金融楼", new Vector3(4.75f, 2.75f, 0f), new Vector3(3.3f, 2.05f, 0.16f), 1.55f, new Color(0.14f, 0.16f, 0.24f, 1f), new Color(0.05f, 0.06f, 0.1f, 1f), "FINANCE");
            CreateBuildingVolume("港区电房", new Vector3(8.85f, 5.25f, 0f), new Vector3(2.7f, 2.05f, 0.16f), 1.05f, new Color(0.12f, 0.17f, 0.22f, 1f), new Color(0.05f, 0.07f, 0.08f, 1f), "POWER");
            CreateBuildingVolume("天台机房", new Vector3(8.95f, 1.65f, 0f), new Vector3(2.65f, 1.8f, 0.16f), 1.3f, new Color(0.16f, 0.16f, 0.24f, 1f), new Color(0.07f, 0.07f, 0.1f, 1f), "ROOF");
            CreateBuildingVolume("警队指挥车棚", new Vector3(0f, -5.35f, 0f), new Vector3(4.25f, 1.85f, 0.16f), 0.72f, new Color(0.1f, 0.17f, 0.24f, 1f), new Color(0.04f, 0.07f, 0.1f, 1f), "COMMAND");
            CreateBuildingVolume("证物库冷仓", new Vector3(-8.6f, -5.05f, 0f), new Vector3(3.25f, 1.9f, 0.16f), 1.15f, new Color(0.16f, 0.16f, 0.23f, 1f), new Color(0.07f, 0.07f, 0.1f, 1f), "EVIDENCE");
            CreateBuildingVolume("后巷排档楼", new Vector3(5.6f, -1.55f, 0f), new Vector3(3.45f, 2.1f, 0.16f), 0.86f, new Color(0.26f, 0.13f, 0.08f, 1f), new Color(0.1f, 0.05f, 0.04f, 1f), "ALLEY");
            CreateBuildingVolume("地下诊所唐楼", new Vector3(6.15f, -5.05f, 0f), new Vector3(3.35f, 1.9f, 0.16f), 1.22f, new Color(0.12f, 0.22f, 0.18f, 1f), new Color(0.05f, 0.09f, 0.07f, 1f), "CLINIC");
            CreateTopDownFacilityBackdrop();
        }

        private void CreateBuildingVolume(string name, Vector3 center, Vector3 size, float height, Color facadeColor, Color roofColor, string sign)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Color trim = new Color(0.5f, 0.54f, 0.5f, 1f);
            Color darkTrim = new Color(0.035f, 0.045f, 0.05f, 1f);

            CreateProp("2.5D 建筑体 " + name + " 顶视阴影", center + new Vector3(0.1f, -0.1f, -0.13f), new Vector3(size.x + 0.3f, size.y + 0.26f, 0.05f), new Color(0f, 0f, 0f, 0.22f));
            CreateProp("2.5D 建筑体 " + name + " 室内地台", center + new Vector3(0f, 0f, -0.04f), new Vector3(size.x * 0.93f, size.y * 0.86f, 0.08f), Darken(facadeColor, 1.18f));
            CreateProp("屋顶 " + name + " 顶视房间铭牌", center + new Vector3(0f, halfHeight - 0.18f, 0.15f), new Vector3(Mathf.Min(size.x * 0.72f, 2.35f), 0.14f, 0.08f), roofColor);
            CreateProp("2.5D 建筑体 " + name + " 北墙厚边", center + new Vector3(0f, halfHeight - 0.04f, 0.18f), new Vector3(size.x, 0.13f, 0.14f), darkTrim);
            CreateProp("2.5D 建筑体 " + name + " 南墙厚边", center + new Vector3(0f, -halfHeight + 0.04f, 0.18f), new Vector3(size.x, 0.13f, 0.14f), darkTrim);
            CreateProp("2.5D 建筑体 " + name + " 西墙厚边", center + new Vector3(-halfWidth + 0.04f, 0f, 0.18f), new Vector3(0.13f, size.y, 0.14f), darkTrim);
            CreateProp("2.5D 建筑体 " + name + " 东墙厚边", center + new Vector3(halfWidth - 0.04f, 0f, 0.18f), new Vector3(0.13f, size.y, 0.14f), darkTrim);
            CreateRoomFloorTiles(name, center, size, facadeColor);
            CreateRoomEquipmentBays(name, center, size, height);
            CreateProp("屋顶 " + name + " 门楣灯", center + new Vector3(0f, -halfHeight + 0.18f, 0.22f), new Vector3(Mathf.Min(1.05f, size.x * 0.35f), 0.08f, 0.08f), new Color(0.94f, 0.72f, 0.12f, 1f));
            CreateProp("屋顶 " + name + " 导航箭头", center + new Vector3(-halfWidth + 0.35f, halfHeight - 0.34f, 0.2f), new Vector3(0.22f, 0.18f, 0.08f), trim);
            CreateWorldLabelAt(sign, MapService.ScaleMapPosition(new Vector3(center.x, center.y + halfHeight - 0.3f, -0.18f)), 0.055f);
        }

        private void CreateRoomFloorTiles(string name, Vector3 center, Vector3 size, Color baseColor)
        {
            int columns = Mathf.Clamp(Mathf.RoundToInt(size.x * 1.2f), 3, 7);
            int rows = Mathf.Clamp(Mathf.RoundToInt(size.y * 1.5f), 2, 5);
            float tileWidth = size.x * 0.78f / columns;
            float tileHeight = size.y * 0.62f / rows;
            float startX = center.x - size.x * 0.39f + tileWidth * 0.5f;
            float startY = center.y - size.y * 0.3f + tileHeight * 0.5f;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float x = startX + column * tileWidth;
                    float y = startY + row * tileHeight;
                    float shade = (row + column) % 2 == 0 ? 1.26f : 1.08f;
                    CreateProp("2.5D 建筑体 " + name + " 顶视地砖 " + row + "-" + column, new Vector3(x, y, 0.02f), new Vector3(tileWidth * 0.82f, tileHeight * 0.78f, 0.04f), Darken(baseColor, shade));
                }
            }
        }

        private void CreateRoomEquipmentBays(string name, Vector3 center, Vector3 size, float height)
        {
            Color screen = new Color(0.06f, 0.58f, 0.72f, 1f);
            Color metal = new Color(0.08f, 0.09f, 0.1f, 1f);
            Color warning = new Color(0.86f, 0.68f, 0.12f, 1f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;

            CreateProp("屋顶 " + name + " 设备台 A", center + new Vector3(-halfWidth * 0.46f, -halfHeight * 0.18f, 0.2f), new Vector3(0.38f, 0.26f, 0.12f), metal);
            CreateProp("屋顶 " + name + " 设备屏 A", center + new Vector3(-halfWidth * 0.46f, -halfHeight * 0.02f, 0.26f), new Vector3(0.3f, 0.06f, 0.08f), screen);
            CreateProp("屋顶 " + name + " 设备台 B", center + new Vector3(halfWidth * 0.42f, halfHeight * 0.05f, 0.2f), new Vector3(0.34f, 0.3f, 0.12f), metal);
            CreateProp("屋顶 " + name + " 状态灯 B", center + new Vector3(halfWidth * 0.42f, halfHeight * 0.23f, 0.26f), new Vector3(0.22f, 0.05f, 0.07f), screen);
            CreateProp("2.5D 建筑体 " + name + " 警戒斜纹 A", center + new Vector3(-halfWidth * 0.18f, -halfHeight * 0.36f, 0.16f), new Vector3(0.56f, 0.05f, 0.06f), warning);
            CreateProp("2.5D 建筑体 " + name + " 警戒斜纹 B", center + new Vector3(halfWidth * 0.1f, -halfHeight * 0.36f, 0.16f), new Vector3(0.56f, 0.05f, 0.06f), warning);

            if (height > 1.05f)
            {
                CreateProp("屋顶 " + name + " 高风险设备箱", center + new Vector3(0f, 0f, 0.28f), new Vector3(0.36f, 0.24f, 0.12f), new Color(0.22f, 0.2f, 0.12f, 1f));
                CreateProp("屋顶 " + name + " 红色警示点", center + new Vector3(0f, 0.18f, 0.34f), new Vector3(0.08f, 0.08f, 0.06f), new Color(0.9f, 0.08f, 0.06f, 1f));
            }
        }

        private void CreateTopDownFacilityBackdrop()
        {
            Color[] colors =
            {
                new Color(0.07f, 0.085f, 0.09f, 1f),
                new Color(0.06f, 0.075f, 0.08f, 1f),
                new Color(0.08f, 0.075f, 0.065f, 1f)
            };

            for (int i = 0; i < 12; i++)
            {
                float x = -11.4f + i * 2.08f;
                CreateProp("2.5D 建筑体 外围封闭舱段 " + i, new Vector3(x, 7.55f, -0.22f), new Vector3(1.44f, 0.42f, 0.08f), colors[i % colors.Length]);
                CreateProp("屋顶 外围封闭舱段 " + i, new Vector3(x, 7.22f, -0.18f), new Vector3(1.14f, 0.08f, 0.06f), new Color(0.025f, 0.035f, 0.04f, 1f));
            }
        }

        private void CreateRoadDetailLayer()
        {
            Color panelLine = new Color(0.52f, 0.58f, 0.56f, 1f);
            Color rail = new Color(0.34f, 0.4f, 0.4f, 1f);
            Color vent = new Color(0.05f, 0.065f, 0.07f, 1f);
            Color yellow = new Color(0.84f, 0.66f, 0.08f, 1f);

            for (int i = 0; i < 11; i++)
            {
                float x = -9.8f + i * 1.95f;
                CreateProp("主舱地板接缝 " + i, new Vector3(x, 0f, -0.08f), new Vector3(0.52f, 0.035f, 0.06f), panelLine);
            }

            for (int i = 0; i < 9; i++)
            {
                float x = -8.2f + i * 2.05f;
                CreateProp("南舱地板接缝 " + i, new Vector3(x, -3.65f, -0.08f), new Vector3(0.48f, 0.035f, 0.06f), panelLine);
            }

            for (int i = 0; i < 8; i++)
            {
                float y = -5.7f + i * 1.55f;
                CreateProp("西舱导轨 " + i, new Vector3(-7f, y, -0.08f), new Vector3(0.035f, 0.5f, 0.06f), rail);
                CreateProp("东舱导轨 " + i, new Vector3(7.25f, y, -0.08f), new Vector3(0.035f, 0.5f, 0.06f), rail);
            }

            for (int i = 0; i < 5; i++)
            {
                CreateRotatedProp("码头气闸黄黑条 " + i, new Vector3(-6.95f + i * 0.18f, 4.15f, -0.07f), new Vector3(0.07f, 0.62f, 0.06f), i % 2 == 0 ? yellow : vent, 0f);
                CreateRotatedProp("指挥舱黄黑条 " + i, new Vector3(-0.36f + i * 0.18f, -4.15f, -0.07f), new Vector3(0.07f, 0.62f, 0.06f), i % 2 == 0 ? yellow : vent, 0f);
            }

            CreateVentGrate("通风口 A", new Vector3(-3.2f, 0.32f, -0.05f));
            CreateVentGrate("通风口 B", new Vector3(4.9f, -0.32f, -0.05f));
            CreateVentGrate("通风口 C", new Vector3(-1.2f, -3.98f, -0.05f));

            CreateProp("北侧舱内导向杆", new Vector3(2.2f, 4.15f, -0.06f), new Vector3(0.64f, 0.05f, 0.06f), panelLine);
            CreateShapeProp("北侧舱内导向头", DiamondSprite, new Vector3(2.62f, 4.15f, -0.05f), new Vector3(0.22f, 0.22f, 0.06f), panelLine);
            CreateProp("后舱导向杆", new Vector3(7.25f, -2.8f, -0.06f), new Vector3(0.05f, 0.64f, 0.06f), panelLine);
            CreateShapeProp("后舱导向头", DiamondSprite, new Vector3(7.25f, -3.2f, -0.05f), new Vector3(0.22f, 0.22f, 0.06f), panelLine);
        }

        private void CreateVentGrate(string name, Vector3 position)
        {
            CreateModelProp(name + " CC0 Vent", name.Contains("主") ? "Props/Prop_Vent_Big.fbx" : "Props/Prop_Vent_Small.fbx", position + new Vector3(0f, 0f, 0.08f), new Vector3(0.48f, 0.48f, 0.14f), 0f);
            bool rusted = name.IndexOf("东", StringComparison.Ordinal) >= 0
                || name.IndexOf("右", StringComparison.Ordinal) >= 0
                || name.EndsWith("B", StringComparison.Ordinal);
            Sprite sprite = rusted ? Sprite2DAssetCache.KowloonVentIcon : Sprite2DAssetCache.VentIcon;
            string resourcePath = rusted ? Sprite2DAssetCache.KowloonVentIconResourcePath : Sprite2DAssetCache.VentIconResourcePath;
            CreateRuntimeMapProp("地图小件 " + name + " 实物通风口", sprite, resourcePath, position, new Vector3(0.42f, 0.42f, 0.06f), Color.white);
        }

        private void CreateSharedCityProps()
        {
            Color metal = new Color(0.1f, 0.12f, 0.13f, 1f);
            Color plastic = new Color(0.14f, 0.28f, 0.32f, 1f);
            Color warning = new Color(0.86f, 0.66f, 0.1f, 1f);

            for (int i = 0; i < 5; i++)
            {
                CreatePrimitiveProp("外壳铆钉 " + i, PrimitiveType.Cylinder, new Vector3(-5.2f + i * 2.6f, -6.98f, 0.08f), new Vector3(0.1f, 0.12f, 0.1f), metal);
                CreateProp("外壳加固梁 " + i, new Vector3(-4.0f + i * 2.6f, -6.98f, 0.1f), new Vector3(1.9f, 0.035f, 0.06f), metal);
            }

            CreateSolidProp("墙面监控终端", new Vector3(2.95f, -3.25f, 0.08f), new Vector3(0.16f, 0.88f, 0.28f), new Color(0.08f, 0.16f, 0.18f, 1f));
            CreateProp("终端冷光屏", new Vector3(3.02f, -3.25f, 0.22f), new Vector3(0.05f, 0.72f, 0.16f), new Color(0.32f, 0.86f, 0.95f, 1f));
            CreateSolidProp("警用通讯柱", new Vector3(-2.7f, -3.18f, 0.08f), new Vector3(0.34f, 0.42f, 0.28f), new Color(0.14f, 0.18f, 0.2f, 1f));
            CreateProp("通讯柱灯窗", new Vector3(-2.7f, -3.19f, 0.2f), new Vector3(0.24f, 0.3f, 0.08f), new Color(0.28f, 0.68f, 0.78f, 1f));
            CreateSolidProp("舱内补给柜", new Vector3(1.9f, 0.82f, 0.08f), new Vector3(0.34f, 0.48f, 0.28f), plastic);
            CreateProp("补给柜状态灯", new Vector3(1.9f, 1.06f, 0.22f), new Vector3(0.24f, 0.04f, 0.1f), new Color(0.88f, 0.18f, 0.22f, 1f));
            CreateSolidProp("可疑封控箱 A", new Vector3(-1.05f, -0.68f, 0.06f), new Vector3(0.62f, 0.16f, 0.18f), warning);
            CreateSolidProp("可疑封控箱 B", new Vector3(1.05f, 0.68f, 0.06f), new Vector3(0.62f, 0.16f, 0.18f), warning);
            CreateProp("封控箱黑条 A", new Vector3(-1.05f, -0.68f, 0.16f), new Vector3(0.36f, 0.04f, 0.06f), metal);
            CreateProp("封控箱黑条 B", new Vector3(1.05f, 0.68f, 0.16f), new Vector3(0.36f, 0.04f, 0.06f), metal);

            CreatePrimitiveProp("旋转摄像头底座", PrimitiveType.Cylinder, new Vector3(3.35f, 0.72f, 0.14f), new Vector3(0.12f, 0.1f, 0.12f), metal);
            CreateProp("旋转摄像头机身", new Vector3(3.35f, 0.9f, 0.28f), new Vector3(0.2f, 0.1f, 0.18f), metal);
            CreatePrimitiveProp("摄像头红点", PrimitiveType.Sphere, new Vector3(3.28f, 0.91f, 0.34f), new Vector3(0.04f, 0.04f, 0.04f), new Color(0.9f, 0.08f, 0.05f, 1f));
            CreatePrimitiveProp("摄像头绿点", PrimitiveType.Sphere, new Vector3(3.42f, 0.91f, 0.34f), new Vector3(0.04f, 0.04f, 0.04f), new Color(0.08f, 0.78f, 0.18f, 1f));

            CreateProp("气闸隔离条 A", new Vector3(-5.65f, -3.62f, 0.06f), new Vector3(0.62f, 0.12f, 0.18f), new Color(0.82f, 0.18f, 0.1f, 1f));
            CreateProp("气闸隔离条 B", new Vector3(-4.92f, -3.62f, 0.06f), new Vector3(0.62f, 0.12f, 0.18f), new Color(0.82f, 0.18f, 0.1f, 1f));
            CreateProp("墙边档案柜", new Vector3(-6.45f, 0.78f, 0.06f), new Vector3(0.36f, 0.3f, 0.22f), new Color(0.08f, 0.2f, 0.28f, 1f));
            CreateProp("档案柜屏幕", new Vector3(-6.45f, 0.92f, 0.18f), new Vector3(0.26f, 0.04f, 0.08f), new Color(0.34f, 0.88f, 0.95f, 1f));
            CreateUnderworldPassageNodes();
        }

        private void CreateUnderworldPassageNodes()
        {
            for (int i = 0; i < UnderworldPassageCount; i++)
            {
                Vector3 position = MapService.UnderworldPassageDesignPosition(i, UnderworldPassageCount);
                CreateModelProp("暗线节点 " + i + " CC0 Vent Hatch", "Props/Prop_Vent_Big.fbx", position + new Vector3(0f, 0f, 0.02f), new Vector3(0.62f, 0.62f, 0.16f), i * 23f);
                GameObject node = CreatePrimitiveProp("暗线节点 " + i, PrimitiveType.Cylinder, position + new Vector3(0f, 0f, 0.1f), new Vector3(0.26f, 0.08f, 0.26f), new Color(0.45f, 0.1f, 0.55f, 1f));
                CreatePropChild(node.transform, "暗线井盖纹", new Vector3(0f, 0f, 0.06f), new Vector3(0.64f, 0.12f, 0.08f), new Color(0.9f, 0.42f, 1f, 1f), PrimitiveType.Cube);
                CreatePropChild(node.transform, "暗线箭头", new Vector3(0f, 0.18f, 0.07f), new Vector3(0.16f, 0.22f, 0.08f), new Color(0.78f, 0.2f, 0.86f, 1f), PrimitiveType.Cube);
            }
        }

        private void CreateLargeMapProps()
        {
            CreateDockyardDressing();
            CreateCustomsDressing();
            CreateCctvRoomDressing();
            CreateTeaCafeDressing();
            CreateNightMarketDressing();
            CreateFinanceDressing();
            CreatePowerRoomDressing();
            CreateRooftopDressing();
            CreateCommandPostDressing();
            CreateEvidenceRoomDressing();
            CreateBackLaneDressing();
            CreateClinicDressing();
        }

        private void CreateZone(string zoneName, Vector3 position, Vector3 scale, Color color)
        {
            CreateProp(zoneName, position, scale, color);
            CreateWorldLabelAt(zoneName, MapService.ScaleMapPosition(position + new Vector3(0f, scale.y * 0.34f, -0.16f)), 0.07f);
        }

        private void CreateRoad(string roadName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject road = CreateProp(roadName, position, scale, color);
            road.transform.SetAsFirstSibling();
        }

        private void CreateDockyardDressing()
        {
            Color[] colors =
            {
                new Color(0.08f, 0.15f, 0.22f, 0.78f),
                new Color(0.28f, 0.11f, 0.09f, 0.78f),
                new Color(0.36f, 0.28f, 0.08f, 0.78f),
                new Color(0.08f, 0.22f, 0.15f, 0.78f)
            };

            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    float x = -10.55f + column * 1.45f;
                    float y = 6.05f - row * 0.62f;
                    CreateSolidProp("货柜底影 " + row + "-" + column, new Vector3(x, y, 0.02f), new Vector3(1.16f, 0.34f, 0.08f), colors[(row + column) % colors.Length]);
                    CreateModelProp("成熟港区设施 免费货柜替代模块 " + row + "-" + column, (row + column) % 2 == 0 ? "Props/Prop_Crate4.fbx" : "Props/Prop_Crate3.fbx", new Vector3(x, y, 0.08f), new Vector3(0.92f, 0.34f, 0.34f), column % 2 == 0 ? 0f : 180f, true);
                    CreateProp("货柜细门线 " + row + "-" + column, new Vector3(x - 0.36f, y, 0.18f), new Vector3(0.025f, 0.22f, 0.04f), new Color(0.62f, 0.66f, 0.62f, 0.9f));
                    CreateProp("货柜小编号牌 " + row + "-" + column, new Vector3(x + 0.36f, y + 0.12f, 0.18f), new Vector3(0.13f, 0.035f, 0.035f), new Color(0.82f, 0.72f, 0.22f, 0.9f));
                }
            }

            CreateSolidProp("码头吊机立柱", new Vector3(-10.95f, 4.2f, 0.2f), new Vector3(0.2f, 1.65f, 0.42f), new Color(0.72f, 0.46f, 0.06f, 1f));
            CreateProp("码头吊机横臂", new Vector3(-9.95f, 4.92f, 0.28f), new Vector3(1.95f, 0.12f, 0.22f), new Color(0.72f, 0.46f, 0.06f, 1f));
            CreateProp("吊机挂钩", new Vector3(-9.18f, 4.58f, 0.16f), new Vector3(0.16f, 0.44f, 0.16f), new Color(0.08f, 0.08f, 0.08f, 1f));
            CreatePrimitiveProp("系船柱 A", PrimitiveType.Cylinder, new Vector3(-11.1f, 3.65f, 0.08f), new Vector3(0.18f, 0.08f, 0.18f), new Color(0.08f, 0.09f, 0.1f, 1f));
            CreatePrimitiveProp("系船柱 B", PrimitiveType.Cylinder, new Vector3(-8.75f, 3.65f, 0.08f), new Vector3(0.18f, 0.08f, 0.18f), new Color(0.08f, 0.09f, 0.1f, 1f));
            CreateProp("港口缆绳", new Vector3(-9.9f, 3.68f, 0.04f), new Vector3(2.1f, 0.05f, 0.06f), new Color(0.48f, 0.36f, 0.18f, 1f));
            CreateProp("叉车车身", new Vector3(-8.38f, 4.72f, 0.07f), new Vector3(0.52f, 0.28f, 0.18f), new Color(0.9f, 0.68f, 0.08f, 1f));
            CreateProp("叉车货叉", new Vector3(-7.92f, 4.72f, 0.08f), new Vector3(0.42f, 0.05f, 0.08f), new Color(0.08f, 0.08f, 0.07f, 1f));
            CreatePrimitiveProp("叉车轮 A", PrimitiveType.Cylinder, new Vector3(-8.56f, 4.55f, 0.08f), new Vector3(0.08f, 0.04f, 0.08f), new Color(0.04f, 0.04f, 0.04f, 1f));
            CreatePrimitiveProp("叉车轮 B", PrimitiveType.Cylinder, new Vector3(-8.22f, 4.55f, 0.08f), new Vector3(0.08f, 0.04f, 0.08f), new Color(0.04f, 0.04f, 0.04f, 1f));
            CreateProp("地面绑带 A", new Vector3(-10.6f, 5.1f, 0.02f), new Vector3(0.05f, 0.82f, 0.04f), new Color(0.08f, 0.08f, 0.08f, 1f));
            CreateProp("地面绑带 B", new Vector3(-8.25f, 5.1f, 0.02f), new Vector3(0.05f, 0.82f, 0.04f), new Color(0.08f, 0.08f, 0.08f, 1f));
            CreateModelDominantDockyardForeground();
        }

        private void CreateModelDominantDockyardForeground()
        {
            Vector3 anchor = new Vector3(-9.42f, 4.92f, 0f);
            CreateModelProp("成熟港区设施 开局主视觉金属平台", "Platforms/Platform_Rails_4WideTall.fbx", anchor + new Vector3(0.1f, -0.42f, 0.04f), new Vector3(2.2f, 0.62f, 0.32f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉门框左", "Platforms/Door_Frame_SquareTall.fbx", anchor + new Vector3(-1.56f, 0.06f, 0.18f), new Vector3(0.42f, 1.22f, 0.64f), 90f, true);
            CreateModelProp("成熟港区设施 开局主视觉门框右", "Platforms/Door_Frame_SquareTall.fbx", anchor + new Vector3(1.56f, 0.02f, 0.18f), new Vector3(0.42f, 1.22f, 0.64f), -90f, true);
            CreateModelProp("成熟港区设施 开局主视觉窗墙", "Walls/WallAstra_Straight_Window.fbx", anchor + new Vector3(0f, 0.78f, 0.28f), new Vector3(2.2f, 0.26f, 0.62f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉电缆墙", "Walls/TopCables_Straight_Hanging.fbx", anchor + new Vector3(-0.1f, 1.08f, 0.46f), new Vector3(1.9f, 0.26f, 0.5f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉地灯左", "Props/Prop_Light_Floor.fbx", anchor + new Vector3(-1.04f, -0.82f, 0.12f), new Vector3(0.36f, 0.36f, 0.44f), 0f);
            CreateModelProp("成熟港区设施 开局主视觉地灯右", "Props/Prop_Light_Floor.fbx", anchor + new Vector3(1.1f, -0.78f, 0.12f), new Vector3(0.36f, 0.36f, 0.44f), 180f);
            CreateModelProp("成熟港区设施 开局主视觉接入终端", "Props/Prop_AccessPoint.fbx", anchor + new Vector3(1.15f, 0.48f, 0.14f), new Vector3(0.5f, 0.36f, 0.38f), 180f);
            CreateModelProp("成熟港区设施 开局主视觉电脑台", "Props/Prop_Computer.fbx", anchor + new Vector3(-1.12f, 0.42f, 0.14f), new Vector3(0.46f, 0.34f, 0.34f), 0f);
            CreateModelProp("成熟港区设施 开局主视觉通风机", "Props/Prop_Vent_Big.fbx", anchor + new Vector3(0.02f, 0.26f, 0.18f), new Vector3(0.72f, 0.36f, 0.24f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉弧形护栏左", "Props/Prop_Rail_Round_Big.fbx", anchor + new Vector3(-1.42f, -0.5f, 0.14f), new Vector3(0.64f, 0.46f, 0.28f), 90f, true);
            CreateModelProp("成熟港区设施 开局主视觉弧形护栏右", "Props/Prop_Rail_Round_Big.fbx", anchor + new Vector3(1.42f, -0.5f, 0.14f), new Vector3(0.64f, 0.46f, 0.28f), -90f, true);
            CreateMeshBoxProp("成熟港区设施 开局主视觉冷色导线", anchor + new Vector3(0f, -1.02f, 0.08f), new Vector3(2.15f, 0.04f, 0.05f), new Color(0.08f, 0.72f, 0.86f, 1f));
            CreateMeshBoxProp("成熟港区设施 开局主视觉警戒黄线", anchor + new Vector3(0f, -1.18f, 0.08f), new Vector3(2.3f, 0.04f, 0.05f), new Color(0.92f, 0.7f, 0.08f, 1f));
        }

        private void CreateCustomsDressing()
        {
            CreateSolidProp("查验闸机", new Vector3(-5.0f, 4.42f, 0.06f), new Vector3(1.55f, 0.18f, 0.22f), new Color(0.18f, 0.28f, 0.22f, 1f));
            CreateSolidProp("海关桌", new Vector3(-5.55f, 5.75f, 0.05f), new Vector3(0.82f, 0.42f, 0.18f), new Color(0.24f, 0.24f, 0.2f, 1f));
            CreateSolidProp("扫描门", new Vector3(-4.35f, 5.55f, 0.12f), new Vector3(0.14f, 0.75f, 0.36f), new Color(0.12f, 0.18f, 0.22f, 1f));
            CreateProp("封条箱 A", new Vector3(-5.85f, 4.92f, 0.05f), new Vector3(0.35f, 0.28f, 0.2f), new Color(0.74f, 0.68f, 0.42f, 1f));
            CreateProp("封条箱 B", new Vector3(-5.38f, 4.92f, 0.05f), new Vector3(0.35f, 0.28f, 0.2f), new Color(0.74f, 0.68f, 0.42f, 1f));
            CreateProp("查验告示牌", new Vector3(-4.2f, 4.58f, 0.07f), new Vector3(0.78f, 0.08f, 0.24f), new Color(0.88f, 0.72f, 0.1f, 1f));
            CreateProp("护照托盘", new Vector3(-5.58f, 5.48f, 0.16f), new Vector3(0.32f, 0.16f, 0.05f), new Color(0.08f, 0.18f, 0.36f, 1f));
            CreateProp("查验印章", new Vector3(-5.18f, 5.72f, 0.15f), new Vector3(0.12f, 0.1f, 0.08f), new Color(0.46f, 0.1f, 0.08f, 1f));
            CreateProp("行李 X 光带", new Vector3(-4.38f, 4.92f, 0.08f), new Vector3(0.9f, 0.18f, 0.12f), new Color(0.08f, 0.08f, 0.09f, 1f));
            CreatePrimitiveProp("X 光滚轮 A", PrimitiveType.Cylinder, new Vector3(-4.72f, 4.92f, 0.11f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.5f, 0.5f, 0.46f, 1f));
            CreatePrimitiveProp("X 光滚轮 B", PrimitiveType.Cylinder, new Vector3(-4.08f, 4.92f, 0.11f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.5f, 0.5f, 0.46f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 海关票据机", Sprite2DAssetCache.InteriorRoomPropTicketMachine,
                Sprite2DAssetCache.InteriorRoomPropTicketMachineResourcePath, new Vector3(-5.16f, 4.82f, 0.3f),
                new Vector3(0.42f, 0.5f, 0.08f), Color.white, -2f);
            CreateLimeZuRoomProp("房间实物 LimeZu 海关 X 光检查台", Sprite2DAssetCache.InteriorRoomPropGroceryCheckoutRoller,
                Sprite2DAssetCache.InteriorRoomPropGroceryCheckoutRollerResourcePath, new Vector3(-4.36f, 4.9f, 0.3f),
                new Vector3(0.62f, 0.5f, 0.08f), Color.white, 1f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 海关储物柜", Sprite2DAssetCache.InteriorRoomPropJailLockerFull,
                Sprite2DAssetCache.InteriorRoomPropJailLockerFullResourcePath, new Vector3(-5.78f, 5.88f, 0.32f),
                new Vector3(0.42f, 0.58f, 0.08f), Color.white, 3f, true);
        }

        private void CreateCctvRoomDressing()
        {
            CreateSolidProp("监控控制台", new Vector3(-9.35f, 1.2f, 0.06f), new Vector3(1.28f, 0.28f, 0.18f), new Color(0.06f, 0.12f, 0.16f, 1f));

            for (int i = 0; i < 4; i++)
            {
                CreateProp("监控屏 " + i, new Vector3(-10.0f + i * 0.42f, 2.22f, 0.08f), new Vector3(0.32f, 0.06f, 0.22f), new Color(0.05f, 0.45f, 0.58f, 1f));
                CreateProp("监控屏边框 " + i, new Vector3(-10.0f + i * 0.42f, 2.18f, 0.1f), new Vector3(0.36f, 0.035f, 0.24f), new Color(0.02f, 0.03f, 0.04f, 1f));
            }

            CreateSolidProp("录像机柜", new Vector3(-8.28f, 1.25f, 0.07f), new Vector3(0.36f, 0.42f, 0.28f), new Color(0.12f, 0.14f, 0.18f, 1f));
            CreateProp("折叠椅", new Vector3(-9.95f, 1.48f, 0.05f), new Vector3(0.24f, 0.2f, 0.16f), new Color(0.16f, 0.18f, 0.2f, 1f));
            CreateProp("录像带箱", new Vector3(-8.72f, 2.35f, 0.05f), new Vector3(0.46f, 0.24f, 0.18f), new Color(0.2f, 0.2f, 0.18f, 1f));
            CreateProp("键盘灯条", new Vector3(-9.35f, 1.38f, 0.18f), new Vector3(0.98f, 0.04f, 0.06f), new Color(0.08f, 0.72f, 0.82f, 1f));
            CreateProp("咖啡杯", new Vector3(-9.9f, 1.22f, 0.19f), new Vector3(0.1f, 0.1f, 0.1f), new Color(0.74f, 0.68f, 0.54f, 1f));
            CreateProp("硬盘阵列 A", new Vector3(-8.28f, 1.44f, 0.24f), new Vector3(0.28f, 0.05f, 0.05f), new Color(0.08f, 0.62f, 0.18f, 1f));
            CreateProp("硬盘阵列 B", new Vector3(-8.28f, 1.22f, 0.24f), new Vector3(0.28f, 0.05f, 0.05f), new Color(0.08f, 0.62f, 0.18f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 监控室双屏工作站", Sprite2DAssetCache.OfficeRoomPropDualMonitorDesk,
                Sprite2DAssetCache.OfficeRoomPropDualMonitorDeskResourcePath, new Vector3(-9.34f, 1.3f, 0.32f),
                new Vector3(0.72f, 0.8f, 0.08f), Color.white, -2f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 监控室服务器柜", Sprite2DAssetCache.OfficeRoomPropServerRack,
                Sprite2DAssetCache.OfficeRoomPropServerRackResourcePath, new Vector3(-8.32f, 1.72f, 0.34f),
                new Vector3(0.46f, 0.7f, 0.08f), Color.white, 1f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 监控室摄像机工作台", Sprite2DAssetCache.OfficeRoomPropCctvCameraRig,
                Sprite2DAssetCache.OfficeRoomPropCctvCameraRigResourcePath, new Vector3(-9.96f, 1.92f, 0.3f),
                new Vector3(0.48f, 0.66f, 0.08f), Color.white, 5f);
        }

        private void CreateTeaCafeDressing()
        {
            CreateSolidProp("茶餐厅吧台", new Vector3(-5.6f, 1.95f, 0.06f), new Vector3(0.28f, 1.0f, 0.18f), new Color(0.5f, 0.28f, 0.12f, 1f));

            for (int i = 0; i < 3; i++)
            {
                float y = 1.05f + i * 0.42f;
                CreateSolidProp("卡座桌 " + i, new Vector3(-4.75f, y, 0.05f), new Vector3(0.44f, 0.2f, 0.14f), new Color(0.6f, 0.38f, 0.18f, 1f));
                CreateProp("卡座椅 " + i + "A", new Vector3(-5.1f, y, 0.05f), new Vector3(0.18f, 0.18f, 0.14f), new Color(0.42f, 0.12f, 0.08f, 1f));
                CreateProp("卡座椅 " + i + "B", new Vector3(-4.4f, y, 0.05f), new Vector3(0.18f, 0.18f, 0.14f), new Color(0.42f, 0.12f, 0.08f, 1f));
                CreateProp("奶茶杯 " + i, new Vector3(-4.75f, y + 0.06f, 0.16f), new Vector3(0.08f, 0.08f, 0.08f), new Color(0.78f, 0.56f, 0.32f, 1f));
            }

            CreateProp("收银机", new Vector3(-5.58f, 2.45f, 0.12f), new Vector3(0.18f, 0.18f, 0.14f), new Color(0.12f, 0.16f, 0.18f, 1f));
            CreateProp("厨房隔断", new Vector3(-3.95f, 2.25f, 0.06f), new Vector3(0.7f, 0.12f, 0.18f), new Color(0.72f, 0.62f, 0.42f, 1f));
            CreateSolidProp("冰柜", new Vector3(-3.78f, 1.2f, 0.08f), new Vector3(0.3f, 0.42f, 0.24f), new Color(0.18f, 0.34f, 0.38f, 1f));
            CreateProp("餐牌灯箱", new Vector3(-4.68f, 2.45f, 0.14f), new Vector3(0.72f, 0.07f, 0.18f), new Color(0.92f, 0.72f, 0.26f, 1f));
            CreateProp("厨房炉火", new Vector3(-3.98f, 2.02f, 0.16f), new Vector3(0.16f, 0.07f, 0.08f), new Color(1f, 0.32f, 0.08f, 1f));
            CreateProp("餐具架", new Vector3(-5.58f, 1.42f, 0.18f), new Vector3(0.08f, 0.42f, 0.12f), new Color(0.82f, 0.82f, 0.74f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 茶餐厅蛋糕冰柜", Sprite2DAssetCache.InteriorRoomPropCanteenCakeFridge,
                Sprite2DAssetCache.InteriorRoomPropCanteenCakeFridgeResourcePath, new Vector3(-3.78f, 1.18f, 0.32f),
                new Vector3(0.58f, 0.68f, 0.08f), Color.white, -2f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 茶餐厅四眼炉", Sprite2DAssetCache.InteriorRoomPropKitchenOven4Cookers,
                Sprite2DAssetCache.InteriorRoomPropKitchenOven4CookersResourcePath, new Vector3(-3.96f, 2.08f, 0.3f),
                new Vector3(0.5f, 0.44f, 0.08f), Color.white, 3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 茶餐厅水槽", Sprite2DAssetCache.InteriorRoomPropKitchenSink,
                Sprite2DAssetCache.InteriorRoomPropKitchenSinkResourcePath, new Vector3(-5.56f, 1.62f, 0.28f),
                new Vector3(0.42f, 0.42f, 0.08f), Color.white, -4f);
            CreateLimeZuRoomProp("房间实物 LimeZu 茶餐厅旧电视", Sprite2DAssetCache.InteriorRoomPropOldTv,
                Sprite2DAssetCache.InteriorRoomPropOldTvResourcePath, new Vector3(-4.54f, 2.62f, 0.3f),
                new Vector3(0.38f, 0.4f, 0.08f), Color.white, 6f);
        }

        private void CreateNightMarketDressing()
        {
            for (int i = 0; i < 4; i++)
            {
                float x = -2.35f + i * 1.05f;
                Color stallColor = i % 2 == 0 ? new Color(0.55f, 0.18f, 0.08f, 1f) : new Color(0.18f, 0.38f, 0.18f, 1f);
                CreateSolidProp("夜市摊台 " + i, new Vector3(x, 3.1f, 0.04f), new Vector3(0.72f, 0.36f, 0.2f), stallColor);
                CreateProp("夜市棚顶 " + i, new Vector3(x, 3.35f, 0.18f), new Vector3(0.82f, 0.12f, 0.12f), new Color(0.86f, 0.28f, 0.18f, 1f));
                CreatePrimitiveProp("灯笼 " + i, PrimitiveType.Sphere, new Vector3(x + 0.32f, 3.55f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.95f, 0.22f, 0.12f, 1f));
                CreateProp("食材盘 " + i, new Vector3(x - 0.16f, 3.08f, 0.18f), new Vector3(0.18f, 0.14f, 0.05f), new Color(0.82f, 0.48f, 0.22f, 1f));
                CreateProp("收钱盒 " + i, new Vector3(x + 0.18f, 3.08f, 0.18f), new Vector3(0.16f, 0.1f, 0.06f), new Color(0.08f, 0.1f, 0.12f, 1f));
            }

            CreateProp("霓虹招牌", new Vector3(-0.65f, 3.78f, 0.06f), new Vector3(1.8f, 0.12f, 0.24f), new Color(0.9f, 0.12f, 0.42f, 1f));
            CreateProp("啤酒箱堆", new Vector3(0.92f, 2.05f, 0.05f), new Vector3(0.42f, 0.32f, 0.18f), new Color(0.36f, 0.2f, 0.08f, 1f));
            CreateSolidProp("排队栏杆 A", new Vector3(-2.85f, 2.25f, 0.06f), new Vector3(0.08f, 0.78f, 0.12f), new Color(0.1f, 0.1f, 0.1f, 1f));
            CreateSolidProp("排队栏杆 B", new Vector3(1.15f, 2.25f, 0.06f), new Vector3(0.08f, 0.78f, 0.12f), new Color(0.1f, 0.1f, 0.1f, 1f));
            CreateProp("地摊胶凳 A", new Vector3(-2.38f, 2.08f, 0.05f), new Vector3(0.18f, 0.18f, 0.12f), new Color(0.82f, 0.18f, 0.12f, 1f));
            CreateProp("地摊胶凳 B", new Vector3(0.38f, 2.05f, 0.05f), new Vector3(0.18f, 0.18f, 0.12f), new Color(0.12f, 0.42f, 0.78f, 1f));
            CreateProp("纸皮箱堆", new Vector3(-1.72f, 2.02f, 0.05f), new Vector3(0.36f, 0.24f, 0.16f), new Color(0.64f, 0.42f, 0.22f, 1f));
            CreateProp("鱼档冰床", new Vector3(-2.06f, 2.34f, 0.06f), new Vector3(0.52f, 0.18f, 0.14f), new Color(0.72f, 0.86f, 0.9f, 1f));
            CreateProp("暗号价牌", new Vector3(-1.98f, 2.62f, 0.16f), new Vector3(0.28f, 0.06f, 0.12f), new Color(0.92f, 0.78f, 0.16f, 1f));
            CreateProp("摊档油桶 A", new Vector3(1.32f, 3.04f, 0.08f), new Vector3(0.16f, 0.16f, 0.18f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("摊档油桶 B", new Vector3(1.52f, 3.04f, 0.08f), new Vector3(0.16f, 0.16f, 0.18f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("夜市布帘", new Vector3(-0.12f, 3.56f, 0.18f), new Vector3(0.52f, 0.08f, 0.1f), new Color(0.2f, 0.34f, 0.68f, 1f));
            CreateProp("霓虹小箭头", new Vector3(0.98f, 3.72f, 0.14f), new Vector3(0.22f, 0.14f, 0.08f), new Color(0.08f, 0.86f, 0.9f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 夜市野餐长桌", Sprite2DAssetCache.RoomPropBenchedTable,
                Sprite2DAssetCache.RoomPropBenchedTableResourcePath, new Vector3(-1.02f, 2.18f, 0.24f),
                new Vector3(0.64f, 0.54f, 0.08f), Color.white, -6f);
            CreateLimeZuRoomProp("房间实物 LimeZu 夜市露营椅 A", Sprite2DAssetCache.RoomPropChair,
                Sprite2DAssetCache.RoomPropChairResourcePath, new Vector3(-1.78f, 2.36f, 0.24f),
                new Vector3(0.34f, 0.36f, 0.08f), Color.white, 8f);
            CreateLimeZuRoomProp("房间实物 LimeZu 夜市露营椅 B", Sprite2DAssetCache.RoomPropChair,
                Sprite2DAssetCache.RoomPropChairResourcePath, new Vector3(-0.38f, 2.28f, 0.24f),
                new Vector3(0.34f, 0.36f, 0.08f), Color.white, -10f);
            CreateLimeZuRoomProp("房间实物 LimeZu 夜市鱼档水槽", Sprite2DAssetCache.InteriorRoomPropFishCuttingSink,
                Sprite2DAssetCache.InteriorRoomPropFishCuttingSinkResourcePath, new Vector3(-2.08f, 2.46f, 0.3f),
                new Vector3(0.48f, 0.54f, 0.08f), Color.white, -3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 夜市烧烤炉", Sprite2DAssetCache.InteriorRoomPropKitchenBbq,
                Sprite2DAssetCache.InteriorRoomPropKitchenBbqResourcePath, new Vector3(1.28f, 3.24f, 0.3f),
                new Vector3(0.54f, 0.58f, 0.08f), Color.white, 5f);
            CreateLimeZuRoomProp("房间实物 LimeZu 夜市购物车", Sprite2DAssetCache.InteriorRoomPropShoppingCartBlueFull,
                Sprite2DAssetCache.InteriorRoomPropShoppingCartBlueFullResourcePath, new Vector3(0.78f, 2.52f, 0.28f),
                new Vector3(0.38f, 0.44f, 0.08f), Color.white, -8f);
        }

        private void CreateFinanceDressing()
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 3.8f + i * 0.7f;
                CreateSolidProp("金融办公桌 " + i, new Vector3(x, 2.55f, 0.05f), new Vector3(0.45f, 0.28f, 0.16f), new Color(0.28f, 0.24f, 0.2f, 1f));
                CreateProp("电脑屏 " + i, new Vector3(x, 2.78f, 0.12f), new Vector3(0.28f, 0.05f, 0.18f), new Color(0.05f, 0.4f, 0.6f, 1f));
                CreateProp("账本 " + i, new Vector3(x - 0.14f, 2.48f, 0.16f), new Vector3(0.16f, 0.1f, 0.04f), new Color(0.78f, 0.72f, 0.54f, 1f));
            }

            CreateSolidProp("保险柜", new Vector3(5.95f, 2.08f, 0.08f), new Vector3(0.42f, 0.42f, 0.3f), new Color(0.18f, 0.18f, 0.22f, 1f));
            CreateSolidProp("档案柜 A", new Vector3(5.95f, 3.05f, 0.08f), new Vector3(0.36f, 0.32f, 0.28f), new Color(0.22f, 0.22f, 0.28f, 1f));
            CreateSolidProp("档案柜 B", new Vector3(6.45f, 3.05f, 0.08f), new Vector3(0.36f, 0.32f, 0.28f), new Color(0.22f, 0.22f, 0.28f, 1f));
            CreateProp("金融楼入口", new Vector3(4.75f, 3.55f, 0.06f), new Vector3(1.05f, 0.16f, 0.24f), new Color(0.32f, 0.32f, 0.42f, 1f));
            CreateProp("保险柜转盘", new Vector3(5.95f, 2.28f, 0.26f), new Vector3(0.08f, 0.04f, 0.08f), new Color(0.78f, 0.72f, 0.54f, 1f));
            CreateProp("碎纸机", new Vector3(3.35f, 3.15f, 0.06f), new Vector3(0.28f, 0.26f, 0.18f), new Color(0.12f, 0.12f, 0.14f, 1f));
            CreateProp("碎纸袋", new Vector3(3.18f, 3.35f, 0.04f), new Vector3(0.18f, 0.16f, 0.1f), new Color(0.68f, 0.68f, 0.62f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 金融行情监控屏", Sprite2DAssetCache.RoomPropMonitor,
                Sprite2DAssetCache.RoomPropMonitorResourcePath, new Vector3(4.48f, 2.92f, 0.28f),
                new Vector3(0.5f, 0.44f, 0.08f), Color.white);
            CreateLimeZuRoomProp("房间实物 LimeZu 金融主控大屏", Sprite2DAssetCache.RoomPropControlBigMonitor,
                Sprite2DAssetCache.RoomPropControlBigMonitorResourcePath, new Vector3(5.22f, 2.96f, 0.3f),
                new Vector3(0.62f, 0.5f, 0.08f), Color.white, 2f);
            CreateLimeZuRoomProp("房间实物 LimeZu 金融金库现金保险柜", Sprite2DAssetCache.InteriorRoomPropSafeBucks,
                Sprite2DAssetCache.InteriorRoomPropSafeBucksResourcePath, new Vector3(5.92f, 2.08f, 0.34f),
                new Vector3(0.48f, 0.48f, 0.08f), Color.white, -2f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 金融金条保险柜", Sprite2DAssetCache.InteriorRoomPropSafeGold,
                Sprite2DAssetCache.InteriorRoomPropSafeGoldResourcePath, new Vector3(6.42f, 2.28f, 0.32f),
                new Vector3(0.42f, 0.42f, 0.08f), Color.white, 4f);
            CreateLimeZuRoomProp("房间实物 LimeZu 金融安防摄像头", Sprite2DAssetCache.InteriorRoomPropSecurityCameraWallRight,
                Sprite2DAssetCache.InteriorRoomPropSecurityCameraWallRightResourcePath, new Vector3(3.62f, 3.42f, 0.34f),
                new Vector3(0.32f, 0.34f, 0.08f), Color.white, 12f);
        }

        private void CreatePowerRoomDressing()
        {
            for (int i = 0; i < 3; i++)
            {
                CreateSolidProp("电房变压器 " + i, new Vector3(8.15f + i * 0.55f, 5.65f, 0.04f), new Vector3(0.34f, 0.52f, 0.32f), new Color(0.18f, 0.24f, 0.34f, 1f));
                CreateProp("电缆桥架 " + i, new Vector3(8.15f + i * 0.55f, 4.52f, 0.08f), new Vector3(0.42f, 0.08f, 0.12f), new Color(0.04f, 0.04f, 0.05f, 1f));
                CreateProp("变压器指示灯 " + i, new Vector3(8.15f + i * 0.55f, 5.92f, 0.22f), new Vector3(0.06f, 0.04f, 0.05f), new Color(0.08f, 0.82f, 0.18f, 1f));
            }

            CreateSolidProp("电闸面板", new Vector3(9.72f, 5.12f, 0.09f), new Vector3(0.28f, 0.62f, 0.28f), new Color(0.08f, 0.12f, 0.18f, 1f));
            CreateProp("黄色警戒线", new Vector3(8.78f, 4.42f, 0.06f), new Vector3(1.45f, 0.08f, 0.1f), new Color(0.9f, 0.7f, 0.08f, 1f));
            CreatePrimitiveProp("压力表", PrimitiveType.Cylinder, new Vector3(9.4f, 5.55f, 0.14f), new Vector3(0.12f, 0.04f, 0.12f), new Color(0.72f, 0.78f, 0.75f, 1f));
            CreateProp("红色急停钮", new Vector3(9.72f, 5.36f, 0.25f), new Vector3(0.08f, 0.04f, 0.06f), new Color(0.9f, 0.06f, 0.04f, 1f));
            CreateProp("地面电缆 A", new Vector3(8.68f, 5.05f, 0.01f), new Vector3(1.15f, 0.04f, 0.04f), new Color(0.02f, 0.02f, 0.025f, 1f));
            CreateProp("地面电缆 B", new Vector3(9.15f, 5.35f, 0.01f), new Vector3(0.04f, 0.72f, 0.04f), new Color(0.02f, 0.02f, 0.025f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 电房备用发电机", Sprite2DAssetCache.RoomPropGenerator,
                Sprite2DAssetCache.RoomPropGeneratorResourcePath, new Vector3(8.22f, 4.92f, 0.28f),
                new Vector3(0.7f, 0.62f, 0.08f), Color.white, -2f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 电房维修灯塔", Sprite2DAssetCache.RoomPropLightTower,
                Sprite2DAssetCache.RoomPropLightTowerResourcePath, new Vector3(9.38f, 4.72f, 0.34f),
                new Vector3(0.52f, 0.76f, 0.08f), Color.white, 3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 电房工具箱", Sprite2DAssetCache.RoomPropToolBox,
                Sprite2DAssetCache.RoomPropToolBoxResourcePath, new Vector3(9.68f, 4.58f, 0.22f),
                new Vector3(0.34f, 0.32f, 0.08f), Color.white, -6f);
            CreateLimeZuRoomProp("房间实物 LimeZu 电房检修陷门", Sprite2DAssetCache.InteriorRoomPropTrapdoor,
                Sprite2DAssetCache.InteriorRoomPropTrapdoorResourcePath, new Vector3(8.82f, 4.46f, 0.2f),
                new Vector3(0.46f, 0.42f, 0.08f), Color.white, 1f);
            CreateLimeZuRoomProp("房间实物 LimeZu 电房安全摄像头", Sprite2DAssetCache.InteriorRoomPropSecurityCameraWallRight,
                Sprite2DAssetCache.InteriorRoomPropSecurityCameraWallRightResourcePath, new Vector3(9.84f, 5.82f, 0.34f),
                new Vector3(0.3f, 0.32f, 0.08f), Color.white, -12f);
        }

        private void CreateRooftopDressing()
        {
            CreateSolidPrimitiveProp("天台水塔", PrimitiveType.Cylinder, new Vector3(9.58f, 1.95f, 0.14f), new Vector3(0.36f, 0.24f, 0.36f), new Color(0.42f, 0.42f, 0.46f, 1f));
            CreateSolidProp("空调外机 A", new Vector3(8.35f, 1.1f, 0.07f), new Vector3(0.38f, 0.3f, 0.22f), new Color(0.54f, 0.54f, 0.52f, 1f));
            CreateSolidProp("空调外机 B", new Vector3(8.85f, 1.1f, 0.07f), new Vector3(0.38f, 0.3f, 0.22f), new Color(0.54f, 0.54f, 0.52f, 1f));
            CreateSolidProp("天台梯门", new Vector3(9.8f, 1.18f, 0.08f), new Vector3(0.32f, 0.46f, 0.28f), new Color(0.18f, 0.18f, 0.22f, 1f));
            CreateSolidProp("围栏北", new Vector3(8.95f, 2.42f, 0.08f), new Vector3(1.7f, 0.07f, 0.14f), new Color(0.1f, 0.1f, 0.12f, 1f));
            CreateSolidProp("围栏东", new Vector3(10.05f, 1.65f, 0.08f), new Vector3(0.07f, 1.35f, 0.14f), new Color(0.1f, 0.1f, 0.12f, 1f));
            CreateProp("天台排水沟", new Vector3(8.12f, 2.16f, 0.02f), new Vector3(0.48f, 0.04f, 0.05f), new Color(0.04f, 0.06f, 0.06f, 1f));
            CreateProp("晾衣绳", new Vector3(9.05f, 1.36f, 0.2f), new Vector3(0.72f, 0.03f, 0.04f), new Color(0.82f, 0.82f, 0.72f, 1f));
            CreateProp("晾晒布 A", new Vector3(8.85f, 1.28f, 0.18f), new Vector3(0.18f, 0.1f, 0.06f), new Color(0.62f, 0.18f, 0.32f, 1f));
            CreateProp("晾晒布 B", new Vector3(9.18f, 1.28f, 0.18f), new Vector3(0.18f, 0.1f, 0.06f), new Color(0.22f, 0.42f, 0.7f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 天台旧电视监控", Sprite2DAssetCache.InteriorRoomPropOldTv,
                Sprite2DAssetCache.InteriorRoomPropOldTvResourcePath, new Vector3(8.32f, 1.3f, 0.3f),
                new Vector3(0.34f, 0.38f, 0.08f), Color.white, -8f);
            CreateLimeZuRoomProp("房间实物 LimeZu 天台暗门", Sprite2DAssetCache.InteriorRoomPropTrapdoor,
                Sprite2DAssetCache.InteriorRoomPropTrapdoorResourcePath, new Vector3(9.62f, 2.3f, 0.18f),
                new Vector3(0.42f, 0.4f, 0.08f), Color.white, 2f, true);
        }

        private void CreateCommandPostDressing()
        {
            CreateSolidProp("警用指挥车", new Vector3(0.2f, -5.3f, 0.05f), new Vector3(1.25f, 0.72f, 0.3f), new Color(0.08f, 0.12f, 0.14f, 1f));
            CreateProp("车顶天线", new Vector3(0.2f, -4.82f, 0.28f), new Vector3(0.06f, 0.4f, 0.08f), new Color(0.02f, 0.02f, 0.02f, 1f));
            CreateSolidProp("指挥折叠桌", new Vector3(-1.12f, -5.45f, 0.05f), new Vector3(0.8f, 0.32f, 0.16f), new Color(0.22f, 0.22f, 0.2f, 1f));
            CreateProp("行动白板", new Vector3(-1.12f, -4.92f, 0.12f), new Vector3(0.75f, 0.08f, 0.3f), new Color(0.82f, 0.86f, 0.82f, 1f));
            CreateSolidProp("警灯路障", new Vector3(1.7f, -4.35f, 0.06f), new Vector3(1.2f, 0.12f, 0.18f), new Color(0.1f, 0.28f, 0.9f, 1f));
            CreatePrimitiveProp("路锥 A", PrimitiveType.Cylinder, new Vector3(2.2f, -5.78f, 0.07f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.9f, 0.34f, 0.08f, 1f));
            CreatePrimitiveProp("路锥 B", PrimitiveType.Cylinder, new Vector3(2.62f, -5.78f, 0.07f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.9f, 0.34f, 0.08f, 1f));
            CreatePrimitiveProp("路锥 C", PrimitiveType.Cylinder, new Vector3(3.04f, -5.78f, 0.07f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.9f, 0.34f, 0.08f, 1f));
            CreateProp("车窗玻璃 A", new Vector3(-0.16f, -4.95f, 0.24f), new Vector3(0.28f, 0.05f, 0.1f), new Color(0.18f, 0.48f, 0.58f, 1f));
            CreateProp("车窗玻璃 B", new Vector3(0.42f, -4.95f, 0.24f), new Vector3(0.28f, 0.05f, 0.1f), new Color(0.18f, 0.48f, 0.58f, 1f));
            CreateProp("地图文件 A", new Vector3(-1.2f, -5.38f, 0.16f), new Vector3(0.18f, 0.12f, 0.04f), new Color(0.84f, 0.78f, 0.58f, 1f));
            CreateProp("地图文件 B", new Vector3(-0.92f, -5.48f, 0.16f), new Vector3(0.18f, 0.12f, 0.04f), new Color(0.84f, 0.78f, 0.58f, 1f));
            CreateProp("警灯红", new Vector3(-0.2f, -4.88f, 0.34f), new Vector3(0.16f, 0.06f, 0.06f), new Color(0.9f, 0.06f, 0.06f, 1f));
            CreateProp("警灯蓝", new Vector3(0.6f, -4.88f, 0.34f), new Vector3(0.16f, 0.06f, 0.06f), new Color(0.08f, 0.24f, 0.9f, 1f));
            CreateProp("无人机起降垫", new Vector3(0.96f, -4.62f, 0.08f), new Vector3(0.52f, 0.32f, 0.08f), new Color(0.08f, 0.14f, 0.16f, 1f));
            CreateProp("无人机机臂 A", new Vector3(0.96f, -4.62f, 0.18f), new Vector3(0.5f, 0.05f, 0.06f), new Color(0.08f, 0.62f, 0.8f, 1f));
            CreateProp("无人机机臂 B", new Vector3(0.96f, -4.62f, 0.19f), new Vector3(0.05f, 0.34f, 0.06f), new Color(0.08f, 0.62f, 0.8f, 1f));
            CreateProp("警用电池箱", new Vector3(1.62f, -5.72f, 0.06f), new Vector3(0.32f, 0.22f, 0.16f), new Color(0.18f, 0.26f, 0.28f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 指挥行动白板", Sprite2DAssetCache.OfficeRoomPropWhiteboard,
                Sprite2DAssetCache.OfficeRoomPropWhiteboardResourcePath, new Vector3(-1.18f, -4.82f, 0.32f),
                new Vector3(0.68f, 0.7f, 0.08f), Color.white, -3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 指挥打印机", Sprite2DAssetCache.OfficeRoomPropPrinter,
                Sprite2DAssetCache.OfficeRoomPropPrinterResourcePath, new Vector3(-0.52f, -5.64f, 0.28f),
                new Vector3(0.38f, 0.48f, 0.08f), Color.white, 4f);
        }

        private void CreateEvidenceRoomDressing()
        {
            CreateSolidProp("证物冷柜", new Vector3(-8.95f, -5.28f, 0.04f), new Vector3(0.72f, 0.42f, 0.26f), new Color(0.16f, 0.34f, 0.38f, 1f));

            for (int i = 0; i < 3; i++)
            {
                CreateSolidProp("证物货架 " + i, new Vector3(-9.92f + i * 0.62f, -4.45f, 0.07f), new Vector3(0.42f, 0.18f, 0.26f), new Color(0.24f, 0.22f, 0.18f, 1f));
                CreateProp("封存箱 " + i, new Vector3(-9.92f + i * 0.62f, -5.82f, 0.05f), new Vector3(0.36f, 0.28f, 0.18f), new Color(0.7f, 0.62f, 0.38f, 1f));
                CreateProp("证物标签 " + i, new Vector3(-9.92f + i * 0.62f, -4.32f, 0.22f), new Vector3(0.18f, 0.04f, 0.05f), new Color(0.88f, 0.78f, 0.18f, 1f));
            }

            CreateProp("证物封条", new Vector3(-8.18f, -4.42f, 0.08f), new Vector3(0.62f, 0.08f, 0.1f), new Color(0.92f, 0.78f, 0.08f, 1f));
            CreateProp("鉴证灯箱", new Vector3(-7.52f, -5.18f, 0.08f), new Vector3(0.42f, 0.32f, 0.22f), new Color(0.18f, 0.52f, 0.58f, 1f));
            CreateProp("血样冷藏盒", new Vector3(-8.82f, -5.1f, 0.2f), new Vector3(0.2f, 0.12f, 0.06f), new Color(0.68f, 0.08f, 0.08f, 1f));
            CreateProp("证物相片板", new Vector3(-7.72f, -4.58f, 0.16f), new Vector3(0.36f, 0.08f, 0.18f), new Color(0.82f, 0.82f, 0.74f, 1f));
            CreateProp("紫外灯条", new Vector3(-7.52f, -5.02f, 0.22f), new Vector3(0.34f, 0.04f, 0.06f), new Color(0.42f, 0.24f, 0.86f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 证物库封存大包", Sprite2DAssetCache.LandmarkPackage,
                Sprite2DAssetCache.LandmarkPackageResourcePath, new Vector3(-8.24f, -5.74f, 0.22f),
                new Vector3(0.42f, 0.36f, 0.08f), Color.white, -4f);
            CreateLimeZuRoomProp("房间实物 LimeZu 证物库临时投递箱", Sprite2DAssetCache.LandmarkMailbox,
                Sprite2DAssetCache.LandmarkMailboxResourcePath, new Vector3(-7.66f, -5.76f, 0.22f),
                new Vector3(0.54f, 0.44f, 0.08f), Color.white, 3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 证物库案件图表板", Sprite2DAssetCache.OfficeRoomPropChartBoard,
                Sprite2DAssetCache.OfficeRoomPropChartBoardResourcePath, new Vector3(-7.58f, -4.76f, 0.32f),
                new Vector3(0.62f, 0.66f, 0.08f), Color.white, 2f);
            CreateLimeZuRoomProp("房间实物 LimeZu 证物库办公打印台", Sprite2DAssetCache.OfficeRoomPropPrinter,
                Sprite2DAssetCache.OfficeRoomPropPrinterResourcePath, new Vector3(-8.92f, -4.8f, 0.3f),
                new Vector3(0.4f, 0.48f, 0.08f), Color.white, -5f);
        }

        private void CreateBackLaneDressing()
        {
            CreateSolidProp("后巷垃圾箱", new Vector3(6.2f, -0.95f, 0.04f), new Vector3(0.62f, 0.38f, 0.22f), new Color(0.05f, 0.26f, 0.14f, 1f));
            CreateSolidProp("排档炉头", new Vector3(5.08f, -1.28f, 0.06f), new Vector3(0.42f, 0.28f, 0.2f), new Color(0.18f, 0.18f, 0.16f, 1f));
            CreatePrimitiveProp("煤气瓶 A", PrimitiveType.Cylinder, new Vector3(5.62f, -0.82f, 0.08f), new Vector3(0.12f, 0.18f, 0.12f), new Color(0.18f, 0.42f, 0.42f, 1f));
            CreatePrimitiveProp("煤气瓶 B", PrimitiveType.Cylinder, new Vector3(5.88f, -0.82f, 0.08f), new Vector3(0.12f, 0.18f, 0.12f), new Color(0.18f, 0.42f, 0.42f, 1f));
            CreateProp("雨棚", new Vector3(5.52f, -1.95f, 0.16f), new Vector3(1.28f, 0.12f, 0.12f), new Color(0.42f, 0.1f, 0.08f, 1f));
            CreateSolidProp("黑帮摩托", new Vector3(6.85f, -2.08f, 0.06f), new Vector3(0.66f, 0.18f, 0.16f), new Color(0.08f, 0.08f, 0.1f, 1f));
            CreateProp("排档火苗", new Vector3(5.08f, -1.12f, 0.18f), new Vector3(0.16f, 0.08f, 0.08f), new Color(1f, 0.28f, 0.06f, 1f));
            CreateProp("墙面涂鸦", new Vector3(4.3f, -0.72f, 0.12f), new Vector3(0.58f, 0.05f, 0.14f), new Color(0.78f, 0.12f, 0.48f, 1f));
            CreateProp("摩托车把", new Vector3(7.18f, -2.08f, 0.14f), new Vector3(0.16f, 0.04f, 0.05f), new Color(0.7f, 0.7f, 0.64f, 1f));
            CreatePrimitiveProp("摩托前轮", PrimitiveType.Cylinder, new Vector3(7.16f, -2.08f, 0.08f), new Vector3(0.09f, 0.04f, 0.09f), new Color(0.02f, 0.02f, 0.025f, 1f));
            CreatePrimitiveProp("摩托后轮", PrimitiveType.Cylinder, new Vector3(6.54f, -2.08f, 0.08f), new Vector3(0.09f, 0.04f, 0.09f), new Color(0.02f, 0.02f, 0.025f, 1f));
            CreateProp("摩托车牌架", new Vector3(6.86f, -1.84f, 0.16f), new Vector3(0.22f, 0.05f, 0.06f), new Color(0.84f, 0.84f, 0.76f, 1f));
            CreateProp("后巷油污", new Vector3(6.22f, -2.42f, 0.01f), new Vector3(0.54f, 0.14f, 0.04f), new Color(0.02f, 0.025f, 0.02f, 1f));
            CreateProp("外卖箱", new Vector3(4.62f, -2.42f, 0.06f), new Vector3(0.34f, 0.24f, 0.18f), new Color(0.86f, 0.36f, 0.1f, 1f));
            CreateProp("后门铁闩", new Vector3(4.0f, -1.18f, 0.12f), new Vector3(0.08f, 0.52f, 0.08f), new Color(0.5f, 0.5f, 0.46f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 后巷黑色垃圾桶", Sprite2DAssetCache.RoomPropTrashCan,
                Sprite2DAssetCache.RoomPropTrashCanResourcePath, new Vector3(6.62f, -0.66f, 0.22f),
                new Vector3(0.42f, 0.4f, 0.08f), Color.white, -4f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 后巷垃圾堆", Sprite2DAssetCache.RoomPropTrashPile,
                Sprite2DAssetCache.RoomPropTrashPileResourcePath, new Vector3(6.9f, -1.28f, 0.18f),
                new Vector3(0.48f, 0.38f, 0.08f), Color.white, 7f);
            CreateLimeZuRoomProp("房间实物 LimeZu 后巷屠宰挂肉", Sprite2DAssetCache.InteriorRoomPropButcherCarcass,
                Sprite2DAssetCache.InteriorRoomPropButcherCarcassResourcePath, new Vector3(5.08f, -1.02f, 0.32f),
                new Vector3(0.4f, 0.48f, 0.08f), Color.white, -3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 后巷店铺冰柜", Sprite2DAssetCache.InteriorRoomPropGroceryGlassFridge,
                Sprite2DAssetCache.InteriorRoomPropGroceryGlassFridgeResourcePath, new Vector3(4.44f, -1.82f, 0.32f),
                new Vector3(0.42f, 0.58f, 0.08f), Color.white, 5f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 后巷收银滚台", Sprite2DAssetCache.InteriorRoomPropGroceryCheckoutRoller,
                Sprite2DAssetCache.InteriorRoomPropGroceryCheckoutRollerResourcePath, new Vector3(5.76f, -2.22f, 0.28f),
                new Vector3(0.52f, 0.42f, 0.08f), Color.white, -7f);
        }

        private void CreateClinicDressing()
        {
            CreateSolidProp("诊所病床 A", new Vector3(5.55f, -5.45f, 0.04f), new Vector3(0.78f, 0.38f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
            CreateSolidProp("诊所病床 B", new Vector3(6.65f, -5.45f, 0.04f), new Vector3(0.78f, 0.38f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
            CreateSolidProp("药柜", new Vector3(5.2f, -4.45f, 0.08f), new Vector3(0.42f, 0.32f, 0.3f), new Color(0.2f, 0.34f, 0.28f, 1f));
            CreateProp("手术灯臂", new Vector3(6.1f, -4.78f, 0.18f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.82f, 0.82f, 0.72f, 1f));
            CreatePrimitiveProp("手术灯", PrimitiveType.Sphere, new Vector3(6.1f, -5.04f, 0.22f), new Vector3(0.16f, 0.16f, 0.08f), new Color(0.9f, 0.86f, 0.68f, 1f));
            CreatePrimitiveProp("输液架", PrimitiveType.Cylinder, new Vector3(7.18f, -5.22f, 0.14f), new Vector3(0.06f, 0.26f, 0.06f), new Color(0.74f, 0.74f, 0.68f, 1f));
            CreateProp("病床血压仪 A", new Vector3(5.18f, -5.22f, 0.16f), new Vector3(0.12f, 0.1f, 0.08f), new Color(0.08f, 0.14f, 0.18f, 1f));
            CreateProp("病床血压仪 B", new Vector3(6.28f, -5.22f, 0.16f), new Vector3(0.12f, 0.1f, 0.08f), new Color(0.08f, 0.14f, 0.18f, 1f));
            CreateProp("药瓶 A", new Vector3(5.08f, -4.3f, 0.25f), new Vector3(0.06f, 0.06f, 0.12f), new Color(0.72f, 0.82f, 0.86f, 1f));
            CreateProp("药瓶 B", new Vector3(5.28f, -4.3f, 0.25f), new Vector3(0.06f, 0.06f, 0.12f), new Color(0.82f, 0.36f, 0.36f, 1f));
            CreateProp("隐蔽病历箱", new Vector3(7.22f, -4.48f, 0.06f), new Vector3(0.34f, 0.24f, 0.18f), new Color(0.42f, 0.28f, 0.18f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所急救 SOS 箱", Sprite2DAssetCache.RoomPropSosBox,
                Sprite2DAssetCache.RoomPropSosBoxResourcePath, new Vector3(5.52f, -4.34f, 0.28f),
                new Vector3(0.46f, 0.42f, 0.08f), Color.white);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所监护屏", Sprite2DAssetCache.RoomPropMonitor,
                Sprite2DAssetCache.RoomPropMonitorResourcePath, new Vector3(6.54f, -4.76f, 0.28f),
                new Vector3(0.44f, 0.4f, 0.08f), Color.white, 4f);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所等候椅", Sprite2DAssetCache.RoomPropChair,
                Sprite2DAssetCache.RoomPropChairResourcePath, new Vector3(7.18f, -5.76f, 0.2f),
                new Vector3(0.34f, 0.34f, 0.08f), Color.white, -8f);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所医疗推车", Sprite2DAssetCache.OfficeRoomPropMedicalCart,
                Sprite2DAssetCache.OfficeRoomPropMedicalCartResourcePath, new Vector3(5.94f, -5.12f, 0.34f),
                new Vector3(0.52f, 0.64f, 0.08f), Color.white, 1f);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所角落电脑台", Sprite2DAssetCache.OfficeRoomPropCornerDesk,
                Sprite2DAssetCache.OfficeRoomPropCornerDeskResourcePath, new Vector3(7.0f, -4.62f, 0.3f),
                new Vector3(0.52f, 0.62f, 0.08f), Color.white, -4f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所核磁设备", Sprite2DAssetCache.InteriorRoomPropHospitalResonanceMachine,
                Sprite2DAssetCache.InteriorRoomPropHospitalResonanceMachineResourcePath, new Vector3(5.48f, -5.52f, 0.36f),
                new Vector3(0.64f, 0.7f, 0.08f), Color.white, -2f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所彩色监护屏", Sprite2DAssetCache.InteriorRoomPropHospitalScreenColor,
                Sprite2DAssetCache.InteriorRoomPropHospitalScreenColorResourcePath, new Vector3(6.52f, -4.5f, 0.34f),
                new Vector3(0.42f, 0.44f, 0.08f), Color.white, 3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所 X 光机", Sprite2DAssetCache.InteriorRoomPropHospitalXrayMachine,
                Sprite2DAssetCache.InteriorRoomPropHospitalXrayMachineResourcePath, new Vector3(6.68f, -5.44f, 0.36f),
                new Vector3(0.56f, 0.64f, 0.08f), Color.white, 4f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所医用水槽", Sprite2DAssetCache.InteriorRoomPropHospitalSink,
                Sprite2DAssetCache.InteriorRoomPropHospitalSinkResourcePath, new Vector3(5.16f, -4.74f, 0.34f),
                new Vector3(0.42f, 0.48f, 0.08f), Color.white, -4f);
            CreateLimeZuRoomProp("房间实物 LimeZu 诊所太平柜门", Sprite2DAssetCache.InteriorRoomPropMorgueFreezerCorpseDoor,
                Sprite2DAssetCache.InteriorRoomPropMorgueFreezerCorpseDoorResourcePath, new Vector3(7.42f, -4.98f, 0.32f),
                new Vector3(0.42f, 0.56f, 0.08f), Color.white, 2f, true);
        }

        private void CreateShipFloor()
        {
            Color voidColor = new Color(0.018f, 0.024f, 0.028f, 1f);
            Color hull = new Color(0.07f, 0.086f, 0.094f, 1f);
            Color innerHull = new Color(0.095f, 0.112f, 0.12f, 1f);
            Color sidePod = new Color(0.082f, 0.1f, 0.108f, 1f);

            CreateProp("行动舰外暗区", new Vector3(0f, 0f, -0.39f), new Vector3(26.6f, 16.8f, 0.08f), voidColor);
            CreateShapeProp("行动舰圆角主外壳", RoundedRectSprite, new Vector3(0f, 0f, -0.36f), new Vector3(23.8f, 13.7f, 0.08f), hull);
            CreateShapeProp("行动舰圆角内甲板", RoundedRectSprite, new Vector3(0f, 0f, -0.35f), new Vector3(22.2f, 12.4f, 0.08f), innerHull);
            CreateShapeProp("行动舰左推进舱外壳", RoundedRectSprite, new Vector3(-10.55f, 0.2f, -0.355f), new Vector3(3.0f, 8.8f, 0.08f), sidePod);
            CreateShapeProp("行动舰右推进舱外壳", RoundedRectSprite, new Vector3(10.55f, 0.15f, -0.355f), new Vector3(3.0f, 8.65f, 0.08f), sidePod);
            CreateSolidProp("北侧厚舱壁", new Vector3(0f, 6.62f, -0.12f), new Vector3(21.2f, 0.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));
            CreateSolidProp("南侧厚舱壁", new Vector3(0f, -6.62f, -0.12f), new Vector3(21.2f, 0.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));
            CreateSolidProp("西侧外舱壁", new Vector3(-11.55f, 0f, -0.12f), new Vector3(0.2f, 10.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));
            CreateSolidProp("东侧外舱壁", new Vector3(11.55f, 0f, -0.12f), new Vector3(0.2f, 10.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));

            for (int i = 0; i < 15; i++)
            {
                float x = -10.5f + i * 1.5f;
                CreateProp("行动舰甲板横向拼缝 " + i, new Vector3(x, 6.2f, -0.22f), new Vector3(0.34f, 0.035f, 0.04f), new Color(0.28f, 0.34f, 0.35f, 1f));
                CreateProp("行动舰底舱横向拼缝 " + i, new Vector3(x, -6.18f, -0.22f), new Vector3(0.34f, 0.035f, 0.04f), new Color(0.28f, 0.34f, 0.35f, 1f));
            }
        }

        private void CreateShipCorridors()
        {
            Color main = new Color(0.205f, 0.232f, 0.242f, 1f);
            Color branch = new Color(0.172f, 0.198f, 0.21f, 1f);
            Color trim = new Color(0.48f, 0.56f, 0.56f, 1f);
            Color guide = new Color(0.88f, 0.68f, 0.09f, 1f);

            CreateShipCorridor("中心会议圆舱", new Vector3(0f, -0.35f, -0.21f), new Vector3(3.0f, 2.35f, 0.08f), main, true);
            CreateShipCorridor("主横连廊", new Vector3(0f, -0.18f, -0.24f), new Vector3(15.5f, 1.2f, 0.08f), main, false);
            CreateShipCorridor("上层主连廊", new Vector3(0f, 3.65f, -0.24f), new Vector3(16.4f, 1.04f, 0.08f), branch, false);
            CreateShipCorridor("下层主连廊", new Vector3(0.12f, -3.9f, -0.24f), new Vector3(15.4f, 1.04f, 0.08f), branch, false);
            CreateShipCorridor("左竖连廊", new Vector3(-6.85f, 0.15f, -0.24f), new Vector3(1.08f, 8.35f, 0.08f), branch, false);
            CreateShipCorridor("右竖连廊", new Vector3(7.05f, 0.08f, -0.24f), new Vector3(1.08f, 8.18f, 0.08f), branch, false);
            CreateShipCorridor("中心上连廊", new Vector3(0f, 1.85f, -0.23f), new Vector3(1.08f, 3.15f, 0.08f), main, false);
            CreateShipCorridor("中心下连廊", new Vector3(0f, -2.35f, -0.23f), new Vector3(1.08f, 3.05f, 0.08f), main, false);
            CreateShipCorridor("左上斜接舱", new Vector3(-3.2f, 2.1f, -0.23f), new Vector3(4.35f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("右上斜接舱", new Vector3(3.35f, 2.08f, -0.23f), new Vector3(4.45f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("左下斜接舱", new Vector3(-3.25f, -2.15f, -0.23f), new Vector3(4.25f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("右下斜接舱", new Vector3(3.42f, -2.12f, -0.23f), new Vector3(4.25f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("左侧气闸短廊", new Vector3(-9.3f, -0.18f, -0.24f), new Vector3(3.95f, 0.92f, 0.08f), branch, false);
            CreateShipCorridor("右侧气闸短廊", new Vector3(9.2f, -0.18f, -0.24f), new Vector3(3.7f, 0.92f, 0.08f), branch, false);

            CreateShipNode("西北舱路口", new Vector3(-6.85f, 3.65f, -0.18f), 0.44f, trim);
            CreateShipNode("东北舱路口", new Vector3(7.05f, 3.65f, -0.18f), 0.44f, trim);
            CreateShipNode("西南舱路口", new Vector3(-6.85f, -3.9f, -0.18f), 0.44f, trim);
            CreateShipNode("东南舱路口", new Vector3(7.05f, -3.9f, -0.18f), 0.44f, trim);
            CreateShipNode("会议桌圆环", new Vector3(0f, -0.35f, -0.16f), 0.62f, new Color(0.52f, 0.62f, 0.62f, 1f));

            for (int i = 0; i < 6; i++)
            {
                CreateProp("主走廊导向线 " + i, new Vector3(-5.2f + i * 2.05f, -0.18f, -0.1f), new Vector3(0.78f, 0.055f, 0.05f), guide);
                CreateProp("上层导向线 " + i, new Vector3(-5.3f + i * 2.12f, 3.65f, -0.1f), new Vector3(0.72f, 0.045f, 0.05f), new Color(0.54f, 0.62f, 0.62f, 1f));
                CreateProp("下层导向线 " + i, new Vector3(-5.1f + i * 2.08f, -3.9f, -0.1f), new Vector3(0.72f, 0.045f, 0.05f), new Color(0.54f, 0.62f, 0.62f, 1f));
            }
        }

        private void CreateShipCorridor(string name, Vector3 center, Vector3 size, Color color, bool round)
        {
            GameObject corridor = round
                ? CreateShapeProp(name, RoundedRectSprite, center, size, color)
                : CreateShapeProp(name, RoundedRectSprite, center, size, color);
            corridor.transform.SetAsFirstSibling();
            RegisterWalkableArea(center, size);
        }

        private void CreateShipNode(string name, Vector3 center, float radius, Color color)
        {
            CreateShapeProp(name, CircleSprite, center, new Vector3(radius, radius, 0.08f), color);
            CreateShapeProp(name + " 内圈", CircleSprite, center + new Vector3(0f, 0f, 0.02f), new Vector3(radius * 0.58f, radius * 0.58f, 0.08f), new Color(0.16f, 0.19f, 0.2f, 1f));
            RegisterWalkableArea(center, new Vector3(radius * 1.9f, radius * 1.9f, 0.08f));
        }

        private void CreateCorridorVolumeLayer()
        {
            Color rail = new Color(0.055f, 0.07f, 0.078f, 1f);
            Color trim = new Color(0.48f, 0.56f, 0.56f, 1f);
            Color light = new Color(0.08f, 0.78f, 0.92f, 1f);
            CreateCorridorRails("主横连廊", new Vector3(0f, -0.18f, 0f), 15.3f, true, rail, trim);
            CreateCorridorRails("上层主连廊", new Vector3(0f, 3.65f, 0f), 16.0f, true, rail, trim);
            CreateCorridorRails("下层主连廊", new Vector3(0.12f, -3.9f, 0f), 15.0f, true, rail, trim);
            CreateCorridorRails("左竖连廊", new Vector3(-6.85f, 0.15f, 0f), 8.0f, false, rail, trim);
            CreateCorridorRails("右竖连廊", new Vector3(7.05f, 0.08f, 0f), 7.85f, false, rail, trim);
            CreateCorridorRails("中心上连廊", new Vector3(0f, 1.85f, 0f), 2.8f, false, rail, trim);
            CreateCorridorRails("中心下连廊", new Vector3(0f, -2.35f, 0f), 2.72f, false, rail, trim);

            for (int i = 0; i < 9; i++)
            {
                float x = -7.2f + i * 1.8f;
                CreateMeshBoxProp("屋顶 主走廊顶灯 " + i, new Vector3(x, 0.52f, 0.42f), new Vector3(0.46f, 0.055f, 0.08f), light);
                CreateMeshBoxProp("屋顶 下走廊地灯 " + i, new Vector3(x + 0.28f, -4.42f, 0.28f), new Vector3(0.34f, 0.045f, 0.06f), Darken(light, 0.85f));
                CreateMeshBoxProp("屋顶 上走廊地灯 " + i, new Vector3(x + 0.16f, 4.18f, 0.28f), new Vector3(0.34f, 0.045f, 0.06f), Darken(light, 0.85f));
            }

            CreateMeshPrimitiveProp("屋顶 会议舱圆形投影台", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0.02f), new Vector3(0.92f, 0.03f, 0.92f), new Color(0.42f, 0.48f, 0.48f, 1f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("屋顶 会议舱证据屏 A", new Vector3(-0.64f, 0.22f, 0.38f), new Vector3(0.38f, 0.045f, 0.22f), light);
            CreateMeshBoxProp("屋顶 会议舱证据屏 B", new Vector3(0.64f, 0.22f, 0.38f), new Vector3(0.38f, 0.045f, 0.22f), new Color(0.95f, 0.22f, 0.18f, 1f));
        }

        private void CreateCorridorRails(string name, Vector3 center, float length, bool horizontal, Color rail, Color trim)
        {
            if (horizontal)
            {
                CreateMeshBoxProp("2.5D 建筑体 " + name + " 上沿立体护栏", center + new Vector3(0f, 0.58f, 0.22f), new Vector3(length, 0.08f, 0.22f), rail);
                CreateMeshBoxProp("2.5D 建筑体 " + name + " 下沿立体护栏", center + new Vector3(0f, -0.58f, 0.22f), new Vector3(length, 0.08f, 0.22f), rail);

                for (int i = 0; i < Mathf.CeilToInt(length / 2.1f); i++)
                {
                    float x = -length * 0.5f + 0.8f + i * 2.1f;
                    CreateMeshBoxProp("屋顶 " + name + " 立柱 U" + i, center + new Vector3(x, 0.58f, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
                    CreateMeshBoxProp("屋顶 " + name + " 立柱 D" + i, center + new Vector3(x, -0.58f, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
                }

                return;
            }

            CreateMeshBoxProp("2.5D 建筑体 " + name + " 左沿立体护栏", center + new Vector3(-0.52f, 0f, 0.22f), new Vector3(0.08f, length, 0.22f), rail);
            CreateMeshBoxProp("2.5D 建筑体 " + name + " 右沿立体护栏", center + new Vector3(0.52f, 0f, 0.22f), new Vector3(0.08f, length, 0.22f), rail);

            for (int i = 0; i < Mathf.CeilToInt(length / 1.8f); i++)
            {
                float y = -length * 0.5f + 0.7f + i * 1.8f;
                CreateMeshBoxProp("屋顶 " + name + " 立柱 L" + i, center + new Vector3(-0.52f, y, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
                CreateMeshBoxProp("屋顶 " + name + " 立柱 R" + i, center + new Vector3(0.52f, y, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
            }
        }

        private void CreateShipRooms()
        {
            foreach (OnlineMapService.ShipRoomSpec room in MapService.ShipRooms())
            {
                CreateShipRoom(room);
            }
        }


        private void CreateShipRoom(OnlineMapService.ShipRoomSpec room)
        {
            Color wall = new Color(0.052f, 0.064f, 0.07f, 1f);
            Color trim = new Color(0.62f, 0.62f, 0.54f, 1f);
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;

            CreateShapeProp("2.5D 建筑体 " + room.Name + " 外舱轮廓", RoundedRectSprite, room.Center + new Vector3(0f, 0f, -0.1f), new Vector3(room.Size.x + 0.22f, room.Size.y + 0.22f, 0.08f), wall);
            CreateShapeProp("2.5D 建筑体 " + room.Name + " 圆角房间底", RoundedRectSprite, room.Center + new Vector3(0f, 0f, -0.07f), room.Size, Darken(room.Floor, 0.86f));
            CreateShapeProp("2.5D 建筑体 " + room.Name + " 中央地板", RoundedRectSprite, room.Center + new Vector3(0f, 0f, -0.04f), new Vector3(room.Size.x * 0.9f, room.Size.y * 0.76f, 0.08f), room.Floor);
            CreateRoomVolumeShell(room, wall, trim);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 北厚墙", room.Center + new Vector3(0f, halfHeight - 0.06f, 0.16f), new Vector3(room.Size.x * 0.86f, 0.14f, 0.14f), wall, room.Entrance == OnlineMapService.MapEntrance.North);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 南厚墙", room.Center + new Vector3(0f, -halfHeight + 0.06f, 0.16f), new Vector3(room.Size.x * 0.86f, 0.14f, 0.14f), wall, room.Entrance == OnlineMapService.MapEntrance.South);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 西厚墙", room.Center + new Vector3(-halfWidth + 0.06f, 0f, 0.16f), new Vector3(0.14f, room.Size.y * 0.76f, 0.14f), wall, room.Entrance == OnlineMapService.MapEntrance.West);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 东厚墙", room.Center + new Vector3(halfWidth - 0.06f, 0f, 0.16f), new Vector3(0.14f, room.Size.y * 0.76f, 0.14f), wall, room.Entrance == OnlineMapService.MapEntrance.East);
            CreateProp("屋顶 " + room.Name + " 北舱金属边", room.Center + new Vector3(0f, halfHeight - 0.22f, 0.19f), new Vector3(room.Size.x * 0.58f, 0.055f, 0.08f), trim);
            CreateProp("屋顶 " + room.Name + " 舱门灯带", DoorLightPosition(room), DoorLightScale(room), DoorColor(room));
            CreateWorldLabelAt(room.Label, MapService.ScaleMapPosition(room.Center + new Vector3(0f, halfHeight - 0.34f, -0.17f)), 0.052f);
            CreateRoomFloorTiles(room.Name, room.Center, room.Size, room.Floor);
            CreateRoomFurniture(room);
            RegisterWalkableArea(room.Center, new Vector3(room.Size.x * 0.86f, room.Size.y * 0.7f, 0.08f));
        }

        private void CreateRoomVolumeShell(OnlineMapService.ShipRoomSpec room, Color wall, Color trim)
        {
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;
            float height = RoomVisualHeight(room);
            Color side = Darken(room.Floor, 0.52f);
            Color roof = Darken(room.Floor, 0.74f);
            Color glass = new Color(0.08f, 0.34f, 0.44f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 后立面体", room.Center + new Vector3(0f, halfHeight + 0.12f, height * 0.5f), new Vector3(room.Size.x * 0.92f, 0.16f, height), side);
            CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 左侧立面体", room.Center + new Vector3(-halfWidth - 0.06f, 0f, height * 0.43f), new Vector3(0.14f, room.Size.y * 0.72f, height * 0.86f), Darken(side, 0.86f));
            CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 右侧立面体", room.Center + new Vector3(halfWidth + 0.06f, 0f, height * 0.43f), new Vector3(0.14f, room.Size.y * 0.72f, height * 0.86f), Darken(side, 0.88f));
            CreateMeshBoxProp("屋顶 " + room.Name + " 主板体", room.Center + new Vector3(0f, halfHeight * 0.18f, height + 0.03f), new Vector3(room.Size.x * 0.68f, room.Size.y * 0.26f, 0.08f), roof);
            CreateMeshBoxProp("屋顶 " + room.Name + " 前缘体", room.Center + new Vector3(0f, -halfHeight + 0.1f, height * 0.62f), new Vector3(room.Size.x * 0.48f, 0.12f, height * 0.18f), trim);

            for (int i = 0; i < 3; i++)
            {
                float x = -room.Size.x * 0.25f + i * room.Size.x * 0.25f;
                CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 窗格 " + i, room.Center + new Vector3(x, halfHeight + 0.215f, height * 0.58f), new Vector3(0.34f, 0.035f, 0.18f), glass);
            }

            CreateRooftopKit(room, height);
        }

        private void CreateRooftopKit(OnlineMapService.ShipRoomSpec room, float height)
        {
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;
            Color metal = new Color(0.22f, 0.24f, 0.24f, 1f);
            Color vent = new Color(0.055f, 0.07f, 0.075f, 1f);
            Color light = DoorColor(room);

            CreateMeshBoxProp("屋顶 " + room.Name + " 空调箱", room.Center + new Vector3(-halfWidth * 0.35f, halfHeight * 0.12f, height + 0.14f), new Vector3(0.34f, 0.22f, 0.18f), metal);
            CreateMeshBoxProp("屋顶 " + room.Name + " 风管 A", room.Center + new Vector3(halfWidth * 0.24f, halfHeight * 0.06f, height + 0.12f), new Vector3(0.5f, 0.08f, 0.12f), vent);
            CreateMeshBoxProp("屋顶 " + room.Name + " 风管 B", room.Center + new Vector3(halfWidth * 0.32f, -halfHeight * 0.12f, height + 0.12f), new Vector3(0.08f, 0.36f, 0.12f), vent);
            CreateMeshPrimitiveProp("屋顶 " + room.Name + " 信号灯", PrimitiveType.Cylinder, room.Center + new Vector3(halfWidth * 0.42f, halfHeight * 0.28f, height + 0.22f), new Vector3(0.08f, 0.08f, 0.12f), light, Quaternion.Euler(90f, 0f, 0f));

            if (room.Label.Contains("电力") || room.Label.Contains("监控") || room.Label.Contains("情报"))
            {
                CreateMeshBoxProp("屋顶 " + room.Name + " 天线杆", room.Center + new Vector3(0f, halfHeight * 0.24f, height + 0.32f), new Vector3(0.04f, 0.04f, 0.48f), new Color(0.72f, 0.76f, 0.72f, 1f));
                CreateMeshBoxProp("屋顶 " + room.Name + " 天线横臂", room.Center + new Vector3(0f, halfHeight * 0.24f, height + 0.54f), new Vector3(0.42f, 0.035f, 0.04f), new Color(0.72f, 0.76f, 0.72f, 1f));
            }
        }

        private static float RoomVisualHeight(OnlineMapService.ShipRoomSpec room)
        {
            if (room.Label.Contains("账房") || room.Label.Contains("监控") || room.Label.Contains("电力"))
            {
                return 0.82f;
            }

            if (room.Label.Contains("冷藏") || room.Label.Contains("诊疗") || room.Label.Contains("观测"))
            {
                return 0.72f;
            }

            if (room.Label.Contains("情报") || room.Label.Contains("黑市"))
            {
                return 0.58f;
            }

            return 0.66f;
        }

        private void CreateWallSegmentWithDoor(string wallName, Vector3 position, Vector3 scale, Color color, bool hasDoor)
        {
            if (!hasDoor)
            {
                CreateWallSegment(wallName, position, scale, color);
                return;
            }

            bool horizontal = scale.x >= scale.y;
            float length = horizontal ? scale.x : scale.y;
            float gap = Mathf.Clamp(length * 0.36f, 0.64f, 0.95f);
            float segmentLength = Mathf.Max(0.12f, (length - gap) * 0.5f);

            if (horizontal)
            {
                float offset = gap * 0.5f + segmentLength * 0.5f;
                CreateWallSegment(wallName + " L", position + new Vector3(-offset, 0f, 0f), new Vector3(segmentLength, scale.y, scale.z), color);
                CreateWallSegment(wallName + " R", position + new Vector3(offset, 0f, 0f), new Vector3(segmentLength, scale.y, scale.z), color);
                return;
            }

            float verticalOffset = gap * 0.5f + segmentLength * 0.5f;
            CreateWallSegment(wallName + " B", position + new Vector3(0f, -verticalOffset, 0f), new Vector3(scale.x, segmentLength, scale.z), color);
            CreateWallSegment(wallName + " T", position + new Vector3(0f, verticalOffset, 0f), new Vector3(scale.x, segmentLength, scale.z), color);
        }

        private static Vector3 DoorLightPosition(OnlineMapService.ShipRoomSpec room)
        {
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;

            switch (room.Entrance)
            {
                case OnlineMapService.MapEntrance.North:
                    return room.Center + new Vector3(0f, halfHeight - 0.12f, 0.22f);
                case OnlineMapService.MapEntrance.South:
                    return room.Center + new Vector3(0f, -halfHeight + 0.12f, 0.22f);
                case OnlineMapService.MapEntrance.East:
                    return room.Center + new Vector3(halfWidth - 0.12f, 0f, 0.22f);
                default:
                    return room.Center + new Vector3(-halfWidth + 0.12f, 0f, 0.22f);
            }
        }

        private static Vector3 DoorLightScale(OnlineMapService.ShipRoomSpec room)
        {
            if (room.Entrance == OnlineMapService.MapEntrance.North || room.Entrance == OnlineMapService.MapEntrance.South)
            {
                return new Vector3(Mathf.Min(room.Size.x * 0.42f, 1.25f), 0.07f, 0.08f);
            }

            return new Vector3(0.07f, Mathf.Min(room.Size.y * 0.42f, 0.86f), 0.08f);
        }

        private static Color DoorColor(OnlineMapService.ShipRoomSpec room)
        {
            if (room.Label.Contains("情报") || room.Label.Contains("黑市"))
            {
                return new Color(0.95f, 0.18f, 0.32f, 1f);
            }

            if (room.Label.Contains("账房") || room.Label.Contains("指挥"))
            {
                return new Color(0.32f, 0.68f, 1f, 1f);
            }

            if (room.Label.Contains("诊疗") || room.Label.Contains("冷藏"))
            {
                return new Color(0.55f, 0.82f, 0.76f, 1f);
            }

            return new Color(0.95f, 0.72f, 0.1f, 1f);
        }

        private void CreateRoomFurniture(OnlineMapService.ShipRoomSpec room)
        {
            Color metal = new Color(0.08f, 0.1f, 0.11f, 1f);
            Color screen = new Color(0.06f, 0.62f, 0.78f, 1f);
            Color warning = new Color(0.9f, 0.68f, 0.08f, 1f);

            switch (room.Name)
            {
                case "西码头货柜场":
                    CreateWallConsoleSet(room, 0);
                    CreateContainerRack(room.Center + new Vector3(-0.72f, 0.3f, 0.06f), 0);
                    CreateContainerRack(room.Center + new Vector3(0.75f, -0.32f, 0.06f), 2);
                    CreateSolidProp("货柜舱封锁箱", room.Center + new Vector3(0.4f, 0.56f, 0.06f), new Vector3(0.62f, 0.28f, 0.2f), new Color(0.78f, 0.55f, 0.08f, 1f));
                    CreateSolidProp("货柜舱吊臂基座", room.Center + new Vector3(1.55f, 0.42f, 0.08f), new Vector3(0.28f, 0.74f, 0.22f), warning);
                    CreateProp("货柜舱吊臂横梁", room.Center + new Vector3(1.2f, 0.72f, 0.2f), new Vector3(0.92f, 0.08f, 0.08f), warning);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 货柜舱购物车", Sprite2DAssetCache.InteriorRoomPropShoppingCartBlueFull,
                        Sprite2DAssetCache.InteriorRoomPropShoppingCartBlueFullResourcePath, room.Center, new Vector3(-1.22f, -0.44f, 0.28f),
                        new Vector3(0.34f, 0.42f, 0.08f), -6f);
                    break;
                case "海关查验区":
                    CreateWallConsoleSet(room, 1);
                    CreateSolidProp("查验舱扫描门", room.Center + new Vector3(0.82f, 0.15f, 0.1f), new Vector3(0.14f, 0.82f, 0.28f), metal);
                    CreateSolidProp("查验舱检查桌", room.Center + new Vector3(-0.46f, 0.12f, 0.07f), new Vector3(0.8f, 0.34f, 0.18f), new Color(0.24f, 0.26f, 0.2f, 1f));
                    CreateProp("查验舱屏幕", room.Center + new Vector3(-0.46f, 0.38f, 0.2f), new Vector3(0.46f, 0.06f, 0.08f), screen);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 查验舱票据机", Sprite2DAssetCache.InteriorRoomPropTicketMachine,
                        Sprite2DAssetCache.InteriorRoomPropTicketMachineResourcePath, room.Center, new Vector3(-0.92f, -0.42f, 0.28f),
                        new Vector3(0.34f, 0.42f, 0.08f), -2f);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 查验舱滚台", Sprite2DAssetCache.InteriorRoomPropGroceryCheckoutRoller,
                        Sprite2DAssetCache.InteriorRoomPropGroceryCheckoutRollerResourcePath, room.Center, new Vector3(0.32f, -0.34f, 0.28f),
                        new Vector3(0.48f, 0.38f, 0.08f), 2f);
                    break;
                case "监控室":
                    CreateWallConsoleSet(room, 2);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateProp("监控墙屏 " + i, room.Center + new Vector3(-0.62f + i * 0.48f, 0.45f, 0.18f), new Vector3(0.36f, 0.08f, 0.16f), screen);
                    }

                    CreateSolidProp("监控操控台", room.Center + new Vector3(-0.15f, -0.18f, 0.07f), new Vector3(0.92f, 0.28f, 0.18f), metal);
                    CreateLimeZuRoomProp("房间实物 LimeZu 舰内监控双屏工作站", Sprite2DAssetCache.OfficeRoomPropDualMonitorDesk,
                        Sprite2DAssetCache.OfficeRoomPropDualMonitorDeskResourcePath, room.Center + new Vector3(-0.18f, -0.14f, 0.3f),
                        new Vector3(0.5f, 0.58f, 0.08f), Color.white, -1f);
                    CreateLimeZuRoomProp("房间实物 LimeZu 舰内监控服务器柜", Sprite2DAssetCache.OfficeRoomPropServerRack,
                        Sprite2DAssetCache.OfficeRoomPropServerRackResourcePath, room.Center + new Vector3(0.82f, 0.18f, 0.32f),
                        new Vector3(0.36f, 0.56f, 0.08f), Color.white, 2f);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内监控安防摄像头", Sprite2DAssetCache.InteriorRoomPropSecurityCameraWallRight,
                        Sprite2DAssetCache.InteriorRoomPropSecurityCameraWallRightResourcePath, room.Center, new Vector3(-1.0f, 0.56f, 0.34f),
                        new Vector3(0.26f, 0.3f, 0.08f), 8f);
                    break;
                case "茶餐厅":
                    CreateWallConsoleSet(room, 3);
                    CreateSolidProp("休息舱吧台", room.Center + new Vector3(-0.78f, 0.08f, 0.07f), new Vector3(0.28f, 1.0f, 0.18f), new Color(0.46f, 0.25f, 0.12f, 1f));
                    CreateBoothSet(room.Center + new Vector3(0.34f, 0.38f, 0.06f), "上");
                    CreateBoothSet(room.Center + new Vector3(0.34f, -0.34f, 0.06f), "下");
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内茶餐厅冰柜", Sprite2DAssetCache.InteriorRoomPropCanteenCakeFridge,
                        Sprite2DAssetCache.InteriorRoomPropCanteenCakeFridgeResourcePath, room.Center, new Vector3(-1.0f, -0.44f, 0.28f),
                        new Vector3(0.42f, 0.54f, 0.08f), 2f);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内茶餐厅水槽", Sprite2DAssetCache.InteriorRoomPropKitchenSink,
                        Sprite2DAssetCache.InteriorRoomPropKitchenSinkResourcePath, room.Center, new Vector3(-0.7f, 0.58f, 0.28f),
                        new Vector3(0.32f, 0.36f, 0.08f), -4f);
                    break;
                case "夜市主街":
                    CreateWallConsoleSet(room, 4);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateSolidProp("情报摊台 " + i, room.Center + new Vector3(-1.15f + i * 1.05f, 0.34f, 0.07f), new Vector3(0.62f, 0.26f, 0.18f), i % 2 == 0 ? new Color(0.62f, 0.12f, 0.1f, 1f) : new Color(0.12f, 0.36f, 0.4f, 1f));
                        CreateProp("情报霓虹牌 " + i, room.Center + new Vector3(-1.15f + i * 1.05f, 0.56f, 0.2f), new Vector3(0.5f, 0.05f, 0.08f), i % 2 == 0 ? new Color(0.96f, 0.22f, 0.52f, 1f) : screen);
                    }
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内夜市烧烤炉", Sprite2DAssetCache.InteriorRoomPropKitchenBbq,
                        Sprite2DAssetCache.InteriorRoomPropKitchenBbqResourcePath, room.Center, new Vector3(0.96f, -0.32f, 0.28f),
                        new Vector3(0.42f, 0.48f, 0.08f), 4f);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内夜市鱼档", Sprite2DAssetCache.InteriorRoomPropFishCuttingSink,
                        Sprite2DAssetCache.InteriorRoomPropFishCuttingSinkResourcePath, room.Center, new Vector3(-1.1f, -0.32f, 0.28f),
                        new Vector3(0.38f, 0.42f, 0.08f), -5f);
                    break;
                case "金融楼":
                    CreateWallConsoleSet(room, 5);
                    CreateSolidProp("账房保险柜", room.Center + new Vector3(0.92f, -0.24f, 0.09f), new Vector3(0.48f, 0.46f, 0.28f), new Color(0.18f, 0.18f, 0.22f, 1f));
                    CreateSolidProp("账房桌", room.Center + new Vector3(-0.34f, 0.12f, 0.07f), new Vector3(0.86f, 0.3f, 0.18f), new Color(0.26f, 0.22f, 0.18f, 1f));
                    CreateProp("账房现金条", room.Center + new Vector3(-0.08f, 0.34f, 0.2f), new Vector3(0.52f, 0.05f, 0.06f), new Color(0.18f, 0.58f, 0.25f, 1f));
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内账房现金保险柜", Sprite2DAssetCache.InteriorRoomPropSafeBucks,
                        Sprite2DAssetCache.InteriorRoomPropSafeBucksResourcePath, room.Center, new Vector3(0.82f, -0.18f, 0.3f),
                        new Vector3(0.4f, 0.4f, 0.08f), 2f, true);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内账房金条柜", Sprite2DAssetCache.InteriorRoomPropSafeGold,
                        Sprite2DAssetCache.InteriorRoomPropSafeGoldResourcePath, room.Center, new Vector3(1.18f, 0.34f, 0.3f),
                        new Vector3(0.34f, 0.36f, 0.08f), -4f);
                    break;
                case "电房":
                    CreateWallConsoleSet(room, 6);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateSolidProp("电力舱变压器 " + i, room.Center + new Vector3(-0.64f + i * 0.5f, 0.28f, 0.08f), new Vector3(0.32f, 0.46f, 0.28f), new Color(0.18f, 0.24f, 0.34f, 1f));
                        CreateProp("电力舱指示灯 " + i, room.Center + new Vector3(-0.64f + i * 0.5f, 0.54f, 0.22f), new Vector3(0.06f, 0.04f, 0.05f), i == 1 ? Color.red : Color.green);
                    }

                    CreateProp("电力舱黄黑警戒线", room.Center + new Vector3(0f, -0.46f, 0.1f), new Vector3(1.45f, 0.08f, 0.08f), warning);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内电力检修陷门", Sprite2DAssetCache.InteriorRoomPropTrapdoor,
                        Sprite2DAssetCache.InteriorRoomPropTrapdoorResourcePath, room.Center, new Vector3(0.72f, -0.48f, 0.22f),
                        new Vector3(0.34f, 0.34f, 0.08f), 1f);
                    break;
                case "天台通道":
                    CreateWallConsoleSet(room, 7);
                    CreateSolidProp("观测舱望远镜座", room.Center + new Vector3(-0.28f, 0f, 0.08f), new Vector3(0.5f, 0.22f, 0.18f), metal);
                    CreateProp("观测舱镜筒", room.Center + new Vector3(0.08f, 0.02f, 0.2f), new Vector3(0.42f, 0.08f, 0.08f), screen);
                    CreateProp("观测舱气象屏", room.Center + new Vector3(0.82f, 0.38f, 0.18f), new Vector3(0.44f, 0.06f, 0.14f), screen);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内天台旧电视", Sprite2DAssetCache.InteriorRoomPropOldTv,
                        Sprite2DAssetCache.InteriorRoomPropOldTvResourcePath, room.Center, new Vector3(0.76f, -0.32f, 0.28f),
                        new Vector3(0.3f, 0.32f, 0.08f), -5f);
                    break;
                case "指挥车广场":
                    CreateWallConsoleSet(room, 8);
                    CreateShapeProp("指挥舱圆桌", CircleSprite, room.Center + new Vector3(0f, -0.02f, 0.08f), new Vector3(1.0f, 0.62f, 0.12f), new Color(0.5f, 0.52f, 0.48f, 1f));
                    CreateSolidProp("行动白板", room.Center + new Vector3(-1.42f, 0.18f, 0.08f), new Vector3(0.54f, 0.16f, 0.22f), new Color(0.82f, 0.86f, 0.82f, 1f));
                    CreateProp("指挥警灯条", room.Center + new Vector3(1.35f, 0.18f, 0.14f), new Vector3(0.82f, 0.08f, 0.08f), new Color(0.12f, 0.32f, 0.96f, 1f));
                    CreateLimeZuRoomProp("房间实物 LimeZu 舰内指挥白板", Sprite2DAssetCache.OfficeRoomPropWhiteboard,
                        Sprite2DAssetCache.OfficeRoomPropWhiteboardResourcePath, room.Center + new Vector3(-1.22f, 0.3f, 0.3f),
                        new Vector3(0.48f, 0.52f, 0.08f), Color.white, -4f);
                    CreateLimeZuRoomProp("房间实物 LimeZu 舰内指挥图表板", Sprite2DAssetCache.OfficeRoomPropChartBoard,
                        Sprite2DAssetCache.OfficeRoomPropChartBoardResourcePath, room.Center + new Vector3(0.92f, 0.32f, 0.3f),
                        new Vector3(0.44f, 0.5f, 0.08f), Color.white, 3f);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内指挥激光防线", Sprite2DAssetCache.InteriorRoomPropMuseumLaserHorizontal,
                        Sprite2DAssetCache.InteriorRoomPropMuseumLaserHorizontalResourcePath, room.Center, new Vector3(0.0f, -0.46f, 0.28f),
                        new Vector3(0.44f, 0.2f, 0.08f), 0f);
                    break;
                case "证物库":
                    CreateWallConsoleSet(room, 9);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateSolidProp("证物舱货架 " + i, room.Center + new Vector3(-0.88f + i * 0.56f, 0.34f, 0.08f), new Vector3(0.34f, 0.18f, 0.24f), new Color(0.24f, 0.22f, 0.18f, 1f));
                    }

                    CreateSolidProp("证物舱冷柜", room.Center + new Vector3(0.62f, -0.3f, 0.07f), new Vector3(0.62f, 0.34f, 0.24f), new Color(0.16f, 0.34f, 0.38f, 1f));
                    CreateLimeZuRoomProp("房间实物 LimeZu 舰内证物打印机", Sprite2DAssetCache.OfficeRoomPropPrinter,
                        Sprite2DAssetCache.OfficeRoomPropPrinterResourcePath, room.Center + new Vector3(-0.92f, -0.32f, 0.28f),
                        new Vector3(0.32f, 0.4f, 0.08f), Color.white, -3f);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内证物太平柜", Sprite2DAssetCache.InteriorRoomPropMorgueFreezerCorpseDoor,
                        Sprite2DAssetCache.InteriorRoomPropMorgueFreezerCorpseDoorResourcePath, room.Center, new Vector3(0.68f, -0.28f, 0.3f),
                        new Vector3(0.34f, 0.46f, 0.08f), 3f, true);
                    break;
                case "后巷排档":
                    CreateWallConsoleSet(room, 10);
                    CreateSolidProp("维修舱炉台", room.Center + new Vector3(-0.48f, 0.24f, 0.07f), new Vector3(0.52f, 0.28f, 0.2f), metal);
                    CreatePrimitiveProp("维修舱煤气瓶 A", PrimitiveType.Cylinder, room.Center + new Vector3(0.28f, 0.34f, 0.08f), new Vector3(0.11f, 0.16f, 0.11f), new Color(0.18f, 0.42f, 0.42f, 1f));
                    CreateSolidProp("维修舱摩托", room.Center + new Vector3(0.82f, -0.34f, 0.06f), new Vector3(0.66f, 0.18f, 0.16f), new Color(0.08f, 0.08f, 0.1f, 1f));
                    CreateProp("维修舱火苗", room.Center + new Vector3(-0.48f, 0.42f, 0.2f), new Vector3(0.16f, 0.08f, 0.08f), new Color(1f, 0.28f, 0.06f, 1f));
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内后巷冰柜", Sprite2DAssetCache.InteriorRoomPropGroceryGlassFridge,
                        Sprite2DAssetCache.InteriorRoomPropGroceryGlassFridgeResourcePath, room.Center, new Vector3(-0.96f, -0.36f, 0.28f),
                        new Vector3(0.34f, 0.46f, 0.08f), -2f, true);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内后巷挂肉", Sprite2DAssetCache.InteriorRoomPropButcherCarcass,
                        Sprite2DAssetCache.InteriorRoomPropButcherCarcassResourcePath, room.Center, new Vector3(0.14f, 0.56f, 0.3f),
                        new Vector3(0.3f, 0.38f, 0.08f), 4f);
                    break;
                case "地下诊所":
                    CreateWallConsoleSet(room, 11);
                    CreateSolidProp("诊疗舱病床 A", room.Center + new Vector3(-0.58f, -0.22f, 0.07f), new Vector3(0.7f, 0.34f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
                    CreateSolidProp("诊疗舱病床 B", room.Center + new Vector3(0.52f, -0.22f, 0.07f), new Vector3(0.7f, 0.34f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
                    CreateSolidProp("诊疗舱药柜", room.Center + new Vector3(-1.18f, 0.32f, 0.09f), new Vector3(0.34f, 0.3f, 0.24f), new Color(0.2f, 0.34f, 0.28f, 1f));
                    CreateProp("诊疗舱手术灯", room.Center + new Vector3(0.05f, 0.46f, 0.22f), new Vector3(0.34f, 0.06f, 0.08f), new Color(0.9f, 0.86f, 0.68f, 1f));
                    CreateLimeZuRoomProp("房间实物 LimeZu 舰内诊疗推车", Sprite2DAssetCache.OfficeRoomPropMedicalCart,
                        Sprite2DAssetCache.OfficeRoomPropMedicalCartResourcePath, room.Center + new Vector3(1.12f, 0.14f, 0.3f),
                        new Vector3(0.42f, 0.54f, 0.08f), Color.white, 2f);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内诊疗核磁", Sprite2DAssetCache.InteriorRoomPropHospitalResonanceMachine,
                        Sprite2DAssetCache.InteriorRoomPropHospitalResonanceMachineResourcePath, room.Center, new Vector3(-0.52f, -0.2f, 0.32f),
                        new Vector3(0.46f, 0.52f, 0.08f), -2f, true);
                    CreateLimeZuRoomPropAt("房间实物 LimeZu 舰内诊疗 X 光屏", Sprite2DAssetCache.InteriorRoomPropHospitalXrayScreen,
                        Sprite2DAssetCache.InteriorRoomPropHospitalXrayScreenResourcePath, room.Center, new Vector3(0.42f, 0.38f, 0.32f),
                        new Vector3(0.36f, 0.42f, 0.08f), 3f);
                    break;
            }
        }

        private void CreateWallConsoleSet(OnlineMapService.ShipRoomSpec room, int seed)
        {
            Color body = seed % 2 == 0 ? new Color(0.08f, 0.15f, 0.18f, 1f) : new Color(0.16f, 0.12f, 0.16f, 1f);
            Color screen = seed % 3 == 0 ? new Color(0.05f, 0.68f, 0.82f, 1f) : new Color(0.2f, 0.78f, 0.56f, 1f);
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;

            CreateSolidProp("舱室边柜 " + room.Name + " A", room.Center + new Vector3(-halfWidth * 0.58f, halfHeight * 0.22f, 0.08f), new Vector3(0.28f, 0.34f, 0.22f), body);
            CreateProp("舱室边柜屏 " + room.Name + " A", room.Center + new Vector3(-halfWidth * 0.58f, halfHeight * 0.42f, 0.22f), new Vector3(0.2f, 0.045f, 0.06f), screen);
            CreateSolidProp("舱室边柜 " + room.Name + " B", room.Center + new Vector3(halfWidth * 0.58f, -halfHeight * 0.2f, 0.08f), new Vector3(0.28f, 0.34f, 0.22f), body);
            CreateProp("舱室边柜屏 " + room.Name + " B", room.Center + new Vector3(halfWidth * 0.58f, 0f, 0.22f), new Vector3(0.2f, 0.045f, 0.06f), screen);
            CreateLimeZuRoomProp("房间实物 LimeZu 舱室通用边柜 " + room.Name + " A", Sprite2DAssetCache.InteriorRoomPropJailLockerFull,
                Sprite2DAssetCache.InteriorRoomPropJailLockerFullResourcePath, room.Center + new Vector3(-halfWidth * 0.58f, halfHeight * 0.22f, 0.3f),
                new Vector3(0.28f, 0.38f, 0.08f), Color.white, -2f);
            CreateLimeZuRoomProp("房间实物 LimeZu 舱室通用监控屏 " + room.Name + " B", Sprite2DAssetCache.InteriorRoomPropHospitalScreenColor,
                Sprite2DAssetCache.InteriorRoomPropHospitalScreenColorResourcePath, room.Center + new Vector3(halfWidth * 0.58f, -halfHeight * 0.2f, 0.3f),
                new Vector3(0.28f, 0.34f, 0.08f), Color.white, 2f);
            CreateProp("屋顶 " + room.Name + " 线缆槽 A", room.Center + new Vector3(0f, halfHeight * 0.34f, 0.12f), new Vector3(room.Size.x * 0.32f, 0.035f, 0.06f), new Color(0.04f, 0.055f, 0.06f, 1f));
            CreateProp("屋顶 " + room.Name + " 线缆槽 B", room.Center + new Vector3(0f, -halfHeight * 0.34f, 0.12f), new Vector3(room.Size.x * 0.3f, 0.035f, 0.06f), new Color(0.04f, 0.055f, 0.06f, 1f));
        }

        private void CreateContainerRack(Vector3 center, int seed)
        {
            for (int i = 0; i < 3; i++)
            {
                bool oldCrate = (seed + i) % 2 == 1;
                Sprite sprite = oldCrate ? Sprite2DAssetCache.PropKowloonCrate : Sprite2DAssetCache.PropCrate;
                string resourcePath = oldCrate ? Sprite2DAssetCache.KowloonPropCrateResourcePath : Sprite2DAssetCache.PropCrateResourcePath;
                CreateRuntimeMapProp("地图小件 货柜舱迷你货柜 " + seed + "-" + i, sprite, resourcePath,
                    center + new Vector3(-0.42f + i * 0.42f, 0f, 0f), new Vector3(0.34f, 0.24f, 0.18f),
                    Color.white, i % 2 == 0 ? -3f : 4f, true);
            }
        }

        private void CreateBoothSet(Vector3 center, string suffix)
        {
            CreateSolidProp("休息舱餐桌 " + suffix, center, new Vector3(0.44f, 0.2f, 0.14f), new Color(0.58f, 0.36f, 0.18f, 1f));
            CreateProp("休息舱座椅 L " + suffix, center + new Vector3(-0.34f, 0f, 0.05f), new Vector3(0.18f, 0.18f, 0.08f), new Color(0.22f, 0.16f, 0.28f, 1f));
            CreateProp("休息舱座椅 R " + suffix, center + new Vector3(0.34f, 0f, 0.05f), new Vector3(0.18f, 0.18f, 0.08f), new Color(0.22f, 0.16f, 0.28f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 休息舱餐桌 " + suffix, Sprite2DAssetCache.RoomPropBenchedTable,
                Sprite2DAssetCache.RoomPropBenchedTableResourcePath, center + new Vector3(0f, 0f, 0.24f),
                new Vector3(0.42f, 0.38f, 0.08f), Color.white, suffix == "上" ? 3f : -3f);
            CreateLimeZuRoomProp("房间实物 LimeZu 休息舱旧电视 " + suffix, Sprite2DAssetCache.InteriorRoomPropOldTv,
                Sprite2DAssetCache.InteriorRoomPropOldTvResourcePath, center + new Vector3(0f, 0.24f, 0.28f),
                new Vector3(0.28f, 0.3f, 0.08f), Color.white);
        }

        private void CreateShipRoomFrames()
        {
            foreach (OnlineMapService.ShipRoomSpec room in MapService.ShipRooms())
            {
                CreateDoorMarker(room.Label + " 气闸门", DoorLightPosition(room), DoorLightScale(room), DoorColor(room));
            }
        }

        private void CreateTaskConsole(string name, Vector3 position, int index)
        {
            Color baseColor = index % 3 == 0 ? new Color(0.08f, 0.16f, 0.18f, 1f) : index % 3 == 1 ? new Color(0.16f, 0.13f, 0.18f, 1f) : new Color(0.16f, 0.12f, 0.08f, 1f);
            string modelPath = index % 4 == 0 ? "Props/Prop_AccessPoint.fbx" : "Props/Prop_Computer.fbx";
            CreateSolidModelProp(name + " CC0 控制台", modelPath, position + new Vector3(0f, 0f, 0.04f), new Vector3(0.55f, 0.38f, 0.3f), index % 2 == 0 ? 0f : 180f);
            CreateSolidProp(name + " 底座碰撞体", position, new Vector3(0.44f, 0.28f, 0.14f), new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f));
            CreateMeshBoxProp(name + " 立体台座", position + new Vector3(0f, 0f, 0.14f), new Vector3(0.5f, 0.34f, 0.22f), baseColor);
            CreateMeshBoxProp(name + " 立体斜屏", position + new Vector3(0f, 0.18f, 0.34f), new Vector3(0.38f, 0.05f, 0.18f), new Color(0.05f, 0.72f, 0.86f, 1f));
            CreateMeshPrimitiveProp(name + " 实体状态灯", PrimitiveType.Cylinder, position + new Vector3(0.22f, -0.12f, 0.36f), new Vector3(0.06f, 0.06f, 0.08f), new Color(0.95f, 0.72f, 0.1f, 1f), Quaternion.Euler(90f, 0f, 0f));
            CreateProp(name + " 屏幕发光层", position + new Vector3(0f, 0.16f, 0.12f), new Vector3(0.32f, 0.06f, 0.08f), new Color(0.05f, 0.72f, 0.86f, 1f));
            CreateShapeProp(name + " 状态灯", CircleSprite, position + new Vector3(0.2f, -0.1f, 0.16f), new Vector3(0.08f, 0.08f, 0.05f), new Color(0.95f, 0.72f, 0.1f, 1f));
        }

        private void CreateShipAmbientDressing()
        {
            CreateVentGrate("中心暗线通风口", new Vector3(-3.2f, 0.32f, -0.05f));
            CreateVentGrate("东侧暗线通风口", new Vector3(4.9f, -0.32f, -0.05f));
            CreateVentGrate("南舱暗线通风口", new Vector3(-1.2f, -3.98f, -0.05f));
            CreateVentGrate("西侧主通风口", new Vector3(-6.85f, -1.2f, -0.05f));
            CreateVentGrate("右上主通风口", new Vector3(7.05f, 2.45f, -0.05f));
            CreateShapeProp("会议圆桌", CircleSprite, new Vector3(0f, -0.35f, 0.08f), new Vector3(1.15f, 0.72f, 0.12f), new Color(0.42f, 0.45f, 0.4f, 1f));
            CreateSolidProp("会议桌证物箱", new Vector3(0.55f, -0.36f, 0.12f), new Vector3(0.34f, 0.2f, 0.16f), new Color(0.16f, 0.12f, 0.08f, 1f));
            CreateSolidProp("会议桌档案箱", new Vector3(-0.55f, -0.39f, 0.12f), new Vector3(0.34f, 0.2f, 0.16f), new Color(0.08f, 0.14f, 0.18f, 1f));
            CreatePrimitiveProp("会议桌红灯", PrimitiveType.Sphere, new Vector3(-0.15f, -0.02f, 0.12f), new Vector3(0.08f, 0.08f, 0.08f), new Color(0.9f, 0.08f, 0.06f, 1f));
            CreatePrimitiveProp("会议桌蓝灯", PrimitiveType.Sphere, new Vector3(0.15f, -0.02f, 0.12f), new Vector3(0.08f, 0.08f, 0.08f), new Color(0.08f, 0.35f, 0.95f, 1f));
            CreateProp("会议座位弧 L", new Vector3(-0.92f, -0.35f, 0.1f), new Vector3(0.34f, 0.18f, 0.08f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("会议座位弧 R", new Vector3(0.92f, -0.35f, 0.1f), new Vector3(0.34f, 0.18f, 0.08f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("屋顶 舰桥指挥铭牌", new Vector3(0f, 0.92f, 0.18f), new Vector3(1.2f, 0.08f, 0.08f), new Color(0.42f, 0.72f, 0.84f, 1f));
            CreateLimeZuRoomProp("房间实物 LimeZu 舰桥会议旧电视", Sprite2DAssetCache.InteriorRoomPropOldTv,
                Sprite2DAssetCache.InteriorRoomPropOldTvResourcePath, new Vector3(-0.62f, 0.18f, 0.3f),
                new Vector3(0.32f, 0.34f, 0.08f), Color.white, -6f);
            CreateLimeZuRoomProp("房间实物 LimeZu 舰桥案件图表板", Sprite2DAssetCache.OfficeRoomPropChartBoard,
                Sprite2DAssetCache.OfficeRoomPropChartBoardResourcePath, new Vector3(0.62f, 0.18f, 0.3f),
                new Vector3(0.34f, 0.4f, 0.08f), Color.white, 5f);
            CreateLimeZuRoomProp("房间实物 LimeZu 舰桥储物柜", Sprite2DAssetCache.InteriorRoomPropJailLockerFull,
                Sprite2DAssetCache.InteriorRoomPropJailLockerFullResourcePath, new Vector3(-1.92f, 0.82f, 0.3f),
                new Vector3(0.34f, 0.46f, 0.08f), Color.white, -2f, true);
            CreateLimeZuRoomProp("房间实物 LimeZu 舰桥暗线陷门", Sprite2DAssetCache.InteriorRoomPropTrapdoor,
                Sprite2DAssetCache.InteriorRoomPropTrapdoorResourcePath, new Vector3(1.8f, -0.78f, 0.2f),
                new Vector3(0.36f, 0.34f, 0.08f), Color.white, 2f);

            for (int i = 0; i < 10; i++)
            {
                float x = -10f + i * 2.2f;
                CreateProp("舱壁铆钉列 " + i, new Vector3(x, 6.75f, 0.04f), new Vector3(0.12f, 0.05f, 0.05f), new Color(0.48f, 0.54f, 0.54f, 1f));
                CreateProp("南舱铆钉列 " + i, new Vector3(x, -6.85f, 0.04f), new Vector3(0.12f, 0.05f, 0.05f), new Color(0.48f, 0.54f, 0.54f, 1f));
            }

            CreateCorridorServiceProps();
        }

        private void CreateDenseMapMicroDressing()
        {
            CreateCorridorFloorPanels();
            CreateCorridorCameraNetwork();
            CreateCorridorCableRuns();
            CreateRoomMicroProps();
            CreateExteriorHullProps();
        }

        private void CreatePlayableScaleSetDressing()
        {
            CreateMainCorridorSetPieces();
            CreateRoomForegroundSilhouettes();
            CreateActionCameraForegroundOccluders();
            CreateDistrictHeroSetPieces();
            CreateLowerDeckActivitySets();
            CreateOrganicRouteLanguage();
            CreatePremiumTaskSetPieces();
            CreateMatureDockyardSetPieces();
            CreateTaskInteractionHalos();
            CreateEmergencyMeetingTableSet();
            CreatePhysicsCollisionMarkers();
            CreateActionViewShowcaseLayer();
        }

        private void CreateActionViewShowcaseLayer()
        {
            Color floor = new Color(0.082f, 0.096f, 0.1f, 1f);
            Color wall = new Color(0.018f, 0.026f, 0.03f, 1f);
            Color glass = new Color(0.08f, 0.44f, 0.52f, 0.92f);
            Color police = new Color(0.08f, 0.32f, 0.92f, 1f);
            Color gang = new Color(0.84f, 0.08f, 0.06f, 1f);
            Color amber = new Color(0.94f, 0.7f, 0.12f, 1f);
            Color paper = new Color(0.82f, 0.82f, 0.72f, 1f);
            Color shadow = new Color(0f, 0f, 0f, 0.58f);

            CreateShapeProp("行动视角样板层 中央会议圆形地毯", CircleSprite, new Vector3(0f, -0.35f, -0.025f), new Vector3(1.62f, 1.02f, 0.05f), new Color(0.14f, 0.17f, 0.18f, 1f));
            CreateMeshPrimitiveProp("行动视角样板层 中央会议投票圆桌", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0.16f), new Vector3(0.72f, 0.05f, 0.72f), new Color(0.32f, 0.36f, 0.34f, 1f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("行动视角样板层 圆桌证据蓝线", new Vector3(-0.14f, -0.11f, 0.34f), new Vector3(0.86f, 0.035f, 0.05f), glass, 6f);
            CreateMeshBoxProp("行动视角样板层 圆桌嫌疑红线", new Vector3(0.18f, -0.56f, 0.34f), new Vector3(0.58f, 0.035f, 0.05f), gang, -12f);

            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                Vector3 seat = new Vector3(Mathf.Cos(angle) * 1.24f, -0.35f + Mathf.Sin(angle) * 0.72f, 0.13f);
                CreateMeshPrimitiveProp("行动视角样板层 会议座位尺度点 " + i, PrimitiveType.Cylinder, seat, new Vector3(0.16f, 0.035f, 0.16f), i % 2 == 0 ? police : gang, Quaternion.Euler(90f, 0f, 0f));
            }

            (string name, Vector3 center, Vector3 size, Color color)[] roomSlices =
            {
                ("监控室近景切片", new Vector3(-2.85f, 0.92f, 0f), new Vector3(2.45f, 1.18f, 0.08f), new Color(0.1f, 0.18f, 0.24f, 1f)),
                ("茶餐厅近景切片", new Vector3(-3.92f, -1.58f, 0f), new Vector3(2.15f, 1.04f, 0.08f), new Color(0.3f, 0.17f, 0.1f, 1f)),
                ("情报夜市近景切片", new Vector3(1.72f, 1.18f, 0f), new Vector3(2.65f, 1.1f, 0.08f), new Color(0.24f, 0.12f, 0.08f, 1f)),
                ("主干道封控近景切片", new Vector3(2.55f, -1.58f, 0f), new Vector3(2.32f, 1.05f, 0.08f), new Color(0.12f, 0.16f, 0.17f, 1f))
            };

            for (int i = 0; i < roomSlices.Length; i++)
            {
                Vector3 center = roomSlices[i].center;
                Vector3 size = roomSlices[i].size;
                float halfWidth = size.x * 0.5f;
                float halfHeight = size.y * 0.5f;
                CreateShapeProp("行动视角样板层 " + roomSlices[i].name + " 圆角地面", RoundedRectSprite, center + new Vector3(0f, 0f, -0.035f), size, roomSlices[i].color);
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 后墙体", center + new Vector3(0f, halfHeight + 0.08f, 0.46f), new Vector3(size.x, 0.11f, 0.78f), wall);
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 前景檐影", center + new Vector3(0f, -halfHeight - 0.08f, 0.58f), new Vector3(size.x * 0.78f, 0.13f, 0.34f), shadow);
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 左侧厚墙", center + new Vector3(-halfWidth - 0.06f, 0f, 0.34f), new Vector3(0.1f, size.y * 0.72f, 0.52f), Darken(wall, 1.25f));
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 右侧厚墙", center + new Vector3(halfWidth + 0.06f, 0f, 0.34f), new Vector3(0.1f, size.y * 0.72f, 0.52f), Darken(wall, 1.18f));
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 门楣灯", center + new Vector3(0f, -halfHeight + 0.08f, 0.66f), new Vector3(size.x * 0.42f, 0.035f, 0.08f), i % 2 == 0 ? glass : amber);

                for (int window = 0; window < 3; window++)
                {
                    float x = -halfWidth * 0.46f + window * halfWidth * 0.46f;
                    CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 后窗 " + window, center + new Vector3(x, halfHeight + 0.145f, 0.62f), new Vector3(0.28f, 0.035f, 0.14f), glass);
                }
            }

            Vector3[] routePanels =
            {
                new Vector3(-3.05f, -0.22f, 0.02f),
                new Vector3(-1.65f, 0.22f, 0.02f),
                new Vector3(0.15f, 0.48f, 0.02f),
                new Vector3(1.72f, 0.12f, 0.02f),
                new Vector3(3.0f, -0.58f, 0.02f),
                new Vector3(1.18f, -1.72f, 0.02f),
                new Vector3(-0.72f, -1.52f, 0.02f),
                new Vector3(-2.55f, -1.1f, 0.02f)
            };

            for (int i = 0; i < routePanels.Length; i++)
            {
                float rotation = i % 2 == 0 ? -14f : 16f;
                CreateMeshBoxProp("行动视角样板层 非直角走廊地砖 " + i, routePanels[i], new Vector3(1.04f, 0.22f, 0.05f), floor, rotation);
                CreateMeshBoxProp("行动视角样板层 非直角导向灯 " + i, routePanels[i] + new Vector3(0f, 0.16f, 0.08f), new Vector3(0.72f, 0.035f, 0.05f), i % 2 == 0 ? amber : glass, rotation);
            }

            (Vector3 position, Vector3 size, float rotation, Color color)[] floorBreakup =
            {
                (new Vector3(-0.72f, -0.82f, 0.035f), new Vector3(0.86f, 0.16f, 0.05f), -10f, new Color(0.14f, 0.17f, 0.18f, 1f)),
                (new Vector3(0.62f, -0.92f, 0.035f), new Vector3(0.94f, 0.14f, 0.05f), 12f, new Color(0.12f, 0.15f, 0.16f, 1f)),
                (new Vector3(-1.08f, 0.38f, 0.035f), new Vector3(0.78f, 0.14f, 0.05f), 8f, new Color(0.11f, 0.145f, 0.15f, 1f)),
                (new Vector3(0.92f, 0.42f, 0.035f), new Vector3(0.72f, 0.14f, 0.05f), -8f, new Color(0.14f, 0.16f, 0.16f, 1f)),
                (new Vector3(-2.2f, -0.36f, 0.035f), new Vector3(0.62f, 0.12f, 0.05f), -16f, new Color(0.08f, 0.42f, 0.5f, 1f)),
                (new Vector3(2.08f, -0.28f, 0.035f), new Vector3(0.62f, 0.12f, 0.05f), 16f, new Color(0.86f, 0.62f, 0.12f, 1f)),
                (new Vector3(-0.12f, -1.32f, 0.04f), new Vector3(1.36f, 0.035f, 0.05f), 0f, new Color(0.08f, 0.72f, 0.86f, 1f)),
                (new Vector3(0.06f, 0.92f, 0.04f), new Vector3(1.24f, 0.035f, 0.05f), 0f, new Color(0.94f, 0.72f, 0.12f, 1f))
            };

            for (int i = 0; i < floorBreakup.Length; i++)
            {
                CreateMeshBoxProp("行动视角样板层 中心地面细节 " + i, floorBreakup[i].position, floorBreakup[i].size, floorBreakup[i].color, floorBreakup[i].rotation);
            }

            CreateActionViewTaskShowcase(13, new Vector3(-1.42f, -0.18f, 0f), "通讯干扰终端");
            CreateActionViewTaskShowcase(5, new Vector3(-3.88f, 0.08f, 0f), "茶餐厅线人录音");
            CreateActionViewTaskShowcase(20, new Vector3(-1.95f, 1.55f, 0f), "夜市暗号");
            CreateActionViewTaskShowcase(18, new Vector3(1.74f, -2.18f, 0f), "车牌追踪");

            (Vector3 position, Color primary, Color accent, string label)[] npcRefs =
            {
                (new Vector3(-2.42f, -0.82f, 0.16f), police, glass, "警方"),
                (new Vector3(0.92f, -0.85f, 0.16f), gang, amber, "嫌疑"),
                (new Vector3(-0.78f, 0.86f, 0.16f), new Color(0.22f, 0.48f, 0.32f, 1f), amber, "路人"),
                (new Vector3(2.48f, 0.38f, 0.16f), new Color(0.36f, 0.28f, 0.42f, 1f), glass, "卧底")
            };

            for (int i = 0; i < npcRefs.Length; i++)
            {
                CreateActionViewScaleCharacter("行动视角样板层 尺度NPC " + i, npcRefs[i].position, npcRefs[i].primary, npcRefs[i].accent, npcRefs[i].label);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = -4.8f + i * 0.86f;
                float y = i % 2 == 0 ? -2.42f : 1.92f;
                CreateMeshBoxProp("行动视角样板层 街区杂物箱 " + i, new Vector3(x, y, 0.13f), new Vector3(0.34f, 0.22f, 0.24f), i % 3 == 0 ? amber : i % 3 == 1 ? new Color(0.12f, 0.36f, 0.42f, 1f) : new Color(0.42f, 0.18f, 0.12f, 1f), i % 2 == 0 ? -8f : 10f);
            }

            CreateMeshBoxProp("行动视角样板层 近景警戒线 A", new Vector3(-1.88f, -2.38f, 0.22f), new Vector3(1.35f, 0.04f, 0.08f), amber, -10f);
            CreateMeshBoxProp("行动视角样板层 近景警戒线 B", new Vector3(1.35f, -2.12f, 0.22f), new Vector3(1.18f, 0.04f, 0.08f), amber, 12f);
            CreateMeshBoxProp("行动视角样板层 近景电缆桥", new Vector3(0.05f, 1.58f, 0.74f), new Vector3(2.85f, 0.08f, 0.14f), wall);
            CreateMeshBoxProp("行动视角样板层 电缆桥冷光条", new Vector3(0.05f, 1.48f, 0.88f), new Vector3(2.18f, 0.035f, 0.05f), glass);
            CreateMeshBoxProp("行动视角样板层 证据白板主面", new Vector3(-0.78f, -1.88f, 0.52f), new Vector3(0.82f, 0.06f, 0.42f), paper, -8f);
            CreateMeshBoxProp("行动视角样板层 证据白板红线", new Vector3(-0.9f, -1.84f, 0.78f), new Vector3(0.5f, 0.025f, 0.04f), gang, 8f);
            CreateMeshBoxProp("行动视角样板层 证据白板蓝线", new Vector3(-0.62f, -1.84f, 0.66f), new Vector3(0.42f, 0.025f, 0.04f), police, -14f);
        }

        private void CreateActionViewTaskShowcase(int taskId, Vector3 position, string label)
        {
            Color accent = TaskPanelAccent(taskId);
            Color dark = new Color(0.035f, 0.045f, 0.05f, 1f);
            Color amber = new Color(0.94f, 0.72f, 0.12f, 1f);

            CreateShapeProp("行动视角样板层 " + label + " 任务地面光圈", SoftCircleSprite, position + new Vector3(0f, 0f, 0.035f), new Vector3(1.16f, 0.72f, 0.05f), new Color(accent.r, accent.g, accent.b, 0.26f));
            CreateMeshBoxProp("行动视角样板层 " + label + " 大型任务台", position + new Vector3(0f, 0f, 0.28f), new Vector3(0.72f, 0.38f, 0.38f), dark);
            CreateMeshBoxProp("行动视角样板层 " + label + " 高亮交互屏", position + new Vector3(0f, 0.24f, 0.62f), new Vector3(0.52f, 0.04f, 0.22f), accent);
            CreateMeshBoxProp("行动视角样板层 " + label + " E键提示牌", position + new Vector3(-0.48f, 0.18f, 0.72f), new Vector3(0.22f, 0.035f, 0.14f), amber);
            CreateMeshPrimitiveProp("行动视角样板层 " + label + " 顶部信标", PrimitiveType.Cylinder, position + new Vector3(0.46f, -0.16f, 0.68f), new Vector3(0.06f, 0.06f, 0.46f), accent, Quaternion.identity);
            CreateMeshBoxProp("行动视角样板层 " + label + " 信标灯帽", position + new Vector3(0.46f, -0.16f, 0.96f), new Vector3(0.18f, 0.035f, 0.08f), amber);
            CreateWorldLabelAt(label, MapService.ScaleMapPosition(position + new Vector3(0f, 0.6f, 0.1f)), 0.06f);
        }

        private void CreateActionViewScaleCharacter(string name, Vector3 position, Color primary, Color accent, string label)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(worldRoot.transform, false);
            root.transform.position = MapService.ScaleMapPosition(position);
            root.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            CreateMeshPrimitiveChild(root.transform, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.32f, -0.12f), new Vector3(0.52f, 0.08f, 0.28f), new Color(0f, 0f, 0f, 0.32f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPrimitiveChild(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, -0.04f, 0.2f), new Vector3(0.26f, 0.26f, 0.56f), primary, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPrimitiveChild(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0.04f, 0.25f, 0.52f), new Vector3(0.3f, 0.26f, 0.26f), primary, Quaternion.identity);
            CreateMeshBoxChild(root.transform, "Visor", new Vector3(0.13f, 0.42f, 0.56f), new Vector3(0.22f, 0.035f, 0.1f), new Color(0.58f, 0.9f, 1f, 1f));
            CreateMeshPrimitiveChild(root.transform, "Arm L", PrimitiveType.Capsule, new Vector3(-0.22f, -0.04f, 0.28f), new Vector3(0.07f, 0.07f, 0.28f), accent, Quaternion.Euler(90f, 0f, 12f));
            CreateMeshPrimitiveChild(root.transform, "Arm R", PrimitiveType.Capsule, new Vector3(0.22f, -0.04f, 0.28f), new Vector3(0.07f, 0.07f, 0.28f), accent, Quaternion.Euler(90f, 0f, -12f));
            CreateMeshBoxChild(root.transform, "Role Strip " + label, new Vector3(0f, 0.05f, 0.5f), new Vector3(0.2f, 0.035f, 0.06f), accent);
            SetSortingFromZ(root);
        }

        private void CreateMainCorridorSetPieces()
        {
            Color dark = new Color(0.035f, 0.045f, 0.048f, 1f);
            Color metal = new Color(0.12f, 0.145f, 0.15f, 1f);
            Color screen = new Color(0.04f, 0.7f, 0.84f, 1f);
            Color warning = new Color(0.92f, 0.72f, 0.08f, 1f);

            for (int i = 0; i < 8; i++)
            {
                float x = -7.1f + i * 2.05f;
                CreateModelProp("CC0 主廊强化舱板 " + i, i % 2 == 0 ? "Walls/TopCables_Straight.fbx" : "Walls/TopAstra_Straight.fbx", new Vector3(x, -0.78f, 0.18f), new Vector3(0.92f, 0.18f, 0.34f), 0f, true);
                CreateModelProp("CC0 主廊上墙窗 " + i, "Walls/WallAstra_Straight_Window.fbx", new Vector3(x + 0.16f, 0.92f, 0.2f), new Vector3(0.96f, 0.22f, 0.36f), 180f, true);
                CreateMeshBoxProp("主廊检修盖发光边 " + i, new Vector3(x, -0.18f, 0.06f), new Vector3(0.54f, 0.035f, 0.04f), i % 2 == 0 ? screen : warning);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -5.8f + i * 2.3f;
                CreateModelProp("CC0 上层连廊窗墙 " + i, "Walls/TopWindow_Straight.fbx", new Vector3(x, 4.33f, 0.2f), new Vector3(1.05f, 0.2f, 0.36f), 0f, true);
                CreateModelProp("CC0 下层连廊电缆墙 " + i, "Walls/TopCables_Straight_Hanging.fbx", new Vector3(x + 0.26f, -4.58f, 0.2f), new Vector3(1.05f, 0.2f, 0.36f), 180f, true);
            }

            Vector3[] kioskPositions =
            {
                new Vector3(-3.8f, -0.84f, 0.1f),
                new Vector3(3.65f, 0.66f, 0.1f),
                new Vector3(-6.1f, -3.42f, 0.1f),
                new Vector3(6.36f, 3.18f, 0.1f)
            };

            for (int i = 0; i < kioskPositions.Length; i++)
            {
                Vector3 position = kioskPositions[i];
                CreateSolidModelProp("CC0 巡逻服务柜 " + i, "Props/Prop_AccessPoint.fbx", position + new Vector3(0f, 0f, 0.03f), new Vector3(0.42f, 0.32f, 0.32f), i % 2 == 0 ? 0f : 180f);
                CreateMeshBoxProp("巡逻服务柜屏 " + i, position + new Vector3(0f, 0.18f, 0.32f), new Vector3(0.28f, 0.04f, 0.1f), screen);
                CreateMeshBoxProp("巡逻服务柜黄黑边 " + i, position + new Vector3(0f, -0.18f, 0.2f), new Vector3(0.38f, 0.04f, 0.06f), warning);
            }

            CreateSolidProp("主廊移动拒马 A", new Vector3(-2.3f, 0.45f, 0.07f), new Vector3(0.68f, 0.14f, 0.2f), dark);
            CreateSolidProp("主廊移动拒马 B", new Vector3(2.48f, -0.78f, 0.07f), new Vector3(0.68f, 0.14f, 0.2f), dark);
            CreateMeshBoxProp("拒马反光条 A", new Vector3(-2.3f, 0.5f, 0.22f), new Vector3(0.52f, 0.035f, 0.05f), warning);
            CreateMeshBoxProp("拒马反光条 B", new Vector3(2.48f, -0.73f, 0.22f), new Vector3(0.52f, 0.035f, 0.05f), warning);
            CreateMeshBoxProp("主廊地面油污暗斑", new Vector3(4.9f, -0.12f, -0.02f), new Vector3(0.64f, 0.18f, 0.04f), new Color(0.015f, 0.018f, 0.018f, 1f));
            CreateMeshBoxProp("下廊刹车痕 A", new Vector3(-2.2f, -3.68f, -0.02f), new Vector3(0.72f, 0.045f, 0.04f), dark, -8f);
            CreateMeshBoxProp("下廊刹车痕 B", new Vector3(-1.48f, -3.8f, -0.02f), new Vector3(0.62f, 0.045f, 0.04f), dark, -8f);
            CreateMeshBoxProp("中心交叉口巡逻箭头 A", new Vector3(-0.62f, -1.15f, 0.02f), new Vector3(0.28f, 0.18f, 0.04f), warning, -22f);
            CreateMeshBoxProp("中心交叉口巡逻箭头 B", new Vector3(0.72f, 0.52f, 0.02f), new Vector3(0.28f, 0.18f, 0.04f), screen, 22f);
            CreateModelProp("CC0 中心环形护栏 L", "Props/Prop_Rail_Round_Big.fbx", new Vector3(-0.72f, -0.35f, 0.18f), new Vector3(0.62f, 0.42f, 0.24f), 90f, true);
            CreateModelProp("CC0 中心环形护栏 R", "Props/Prop_Rail_Round_Big.fbx", new Vector3(0.72f, -0.35f, 0.18f), new Vector3(0.62f, 0.42f, 0.24f), -90f, true);
            CreateMeshBoxProp("主廊管线桥", new Vector3(0f, 0.96f, 0.32f), new Vector3(2.4f, 0.07f, 0.12f), metal);
            CreateModelProp("CC0 管线桥夹具 A", "Props/Prop_PipeHolder.fbx", new Vector3(-1.12f, 0.98f, 0.38f), new Vector3(0.2f, 0.16f, 0.18f), 0f);
            CreateModelProp("CC0 管线桥夹具 B", "Props/Prop_PipeHolder.fbx", new Vector3(1.12f, 0.98f, 0.38f), new Vector3(0.2f, 0.16f, 0.18f), 180f);
        }

        private void CreateRoomForegroundSilhouettes()
        {
            foreach (OnlineMapService.ShipRoomSpec room in MapService.ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                Color shadow = new Color(0.015f, 0.018f, 0.02f, 0.42f);
                Color glass = new Color(0.16f, 0.38f, 0.45f, 0.92f);

                CreateMeshBoxProp("前景墙体阴影 " + room.Name + " N", room.Center + new Vector3(0f, halfHeight + 0.08f, 0.22f), new Vector3(room.Size.x * 0.9f, 0.08f, 0.18f), shadow);
                CreateMeshBoxProp("前景墙体阴影 " + room.Name + " S", room.Center + new Vector3(0f, -halfHeight - 0.08f, 0.2f), new Vector3(room.Size.x * 0.82f, 0.08f, 0.16f), shadow);
                CreateModelProp("CC0 " + room.Name + " 顶部窗墙", "Walls/WallWindow_Straight.fbx", room.Center + new Vector3(0f, halfHeight - 0.08f, 0.28f), new Vector3(Mathf.Min(room.Size.x * 0.64f, 1.9f), 0.2f, 0.36f), 0f, true);
                CreateMeshBoxProp("房间玻璃反光 " + room.Name, room.Center + new Vector3(halfWidth * 0.38f, halfHeight - 0.2f, 0.38f), new Vector3(Mathf.Min(0.72f, room.Size.x * 0.24f), 0.035f, 0.08f), glass);
                CreateMeshBoxProp("房间地面编号条 " + room.Name, room.Center + new Vector3(-halfWidth * 0.32f, -halfHeight * 0.34f, 0.04f), new Vector3(Mathf.Min(0.86f, room.Size.x * 0.24f), 0.04f, 0.04f), DoorColor(room));

                if (room.Size.x > 3.3f)
                {
                    CreateModelProp("CC0 " + room.Name + " 角落圆柱 A", "Columns/Column_Round.fbx", room.Center + new Vector3(-halfWidth + 0.38f, halfHeight - 0.32f, 0.18f), new Vector3(0.22f, 0.22f, 0.4f), 0f);
                    CreateModelProp("CC0 " + room.Name + " 角落圆柱 B", "Columns/Column_Pipes.fbx", room.Center + new Vector3(halfWidth - 0.4f, -halfHeight + 0.32f, 0.18f), new Vector3(0.22f, 0.22f, 0.4f), 0f);
                }
            }
        }

        private void CreateActionCameraForegroundOccluders()
        {
            Color deepShadow = new Color(0.006f, 0.009f, 0.011f, 0.74f);
            Color bulkhead = new Color(0.018f, 0.025f, 0.03f, 0.92f);
            Color glass = new Color(0.12f, 0.32f, 0.38f, 0.68f);
            Color trim = new Color(0.44f, 0.52f, 0.52f, 0.86f);

            foreach (OnlineMapService.ShipRoomSpec room in MapService.ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                float topY = halfHeight + 0.2f;
                float bottomY = -halfHeight - 0.16f;

                CreateMeshBoxProp("前景遮挡层 " + room.Name + " 上檐阴影", room.Center + new Vector3(0f, topY, 0.78f), new Vector3(room.Size.x * 0.82f, 0.18f, 0.42f), deepShadow);
                CreateMeshBoxProp("前景遮挡层 " + room.Name + " 下檐黑边", room.Center + new Vector3(0f, bottomY, 0.58f), new Vector3(room.Size.x * 0.58f, 0.12f, 0.32f), bulkhead);

                if (room.Entrance == OnlineMapService.MapEntrance.East || room.Entrance == OnlineMapService.MapEntrance.West)
                {
                    float side = room.Entrance == OnlineMapService.MapEntrance.East ? halfWidth + 0.18f : -halfWidth - 0.18f;
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 侧门厚框", room.Center + new Vector3(side, 0f, 0.62f), new Vector3(0.18f, room.Size.y * 0.56f, 0.36f), bulkhead);
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 侧门玻璃", room.Center + new Vector3(side, 0f, 0.88f), new Vector3(0.05f, room.Size.y * 0.32f, 0.18f), glass);
                }
                else
                {
                    float side = room.Entrance == OnlineMapService.MapEntrance.North ? topY : bottomY;
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 横门厚框", room.Center + new Vector3(0f, side, 0.62f), new Vector3(room.Size.x * 0.36f, 0.14f, 0.36f), bulkhead);
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 横门灯缝", room.Center + new Vector3(0f, side, 0.88f), new Vector3(room.Size.x * 0.28f, 0.035f, 0.08f), DoorColor(room));
                }

                CreateMeshBoxProp("前景遮挡层 " + room.Name + " 识别灯带", room.Center + new Vector3(-halfWidth * 0.35f, topY - 0.1f, 0.98f), new Vector3(Mathf.Min(room.Size.x * 0.26f, 0.96f), 0.035f, 0.08f), trim);
            }

            Vector3[] corridorOccluders =
            {
                new Vector3(-5.3f, 0.7f, 0.74f),
                new Vector3(-0.8f, 0.82f, 0.74f),
                new Vector3(3.9f, 0.68f, 0.74f),
                new Vector3(-5.2f, -4.5f, 0.72f),
                new Vector3(0.2f, -4.55f, 0.72f),
                new Vector3(5.35f, -4.5f, 0.72f)
            };

            for (int i = 0; i < corridorOccluders.Length; i++)
            {
                Vector3 position = corridorOccluders[i];
                CreateMeshBoxProp("前景遮挡层 主廊低顶梁 " + i, position, new Vector3(1.36f, 0.12f, 0.36f), bulkhead);
                CreateMeshBoxProp("前景遮挡层 主廊顶梁冷光 " + i, position + new Vector3(0f, -0.08f, 0.22f), new Vector3(1.0f, 0.035f, 0.06f), new Color(0.08f, 0.72f, 0.86f, 0.92f));
            }
        }

        private void CreateDistrictHeroSetPieces()
        {
            CreateDockyardHeroSet();
            CreateMarketHeroSet();
            CreateCommandAndEvidenceHeroSet();
            CreateClinicAndBackLaneHeroSet();
            CreateFinancePowerHeroSet();
        }

        private void CreateDockyardHeroSet()
        {
            Color crane = new Color(0.84f, 0.56f, 0.06f, 1f);
            Color steel = new Color(0.06f, 0.075f, 0.08f, 1f);
            Color blue = new Color(0.08f, 0.18f, 0.28f, 0.78f);
            Color red = new Color(0.28f, 0.1f, 0.08f, 0.78f);

            CreateSolidProp("2.5D 建筑体 巨型货柜龙门架左脚", new Vector3(-10.7f, 5.18f, 0.22f), new Vector3(0.16f, 1.78f, 0.64f), crane);
            CreateSolidProp("2.5D 建筑体 巨型货柜龙门架右脚", new Vector3(-8.2f, 5.18f, 0.22f), new Vector3(0.16f, 1.78f, 0.64f), crane);
            CreateMeshBoxProp("屋顶 巨型货柜龙门架横梁", new Vector3(-9.45f, 6.05f, 0.98f), new Vector3(2.85f, 0.12f, 0.14f), crane);
            CreateMeshBoxProp("屋顶 巨型货柜龙门架吊轨", new Vector3(-9.45f, 5.62f, 0.74f), new Vector3(2.32f, 0.06f, 0.08f), steel);
            CreateMeshBoxProp("屋顶 巨型货柜龙门架吊钩线", new Vector3(-9.05f, 5.35f, 0.52f), new Vector3(0.04f, 0.52f, 0.06f), steel);
            CreateSolidProp("2.5D 建筑体 高层货柜底影一层", new Vector3(-9.55f, 5.46f, 0.06f), new Vector3(1.18f, 0.38f, 0.08f), blue);
            CreateSolidProp("2.5D 建筑体 高层货柜底影二层", new Vector3(-9.08f, 5.86f, 0.26f), new Vector3(1.08f, 0.34f, 0.08f), red);
            CreateModelProp("成熟港区设施 龙门架下免费货柜一层", "Props/Prop_Crate4.fbx", new Vector3(-9.55f, 5.46f, 0.16f), new Vector3(1.02f, 0.36f, 0.34f), 0f, true);
            CreateModelProp("成熟港区设施 龙门架下免费货柜二层", "Props/Prop_Crate3.fbx", new Vector3(-9.08f, 5.86f, 0.38f), new Vector3(0.92f, 0.32f, 0.32f), 180f, true);
            CreateMeshBoxProp("前景遮挡层 货柜区吊机暗影", new Vector3(-9.62f, 4.38f, 0.86f), new Vector3(2.1f, 0.16f, 0.28f), new Color(0f, 0f, 0f, 0.62f));
        }

        private void CreateMarketHeroSet()
        {
            Color redCanvas = new Color(0.72f, 0.12f, 0.08f, 1f);
            Color greenCanvas = new Color(0.12f, 0.38f, 0.22f, 1f);
            Color neon = new Color(0.95f, 0.16f, 0.46f, 1f);
            Color amber = new Color(0.94f, 0.72f, 0.18f, 1f);

            for (int i = 0; i < 4; i++)
            {
                float x = -2.45f + i * 0.98f;
                CreateMeshBoxProp("2.5D 建筑体 夜市折叠棚立柱 " + i, new Vector3(x, 3.42f, 0.3f), new Vector3(0.06f, 0.5f, 0.44f), new Color(0.06f, 0.045f, 0.04f, 1f));
                CreateMeshBoxProp("屋顶 夜市彩棚 " + i, new Vector3(x, 3.64f, 0.68f), new Vector3(0.92f, 0.18f, 0.18f), i % 2 == 0 ? redCanvas : greenCanvas);
                CreateMeshBoxProp("屋顶 夜市招牌灯字 " + i, new Vector3(x, 3.76f, 0.86f), new Vector3(0.54f, 0.035f, 0.06f), i % 2 == 0 ? neon : amber);
            }

            CreateMeshBoxProp("前景遮挡层 夜市人潮顶棚阴影", new Vector3(-0.84f, 2.62f, 0.82f), new Vector3(3.2f, 0.14f, 0.28f), new Color(0.03f, 0.012f, 0.01f, 0.68f));
            CreateMeshBoxProp("2.5D 建筑体 茶餐厅骑楼雨棚", new Vector3(-4.82f, 2.6f, 0.62f), new Vector3(1.82f, 0.2f, 0.18f), new Color(0.72f, 0.42f, 0.14f, 1f));
            CreateMeshBoxProp("屋顶 茶餐厅霓虹长牌", new Vector3(-4.82f, 2.82f, 0.86f), new Vector3(1.4f, 0.04f, 0.08f), neon);
        }

        private void CreateCommandAndEvidenceHeroSet()
        {
            Color policeBlue = new Color(0.08f, 0.28f, 0.9f, 1f);
            Color policeRed = new Color(0.9f, 0.08f, 0.06f, 1f);
            Color paper = new Color(0.82f, 0.82f, 0.74f, 1f);
            Color uv = new Color(0.45f, 0.24f, 0.92f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 指挥车车身高体", new Vector3(0.12f, -5.34f, 0.38f), new Vector3(1.82f, 0.82f, 0.62f), new Color(0.06f, 0.11f, 0.15f, 1f));
            CreateMeshBoxProp("屋顶 指挥车顶灯红", new Vector3(-0.34f, -4.78f, 0.9f), new Vector3(0.34f, 0.06f, 0.08f), policeRed);
            CreateMeshBoxProp("屋顶 指挥车顶灯蓝", new Vector3(0.48f, -4.78f, 0.9f), new Vector3(0.34f, 0.06f, 0.08f), policeBlue);
            CreateMeshBoxProp("前景遮挡层 指挥车车头阴影", new Vector3(0.12f, -4.72f, 0.72f), new Vector3(1.9f, 0.16f, 0.28f), new Color(0f, 0f, 0f, 0.64f));
            CreateMeshBoxProp("2.5D 建筑体 行动白板高架", new Vector3(-1.35f, -5.72f, 0.48f), new Vector3(0.96f, 0.08f, 0.56f), paper);
            CreateMeshBoxProp("屋顶 行动白板红线", new Vector3(-1.46f, -5.66f, 0.78f), new Vector3(0.66f, 0.025f, 0.06f), policeRed, 12f);

            CreateMeshBoxProp("2.5D 建筑体 证物冷柜高体", new Vector3(-8.3f, -5.16f, 0.38f), new Vector3(1.42f, 0.5f, 0.58f), new Color(0.12f, 0.28f, 0.34f, 1f));
            CreateMeshBoxProp("屋顶 证物紫外扫描架", new Vector3(-8.3f, -4.78f, 0.78f), new Vector3(1.2f, 0.055f, 0.08f), uv);
            CreateMeshBoxProp("前景遮挡层 证物库冷柜门影", new Vector3(-8.3f, -4.58f, 0.72f), new Vector3(1.28f, 0.12f, 0.24f), new Color(0f, 0f, 0f, 0.62f));
        }

        private void CreateClinicAndBackLaneHeroSet()
        {
            Color clinic = new Color(0.36f, 0.72f, 0.62f, 1f);
            Color metal = new Color(0.08f, 0.09f, 0.09f, 1f);
            Color canvas = new Color(0.48f, 0.1f, 0.08f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 诊所招牌高体", new Vector3(7.55f, -5.05f, 0.76f), new Vector3(0.16f, 1.08f, 0.56f), new Color(0.06f, 0.14f, 0.1f, 1f));
            CreateMeshBoxProp("屋顶 诊所绿十字竖", new Vector3(7.62f, -5.05f, 1.1f), new Vector3(0.04f, 0.52f, 0.08f), clinic);
            CreateMeshBoxProp("屋顶 诊所绿十字横", new Vector3(7.62f, -5.05f, 1.1f), new Vector3(0.04f, 0.08f, 0.34f), clinic);
            CreateMeshBoxProp("前景遮挡层 诊所帘影", new Vector3(6.18f, -4.28f, 0.76f), new Vector3(1.6f, 0.12f, 0.34f), new Color(0.02f, 0.04f, 0.035f, 0.66f));

            CreateMeshBoxProp("2.5D 建筑体 后巷排档雨棚高体", new Vector3(5.62f, -1.92f, 0.62f), new Vector3(1.7f, 0.24f, 0.22f), canvas);
            CreateMeshBoxProp("屋顶 后巷油烟管", new Vector3(4.92f, -1.58f, 0.84f), new Vector3(0.14f, 0.14f, 0.52f), metal);
            CreateMeshBoxProp("前景遮挡层 后巷暗门阴影", new Vector3(6.28f, -0.72f, 0.68f), new Vector3(1.12f, 0.12f, 0.28f), new Color(0f, 0f, 0f, 0.64f));
        }

        private void CreateFinancePowerHeroSet()
        {
            Color glass = new Color(0.08f, 0.36f, 0.48f, 1f);
            Color gold = new Color(0.92f, 0.7f, 0.16f, 1f);
            Color warning = new Color(0.92f, 0.18f, 0.08f, 1f);
            Color blue = new Color(0.16f, 0.52f, 0.92f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 金融楼玻璃幕墙", new Vector3(4.78f, 3.75f, 0.78f), new Vector3(1.8f, 0.12f, 0.72f), glass);
            for (int i = 0; i < 4; i++)
            {
                CreateMeshBoxProp("屋顶 金融楼窗格 " + i, new Vector3(4.18f + i * 0.38f, 3.82f, 0.98f), new Vector3(0.22f, 0.03f, 0.08f), blue);
            }
            CreateMeshBoxProp("屋顶 金融楼金色招牌", new Vector3(4.78f, 3.94f, 1.18f), new Vector3(1.34f, 0.035f, 0.07f), gold);

            CreateMeshBoxProp("2.5D 建筑体 电房高压母线架", new Vector3(8.78f, 6.08f, 0.72f), new Vector3(1.74f, 0.12f, 0.56f), new Color(0.09f, 0.12f, 0.18f, 1f));
            CreateMeshBoxProp("屋顶 电房红色警报条", new Vector3(8.78f, 6.18f, 1.08f), new Vector3(1.42f, 0.035f, 0.07f), warning);
            CreateMeshBoxProp("前景遮挡层 电房电缆阴影", new Vector3(8.78f, 4.42f, 0.74f), new Vector3(1.6f, 0.14f, 0.3f), new Color(0f, 0f, 0f, 0.62f));
        }

        private void CreateLowerDeckActivitySets()
        {
            Color commandBlue = new Color(0.08f, 0.36f, 0.72f, 1f);
            Color evidencePurple = new Color(0.42f, 0.24f, 0.84f, 1f);
            Color clinicGreen = new Color(0.24f, 0.58f, 0.46f, 1f);
            Color metal = new Color(0.07f, 0.085f, 0.09f, 1f);
            Color paper = new Color(0.82f, 0.82f, 0.72f, 1f);

            CreateSolidModelProp("CC0 指挥车车头", "Props/Prop_Crate4.fbx", new Vector3(-1.28f, -5.22f, 0.12f), new Vector3(0.72f, 0.4f, 0.32f), 0f);
            CreateSolidModelProp("CC0 指挥车设备箱", "Props/Prop_AccessPoint.fbx", new Vector3(1.18f, -5.25f, 0.12f), new Vector3(0.58f, 0.38f, 0.32f), 180f);
            CreateMeshBoxProp("指挥车蓝白灯 A", new Vector3(-0.72f, -4.78f, 0.28f), new Vector3(0.42f, 0.055f, 0.08f), commandBlue);
            CreateMeshBoxProp("指挥车红白灯 B", new Vector3(0.62f, -4.78f, 0.28f), new Vector3(0.42f, 0.055f, 0.08f), new Color(0.82f, 0.1f, 0.08f, 1f));
            CreateMeshBoxProp("行动路线白板主面", new Vector3(-0.18f, -5.74f, 0.22f), new Vector3(1.36f, 0.06f, 0.28f), paper);
            CreateMeshBoxProp("行动路线红线", new Vector3(-0.28f, -5.72f, 0.38f), new Vector3(0.72f, 0.025f, 0.04f), new Color(0.86f, 0.08f, 0.06f, 1f), 8f);
            CreateMeshBoxProp("行动路线蓝线", new Vector3(0.18f, -5.7f, 0.39f), new Vector3(0.62f, 0.025f, 0.04f), commandBlue, -12f);

            for (int i = 0; i < 4; i++)
            {
                float x = -9.62f + i * 0.58f;
                CreateSolidModelProp("CC0 证物库矮架 " + i, i % 2 == 0 ? "Props/Prop_Chest.fbx" : "Props/Prop_Crate3.fbx", new Vector3(x, -5.32f, 0.1f), new Vector3(0.42f, 0.28f, 0.28f), i * 12f);
                CreateMeshBoxProp("证物库紫外编号 " + i, new Vector3(x, -5.05f, 0.34f), new Vector3(0.22f, 0.035f, 0.05f), evidencePurple);
            }

            CreateSolidProp("证物库移动冷柜", new Vector3(-7.68f, -4.58f, 0.08f), new Vector3(0.72f, 0.34f, 0.22f), new Color(0.16f, 0.32f, 0.36f, 1f));
            CreateMeshBoxProp("证物库冷柜温度屏", new Vector3(-7.68f, -4.34f, 0.26f), new Vector3(0.38f, 0.04f, 0.08f), new Color(0.06f, 0.74f, 0.86f, 1f));
            CreateMeshBoxProp("证物库脚印胶片", new Vector3(-8.58f, -5.72f, 0.06f), new Vector3(0.48f, 0.12f, 0.04f), new Color(0.02f, 0.025f, 0.028f, 1f), -14f);

            CreateSolidModelProp("CC0 诊所推车", "Props/Prop_ItemHolder.fbx", new Vector3(5.28f, -4.62f, 0.12f), new Vector3(0.42f, 0.32f, 0.28f), 0f);
            CreateSolidModelProp("CC0 诊所仪器柜", "Props/Prop_AccessPoint.fbx", new Vector3(7.12f, -5.42f, 0.12f), new Vector3(0.42f, 0.34f, 0.3f), 180f);
            CreateMeshBoxProp("诊所生命监护绿线", new Vector3(7.12f, -5.16f, 0.34f), new Vector3(0.32f, 0.035f, 0.05f), clinicGreen);
            CreateMeshBoxProp("诊所隔帘轨", new Vector3(6.16f, -4.34f, 0.35f), new Vector3(1.45f, 0.04f, 0.07f), metal);
            CreateMeshBoxProp("诊所半透明隔帘 A", new Vector3(5.72f, -4.48f, 0.24f), new Vector3(0.08f, 0.38f, 0.16f), new Color(0.5f, 0.78f, 0.72f, 0.78f));
            CreateMeshBoxProp("诊所半透明隔帘 B", new Vector3(6.58f, -4.48f, 0.24f), new Vector3(0.08f, 0.38f, 0.16f), new Color(0.5f, 0.78f, 0.72f, 0.78f));

            CreateSolidModelProp("CC0 后巷油桶堆", "Props/Prop_Barrel_Large.fbx", new Vector3(5.1f, -2.22f, 0.1f), new Vector3(0.36f, 0.32f, 0.28f), 0f);
            CreateSolidModelProp("CC0 后巷工具箱", "Props/Prop_Chest.fbx", new Vector3(6.48f, -1.2f, 0.1f), new Vector3(0.48f, 0.32f, 0.28f), 90f);
            CreateMeshBoxProp("后巷雨棚阴影", new Vector3(5.68f, -1.92f, 0.28f), new Vector3(1.55f, 0.08f, 0.1f), new Color(0.1f, 0.035f, 0.03f, 1f));
        }

        private void CreateOrganicRouteLanguage()
        {
            Color routeShadow = new Color(0.045f, 0.052f, 0.052f, 1f);
            Color routeEdge = new Color(0.32f, 0.39f, 0.39f, 1f);
            Color yellow = new Color(0.9f, 0.68f, 0.1f, 1f);
            Color blue = new Color(0.08f, 0.58f, 0.8f, 1f);

            Vector3[] nodes =
            {
                new Vector3(-7.18f, 3.92f, -0.06f),
                new Vector3(-4.22f, 2.42f, -0.06f),
                new Vector3(-0.72f, 0.78f, -0.06f),
                new Vector3(3.22f, 1.2f, -0.06f),
                new Vector3(6.98f, 3.78f, -0.06f),
                new Vector3(-6.92f, -3.78f, -0.06f),
                new Vector3(-2.48f, -2.18f, -0.06f),
                new Vector3(2.28f, -2.42f, -0.06f),
                new Vector3(6.88f, -3.58f, -0.06f)
            };

            for (int i = 0; i < nodes.Length; i++)
            {
                Vector3 node = nodes[i];
                CreateShapeProp("非直角动线 弯角缓冲区 " + i, SoftCircleSprite, node, new Vector3(i % 2 == 0 ? 1.18f : 0.92f, i % 2 == 0 ? 0.68f : 0.56f, 0.06f), routeShadow);
                CreateMeshBoxProp("非直角动线 弯角导向灯 " + i, node + new Vector3(0f, 0.28f, 0.08f), new Vector3(0.54f, 0.035f, 0.05f), i % 2 == 0 ? yellow : blue, i % 3 == 0 ? -12f : 10f);
            }

            for (int i = 0; i < 8; i++)
            {
                float x = -7.2f + i * 2.05f;
                CreateRotatedProp("非直角动线 主廊错位地砖 " + i, new Vector3(x, i % 2 == 0 ? -0.52f : 0.18f, -0.05f), new Vector3(0.62f, 0.18f, 0.05f), routeEdge, i % 2 == 0 ? -9f : 8f);
            }

            CreateMeshBoxProp("非直角动线 夜市蛇形标线 A", new Vector3(-2.1f, 2.28f, 0.04f), new Vector3(1.18f, 0.04f, 0.05f), yellow, -18f);
            CreateMeshBoxProp("非直角动线 夜市蛇形标线 B", new Vector3(-0.72f, 2.9f, 0.04f), new Vector3(1.08f, 0.04f, 0.05f), blue, 16f);
            CreateMeshBoxProp("非直角动线 后巷急弯灯带", new Vector3(5.7f, -2.72f, 0.04f), new Vector3(1.32f, 0.04f, 0.05f), yellow, -14f);
            CreateMeshBoxProp("非直角动线 证物库转角冷光", new Vector3(-7.25f, -4.28f, 0.04f), new Vector3(1.02f, 0.04f, 0.05f), blue, 18f);
        }

        private void CreatePremiumTaskSetPieces()
        {
            if (_tasks == null) return;

            for (int i = 0; i < _tasks.Count; i++)
            {
                OnlineTaskState task = _tasks[i];
                Vector3 position = new Vector3(task.Position.x / MapService.DesignScaleX, task.Position.y / MapService.DesignScaleY, 0f);
                CreatePremiumTaskSetPiece(task.Id, task.Name, position);
            }
        }

        private void CreatePremiumTaskSetPiece(int taskId, string taskName, Vector3 position)
        {
            Color accent = TaskPanelAccent(taskId);
            Color dark = new Color(0.035f, 0.045f, 0.05f, 1f);
            Color metal = new Color(0.14f, 0.16f, 0.16f, 1f);
            Color warning = new Color(0.92f, 0.7f, 0.08f, 1f);
            int mode = TaskTemplateMode(taskId);

            CreateShapeProp("成熟任务站 " + taskName + " 地面工作区", RoundedRectSprite, position + new Vector3(0f, 0f, -0.045f), new Vector3(0.98f, 0.58f, 0.05f), new Color(accent.r, accent.g, accent.b, 0.16f));
            CreateMeshBoxProp("成熟任务站 " + taskName + " 立体背板", position + new Vector3(0f, 0.34f, 0.44f), new Vector3(0.82f, 0.08f, 0.48f), Darken(accent, 0.36f));
            CreateMeshBoxProp("成熟任务站 " + taskName + " 主操作台", position + new Vector3(0f, -0.02f, 0.26f), new Vector3(0.72f, 0.36f, 0.28f), dark);
            CreateMeshBoxProp("成熟任务站 " + taskName + " 状态屏", position + new Vector3(0f, 0.24f, 0.62f), new Vector3(0.46f, 0.04f, 0.18f), accent);
            CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 警示灯", PrimitiveType.Cylinder, position + new Vector3(0.42f, -0.18f, 0.54f), new Vector3(0.08f, 0.08f, 0.1f), warning, Quaternion.Euler(90f, 0f, 0f));

            if (mode == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    CreateMeshBoxProp("成熟任务站 " + taskName + " 多屏矩阵 " + i, position + new Vector3(-0.28f + i * 0.28f, 0.36f, 0.76f), new Vector3(0.2f, 0.035f, 0.12f), new Color(0.04f, 0.74f, 0.86f, 1f));
                }
            }
            else if (mode == 1)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 封条闸门左", position + new Vector3(-0.36f, 0.02f, 0.46f), new Vector3(0.08f, 0.52f, 0.34f), metal);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 封条闸门右", position + new Vector3(0.36f, 0.02f, 0.46f), new Vector3(0.08f, 0.52f, 0.34f), metal);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 黄色封条", position + new Vector3(0f, -0.26f, 0.58f), new Vector3(0.76f, 0.04f, 0.06f), warning);
            }
            else if (mode == 2)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 高压闸刀", position + new Vector3(0.2f, 0.04f, 0.7f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.86f, 0.12f, 0.08f, 1f), -18f);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 电缆线束 A", position + new Vector3(-0.22f, -0.18f, 0.48f), new Vector3(0.42f, 0.04f, 0.05f), metal, 12f);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 电缆线束 B", position + new Vector3(-0.18f, 0.1f, 0.5f), new Vector3(0.36f, 0.04f, 0.05f), metal, -10f);
            }
            else if (mode == 3)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 证物托盘", position + new Vector3(0f, -0.1f, 0.48f), new Vector3(0.52f, 0.24f, 0.08f), new Color(0.82f, 0.84f, 0.76f, 1f));
                CreateMeshBoxProp("成熟任务站 " + taskName + " 扫描光带", position + new Vector3(0f, 0.08f, 0.68f), new Vector3(0.52f, 0.035f, 0.08f), new Color(0.4f, 0.24f, 0.86f, 1f));
                CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 样本管", PrimitiveType.Cylinder, position + new Vector3(0.26f, -0.18f, 0.62f), new Vector3(0.05f, 0.05f, 0.16f), accent, Quaternion.identity);
            }
            else if (mode == 4)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 账本抽屉", position + new Vector3(-0.22f, -0.18f, 0.5f), new Vector3(0.28f, 0.16f, 0.08f), new Color(0.46f, 0.34f, 0.16f, 1f));
                CreateMeshBoxProp("成熟任务站 " + taskName + " 现金捆", position + new Vector3(0.2f, -0.18f, 0.5f), new Vector3(0.22f, 0.14f, 0.08f), new Color(0.16f, 0.5f, 0.22f, 1f));
                CreateMeshBoxProp("成熟任务站 " + taskName + " 冻结蓝屏", position + new Vector3(0f, 0.36f, 0.82f), new Vector3(0.58f, 0.035f, 0.08f), new Color(0.08f, 0.46f, 0.88f, 1f));
            }
            else
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 路线板", position + new Vector3(0f, 0.28f, 0.74f), new Vector3(0.56f, 0.04f, 0.24f), new Color(0.78f, 0.72f, 0.54f, 1f));
                CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 路线红点", PrimitiveType.Cylinder, position + new Vector3(-0.16f, 0.32f, 0.86f), new Vector3(0.05f, 0.05f, 0.04f), new Color(0.88f, 0.1f, 0.06f, 1f), Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 路线蓝点", PrimitiveType.Cylinder, position + new Vector3(0.18f, 0.22f, 0.86f), new Vector3(0.05f, 0.05f, 0.04f), new Color(0.08f, 0.32f, 0.9f, 1f), Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private void CreateMatureDockyardSetPieces()
        {
            CreateMatureAssetCluster("北货柜泊位", new Vector3(-9.42f, 5.16f, 0f), 0f, 0);
            CreateMatureAssetCluster("西侧水警泊位", new Vector3(-9.78f, 1.6f, 0f), -90f, 1);
            CreateMatureAssetCluster("夜市后勤口", new Vector3(-3.68f, 2.94f, 0f), 8f, 2);
            CreateMatureAssetCluster("金融楼卸货口", new Vector3(4.98f, 3.12f, 0f), -6f, 3);
            CreateMatureAssetCluster("电房维修坪", new Vector3(8.42f, 5.58f, 0f), 2f, 4);
            CreateMatureAssetCluster("后巷诊所口", new Vector3(6.08f, -3.82f, 0f), -12f, 5);
            CreateMatureAssetCluster("证物库外场", new Vector3(-8.34f, -4.86f, 0f), 5f, 6);
            CreateMatureAssetCluster("指挥车警戒线", new Vector3(0.36f, -5.16f, 0f), 0f, 7);

            string[] railModels =
            {
                "Props/Prop_Rail_2.fbx",
                "Props/Prop_Rail_3.fbx",
                "Props/Prop_Rail_4.fbx",
                "Props/Prop_Rail_Round_Small.fbx"
            };

            Vector3[] railLine =
            {
                new Vector3(-7.4f, 4.22f, 0f),
                new Vector3(-5.54f, 3.18f, 0f),
                new Vector3(-2.22f, 1.86f, 0f),
                new Vector3(1.08f, 1.38f, 0f),
                new Vector3(4.78f, 2.32f, 0f),
                new Vector3(7.4f, 3.96f, 0f),
                new Vector3(6.38f, -2.92f, 0f),
                new Vector3(2.42f, -3.12f, 0f),
                new Vector3(-2.26f, -3.0f, 0f),
                new Vector3(-6.72f, -3.72f, 0f)
            };

            for (int i = 0; i < railLine.Length; i++)
            {
                float rotation = i % 2 == 0 ? 18f : -14f;
                CreateModelProp("成熟港区设施 免费护栏动线 " + i, railModels[i % railModels.Length], railLine[i], new Vector3(0.58f, 0.22f, 0.2f), rotation, true);
                CreateModelProp("成熟港区设施 免费地面箭头标识 " + i, i % 3 == 0 ? "Decals/Decal_Line_Bend1_R.fbx" : "Decals/Decal_Line_Straight.fbx", railLine[i] + new Vector3(0.0f, -0.28f, -0.02f), new Vector3(0.48f, 0.22f, 0.04f), rotation, true);
            }

            OnlineMapService.ShipRoomSpec[] rooms = MapService.ShipRooms();

            for (int i = 0; i < rooms.Length; i++)
            {
                OnlineMapService.ShipRoomSpec room = rooms[i];
                float halfWidth = room.Size.x * 0.5f;
                Vector3 left = room.Center + new Vector3(-halfWidth + 0.48f, 0.12f, 0.16f);
                Vector3 right = room.Center + new Vector3(halfWidth - 0.48f, -0.18f, 0.16f);
                CreateModelProp("成熟港区设施 房间免费通风机 " + room.Name, "Props/Prop_Vent_Wide.fbx", left, new Vector3(0.52f, 0.18f, 0.18f), i % 2 == 0 ? 0f : 180f, true);
                CreateModelProp("成熟港区设施 房间免费照明灯 " + room.Name, i % 2 == 0 ? "Props/Prop_Light_Wide.fbx" : "Props/Prop_Light_Small.fbx", right, new Vector3(0.46f, 0.18f, 0.16f), i % 2 == 0 ? 180f : 0f, true);
            }

            CreateMatureDockyardVehicleAndStreetLayer();
            CreateMatureDockyardCrowdScaleProps();
        }

        private void CreateMatureDockyardVehicleAndStreetLayer()
        {
            Color policeBlue = new Color(0.08f, 0.24f, 0.78f, 1f);
            Color policeRed = new Color(0.86f, 0.08f, 0.06f, 1f);
            Color taxiRed = new Color(0.7f, 0.08f, 0.06f, 1f);
            Color taxiWhite = new Color(0.86f, 0.86f, 0.78f, 1f);
            Color van = new Color(0.1f, 0.16f, 0.18f, 1f);

            CreateVehicleSetPiece("成熟港区设施 警用冲锋车", new Vector3(-0.15f, -5.38f, 0.1f), new Vector3(1.55f, 0.72f, 0.42f), van, policeBlue, 0f);
            CreateVehicleSetPiece("成熟港区设施 茶餐厅红的士", new Vector3(-4.15f, 0.78f, 0.1f), new Vector3(1.25f, 0.54f, 0.34f), taxiRed, taxiWhite, 8f);
            CreateVehicleSetPiece("成熟港区设施 后巷黑色面包车", new Vector3(6.62f, -2.32f, 0.1f), new Vector3(1.38f, 0.58f, 0.38f), new Color(0.035f, 0.04f, 0.045f, 1f), new Color(0.38f, 0.46f, 0.48f, 1f), -10f);

            CreateMeshBoxProp("成熟港区设施 警车顶灯红", new Vector3(-0.55f, -4.86f, 0.52f), new Vector3(0.24f, 0.05f, 0.07f), policeRed);
            CreateMeshBoxProp("成熟港区设施 警车顶灯蓝", new Vector3(0.45f, -4.86f, 0.52f), new Vector3(0.24f, 0.05f, 0.07f), policeBlue);

            Vector3[] roadblockPositions =
            {
                new Vector3(-3.25f, -3.64f, 0.1f),
                new Vector3(-2.62f, -3.78f, 0.1f),
                new Vector3(1.82f, -4.18f, 0.1f),
                new Vector3(2.48f, -4.02f, 0.1f),
                new Vector3(4.18f, 1.34f, 0.1f),
                new Vector3(4.8f, 1.12f, 0.1f),
                new Vector3(-7.38f, 4.28f, 0.1f),
                new Vector3(-6.78f, 4.08f, 0.1f)
            };

            for (int i = 0; i < roadblockPositions.Length; i++)
            {
                Vector3 position = roadblockPositions[i];
                CreateSolidMeshBoxProp("成熟港区设施 可碰撞水马路障 " + i, position, new Vector3(0.46f, 0.12f, 0.22f), i % 2 == 0 ? policeBlue : policeRed, i % 2 == 0 ? -12f : 14f);
                CreateMeshBoxProp("成熟港区设施 水马反光白条 " + i, position + new Vector3(0f, 0.02f, 0.18f), new Vector3(0.34f, 0.035f, 0.04f), new Color(0.86f, 0.86f, 0.78f, 1f), i % 2 == 0 ? -12f : 14f);
            }
        }

        private void CreateVehicleSetPiece(string name, Vector3 position, Vector3 size, Color body, Color stripe, float rotationDegrees)
        {
            CreateSolidMeshBoxProp(name + " 车身", position + new Vector3(0f, 0f, 0.18f), size, body, rotationDegrees);
            CreateMeshBoxProp(name + " 前挡风玻璃", position + new Vector3(size.x * 0.18f, size.y * 0.28f, 0.48f), new Vector3(size.x * 0.24f, 0.04f, 0.1f), new Color(0.12f, 0.42f, 0.52f, 1f), rotationDegrees);
            CreateMeshBoxProp(name + " 侧面识别条", position + new Vector3(0f, -size.y * 0.28f, 0.42f), new Vector3(size.x * 0.72f, 0.035f, 0.06f), stripe, rotationDegrees);

            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -size.x * 0.32f : size.x * 0.32f;
                float y = i % 2 == 0 ? -size.y * 0.36f : size.y * 0.36f;
                CreateMeshPrimitiveProp(name + " 轮胎 " + i, PrimitiveType.Cylinder, position + new Vector3(x, y, 0.14f), new Vector3(0.12f, 0.04f, 0.12f), new Color(0.015f, 0.015f, 0.018f, 1f), Quaternion.Euler(90f, 0f, rotationDegrees));
            }
        }

        private void CreateMatureDockyardCrowdScaleProps()
        {
            Color cone = new Color(0.9f, 0.34f, 0.08f, 1f);
            Color white = new Color(0.86f, 0.86f, 0.78f, 1f);
            Color sign = new Color(0.92f, 0.72f, 0.08f, 1f);

            for (int i = 0; i < 24; i++)
            {
                float band = i % 6;
                float row = i / 6;
                Vector3 position = new Vector3(-5.9f + band * 2.25f + (row % 2) * 0.38f, -6.25f + row * 0.62f, 0.08f);
                CreateMeshPrimitiveProp("成熟港区设施 路锥阵列 " + i, PrimitiveType.Cylinder, position, new Vector3(0.1f, 0.08f, 0.12f), cone, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshBoxProp("成熟港区设施 路锥白条 " + i, position + new Vector3(0f, 0f, 0.11f), new Vector3(0.11f, 0.035f, 0.025f), white);
            }

            Vector3[] signPositions =
            {
                new Vector3(-8.88f, 3.86f, 0.32f),
                new Vector3(-3.28f, 2.26f, 0.32f),
                new Vector3(2.68f, -3.36f, 0.32f),
                new Vector3(7.72f, 3.78f, 0.32f),
                new Vector3(6.34f, -4.12f, 0.32f)
            };

            for (int i = 0; i < signPositions.Length; i++)
            {
                CreateMeshBoxProp("成熟港区设施 港区警示立牌 " + i, signPositions[i], new Vector3(0.46f, 0.055f, 0.34f), sign, i % 2 == 0 ? 8f : -8f);
                CreateMeshBoxProp("成熟港区设施 警示立牌黑条 " + i, signPositions[i] + new Vector3(0f, 0.04f, 0.1f), new Vector3(0.32f, 0.025f, 0.04f), new Color(0.04f, 0.04f, 0.04f, 1f), i % 2 == 0 ? 8f : -8f);
            }
        }

        private void CreateMatureAssetCluster(string clusterName, Vector3 center, float rotation, int variant)
        {
            string[] bulkyProps =
            {
                "Props/Prop_Crate3.fbx",
                "Props/Prop_Crate4.fbx",
                "Props/Prop_Chest.fbx",
                "Props/Prop_Barrel_Large.fbx"
            };

            string[] utilityProps =
            {
                "Props/Prop_AccessPoint.fbx",
                "Props/Prop_Computer.fbx",
                "Props/Prop_ItemHolder.fbx",
                "Props/Prop_Cable_1.fbx",
                "Props/Prop_Cable_3.fbx",
                "Props/Prop_Vent_Big.fbx",
                "Props/Prop_Light_Floor.fbx",
                "Props/Prop_PipeHolder.fbx"
            };

            string[] platformProps =
            {
                "Platforms/Platform_Metal2.fbx",
                "Platforms/Platform_DarkPlates.fbx",
                "Platforms/Platform_3Plates.fbx",
                "Platforms/Platform_Rails_4Wide.fbx",
                "Platforms/Platform_Stairs_2.fbx",
                "Platforms/Door_Frame_A.fbx"
            };

            Vector3[] offsets =
            {
                new Vector3(-0.72f, 0.22f, 0.08f),
                new Vector3(-0.28f, -0.22f, 0.08f),
                new Vector3(0.34f, 0.22f, 0.08f),
                new Vector3(0.76f, -0.16f, 0.08f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 offset = RotateOffset(offsets[i], rotation);
                CreateModelProp("成熟港区设施 " + clusterName + " 免费货物组 " + i, bulkyProps[(variant + i) % bulkyProps.Length], center + offset, new Vector3(0.48f, 0.34f, 0.32f), rotation + i * 11f, false);
            }

            for (int i = 0; i < utilityProps.Length; i++)
            {
                float angle = rotation + i * 31f;
                Vector3 ring = RotateOffset(new Vector3(Mathf.Cos(i * 0.72f) * 1.02f, Mathf.Sin(i * 0.72f) * 0.58f, 0.1f), rotation);
                Vector3 footprint = i % 3 == 0 ? new Vector3(0.38f, 0.24f, 0.28f) : new Vector3(0.32f, 0.2f, 0.24f);
                CreateModelProp("成熟港区设施 " + clusterName + " 免费设备件 " + i, utilityProps[i], center + ring, footprint, angle, false);
            }

            for (int i = 0; i < platformProps.Length; i++)
            {
                Vector3 strip = RotateOffset(new Vector3(-1.12f + i * 0.45f, 0.78f, -0.02f), rotation);
                CreateModelProp("成熟港区设施 " + clusterName + " 免费平台件 " + i, platformProps[i], center + strip, new Vector3(0.52f, 0.26f, 0.14f), rotation + (i % 2 == 0 ? 0f : 180f), true);
            }

            CreateMeshBoxProp("成熟港区设施 " + clusterName + " 警戒反光地线 A", center + RotateOffset(new Vector3(0f, -0.72f, 0.04f), rotation), new Vector3(1.68f, 0.035f, 0.05f), new Color(0.92f, 0.7f, 0.08f, 1f), rotation);
            CreateMeshBoxProp("成熟港区设施 " + clusterName + " 冷光编号条 B", center + RotateOffset(new Vector3(0.42f, 0.62f, 0.06f), rotation), new Vector3(0.92f, 0.035f, 0.05f), new Color(0.08f, 0.72f, 0.86f, 1f), rotation + 8f);
            CreateShapeProp("成熟港区设施 " + clusterName + " 作业区底影", RoundedRectSprite, center + new Vector3(0f, 0f, -0.055f), new Vector3(2.38f, 1.28f, 0.04f), new Color(0.02f, 0.026f, 0.028f, 0.68f));
        }

        private static Vector3 RotateOffset(Vector3 offset, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector3(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos, offset.z);
        }

        private void CreateTaskInteractionHalos()
        {
            if (_tasks == null) return;

            for (int i = 0; i < _tasks.Count; i++)
            {
                OnlineTaskState task = _tasks[i];
                Vector3 designPosition = new Vector3(task.Position.x / MapService.DesignScaleX, task.Position.y / MapService.DesignScaleY, 0f);
                Color accent = TaskPanelAccent(task.Id);
                CreateShapeProp("任务交互范围环 " + task.Name, CircleSprite, designPosition + new Vector3(0f, 0f, -0.03f), new Vector3(0.72f, 0.46f, 0.04f), new Color(accent.r, accent.g, accent.b, 0.2f));
                CreateShapeProp("任务可读性 外发光底环 " + task.Name, SoftCircleSprite, designPosition + new Vector3(0f, 0f, 0.03f), new Vector3(0.96f, 0.62f, 0.05f), new Color(accent.r, accent.g, accent.b, 0.24f));
                CreateMeshBoxProp("任务可读性 交互键 E " + task.Name, designPosition + new Vector3(-0.36f, 0.34f, 0.52f), new Vector3(0.18f, 0.035f, 0.12f), new Color(0.94f, 0.76f, 0.12f, 1f));
                CreateMeshBoxProp("任务可读性 状态灯条 " + task.Name, designPosition + new Vector3(0.02f, 0.38f, 0.58f), new Vector3(0.52f, 0.04f, 0.08f), accent);
                CreateMeshPrimitiveProp("任务可读性 竖向信标 " + task.Name, PrimitiveType.Cylinder, designPosition + new Vector3(0.42f, 0.18f, 0.42f), new Vector3(0.04f, 0.04f, 0.58f), accent, Quaternion.identity);
                CreateMeshBoxProp("任务可读性 信标顶灯 " + task.Name, designPosition + new Vector3(0.42f, 0.18f, 0.74f), new Vector3(0.16f, 0.035f, 0.08f), new Color(0.96f, 0.92f, 0.42f, 1f));

                if (task.Id % 3 == 0)
                {
                    CreateModelProp("CC0 任务备用小终端 " + task.Name, "Props/Prop_Computer.fbx", designPosition + new Vector3(0.36f, -0.22f, 0.08f), new Vector3(0.32f, 0.24f, 0.24f), 180f);
                }
                else if (task.Id % 3 == 1)
                {
                    CreateModelProp("CC0 任务工具架 " + task.Name, "Props/Prop_ItemHolder.fbx", designPosition + new Vector3(-0.36f, 0.2f, 0.08f), new Vector3(0.28f, 0.22f, 0.24f), 0f);
                }
                else
                {
                    CreateModelProp("CC0 任务线缆夹 " + task.Name, "Props/Prop_Clamp.fbx", designPosition + new Vector3(0.32f, 0.18f, 0.08f), new Vector3(0.24f, 0.2f, 0.22f), 90f);
                }
            }
        }

        private void CreateEmergencyMeetingTableSet()
        {
            Color table = new Color(0.24f, 0.28f, 0.28f, 1f);
            Color seat = new Color(0.08f, 0.12f, 0.14f, 1f);
            CreateMeshPrimitiveProp("会议桌低矮圆台", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0.08f), new Vector3(0.74f, 0.035f, 0.74f), table, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("会议桌证据投影线 A", new Vector3(0f, -0.12f, 0.28f), new Vector3(0.82f, 0.035f, 0.04f), new Color(0.05f, 0.72f, 0.86f, 1f));
            CreateMeshBoxProp("会议桌证据投影线 B", new Vector3(0.18f, -0.56f, 0.28f), new Vector3(0.46f, 0.035f, 0.04f), new Color(0.95f, 0.22f, 0.18f, 1f));

            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 1.18f, -0.35f + Mathf.Sin(angle) * 0.78f, 0.09f);
                CreateMeshPrimitiveProp("会议玩家座位 " + i, PrimitiveType.Cylinder, position, new Vector3(0.16f, 0.025f, 0.16f), seat, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private void CreatePhysicsCollisionMarkers()
        {
            Color bumper = new Color(0.04f, 0.05f, 0.052f, 1f);
            Color stripe = new Color(0.92f, 0.72f, 0.08f, 1f);

            Vector3[] positions =
            {
                new Vector3(-7.65f, 0.92f, 0.08f),
                new Vector3(7.86f, -0.72f, 0.08f),
                new Vector3(-4.1f, -4.46f, 0.08f),
                new Vector3(4.35f, 4.14f, 0.08f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 position = positions[i];
                CreateSolidProp("实体碰撞防撞墩 " + i, position, new Vector3(0.22f, 0.42f, 0.2f), bumper);
                CreateMeshBoxProp("防撞墩反光贴 " + i, position + new Vector3(0f, 0.18f, 0.2f), new Vector3(0.18f, 0.035f, 0.05f), stripe);
            }
        }

        private void CreateLargeScalePortSetPieces()
        {
            CreateExteriorDockVista();
            CreateDistrictIdentityLandmarks();
            CreateShipLikeSightlineWalls();
            CreateLargeHongKongPortBackdrop();
            CreateLargeDistrictDepthSilhouettes();
            CreateLargePlayableSightlineSetPieces();
            CreateRoundEndShowcaseSet();
        }

        private void CreateLargeRoomReadabilityLayer()
        {
            Color outerWall = new Color(0.018f, 0.026f, 0.03f, 1f);
            Color innerWall = new Color(0.05f, 0.064f, 0.07f, 1f);
            Color shadow = new Color(0.004f, 0.006f, 0.008f, 0.62f);
            Color glass = new Color(0.08f, 0.38f, 0.46f, 1f);

            foreach (OnlineMapService.ShipRoomSpec room in MapService.ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                float height = RoomVisualHeight(room) + 0.24f;
                Color light = DoorColor(room);

                CreateMeshBoxProp("大场景港区层 房间高外壳北 " + room.Name, room.Center + new Vector3(0f, halfHeight + 0.2f, height * 0.56f), new Vector3(room.Size.x + 0.52f, 0.18f, height), outerWall);
                CreateMeshBoxProp("大场景港区层 房间高外壳西 " + room.Name, room.Center + new Vector3(-halfWidth - 0.18f, 0f, height * 0.46f), new Vector3(0.18f, room.Size.y + 0.26f, height * 0.82f), innerWall);
                CreateMeshBoxProp("大场景港区层 房间高外壳东 " + room.Name, room.Center + new Vector3(halfWidth + 0.18f, 0f, height * 0.46f), new Vector3(0.18f, room.Size.y + 0.26f, height * 0.82f), innerWall);
                CreateMeshBoxProp("前景遮挡层 房间前檐阴影 " + room.Name, room.Center + new Vector3(0f, -halfHeight - 0.12f, 0.86f), new Vector3(room.Size.x * 0.82f, 0.18f, 0.44f), shadow);
                CreateMeshBoxProp("屋顶 房间厚檐发光边 " + room.Name, room.Center + new Vector3(0f, halfHeight + 0.32f, height + 0.1f), new Vector3(room.Size.x * 0.62f, 0.05f, 0.08f), light);

                for (int i = 0; i < 3; i++)
                {
                    float x = -room.Size.x * 0.28f + i * room.Size.x * 0.28f;
                    CreateMeshBoxProp("大场景港区层 房间远窗 " + room.Name + " " + i, room.Center + new Vector3(x, halfHeight + 0.31f, height * 0.62f), new Vector3(0.26f, 0.035f, 0.16f), glass);
                }

                CreateRoomPortalKit(room);
            }

            CreateCurvedCorridorReadability();
            CreatePlayableSightlineBlockers();
        }

        private void CreateRoomPortalKit(OnlineMapService.ShipRoomSpec room)
        {
            Vector3 door = DoorLightPosition(room);
            Vector3 doorScale = DoorLightScale(room);
            Color light = DoorColor(room);
            bool horizontal = room.Entrance == OnlineMapService.MapEntrance.North || room.Entrance == OnlineMapService.MapEntrance.South;
            float rotation = horizontal ? 0f : 90f;
            Vector3 frameSize = horizontal
                ? new Vector3(Mathf.Max(0.7f, doorScale.x + 0.3f), 0.34f, 0.6f)
                : new Vector3(0.34f, Mathf.Max(0.7f, doorScale.y + 0.3f), 0.6f);
            Vector3 offset = Vector3.zero;

            switch (room.Entrance)
            {
                case OnlineMapService.MapEntrance.North:
                    offset = new Vector3(0f, 0.28f, 0.1f);
                    break;
                case OnlineMapService.MapEntrance.South:
                    offset = new Vector3(0f, -0.28f, 0.1f);
                    break;
                case OnlineMapService.MapEntrance.East:
                    offset = new Vector3(0.28f, 0f, 0.1f);
                    break;
                case OnlineMapService.MapEntrance.West:
                    offset = new Vector3(-0.28f, 0f, 0.1f);
                    break;
            }

            Vector3 portalCenter = door + offset;
            CreateModelProp("大场景港区层 房间门框模型 " + room.Name, "Platforms/Door_Frame_SquareTall.fbx", portalCenter, frameSize, rotation, true);
            CreateMeshBoxProp("大场景港区层 房间门楣灯 " + room.Name, portalCenter + new Vector3(0f, 0f, 0.34f), horizontal ? new Vector3(frameSize.x * 0.58f, 0.04f, 0.08f) : new Vector3(0.04f, frameSize.y * 0.58f, 0.08f), light, rotation);
            CreateMeshBoxProp("前景遮挡层 门口短阴影 " + room.Name, portalCenter + new Vector3(0f, -0.08f, 0.46f), horizontal ? new Vector3(frameSize.x * 0.76f, 0.1f, 0.22f) : new Vector3(0.1f, frameSize.y * 0.76f, 0.22f), new Color(0f, 0f, 0f, 0.46f), rotation);
        }

        private void CreateCurvedCorridorReadability()
        {
            Color routeBlue = new Color(0.08f, 0.55f, 0.72f, 1f);
            Color routeAmber = new Color(0.95f, 0.68f, 0.1f, 1f);
            Color floorDark = new Color(0.045f, 0.056f, 0.06f, 1f);

            Vector3[] routeCenters =
            {
                new Vector3(-6.4f, 3.72f, 0.04f),
                new Vector3(-3.9f, 3.08f, 0.04f),
                new Vector3(-1.25f, 1.55f, 0.04f),
                new Vector3(1.82f, 1.42f, 0.04f),
                new Vector3(4.4f, 0.26f, 0.04f),
                new Vector3(6.62f, -1.92f, 0.04f),
                new Vector3(3.28f, -3.42f, 0.04f),
                new Vector3(-0.22f, -3.28f, 0.04f),
                new Vector3(-4.55f, -3.55f, 0.04f),
                new Vector3(-7.2f, -1.72f, 0.04f)
            };

            for (int i = 0; i < routeCenters.Length; i++)
            {
                float rotation = i % 2 == 0 ? -16f : 18f;
                CreateMeshBoxProp("非直角动线 主路弧形地板块 " + i, routeCenters[i], new Vector3(1.28f, 0.18f, 0.05f), floorDark, rotation);
                CreateModelProp("非直角动线 免费弯线地贴 " + i, i % 2 == 0 ? "Decals/Decal_Line_Bend1_R.fbx" : "Decals/Decal_Line_Bend2_L.fbx", routeCenters[i] + new Vector3(0f, 0f, 0.03f), new Vector3(0.72f, 0.36f, 0.04f), rotation, true);
                CreateMeshBoxProp("非直角动线 巡逻导光条 " + i, routeCenters[i] + new Vector3(0f, 0.16f, 0.08f), new Vector3(0.82f, 0.035f, 0.04f), i % 2 == 0 ? routeBlue : routeAmber, rotation);
            }
        }

        private void CreatePlayableSightlineBlockers()
        {
            Color darkMetal = new Color(0.025f, 0.032f, 0.035f, 1f);
            Color cable = new Color(0.08f, 0.09f, 0.09f, 1f);
            Color policeLight = new Color(0.08f, 0.38f, 0.95f, 1f);
            Color gangLight = new Color(0.9f, 0.12f, 0.08f, 1f);

            (Vector3 center, Vector3 size, float rotation)[] blockers =
            {
                (new Vector3(-5.92f, 0.55f, 0.2f), new Vector3(1.05f, 0.18f, 0.5f), -12f),
                (new Vector3(-3.05f, -1.18f, 0.2f), new Vector3(0.18f, 1.05f, 0.5f), 10f),
                (new Vector3(2.68f, -1.02f, 0.2f), new Vector3(1.1f, 0.18f, 0.5f), 12f),
                (new Vector3(5.02f, 1.02f, 0.2f), new Vector3(0.18f, 1.02f, 0.5f), -10f),
                (new Vector3(-7.82f, 3.18f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), 18f),
                (new Vector3(7.58f, 3.32f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), -18f),
                (new Vector3(-5.98f, -4.72f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), -8f),
                (new Vector3(3.68f, -4.72f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), 8f)
            };

            for (int i = 0; i < blockers.Length; i++)
            {
                CreateSolidMeshBoxProp("大场景港区层 可玩视线阻挡墙 " + i, blockers[i].center, blockers[i].size, darkMetal, blockers[i].rotation);
                CreateMeshBoxProp("大场景港区层 阻挡墙电缆 " + i, blockers[i].center + new Vector3(0f, 0f, 0.32f), new Vector3(blockers[i].size.x * 0.64f, 0.035f, 0.06f), cable, blockers[i].rotation);
                CreateMeshBoxProp("大场景港区层 阻挡墙警匪状态灯 " + i, blockers[i].center + new Vector3(0f, 0.11f, 0.42f), new Vector3(Mathf.Max(0.16f, blockers[i].size.x * 0.38f), 0.035f, 0.05f), i % 2 == 0 ? policeLight : gangLight, blockers[i].rotation);
            }
        }

        private void CreateOfficialFreeAssetStoreLayer()
        {
            CreateOfficialFreeRoadTiles();
            CreateOfficialFreeBuildingShells();
            CreateOfficialFreeStreetFurniture();
            CreateOfficialFreeVehicleSetPieces();
            CreateOfficialFreeCrowdAndTaskDressing();
            CreateDenseOfficialFreeStreetLayer();
        }

        private void CreateOfficialFreeRoadTiles()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation)[] roadTiles =
            {
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Crossroads_1", new Vector3(0f, -0.08f, -0.22f), new Vector3(1.55f, 1.15f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-3.85f, 0.08f, -0.22f), new Vector3(3.1f, 0.48f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(4.15f, -0.15f, -0.22f), new Vector3(3.25f, 0.48f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-6.95f, 3.78f, -0.22f), new Vector3(3.6f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(5.15f, 3.98f, -0.22f), new Vector3(3.95f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-6.25f, -3.72f, -0.22f), new Vector3(3.7f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(4.9f, -3.58f, -0.22f), new Vector3(3.6f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-7.18f, 1.45f, -0.22f), new Vector3(2.65f, 0.46f, 0.06f), 90f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(7.12f, 1.18f, -0.22f), new Vector3(2.85f, 0.46f, 0.06f), 90f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_turn", new Vector3(8.82f, 4.18f, -0.21f), new Vector3(0.92f, 0.82f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_turn", new Vector3(-7.0f, -4.42f, -0.21f), new Vector3(0.82f, 0.92f, 0.06f), 180f)
            };

            for (int i = 0; i < roadTiles.Length; i++)
            {
                GameObject tile = CreateAssetStoreProp("官方免费素材层 模块化道路 " + i, roadTiles[i].path, roadTiles[i].position, roadTiles[i].footprint, roadTiles[i].rotation, true);

                if (tile != null)
                {
                    tile.transform.SetAsFirstSibling();
                }
            }

            for (int i = 0; i < 10; i++)
            {
                float x = -8.5f + i * 1.9f;
                CreateAssetStoreProp("官方免费素材层 道路标记 " + i, AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Road_1_line", new Vector3(x, i % 2 == 0 ? 0.44f : -0.58f, -0.18f), new Vector3(0.42f, 0.16f, 0.04f), i % 2 == 0 ? 0f : 12f, false);
            }
        }

        private void CreateOfficialFreeBuildingShells()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation, bool solid)[] buildings =
            {
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Factory", new Vector3(-10.75f, 6.72f, 0.04f), new Vector3(1.35f, 0.78f, 0.9f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building Sky_big_color01", new Vector3(4.75f, 4.38f, 0.04f), new Vector3(1.22f, 0.78f, 1.28f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Coffee Shop", new Vector3(-4.82f, 2.68f, 0.04f), new Vector3(1.0f, 0.64f, 0.66f), 0f, false),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Restaurant", new Vector3(-0.9f, 3.95f, 0.04f), new Vector3(1.25f, 0.7f, 0.72f), 0f, false),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Drug Store", new Vector3(6.85f, -4.18f, 0.04f), new Vector3(1.0f, 0.64f, 0.74f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Auto Service", new Vector3(6.15f, -2.62f, 0.04f), new Vector3(1.1f, 0.7f, 0.72f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building Sky_small_color02", new Vector3(8.82f, 2.72f, 0.04f), new Vector3(0.92f, 0.62f, 1.0f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Super Market", new Vector3(-8.92f, -4.0f, 0.04f), new Vector3(1.18f, 0.72f, 0.76f), 0f, true)
            };

            for (int i = 0; i < buildings.Length; i++)
            {
                string name = "官方免费素材层 城市建筑壳 " + i;

                if (buildings[i].solid)
                {
                    CreateSolidAssetStoreProp(name, buildings[i].path, buildings[i].position, buildings[i].footprint, buildings[i].rotation, false);
                }
                else
                {
                    CreateAssetStoreProp(name, buildings[i].path, buildings[i].position, buildings[i].footprint, buildings[i].rotation, false);
                }
            }

            string[] syntyBuildings =
            {
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_04",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_07",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_10"
            };

            for (int i = 0; i < syntyBuildings.Length; i++)
            {
                CreateAssetStoreProp("官方免费素材层 远景楼宇补强 " + i, syntyBuildings[i], new Vector3(-10.2f + i * 6.6f, 7.02f, 0.22f), new Vector3(1.45f, 0.34f, 1.05f + i * 0.12f), 0f, false);
            }
        }

        private void CreateOfficialFreeStreetFurniture()
        {
            string[] furniture =
            {
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Bench_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Hydrant",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Traffic_cone",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Trash_can_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole_traffic_light",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Sewer_hatch",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Control Barrier Fence",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_medium",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light"
            };

            Vector3[] positions =
            {
                new Vector3(-5.1f, 0.78f, 0.1f),
                new Vector3(-8.4f, 3.42f, 0.1f),
                new Vector3(-3.4f, -3.12f, 0.1f),
                new Vector3(2.4f, -3.18f, 0.1f),
                new Vector3(4.25f, 0.92f, 0.1f),
                new Vector3(7.88f, 3.42f, 0.1f),
                new Vector3(-1.12f, -0.82f, 0.1f),
                new Vector3(0.68f, -4.74f, 0.1f),
                new Vector3(-1.05f, 4.02f, 0.1f),
                new Vector3(8.92f, -0.86f, 0.1f),
                new Vector3(-7.7f, -4.92f, 0.1f),
                new Vector3(6.8f, -4.84f, 0.1f),
                new Vector3(-10.2f, 4.24f, 0.1f),
                new Vector3(10.15f, -3.52f, 0.1f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                string path = furniture[i % furniture.Length];
                Vector3 footprint = i % 4 == 0 ? new Vector3(0.38f, 0.24f, 0.42f) : new Vector3(0.28f, 0.2f, 0.32f);

                if (i % 3 != 1)
                {
                    CreateSolidAssetStoreProp("官方免费素材层 街道小物 " + i, path, positions[i], footprint, i * 17f, false);
                }
                else
                {
                    CreateAssetStoreProp("官方免费素材层 街道小物 " + i, path, positions[i], footprint, i * 17f, false);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                CreateAssetStoreProp("官方免费素材层 行道树 " + i, AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Tree1", new Vector3(-10.8f + i * 3.05f, i % 2 == 0 ? 6.7f : -6.62f, 0.08f), new Vector3(0.42f, 0.42f, 0.82f), 0f, false);
            }
        }

        private void CreateOfficialFreeVehicleSetPieces()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation)[] vehicles =
            {
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car", new Vector3(0.88f, -6.0f, 0.1f), new Vector3(0.88f, 0.42f, 0.34f), 0f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi", new Vector3(5.45f, -2.58f, 0.1f), new Vector3(0.82f, 0.4f, 0.32f), -12f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Container_color01", new Vector3(-10.42f, 5.62f, 0.12f), new Vector3(1.05f, 0.45f, 0.42f), 4f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Container_color02", new Vector3(-8.9f, 6.18f, 0.12f), new Vector3(1.05f, 0.45f, 0.42f), -4f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Ambulance", new Vector3(7.1f, -5.62f, 0.1f), new Vector3(0.92f, 0.42f, 0.34f), 0f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color01", new Vector3(-2.1f, 6.82f, 0.1f), new Vector3(1.25f, 0.48f, 0.38f), 0f)
            };

            for (int i = 0; i < vehicles.Length; i++)
            {
                CreateSolidAssetStoreProp("官方免费素材层 车辆道具 " + i, vehicles[i].path, vehicles[i].position, vehicles[i].footprint, vehicles[i].rotation, false);
            }
        }

        private void CreateOfficialFreeCrowdAndTaskDressing()
        {
            string[] crowd =
            {
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Female_01",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Town_Female_01"
            };

            Vector3[] crowdPositions =
            {
                new Vector3(-5.55f, 4.54f, 0.12f),
                new Vector3(-4.22f, 1.26f, 0.12f),
                new Vector3(-1.88f, 3.55f, 0.12f),
                new Vector3(4.1f, 2.02f, 0.12f),
                new Vector3(5.82f, -0.82f, 0.12f),
                new Vector3(-8.25f, -4.52f, 0.12f)
            };

            for (int i = 0; i < crowdPositions.Length; i++)
            {
                GameObject character = CreateAssetStoreProp("官方免费素材层 场景人群 " + i, crowd[i % crowd.Length], crowdPositions[i], new Vector3(0.24f, 0.24f, 0.58f), i % 2 == 0 ? 0f : 180f, false);

                if (character != null)
                {
                    character.transform.localScale *= 0.78f;
                }
            }

            string[] taskProps =
            {
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Cardboard_Box_02",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/SM_PolygonPrototype_Prop_Ladder_1x2_01P",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Bus Stop"
            };

            if (_tasks == null) return;

            for (int i = 0; i < _tasks.Count; i += 3)
            {
                OnlineTaskState task = _tasks[i];
                Vector3 designPosition = new Vector3(task.Position.x / MapService.DesignScaleX, task.Position.y / MapService.DesignScaleY, 0.18f);
                CreateAssetStoreProp("官方免费素材层 任务旁实物 " + task.Id, taskProps[i % taskProps.Length], designPosition + new Vector3(0.42f, -0.28f, 0f), new Vector3(0.34f, 0.26f, 0.32f), i * 11f, false);
            }
        }

        private void CreateDenseOfficialFreeStreetLayer()
        {
            string[] shopBuildings =
            {
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Bar",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Bakery",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Chicken Shop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Clothing",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Fast Food",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Fruits  Shop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Gas Station",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Gift Shop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Music Store",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Pizza",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Residential_color01",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building Sky_big_color02"
            };

            (Vector3 position, Vector3 footprint, float rotation, bool solid)[] buildingPlacements =
            {
                (new Vector3(-11.2f, 5.48f, 0.18f), new Vector3(1.08f, 0.62f, 0.96f), 2f, true),
                (new Vector3(-9.92f, 6.86f, 0.18f), new Vector3(1.02f, 0.58f, 0.82f), -5f, true),
                (new Vector3(-5.92f, 6.72f, 0.18f), new Vector3(0.96f, 0.56f, 0.78f), 3f, true),
                (new Vector3(-4.1f, 2.94f, 0.18f), new Vector3(0.92f, 0.52f, 0.72f), -8f, false),
                (new Vector3(-2.05f, 4.2f, 0.18f), new Vector3(0.98f, 0.56f, 0.76f), 7f, false),
                (new Vector3(0.85f, 4.26f, 0.18f), new Vector3(1.05f, 0.58f, 0.8f), -6f, false),
                (new Vector3(3.52f, 3.95f, 0.18f), new Vector3(1.08f, 0.58f, 0.92f), 4f, true),
                (new Vector3(6.1f, 3.72f, 0.18f), new Vector3(1.0f, 0.56f, 0.76f), -3f, true),
                (new Vector3(8.4f, 4.02f, 0.18f), new Vector3(1.08f, 0.58f, 0.96f), 6f, true),
                (new Vector3(9.62f, 1.72f, 0.18f), new Vector3(0.96f, 0.52f, 0.84f), -7f, true),
                (new Vector3(7.55f, -2.2f, 0.18f), new Vector3(1.02f, 0.58f, 0.82f), 8f, true),
                (new Vector3(5.82f, -4.78f, 0.18f), new Vector3(1.1f, 0.62f, 0.88f), -4f, true),
                (new Vector3(1.72f, -6.28f, 0.18f), new Vector3(1.18f, 0.62f, 0.84f), 3f, true),
                (new Vector3(-3.68f, -5.98f, 0.18f), new Vector3(1.08f, 0.58f, 0.82f), -5f, true),
                (new Vector3(-7.82f, -5.92f, 0.18f), new Vector3(1.0f, 0.56f, 0.8f), 6f, true),
                (new Vector3(-10.35f, -3.64f, 0.18f), new Vector3(1.08f, 0.58f, 0.84f), -7f, true)
            };

            for (int i = 0; i < buildingPlacements.Length; i++)
            {
                string name = "官方免费街区密度层 临街铺面 " + i;

                if (buildingPlacements[i].solid)
                {
                    CreateSolidAssetStoreProp(name, shopBuildings[i % shopBuildings.Length], buildingPlacements[i].position, buildingPlacements[i].footprint, buildingPlacements[i].rotation, false);
                }
                else
                {
                    CreateAssetStoreProp(name, shopBuildings[i % shopBuildings.Length], buildingPlacements[i].position, buildingPlacements[i].footprint, buildingPlacements[i].rotation, false);
                }
            }

            CreateDenseOfficialFreeRoadFurniture();
            CreateDenseOfficialFreeTransitAndVehicleProps();
            CreateDenseOfficialFreeTaskAnchors();
        }

        private void CreateDenseOfficialFreeRoadFurniture()
        {
            string[] furniture =
            {
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Traffic_cone",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Traffic_light",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole_traffic_light",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Sewer_hatch",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Bench_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Trash_can_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Hydrant",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole1",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic cone",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Sign_stop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Signal_small",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_small",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Switch_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Keypad_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Papers_05",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Control Barrier Fence",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_large",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof Solar Panel",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof Antenna"
            };

            Vector3[] positions =
            {
                new Vector3(-8.78f, 4.28f, 0.14f),
                new Vector3(-7.42f, 4.12f, 0.14f),
                new Vector3(-5.85f, 3.54f, 0.14f),
                new Vector3(-4.62f, 2.42f, 0.14f),
                new Vector3(-3.16f, 0.72f, 0.14f),
                new Vector3(-1.62f, 1.46f, 0.14f),
                new Vector3(0.68f, 0.86f, 0.14f),
                new Vector3(2.2f, 1.28f, 0.14f),
                new Vector3(3.92f, 0.72f, 0.14f),
                new Vector3(5.18f, 1.55f, 0.14f),
                new Vector3(6.82f, 3.42f, 0.14f),
                new Vector3(8.18f, 4.55f, 0.14f),
                new Vector3(8.42f, 2.52f, 0.14f),
                new Vector3(7.62f, 0.12f, 0.14f),
                new Vector3(6.4f, -1.62f, 0.14f),
                new Vector3(4.52f, -2.62f, 0.14f),
                new Vector3(2.6f, -3.72f, 0.14f),
                new Vector3(0.42f, -4.62f, 0.14f),
                new Vector3(-1.62f, -4.48f, 0.14f),
                new Vector3(-3.85f, -3.68f, 0.14f),
                new Vector3(-5.92f, -3.85f, 0.14f),
                new Vector3(-7.52f, -4.55f, 0.14f),
                new Vector3(-8.72f, -2.18f, 0.14f),
                new Vector3(-7.82f, -0.42f, 0.14f),
                new Vector3(-6.92f, 1.28f, 0.14f),
                new Vector3(-4.02f, 4.12f, 0.14f),
                new Vector3(-0.85f, 3.84f, 0.14f),
                new Vector3(2.88f, 3.32f, 0.14f),
                new Vector3(5.65f, 4.32f, 0.14f),
                new Vector3(9.35f, -3.95f, 0.14f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                bool solid = i % 5 == 0 || i % 7 == 0;
                Vector3 footprint = i % 4 == 0
                    ? new Vector3(0.34f, 0.24f, 0.48f)
                    : i % 4 == 1
                        ? new Vector3(0.24f, 0.2f, 0.36f)
                        : new Vector3(0.2f, 0.18f, 0.3f);

                if (solid)
                {
                    CreateSolidAssetStoreProp("官方免费街区密度层 路边物件 " + i, furniture[i % furniture.Length], positions[i], footprint, i * 13f, false);
                }
                else
                {
                    CreateAssetStoreProp("官方免费街区密度层 路边物件 " + i, furniture[i % furniture.Length], positions[i], footprint, i * 13f, false);
                }
            }
        }

        private void CreateDenseOfficialFreeTransitAndVehicleProps()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation, bool solid)[] vehicles =
            {
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car", new Vector3(-1.1f, -5.92f, 0.14f), new Vector3(0.9f, 0.42f, 0.36f), 4f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car", new Vector3(1.42f, -5.78f, 0.14f), new Vector3(0.9f, 0.42f, 0.36f), -6f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi", new Vector3(-3.05f, 3.98f, 0.14f), new Vector3(0.84f, 0.38f, 0.32f), -11f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi", new Vector3(3.25f, -3.38f, 0.14f), new Vector3(0.84f, 0.38f, 0.32f), 14f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color02", new Vector3(-3.8f, 6.68f, 0.14f), new Vector3(1.2f, 0.48f, 0.4f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Truck_color01", new Vector3(-10.3f, 6.02f, 0.14f), new Vector3(1.12f, 0.46f, 0.4f), 3f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Container_color03", new Vector3(-9.45f, 4.82f, 0.14f), new Vector3(1.12f, 0.46f, 0.42f), -2f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Ambulance", new Vector3(7.78f, -5.15f, 0.14f), new Vector3(0.92f, 0.42f, 0.34f), -8f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_SUV_color02", new Vector3(8.45f, -1.88f, 0.14f), new Vector3(0.86f, 0.4f, 0.34f), 8f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color02", new Vector3(5.52f, -0.62f, 0.14f), new Vector3(0.88f, 0.4f, 0.34f), -12f, true)
            };

            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i].solid)
                {
                    CreateSolidAssetStoreProp("官方免费街区密度层 交通车辆 " + i, vehicles[i].path, vehicles[i].position, vehicles[i].footprint, vehicles[i].rotation, false);
                }
                else
                {
                    CreateAssetStoreProp("官方免费街区密度层 交通车辆 " + i, vehicles[i].path, vehicles[i].position, vehicles[i].footprint, vehicles[i].rotation, false);
                }
            }

            string[] pavement =
            {
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Pavement",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_5m",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Road_1_line",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Crossroads_1_lines_walk"
            };

            for (int i = 0; i < 14; i++)
            {
                float x = -10.2f + i * 1.58f;
                float y = i % 2 == 0 ? 6.36f : -6.12f;
                CreateAssetStoreProp("官方免费街区密度层 人行道铺面 " + i, pavement[i % pavement.Length], new Vector3(x, y, -0.2f), new Vector3(0.78f, 0.32f, 0.04f), i % 2 == 0 ? 0f : 180f, true);
            }
        }

        private void CreateDenseOfficialFreeTaskAnchors()
        {
            string[] anchors =
            {
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof prop air",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof_prop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Bus Stop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Dustbin",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Switch_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Manhole_01",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Sewer_hatch",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_medium"
            };

            if (_tasks == null) return;

            for (int i = 0; i < _tasks.Count; i++)
            {
                OnlineTaskState task = _tasks[i];
                Vector3 designPosition = new Vector3(task.Position.x / MapService.DesignScaleX, task.Position.y / MapService.DesignScaleY, 0.22f);
                Vector3 offset = new Vector3(i % 2 == 0 ? -0.38f : 0.38f, i % 3 == 0 ? 0.3f : -0.26f, 0f);
                CreateAssetStoreProp("官方免费街区密度层 任务实体锚点 " + task.Id, anchors[i % anchors.Length], designPosition + offset, new Vector3(0.34f, 0.26f, 0.34f), i * 19f, false);
            }
        }

        private void CreateLargeHongKongPortBackdrop()
        {
            Color skylineDark = new Color(0.024f, 0.032f, 0.04f, 1f);
            Color skylineMid = new Color(0.038f, 0.052f, 0.064f, 1f);
            Color windowBlue = new Color(0.08f, 0.44f, 0.58f, 1f);
            Color windowAmber = new Color(0.86f, 0.62f, 0.18f, 1f);

            for (int i = 0; i < 14; i++)
            {
                float x = -11.4f + i * 1.75f;
                float height = 0.46f + i % 5 * 0.16f;
                CreateMeshBoxProp("大场景港区层 远景香港楼宇体 " + i, new Vector3(x, 7.36f, height * 0.5f), new Vector3(1.0f + i % 3 * 0.22f, 0.16f, height), i % 2 == 0 ? skylineDark : skylineMid);

                for (int w = 0; w < 3; w++)
                {
                    CreateMeshBoxProp("大场景港区层 远景楼宇窗格 " + i + "-" + w, new Vector3(x - 0.28f + w * 0.28f, 7.47f, 0.22f + w * 0.12f), new Vector3(0.12f, 0.026f, 0.045f), (i + w) % 2 == 0 ? windowBlue : windowAmber);
                }
            }

            for (int i = 0; i < 10; i++)
            {
                float y = -5.8f + i * 1.18f;
                CreateMeshBoxProp("大场景港区层 西侧海面码头反光 " + i, new Vector3(-12.25f, y, -0.2f), new Vector3(0.74f, 0.035f, 0.04f), new Color(0.16f, 0.36f, 0.44f, 1f));
                CreateMeshBoxProp("大场景港区层 东侧海面码头反光 " + i, new Vector3(12.25f, y + 0.5f, -0.2f), new Vector3(0.66f, 0.035f, 0.04f), new Color(0.12f, 0.32f, 0.42f, 1f));
            }

            CreateMeshBoxProp("大场景港区层 远景青马桥剪影", new Vector3(0f, 7.05f, 0.42f), new Vector3(8.6f, 0.06f, 0.08f), new Color(0.08f, 0.1f, 0.1f, 1f));
            CreateMeshBoxProp("大场景港区层 远景桥塔左", new Vector3(-3.2f, 7.12f, 0.72f), new Vector3(0.08f, 0.08f, 0.78f), new Color(0.08f, 0.1f, 0.1f, 1f));
            CreateMeshBoxProp("大场景港区层 远景桥塔右", new Vector3(3.25f, 7.12f, 0.74f), new Vector3(0.08f, 0.08f, 0.82f), new Color(0.08f, 0.1f, 0.1f, 1f));
        }

        private void CreateLargeDistrictDepthSilhouettes()
        {
            Color nearShadow = new Color(0.006f, 0.008f, 0.01f, 0.82f);
            Color metalDark = new Color(0.035f, 0.044f, 0.048f, 1f);
            Color trim = new Color(0.42f, 0.48f, 0.48f, 1f);

            Vector3[] gantries =
            {
                new Vector3(-9.5f, 6.42f, 0.86f),
                new Vector3(-5.18f, 6.24f, 0.74f),
                new Vector3(8.7f, 6.12f, 0.8f),
                new Vector3(5.85f, -4.12f, 0.72f),
                new Vector3(-8.55f, -4.12f, 0.76f)
            };

            for (int i = 0; i < gantries.Length; i++)
            {
                Vector3 center = gantries[i];
                CreateMeshBoxProp("大场景港区层 区域门架横梁 " + i, center, new Vector3(2.25f, 0.12f, 0.14f), metalDark);
                CreateMeshBoxProp("大场景港区层 区域门架左柱 " + i, center + new Vector3(-1.05f, -0.18f, -0.32f), new Vector3(0.1f, 0.1f, 0.7f), metalDark);
                CreateMeshBoxProp("大场景港区层 区域门架右柱 " + i, center + new Vector3(1.05f, -0.18f, -0.32f), new Vector3(0.1f, 0.1f, 0.7f), metalDark);
                CreateMeshBoxProp("大场景港区层 区域门架冷光 " + i, center + new Vector3(0f, -0.08f, 0.08f), new Vector3(1.7f, 0.035f, 0.05f), trim);
            }

            Vector3[] foregroundShadows =
            {
                new Vector3(-9.4f, 3.58f, 0.82f),
                new Vector3(-4.8f, 0.72f, 0.78f),
                new Vector3(4.75f, 1.22f, 0.8f),
                new Vector3(8.85f, 3.72f, 0.82f),
                new Vector3(0.0f, -4.1f, 0.78f),
                new Vector3(6.15f, -3.68f, 0.8f)
            };

            for (int i = 0; i < foregroundShadows.Length; i++)
            {
                CreateMeshBoxProp("大场景港区层 近景房檐投影 " + i, foregroundShadows[i], new Vector3(2.4f, 0.18f, 0.42f), nearShadow, i % 2 == 0 ? 0f : 4f);
            }
        }

        private void CreateLargePlayableSightlineSetPieces()
        {
            Color wall = new Color(0.025f, 0.034f, 0.038f, 1f);
            Color accent = new Color(0.08f, 0.68f, 0.84f, 1f);
            Color warning = new Color(0.9f, 0.68f, 0.08f, 1f);

            Vector3[] blockers =
            {
                new Vector3(-5.68f, 4.18f, 0.22f),
                new Vector3(-3.22f, 3.18f, 0.22f),
                new Vector3(2.85f, 3.12f, 0.22f),
                new Vector3(5.88f, 1.08f, 0.22f),
                new Vector3(3.48f, -2.78f, 0.22f),
                new Vector3(-4.72f, -2.82f, 0.22f),
                new Vector3(-8.18f, -1.12f, 0.22f),
                new Vector3(7.88f, -1.12f, 0.22f)
            };

            for (int i = 0; i < blockers.Length; i++)
            {
                bool horizontal = i % 2 == 0;
                Vector3 scale = horizontal ? new Vector3(1.35f, 0.16f, 0.42f) : new Vector3(0.18f, 1.08f, 0.42f);
                CreateSolidMeshBoxProp("大场景港区层 真实视线阻挡设备 " + i, blockers[i], scale, wall, i % 3 == 0 ? -8f : 8f);
                CreateMeshBoxProp("大场景港区层 阻挡设备编号灯 " + i, blockers[i] + new Vector3(0f, 0.08f, 0.28f), horizontal ? new Vector3(0.78f, 0.035f, 0.05f) : new Vector3(0.035f, 0.62f, 0.05f), i % 2 == 0 ? accent : warning, i % 3 == 0 ? -8f : 8f);
            }

            for (int i = 0; i < 8; i++)
            {
                float x = -7.4f + i * 2.1f;
                CreateMeshBoxProp("大场景港区层 可读性道路弧线 " + i, new Vector3(x, i % 2 == 0 ? -0.92f : 0.68f, 0.04f), new Vector3(0.92f, 0.035f, 0.04f), i % 2 == 0 ? warning : accent, i % 2 == 0 ? -14f : 14f);
            }
        }

        private void CreateCommercialArtAdapterLayer()
        {
            Color policeBlue = new Color(0.08f, 0.28f, 0.88f, 1f);
            Color gangRed = new Color(0.82f, 0.1f, 0.08f, 1f);
            Color neonCyan = new Color(0.08f, 0.72f, 0.9f, 1f);
            Color neonAmber = new Color(0.9f, 0.68f, 0.12f, 1f);
            Color neonPink = new Color(0.94f, 0.18f, 0.46f, 1f);
            Color steel = new Color(0.08f, 0.1f, 0.12f, 1f);
            Color glass = new Color(0.12f, 0.38f, 0.46f, 1f);

            CreateMeshBoxProp("资源适配层 港区主门头", new Vector3(0f, 6.95f, 0.96f), new Vector3(4.2f, 0.16f, 0.26f), policeBlue);
            CreateMeshBoxProp("资源适配层 港区副门头", new Vector3(-8.95f, 4.72f, 0.9f), new Vector3(2.2f, 0.12f, 0.22f), gangRed);
            CreateMeshBoxProp("资源适配层 港区夜市大灯牌", new Vector3(-1.1f, 4.02f, 0.82f), new Vector3(2.42f, 0.1f, 0.24f), neonPink);
            CreateMeshBoxProp("资源适配层 港区证物区门头", new Vector3(-8.7f, -3.98f, 0.86f), new Vector3(2.0f, 0.1f, 0.22f), neonCyan);
            CreateMeshBoxProp("资源适配层 港区诊所灯箱", new Vector3(7.62f, -4.9f, 0.88f), new Vector3(1.72f, 0.1f, 0.22f), neonAmber);
            CreateMeshBoxProp("资源适配层 港区金融楼顶牌", new Vector3(4.8f, 4.1f, 0.9f), new Vector3(1.98f, 0.1f, 0.22f), neonAmber);
            CreateMeshBoxProp("资源适配层 港区电房告示墙", new Vector3(8.9f, 6.0f, 0.86f), new Vector3(1.7f, 0.08f, 0.2f), policeBlue);
            CreateMeshBoxProp("资源适配层 港区指挥车指示板", new Vector3(0f, -6.02f, 0.84f), new Vector3(2.1f, 0.08f, 0.2f), neonCyan);

            CreateModelProp("资源适配层 港区门卫岗亭", "Props/Prop_AccessPoint.fbx", new Vector3(-7.52f, 4.92f, 0.12f), new Vector3(0.72f, 0.48f, 0.38f), 90f);
            CreateModelProp("资源适配层 港区广播终端", "Props/Prop_Computer.fbx", new Vector3(0.88f, 6.28f, 0.12f), new Vector3(0.72f, 0.46f, 0.36f), 0f);
            CreateModelProp("资源适配层 港区门禁闸机", "Platforms/Door_Frame_A.fbx", new Vector3(-4.95f, 5.06f, 0.14f), new Vector3(0.56f, 0.98f, 0.42f), 90f, true);
            CreateModelProp("资源适配层 港区大箱体", "Props/Prop_Chest.fbx", new Vector3(6.8f, -1.92f, 0.12f), new Vector3(0.74f, 0.52f, 0.42f), -8f);
            CreateModelProp("资源适配层 港区灯柱", "Props/Prop_Light_Wide.fbx", new Vector3(9.62f, 4.92f, 0.18f), new Vector3(0.68f, 0.22f, 0.22f), 0f, true);
            CreateModelProp("资源适配层 港区通风架", "Props/Prop_Vent_Big.fbx", new Vector3(-2.1f, -0.94f, 0.12f), new Vector3(0.74f, 0.42f, 0.24f), 0f, true);
            CreateModelProp("资源适配层 港区钢架", "Platforms/Platform_Rails_4Wide.fbx", new Vector3(2.2f, 0.84f, 0.16f), new Vector3(1.28f, 0.26f, 0.38f), 0f, true);

            CreateSolidMeshBoxProp("资源适配层 港区入口钢箱", new Vector3(-10.16f, 5.48f, 0.1f), new Vector3(1.28f, 0.42f, 0.36f), steel, 4f);
            CreateSolidMeshBoxProp("资源适配层 港区侧边玻璃棚", new Vector3(6.16f, 2.42f, 0.12f), new Vector3(1.14f, 0.28f, 0.26f), glass, -6f);
            CreateSolidMeshBoxProp("资源适配层 港区检修高柜", new Vector3(-6.48f, -4.12f, 0.12f), new Vector3(0.72f, 0.42f, 0.46f), steel, 10f);
        }

        private void CreateExteriorDockVista()
        {
            Color water = new Color(0.03f, 0.08f, 0.1f, 1f);
            Color dock = new Color(0.12f, 0.14f, 0.13f, 1f);
            Color crane = new Color(0.84f, 0.58f, 0.08f, 1f);
            CreateShapeProp("维港远景水面西", RoundedRectSprite, new Vector3(-12.2f, 3.0f, -0.32f), new Vector3(1.6f, 8.6f, 0.06f), water);
            CreateShapeProp("维港远景水面东", RoundedRectSprite, new Vector3(12.2f, -2.8f, -0.32f), new Vector3(1.6f, 8.4f, 0.06f), water);
            CreateMeshBoxProp("码头外缘泊位线西", new Vector3(-10.72f, 4.32f, 0.08f), new Vector3(0.08f, 2.65f, 0.12f), dock);
            CreateMeshBoxProp("码头外缘泊位线东", new Vector3(10.72f, -2.72f, 0.08f), new Vector3(0.08f, 2.5f, 0.12f), dock);
            CreateSolidProp("外景集装箱堆 A", new Vector3(-10.92f, 5.88f, 0.05f), new Vector3(0.74f, 0.34f, 0.18f), new Color(0.08f, 0.24f, 0.52f, 1f));
            CreateSolidProp("外景集装箱堆 B", new Vector3(-10.98f, 5.42f, 0.05f), new Vector3(0.72f, 0.34f, 0.18f), new Color(0.58f, 0.12f, 0.08f, 1f));
            CreateSolidProp("外景集装箱堆 C", new Vector3(10.86f, -4.72f, 0.05f), new Vector3(0.74f, 0.34f, 0.18f), new Color(0.12f, 0.38f, 0.2f, 1f));
            CreateMeshBoxProp("外景龙门吊立柱 A", new Vector3(-10.72f, 5.2f, 0.44f), new Vector3(0.08f, 1.42f, 0.64f), crane);
            CreateMeshBoxProp("外景龙门吊横梁 A", new Vector3(-10.72f, 5.86f, 0.84f), new Vector3(0.92f, 0.06f, 0.08f), crane);
            CreateMeshBoxProp("外景龙门吊吊钩 A", new Vector3(-10.34f, 5.62f, 0.48f), new Vector3(0.08f, 0.42f, 0.08f), new Color(0.05f, 0.05f, 0.05f, 1f));
            CreateMeshBoxProp("东侧巡逻船体", new Vector3(10.88f, -1.42f, 0.06f), new Vector3(0.92f, 0.36f, 0.16f), new Color(0.08f, 0.16f, 0.22f, 1f));
            CreateMeshBoxProp("东侧巡逻船警灯", new Vector3(10.88f, -1.12f, 0.22f), new Vector3(0.52f, 0.05f, 0.06f), new Color(0.08f, 0.36f, 0.92f, 1f));

            for (int i = 0; i < 8; i++)
            {
                float y = -5.8f + i * 1.42f;
                CreateProp("水面反光西 " + i, new Vector3(-12.18f, y, -0.26f), new Vector3(0.68f, 0.035f, 0.04f), new Color(0.18f, 0.38f, 0.42f, 1f));
                CreateProp("水面反光东 " + i, new Vector3(12.18f, y + 0.62f, -0.26f), new Vector3(0.62f, 0.035f, 0.04f), new Color(0.16f, 0.34f, 0.42f, 1f));
            }
        }

        private void CreateDistrictIdentityLandmarks()
        {
            Color neonPink = new Color(0.96f, 0.16f, 0.46f, 1f);
            Color neonBlue = new Color(0.06f, 0.72f, 0.9f, 1f);
            Color amber = new Color(0.9f, 0.66f, 0.12f, 1f);
            CreateMeshBoxProp("茶餐厅大型霓虹牌底", new Vector3(-4.8f, 2.42f, 0.52f), new Vector3(1.2f, 0.08f, 0.26f), new Color(0.08f, 0.035f, 0.03f, 1f));
            CreateMeshBoxProp("茶餐厅大型霓虹字 A", new Vector3(-5.08f, 2.46f, 0.66f), new Vector3(0.42f, 0.035f, 0.05f), neonPink);
            CreateMeshBoxProp("茶餐厅大型霓虹字 B", new Vector3(-4.52f, 2.46f, 0.66f), new Vector3(0.42f, 0.035f, 0.05f), amber);
            CreateMeshBoxProp("金融楼洗钱账房招牌", new Vector3(4.78f, 3.72f, 0.62f), new Vector3(1.35f, 0.07f, 0.28f), new Color(0.04f, 0.05f, 0.08f, 1f));
            CreateMeshBoxProp("金融楼招牌蓝线", new Vector3(4.78f, 3.76f, 0.78f), new Vector3(1.08f, 0.03f, 0.04f), neonBlue);
            CreateMeshBoxProp("夜市棚顶排档灯箱", new Vector3(-1.02f, 3.74f, 0.48f), new Vector3(1.8f, 0.08f, 0.22f), new Color(0.2f, 0.04f, 0.04f, 1f));
            CreateMeshBoxProp("夜市灯箱霓虹线", new Vector3(-1.02f, 3.78f, 0.62f), new Vector3(1.48f, 0.035f, 0.05f), neonPink);
            CreateMeshBoxProp("证物库冷链大门", new Vector3(-7.08f, -5.05f, 0.42f), new Vector3(0.08f, 1.08f, 0.42f), new Color(0.08f, 0.24f, 0.28f, 1f));
            CreateMeshBoxProp("证物库冷链状态灯", new Vector3(-7.02f, -4.72f, 0.68f), new Vector3(0.035f, 0.38f, 0.05f), neonBlue);
            CreateMeshBoxProp("地下诊所唐楼外墙牌", new Vector3(7.62f, -5.02f, 0.54f), new Vector3(0.08f, 0.88f, 0.3f), new Color(0.08f, 0.16f, 0.12f, 1f));
            CreateMeshBoxProp("地下诊所十字灯", new Vector3(7.66f, -5.02f, 0.72f), new Vector3(0.04f, 0.46f, 0.04f), new Color(0.52f, 0.92f, 0.78f, 1f));
            CreateMeshBoxProp("地下诊所十字灯横", new Vector3(7.66f, -5.02f, 0.72f), new Vector3(0.04f, 0.08f, 0.24f), new Color(0.52f, 0.92f, 0.78f, 1f));
            CreateKeyLandmarkVisuals(neonPink, neonBlue, amber);

            for (int i = 0; i < 6; i++)
            {
                CreateMeshBoxProp("货柜区编号灯 " + i, new Vector3(-10.78f + i * 0.58f, 6.08f, 0.32f), new Vector3(0.26f, 0.035f, 0.06f), i % 2 == 0 ? amber : neonBlue);
                CreateMeshBoxProp("电房高压警示灯 " + i, new Vector3(8.0f + i * 0.34f, 6.1f, 0.36f), new Vector3(0.16f, 0.035f, 0.06f), i % 2 == 0 ? Color.red : amber);
            }
        }

        private void CreateKeyLandmarkVisuals(Color neonPink, Color neonBlue, Color amber)
        {
            Sprite2DAssetCache.Ensure();

            CreateLimeZuLandmarkProp("关键地标 LimeZu 茶餐厅主招牌", Sprite2DAssetCache.LandmarkOfficeSign1,
                Sprite2DAssetCache.LandmarkOfficeSign1ResourcePath, new Vector3(-4.82f, 2.22f, 0.52f),
                new Vector3(1.62f, 1.72f, 0.08f), Color.white, -4f);
            CreateLimeZuLandmarkProp("关键地标 LimeZu 茶餐厅开门", Sprite2DAssetCache.LandmarkDoorOpen,
                Sprite2DAssetCache.LandmarkDoorOpenResourcePath, new Vector3(-5.58f, 1.9f, 0.36f),
                new Vector3(0.46f, 0.58f, 0.08f), Color.white, -4f);
            CreateMeshBoxProp("关键地标 茶餐厅霓虹灯带", new Vector3(-4.82f, 2.42f, 0.68f), new Vector3(1.08f, 0.035f, 0.05f), neonPink, -4f);

            CreateLimeZuLandmarkProp("关键地标 LimeZu 夜市遮阳棚", Sprite2DAssetCache.LandmarkUmbrella,
                Sprite2DAssetCache.LandmarkUmbrellaResourcePath, new Vector3(-1.18f, 3.36f, 0.5f),
                new Vector3(0.64f, 0.62f, 0.08f), Color.white, 4f);
            CreateLimeZuLandmarkProp("关键地标 LimeZu 夜市桌饮", Sprite2DAssetCache.LandmarkTinyTable,
                Sprite2DAssetCache.LandmarkTinyTableResourcePath, new Vector3(-0.38f, 3.26f, 0.36f),
                new Vector3(0.42f, 0.42f, 0.08f), Color.white, 4f);
            CreateMeshBoxProp("关键地标 夜市拱门粉灯", new Vector3(-0.92f, 3.62f, 0.74f), new Vector3(1.68f, 0.035f, 0.05f), neonPink, 4f);

            CreateLimeZuLandmarkProp("关键地标 LimeZu 证物库封存箱", Sprite2DAssetCache.LandmarkPackage,
                Sprite2DAssetCache.LandmarkPackageResourcePath, new Vector3(-7.72f, -4.82f, 0.4f),
                new Vector3(0.42f, 0.38f, 0.08f), Color.white, -3f);
            CreateLimeZuLandmarkProp("关键地标 LimeZu 证物库投递箱", Sprite2DAssetCache.LandmarkMailbox,
                Sprite2DAssetCache.LandmarkMailboxResourcePath, new Vector3(-7.3f, -5.24f, 0.4f),
                new Vector3(0.72f, 0.54f, 0.08f), Color.white);
            CreateMeshBoxProp("关键地标 证物库温控蓝灯", new Vector3(-7.36f, -4.98f, 0.64f), new Vector3(0.04f, 0.56f, 0.05f), neonBlue);

            CreateLimeZuLandmarkProp("关键地标 LimeZu 金融楼招牌", Sprite2DAssetCache.LandmarkOfficeSign2,
                Sprite2DAssetCache.LandmarkOfficeSign2ResourcePath, new Vector3(4.82f, 3.7f, 0.58f),
                new Vector3(1.52f, 1.62f, 0.08f), Color.white);
            CreateLimeZuLandmarkProp("关键地标 LimeZu 金融楼金库通风", Sprite2DAssetCache.LandmarkAirDuct,
                Sprite2DAssetCache.LandmarkAirDuctResourcePath, new Vector3(5.52f, 3.22f, 0.38f),
                new Vector3(0.48f, 0.48f, 0.08f), Color.white);
            CreateMeshBoxProp("关键地标 金融楼金库警戒线", new Vector3(4.94f, 3.28f, 0.58f), new Vector3(0.68f, 0.035f, 0.05f), amber);

            CreateLimeZuLandmarkProp("关键地标 LimeZu 电房高压门", Sprite2DAssetCache.LandmarkDoorOpen,
                Sprite2DAssetCache.LandmarkDoorOpenResourcePath, new Vector3(8.12f, 5.64f, 0.42f),
                new Vector3(0.5f, 0.56f, 0.08f), Color.white);
            CreateLimeZuLandmarkProp("关键地标 LimeZu 电房屋顶设备", Sprite2DAssetCache.LandmarkAirDuct,
                Sprite2DAssetCache.LandmarkAirDuctResourcePath, new Vector3(8.84f, 5.92f, 0.58f),
                new Vector3(0.58f, 0.52f, 0.08f), Color.white);
            CreateMeshBoxProp("关键地标 电房红色警报条", new Vector3(8.45f, 5.9f, 0.66f), new Vector3(0.86f, 0.035f, 0.05f), Color.red);

            CreateLimeZuLandmarkProp("关键地标 LimeZu 码头冷链车", Sprite2DAssetCache.LandmarkTruckFront,
                Sprite2DAssetCache.LandmarkTruckFrontResourcePath, new Vector3(-10.38f, 5.5f, 0.5f),
                new Vector3(1.18f, 0.96f, 0.08f), Color.white, -2f);
            CreateLimeZuLandmarkProp("关键地标 LimeZu 码头绿植路障", Sprite2DAssetCache.LandmarkPottedPlant,
                Sprite2DAssetCache.LandmarkPottedPlantResourcePath, new Vector3(-9.52f, 5.96f, 0.36f),
                new Vector3(0.38f, 0.42f, 0.08f), Color.white);
            CreateMeshBoxProp("关键地标 码头吊机钢缆", new Vector3(-10.08f, 5.88f, 0.66f), new Vector3(0.04f, 0.42f, 0.04f), new Color(0.02f, 0.025f, 0.028f, 1f));
        }

        private void CreateShipLikeSightlineWalls()
        {
            Color bulkhead = new Color(0.025f, 0.035f, 0.04f, 1f);
            Color highlight = new Color(0.42f, 0.5f, 0.5f, 1f);
            Vector3[] wallCenters =
            {
                new Vector3(-2.95f, 1.18f, 0.38f),
                new Vector3(3.05f, 1.18f, 0.38f),
                new Vector3(-3.05f, -1.58f, 0.36f),
                new Vector3(3.05f, -1.58f, 0.36f),
                new Vector3(-8.18f, -2.42f, 0.36f),
                new Vector3(8.24f, 2.62f, 0.36f)
            };

            for (int i = 0; i < wallCenters.Length; i++)
            {
                Vector3 center = wallCenters[i];
                bool horizontal = i < 4;
                Vector3 scale = horizontal ? new Vector3(1.4f, 0.12f, 0.42f) : new Vector3(0.12f, 1.35f, 0.42f);
                CreateSolidProp("视线遮挡厚舱壁 " + i, center, scale, bulkhead);
                CreateMeshBoxProp("视线遮挡舱壁高光 " + i, center + new Vector3(0f, horizontal ? 0.08f : 0f, 0.26f), horizontal ? new Vector3(1.1f, 0.035f, 0.06f) : new Vector3(0.035f, 1.05f, 0.06f), highlight);
            }
        }

        private void CreateRoundEndShowcaseSet()
        {
            Color police = new Color(0.08f, 0.32f, 0.82f, 1f);
            Color gang = new Color(0.78f, 0.08f, 0.06f, 1f);
            CreateMeshBoxProp("结算舞台警方投影", new Vector3(-0.62f, 0.18f, 0.58f), new Vector3(0.52f, 0.05f, 0.3f), police);
            CreateMeshBoxProp("结算舞台黑帮投影", new Vector3(0.62f, 0.18f, 0.58f), new Vector3(0.52f, 0.05f, 0.3f), gang);
            CreateMeshBoxProp("结算舞台证据时间线", new Vector3(0f, -0.92f, 0.18f), new Vector3(1.9f, 0.055f, 0.08f), new Color(0.84f, 0.72f, 0.22f, 1f));
            CreateMeshBoxProp("结算舞台投票箱", new Vector3(0f, -0.35f, 0.42f), new Vector3(0.42f, 0.28f, 0.34f), new Color(0.12f, 0.16f, 0.17f, 1f));
        }

        private void CreateCorridorFloorPanels()
        {
            Color seam = new Color(0.34f, 0.4f, 0.4f, 1f);
            Color plateA = new Color(0.14f, 0.165f, 0.17f, 1f);
            Color plateB = new Color(0.12f, 0.145f, 0.15f, 1f);

            for (int i = 0; i < 13; i++)
            {
                float x = -6.9f + i * 1.15f;
                CreateProp("主横连廊可拆地板 " + i, new Vector3(x, -0.18f, -0.075f), new Vector3(0.82f, 0.34f, 0.04f), i % 2 == 0 ? plateA : plateB);
                CreateProp("主横连廊地板编号条 " + i, new Vector3(x, 0.18f, -0.04f), new Vector3(0.34f, 0.035f, 0.04f), seam);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = -6.5f + i * 1.18f;
                CreateProp("上层连廊可拆地板 " + i, new Vector3(x, 3.65f, -0.075f), new Vector3(0.78f, 0.3f, 0.04f), i % 2 == 0 ? plateB : plateA);
                CreateProp("下层连廊可拆地板 " + i, new Vector3(x + 0.18f, -3.9f, -0.075f), new Vector3(0.78f, 0.3f, 0.04f), i % 2 == 0 ? plateA : plateB);
            }

            for (int i = 0; i < 7; i++)
            {
                float y = -2.9f + i * 0.95f;
                CreateProp("左竖连廊竖向舱板 " + i, new Vector3(-6.85f, y, -0.075f), new Vector3(0.3f, 0.62f, 0.04f), i % 2 == 0 ? plateA : plateB);
                CreateProp("右竖连廊竖向舱板 " + i, new Vector3(7.05f, y, -0.075f), new Vector3(0.3f, 0.62f, 0.04f), i % 2 == 0 ? plateB : plateA);
            }
        }

        private void CreateCorridorCameraNetwork()
        {
            Vector3[] positions =
            {
                new Vector3(-5.6f, 0.54f, 0.34f),
                new Vector3(-0.8f, 0.54f, 0.34f),
                new Vector3(4.25f, 0.38f, 0.34f),
                new Vector3(-6.55f, 3.12f, 0.34f),
                new Vector3(6.78f, 3.12f, 0.34f),
                new Vector3(-6.55f, -3.34f, 0.34f),
                new Vector3(6.95f, -3.34f, 0.34f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                CreateWallCamera("走廊监控 " + i, positions[i], i % 2 == 0 ? 0f : 180f);
            }
        }

        private void CreateWallCamera(string name, Vector3 position, float rotation)
        {
            CreateModelProp(name + " CC0 支架", "Props/Prop_Clamp.fbx", position + new Vector3(0f, 0f, 0.02f), new Vector3(0.22f, 0.18f, 0.2f), rotation);
            CreateMeshBoxProp(name + " 机身", position + new Vector3(0f, 0.08f, 0.08f), new Vector3(0.18f, 0.12f, 0.1f), new Color(0.04f, 0.055f, 0.06f, 1f), rotation);
            CreateMeshPrimitiveProp(name + " 镜头", PrimitiveType.Sphere, position + new Vector3(0f, 0.17f, 0.08f), new Vector3(0.08f, 0.08f, 0.06f), new Color(0.08f, 0.78f, 0.92f, 1f), Quaternion.identity);
        }

        private void CreateCorridorCableRuns()
        {
            Color cable = new Color(0.025f, 0.035f, 0.038f, 1f);
            Color signal = new Color(0.08f, 0.64f, 0.78f, 1f);

            for (int i = 0; i < 8; i++)
            {
                float x = -7.2f + i * 2.05f;
                CreateModelProp("CC0 主廊线缆 " + i, "Props/Prop_Cable_1.fbx", new Vector3(x, 0.5f, 0.18f), new Vector3(0.78f, 0.08f, 0.12f), i % 2 == 0 ? 0f : 180f, true);
                CreateProp("主廊线缆阴影 " + i, new Vector3(x, 0.44f, 0.06f), new Vector3(0.78f, 0.035f, 0.04f), cable);
            }

            for (int i = 0; i < 6; i++)
            {
                float y = -2.45f + i * 0.98f;
                CreateModelProp("CC0 左竖管线 " + i, "Props/Prop_Cable_3.fbx", new Vector3(-7.42f, y, 0.16f), new Vector3(0.08f, 0.62f, 0.12f), 90f, true);
                CreateModelProp("CC0 右竖管线 " + i, "Props/Prop_Cable_3.fbx", new Vector3(7.58f, y, 0.16f), new Vector3(0.08f, 0.62f, 0.12f), 90f, true);
                CreateProp("右竖状态光点 " + i, new Vector3(7.34f, y + 0.18f, 0.11f), new Vector3(0.06f, 0.045f, 0.04f), signal);
            }
        }

        private void CreateRoomMicroProps()
        {
            foreach (OnlineMapService.ShipRoomSpec room in MapService.ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                Color label = DoorColor(room);

                CreateModelProp("CC0 " + room.Name + " 墙面窄灯", "Props/Prop_Light_Small.fbx", room.Center + new Vector3(-halfWidth + 0.4f, halfHeight - 0.28f, 0.26f), new Vector3(0.18f, 0.18f, 0.16f), 0f);
                CreateMeshBoxProp("屋顶 " + room.Name + " 门牌背光", room.Center + new Vector3(halfWidth * 0.18f, halfHeight - 0.3f, 0.31f), new Vector3(Mathf.Min(0.72f, room.Size.x * 0.22f), 0.04f, 0.08f), label);
                CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 小型通风百叶", room.Center + new Vector3(halfWidth - 0.32f, halfHeight - 0.12f, 0.36f), new Vector3(0.28f, 0.035f, 0.13f), new Color(0.04f, 0.055f, 0.06f, 1f));
                bool rustedVent = room.Label.IndexOf("黑市", StringComparison.Ordinal) >= 0
                    || room.Label.IndexOf("后巷", StringComparison.Ordinal) >= 0;
                Sprite ventSprite = rustedVent ? Sprite2DAssetCache.KowloonVentIcon : Sprite2DAssetCache.VentIcon;
                string ventPath = rustedVent ? Sprite2DAssetCache.KowloonVentIconResourcePath : Sprite2DAssetCache.VentIconResourcePath;
                CreateRuntimeMapProp("地图小件 " + room.Name + " 房间百叶贴图", ventSprite, ventPath,
                    room.Center + new Vector3(halfWidth - 0.32f, halfHeight - 0.12f, 0.38f),
                    new Vector3(0.28f, 0.28f, 0.08f), Color.white, rustedVent ? -4f : 0f);

                for (int i = 0; i < 3; i++)
                {
                    CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 百叶缝 " + i, room.Center + new Vector3(halfWidth - 0.32f, halfHeight - 0.095f, 0.32f + i * 0.045f), new Vector3(0.23f, 0.025f, 0.015f), new Color(0.46f, 0.52f, 0.52f, 1f));
                }
            }
        }

        private void CreateExteriorHullProps()
        {
            Color red = new Color(0.86f, 0.08f, 0.08f, 1f);
            Color blue = new Color(0.08f, 0.28f, 0.82f, 1f);
            Color amber = new Color(0.92f, 0.68f, 0.08f, 1f);

            for (int i = 0; i < 9; i++)
            {
                float x = -8.6f + i * 2.15f;
                CreateMeshPrimitiveProp("屋顶 外壳应急警灯红 " + i, PrimitiveType.Cylinder, new Vector3(x, 6.78f, 0.16f), new Vector3(0.12f, 0.12f, 0.08f), i % 2 == 0 ? red : blue, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPrimitiveProp("屋顶 南外壳定位灯 " + i, PrimitiveType.Cylinder, new Vector3(x + 0.72f, -6.78f, 0.16f), new Vector3(0.1f, 0.1f, 0.08f), amber, Quaternion.Euler(90f, 0f, 0f));
            }

            CreateModelProp("CC0 左侧维修梯", "Platforms/Platform_Stairs_4Wide.fbx", new Vector3(-10.9f, -2.8f, 0.18f), new Vector3(0.64f, 1.2f, 0.36f), 90f, true);
            CreateModelProp("CC0 右侧维修梯", "Platforms/Platform_Stairs_4Wide.fbx", new Vector3(10.75f, 2.7f, 0.18f), new Vector3(0.64f, 1.2f, 0.36f), -90f, true);
        }

        private void CreateCorridorServiceProps()
        {
            Color screen = new Color(0.06f, 0.62f, 0.78f, 1f);
            Color warning = new Color(0.86f, 0.66f, 0.08f, 1f);

            for (int i = 0; i < 5; i++)
            {
                float x = -5.4f + i * 2.7f;
                CreateRuntimeMapProp("地图小件 主走廊壁柜 " + i, Sprite2DAssetCache.PropKowloonCrate,
                    Sprite2DAssetCache.KowloonPropCrateResourcePath, new Vector3(x, 0.58f, 0.07f),
                    new Vector3(0.32f, 0.22f, 0.2f), Color.white, i % 2 == 0 ? -2f : 2f, true);
                CreateProp("主走廊壁柜屏 " + i, new Vector3(x, 0.72f, 0.2f), new Vector3(0.22f, 0.04f, 0.06f), screen);
                Sprite supplySprite = i % 2 == 0 ? Sprite2DAssetCache.PropCrate : Sprite2DAssetCache.PropBarrel;
                string supplyPath = i % 2 == 0 ? Sprite2DAssetCache.PropCrateResourcePath : Sprite2DAssetCache.PropBarrelResourcePath;
                CreateRuntimeMapProp("地图小件 下层走廊补给箱 " + i, supplySprite, supplyPath,
                    new Vector3(-5.2f + i * 2.55f, -4.42f, 0.07f), new Vector3(0.42f, 0.28f, 0.18f),
                    Color.white, i % 2 == 0 ? 3f : -4f, true);
            }

            for (int i = 0; i < 4; i++)
            {
                float y = -2.5f + i * 1.45f;
                CreateRuntimeMapProp("地图小件 左竖连廊封控箱 " + i, Sprite2DAssetCache.PropCrate,
                    Sprite2DAssetCache.PropCrateResourcePath, new Vector3(-7.45f, y, 0.07f),
                    new Vector3(0.26f, 0.34f, 0.18f), Color.white, 90f, true);
                CreateRuntimeMapProp("地图小件 右竖连廊封控箱 " + i, Sprite2DAssetCache.PropKowloonCrate,
                    Sprite2DAssetCache.KowloonPropCrateResourcePath, new Vector3(7.65f, y, 0.07f),
                    new Vector3(0.26f, 0.34f, 0.18f), Color.white, -90f, true);
            }

            CreateProp("主走廊红色警戒条", new Vector3(2.2f, 0.52f, 0.08f), new Vector3(1.2f, 0.08f, 0.08f), new Color(0.86f, 0.08f, 0.06f, 1f));
            CreateProp("上层走廊证物导线", new Vector3(-1.8f, 4.14f, 0.08f), new Vector3(1.6f, 0.055f, 0.08f), new Color(0.48f, 0.84f, 0.82f, 1f));
            CreateProp("下层走廊警戒导线", new Vector3(3.2f, -3.36f, 0.08f), new Vector3(1.4f, 0.055f, 0.08f), warning);
        }

        private void CreateQuaterniusModelDressing()
        {
            CreateModelRoomKits();
            CreateModelCorridorKits();
            CreateModelFloorPlates();
        }

        private void CreateModelRoomKits()
        {
            foreach (OnlineMapService.ShipRoomSpec room in MapService.ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;

                CreateModelProp("CC0 舱内顶灯 " + room.Name, "Props/Prop_Light_Wide.fbx", room.Center + new Vector3(0f, halfHeight * 0.48f, 0.28f), new Vector3(0.72f, 0.18f, 0.18f), 0f);

                switch (room.Name)
                {
                    case "西码头货柜场":
                        CreateSolidModelProp("CC0 蓝色货柜 " + room.Name, "Props/Prop_Crate4.fbx", room.Center + new Vector3(-1.25f, 0.22f, 0.1f), new Vector3(0.78f, 0.46f, 0.42f), 0f);
                        CreateSolidModelProp("CC0 封存货箱 " + room.Name, "Props/Prop_Chest.fbx", room.Center + new Vector3(0.45f, -0.35f, 0.1f), new Vector3(0.62f, 0.42f, 0.36f), 12f);
                        CreateSolidModelProp("CC0 堆货箱 " + room.Name, "Props/Prop_Crate3.fbx", room.Center + new Vector3(1.28f, 0.42f, 0.1f), new Vector3(0.54f, 0.36f, 0.34f), -8f);
                        break;
                    case "海关查验区":
                        CreateSolidModelProp("CC0 查验终端 " + room.Name, "Props/Prop_Computer.fbx", room.Center + new Vector3(-0.55f, 0.22f, 0.12f), new Vector3(0.62f, 0.42f, 0.38f), 180f);
                        CreateModelProp("CC0 查验门框 " + room.Name, "Platforms/Door_Frame_SquareTall.fbx", room.Center + new Vector3(0.82f, 0.15f, 0.18f), new Vector3(0.42f, 0.96f, 0.46f), 90f, true);
                        break;
                    case "监控室":
                        CreateSolidModelProp("CC0 监控电脑 A", "Props/Prop_Computer.fbx", room.Center + new Vector3(-0.52f, -0.18f, 0.12f), new Vector3(0.58f, 0.38f, 0.36f), 0f);
                        CreateSolidModelProp("CC0 监控电脑 B", "Props/Prop_AccessPoint.fbx", room.Center + new Vector3(0.38f, -0.18f, 0.12f), new Vector3(0.54f, 0.36f, 0.34f), 0f);
                        break;
                    case "茶餐厅":
                        CreateSolidModelProp("CC0 休息舱箱柜 A", "Props/Prop_Chest.fbx", room.Center + new Vector3(-0.86f, 0.35f, 0.12f), new Vector3(0.55f, 0.36f, 0.32f), 90f);
                        CreateSolidModelProp("CC0 休息舱箱柜 B", "Props/Prop_Crate3.fbx", room.Center + new Vector3(0.82f, -0.28f, 0.1f), new Vector3(0.42f, 0.34f, 0.3f), -8f);
                        break;
                    case "夜市主街":
                        for (int i = 0; i < 3; i++)
                        {
                            CreateSolidModelProp("CC0 情报摊设备 " + i, i == 1 ? "Props/Prop_ItemHolder.fbx" : "Props/Prop_Crate4.fbx", room.Center + new Vector3(-1.1f + i * 1.05f, 0.18f, 0.1f), new Vector3(0.56f, 0.38f, 0.34f), i * 9f);
                        }
                        break;
                    case "金融楼":
                        CreateSolidModelProp("CC0 账房保险柜", "Props/Prop_Chest.fbx", room.Center + new Vector3(0.92f, -0.24f, 0.12f), new Vector3(0.62f, 0.48f, 0.42f), -90f);
                        CreateSolidModelProp("CC0 账房电脑", "Props/Prop_Computer.fbx", room.Center + new Vector3(-0.34f, 0.12f, 0.12f), new Vector3(0.62f, 0.38f, 0.36f), 0f);
                        break;
                    case "电房":
                        for (int i = 0; i < 3; i++)
                        {
                            CreateSolidModelProp("CC0 电力设备 " + i, "Props/Prop_AccessPoint.fbx", room.Center + new Vector3(-0.64f + i * 0.5f, 0.22f, 0.12f), new Vector3(0.4f, 0.38f, 0.38f), i % 2 == 0 ? 0f : 180f);
                        }
                        break;
                    case "天台通道":
                        CreateSolidModelProp("CC0 观测平台", "Platforms/Platform_Round1.fbx", room.Center + new Vector3(-0.18f, 0f, 0.08f), new Vector3(0.72f, 0.62f, 0.18f), 0f, true);
                        CreateSolidModelProp("CC0 观测灯", "Props/Prop_Light_Floor.fbx", room.Center + new Vector3(0.78f, 0.28f, 0.1f), new Vector3(0.35f, 0.35f, 0.38f), 0f);
                        break;
                    case "指挥车广场":
                        CreateSolidModelProp("CC0 指挥圆桌箱", "Props/Prop_Chest.fbx", room.Center + new Vector3(0.0f, -0.04f, 0.14f), new Vector3(0.78f, 0.48f, 0.4f), 0f);
                        CreateSolidModelProp("CC0 指挥终端", "Props/Prop_AccessPoint.fbx", room.Center + new Vector3(-1.25f, 0.22f, 0.12f), new Vector3(0.48f, 0.38f, 0.36f), 90f);
                        break;
                    case "证物库":
                        for (int i = 0; i < 3; i++)
                        {
                            CreateSolidModelProp("CC0 证物箱 " + i, i == 2 ? "Props/Prop_Chest.fbx" : "Props/Prop_Crate3.fbx", room.Center + new Vector3(-0.9f + i * 0.6f, 0.25f, 0.1f), new Vector3(0.45f, 0.34f, 0.32f), i * 7f);
                        }
                        break;
                    case "后巷排档":
                        CreateSolidModelProp("CC0 维修箱", "Props/Prop_Crate4.fbx", room.Center + new Vector3(-0.5f, 0.24f, 0.1f), new Vector3(0.56f, 0.38f, 0.32f), 12f);
                        CreateSolidModelProp("CC0 管线夹具", "Props/Prop_PipeHolder.fbx", room.Center + new Vector3(0.82f, -0.28f, 0.1f), new Vector3(0.62f, 0.3f, 0.32f), 90f);
                        break;
                    case "地下诊所":
                        CreateSolidModelProp("CC0 诊疗柜", "Props/Prop_Chest.fbx", room.Center + new Vector3(-1.18f, 0.32f, 0.1f), new Vector3(0.48f, 0.38f, 0.34f), 90f);
                        CreateModelProp("CC0 诊疗灯", "Props/Prop_Light_Floor.fbx", room.Center + new Vector3(0.05f, 0.46f, 0.14f), new Vector3(0.34f, 0.34f, 0.4f), 0f);
                        break;
                }

                CreateModelProp("CC0 舱室立柱 L " + room.Name, "Columns/Column_MetalSupport.fbx", room.Center + new Vector3(-halfWidth + 0.26f, -halfHeight + 0.22f, 0.18f), new Vector3(0.22f, 0.22f, 0.42f), 0f);
                CreateModelProp("CC0 舱室立柱 R " + room.Name, "Columns/Column_MetalSupport.fbx", room.Center + new Vector3(halfWidth - 0.26f, halfHeight - 0.22f, 0.18f), new Vector3(0.22f, 0.22f, 0.42f), 0f);
            }
        }

        private static string RoomPlatformModel(OnlineMapService.ShipRoomSpec room)
        {
            if (room.Label.Contains("账房") || room.Label.Contains("监控") || room.Label.Contains("电力"))
            {
                return "Platforms/Platform_DarkPlates.fbx";
            }

            if (room.Label.Contains("指挥") || room.Label.Contains("观测"))
            {
                return "Platforms/Platform_CenterPlate.fbx";
            }

            return "Platforms/Platform_Simple.fbx";
        }

        private void CreateModelCorridorKits()
        {
            for (int i = 0; i < 7; i++)
            {
                float x = -6.2f + i * 2.05f;
                CreateModelProp("CC0 主廊顶灯 " + i, "Props/Prop_Light_Wide.fbx", new Vector3(x, 0.5f, 0.18f), new Vector3(0.72f, 0.16f, 0.16f), 0f, true);
                CreateModelProp("CC0 下廊地灯 " + i, "Props/Prop_Light_Small.fbx", new Vector3(x + 0.35f, -4.48f, 0.08f), new Vector3(0.25f, 0.25f, 0.18f), 0f);
            }

            for (int i = 0; i < 5; i++)
            {
                float y = -3.05f + i * 1.45f;
                CreateModelProp("CC0 左廊栏杆 " + i, "Props/Prop_Rail_3.fbx", new Vector3(-7.48f, y, 0.12f), new Vector3(0.22f, 0.74f, 0.2f), 90f, true);
                CreateModelProp("CC0 右廊栏杆 " + i, "Props/Prop_Rail_3.fbx", new Vector3(7.68f, y, 0.12f), new Vector3(0.22f, 0.74f, 0.2f), 90f, true);
            }

            CreateSolidModelProp("CC0 主廊封锁箱 A", "Props/Prop_Crate4.fbx", new Vector3(-1.05f, -0.68f, 0.1f), new Vector3(0.7f, 0.26f, 0.32f), 0f);
            CreateSolidModelProp("CC0 主廊封锁箱 B", "Props/Prop_Crate3.fbx", new Vector3(1.05f, 0.68f, 0.1f), new Vector3(0.7f, 0.26f, 0.32f), 180f);
            CreateModelProp("CC0 会议舱圆环平台", "Platforms/Platform_Round1.fbx", new Vector3(0f, -0.35f, -0.02f), new Vector3(1.55f, 1.25f, 0.1f), 0f, true);
            CreateModelProp("CC0 中央门禁电脑", "Props/Prop_Computer.fbx", new Vector3(1.95f, 0.82f, 0.12f), new Vector3(0.42f, 0.36f, 0.34f), -90f);
            CreateModelProp("CC0 墙面监控终端", "Props/Prop_AccessPoint.fbx", new Vector3(2.95f, -3.25f, 0.12f), new Vector3(0.3f, 0.82f, 0.36f), 90f, true);
        }

        private void CreateModelFloorPlates()
        {
            for (int i = 0; i < 8; i++)
            {
                float x = -7f + i * 2f;
                CreateModelProp("CC0 上层地板模块 " + i, i % 2 == 0 ? "Platforms/Platform_3Plates.fbx" : "Platforms/Platform_Squares.fbx", new Vector3(x, 3.65f, 0.02f), new Vector3(0.46f, 0.32f, 0.05f), 0f);
                CreateModelProp("CC0 下层地板模块 " + i, i % 2 == 0 ? "Platforms/Platform_Metal2.fbx" : "Platforms/Platform_Simple2.fbx", new Vector3(x, -3.9f, 0.02f), new Vector3(0.46f, 0.32f, 0.05f), 0f);
            }
        }


    }
}
