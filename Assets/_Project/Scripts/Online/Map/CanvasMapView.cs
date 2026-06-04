using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// M7.4 Canvas 版 2D 地图 UI。
    ///
    /// 数据源：OnlineMapService.ShipRooms() + OnlineMatchController 玩家/任务/尸体状态。
    /// 身份过滤：平民不可见暗线/卧底标记。
    /// 交互：拖拽平移 + 滚轮缩放。
    ///
    /// 小地图（corner minimap）和大地图（overlay）共用此组件，
    /// 通过 IsLargeMap 字段切换布局。
    /// </summary>
    public class CanvasMapView : MonoBehaviour
    {
        [Header("尺寸")]
        [SerializeField] private float mapPixelSize = 512f;      // 地图在 Canvas 上的像素尺寸
        [SerializeField] private float roomMarkerSize = 36f;      // 房间标记大小
        [SerializeField] private float playerMarkerSize = 12f;    // 玩家点大小
        [SerializeField] private float taskMarkerSize = 6f;       // 任务点大小

        [Header("模式")]
        [SerializeField] private bool isLargeMap;                 // true=大地图 overlay, false=小地图 corner
        [SerializeField] private bool visible;

        [Header("颜色")]
        [SerializeField] private Color mapBgColor = new Color(0.02f, 0.03f, 0.04f, 0.92f);
        [SerializeField] private Color roomColor = new Color(0.08f, 0.10f, 0.12f, 0.75f);
        [SerializeField] private Color corridorColor = new Color(0.05f, 0.07f, 0.09f, 0.4f);
        [SerializeField] private Color playerLocalColor = Color.cyan;
        [SerializeField] private Color playerAllyColor = Color.green;
        [SerializeField] private Color playerNeutralColor = Color.white;
        [SerializeField] private Color playerEnemyColor = Color.red;
        [SerializeField] private Color bodyColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color taskColor = new Color(0.3f, 0.7f, 0.9f, 0.7f);
        [SerializeField] private Color sabotagedColor = new Color(1f, 0.4f, 0.1f, 0.8f);

        // 运行时引用
        private OnlineMatchController _controller;
        private OnlineMapService _mapService;
        private Canvas _canvas;
        private GameObject _rootPanel;
        private RectTransform _mapArea;
        private RectTransform _mapContent;     // 可平移的容器
        private readonly List<GameObject> _dynamicMarkers = new List<GameObject>();

        // 拖拽/缩放状态
        private Vector2 _panOffset;
        private float _zoomLevel = 1f;
        private Vector2 _dragStartPos;
        private bool _isDragging;

        // ── 世界坐标到 Canvas 坐标的映射 ──
        // 世界地图大致范围决定了缩放比
        private const float WorldMapHalfWidth = 14f;   // ±14 单位
        private const float WorldMapHalfHeight = 8f;

        public bool IsVisible
        {
            get => visible;
            set
            {
                visible = value;
                if (_rootPanel != null) _rootPanel.SetActive(value);
                if (value) RefreshAll();
            }
        }

        public void Initialize(OnlineMatchController controller, OnlineMapService mapService)
        {
            _controller = controller;
            _mapService = mapService;
            BuildUI();
        }

        // ══════════════════════════════════════════════════════
        // UI 构建
        // ══════════════════════════════════════════════════════

        private void BuildUI()
        {
            // Canvas
            _canvas = GetOrCreateCanvas();
            _canvas.sortingOrder = isLargeMap ? 500 : 100;

            // 根面板
            _rootPanel = CreatePanel("MapRoot", _canvas.transform, mapBgColor);
            _rootPanel.GetComponent<Image>().raycastTarget = true;

            if (isLargeMap)
            {
                // 大地图：居中 600x600
                var rt = _rootPanel.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(mapPixelSize + 48f, mapPixelSize + 48f);
                rt.anchoredPosition = Vector2.zero;

                // 关闭按钮
                CreateCloseButton();
            }
            else
            {
                // 小地图：右下角 180x180
                var rt = _rootPanel.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.sizeDelta = new Vector2(180f, 180f);
                rt.anchoredPosition = new Vector2(-16f, 16f);
            }

            // 地图区域（裁剪）
            var mapAreaGO = CreatePanel("MapArea", _rootPanel.transform, Color.clear);
            _mapArea = mapAreaGO.GetComponent<RectTransform>();
            _mapArea.anchorMin = _mapArea.anchorMax = new Vector2(0.5f, 0.5f);
            _mapArea.sizeDelta = new Vector2(mapPixelSize, mapPixelSize);
            _mapArea.anchoredPosition = Vector2.zero;

            // 为裁剪添加 Mask
            var mask = mapAreaGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            mapAreaGO.AddComponent<Image>().color = Color.clear;
            mapAreaGO.GetComponent<Image>().raycastTarget = true;

            // 地图内容（可平移）
            var contentGO = new GameObject("MapContent", typeof(RectTransform));
            contentGO.transform.SetParent(_mapArea, false);
            _mapContent = contentGO.GetComponent<RectTransform>();
            _mapContent.anchorMin = _mapContent.anchorMax = new Vector2(0.5f, 0.5f);
            _mapContent.sizeDelta = new Vector2(mapPixelSize, mapPixelSize);

            // 绘制房间和走廊（静态）
            DrawStaticMap();

            // 固定标题
            var title = CreateText("MapTitle", _rootPanel.transform,
                isLargeMap ? "港区地图" : "", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = isLargeMap ? new Vector2(0f, -8f) : new Vector2(0f, -2f);
            titleRT.sizeDelta = new Vector2(200f, 24f);

            // 拖拽事件
            AddDragHandler(mapAreaGO);

            _rootPanel.SetActive(visible);
        }

        private void DrawStaticMap()
        {
            if (_mapService == null) return;

            var rooms = _mapService.ShipRooms();
            float mapHalfW = mapPixelSize * 0.5f;

            foreach (var room in rooms)
            {
                // 设计坐标 → Canvas 像素坐标
                Vector2 pixelPos = WorldToCanvas(room.Center);
                Vector2 pixelSize = WorldSizeToCanvas(room.Size);
                pixelSize = new Vector2(
                    Mathf.Max(pixelSize.x, roomMarkerSize),
                    Mathf.Max(pixelSize.y, roomMarkerSize));

                // 房间矩形
                var roomGO = CreatePanel("Room_" + room.Label, _mapContent, room.Floor);
                var rt = roomGO.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = pixelSize;
                rt.anchoredPosition = pixelPos;

                // 房间标签（大地图时显示）
                if (isLargeMap)
                {
                    var label = CreateText("Label", roomGO.transform,
                        room.Label, 10, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
                    var lrt = label.GetComponent<RectTransform>();
                    lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                    lrt.sizeDelta = new Vector2(pixelSize.x * 0.8f, 16f);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 动态标记（玩家、任务、尸体）
        // ══════════════════════════════════════════════════════

        private void RefreshDynamicMarkers()
        {
            ClearDynamicMarkers();

            if (_controller == null) return;

            // 绘制任务点
            DrawTaskMarkers();

            // 绘制玩家
            DrawPlayerMarkers();

            // 绘制尸体
            DrawBodyMarkers();
        }

        private void DrawTaskMarkers()
        {
            var tasks = _controller.Tasks;
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                Vector2 pixelPos = WorldToCanvas(task.Position);
                bool sabotaged = task.Sabotaged;
                bool done = task.Completed;

                Color color = sabotaged ? sabotagedColor :
                    done ? new Color(0.3f, 0.6f, 0.3f, 0.6f) : taskColor;

                float size = sabotaged ? taskMarkerSize * 1.5f : taskMarkerSize;
                CreateMarker($"Task_{i}", _mapContent, pixelPos, size, color);
            }
        }

        private void DrawPlayerMarkers()
        {
            if (_controller == null) return;

            var players = _controller.Players;
            ulong localId = _controller.LocalClientIdValue;

            foreach (var kv in players)
            {
                var player = kv.Value;
                if (!player.Alive) continue;

                Vector2 pixelPos = WorldToCanvas(player.Position);
                Color color = GetPlayerMarkerColor(player, localId);

                CreateMarker($"Player_{player.ClientId}", _mapContent,
                    pixelPos, playerMarkerSize, color, player.ClientId == localId);
            }
        }

        private Color GetPlayerMarkerColor(OnlinePlayerState player, ulong localId)
        {
            if (player.ClientId == localId)
                return playerLocalColor;

            bool localIsGang = _controller.IsGangFaction(localId);
            bool playerIsGang = _controller.IsGangFaction(player.ClientId);

            if (localIsGang == playerIsGang)
                return playerAllyColor;

            return playerNeutralColor;
        }

        private void DrawBodyMarkers()
        {
            var bodies = _controller.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                Vector2 pixelPos = WorldToCanvas(body.Position);
                CreateMarker($"Body_{i}", _mapContent, pixelPos,
                    playerMarkerSize * 1.3f, bodyColor);
            }
        }

        // ══════════════════════════════════════════════════════
        // 坐标转换
        // ══════════════════════════════════════════════════════

        private Vector2 WorldToCanvas(Vector3 worldPos)
        {
            // 设计坐标已经 mapService 缩放过，直接映射到像素
            float px = (worldPos.x / WorldMapHalfWidth) * (mapPixelSize * 0.5f);
            float py = (worldPos.y / WorldMapHalfHeight) * (mapPixelSize * 0.5f);
            return new Vector2(px, py) * _zoomLevel + _panOffset;
        }

        private Vector2 WorldSizeToCanvas(Vector3 worldSize)
        {
            float w = (worldSize.x / (WorldMapHalfWidth * 2f)) * mapPixelSize * _zoomLevel;
            float h = (worldSize.y / (WorldMapHalfHeight * 2f)) * mapPixelSize * _zoomLevel;
            return new Vector2(Mathf.Max(w, 4f), Mathf.Max(h, 4f));
        }

        // ══════════════════════════════════════════════════════
        // 交互
        // ══════════════════════════════════════════════════════

        private void AddDragHandler(GameObject target)
        {
            // 使用 EventTrigger 或简单轮询 Input
        }

        private void Update()
        {
            if (!visible || _controller == null) return;

            // 缩放
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _zoomLevel = Mathf.Clamp(_zoomLevel + scroll * 0.3f, 0.5f, 3f);
                RefreshAll();
            }

            // 拖拽
            if (Input.GetMouseButtonDown(0))
                _dragStartPos = Input.mousePosition;
            if (Input.GetMouseButton(0) && Vector2.Distance(_dragStartPos, (Vector2)Input.mousePosition) > 5f)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _dragStartPos;
                _panOffset += delta;
                _dragStartPos = Input.mousePosition;
                RefreshAll();
            }

            // 动态元素每 0.5 秒刷新
            if (Time.frameCount % 30 == 0)
                RefreshDynamicMarkers();
        }

        private void RefreshAll()
        {
            // 重建静态地图（带缩放）
            for (int i = _mapContent.childCount - 1; i >= 0; i--)
            {
                var child = _mapContent.GetChild(i);
                if (!_dynamicMarkers.Contains(child.gameObject))
                    Object.Destroy(child.gameObject);
            }
            _dynamicMarkers.Clear();
            DrawStaticMap();
            RefreshDynamicMarkers();
        }

        // ══════════════════════════════════════════════════════
        // 工厂方法
        // ══════════════════════════════════════════════════════

        private void CreateMarker(string name, Transform parent, Vector2 position, float size, Color color, bool pulse = false)
        {
            var go = CreatePanel(name, parent, color);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = position;
            _dynamicMarkers.Add(go);
        }

        private void ClearDynamicMarkers()
        {
            foreach (var marker in _dynamicMarkers)
            {
                if (marker != null) Object.Destroy(marker);
            }
            _dynamicMarkers.Clear();
        }

        private void CreateCloseButton()
        {
            var btnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(_rootPanel.transform, false);
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(32f, 32f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            btnGO.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            btnGO.GetComponent<Button>().onClick.AddListener(() => IsVisible = false);

            var label = CreateText("X", btnGO.transform, "X", 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = Vector2.zero;
            lrt.sizeDelta = new Vector2(32f, 32f);
        }

        private Canvas GetOrCreateCanvas()
        {
            var existing = FindAnyObjectByType<Canvas>();
            if (existing != null) return existing;

            var go = new GameObject("MapCanvas");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color bg)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = bg;
            obj.GetComponent<Image>().raycastTarget = false;
            return obj;
        }

        private static Text CreateText(string name, Transform parent,
            string content, int fontSize, Color color, FontStyle style, TextAnchor align)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text t = obj.GetComponent<Text>();
            t.text = content;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
    }
}
