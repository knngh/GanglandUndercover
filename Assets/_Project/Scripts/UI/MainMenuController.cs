using GanglandUndercover.Audio;
using GanglandUndercover.Gameplay;
using GanglandUndercover.Online;
using GanglandUndercover.SocialDeduction;
using GanglandUndercover.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 主菜单控制器 — Among Us 太空主题 v2。
    /// 全屏深空背景 + 浮动粒子星空 + 发光标题 + 角色按钮（阵营色边框+图标）+ 底部版本号。
    /// 颜色/字体/间距由 ThemeManager 统一管理。
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        // ─── ThemeManager 颜色快捷引用 ──────────────────────────
        private static Color BgDark         => ThemeManager.BackgroundDark;
        private static Color PanelBg        => ThemeManager.PanelBackground;
        private static Color BtnPrimary     => ThemeManager.ButtonPrimary;
        private static Color DangerRed      => ThemeManager.DangerRed;
        private static Color UndercoverBlue => ThemeManager.UndercoverBlue;
        private static Color PoliceGray     => ThemeManager.PoliceGray;
        private static Color MoleTeal       => ThemeManager.MoleTeal;
        private static Color NeonCyan       => ThemeManager.NeonCyan;
        private static Color TitleGold      => ThemeManager.TitleGold;
        private static Color TextPrimary    => ThemeManager.TextPrimary;
        private static Color TextMuted      => ThemeManager.TextMuted;

        // ─── 身份数据 ──────────────────────────────────────────
        private static readonly string[] RoleLabels   = { "卧底", "黑帮", "警察", "线人" };
        private static readonly string[] RoleSubLabels = { "Undercover", "Gang", "Police", "Mole" };
        private static readonly string[] RoleDescs =
        {
            "潜伏黑帮内部，窃取证据提交警方专案组",
            "掌控九龙港区，阻止证据链闭合暴露身份",
            "带队收网清剿黑帮，尽快完成证据链锁定",
            "混入警方技侦部门，暗中收集卧底活动情报"
        };
        private static readonly string[] RoleIcons = { "U", "G", "P", "M" };
        private static readonly SocialRole[] RoleValues =
        {
            SocialRole.Undercover, SocialRole.Gang,
            SocialRole.Police, SocialRole.Mole
        };

        // ─── 地图数据 ──────────────────────────────────────────
        private static readonly string[] MapNames    = { "九龙港区", "警察局" };
        private static readonly string[] MapSubNames = { "Gangland District", "Police Station" };
        private static readonly string[] MapDescs =
        {
            "夜幕下的九龙港区：货柜码头、夜市巷、地下诊所…黑帮与警察的暗战之地",
            "警方总部大楼：大厅、审讯室、证物室、武器库…暗流涌动的警局内部"
        };
        private static readonly MapType[] MapValues = { MapType.GanglandDistrict, MapType.PoliceStation };

        // ─── 引用 ──────────────────────────────────────────────
        private PrototypeBootstrap _bootstrap;
        private Canvas _canvas;
        private GameObject _rootPanel;
        private UIParticleEffect _particles;
        private int _roleIndex;
        private int _mapIndex;
        private bool _visible = true;
        private Image _selectionHighlight;
        private Image _mapHighlight;
        private Text _mapDescText; // 地图描述文本引用
        private Text _loginStatusText;
        private Text _settingsStatusText;
        private Text _settingsPanelStatusText;
        private GameObject _settingsOverlay;
        private Slider _masterVolumeSlider;
        private Slider _sfxVolumeSlider;
        private Slider _bgmVolumeSlider;
        private Slider _voiceVolumeSlider;
        private Slider _mouseSensitivitySlider;
        private Text _masterVolumeValueText;
        private Text _sfxVolumeValueText;
        private Text _bgmVolumeValueText;
        private Text _voiceVolumeValueText;
        private Text _mouseSensitivityValueText;
        private string _onlinePlayerName = "港区玩家";
        private float _nextLoginStatusRefreshTime;

        public bool IsVisible => _visible;
        public bool SettingsPanelVisible => _settingsOverlay != null && _settingsOverlay.activeSelf;

        public void Initialize(PrototypeBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            EnsureSettingsManager();
            BuildUI();
            if (_visible) Show(); else Hide();
        }

        public void Show()
        {
            _visible = true;
            if (_rootPanel != null) _rootPanel.SetActive(true);
            if (_particles != null) _particles.enabled = true;
        }

        public void Hide()
        {
            _visible = false;
            if (_rootPanel != null) _rootPanel.SetActive(false);
            if (_particles != null) _particles.enabled = false;
        }

        private void Update()
        {
            if (!_visible || Time.unscaledTime < _nextLoginStatusRefreshTime)
            {
                return;
            }

            _nextLoginStatusRefreshTime = Time.unscaledTime + 1f;
            RefreshLoginStatus();
        }

        // ══════════════════════════════════════════════════════
        // UI 构建
        // ══════════════════════════════════════════════════════
        private void BuildUI()
        {
            _canvas = GetOrCreateCanvas();
            const float refW = 1920f;

            // 根面板：深空背景
            _rootPanel = CreatePanel("MainMenuRoot", _canvas.transform, BgDark);
            Stretch(_rootPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _rootPanel.GetComponent<Image>().raycastTarget = true;

            // 浮动粒子星空
            var pObj = CreatePanel("StarfieldParticles", _rootPanel.transform, new Color(0, 0, 0, 0));
            Stretch(pObj, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pObj.GetComponent<Image>().raycastTarget = false;
            _particles = pObj.AddComponent<UIParticleEffect>();

            // 渐变遮罩
            CreateGradientOverlay(_rootPanel.transform,
                ThemeManager.WithAlpha(BgDark, 0.3f),
                ThemeManager.WithAlpha(BgDark, 0.95f));

            // ── 大标题（发光：Outline + Shadow）─────────────
            var titleT = MakeText("Title", _rootPanel.transform,
                "港 区 潜 线", ThemeManager.FontSizeTitle, TitleGold, FontStyle.Bold, TextAnchor.MiddleCenter);
            AddShadow(titleT, Hex("#1a1a3e"), new Vector2(3f, -3f));
            AddOutline(titleT, new Color(1f, 1f, 1f, 0.35f));
            Center(titleT.gameObject, 0f, 380f, refW * 0.8f, 70f);

            var subT = MakeText("Subtitle", _rootPanel.transform,
                "Gangland Undercover", ThemeManager.FontSizeSubtitle, NeonCyan,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            AddShadow(subT, Hex("#0a2a3a"), new Vector2(1.5f, -1.5f));
            Center(subT.gameObject, 0f, 316f, refW * 0.8f, 38f);

            var tagT = MakeText("Tagline", _rootPanel.transform,
                "社交推理  |  九龙港区  |  警匪卧底  |  4人局",
                ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(tagT.gameObject, 0f, 278f, refW * 0.8f, 24f);

            // ── 离线面板（左半屏）───────────────────────────
            var offlinePanel = CreateBorderedPanel("OfflinePanel", _rootPanel.transform, PanelBg);
            Center(offlinePanel, -470f, 0f, 720f, 530f);

            PanelHeader(offlinePanel.transform, "离 线 模 式", "单机体验 · 4人对战", -22f);
            Divider(offlinePanel.transform, -54f, 500f);
            SubHeader(offlinePanel.transform, "—  选 择 身 份  —", -76f);

            _selectionHighlight = HighlightBar(offlinePanel.transform, -116f);

            // 四个身份卡片
            const float cardW = 132f, cardH = 162f, cardGap = 22f;
            float sx = -((cardW + cardGap) * 2f - cardGap) * 0.5f + cardW * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                BuildRoleCard(offlinePanel.transform, "RoleCard_" + i,
                    RoleIcons[i], RoleLabels[i], RoleSubLabels[i],
                    sx + i * (cardW + cardGap), -202f, cardW, cardH,
                    GetRoleColor(i), () => OnRoleSelected(idx));
            }

            var descT = MakeText("RoleDesc", offlinePanel.transform,
                RoleDescs[0], ThemeManager.FontSizeSmall,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(descT.gameObject, 0f, -322f, 540f, 48f);

            // ── 地图选择 ────────────────────────────────
            SubHeader(offlinePanel.transform, "—  选 择 地 图  —", -360f);

            const float mapBtnW = 150f, mapBtnH = 38f, mapBtnGap = 24f;
            float mx = -((mapBtnW + mapBtnGap) * 1f - mapBtnGap) * 0.5f + mapBtnW * 0.5f;

            _mapHighlight = HighlightBar(offlinePanel.transform, -396f);
            _mapHighlight.rectTransform.sizeDelta = new Vector2(158f, 3f);

            for (int i = 0; i < 2; i++)
            {
                int mi = i;
                var mb = BuildButton("MapBtn_" + i, offlinePanel.transform,
                    MapNames[i], mapBtnW, mapBtnH,
                    i == 0 ? ThemeManager.WithAlpha(BtnPrimary, 0.6f) : ThemeManager.WithAlpha(PoliceGray, 0.6f),
                    Color.white, ThemeManager.FontSizeSmall);
                Center(mb, mx + i * (mapBtnW + mapBtnGap), -396f, mapBtnW, mapBtnH);
                mb.GetComponent<Button>().onClick.AddListener(() => OnMapSelected(mi));
            }

            _mapDescText = MakeText("MapDesc", offlinePanel.transform,
                MapDescs[0], ThemeManager.FontSizeFooter,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(_mapDescText.gameObject, 0f, -428f, 500f, 40f);

            var startBtn = BuildButton("StartButton", offlinePanel.transform,
                "开  始  游  戏", 280f, ThemeManager.ButtonHeight + 4f,
                BtnPrimary, Color.white, ThemeManager.FontSizeButton);
            Center(startBtn, 0f, -482f, 280f, ThemeManager.ButtonHeight + 4f);
            startBtn.GetComponent<Button>().onClick.AddListener(OnStartOffline);

            // ── 联机面板（右半屏）───────────────────────────
            var onlinePanel = CreateBorderedPanel("OnlinePanel", _rootPanel.transform, PanelBg);
            Center(onlinePanel, 470f, 0f, 720f, 530f);

            PanelHeader(onlinePanel.transform, "登 录 / 联 机", "匿名登录 · Lobby / Relay / Sessions", -22f);
            Divider(onlinePanel.transform, -54f, 500f);

            var infoT = MakeText("OnlineInfo", onlinePanel.transform,
                "使用 Unity 匿名登录进入大厅\n房间列表、Relay 房间码和文本聊天会沿用这个玩家代号",
                ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(infoT.gameObject, 0f, -104f, 540f, 64f);

            var nameT = MakeText("OnlineNameLabel", onlinePanel.transform,
                "玩家代号", ThemeManager.FontSizeSmall, TextMuted, FontStyle.Bold, TextAnchor.MiddleLeft);
            Center(nameT.gameObject, -158f, -160f, 120f, 26f);

            var nameInput = BuildInput("OnlinePlayerNameInput", onlinePanel.transform, _onlinePlayerName, 320f, 42f);
            Center(nameInput, 42f, -160f, 320f, 42f);
            InputField nameField = nameInput.GetComponent<InputField>();
            nameField.onEndEdit.AddListener(value => OnOnlinePlayerNameChanged(value));

            _loginStatusText = MakeText("LoginStatus", onlinePanel.transform,
                BuildLoginStatusLine(null), ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            Center(_loginStatusText.gameObject, 0f, -216f, 520f, 54f);

            var loginBtn = BuildButton("AnonymousLoginButton", onlinePanel.transform,
                "匿 名 登 录", 132f, ThemeManager.ButtonHeight + 4f,
                MoleTeal, Color.white, ThemeManager.FontSizeSmall);
            Center(loginBtn, -74f, -286f, 132f, ThemeManager.ButtonHeight + 4f);
            loginBtn.GetComponent<Button>().onClick.AddListener(OnAnonymousLogin);

            var enterBtn = BuildButton("EnterLobbyButton", onlinePanel.transform,
                "进 入 大 厅", 132f, ThemeManager.ButtonHeight + 4f,
                BtnPrimary, Color.white, ThemeManager.FontSizeButton);
            Center(enterBtn, 74f, -286f, 132f, ThemeManager.ButtonHeight + 4f);
            enterBtn.GetComponent<Button>().onClick.AddListener(OnEnterLobby);

            // F1: 重看教程按钮
            var tutorialBtn = BuildButton("ReplayTutorialButton", onlinePanel.transform,
                "教  程  回  顾", 280f, ThemeManager.ButtonHeight + 4f,
                MoleTeal, Color.white, ThemeManager.FontSizeButton);
            Center(tutorialBtn, 0f, -354f, 280f, ThemeManager.ButtonHeight + 4f);
            tutorialBtn.GetComponent<Button>().onClick.AddListener(OnReplayTutorial);

            // ── 设置中心 ──────────────────────────────────
            SubHeader(onlinePanel.transform, "—  设 置 中 心  —", -416f);
            _settingsStatusText = MakeText("SettingsStatus", onlinePanel.transform,
                BuildSettingsStatusLine(CurrentSettings()), ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(_settingsStatusText.gameObject, 0f, -440f, 560f, 24f);

            BuildSettingsButton(onlinePanel.transform, "打开设置", 0f, -478f, 280f, 34f, OnOpenSettingsPanel);

            // ── 底部版本号 ──────────────────────────────────
            var verT = MakeText("Version", _rootPanel.transform,
                "v0.8  ·  Gangland Undercover  ·  Among Us Inspired",
                ThemeManager.FontSizeFooter, Hex("#4a4a5a"),
                FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(verT.gameObject, 0f, -510f, refW * 0.7f, 20f);

            BuildSettingsOverlay();
        }

        // ══════════════════════════════════════════════════════
        // 交互
        // ══════════════════════════════════════════════════════
        private void OnRoleSelected(int index)
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            _roleIndex = index;

            var descT = FindText(_rootPanel, "RoleDesc");
            if (descT != null) descT.text = RoleDescs[index];

            // 移动高亮条
            const float cardW = 132f, cardGap = 22f;
            float sx = -((cardW + cardGap) * 2f - cardGap) * 0.5f + cardW * 0.5f;
            if (_selectionHighlight != null)
            {
                var hrt = _selectionHighlight.rectTransform;
                hrt.anchoredPosition = new Vector2(sx + index * (cardW + cardGap), hrt.anchoredPosition.y);
                _selectionHighlight.color = GetRoleColor(index);
            }

            for (int i = 0; i < 4; i++)
            {
                var card = FindChild(_rootPanel, "RoleCard_" + i);
                if (card == null) continue;
                var bg = card.GetComponent<Image>();
                if (bg != null)
                    bg.color = i == index
                        ? ThemeManager.WithAlpha(GetRoleColor(i), 0.35f)
                        : ThemeManager.WithAlpha(GetRoleColor(i), 0.10f);
            }
        }

        private void OnMapSelected(int index)
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            _mapIndex = index;

            if (_mapDescText != null)
                _mapDescText.text = MapDescs[index];

            const float mapBtnW = 150f, mapBtnGap = 24f;
            float mx = -((mapBtnW + mapBtnGap) * 1f - mapBtnGap) * 0.5f + mapBtnW * 0.5f;

            if (_mapHighlight != null)
            {
                var hrt = _mapHighlight.rectTransform;
                hrt.anchoredPosition = new Vector2(mx + index * (mapBtnW + mapBtnGap), hrt.anchoredPosition.y);
                _mapHighlight.color = index == 0 ? BtnPrimary : PoliceGray;
            }

            for (int i = 0; i < 2; i++)
            {
                var btn = FindChild(_rootPanel, "MapBtn_" + i);
                if (btn != null)
                {
                    var btnImg = btn.GetComponent<Image>();
                    if (btnImg != null)
                        btnImg.color = i == index
                            ? ThemeManager.WithAlpha(i == 0 ? BtnPrimary : PoliceGray, 0.9f)
                            : ThemeManager.WithAlpha(i == 0 ? BtnPrimary : PoliceGray, 0.35f);
                }
            }
        }

        private void OnStartOffline()
        {
            var role = _roleIndex < RoleValues.Length ? RoleValues[_roleIndex] : SocialRole.Undercover;
            var mapType = _mapIndex < MapValues.Length ? MapValues[_mapIndex] : MapType.GanglandDistrict;
            Hide();
            _bootstrap?.StartOfflineGame(role, mapType);
        }

        private void OnEnterLobby()
        {
            _bootstrap?.SetOnlinePlayerName(_onlinePlayerName);
            Hide();
            _bootstrap?.StartOnlineGame();
        }

        /// <summary>F1: 重看新手教程</summary>
        private void OnReplayTutorial()
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            Hide();
            // 查找 TutorialGateway 并重启教程
            var gateway = FindAnyObjectByType<TutorialGateway>();
            if (gateway != null)
            {
                gateway.RestartTutorial();
                Debug.Log("[MainMenu] Tutorial replay started via TutorialGateway.");
            }
            else
            {
                Debug.LogWarning("[MainMenu] TutorialGateway not found in scene.");
                // 回退到主菜单
                Show();
            }
        }

        // ══════════════════════════════════════════════════════
        // Canvas
        // ══════════════════════════════════════════════════════
        private Canvas GetOrCreateCanvas()
        {
            var ui = FindAnyObjectByType<UIManager>();
            if (ui != null && ui.MainCanvas != null) return ui.MainCanvas;
            var existing = FindAnyObjectByType<Canvas>();
            if (existing != null) return existing;
            var go = new GameObject("UICanvas_Fallback");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        // ══════════════════════════════════════════════════════
        // 组件工厂
        // ══════════════════════════════════════════════════════

        private static GameObject CreatePanel(string n, Transform p, Color c)
        {
            var o = new GameObject(n, typeof(RectTransform), typeof(Image));
            o.transform.SetParent(p, false);
            o.GetComponent<Image>().color = c;
            o.GetComponent<Image>().raycastTarget = false;
            return o;
        }

        private static GameObject CreateBorderedPanel(string n, Transform p, Color bg)
        {
            var o = CreatePanel(n, p, bg);
            // 顶部亮线
            var tl = new GameObject(n + "_Top", typeof(RectTransform), typeof(Image));
            tl.transform.SetParent(o.transform, false);
            tl.GetComponent<Image>().color = NeonCyan; tl.GetComponent<Image>().raycastTarget = false;
            Stretch(tl, new Vector2(0.02f, 0.98f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
            // 底部亮线
            var bl = new GameObject(n + "_Bot", typeof(RectTransform), typeof(Image));
            bl.transform.SetParent(o.transform, false);
            bl.GetComponent<Image>().color = NeonCyan; bl.GetComponent<Image>().raycastTarget = false;
            Stretch(bl, new Vector2(0.02f, 0f), new Vector2(0.98f, 0.02f), Vector2.zero, Vector2.zero);
            return o;
        }

        private static void PanelHeader(Transform p, string title, string sub, float y)
        {
            var t = MakeText("HdrTitle", p, title, ThemeManager.FontSizeHeader, TitleGold, FontStyle.Bold, TextAnchor.MiddleCenter);
            Center(t.gameObject, 0f, y, 500f, 34f);
            var s = MakeText("HdrSub", p, sub, ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(s.gameObject, 0f, y - 30f, 500f, 20f);
        }

        private static void SubHeader(Transform p, string txt, float y)
        {
            var t = MakeText("SubHdr", p, txt, ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(t.gameObject, 0f, y, 400f, 22f);
        }

        private static void Divider(Transform p, float y, float w)
        {
            var o = CreatePanel("Divider", p, ThemeManager.Divider);
            var r = o.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = new Vector2(w, 1f);
        }

        private static Image HighlightBar(Transform p, float y)
        {
            var o = CreatePanel("SelHighlight", p, UndercoverBlue);
            var img = o.GetComponent<Image>();
            var r = o.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(-231f, y);
            r.sizeDelta = new Vector2(140f, 4f);
            return img;
        }

        private static void BuildRoleCard(Transform p, string name,
            string icon, string label, string sub,
            float x, float y, float w, float h, Color c, System.Action onClick)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(p, false);
            var img = card.GetComponent<Image>();
            img.color = ThemeManager.WithAlpha(c, 0.12f); img.raycastTarget = true;
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);

            // 边框
            var frame = CreatePanel(name + "_Frame", card.transform, c);
            Stretch(frame, Vector2.zero, Vector2.one, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            frame.transform.SetAsFirstSibling();

            // 内层
            var inner = CreatePanel(name + "_Inner", card.transform, ThemeManager.CardBackground);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            // 图标字
            var iconT = MakeText(name + "_Icon", card.transform, icon, 44, c, FontStyle.Bold, TextAnchor.MiddleCenter);
            Center(iconT.gameObject, 0f, 16f, w, 52f);

            // 名称
            var nameT = MakeText(name + "_Name", card.transform, label, ThemeManager.FontSizeBody, TextPrimary, FontStyle.Bold, TextAnchor.MiddleCenter);
            Center(nameT.gameObject, 0f, -30f, w, 28f);

            // 副标题
            var subT = MakeText(name + "_Sub", card.transform, sub, ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(subT.gameObject, 0f, -52f, w, 16f);

            var btn = card.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = ThemeManager.WithAlpha(c, 0.12f);
            cb.highlightedColor = ThemeManager.WithAlpha(c, 0.35f);
            cb.pressedColor = ThemeManager.WithAlpha(c, 0.25f);
            cb.disabledColor = new Color(0.15f, 0.15f, 0.18f, 0.5f);
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());
        }

        private static GameObject BuildButton(string n, Transform p,
            string label, float w, float h, Color bg, Color tc, int fs)
        {
            var o = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(Button));
            o.transform.SetParent(p, false);
            o.GetComponent<Image>().color = bg;

            var border = CreatePanel(n + "_Border", o.transform, NeonCyan);
            Stretch(border, Vector2.zero, Vector2.one, new Vector2(-3f, -3f), new Vector2(3f, 3f));
            border.transform.SetAsFirstSibling();

            var b = o.GetComponent<Button>();
            var cb = b.colors;
            cb.normalColor = bg;
            cb.highlightedColor = ThemeManager.ScaleColor(bg, 1.3f);
            cb.pressedColor = ThemeManager.ScaleColor(bg, 0.65f);
            cb.disabledColor = new Color(0.18f, 0.18f, 0.22f, 0.6f);
            b.colors = cb;

            var t = MakeText("Label", o.transform, label, fs, tc, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(t.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return o;
        }

        private static GameObject BuildInput(string n, Transform p, string value, float w, float h)
        {
            var o = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(InputField));
            o.transform.SetParent(p, false);
            o.GetComponent<Image>().color = ThemeManager.InputBackground;

            var field = o.GetComponent<InputField>();
            field.characterLimit = 16;
            field.lineType = InputField.LineType.SingleLine;

            var text = MakeText("Text", o.transform, value, ThemeManager.FontSizeSmall, TextPrimary, FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(text.gameObject, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            field.textComponent = text;
            field.text = value;

            var placeholder = MakeText("Placeholder", o.transform, "港区玩家", ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(placeholder.gameObject, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            field.placeholder = placeholder;
            return o;
        }

        private static GameObject BuildSettingsButton(Transform parent, string label, float x, float y, float w, float h, System.Action onClick)
        {
            GameObject button = BuildButton("Settings_" + label, parent, label, w, h, ThemeManager.WithAlpha(MoleTeal, 0.58f), Color.white, ThemeManager.FontSizeFooter);
            Center(button, x, y, w, h);
            button.GetComponent<Button>().onClick.AddListener(() => onClick());
            return button;
        }

        private void BuildSettingsOverlay()
        {
            _settingsOverlay = CreatePanel("SettingsOverlay", _rootPanel.transform, new Color(0.005f, 0.008f, 0.012f, 0.82f));
            Stretch(_settingsOverlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _settingsOverlay.GetComponent<Image>().raycastTarget = true;

            GameObject panel = CreateBorderedPanel("SettingsPanel", _settingsOverlay.transform, PanelBg);
            Center(panel, 0f, 0f, 820f, 620f);

            PanelHeader(panel.transform, "设 置 中 心", "音频 · 画面 · 游戏 · 辅助功能", -28f);
            Divider(panel.transform, -68f, 640f);

            _settingsPanelStatusText = MakeText("SettingsPanelStatus", panel.transform,
                BuildSettingsStatusLine(CurrentSettings()), ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(_settingsPanelStatusText.gameObject, 0f, -96f, 660f, 26f);

            SettingsData current = CurrentSettings();
            _masterVolumeSlider = BuildSettingsSlider(panel.transform, "主音量", -205f, -154f, current.MasterVolume, OnSetMasterVolume, out _masterVolumeValueText);
            _sfxVolumeSlider = BuildSettingsSlider(panel.transform, "音效", 205f, -154f, current.SfxVolume, OnSetSfxVolume, out _sfxVolumeValueText);
            _bgmVolumeSlider = BuildSettingsSlider(panel.transform, "音乐", -205f, -224f, current.BgmVolume, OnSetBgmVolume, out _bgmVolumeValueText);
            _voiceVolumeSlider = BuildSettingsSlider(panel.transform, "聊天音量", 205f, -224f, current.VoiceChatVolume, OnSetVoiceVolume, out _voiceVolumeValueText);
            _mouseSensitivitySlider = BuildSettingsSlider(panel.transform, "鼠标灵敏度", 0f, -294f, Mathf.InverseLerp(0.1f, 10f, current.MouseSensitivity), OnSetMouseSensitivityNormalized, out _mouseSensitivityValueText);

            const float buttonW = 148f;
            const float buttonH = 38f;
            const float gap = 18f;
            float startX = -((buttonW + gap) * 4f - gap) * 0.5f + buttonW * 0.5f;
            BuildSettingsButton(panel.transform, "画质", startX, -374f, buttonW, buttonH, OnCycleQuality);
            BuildSettingsButton(panel.transform, "窗口", startX + (buttonW + gap), -374f, buttonW, buttonH, OnCycleWindowMode);
            BuildSettingsButton(panel.transform, "色盲", startX + (buttonW + gap) * 2f, -374f, buttonW, buttonH, OnCycleColorBlindMode);
            BuildSettingsButton(panel.transform, "帧率", startX + (buttonW + gap) * 3f, -374f, buttonW, buttonH, OnCycleFrameRate);

            BuildSettingsButton(panel.transform, "垂直同步", startX, -430f, buttonW, buttonH, OnToggleVSync);
            BuildSettingsButton(panel.transform, "自由发言", startX + (buttonW + gap), -430f, buttonW, buttonH, OnCycleVoiceMode);
            BuildSettingsButton(panel.transform, "重置设置", startX + (buttonW + gap) * 2f, -430f, buttonW, buttonH, OnResetSettings);
            BuildSettingsButton(panel.transform, "关闭", startX + (buttonW + gap) * 3f, -430f, buttonW, buttonH, OnCloseSettingsPanel);

            var hintText = MakeText("SettingsHint", panel.transform,
                "设置会立即保存并应用；联机语音已改为文本聊天，聊天音量用于后续提示音与文本聊天反馈。",
                ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(hintText.gameObject, 0f, -500f, 680f, 34f);

            _settingsOverlay.SetActive(false);
            RefreshSettingsPanelControls();
        }

        private static Slider BuildSettingsSlider(
            Transform parent,
            string label,
            float x,
            float y,
            float value,
            UnityEngine.Events.UnityAction<float> onChanged,
            out Text valueText)
        {
            GameObject row = new GameObject("SettingsSlider_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            Center(row, x, y, 340f, 50f);

            Text labelText = MakeText("Label", row.transform, label, ThemeManager.FontSizeSmall, TextMuted, FontStyle.Bold, TextAnchor.MiddleLeft);
            Stretch(labelText.gameObject, Vector2.zero, Vector2.one, new Vector2(0f, 22f), new Vector2(-230f, 0f));

            valueText = MakeText("Value", row.transform, string.Empty, ThemeManager.FontSizeFooter, TextPrimary, FontStyle.Normal, TextAnchor.MiddleRight);
            Stretch(valueText.gameObject, Vector2.zero, Vector2.one, new Vector2(250f, 22f), Vector2.zero);

            GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(row.transform, false);
            Stretch(sliderObject, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -26f));

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            GameObject background = CreatePanel("Background", sliderObject.transform, ThemeManager.InputBackground);
            Stretch(background, Vector2.zero, Vector2.one, new Vector2(0f, 8f), new Vector2(0f, -8f));

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea, Vector2.zero, Vector2.one, new Vector2(4f, 8f), new Vector2(-4f, -8f));

            GameObject fill = CreatePanel("Fill", fillArea.transform, NeonCyan);
            Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slider.fillRect = fill.GetComponent<RectTransform>();

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            Stretch(handleArea, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));

            GameObject handle = CreatePanel("Handle", handleArea.transform, TitleGold);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16f, 30f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        private static Text MakeText(string n, Transform p, string content, int fs, Color c, FontStyle s, TextAnchor a)
        {
            var o = new GameObject(n, typeof(RectTransform), typeof(Text));
            o.transform.SetParent(p, false);
            var t = o.GetComponent<Text>();
            t.text = content; t.font = LoadFont(); t.fontSize = fs;
            t.color = c; t.fontStyle = s; t.alignment = a;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void AddShadow(Text t, Color c, Vector2 d)
        {
            if (t == null) return;
            var s = t.gameObject.GetComponent<Shadow>() ?? t.gameObject.AddComponent<Shadow>();
            s.effectColor = c; s.effectDistance = d;
        }

        private static void AddOutline(Text t, Color c)
        {
            if (t == null) return;
            var o = t.gameObject.GetComponent<Outline>() ?? t.gameObject.AddComponent<Outline>();
            o.effectColor = c; o.effectDistance = new Vector2(2f, -2f);
        }

        private static void CreateGradientOverlay(Transform p, Color top, Color bot)
        {
            var o = CreatePanel("GradientOverlay", p, bot);
            Stretch(o, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tb = CreatePanel("GradTop", o.transform, top);
            Stretch(tb, new Vector2(0f, 0.7f), Vector2.one, Vector2.zero, Vector2.zero);
        }

        // ══════════════════════════════════════════════════════
        // 布局工具
        // ══════════════════════════════════════════════════════
        private static void Stretch(GameObject o, Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
        {
            var rt = o.GetComponent<RectTransform>() ?? o.AddComponent<RectTransform>();
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = omin; rt.offsetMax = omax;
        }

        private static void Center(GameObject o, float ax, float ay, float w, float h)
        {
            var rt = o.GetComponent<RectTransform>() ?? o.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static GameObject FindChild(GameObject parent, string name)
        {
            foreach (Transform t in parent.transform)
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static Text FindText(GameObject parent, string name)
        {
            foreach (Transform t in parent.transform)
                if (t.name == name) return t.GetComponent<Text>();
            return null;
        }

        private static Color GetRoleColor(int i) => i switch
        {
            0 => UndercoverBlue, 1 => DangerRed, 2 => PoliceGray, _ => MoleTeal
        };

        private static Color Hex(string h)
        {
            if (h.StartsWith("#")) h = h.Substring(1);
            return new Color(
                int.Parse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                1f);
        }

        private void OnOnlinePlayerNameChanged(string value)
        {
            _onlinePlayerName = LimitText(value, 16, "港区玩家");
            _bootstrap?.SetOnlinePlayerName(_onlinePlayerName);
            RefreshLoginStatus();
        }

        private async void OnAnonymousLogin()
        {
            UnityServiceBootstrap service = EnsureUnityServiceBootstrap();
            RefreshLoginStatus();

            if (service != null)
            {
                await service.InitializeAsync();
            }

            RefreshLoginStatus();
        }

        private void OnOpenSettingsPanel()
        {
            if (_settingsOverlay == null)
            {
                return;
            }

            RefreshSettingsPanelControls();
            _settingsOverlay.SetActive(true);
        }

        private void OnCloseSettingsPanel()
        {
            if (_settingsOverlay != null)
            {
                _settingsOverlay.SetActive(false);
            }
        }

        private void OnSetMasterVolume(float value)
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetMasterVolume(value);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnSetSfxVolume(float value)
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetSfxVolume(value);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnSetBgmVolume(float value)
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetBgmVolume(value);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnSetVoiceVolume(float value)
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetVoiceChatVolume(value);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnSetMouseSensitivityNormalized(float value)
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetMouseSensitivity(Mathf.Lerp(0.1f, 10f, Mathf.Clamp01(value)));
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnCycleMasterVolume()
        {
            SettingsManager settings = EnsureSettingsManager();
            float current = settings.Current.MasterVolume;
            float next = current >= 0.99f ? 0.4f : Mathf.Clamp01(current + 0.2f);
            settings.SetMasterVolume(next);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnCycleQuality()
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetQualityPreset((settings.Current.QualityPreset + 1) % SettingsManager.QualityPresetNames.Length);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnCycleWindowMode()
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetWindowMode((settings.Current.WindowMode + 1) % SettingsData.WindowModeNames.Length);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnCycleColorBlindMode()
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetColorBlindMode((settings.Current.ColorBlindMode + 1) % 4);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnCycleFrameRate()
        {
            SettingsManager settings = EnsureSettingsManager();
            int currentIndex = System.Array.IndexOf(SettingsManager.FrameRateOptions, settings.Current.FrameRateCap);
            int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % SettingsManager.FrameRateOptions.Length;
            settings.SetFrameRateCap(SettingsManager.FrameRateOptions[nextIndex]);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnToggleVSync()
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetVSync(!settings.Current.VSync);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnCycleVoiceMode()
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.SetVoiceMode((settings.Current.VoiceMode + 1) % SettingsManager.VoiceModeNames.Length);
            settings.Save();
            RefreshSettingsStatus();
        }

        private void OnResetSettings()
        {
            SettingsManager settings = EnsureSettingsManager();
            settings.ResetToDefault();
            RefreshSettingsStatus();
        }

        private void RefreshLoginStatus()
        {
            if (_loginStatusText != null)
            {
                _loginStatusText.text = BuildLoginStatusLine(FindAnyObjectByType<UnityServiceBootstrap>());
            }
        }

        private void RefreshSettingsStatus()
        {
            if (_settingsStatusText != null)
            {
                _settingsStatusText.text = BuildSettingsStatusLine(CurrentSettings());
            }

            RefreshSettingsPanelControls();
        }

        private void RefreshSettingsPanelControls()
        {
            SettingsData settings = CurrentSettings();

            if (_settingsPanelStatusText != null)
            {
                _settingsPanelStatusText.text = BuildSettingsStatusLine(settings);
            }

            SetSliderWithoutNotify(_masterVolumeSlider, settings.MasterVolume, _masterVolumeValueText, FormatPercent(settings.MasterVolume));
            SetSliderWithoutNotify(_sfxVolumeSlider, settings.SfxVolume, _sfxVolumeValueText, FormatPercent(settings.SfxVolume));
            SetSliderWithoutNotify(_bgmVolumeSlider, settings.BgmVolume, _bgmVolumeValueText, FormatPercent(settings.BgmVolume));
            SetSliderWithoutNotify(_voiceVolumeSlider, settings.VoiceChatVolume, _voiceVolumeValueText, FormatPercent(settings.VoiceChatVolume));
            SetSliderWithoutNotify(
                _mouseSensitivitySlider,
                Mathf.InverseLerp(0.1f, 10f, settings.MouseSensitivity),
                _mouseSensitivityValueText,
                settings.MouseSensitivity.ToString("0.0"));
        }

        private static SettingsData CurrentSettings()
        {
            SettingsManager settings = EnsureSettingsManager();
            return settings != null && settings.Current != null ? settings.Current : SettingsData.CreateDefault();
        }

        private static SettingsManager EnsureSettingsManager()
        {
            if (SettingsManager.Instance != null)
            {
                return SettingsManager.Instance;
            }

            GameObject settingsObject = new GameObject("Settings Manager");
            SettingsManager manager = settingsObject.AddComponent<SettingsManager>();
            manager.Load();
            return manager;
        }

        private static UnityServiceBootstrap EnsureUnityServiceBootstrap()
        {
            UnityServiceBootstrap existing = FindAnyObjectByType<UnityServiceBootstrap>();
            if (existing != null)
            {
                return existing;
            }

            GameObject serviceObject = new GameObject("Unity Services Login Preview");
            return serviceObject.AddComponent<UnityServiceBootstrap>();
        }

        private static string BuildLoginStatusLine(UnityServiceBootstrap service)
        {
            if (service == null)
            {
                return "登录: 匿名账号将在进入大厅后初始化\nCloud/Auth/Lobby/Relay 状态会在联机 HUD 中继续显示";
            }

            string player = string.IsNullOrWhiteSpace(service.PlayerId) ? "未分配" : service.PlayerId;
            return "登录: " + (service.AuthenticationReady ? "匿名账号已就绪" : "等待匿名登录")
                + " | PlayerId " + player
                + "\n" + service.ServiceReadinessSummary;
        }

        private static string BuildSettingsStatusLine(SettingsData data)
        {
            SettingsData safe = data ?? SettingsData.CreateDefault();
            string quality = SettingsManager.QualityPresetNames[Mathf.Clamp(safe.QualityPreset, 0, SettingsManager.QualityPresetNames.Length - 1)];
            string window = SettingsData.WindowModeNames[Mathf.Clamp(safe.WindowMode, 0, SettingsData.WindowModeNames.Length - 1)];
            return "音量 " + Mathf.RoundToInt(safe.MasterVolume * 100f)
                + "% | 画质 " + quality
                + " | " + window
                + " | 帧率 " + SettingsManager.GetFrameRateName(safe.FrameRateCap)
                + " | " + (safe.VSync ? "VSync 开" : "VSync 关")
                + " | 聊天 " + SettingsManager.VoiceModeNames[Mathf.Clamp(safe.VoiceMode, 0, SettingsManager.VoiceModeNames.Length - 1)]
                + " | 色盲 " + safe.ColorBlindMode;
        }

        private static string LimitText(string value, int maxLength, string fallback)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return safe.Length <= maxLength ? safe : safe.Substring(0, maxLength);
        }

        private static void SetSliderWithoutNotify(Slider slider, float value, Text valueText, string label)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            }

            if (valueText != null)
            {
                valueText.text = label;
            }
        }

        private static string FormatPercent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }
    }
}
