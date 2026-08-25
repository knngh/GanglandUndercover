using GanglandUndercover.Audio;
using GanglandUndercover.Art;
using GanglandUndercover.Gameplay;
using GanglandUndercover.Online;
using GanglandUndercover.SocialDeduction;
using GanglandUndercover.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 港区夜间行动入口。首屏同时承载单机身份/地图选择与联机匿名身份初始化。
    /// 使用项目已审阅的港区、警局和角色 Sprite，避免与实际游戏割裂的占位视觉。
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        // ─── ThemeManager 颜色快捷引用 ──────────────────────────
        private static Color BgDark         => Hex("#070a0b");
        private static Color PanelBg        => new Color(0.045f, 0.075f, 0.078f, 0.91f);
        private static Color BtnPrimary     => Hex("#a93632");
        private static Color DangerRed      => Hex("#b94a44");
        private static Color UndercoverBlue => Hex("#3f78a6");
        private static Color PoliceGray     => Hex("#87939b");
        private static Color MoleTeal       => Hex("#278f89");
        private static Color NeonCyan       => Hex("#2bb7aa");
        private static Color TitleGold      => Hex("#d2aa55");
        private static Color TextPrimary    => ThemeManager.TextPrimary;
        private static Color TextMuted      => Hex("#99a3a1");
        private static Color SurfaceDark    => Hex("#071214");
        private static Color SurfaceRaised  => Hex("#172627");

        private const float RoleCardWidth = 204f;
        private const float RoleCardGap = 12f;
        private const float RoleCardStartX = -324f;
        private const float MapButtonWidth = 158f;
        private const float MapButtonGap = 12f;
        private const float MapButtonStartX = 190f;

        private const string AiHarbourBackdropPath = "Sprites/AIReviewed/Backgrounds/gangland-harbour-login-v1";
        private const string AiHarbourMapPreviewPath = "Sprites/AIReviewed/MapPreviews/gangland-harbour-map-preview-v1";
        private const string HarbourBackdropPath = "Sprites/Tilesets/Harbour/decorations/industrial-district-menu";
        private const string PoliceMapPreviewPath = "Sprites/Tilesets/PoliceStation/floors/command-deck";

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
        private static readonly string[] RolePortraitPaths =
        {
            "Sprites/Characters/UndercoverAgent/avatar",
            "Sprites/Characters/Enforcer/avatar",
            "Sprites/Characters/Inspector/avatar",
            "Sprites/Characters/Mole/avatar"
        };
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
            "警方总部大楼：大厅、审讯室、证物室、监控室…暗流涌动的警局内部"
        };
        private static readonly MapType[] MapValues = { MapType.GanglandDistrict, MapType.PoliceStation };

        // ─── 引用 ──────────────────────────────────────────────
        private PrototypeBootstrap _bootstrap;
        private Canvas _canvas;
        private GameObject _rootPanel;
        private int _roleIndex;
        private int _mapIndex;
        private bool _visible = true;
        private Image _selectionHighlight;
        private Image _mapHighlight;
        private Image _mapPreviewImage;
        private Image _loginStatusIndicator;
        private Text _mapDescText;
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
            GanglandUndercover.Audio.AudioManager.Instance?.PlayBGM(GanglandUndercover.Audio.MusicTrack.MainMenu);
        }

        public void Hide()
        {
            _visible = false;
            if (_rootPanel != null) _rootPanel.SetActive(false);
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
            UIArtCache.Ensure();

            _rootPanel = CreatePanel("MainMenuRoot", _canvas.transform, BgDark);
            Stretch(_rootPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _rootPanel.GetComponent<Image>().raycastTarget = true;

            BuildHarbourBackdrop(_rootPanel.transform);

            // 品牌区保持开阔，让港区夜景成为第一视线而不是另一张卡片。
            GameObject brandLockup = new GameObject("BrandLockup", typeof(RectTransform));
            brandLockup.transform.SetParent(_rootPanel.transform, false);
            Center(brandLockup, -640f, 398f, 780f, 182f);

            GameObject brandRule = CreatePanel("BrandRule", brandLockup.transform, TitleGold);
            Center(brandRule, -377f, 22f, 5f, 126f);

            var titleT = MakeText("Title", _rootPanel.transform,
                "港区潜线", 68, TextPrimary, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleT.transform.SetParent(brandLockup.transform, false);
            AddShadow(titleT, Color.black, new Vector2(3f, -3f));
            Center(titleT.gameObject, 35f, 42f, 640f, 72f);

            var subT = MakeText("Subtitle", _rootPanel.transform,
                "GANGLAND UNDERCOVER", 21, TitleGold, FontStyle.Bold, TextAnchor.MiddleLeft);
            subT.transform.SetParent(brandLockup.transform, false);
            Center(subT.gameObject, 35f, -12f, 640f, 30f);

            var tagT = MakeText("Tagline", _rootPanel.transform,
                "九龙港区  /  夜间行动席位  /  身份不可公开",
                18, TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            tagT.transform.SetParent(brandLockup.transform, false);
            Center(tagT.gameObject, 35f, -54f, 640f, 30f);

            // 左侧单机行动台。
            var offlinePanel = CreateBorderedPanel("OfflinePanel", _rootPanel.transform, PanelBg);
            Center(offlinePanel, -430f, -70f, 960f, 700f);

            BuildSectionHeading(offlinePanel.transform, "单机行动", "LOCAL CASE FILE  /  选择视角与行动区域", 306f);
            Divider(offlinePanel.transform, 262f, 860f);
            BuildFieldLabel(offlinePanel.transform, "01  身份档案", -402f, 230f);

            _selectionHighlight = HighlightBar(offlinePanel.transform, 20f);
            _selectionHighlight.rectTransform.sizeDelta = new Vector2(RoleCardWidth, 4f);
            _selectionHighlight.rectTransform.anchoredPosition = new Vector2(RoleCardStartX, 2f);

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                BuildRoleCard(offlinePanel.transform, "RoleCard_" + i,
                    i, RolePortraitPaths[i], RoleIcons[i], RoleLabels[i], RoleSubLabels[i],
                    RoleCardStartX + i * (RoleCardWidth + RoleCardGap), 116f, RoleCardWidth, 238f,
                    GetRoleColor(i), () => OnRoleSelected(idx));
            }

            GameObject dossierBand = CreatePanel("RoleDossierBand", offlinePanel.transform,
                new Color(0.025f, 0.045f, 0.047f, 0.92f));
            Center(dossierBand, 0f, -30f, 860f, 62f);
            AddInsetBorder(dossierBand.transform, ThemeManager.WithAlpha(UndercoverBlue, 0.52f), 1f);

            var descT = MakeText("RoleDesc", offlinePanel.transform,
                "卧底  ·  " + RoleDescs[0], ThemeManager.FontSizeSmall,
                TextPrimary, FontStyle.Normal, TextAnchor.MiddleLeft);
            Center(descT.gameObject, 8f, -30f, 790f, 42f);

            BuildFieldLabel(offlinePanel.transform, "02  行动区域", -402f, -96f);

            GameObject mapPreviewFrame = CreatePanel("MapPreviewFrame", offlinePanel.transform, SurfaceDark);
            Center(mapPreviewFrame, -245f, -208f, 360f, 208f);
            AddInsetBorder(mapPreviewFrame.transform, TitleGold, 2f);
            _mapPreviewImage = CreateSpriteImage(
                "MapPreview",
                mapPreviewFrame.transform,
                LoadPreferredHarbourMapPreview(),
                Color.white,
                true);
            Stretch(_mapPreviewImage.gameObject, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            _mapHighlight = HighlightBar(offlinePanel.transform, -139f);
            _mapHighlight.rectTransform.sizeDelta = new Vector2(MapButtonWidth, 3f);
            _mapHighlight.rectTransform.anchoredPosition = new Vector2(MapButtonStartX, -166f);

            for (int i = 0; i < 2; i++)
            {
                int mi = i;
                var mb = BuildButton("MapBtn_" + i, offlinePanel.transform,
                    MapNames[i], MapButtonWidth, 44f,
                    i == 0 ? ThemeManager.WithAlpha(BtnPrimary, 0.6f) : ThemeManager.WithAlpha(PoliceGray, 0.6f),
                    Color.white, ThemeManager.FontSizeSmall);
                Center(mb, MapButtonStartX + i * (MapButtonWidth + MapButtonGap), -143f, MapButtonWidth, 44f);
                mb.GetComponent<Button>().onClick.AddListener(() => OnMapSelected(mi));
            }

            _mapDescText = MakeText("MapDesc", offlinePanel.transform,
                MapNames[0] + "  /  " + MapDescs[0], ThemeManager.FontSizeFooter,
                TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            Center(_mapDescText.gameObject, 205f, -212f, 350f, 78f);

            var startBtn = BuildButton("StartButton", offlinePanel.transform,
                "开始单机行动  →", 370f, 70f,
                BtnPrimary, Color.white, 20);
            Center(startBtn, 205f, -296f, 370f, 70f);
            startBtn.GetComponent<Button>().onClick.AddListener(OnStartOffline);

            // 右侧联机身份台。信息层级固定为代号 -> 认证状态 -> 进入大厅。
            var onlinePanel = CreateBorderedPanel("OnlinePanel", _rootPanel.transform, PanelBg);
            Center(onlinePanel, 550f, -40f, 620f, 780f);

            BuildSectionHeading(onlinePanel.transform, "联机行动", "匿名身份 / Lobby / Relay", 340f);
            Divider(onlinePanel.transform, 292f, 520f);

            var infoT = MakeText("OnlineInfo", onlinePanel.transform,
                "设置行动代号。进入大厅后可创建房间、输入 Relay 房间码或浏览可加入的行动。",
                ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            Center(infoT.gameObject, 0f, 245f, 500f, 52f);

            var nameT = MakeText("OnlineNameLabel", onlinePanel.transform,
                "行动代号", ThemeManager.FontSizeSmall, TitleGold, FontStyle.Bold, TextAnchor.MiddleLeft);
            Center(nameT.gameObject, 0f, 190f, 500f, 24f);

            var nameInput = BuildInput("OnlinePlayerNameInput", onlinePanel.transform, _onlinePlayerName, 500f, 52f);
            Center(nameInput, 0f, 147f, 500f, 52f);
            InputField nameField = nameInput.GetComponent<InputField>();
            nameField.onEndEdit.AddListener(value => OnOnlinePlayerNameChanged(value));

            GameObject connectionState = CreatePanel("ConnectionState", onlinePanel.transform, SurfaceDark);
            Center(connectionState, 0f, 62f, 500f, 96f);
            AddInsetBorder(connectionState.transform, ThemeManager.WithAlpha(NeonCyan, 0.45f), 1f);
            _loginStatusIndicator = CreatePanel("StatusIndicator", connectionState.transform, TitleGold).GetComponent<Image>();
            Center(_loginStatusIndicator.gameObject, -226f, 22f, 8f, 36f);
            var connectionLabel = MakeText("ConnectionLabel", connectionState.transform,
                "身份认证", ThemeManager.FontSizeSmall, TitleGold, FontStyle.Bold, TextAnchor.MiddleLeft);
            Center(connectionLabel.gameObject, 4f, 26f, 430f, 22f);
            _loginStatusText = MakeText("LoginStatus", connectionState.transform,
                BuildLoginStatusLine(null), ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            Center(_loginStatusText.gameObject, 4f, -14f, 430f, 52f);

            var loginBtn = BuildButton("AnonymousLoginButton", onlinePanel.transform,
                "初始化匿名身份", 500f, 54f,
                MoleTeal, Color.white, ThemeManager.FontSizeButton);
            Center(loginBtn, 0f, -27f, 500f, 54f);
            loginBtn.GetComponent<Button>().onClick.AddListener(OnAnonymousLogin);

            Divider(onlinePanel.transform, -72f, 500f);

            var enterBtn = BuildButton("EnterLobbyButton", onlinePanel.transform,
                "进入联机大厅", 500f, 72f,
                BtnPrimary, Color.white, 21);
            Center(enterBtn, 0f, -125f, 500f, 72f);
            enterBtn.GetComponent<Button>().onClick.AddListener(OnEnterLobby);

            var onlineHint = MakeText("OnlineHint", onlinePanel.transform,
                "无需注册账号；联机服务状态会在大厅持续显示。",
                ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(onlineHint.gameObject, 0f, -172f, 500f, 22f);

            var tutorialBtn = BuildButton("ReplayTutorialButton", onlinePanel.transform,
                "行动教程", 240f, 48f,
                SurfaceRaised, TextPrimary, ThemeManager.FontSizeSmall);
            Center(tutorialBtn, -130f, -225f, 240f, 48f);
            tutorialBtn.GetComponent<Button>().onClick.AddListener(OnReplayTutorial);

            BuildSettingsButton(onlinePanel.transform, "系统设置", 130f, -225f, 240f, 48f, OnOpenSettingsPanel);

            _settingsStatusText = MakeText("SettingsStatus", onlinePanel.transform,
                BuildSettingsStatusLine(CurrentSettings()), ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            Center(_settingsStatusText.gameObject, 0f, -286f, 500f, 46f);

            var verT = MakeText("Version", _rootPanel.transform,
                "PRE-ALPHA 0.8  /  本地与联机原型",
                ThemeManager.FontSizeFooter, Hex("#66706e"),
                FontStyle.Normal, TextAnchor.MiddleRight);
            Center(verT.gameObject, 550f, -494f, 620f, 22f);

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
            if (descT != null) descT.text = RoleLabels[index] + "  ·  " + RoleDescs[index];

            // 移动高亮条
            if (_selectionHighlight != null)
            {
                var hrt = _selectionHighlight.rectTransform;
                hrt.anchoredPosition = new Vector2(
                    RoleCardStartX + index * (RoleCardWidth + RoleCardGap),
                    hrt.anchoredPosition.y);
                _selectionHighlight.color = GetRoleColor(index);
            }

            for (int i = 0; i < 4; i++)
            {
                var card = FindChild(_rootPanel, "RoleCard_" + i);
                if (card == null) continue;
                var bg = card.GetComponent<Image>();
                if (bg != null)
                    bg.color = i == index
                        ? ThemeManager.WithAlpha(GetRoleColor(i), 0.34f)
                        : ThemeManager.WithAlpha(SurfaceRaised, 0.96f);
            }
        }

        private void OnMapSelected(int index)
        {
            AudioManager.Instance?.PlaySFX(SoundEffect.UIClick);
            _mapIndex = index;

            if (_mapDescText != null)
                _mapDescText.text = MapNames[index] + "  /  " + MapDescs[index];

            if (_mapHighlight != null)
            {
                var hrt = _mapHighlight.rectTransform;
                hrt.anchoredPosition = new Vector2(
                    MapButtonStartX + index * (MapButtonWidth + MapButtonGap),
                    hrt.anchoredPosition.y);
                _mapHighlight.color = index == 0 ? BtnPrimary : PoliceGray;
            }

            if (_mapPreviewImage != null)
            {
                _mapPreviewImage.sprite = index == 0
                    ? LoadPreferredHarbourMapPreview()
                    : LoadSprite(PoliceMapPreviewPath);
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

        private static void BuildHarbourBackdrop(Transform parent)
        {
            Image backdrop = CreateSpriteImage(
                "HarbourBackdrop",
                parent,
                LoadPreferredHarbourBackdrop(),
                new Color(1f, 1f, 1f, 1f),
                false);
            Center(backdrop.gameObject, 0f, 0f, 1920f, 1080f);

            GameObject wash = CreatePanel("BackdropWash", parent, new Color(0.005f, 0.014f, 0.016f, 0.12f));
            Stretch(wash, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject topHaze = CreatePanel("TopHaze", parent, new Color(0.01f, 0.04f, 0.05f, 0.34f));
            Stretch(topHaze, new Vector2(0f, 0.58f), Vector2.one, Vector2.zero, Vector2.zero);

            GameObject rightShade = CreatePanel("RightShade", parent, new Color(0.008f, 0.016f, 0.018f, 0.84f));
            Stretch(rightShade, new Vector2(0.64f, 0f), Vector2.one, Vector2.zero, Vector2.zero);

            GameObject bottomShade = CreatePanel("BottomShade", parent, new Color(0.005f, 0.012f, 0.014f, 0.48f));
            Stretch(bottomShade, Vector2.zero, new Vector2(1f, 0.25f), Vector2.zero, Vector2.zero);

            GameObject horizon = CreatePanel("HorizonLight", parent, new Color(0.08f, 0.34f, 0.35f, 0.22f));
            Center(horizon, -340f, -46f, 1120f, 3f);

            GameObject horizonEcho = CreatePanel("HorizonEcho", parent, new Color(0.82f, 0.55f, 0.20f, 0.18f));
            Center(horizonEcho, -340f, -62f, 760f, 2f);

            GameObject locationRail = CreatePanel("LocationRail", parent, TitleGold);
            Center(locationRail, -946f, 0f, 4f, 1080f);
        }

        private static Image CreateSpriteImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            bool preserveAspect)
        {
            GameObject imageObject = CreatePanel(name, parent, color);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            return Resources.Load<Sprite>(resourcePath);
        }

        private static Sprite LoadPreferredHarbourBackdrop()
        {
            return LoadSprite(AiHarbourBackdropPath) ?? LoadSprite(HarbourBackdropPath);
        }

        private static Sprite LoadPreferredHarbourMapPreview()
        {
            return LoadSprite(AiHarbourMapPreviewPath) ?? LoadSprite(HarbourBackdropPath);
        }

        private static void BuildSectionHeading(Transform parent, string title, string subtitle, float y)
        {
            float width = Mathf.Max(420f, ((RectTransform)parent).sizeDelta.x - 100f);
            Text titleText = MakeText(
                "HdrTitle",
                parent,
                title,
                24,
                TextPrimary,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            Center(titleText.gameObject, 0f, y, width, 34f);

            Text subtitleText = MakeText(
                "HdrSub",
                parent,
                subtitle,
                ThemeManager.FontSizeSmall,
                TextMuted,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            Center(subtitleText.gameObject, 0f, y - 30f, width, 22f);
        }

        private static void BuildFieldLabel(Transform parent, string text, float x, float y)
        {
            Text label = MakeText(
                "FieldLabel_" + text,
                parent,
                text,
                ThemeManager.FontSizeSmall,
                TitleGold,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            Center(label.gameObject, x, y, 160f, 24f);
        }

        private static void AddInsetBorder(Transform parent, Color color, float thickness)
        {
            GameObject top = CreatePanel("BorderTop", parent, color);
            Stretch(top, new Vector2(0f, 1f), Vector2.one, new Vector2(thickness, -thickness), new Vector2(-thickness, 0f));

            GameObject bottom = CreatePanel("BorderBottom", parent, color);
            Stretch(bottom, Vector2.zero, new Vector2(1f, 0f), new Vector2(thickness, 0f), new Vector2(-thickness, thickness));

            GameObject left = CreatePanel("BorderLeft", parent, color);
            Stretch(left, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));

            GameObject right = CreatePanel("BorderRight", parent, color);
            Stretch(right, new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
        }

        private static GameObject CreateBorderedPanel(string n, Transform p, Color bg)
        {
            var o = CreatePanel(n, p, bg);
            Image panelImage = o.GetComponent<Image>();
            if (UIArtCache.PanelFrame != null)
            {
                panelImage.sprite = UIArtCache.PanelFrame;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = new Color(0.24f, 0.38f, 0.39f, bg.a);
            }

            var tl = new GameObject(n + "_Top", typeof(RectTransform), typeof(Image));
            tl.transform.SetParent(o.transform, false);
            tl.GetComponent<Image>().color = TitleGold; tl.GetComponent<Image>().raycastTarget = false;
            Stretch(tl, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 4f));

            var bl = new GameObject(n + "_Bot", typeof(RectTransform), typeof(Image));
            bl.transform.SetParent(o.transform, false);
            bl.GetComponent<Image>().color = ThemeManager.WithAlpha(TitleGold, 0.25f); bl.GetComponent<Image>().raycastTarget = false;
            Stretch(bl, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f));
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
            int index, string portraitPath, string icon, string label, string sub,
            float x, float y, float w, float h, Color c, System.Action onClick)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(p, false);
            var img = card.GetComponent<Image>();
            img.color = index == 0
                ? ThemeManager.WithAlpha(c, 0.34f)
                : ThemeManager.WithAlpha(SurfaceRaised, 0.96f);
            img.raycastTarget = true;
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);

            AddInsetBorder(card.transform, ThemeManager.WithAlpha(c, 0.62f), 1f);

            GameObject portraitFrame = CreatePanel(name + "_PortraitFrame", card.transform, SurfaceDark);
            Center(portraitFrame, 0f, 42f, 150f, 150f);
            AddInsetBorder(portraitFrame.transform, ThemeManager.WithAlpha(c, 0.86f), 2f);

            Image portrait = CreateSpriteImage(
                "RolePortrait_" + index,
                portraitFrame.transform,
                LoadSprite(portraitPath),
                Color.white,
                true);
            Stretch(portrait.gameObject, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));

            if (portrait.sprite == null)
            {
                Text fallbackIcon = MakeText(
                    name + "_Icon",
                    portraitFrame.transform,
                    icon,
                    50,
                    c,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter);
                Stretch(fallbackIcon.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            var indexT = MakeText(name + "_Index", card.transform, "0" + (index + 1),
                14, ThemeManager.WithAlpha(c, 0.94f), FontStyle.Bold, TextAnchor.MiddleLeft);
            Center(indexT.gameObject, -w * 0.5f + 18f, h * 0.5f - 18f, 34f, 20f);

            var nameT = MakeText(name + "_Name", card.transform, label, 20, TextPrimary, FontStyle.Bold, TextAnchor.MiddleCenter);
            Center(nameT.gameObject, 0f, -62f, w - 16f, 30f);

            var subT = MakeText(name + "_Sub", card.transform, sub, ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(subT.gameObject, 0f, -90f, w - 16f, 18f);

            GameObject roleRail = CreatePanel(name + "_Rail", card.transform, c);
            Center(roleRail, 0f, -114f, w, 4f);

            var btn = card.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = SurfaceRaised;
            cb.highlightedColor = ThemeManager.ScaleColor(SurfaceRaised, 1.35f);
            cb.pressedColor = ThemeManager.WithAlpha(c, 0.32f);
            cb.disabledColor = new Color(0.12f, 0.14f, 0.14f, 0.5f);
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());
        }

        private static GameObject BuildButton(string n, Transform p,
            string label, float w, float h, Color bg, Color tc, int fs)
        {
            var o = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(Button));
            o.transform.SetParent(p, false);
            Image buttonImage = o.GetComponent<Image>();
            buttonImage.color = bg;
            if (UIArtCache.ButtonNormal != null)
            {
                buttonImage.sprite = UIArtCache.ButtonNormal;
                buttonImage.type = Image.Type.Sliced;
            }

            GameObject accent = CreatePanel(n + "_Accent", o.transform, ThemeManager.ScaleColor(bg, 1.45f));
            Stretch(accent, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f));

            var b = o.GetComponent<Button>();
            var cb = b.colors;
            cb.normalColor = bg;
            cb.highlightedColor = ThemeManager.ScaleColor(bg, 1.3f);
            cb.pressedColor = ThemeManager.ScaleColor(bg, 0.65f);
            cb.disabledColor = new Color(0.18f, 0.18f, 0.22f, 0.6f);
            b.colors = cb;

            var t = MakeText("Label", o.transform, label, fs, tc, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(t.gameObject, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            return o;
        }

        private static GameObject BuildInput(string n, Transform p, string value, float w, float h)
        {
            var o = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(InputField));
            o.transform.SetParent(p, false);
            o.GetComponent<Image>().color = ThemeManager.InputBackground;
            AddInsetBorder(o.transform, ThemeManager.WithAlpha(NeonCyan, 0.42f), 1f);

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
            GameObject button = BuildButton("Settings_" + label, parent, label, w, h, SurfaceRaised, Color.white, ThemeManager.FontSizeFooter);
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
            BuildSettingsButton(panel.transform, "自由发送", startX + (buttonW + gap), -430f, buttonW, buttonH, OnCycleVoiceMode);
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
            t.text = content; t.font = UIStyle.GetFontForText(content, fs); t.fontSize = fs;
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
            UnityServiceBootstrap service = FindAnyObjectByType<UnityServiceBootstrap>();
            if (_loginStatusText != null)
            {
                _loginStatusText.text = BuildLoginStatusLine(service);
            }

            if (_loginStatusIndicator != null)
            {
                _loginStatusIndicator.color = service != null && service.AuthenticationReady
                    ? MoleTeal
                    : TitleGold;
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
