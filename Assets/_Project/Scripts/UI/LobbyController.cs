using System.Collections.Generic;
using GanglandUndercover.Audio;
using GanglandUndercover.Gameplay;
using GanglandUndercover.Online;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 联机大厅控制器 — uGUI 正式化版本。
    /// Canvas + InputField + Button，房间码输入、创建/加入、玩家列表、开始按钮。
    /// 对标 Among Us 风格：深色背景、居中面板、橙色强调色。
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        private static readonly Color BgDark       = new Color(0.015f, 0.022f, 0.025f, 1f);
        private static readonly Color PanelBg      = new Color(0.042f, 0.055f, 0.058f, 0.96f);
        private static readonly Color AccentOrange = new Color(0.86f, 0.48f, 0.13f, 1f);
        private static readonly Color AccentBlue  = new Color(0.08f, 0.62f, 0.82f, 1f);
        private static readonly Color MutedColor   = new Color(0.52f, 0.55f, 0.54f, 1f);
        private static readonly Color TextColor    = new Color(0.92f, 0.94f, 0.93f, 1f);
        private static readonly Color InputBg     = new Color(0.02f, 0.026f, 0.028f, 0.96f);
        private static readonly Color ButtonNormal = new Color(0.17f, 0.19f, 0.18f, 1f);

        private PrototypeBootstrap _bootstrap;
        private OnlineSyncManager _onlineManager;
        private Canvas _canvas;
        private GameObject _rootPanel;
        private InputField _roomCodeInput;
        private Transform _playerListRoot;
        private Text _statusText;
        private Button _startButton;
        private bool _visible;

        public bool IsVisible => _visible;

        public void Initialize(PrototypeBootstrap bootstrap, OnlineSyncManager onlineManager)
        {
            _bootstrap = bootstrap;
            _onlineManager = onlineManager;
            BuildUI();
        }

        public void Show()
        {
            _visible = true;
            if (_rootPanel != null) _rootPanel.SetActive(true);
            RefreshPlayerList();
        }

        public void Hide()
        {
            _visible = false;
            if (_rootPanel != null) _rootPanel.SetActive(false);
        }

        private void BuildUI()
        {
            _canvas = GetOrCreateCanvas();

            _rootPanel = new GameObject("LobbyRoot", typeof(RectTransform), typeof(Image));
            _rootPanel.transform.SetParent(_canvas.transform, false);
            _rootPanel.GetComponent<Image>().color = BgDark;
            _rootPanel.GetComponent<Image>().raycastTarget = true;
            StretchStick(_rootPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            const float refW = 1920f;

            SetPos(CreateText("Title", _rootPanel.transform,
                "联  机  大  厅", 38, AccentOrange, FontStyle.Bold, TextAnchor.MiddleCenter).gameObject,
                0f, 420f, refW * 0.8f, 56f);

            GameObject lobbyPanel = CreatePanel("LobbyPanel", _rootPanel.transform, PanelBg);
            SetPos(lobbyPanel, 0f, 80f, 880f, 480f);

            // 房间码标签
            CenterTopLabel(CreateText("RoomCodeLabel", lobbyPanel.transform,
                "房间码", 16, MutedColor, FontStyle.Normal, TextAnchor.MiddleLeft).GetComponent<RectTransform>(),
                -30f, 26f);

            _roomCodeInput = CreateInputField("RoomCodeInput", lobbyPanel.transform,
                "输入 4~6 位房间码", 18, TextColor, InputBg);
            CenterTopLabel(GetRect(_roomCodeInput.gameObject), -68f, 42f);
            _roomCodeInput.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 42f);

            // 创建 / 加入按钮
            var createBtn = MakeButton("CreateRoomButton", lobbyPanel.transform,
                "创  建  房  间", 200f, 48f, AccentOrange, Color.white, 18);
            CenterTopLabel(GetRect(createBtn), -130f, 48f);
            GetRect(createBtn).sizeDelta = new Vector2(200f, 48f);
            GetRect(createBtn).anchoredPosition = new Vector2(-110f, -130f);
            createBtn.GetComponent<Button>().onClick.AddListener(OnCreateRoom);

            var joinBtn = MakeButton("JoinRoomButton", lobbyPanel.transform,
                "加  入  房  间", 200f, 48f, AccentBlue, Color.white, 18);
            CenterTopLabel(GetRect(joinBtn), -130f, 48f);
            GetRect(joinBtn).sizeDelta = new Vector2(200f, 48f);
            GetRect(joinBtn).anchoredPosition = new Vector2(110f, -130f);
            joinBtn.GetComponent<Button>().onClick.AddListener(OnJoinRoom);

            _statusText = CreateText("StatusText", lobbyPanel.transform,
                "等待操作...", 14, MutedColor, FontStyle.Normal, TextAnchor.MiddleCenter).GetComponent<Text>();
            CenterTopLabel(GetRect(_statusText.gameObject), -196f, 26f);

            CenterTopLabel(CreateText("PlayerListHeader", lobbyPanel.transform,
                "—  玩  家  列  表  —", 15, MutedColor, FontStyle.Normal, TextAnchor.MiddleCenter).GetComponent<RectTransform>(),
                -232f, 26f);

            GameObject playerScroll = CreatePanel("PlayerListScroll", lobbyPanel.transform, InputBg);
            CenterTopLabel(GetRect(playerScroll), -340f, 140f);
            GetRect(playerScroll).sizeDelta = new Vector2(600f, 140f);
            _playerListRoot = playerScroll.transform;

            _startButton = MakeButton("StartGameButton", lobbyPanel.transform,
                "开  始  游  戏", 260f, 52f, AccentOrange, Color.white, 20).GetComponent<Button>();
            CenterTopLabel(GetRect(_startButton.gameObject), -420f, 52f);
            GetRect(_startButton.gameObject).sizeDelta = new Vector2(260f, 52f);
            _startButton.onClick.AddListener(OnStartOnlineGame);
            _startButton.interactable = false;

            var backBtn = MakeButton("BackButton", lobbyPanel.transform,
                "返  回  主  菜  单", 200f, 44f, ButtonNormal, TextColor, 16);
            CenterTopLabel(GetRect(backBtn), -480f, 44f);
            GetRect(backBtn).sizeDelta = new Vector2(200f, 44f);
            backBtn.GetComponent<Button>().onClick.AddListener(OnBackToMenu);
        }

        public void RefreshPlayerList()
        {
            if (_playerListRoot == null) return;
            for (int i = _playerListRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(_playerListRoot.GetChild(i).gameObject);
            int playerCount = 1;
            for (int i = 0; i < 4; i++)
            {
                bool present = i < playerCount;
                Color c = present ? TextColor : new Color(0.3f, 0.32f, 0.31f, 1f);
                string label = present ? "玩家 " + (i + 1) : "等待加入...";
                GameObject entry = CreateText("PlayerEntry_" + i, _playerListRoot,
                    label, 14, c, FontStyle.Normal, TextAnchor.MiddleLeft).gameObject;
                var r = GetRect(entry);
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(12f, -i * 32f - 6f);
                r.sizeDelta = new Vector2(560f, 28f);
            }
        }

        private void OnCreateRoom()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            if (_onlineManager == null) return;
            if (_statusText != null) _statusText.text = "房间已创建";
            RefreshPlayerList();
        }

        private void OnJoinRoom()
        {
            if (_onlineManager == null || _roomCodeInput == null) return;
            string code = _roomCodeInput.text;
            if (string.IsNullOrWhiteSpace(code))
            {
                if (_statusText != null) _statusText.text = "请输入房间码";
                return;
            }
            if (_statusText != null) _statusText.text = "正在加入房间: " + code;
            RefreshPlayerList();
        }

        private void OnStartOnlineGame()
        {
            Hide();
            _bootstrap?.StartOnlineGame();
        }

        private void OnBackToMenu()
        {
            Hide();
            _bootstrap?.ReturnToMainMenu();
        }

        // ─── Canvas fallback ──────────────────────────────────
        private Canvas GetOrCreateCanvas()
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null && ui.MainCanvas != null) return ui.MainCanvas;
            Canvas existing = FindAnyObjectByType<Canvas>();
            if (existing != null) return existing;
            GameObject go = new GameObject("LobbyCanvas_Fallback");
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        // ─── Factory ──────────────────────────────────────────
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
            t.font = LoadFont();
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static InputField CreateInputField(string name, Transform parent,
            string placeholder, int fontSize, Color textColor, Color bgColor)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = bgColor;
            InputField input = obj.GetComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 12;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(obj.transform, false);
            Text text = textObj.GetComponent<Text>();
            text.font = LoadFont(); text.fontSize = fontSize; text.color = textColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            StretchStick(GetRect(textObj), Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phObj.transform.SetParent(obj.transform, false);
            Text ph = phObj.GetComponent<Text>();
            ph.font = LoadFont(); ph.fontSize = fontSize;
            ph.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
            ph.text = placeholder; ph.alignment = TextAnchor.MiddleLeft;
            ph.horizontalOverflow = HorizontalWrapMode.Wrap;
            ph.verticalOverflow = VerticalWrapMode.Overflow;
            StretchStick(GetRect(phObj), Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            input.textComponent = text;
            input.placeholder = ph;
            return input;
        }

        private static GameObject MakeButton(string name, Transform parent,
            string label, float w, float h, Color bg, Color txtColor, int fontSize)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = bg;
            Button btn = obj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = bg;
            cb.highlightedColor = ScaleColor(bg, 1.35f);
            cb.pressedColor = ScaleColor(bg, 0.65f);
            cb.disabledColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            btn.colors = cb;
            var t = CreateText("Label", obj.transform, label, fontSize, txtColor,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchStick(GetRect(t.gameObject), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return obj;
        }

        private static void CenterTopLabel(RectTransform rt, float y, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        }

        // ─── Layout helpers ───────────────────────────────────
        private static void StretchStick(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private static void SetPos(GameObject obj, float ax, float ay, float w, float h)
        {
            RectTransform rt = GetRect(obj);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static RectTransform GetRect(GameObject obj)
        {
            var rt = obj.GetComponent<RectTransform>();
            return rt != null ? rt : obj.AddComponent<RectTransform>();
        }

        private static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Color ScaleColor(Color c, float f)
        {
            return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);
        }
    }
}