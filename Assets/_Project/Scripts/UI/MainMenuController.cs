using GanglandUndercover.Audio;
using GanglandUndercover.Gameplay;
using GanglandUndercover.SocialDeduction;
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

        public bool IsVisible => _visible;

        public void Initialize(PrototypeBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
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

            PanelHeader(onlinePanel.transform, "联 机 模 式", "多人对战 · Unity Netcode + Relay", -22f);
            Divider(onlinePanel.transform, -54f, 500f);

            var infoT = MakeText("OnlineInfo", onlinePanel.transform,
                "通过房间码加入同一对局\n支持 4 人联机推理对战\n语音/文字交流（需自行组队）",
                ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(infoT.gameObject, 0f, -150f, 480f, 110f);

            var enterBtn = BuildButton("EnterLobbyButton", onlinePanel.transform,
                "进  入  大  厅", 280f, ThemeManager.ButtonHeight + 4f,
                BtnPrimary, Color.white, ThemeManager.FontSizeButton);
            Center(enterBtn, 0f, -250f, 280f, ThemeManager.ButtonHeight + 4f);
            enterBtn.GetComponent<Button>().onClick.AddListener(OnEnterLobby);

            // ── 底部版本号 ──────────────────────────────────
            var verT = MakeText("Version", _rootPanel.transform,
                "v0.8  ·  Gangland Undercover  ·  Among Us Inspired",
                ThemeManager.FontSizeFooter, Hex("#4a4a5a"),
                FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(verT.gameObject, 0f, -510f, refW * 0.7f, 20f);
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
            Hide();
            _bootstrap?.StartOnlineGame();
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
    }
}
