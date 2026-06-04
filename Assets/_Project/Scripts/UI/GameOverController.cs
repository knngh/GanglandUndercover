using System.Collections;
using System.Collections.Generic;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using GanglandUndercover.SocialDeduction;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 结算控制器 — Among Us 风格结算动画。
    /// 全屏遮罩渐入 → 胜利方大标题弹入 → 身份卡片翻转揭示 → 统计数据进度条动画 →
    /// 淘汰时间线 → 自动返回主菜单（3秒倒计时，可跳过）。
    /// 颜色/字体/圆角/动画时长由 ThemeManager 统一管理。
    /// </summary>
    public sealed class GameOverController : MonoBehaviour
    {
        // ─── 主题色 ────────────────────────────────────────────
        private static Color BgDark       => ThemeManager.BackgroundDark;
        private static Color PanelBg      => ThemeManager.PanelBackground;
        private static Color CardBg       => ThemeManager.CardBackground;
        private static Color NeonCyan     => ThemeManager.NeonCyan;
        private static Color TitleGold    => ThemeManager.TitleGold;
        private static Color TextPrimary  => ThemeManager.TextPrimary;
        private static Color TextMuted    => ThemeManager.TextMuted;
        private static Color DangerRed    => ThemeManager.DangerRed;
        private static Color SafeGreen    => ThemeManager.SafeGreen;
        private static Color UndercoverBlue => ThemeManager.UndercoverBlue;
        private static Color MoleTeal     => ThemeManager.MoleTeal;

        [Header("引用")]
        [SerializeField] private Canvas _parentCanvas;

        // 内部状态
        private Canvas _canvas;
        private GameObject _root;
        private UIParticleEffect _particles;
        private PrototypeBootstrap _bootstrap;
        private GameObject _skipButtonObj;
        private GameObject _timelineObj;
        private float _autoReturnTimer;
        private bool _autoReturnActive;

        // PlayerRecord 传递结构
        public struct PlayerRecord
        {
            public string Name;
            public SocialRole Role;
            public Faction Faction;
            public bool Won;
            public int TasksCompleted;
            public int TotalTasks;
            public int IntelSubmitted;
            public int Victims;
            public bool Alive;
            public string EliminatedBy;
        }

        /// <summary>由 PrototypeBootstrap 在创建时调用。</summary>
        public void Initialize(PrototypeBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
        }

        public void Show(List<PlayerRecord> records, Faction winningFaction)
        {
            gameObject.SetActive(true);
            StartCoroutine(PlaySequence(records, winningFaction));
        }

        public void Hide()
        {
            _autoReturnActive = false;
            if (_root != null) _root.SetActive(false);
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            _canvas = _parentCanvas != null ? _parentCanvas : GetOrCreateCanvas();
            BuildRoot();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_autoReturnActive) return;
            _autoReturnTimer -= Time.deltaTime;
            UpdateSkipButtonLabel();
            if (_autoReturnTimer <= 0f)
            {
                _autoReturnActive = false;
                ReturnToMenu();
            }
        }

        // ══════════════════════════════════════════════════════
        // 动画序列
        // ══════════════════════════════════════════════════════
        private IEnumerator PlaySequence(List<PlayerRecord> records, Faction winningFaction)
        {
            if (_root == null) BuildRoot();
            _root.SetActive(true);
            CanvasGroup cg = _root.GetComponent<CanvasGroup>();
            if (cg == null) cg = _root.AddComponent<CanvasGroup>();

            // 清除旧的动态子节点
            foreach (Transform t in _root.transform)
                if (t.name.StartsWith("Dynamic_")) Destroy(t.gameObject);

            // 1) 遮罩渐入
            cg.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < ThemeManager.FadeInDuration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, elapsed / ThemeManager.FadeInDuration);
                yield return null;
            }
            cg.alpha = 1f;

            // 2) 胜利标题弹入
            var winnerTitle = BuildSlideInTitle(winningFaction);
            yield return AnimateSlideIn(winnerTitle, ThemeManager.SlideInDuration, -120f, 0f);

            yield return new WaitForSeconds(0.3f);

            // 3) 身份卡片逐一翻转揭示
            float startY = 100f;
            float cardW = 220f, cardH = 290f, gap = 32f;
            float sx = -((cardW + gap) * 3f - gap) * 0.5f + cardW * 0.5f;
            var flipRoutines = new List<IEnumerator>();
            for (int i = 0; i < records.Count; i++)
            {
                var card = BuildIdentityCard(i, records[i], cardW, cardH);
                var crt = card.GetComponent<RectTransform>();
                crt.anchoredPosition = new Vector2(sx + i * (cardW + gap), startY);
                crt.sizeDelta = new Vector2(cardW, cardH);

                StartCoroutine(FlipRevealCard(card, records[i], i * 0.15f));
            }

            yield return new WaitForSeconds(0.15f * records.Count + ThemeManager.FlipDuration + 0.4f);

            // 4) 统计数据进度条
            var statsPanel = BuildStatsPanel(records);
            statsPanel.GetComponent<CanvasGroup>().alpha = 0f;
            yield return AnimateFadeIn(statsPanel, 0.5f);
            yield return AnimateProgressBars(statsPanel, records);

            yield return new WaitForSeconds(0.4f);

            // 5) 淘汰时间线
            BuildEliminationTimeline(records);
            if (_timelineObj != null)
            {
                _timelineObj.GetComponent<CanvasGroup>().alpha = 0f;
                yield return AnimateFadeIn(_timelineObj, 0.5f);
            }

            yield return new WaitForSeconds(0.3f);

            // 6) 跳过按钮 + 自动返回倒计时
            BuildSkipButton();
            _autoReturnTimer = 3f;
            _autoReturnActive = true;
        }

        // ══════════════════════════════════════════════════════
        // 胜利标题
        // ══════════════════════════════════════════════════════
        private GameObject BuildSlideInTitle(Faction winner)
        {
            string winText = winner switch
            {
                Faction.Gang       => "黑 帮 胜 利",
                Faction.Undercover => "卧 底 胜 利",
                Faction.Police     => "警 察 胜 利",
                _                  => "游 戏 结 束"
            };
            Color winColor = winner switch
            {
                Faction.Gang       => DangerRed,
                Faction.Undercover => UndercoverBlue,
                Faction.Police     => SafeGreen,
                _                  => TextPrimary
            };

            var go = new GameObject("Dynamic_WinnerTitle", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_root.transform, false);
            var txt = go.GetComponent<Text>();
            txt.text = winText;
            txt.font = LoadFont();
            txt.fontSize = 52;
            txt.color = winColor;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(4f, -4f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = ThemeManager.WithAlpha(winColor, 0.3f);
            outline.effectDistance = new Vector2(3f, -3f);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 120f);
            rt.sizeDelta = new Vector2(800f, 80f);

            return go;
        }

        private IEnumerator AnimateSlideIn(GameObject obj, float duration, float fromY, float toY)
        {
            var rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, fromY);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out cubic
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(fromY, toY, eased));
                yield return null;
            }
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, toY);
        }

        // ══════════════════════════════════════════════════════
        // 身份卡片翻转
        // ══════════════════════════════════════════════════════
        private GameObject BuildIdentityCard(int index, PlayerRecord rec, float w, float h)
        {
            var card = new GameObject("Dynamic_Card_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(_root.transform, false);
            card.GetComponent<Image>().color = CardBg;
            card.GetComponent<Image>().raycastTarget = true;

            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // 阵营色边框
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(card.transform, false);
            border.GetComponent<Image>().color = ThemeManager.GetFactionColor(rec.Faction);
            border.GetComponent<Image>().raycastTarget = false;
            Stretch(border, Vector2.zero, Vector2.one, new Vector2(-2f, -2f), new Vector2(2f, 2f));
            border.transform.SetAsFirstSibling();

            // 内层
            var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(card.transform, false);
            inner.GetComponent<Image>().color = Hex("#0e0e26");
            inner.GetComponent<Image>().raycastTarget = false;
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            // 初始隐藏（翻面效果：缩放x为0再展开）
            rt.localScale = new Vector3(0f, 1f, 1f);

            return card;
        }

        private IEnumerator FlipRevealCard(GameObject card, PlayerRecord rec, float delay)
        {
            yield return new WaitForSeconds(delay);

            var rt = card.GetComponent<RectTransform>();
            float elapsed = 0f;
            while (elapsed < ThemeManager.FlipDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / (ThemeManager.FlipDuration * 0.5f));
                rt.localScale = new Vector3(Mathf.Lerp(0f, 1f, t), 1f, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;

            // 填充内容
            float w = rt.sizeDelta.x, h = rt.sizeDelta.y;
            var inner = card.transform.Find("Inner");
            if (inner == null) yield break;

            Color roleColor = ThemeManager.GetRoleColor(rec.Role);
            // 感叹号或勾
            string statusIcon = rec.Won ? "WIN" : "LOSE";
            Color statusColor = rec.Won ? ThemeManager.SafeGreen : ThemeManager.DangerRed;

            // 状态标记
            var statusObj = MakeText("Status", inner, statusIcon, 38, statusColor, FontStyle.Bold, TextAnchor.UpperCenter);
            Center(statusObj.gameObject, 0f, h * 0.35f, w, 50f);

            // 角色名
            var roleObj = MakeText("Role", inner,
                RoleName(rec.Role), ThemeManager.FontSizeHeader, roleColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            Center(roleObj.gameObject, 0f, h * 0.15f, w, 34f);

            // 玩家名
            var nameObj = MakeText("Name", inner,
                rec.Name, ThemeManager.FontSizeBody, TextPrimary, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(nameObj.gameObject, 0f, h * 0.05f, w, 24f);

            // 阵营
            var factionObj = MakeText("Faction", inner,
                FactionName(rec.Faction), ThemeManager.FontSizeSmall, TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(factionObj.gameObject, 0f, h * -0.06f, w, 20f);

            // 存活状态
            string aliveStr = rec.Alive ? "存活" : "已出局";
            Color aliveColor = rec.Alive ? SafeGreen : Hex("#ff6666");
            var aliveObj = MakeText("Alive", inner,
                aliveStr, ThemeManager.FontSizeSmall, aliveColor, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(aliveObj.gameObject, 0f, h * -0.16f, w, 20f);

            // 任务/击杀摘要
            string summary = "";
            if (rec.Role == SocialRole.Gang)
                summary = $"击杀 {rec.Victims}";
            else if (rec.Role == SocialRole.Mole)
                summary = $"情报 {rec.IntelSubmitted}";
            else
                summary = $"任务 {rec.TasksCompleted}/{rec.TotalTasks}";

            var summObj = MakeText("Summary", inner,
                summary, ThemeManager.FontSizeSmall, NeonCyan, FontStyle.Normal, TextAnchor.MiddleCenter);
            Center(summObj.gameObject, 0f, h * -0.26f, w, 20f);
        }

        // ══════════════════════════════════════════════════════
        // 统计数据面板
        // ══════════════════════════════════════════════════════
        private GameObject BuildStatsPanel(List<PlayerRecord> records)
        {
            var panel = new GameObject("Dynamic_StatsPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(_root.transform, false);
            panel.GetComponent<Image>().color = ThemeManager.WithAlpha(PanelBg, 0.92f);
            panel.GetComponent<Image>().raycastTarget = false;

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -280f);
            rt.sizeDelta = new Vector2(800f, records.Count * 64f + 80f);

            // 标题
            var t = MakeText("Title", panel.transform, "统 计 数 据", ThemeManager.FontSizeSubtitle,
                TitleGold, FontStyle.Bold, TextAnchor.MiddleCenter);
            var tRt = t.GetComponent<RectTransform>();
            tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -14f);
            tRt.sizeDelta = new Vector2(600f, 34f);

            // 分割线
            var div = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            div.transform.SetParent(panel.transform, false);
            div.GetComponent<Image>().color = ThemeManager.Divider;
            div.GetComponent<Image>().raycastTarget = false;
            var dRt = div.GetComponent<RectTransform>();
            dRt.anchorMin = dRt.anchorMax = new Vector2(0.5f, 1f);
            dRt.pivot = new Vector2(0.5f, 1f);
            dRt.anchoredPosition = new Vector2(0f, -50f);
            dRt.sizeDelta = new Vector2(700f, 1f);

            // 为每条记录创建进度条行
            float rowH = 58f;
            float rowStartY = -64f;
            for (int i = 0; i < records.Count; i++)
            {
                BuildStatsRow(panel.transform, records[i], i, rowStartY - i * rowH, 700f, rowH);
            }

            return panel;
        }

        private void BuildStatsRow(Transform parent, PlayerRecord rec, int index, float y, float w, float h)
        {
            var row = new GameObject($"Dynamic_StatRow_{index}", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color = index % 2 == 0
                ? ThemeManager.WithAlpha(CardBg, 0.5f) : ThemeManager.CardBackground;
            row.GetComponent<Image>().raycastTarget = false;
            var rRt = row.GetComponent<RectTransform>();
            rRt.anchorMin = rRt.anchorMax = new Vector2(0.5f, 1f);
            rRt.pivot = new Vector2(0.5f, 1f);
            rRt.anchoredPosition = new Vector2(0f, y);
            rRt.sizeDelta = new Vector2(w, h);

            // 名字
            var nameT = MakeText("Name", row.transform, rec.Name.Substring(0, Mathf.Min(rec.Name.Length, 6)),
                ThemeManager.FontSizeSmall, TextPrimary, FontStyle.Normal, TextAnchor.MiddleLeft);
            var nRt = nameT.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0f, 0.5f); nRt.anchorMax = new Vector2(0f, 0.5f);
            nRt.pivot = new Vector2(0f, 0.5f);
            nRt.anchoredPosition = new Vector2(20f, 0f);
            nRt.sizeDelta = new Vector2(100f, 22f);

            // 角色标签
            var roleT = MakeText("Role", row.transform, RoleName(rec.Role),
                ThemeManager.FontSizeFooter, ThemeManager.GetRoleColor(rec.Role), FontStyle.Normal, TextAnchor.MiddleLeft);
            var rlRt = roleT.GetComponent<RectTransform>();
            rlRt.anchorMin = rlRt.anchorMax = new Vector2(0f, 0.5f);
            rlRt.pivot = new Vector2(0f, 0.5f);
            rlRt.anchoredPosition = new Vector2(130f, 0f);
            rlRt.sizeDelta = new Vector2(70f, 18f);

            // 进度条背景
            var barBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(row.transform, false);
            barBg.GetComponent<Image>().color = ThemeManager.InputBackground;
            barBg.GetComponent<Image>().raycastTarget = false;
            var bRt = barBg.GetComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector2(220f, 0f);
            bRt.sizeDelta = new Vector2(440f, 14f);

            // 进度条填充（初始宽度为0，动画撑开）
            var barFill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            barFill.GetComponent<Image>().color = ThemeManager.GetRoleColor(rec.Role);
            barFill.GetComponent<Image>().raycastTarget = false;
            var fRt = barFill.GetComponent<RectTransform>();
            fRt.anchorMin = new Vector2(0f, 0f); fRt.anchorMax = new Vector2(0f, 1f);
            fRt.pivot = new Vector2(0f, 0.5f);
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = new Vector2(0f, 0f);

            // 数值文本
            float progress = GetProgress(rec);
            var valT = MakeText("Value", row.transform,
                GetProgressText(rec), ThemeManager.FontSizeFooter, NeonCyan,
                FontStyle.Normal, TextAnchor.MiddleRight);
            var vRt = valT.GetComponent<RectTransform>();
            vRt.anchorMin = vRt.anchorMax = new Vector2(1f, 0.5f);
            vRt.pivot = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = new Vector2(-20f, 0f);
            vRt.sizeDelta = new Vector2(70f, 18f);
        }

        private IEnumerator AnimateProgressBars(GameObject statsPanel, List<PlayerRecord> records)
        {
            yield return new WaitForSeconds(0.2f);
            Transform panelT = statsPanel.transform;
            float totalDur = ThemeManager.BarFillDuration;
            // 同时启动所有进度条
            float elapsed = 0f;
            while (elapsed < totalDur)
            {
                elapsed += Time.deltaTime;
                float frac = Mathf.Clamp01(elapsed / totalDur);
                for (int i = 0; i < records.Count; i++)
                {
                    var row = panelT.Find($"Dynamic_StatRow_{i}");
                    if (row == null) continue;
                    var barBg = row.Find("BarBg");
                    if (barBg == null) continue;
                    var barFill = barBg.Find("BarFill");
                    if (barFill == null) continue;
                    float target = GetProgress(records[i]);
                    var fRt = barFill.GetComponent<RectTransform>();
                    fRt.anchorMax = new Vector2(Mathf.Lerp(0f, target, frac), 1f);
                }
                yield return null;
            }
        }

        private static float GetProgress(PlayerRecord rec)
        {
            return rec.Role switch
            {
                SocialRole.Gang => Mathf.Clamp01(rec.Victims / 4f),
                SocialRole.Mole => Mathf.Clamp01(rec.IntelSubmitted / 3f),
                _ => rec.TotalTasks > 0 ? Mathf.Clamp01((float)rec.TasksCompleted / rec.TotalTasks) : 0f
            };
        }

        private static string GetProgressText(PlayerRecord rec)
        {
            return rec.Role switch
            {
                SocialRole.Gang => $"{rec.Victims} 击杀",
                SocialRole.Mole => $"{rec.IntelSubmitted} 情报",
                _ => $"{rec.TasksCompleted}/{rec.TotalTasks} 任务"
            };
        }

        // ══════════════════════════════════════════════════════
        // 动画工具
        // ══════════════════════════════════════════════════════
        private IEnumerator AnimateFadeIn(GameObject obj, float duration)
        {
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // ══════════════════════════════════════════════════════
        // 根面板构建
        // ══════════════════════════════════════════════════════
        private void BuildRoot()
        {
            _root = new GameObject("GameOverRoot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _root.transform.SetParent(_canvas.transform, false);
            _root.GetComponent<Image>().color = ThemeManager.OverlayDark;
            _root.GetComponent<Image>().raycastTarget = true;
            Stretch(_root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 粒子效果
            var pObj = new GameObject("GameOverParticles", typeof(RectTransform));
            pObj.transform.SetParent(_root.transform, false);
            Stretch(pObj, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _particles = pObj.AddComponent<UIParticleEffect>();
        }

        // ══════════════════════════════════════════════════════
        // Canvas
        // ══════════════════════════════════════════════════════
        private Canvas GetOrCreateCanvas()
        {
            var existing = FindAnyObjectByType<Canvas>();
            if (existing != null) return existing;
            var go = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            return c;
        }

        // ══════════════════════════════════════════════════════
        // 组件工厂
        // ══════════════════════════════════════════════════════
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

        private static string RoleName(SocialRole r) => r switch
        {
            SocialRole.Gang => "黑帮", SocialRole.Undercover => "卧底",
            SocialRole.Police => "警察", SocialRole.Mole => "线人", _ => "?"
        };

        private static string FactionName(Faction f) => f switch
        {
            Faction.Gang => "黑帮阵营", Faction.Undercover => "卧底阵营",
            Faction.Police => "警察阵营", Faction.Mole => "线人阵营",
            Faction.None => "中立", _ => "?"
        };

        // ══════════════════════════════════════════════════════
        // 淘汰时间线
        // ══════════════════════════════════════════════════════

        private void BuildEliminationTimeline(List<PlayerRecord> records)
        {
            if (_timelineObj != null)
            {
                Destroy(_timelineObj);
                _timelineObj = null;
            }

            var eliminated = new List<PlayerRecord>();
            foreach (var r in records)
                if (!r.Alive)
                    eliminated.Add(r);

            if (eliminated.Count == 0) return;

            _timelineObj = new GameObject("Dynamic_EliminationTimeline",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _timelineObj.transform.SetParent(_root.transform, false);
            _timelineObj.GetComponent<Image>().color = ThemeManager.WithAlpha(PanelBg, 0.85f);
            _timelineObj.GetComponent<Image>().raycastTarget = false;

            var tRt = _timelineObj.GetComponent<RectTransform>();
            tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 0.5f);
            tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.anchoredPosition = new Vector2(0f, -420f);
            float panelW = Mathf.Min(eliminated.Count * 160f + 80f, 780f);
            tRt.sizeDelta = new Vector2(panelW, 90f);

            // 标题
            var title = MakeText("TimelineTitle", _timelineObj.transform,
                "淘汰记录", ThemeManager.FontSizeSmall, DangerRed, FontStyle.Bold, TextAnchor.MiddleCenter);
            var ttRt = title.GetComponent<RectTransform>();
            ttRt.anchorMin = ttRt.anchorMax = new Vector2(0.5f, 1f);
            ttRt.pivot = new Vector2(0.5f, 1f);
            ttRt.anchoredPosition = new Vector2(0f, -10f);
            ttRt.sizeDelta = new Vector2(120f, 20f);

            // 时间线节点
            float startX = -(eliminated.Count - 1) * 80f;
            for (int i = 0; i < eliminated.Count; i++)
            {
                float x = startX + i * 160f;
                BuildTimelineNode(_timelineObj.transform, eliminated[i], x, -45f);
            }
        }

        private void BuildTimelineNode(Transform parent, PlayerRecord rec, float x, float y)
        {
            var node = new GameObject($"Dynamic_TimelineNode_{rec.Name}",
                typeof(RectTransform));
            node.transform.SetParent(parent, false);
            var nRt = node.GetComponent<RectTransform>();
            nRt.anchorMin = nRt.anchorMax = new Vector2(0.5f, 0.5f);
            nRt.pivot = new Vector2(0.5f, 0.5f);
            nRt.anchoredPosition = new Vector2(x, y);
            nRt.sizeDelta = new Vector2(140f, 55f);

            // 红色圆点标记
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(node.transform, false);
            dot.GetComponent<Image>().color = DangerRed;
            dot.GetComponent<Image>().raycastTarget = false;
            {
                var dRt = dot.GetComponent<RectTransform>();
                dRt.anchorMin = dRt.anchorMax = new Vector2(0.5f, 1f);
                dRt.pivot = new Vector2(0.5f, 1f);
                dRt.anchoredPosition = new Vector2(0f, 0f);
                dRt.sizeDelta = new Vector2(12f, 12f);
            }

            // 被淘汰标签
            var eliminatedLabel = MakeText("ElimLabel", node.transform,
                "被淘汰", ThemeManager.FontSizeFooter, DangerRed, FontStyle.Bold, TextAnchor.UpperCenter);
            {
                var eRt = eliminatedLabel.GetComponent<RectTransform>();
                eRt.anchorMin = eRt.anchorMax = new Vector2(0.5f, 1f);
                eRt.pivot = new Vector2(0.5f, 1f);
                eRt.anchoredPosition = new Vector2(0f, -20f);
                eRt.sizeDelta = new Vector2(130f, 16f);
            }

            // 玩家名
            var nameLabel = MakeText("NameLabel", node.transform,
                rec.Name.Length > 6 ? rec.Name.Substring(0, 5) + ".." : rec.Name,
                ThemeManager.FontSizeFooter, TextMuted, FontStyle.Normal, TextAnchor.UpperCenter);
            {
                var nRt2 = nameLabel.GetComponent<RectTransform>();
                nRt2.anchorMin = nRt2.anchorMax = new Vector2(0.5f, 1f);
                nRt2.pivot = new Vector2(0.5f, 1f);
                nRt2.anchoredPosition = new Vector2(0f, -38f);
                nRt2.sizeDelta = new Vector2(130f, 14f);
            }
        }

        // ══════════════════════════════════════════════════════
        // 跳过按钮 + 自动返回主菜单
        // ══════════════════════════════════════════════════════

        private void BuildSkipButton()
        {
            if (_skipButtonObj != null) return;

            _skipButtonObj = new GameObject("Dynamic_SkipButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            _skipButtonObj.transform.SetParent(_root.transform, false);
            _skipButtonObj.GetComponent<Image>().color = ThemeManager.ButtonPrimary;
            _skipButtonObj.GetComponent<Image>().raycastTarget = true;
            _skipButtonObj.GetComponent<Button>().onClick.AddListener(OnSkipClicked);

            var sRt = _skipButtonObj.GetComponent<RectTransform>();
            sRt.anchorMin = sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = new Vector2(0f, -500f);
            sRt.sizeDelta = new Vector2(280f, 48f);

            // 按钮标签
            var label = MakeText("SkipLabel", _skipButtonObj.transform,
                "3秒后返回主菜单（点击跳过）", ThemeManager.FontSizeSmall,
                TextPrimary, FontStyle.Normal, TextAnchor.MiddleCenter);
            var lRt = label.GetComponent<RectTransform>();
            lRt.anchorMin = lRt.anchorMax = new Vector2(0.5f, 0.5f);
            lRt.pivot = new Vector2(0.5f, 0.5f);
            lRt.anchoredPosition = Vector2.zero;
            lRt.sizeDelta = new Vector2(260f, 28f);
        }

        private void UpdateSkipButtonLabel()
        {
            if (_skipButtonObj == null) return;
            var label = _skipButtonObj.transform.Find("SkipLabel");
            if (label == null) return;
            var txt = label.GetComponent<Text>();
            if (txt == null) return;
            int sec = Mathf.CeilToInt(_autoReturnTimer);
            txt.text = $"{sec}秒后返回主菜单（点击跳过）";
        }

        private void OnSkipClicked()
        {
            _autoReturnActive = false;
            ReturnToMenu();
        }

        private void ReturnToMenu()
        {
            Hide();
            if (_bootstrap != null)
            {
                _bootstrap.ReturnToMainMenu();
            }
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
