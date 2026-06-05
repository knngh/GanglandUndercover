using System.Collections.Generic;
using GanglandUndercover.Audio;
using GanglandUndercover.Gameplay;
using GanglandUndercover.Online;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 联机大厅控制器 — M7.1 Relay 连线版本。
    /// 连接按钮到 OnlineMatchController 的 Relay API，实现完整房间创建/加入链路。
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        private static readonly Color BgDark       = new Color(0.015f, 0.022f, 0.025f, 1f);
        private static readonly Color PanelBg      = new Color(0.042f, 0.055f, 0.058f, 0.96f);
        private static readonly Color AccentOrange = new Color(0.86f, 0.48f, 0.13f, 1f);
        private static readonly Color AccentBlue  = new Color(0.08f, 0.62f, 0.82f, 1f);
        private static readonly Color AccentGreen  = new Color(0.18f, 0.78f, 0.35f, 1f);
        private static readonly Color AccentRed    = new Color(0.88f, 0.22f, 0.18f, 1f);
        private static readonly Color MutedColor   = new Color(0.52f, 0.55f, 0.54f, 1f);
        private static readonly Color TextColor    = new Color(0.92f, 0.94f, 0.93f, 1f);
        private static readonly Color InputBg     = new Color(0.02f, 0.026f, 0.028f, 0.96f);
        private static readonly Color ButtonNormal = new Color(0.17f, 0.19f, 0.18f, 1f);

        private PrototypeBootstrap _bootstrap;
        private OnlineSyncManager _onlineManager;
        private OnlineMatchController _matchController;
        private Canvas _canvas;
        private GameObject _rootPanel;
        private InputField _roomCodeInput;
        private Text _roomCodeDisplay;       // M7.1: 显示生成后的房间码
        private Button _copyCodeButton;       // F3: 一键复制房间码
        private Button _readyButton;          // F3: 准备/取消准备
        private bool _isReady;                // F3: 本地玩家准备状态
        private Button _leaveButton;          // F3: 离开房间确认
        private GameObject _leaveConfirmPanel; // F3: 离开确认弹窗
        private Transform _playerListRoot;
        private Text _statusText;
        private Button _startButton;
        private Button _createButton;
        private Button _joinButton;
        private bool _visible;
        private float _refreshTimer;

        public bool IsVisible => _visible;

        /// <summary>
        /// 初始化大厅，接收 OnlineMatchController 引用用于 Relay 操作。
        /// </summary>
        public void Initialize(PrototypeBootstrap bootstrap, OnlineSyncManager onlineManager,
            OnlineMatchController matchController)
        {
            _bootstrap = bootstrap;
            _onlineManager = onlineManager;
            _matchController = matchController;
            BuildUI();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!_visible) return;
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 1.5f)
            {
                _refreshTimer = 0f;
                RefreshPlayerList();
                UpdateStartButtonState();
            }
        }

        public void Show()
        {
            _visible = true;
            if (_rootPanel != null) _rootPanel.SetActive(true);
            RefreshPlayerList();
            UpdateStartButtonState();
            UpdateRoomCodeDisplay();
        }

        public void Hide()
        {
            _visible = false;
            if (_rootPanel != null) _rootPanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════
        // 事件订阅
        // ══════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            if (_matchController == null) return;
            _matchController.OnRelayStatusChanged += OnRelayStatusChanged;
            _matchController.OnRelayRoomCodeReady += OnRoomCodeReady;
            _matchController.OnRelayConnectionChanged += OnConnectionChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_matchController == null) return;
            _matchController.OnRelayStatusChanged -= OnRelayStatusChanged;
            _matchController.OnRelayRoomCodeReady -= OnRoomCodeReady;
            _matchController.OnRelayConnectionChanged -= OnConnectionChanged;
        }

        private void OnRelayStatusChanged(string msg)
        {
            if (_statusText != null && _visible)
                _statusText.text = msg;
        }

        private void OnRoomCodeReady(string code)
        {
            if (_roomCodeDisplay != null)
            {
                _roomCodeDisplay.text = "房间码: " + code;
                _roomCodeDisplay.color = AccentGreen;
            }
        }

        private void OnConnectionChanged(bool connected)
        {
            RefreshPlayerList();
            UpdateStartButtonState();
        }

        // ══════════════════════════════════════════════════════
        // UI 构建
        // ══════════════════════════════════════════════════════

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
            SetPos(lobbyPanel, 0f, 60f, 880f, 520f);

            // 房间码标签
            CenterTopLabel(CreateText("RoomCodeLabel", lobbyPanel.transform,
                "房间码", 16, MutedColor, FontStyle.Normal, TextAnchor.MiddleLeft).GetComponent<RectTransform>(),
                -30f, 26f);

            _roomCodeInput = CreateInputField("RoomCodeInput", lobbyPanel.transform,
                "输入 4~6 位房间码", 18, TextColor, InputBg);
            CenterTopLabel(GetRect(_roomCodeInput.gameObject), -68f, 42f);
            _roomCodeInput.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 42f);
            _roomCodeInput.characterLimit = 6;
            _roomCodeInput.onValidateInput += ValidateRoomCode;

            // M7.1: 房间码显示区（创建成功后显示）
            _roomCodeDisplay = CreateText("RoomCodeDisplay", lobbyPanel.transform,
                string.Empty, 24, AccentGreen, FontStyle.Bold, TextAnchor.MiddleCenter).GetComponent<Text>();
            CenterTopLabel(GetRect(_roomCodeDisplay.gameObject), -105f, 30f);

            // F3: 复制房间码按钮（房间码旁边）
            var copyBtn = MakeButton("CopyCodeButton", lobbyPanel.transform,
                "📋", 40f, 30f, InputBg, MutedColor, 14);
            GetRect(copyBtn).anchorMin = GetRect(copyBtn).anchorMax = new Vector2(0.5f, 1f);
            GetRect(copyBtn).pivot = new Vector2(0.5f, 1f);
            GetRect(copyBtn).anchoredPosition = new Vector2(200f, -105f);
            GetRect(copyBtn).sizeDelta = new Vector2(40f, 30f);
            _copyCodeButton = copyBtn.GetComponent<Button>();
            _copyCodeButton.onClick.AddListener(OnCopyRoomCode);

            // 创建 / 加入按钮
            var createBtn = MakeButton("CreateRoomButton", lobbyPanel.transform,
                "创  建  房  间", 200f, 48f, AccentOrange, Color.white, 18);
            CenterTopLabel(GetRect(createBtn), -148f, 48f);
            GetRect(createBtn).sizeDelta = new Vector2(200f, 48f);
            GetRect(createBtn).anchoredPosition = new Vector2(-110f, -148f);
            _createButton = createBtn.GetComponent<Button>();
            _createButton.onClick.AddListener(OnCreateRoom);

            var joinBtn = MakeButton("JoinRoomButton", lobbyPanel.transform,
                "加  入  房  间", 200f, 48f, AccentBlue, Color.white, 18);
            CenterTopLabel(GetRect(joinBtn), -148f, 48f);
            GetRect(joinBtn).sizeDelta = new Vector2(200f, 48f);
            GetRect(joinBtn).anchoredPosition = new Vector2(110f, -148f);
            _joinButton = joinBtn.GetComponent<Button>();
            _joinButton.onClick.AddListener(OnJoinRoom);

            _statusText = CreateText("StatusText", lobbyPanel.transform,
                "输入房间码加入，或创建新房间", 14, MutedColor, FontStyle.Normal, TextAnchor.MiddleCenter).GetComponent<Text>();
            CenterTopLabel(GetRect(_statusText.gameObject), -210f, 30f);

            CenterTopLabel(CreateText("PlayerListHeader", lobbyPanel.transform,
                "—  玩  家  列  表  —", 15, MutedColor, FontStyle.Normal, TextAnchor.MiddleCenter).GetComponent<RectTransform>(),
                -245f, 26f);

            GameObject playerScroll = CreatePanel("PlayerListScroll", lobbyPanel.transform, InputBg);
            CenterTopLabel(GetRect(playerScroll), -350f, 140f);
            GetRect(playerScroll).sizeDelta = new Vector2(600f, 140f);
            _playerListRoot = playerScroll.transform;

            // Ready / 开始游戏按钮行
            _startButton = MakeButton("StartGameButton", lobbyPanel.transform,
                "开  始  游  戏", 220f, 50f, AccentOrange, Color.white, 20).GetComponent<Button>();
            CenterTopLabel(GetRect(_startButton.gameObject), -435f, 50f);
            GetRect(_startButton.gameObject).sizeDelta = new Vector2(220f, 50f);
            _startButton.onClick.AddListener(OnStartOnlineGame);
            _startButton.interactable = false;

            // F3: Ready/取消准备按钮
            _readyButton = MakeButton("ReadyButton", lobbyPanel.transform,
                "准  备", 160f, 44f, AccentGreen, Color.white, 16).GetComponent<Button>();
            CenterTopLabel(GetRect(_readyButton.gameObject), -435f, 44f);
            GetRect(_readyButton.gameObject).sizeDelta = new Vector2(160f, 44f);
            GetRect(_readyButton.gameObject).anchoredPosition = new Vector2(-200f, -435f);
            _readyButton.onClick.AddListener(OnToggleReady);

            // F3: 离开房间按钮
            _leaveButton = MakeButton("LeaveRoomButton", lobbyPanel.transform,
                "离  开  房  间", 160f, 44f, AccentRed, Color.white, 16).GetComponent<Button>();
            CenterTopLabel(GetRect(_leaveButton.gameObject), -435f, 44f);
            GetRect(_leaveButton.gameObject).sizeDelta = new Vector2(160f, 44f);
            GetRect(_leaveButton.gameObject).anchoredPosition = new Vector2(200f, -435f);
            _leaveButton.onClick.AddListener(OnLeaveRoom);

            // F3: 离开确认弹窗（初始隐藏）
            _leaveConfirmPanel = CreatePanel("LeaveConfirmPanel", lobbyPanel.transform, PanelBg);
            SetPos(_leaveConfirmPanel, 0f, 0f, 360f, 160f);
            _leaveConfirmPanel.SetActive(false);

            CenterTopLabel(CreateText("LeaveConfirmText", _leaveConfirmPanel.transform,
                "确定离开？对局进度将丢失。", 18, TextColor, FontStyle.Normal, TextAnchor.MiddleCenter)
                .GetComponent<RectTransform>(), -40f, 30f);

            var confirmBtn = MakeButton("ConfirmLeaveBtn", _leaveConfirmPanel.transform,
                "确  定  离  开", 140f, 40f, AccentRed, Color.white, 16);
            CenterTopLabel(GetRect(confirmBtn), -90f, 40f);
            GetRect(confirmBtn).sizeDelta = new Vector2(140f, 40f);
            GetRect(confirmBtn).anchoredPosition = new Vector2(-80f, -90f);
            confirmBtn.GetComponent<Button>().onClick.AddListener(OnConfirmLeave);

            var cancelBtn = MakeButton("CancelLeaveBtn", _leaveConfirmPanel.transform,
                "取  消", 140f, 40f, ButtonNormal, TextColor, 16);
            CenterTopLabel(GetRect(cancelBtn), -90f, 40f);
            GetRect(cancelBtn).sizeDelta = new Vector2(140f, 40f);
            GetRect(cancelBtn).anchoredPosition = new Vector2(80f, -90f);
            cancelBtn.GetComponent<Button>().onClick.AddListener(OnCancelLeave);

            // 返回按钮
            var backBtn = MakeButton("BackButton", lobbyPanel.transform,
                "返  回  主  菜  单", 200f, 44f, ButtonNormal, TextColor, 16);
            CenterTopLabel(GetRect(backBtn), -500f, 44f);
            GetRect(backBtn).sizeDelta = new Vector2(200f, 44f);
            backBtn.GetComponent<Button>().onClick.AddListener(OnBackToMenu);
        }

        // ══════════════════════════════════════════════════════
        // 玩家列表 & 按钮状态
        // ══════════════════════════════════════════════════════

        private void RefreshPlayerList()
        {
            if (_playerListRoot == null) return;

            for (int i = _playerListRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(_playerListRoot.GetChild(i).gameObject);

            // M7.1: 从 OnlineMatchController 获取真实玩家数据
            int actualCount = GetConnectedPlayerCount();
            int maxSlots = 8;

            for (int i = 0; i < maxSlots; i++)
            {
                bool present = i < actualCount;
                Color c = present ? TextColor : new Color(0.3f, 0.32f, 0.31f, 1f);
                string label;
                if (present && i == 0)
                    label = "🏠 " + (_matchController != null ? _matchController.LocalPlayerName : "房主");
                else if (present)
                    label = "👤 玩家 " + (i + 1);
                else
                    label = "等待加入...";

                GameObject entry = CreateText("PlayerEntry_" + i, _playerListRoot,
                    label, 14, c, FontStyle.Normal, TextAnchor.MiddleLeft).gameObject;
                var r = GetRect(entry);
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(12f, -i * 32f - 6f);
                r.sizeDelta = new Vector2(560f, 28f);
            }
        }

        private void UpdateStartButtonState()
        {
            if (_startButton == null || _matchController == null) return;
            _startButton.interactable = _matchController.IsHost && GetConnectedPlayerCount() >= 1;
        }

        private void UpdateRoomCodeDisplay()
        {
            if (_roomCodeDisplay == null || _matchController == null) return;
            string code = _matchController.RelayJoinCode;
            if (!string.IsNullOrEmpty(code))
            {
                _roomCodeDisplay.text = "房间码: " + code;
                _roomCodeDisplay.color = AccentGreen;
            }
        }

        private int GetConnectedPlayerCount()
        {
            if (_matchController == null) return 1;
            // OnlineMatchController 的 players 字典记录所有连接玩家
            // localPreviewMode 下自身算一个
            if (_matchController.IsLocalPreview) return 1;
            return _matchController.PlayerCount;
        }

        // ══════════════════════════════════════════════════════
        // 按钮逻辑 — M7.1 Relay 连线
        // ══════════════════════════════════════════════════════

        private void OnCreateRoom()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            if (_matchController == null)
            {
                SetStatus("控制器未就绪，请重启。", AccentRed);
                return;
            }

            SetStatus("正在通过 Relay 创建房间...", AccentOrange);
            _matchController.RequestRelayHost();
        }

        private void OnJoinRoom()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            if (_matchController == null || _roomCodeInput == null)
            {
                SetStatus("控制器未就绪，请重启。", AccentRed);
                return;
            }

            string code = _roomCodeInput.text;
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("请输入 4~6 位房间码", AccentRed);
                return;
            }

            if (code.Length < 4)
            {
                SetStatus("房间码至少 4 位", AccentRed);
                return;
            }

            SetStatus("正在加入房间: " + code, AccentBlue);
            _matchController.RequestRelayClient(code);
        }

        private void OnStartOnlineGame()
        {
            if (_matchController == null || !_matchController.IsHost) return;
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            Hide();
            _bootstrap?.StartOnlineGame();
        }

        private void OnBackToMenu()
        {
            Hide();
            _bootstrap?.ReturnToMainMenu();
        }

        // ══════════════════════════════════════════════════════
        // F3: 新增功能 — 复制房间码 / 准备 / 离开确认 / 踢人
        // ══════════════════════════════════════════════════════

        /// <summary>一键复制房间码到剪贴板</summary>
        private void OnCopyRoomCode()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            if (_matchController == null) return;
            string code = _matchController.RelayJoinCode;
            if (!string.IsNullOrEmpty(code))
            {
                GUIUtility.systemCopyBuffer = code;
                SetStatus("房间码已复制: " + code, AccentGreen);
            }
            else
            {
                SetStatus("暂无房间码，请先创建房间", AccentRed);
            }
        }

        /// <summary>切换准备/取消准备状态</summary>
        private void OnToggleReady()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            _isReady = !_isReady;

            if (_readyButton != null)
            {
                Text label = _readyButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = _isReady ? "取消准备" : "准  备";

                ColorBlock cb = _readyButton.colors;
                cb.normalColor = _isReady ? AccentGreen : ButtonNormal;
                _readyButton.colors = cb;
            }

            // 通知服务器准备状态变更
            _matchController?.SetReady(_isReady);
            SetStatus(_isReady ? "已准备，等待房主开始..." : "已取消准备", AccentBlue);
        }

        /// <summary>点击离开房间 — 弹出确认弹窗</summary>
        private void OnLeaveRoom()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            if (_leaveConfirmPanel != null)
            {
                _leaveConfirmPanel.SetActive(true);
            }
        }

        /// <summary>确认离开</summary>
        private void OnConfirmLeave()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            _leaveConfirmPanel?.SetActive(false);
            _isReady = false;
            _matchController?.LeaveRoom();
            Hide();
            _bootstrap?.ReturnToMainMenu();
        }

        /// <summary>取消离开</summary>
        private void OnCancelLeave()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.ButtonHover);
            if (_leaveConfirmPanel != null)
                _leaveConfirmPanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════

        private void SetStatus(string msg, Color color)
        {
            if (_statusText != null)
            {
                _statusText.text = msg;
                _statusText.color = color;
            }
        }

        /// <summary>房间码只允许大写字母和数字</summary>
        private char ValidateRoomCode(string text, int charIndex, char addedChar)
        {
            if (char.IsLetterOrDigit(addedChar))
                return char.ToUpperInvariant(addedChar);
            return '\0';
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
