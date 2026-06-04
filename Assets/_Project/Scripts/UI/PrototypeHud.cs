using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using GanglandUndercover.SocialDeduction;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 游戏内 HUD — Among Us 太空主题 v2。
    /// 左上角：角色头像框（阵营色边框） + 状态指示器。
    /// 右上角：证据/情报进度条 + 回合计数。
    /// 底部：任务列表卡片式布局，完成项打勾划线。
    /// 行动按钮（通风管/击杀/Sabotage）统一霓虹风格。
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        // ─── 主题色 ────────────────────────────────────────────
        private static Color BgDark        => ThemeManager.BackgroundDark;
        private static Color PanelBg       => ThemeManager.PanelBackground;
        private static Color UndercoverBlue=> ThemeManager.UndercoverBlue;
        private static Color DangerRed     => ThemeManager.DangerRed;
        private static Color PoliceGray    => ThemeManager.PoliceGray;
        private static Color MoleTeal      => ThemeManager.MoleTeal;
        private static Color NeonCyan      => ThemeManager.NeonCyan;
        private static Color TitleGold     => ThemeManager.TitleGold;
        private static Color TextPrimary   => ThemeManager.TextPrimary;
        private static Color TextMuted     => ThemeManager.TextMuted;
        private static Color SafeGreen     => ThemeManager.SafeGreen;

        private GameController _controller;
        private Canvas _canvas;

        // UI 元素引用
        private Text _roleAvatarIcon;
        private Image _roleAvatarFrame;
        private Text _playerNameText;
        private Text _factionText;
        private Text _statusIndicator;
        private Text _dayText;
        private Text _evidenceText;
        private Image _evidenceBarFill;
        private Text _intelText;
        private Image _intelBarFill;
        private GameObject _taskListRoot;
        private RectTransform _taskListContent;
        private Text _infoText;

        public void Bind(GameController controller)
        {
            _controller = controller;
            _controller.Changed += Refresh;
            BuildLayout();
            Refresh();
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.Changed -= Refresh;
        }

        // ══════════════════════════════════════════════════════
        // 布局构建
        // ══════════════════════════════════════════════════════
        private void BuildLayout()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();

            // ── 顶部栏 ───────────────────────────────────────
            var topBar = Panel("TopBar", transform, ThemeManager.WithAlpha(BgDark, 0.88f));
            Stretch(topBar, new Vector2(0f, 0.92f), Vector2.one, Vector2.zero, Vector2.zero);

            BuildAvatar(topBar.transform);
            BuildCenterInfo(topBar.transform);
            BuildRightPanel(topBar.transform);

            // ── 底部行动栏 ──────────────────────────────────
            var bottomBar = Panel("BottomBar", transform, ThemeManager.WithAlpha(BgDark, 0.88f));
            Stretch(bottomBar, Vector2.zero, new Vector2(1f, 0.08f), Vector2.zero, Vector2.zero);

            // 顶部亮线
            var topLine = Panel("BottomLine", bottomBar.transform, NeonCyan);
            Stretch(topLine, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), Vector2.zero);

            BuildActionButtons(bottomBar.transform);

            // ── 左侧信息面板 ────────────────────────────────
            var sidePanel = Panel("SidePanel", transform, ThemeManager.WithAlpha(PanelBg, 0.9f));
            Stretch(sidePanel, new Vector2(0.01f, 0.09f), new Vector2(0.19f, 0.91f), Vector2.zero, Vector2.zero);

            // 边框
            var sFrame = Panel("SideFrame", sidePanel.transform, NeonCyan);
            Stretch(sFrame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), Vector2.zero);

            BuildTaskList(sidePanel.transform);

            // ── 右侧信息面板 ────────────────────────────────
            var rightPanel = Panel("RightPanel", transform, ThemeManager.WithAlpha(PanelBg, 0.9f));
            Stretch(rightPanel, new Vector2(0.81f, 0.09f), new Vector2(0.99f, 0.91f), Vector2.zero, Vector2.zero);

            var rFrame = Panel("RightFrame", rightPanel.transform, NeonCyan);
            Stretch(rFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f));

            BuildInfoPanel(rightPanel.transform);
        }

        // ─── 左上角：角色头像 ──────────────────────────────────
        private void BuildAvatar(Transform parent)
        {
            // 圆形头像框：用 Image mask 模拟
            var avatarRoot = Panel("AvatarRoot", parent, new Color(0, 0, 0, 0));
            var art = avatarRoot.GetComponent<RectTransform>();
            art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f);
            art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = new Vector2(20f, 0f);
            art.sizeDelta = new Vector2(54f, 54f);

            // 阵营色边框
            var frameObj = Panel("AvatarFrame", avatarRoot.transform, UndercoverBlue);
            _roleAvatarFrame = frameObj.GetComponent<Image>();
            Stretch(frameObj, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 内部深色圆
            var innerObj = Panel("AvatarInner", avatarRoot.transform, Hex("#12122e"));
            Stretch(innerObj, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            // 角色字母
            _roleAvatarIcon = MakeText("AvatarIcon", innerObj.transform, "?", 28, TextPrimary, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(_roleAvatarIcon.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 玩家名 + 阵营
            _playerNameText = MakeText("PlayerName", parent, "", ThemeManager.FontSizeBody, TextPrimary, FontStyle.Bold, TextAnchor.MiddleLeft);
            var pnRt = _playerNameText.GetComponent<RectTransform>();
            pnRt.anchorMin = pnRt.anchorMax = new Vector2(0f, 0.5f);
            pnRt.pivot = new Vector2(0f, 0.5f);
            pnRt.anchoredPosition = new Vector2(86f, 8f);
            pnRt.sizeDelta = new Vector2(160f, 24f);

            _factionText = MakeText("Faction", parent, "", ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            var ftRt = _factionText.GetComponent<RectTransform>();
            ftRt.anchorMin = ftRt.anchorMax = new Vector2(0f, 0.5f);
            ftRt.pivot = new Vector2(0f, 0.5f);
            ftRt.anchoredPosition = new Vector2(86f, -12f);
            ftRt.sizeDelta = new Vector2(160f, 20f);
        }

        // ─── 顶部中央：状态 ──────────────────────────────────
        private void BuildCenterInfo(Transform parent)
        {
            _statusIndicator = MakeText("Status", parent, "", ThemeManager.FontSizeSmall, NeonCyan, FontStyle.Normal, TextAnchor.MiddleCenter);
            var sRt = _statusIndicator.GetComponent<RectTransform>();
            sRt.anchorMin = sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = Vector2.zero;
            sRt.sizeDelta = new Vector2(400f, 24f);
        }

        // ─── 右上角：证据/情报进度条 + 回合 ──────────────────
        private void BuildRightPanel(Transform parent)
        {
            // 天数
            _dayText = MakeText("Day", parent, "第 1 天", ThemeManager.FontSizeHeader, TitleGold, FontStyle.Bold, TextAnchor.MiddleRight);
            var dayRt = _dayText.GetComponent<RectTransform>();
            dayRt.anchorMin = dayRt.anchorMax = new Vector2(1f, 0.5f);
            dayRt.pivot = new Vector2(1f, 0.5f);
            dayRt.anchoredPosition = new Vector2(-260f, 8f);
            dayRt.sizeDelta = new Vector2(120f, 30f);

            // 证据进度条
            var evLabel = MakeText("EvLabel", parent, "证据", ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.MiddleRight);
            var elRt = evLabel.GetComponent<RectTransform>();
            elRt.anchorMin = elRt.anchorMax = new Vector2(1f, 0.5f);
            elRt.pivot = new Vector2(1f, 0.5f);
            elRt.anchoredPosition = new Vector2(-260f, -12f);
            elRt.sizeDelta = new Vector2(40f, 16f);

            var evBg = Panel("EvBg", parent, ThemeManager.InputBackground);
            var ebRt = evBg.GetComponent<RectTransform>();
            ebRt.anchorMin = ebRt.anchorMax = new Vector2(1f, 0.5f);
            ebRt.pivot = new Vector2(1f, 0.5f);
            ebRt.anchoredPosition = new Vector2(-210f, -12f);
            ebRt.sizeDelta = new Vector2(100f, 10f);

            var evFill = Panel("EvFill", evBg.transform, UndercoverBlue);
            _evidenceBarFill = evFill.GetComponent<Image>();
            var efRt = evFill.GetComponent<RectTransform>();
            efRt.anchorMin = new Vector2(0f, 0f); efRt.anchorMax = new Vector2(0f, 1f);
            efRt.pivot = Vector2.zero; efRt.offsetMin = Vector2.zero; efRt.offsetMax = Vector2.zero;

            _evidenceText = MakeText("EvText", parent, "0/10", ThemeManager.FontSizeFooter, NeonCyan, FontStyle.Normal, TextAnchor.MiddleLeft);
            var etRt = _evidenceText.GetComponent<RectTransform>();
            etRt.anchorMin = etRt.anchorMax = new Vector2(1f, 0.5f);
            etRt.pivot = new Vector2(1f, 0.5f);
            etRt.anchoredPosition = new Vector2(-100f, -12f);
            etRt.sizeDelta = new Vector2(50f, 16f);

            // 线人情报进度条（仅 Mole/黑帮阵营显示）
            var ilLabel = MakeText("IlLabel", parent, "情报", ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.MiddleRight);
            var ilRt = ilLabel.GetComponent<RectTransform>();
            ilRt.anchorMin = ilRt.anchorMax = new Vector2(1f, 0.5f);
            ilRt.pivot = new Vector2(1f, 0.5f);
            ilRt.anchoredPosition = new Vector2(-20f, -12f);
            ilRt.sizeDelta = new Vector2(40f, 16f);

            var ilBg = Panel("IlBg", parent, ThemeManager.InputBackground);
            var ibRt = ilBg.GetComponent<RectTransform>();
            ibRt.anchorMin = ibRt.anchorMax = new Vector2(1f, 0.5f);
            ibRt.pivot = new Vector2(1f, 0.5f);
            ibRt.anchoredPosition = new Vector2(30f, -12f);
            ibRt.sizeDelta = new Vector2(100f, 10f);

            var ilFill = Panel("IlFill", ilBg.transform, MoleTeal);
            _intelBarFill = ilFill.GetComponent<Image>();
            var iifRt = ilFill.GetComponent<RectTransform>();
            iifRt.anchorMin = new Vector2(0f, 0f); iifRt.anchorMax = new Vector2(0f, 1f);
            iifRt.pivot = Vector2.zero; iifRt.offsetMin = Vector2.zero; iifRt.offsetMax = Vector2.zero;

            _intelText = MakeText("IlText", parent, "0/10", ThemeManager.FontSizeFooter, NeonCyan, FontStyle.Normal, TextAnchor.MiddleLeft);
            var itRt = _intelText.GetComponent<RectTransform>();
            itRt.anchorMin = itRt.anchorMax = new Vector2(1f, 0.5f);
            itRt.pivot = new Vector2(1f, 0.5f);
            itRt.anchoredPosition = new Vector2(140f, -12f);
            itRt.sizeDelta = new Vector2(50f, 16f);
        }

        // ─── 底部行动按钮 ──────────────────────────────────
        private void BuildActionButtons(Transform parent)
        {
            float btnW = 110f, btnH = 38f, startX = -320f, gap = 20f;

            // 这里按不同角色显示不同按钮，但先创建占位
            CreateActionBtn(parent, "VentBtn", "通 风 管", startX, btnW, btnH, NeonCyan);
            CreateActionBtn(parent, "KillBtn", "击  杀", startX + (btnW + gap), btnW, btnH, DangerRed);
            CreateActionBtn(parent, "SabotageBtn", "破  坏", startX + (btnW + gap) * 2, btnW, btnH, MoleTeal);
            CreateActionBtn(parent, "ReportBtn", "报  告", startX + (btnW + gap) * 3, btnW, btnH, SafeGreen);
            CreateActionBtn(parent, "MeetingBtn", "会  议", startX + (btnW + gap) * 4, btnW, btnH, TitleGold);
            CreateActionBtn(parent, "HeatBtn", "热 度 榜", startX + (btnW + gap) * 5, btnW, btnH, TextMuted);
        }

        private static void CreateActionBtn(Transform parent, string name, string label, float x, float w, float h, Color c)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = ThemeManager.WithAlpha(c, 0.18f);
            obj.GetComponent<Image>().raycastTarget = true;

            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(w, h);

            var border = Panel(name + "_Border", obj.transform, c);
            Stretch(border, Vector2.zero, Vector2.one, new Vector2(-1f, -1f), new Vector2(1f, 1f));
            border.transform.SetAsFirstSibling();

            var t = MakeText("Label", obj.transform, label, ThemeManager.FontSizeSmall, TextPrimary, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(t.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var btn = obj.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = ThemeManager.WithAlpha(c, 0.18f);
            cb.highlightedColor = ThemeManager.WithAlpha(c, 0.45f);
            cb.pressedColor = ThemeManager.WithAlpha(c, 0.3f);
            cb.disabledColor = new Color(0.12f, 0.12f, 0.15f, 0.4f);
            btn.colors = cb;
        }

        // ─── 左侧任务列表 ──────────────────────────────────
        private void BuildTaskList(Transform parent)
        {
            var titleObj = MakeText("TaskTitle", parent, "任务列表", ThemeManager.FontSizeSmall, TitleGold, FontStyle.Bold, TextAnchor.UpperCenter);
            var ttRt = titleObj.GetComponent<RectTransform>();
            ttRt.anchorMin = new Vector2(0f, 1f); ttRt.anchorMax = new Vector2(1f, 1f);
            ttRt.pivot = new Vector2(0.5f, 1f);
            ttRt.anchoredPosition = new Vector2(0f, -12f);
            ttRt.sizeDelta = new Vector2(0f, 28f);

            var div = Panel("TaskDiv", parent, ThemeManager.Divider);
            var drt = div.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.1f, 1f); drt.anchorMax = new Vector2(0.9f, 1f);
            drt.pivot = new Vector2(0.5f, 1f);
            drt.anchoredPosition = new Vector2(0f, -42f);
            drt.sizeDelta = new Vector2(0f, 1f);

            // 任务滚动区域
            _taskListContent = new GameObject("TaskListContent", typeof(RectTransform)).GetComponent<RectTransform>();
            _taskListContent.SetParent(parent, false);
            _taskListContent.anchorMin = new Vector2(0f, 0f);
            _taskListContent.anchorMax = new Vector2(1f, 1f);
            _taskListContent.pivot = new Vector2(0.5f, 1f);
            _taskListContent.anchoredPosition = new Vector2(0f, -50f);
            _taskListContent.sizeDelta = new Vector2(-20f, -58f);

            _taskListRoot = _taskListContent.gameObject;
        }

        // ─── 右侧信息面板 ──────────────────────────────────
        private void BuildInfoPanel(Transform parent)
        {
            var titleObj = MakeText("InfoTitle", parent, "案件板", ThemeManager.FontSizeSmall, TitleGold, FontStyle.Bold, TextAnchor.UpperCenter);
            var ttRt = titleObj.GetComponent<RectTransform>();
            ttRt.anchorMin = new Vector2(0f, 1f); ttRt.anchorMax = new Vector2(1f, 1f);
            ttRt.pivot = new Vector2(0.5f, 1f);
            ttRt.anchoredPosition = new Vector2(0f, -12f);
            ttRt.sizeDelta = new Vector2(0f, 28f);

            var div = Panel("InfoDiv", parent, ThemeManager.Divider);
            var drt = div.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.1f, 1f); drt.anchorMax = new Vector2(0.9f, 1f);
            drt.pivot = new Vector2(0.5f, 1f);
            drt.anchoredPosition = new Vector2(0f, -42f);
            drt.sizeDelta = new Vector2(0f, 1f);

            _infoText = MakeText("InfoContent", parent, "", ThemeManager.FontSizeFooter, TextPrimary, FontStyle.Normal, TextAnchor.UpperLeft);
            var iRt = _infoText.GetComponent<RectTransform>();
            iRt.anchorMin = Vector2.zero; iRt.anchorMax = Vector2.one;
            iRt.offsetMin = new Vector2(12f, 12f);
            iRt.offsetMax = new Vector2(-12f, -54f);
        }

        // ══════════════════════════════════════════════════════
        // 刷新
        // ══════════════════════════════════════════════════════
        private void Refresh()
        {
            if (_controller == null) return;
            GameState state = _controller.State;

            // 头像 + 阵营
            var factionColor = ThemeManager.GetFactionColor(state.PlayerFaction);
            var roleColor = ThemeManager.GetRoleColor(state.PlayerRole);

            if (_roleAvatarFrame != null) _roleAvatarFrame.color = factionColor;
            if (_roleAvatarIcon != null)
                _roleAvatarIcon.text = RoleIcon(state.PlayerRole);
            if (_playerNameText != null)
                _playerNameText.text = RoleDisplayName(state.PlayerRole);
            if (_factionText != null)
                _factionText.text = FactionLabel(state.PlayerFaction);

            // 状态
            if (_statusIndicator != null)
                _statusIndicator.text = PhaseLabel(state.Phase);

            // 天数
            if (_dayText != null)
                _dayText.text = $"第 {state.Day} 天";

            // 证据进度条
            float evFrac = GameState.UndercoverEvidenceTarget > 0
                ? Mathf.Clamp01((float)state.UndercoverEvidence / GameState.UndercoverEvidenceTarget)
                : 0f;
            if (_evidenceBarFill != null)
            {
                var efRt = _evidenceBarFill.GetComponent<RectTransform>();
                efRt.anchorMax = new Vector2(evFrac, 1f);
            }
            if (_evidenceText != null)
                _evidenceText.text = $"{state.UndercoverEvidence}/{GameState.UndercoverEvidenceTarget}";

            // 线人情报进度条
            float ilFrac = GameState.MoleIntelTarget > 0
                ? Mathf.Clamp01((float)state.MoleIntel / GameState.MoleIntelTarget)
                : 0f;
            if (_intelBarFill != null)
            {
                var iifRt = _intelBarFill.GetComponent<RectTransform>();
                iifRt.anchorMax = new Vector2(ilFrac, 1f);
            }
            if (_intelText != null)
                _intelText.text = $"{state.MoleIntel}/{GameState.MoleIntelTarget}";

            // 任务列表
            RefreshTaskList(state);

            // 案件板信息
            if (_infoText != null)
            {
                string info = $"阵营控制：黑帮 {state.GangControlledDistricts} / 警察 {state.PoliceControlledDistricts} / 争议 {state.ContestedDistricts}\n";
                info += $"警力热度：{state.PoliceHeat}\n";
                info += $"货运进度：{state.ShipmentProgress}\n";
                info += $"卧底掩护：{state.Cover}%\n";
                info += $"嫌疑程度：{state.Suspicion}\n";
                if (state.Log.Count > 0)
                {
                    info += "\n最近动态：\n";
                    int start = Mathf.Max(0, state.Log.Count - 4);
                    for (int i = start; i < state.Log.Count; i++)
                        info += $"· {state.Log[i]}\n";
                }
                _infoText.text = info;
            }
        }

        private void RefreshTaskList(GameState state)
        {
            if (_taskListRoot == null) return;
            // 清除旧任务项
            foreach (Transform t in _taskListContent)
                Destroy(t.gameObject);

            // 生成任务卡片
            float y = 0f;
            float itemH = 26f;
            AddTaskItem("搜集证据", state.UndercoverEvidence, GameState.UndercoverEvidenceTarget, ref y, itemH);
            AddTaskItem("降低嫌疑", state.Cover, 100, ref y, itemH);
            AddTaskItem("控制区域", state.PoliceControlledDistricts, 5, ref y, itemH);
            AddTaskItem("线人情报", state.MoleIntel, GameState.MoleIntelTarget, ref y, itemH);
        }

        private void AddTaskItem(string label, int current, int target, ref float y, float itemH)
        {
            bool done = current >= target;
            var item = new GameObject("TaskItem_" + label, typeof(RectTransform));
            item.transform.SetParent(_taskListContent, false);
            var rt = item.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, y);
            rt.sizeDelta = new Vector2(320f, itemH);

            string prefix = done ? "=" : "□";
            string content = done ? $"{prefix} {label}" : $"{prefix} {label}  {current}/{target}";
            var text = MakeText("Label", item.transform, content, ThemeManager.FontSizeFooter,
                done ? SafeGreen : TextPrimary,
                done ? FontStyle.Normal : FontStyle.Normal,
                TextAnchor.MiddleLeft);
            var tRt = text.GetComponent<RectTransform>();
            tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 0.5f);
            tRt.pivot = new Vector2(0f, 0.5f);
            tRt.anchoredPosition = new Vector2(0f, 0f);
            tRt.sizeDelta = new Vector2(320f, itemH);

            if (done)
            {
                // 划线效果：在文字下方加一条细线
                var strike = Panel("Strike", item.transform, ThemeManager.WithAlpha(SafeGreen, 0.5f));
                var srt = strike.GetComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(0f, 0.5f);
                srt.anchoredPosition = new Vector2(8f, 0f);
                srt.sizeDelta = new Vector2(text.preferredWidth, 1f);
            }

            y -= itemH + 4f;
        }

        // ══════════════════════════════════════════════════════
        // 标签工具
        // ══════════════════════════════════════════════════════
        private static string RoleIcon(SocialRole r) => r switch
        {
            SocialRole.Gang => "G", SocialRole.Undercover => "U",
            SocialRole.Police => "P", SocialRole.Mole => "M", _ => "?"
        };

        private static string RoleDisplayName(SocialRole r) => r switch
        {
            SocialRole.Gang => "黑帮", SocialRole.Undercover => "卧底",
            SocialRole.Police => "警察", SocialRole.Mole => "线人", _ => "?"
        };

        private static string FactionLabel(Faction f) => f switch
        {
            Faction.Gang => "黑帮阵营", Faction.Undercover => "卧底阵营",
            Faction.Police => "警察阵营", Faction.Mole => "线人阵营",
            Faction.None => "中立", _ => "?"
        };

        private static string PhaseLabel(GamePhase p) => p switch
        {
            GamePhase.RoleSelect => "选择身份",
            GamePhase.PlayerTurn => "你的回合",
            GamePhase.AiTurn => "对手行动中...",
            GamePhase.Meeting => "会议投票",
            GamePhase.GameOver => "游戏结束",
            _ => ""
        };

        // ══════════════════════════════════════════════════════
        // UI 工厂
        // ══════════════════════════════════════════════════════
        private static GameObject Panel(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = color;
            obj.GetComponent<Image>().raycastTarget = false;
            return obj;
        }

        private static Text MakeText(string name, Transform parent, string content, int fs, Color c, FontStyle s, TextAnchor a)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var t = obj.GetComponent<Text>();
            t.text = content; t.font = LoadFont(); t.fontSize = fs;
            t.color = c; t.fontStyle = s; t.alignment = a;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(GameObject obj, Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
        {
            var rt = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = omin; rt.offsetMax = omax;
        }

        private static Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

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
