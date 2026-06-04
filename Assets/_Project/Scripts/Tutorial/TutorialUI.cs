using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.Tutorial
{
    /// <summary>
    /// 教程 UI 控制器 — 半透明遮罩、高亮镂空区域、提示气泡和步骤进度指示器。
    ///
    /// 使用四面板拼合方案实现遮罩镂空效果（无需自定义 Shader）：
    ///   顶部面板 + 底部面板 + 左侧面板 + 右侧面板
    ///   围绕高亮目标形成"窗口"效果。
    ///
    /// 听众模式：订阅 TutorialManager 的步骤事件，自动更新 UI。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class TutorialUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════
        // 序列化配置
        // ══════════════════════════════════════════════════════

        [Header("引用")]
        [Tooltip("教程管理器引用。若为空则在场景中自动查找。")]
        [SerializeField] private TutorialManager _manager;

        [Header("遮罩")]
        [Tooltip("遮罩面板颜色。")]
        [SerializeField] private Color _maskColor = new Color(0f, 0f, 0f, 0.72f);

        [Tooltip("高亮区域边框颜色。")]
        [SerializeField] private Color _highlightBorderColor = ThemeRef.NeonCyan;

        [Tooltip("高亮区域边框宽度。")]
        [SerializeField, Range(1f, 8f)]
        private float _highlightBorderWidth = 2.5f;

        [Tooltip("步骤切换时遮罩淡入/淡出时长。")]
        [SerializeField, Range(0.1f, 1f)]
        private float _transitionDuration = 0.25f;

        [Header("提示气泡")]
        [Tooltip("气泡背景颜色。")]
        [SerializeField] private Color _bubbleColor = new Color(0.12f, 0.14f, 0.2f, 0.95f);

        [Tooltip("气泡文字颜色。")]
        [SerializeField] private Color _bubbleTextColor = ThemeRef.TextPrimary;

        [Tooltip("气泡字体大小。")]
        [SerializeField, Range(12, 32)]
        private int _bubbleFontSize = 16;

        [Tooltip("气泡最大宽度（像素）。")]
        [SerializeField, Range(150f, 500f)]
        private float _bubbleMaxWidth = 320f;

        [Header("进度条")]
        [Tooltip("进度圆点颜色（已完成）。")]
        [SerializeField] private Color _dotCompletedColor = ThemeRef.NeonCyan;

        [Tooltip("进度圆点颜色（未完成）。")]
        [SerializeField] private Color _dotPendingColor = new Color(0.3f, 0.32f, 0.4f, 1f);

        [Tooltip("进度圆点颜色（当前）。")]
        [SerializeField] private Color _dotCurrentColor = ThemeRef.TitleGold;

        [Tooltip("进度圆点直径。")]
        [SerializeField, Range(4f, 16f)]
        private float _dotDiameter = 8f;

        [Tooltip("进度圆点间距。")]
        [SerializeField, Range(8f, 32f)]
        private float _dotSpacing = 16f;

        [Header("跳过按钮")]
        [Tooltip("跳过按钮文字。")]
        [SerializeField] private string _skipButtonText = "跳过教程";

        [SerializeField] private Color _skipButtonColor = new Color(0.25f, 0.27f, 0.35f, 0.85f);
        [SerializeField] private Color _skipButtonTextColor = ThemeRef.TextMuted;

        // ══════════════════════════════════════════════════════
        // 运行时状态
        // ══════════════════════════════════════════════════════

        private Canvas _canvas;
        private RectTransform _canvasRect;

        // 遮罩四面板
        private RectTransform _panelTop;
        private RectTransform _panelBottom;
        private RectTransform _panelLeft;
        private RectTransform _panelRight;

        // 高亮边框
        private RectTransform _highlightBorder;

        // 提示气泡
        private RectTransform _bubbleRoot;
        private Text _bubbleText;
        private RectTransform _bubbleArrow;

        // 进度条
        private RectTransform _progressRoot;
        private List<Image> _progressDots = new List<Image>();

        // 跳过按钮
        private RectTransform _skipButtonRoot;
        private Button _skipButton;
        private Text _skipButtonLabel;

        // 动画
        private Coroutine _transitionRoutine;
        private List<Graphic> _allMaskGraphics = new List<Graphic>();

        // 点击检测
        private TutorialStep _activeStep;

        // ══════════════════════════════════════════════════════
        // 生命周期
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 900; // 高于所有游戏 UI

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            _canvasRect = GetComponent<RectTransform>();
        }

        private void Start()
        {
            BuildUI();

            if (_manager == null)
            {
                _manager = FindAnyObjectByType<TutorialManager>();
            }

            if (_manager != null)
            {
                _manager.OnStepEntered.AddListener(OnStepEntered);
                _manager.OnStepExited.AddListener(OnStepExited);
                _manager.OnTutorialCompleted.AddListener(OnTutorialFinished);
                _manager.OnTutorialSkipped.AddListener(OnTutorialFinished);
            }
            else
            {
                Debug.LogWarning("[TutorialUI] 未找到 TutorialManager，UI 将不会响应教程事件。");
            }

            // 初始隐藏
            SetAllVisible(false);
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.OnStepEntered.RemoveListener(OnStepEntered);
                _manager.OnStepExited.RemoveListener(OnStepExited);
                _manager.OnTutorialCompleted.RemoveListener(OnTutorialFinished);
                _manager.OnTutorialSkipped.RemoveListener(OnTutorialFinished);
            }

            if (_skipButton != null)
            {
                _skipButton.onClick.RemoveListener(OnSkipButtonClicked);
            }
        }

        private void Update()
        {
            // 检测点击：当步骤为 WaitForClick 时
            if (_activeStep != null &&
                _activeStep.WaitCondition == TutorialWaitCondition.WaitForClick &&
                Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }
        }

        // ══════════════════════════════════════════════════════
        // UI 构建
        // ══════════════════════════════════════════════════════

        private void BuildUI()
        {
            BuildMaskPanels();
            BuildHighlightBorder();
            BuildBubble();
            BuildProgressBar();
            BuildSkipButton();
        }

        private void BuildMaskPanels()
        {
            _panelTop = CreatePanel("Panel_Top", _maskColor);
            _panelBottom = CreatePanel("Panel_Bottom", _maskColor);
            _panelLeft = CreatePanel("Panel_Left", _maskColor);
            _panelRight = CreatePanel("Panel_Right", _maskColor);

            _allMaskGraphics.AddRange(new Graphic[]
            {
                _panelTop.GetComponent<Image>(),
                _panelBottom.GetComponent<Image>(),
                _panelLeft.GetComponent<Image>(),
                _panelRight.GetComponent<Image>()
            });
        }

        private void BuildHighlightBorder()
        {
            GameObject borderObj = new GameObject("Border_Highlight", typeof(RectTransform));
            borderObj.transform.SetParent(transform, false);
            _highlightBorder = borderObj.GetComponent<RectTransform>();

            // 使用四个细条组成边框
            CreateBorderEdge(_highlightBorder, "Edge_Top",    Vector2.zero,       new Vector2(1f, 0f));
            CreateBorderEdge(_highlightBorder, "Edge_Bottom", Vector2.zero,       new Vector2(1f, 1f));
            CreateBorderEdge(_highlightBorder, "Edge_Left",   Vector2.zero,       new Vector2(0f, 0f));
            CreateBorderEdge(_highlightBorder, "Edge_Right",  Vector2.one,        new Vector2(0f, 1f));
        }

        private void CreateBorderEdge(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(parent, false);
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = Vector2.zero;

            Image img = edge.GetComponent<Image>();
            img.color = _highlightBorderColor;
            img.raycastTarget = false;

            _allMaskGraphics.Add(img);
        }

        private void BuildBubble()
        {
            GameObject root = new GameObject("Bubble", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _bubbleRoot = root.GetComponent<RectTransform>();
            _bubbleRoot.pivot = new Vector2(0.5f, 0.5f);

            // 气泡背景
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_bubbleRoot, false);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImg = bg.GetComponent<Image>();
            bgImg.color = _bubbleColor;
            bgImg.raycastTarget = false;
            _allMaskGraphics.Add(bgImg);

            // 气泡文字
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(_bubbleRoot, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 10f);
            textRect.offsetMax = new Vector2(-16f, -10f);

            _bubbleText = textObj.GetComponent<Text>();
            _bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bubbleText.fontSize = _bubbleFontSize;
            _bubbleText.color = _bubbleTextColor;
            _bubbleText.alignment = TextAnchor.MiddleCenter;
            _bubbleText.raycastTarget = false;

            // 箭头
            GameObject arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrow.transform.SetParent(_bubbleRoot, false);
            _bubbleArrow = arrow.GetComponent<RectTransform>();
            _bubbleArrow.sizeDelta = new Vector2(14f, 7f);
            _bubbleArrow.pivot = new Vector2(0.5f, 0.5f);

            Image arrowImg = arrow.GetComponent<Image>();
            arrowImg.color = _bubbleColor;
            arrowImg.raycastTarget = false;
            _allMaskGraphics.Add(arrowImg);
        }

        private void BuildProgressBar()
        {
            GameObject root = new GameObject("ProgressBar", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _progressRoot = root.GetComponent<RectTransform>();
            _progressRoot.anchorMin = new Vector2(0.5f, 0f);
            _progressRoot.anchorMax = new Vector2(0.5f, 0f);
            _progressRoot.pivot = new Vector2(0.5f, 0f);
            _progressRoot.anchoredPosition = new Vector2(0f, 60f);
            _progressRoot.sizeDelta = new Vector2(400f, _dotDiameter + 16f);
        }

        private void BuildSkipButton()
        {
            GameObject root = new GameObject("SkipButton", typeof(RectTransform),
                typeof(Image), typeof(Button));
            root.transform.SetParent(transform, false);
            _skipButtonRoot = root.GetComponent<RectTransform>();
            _skipButtonRoot.anchorMin = new Vector2(1f, 1f);
            _skipButtonRoot.anchorMax = new Vector2(1f, 1f);
            _skipButtonRoot.pivot = new Vector2(1f, 1f);
            _skipButtonRoot.anchoredPosition = new Vector2(-40f, -40f);
            _skipButtonRoot.sizeDelta = new Vector2(120f, 36f);

            Image btnImg = root.GetComponent<Image>();
            btnImg.color = _skipButtonColor;
            _allMaskGraphics.Add(btnImg);

            _skipButton = root.GetComponent<Button>();
            _skipButton.onClick.AddListener(OnSkipButtonClicked);

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(root.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            _skipButtonLabel = labelObj.GetComponent<Text>();
            _skipButtonLabel.text = _skipButtonText;
            _skipButtonLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _skipButtonLabel.fontSize = 14;
            _skipButtonLabel.color = _skipButtonTextColor;
            _skipButtonLabel.alignment = TextAnchor.MiddleCenter;
            _skipButtonLabel.raycastTarget = false;
        }

        private RectTransform CreatePanel(string name, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(transform, false);

            Image img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true; // 遮罩面板阻挡点击穿透

            return obj.GetComponent<RectTransform>();
        }

        // ══════════════════════════════════════════════════════
        // 事件处理
        // ══════════════════════════════════════════════════════

        private void OnStepEntered(int stepIndex, TutorialStep step)
        {
            _activeStep = step;

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
            }

            _transitionRoutine = StartCoroutine(TransitionToStep(stepIndex, step));
        }

        private void OnStepExited(int stepIndex)
        {
            _activeStep = null;
        }

        private void OnTutorialFinished()
        {
            _activeStep = null;

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            SetAllVisible(false);
        }

        private void OnSkipButtonClicked()
        {
            if (_manager != null)
            {
                _manager.SkipAll();
            }
        }

        // ══════════════════════════════════════════════════════
        // 步骤过渡动画
        // ══════════════════════════════════════════════════════

        private IEnumerator TransitionToStep(int stepIndex, TutorialStep step)
        {
            // 淡出当前遮罩
            yield return StartCoroutine(FadeMask(0f, _transitionDuration * 0.5f));

            // 更新布局
            UpdateMaskLayout(step);
            UpdateBubble(step);
            UpdateProgressBar(stepIndex);

            // 淡入新遮罩
            yield return StartCoroutine(FadeMask(1f, _transitionDuration * 0.5f));
        }

        private IEnumerator FadeMask(float targetAlpha, float duration)
        {
            if (duration <= 0f)
            {
                SetMaskAlpha(targetAlpha);
                SetAllVisible(targetAlpha > 0f);
                yield break;
            }

            float startAlpha = _allMaskGraphics.Count > 0
                ? _allMaskGraphics[0].color.a
                : 0f;

            SetAllVisible(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                SetMaskAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
                yield return null;
            }

            SetMaskAlpha(targetAlpha);
            if (targetAlpha <= 0f)
            {
                SetAllVisible(false);
            }
        }

        private void SetMaskAlpha(float alpha)
        {
            Color c = _maskColor;
            c.a = _maskColor.a * alpha;
            foreach (Graphic g in _allMaskGraphics)
            {
                // 跳过边框和气泡（它们有自己的颜色逻辑）
                if (g.transform.parent == _highlightBorder) continue;
                if (g.transform.IsChildOf(_bubbleRoot)) continue;
                c = g.color;
                c.a = _maskColor.a * alpha;
                g.color = c;
            }
        }

        private void SetAllVisible(bool visible)
        {
            _panelTop.gameObject.SetActive(visible);
            _panelBottom.gameObject.SetActive(visible);
            _panelLeft.gameObject.SetActive(visible);
            _panelRight.gameObject.SetActive(visible);
            _highlightBorder.gameObject.SetActive(visible);
            _bubbleRoot.gameObject.SetActive(visible);
            _progressRoot.gameObject.SetActive(visible);
            _skipButtonRoot.gameObject.SetActive(visible);
        }

        // ══════════════════════════════════════════════════════
        // 遮罩布局 — 四面板拼合镂空
        // ══════════════════════════════════════════════════════

        private void UpdateMaskLayout(TutorialStep step)
        {
            // 获取高亮目标在 Canvas 空间中的矩形
            Rect highlightRect = GetHighlightRect(step);

            // Canvas 全屏尺寸
            Vector2 canvasSize = _canvasRect.rect.size;

            // ── 顶部面板：从顶部到高亮区域上边缘 ──
            SetPanelRect(_panelTop,
                new Vector2(0f, highlightRect.yMax),
                new Vector2(canvasSize.x, canvasSize.y));

            // ── 底部面板：从高亮区域下边缘到底部 ──
            SetPanelRect(_panelBottom,
                new Vector2(0f, 0f),
                new Vector2(canvasSize.x, highlightRect.yMin));

            // ── 左侧面板：在高亮区域左侧的垂直条 ──
            SetPanelRect(_panelLeft,
                new Vector2(0f, highlightRect.yMin),
                new Vector2(highlightRect.xMin, highlightRect.yMax));

            // ── 右侧面板：在高亮区域右侧的垂直条 ──
            SetPanelRect(_panelRight,
                new Vector2(highlightRect.xMax, highlightRect.yMin),
                new Vector2(canvasSize.x, highlightRect.yMax));

            // ── 高亮边框 ──
            _highlightBorder.anchoredPosition = highlightRect.position;
            _highlightBorder.sizeDelta = highlightRect.size;
            UpdateBorderEdges(highlightRect.size);
        }

        private Rect GetHighlightRect(TutorialStep step)
        {
            float padding = step.HighlightPadding;
            Vector2 canvasSize = _canvasRect.rect.size;

            if (step.HighlightTarget != null)
            {
                // 将目标 RectTransform 的世界坐标转换为 Canvas 局部坐标
                RectTransform target = step.HighlightTarget;
                Vector3[] corners = new Vector3[4];
                target.GetWorldCorners(corners);

                // 转换到 Canvas 局部空间
                Vector2 min = _canvasRect.InverseTransformPoint(corners[0]);
                Vector2 max = _canvasRect.InverseTransformPoint(corners[2]);

                // 转为 Canvas 空间（以左下角为原点）
                Vector2 canvasHalf = canvasSize * 0.5f;
                min += canvasHalf;
                max += canvasHalf;

                min.x -= padding;
                min.y -= padding;
                max.x += padding;
                max.y += padding;

                // 钳制到屏幕内
                min.x = Mathf.Max(min.x, 20f);
                min.y = Mathf.Max(min.y, 20f);
                max.x = Mathf.Min(max.x, canvasSize.x - 20f);
                max.y = Mathf.Min(max.y, canvasSize.y - 20f);

                return new Rect(min, max - min);
            }

            // 默认：屏幕中央 300x200 区域
            float defaultW = 300f + padding * 2f;
            float defaultH = 200f + padding * 2f;
            float cx = (canvasSize.x - defaultW) * 0.5f;
            float cy = (canvasSize.y - defaultH) * 0.5f;
            return new Rect(cx, cy, defaultW, defaultH);
        }

        private void SetPanelRect(RectTransform panel, Vector2 min, Vector2 max)
        {
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;

            Vector2 pos = min;
            Vector2 size = new Vector2(
                Mathf.Max(max.x - min.x, 0f),
                Mathf.Max(max.y - min.y, 0f));

            panel.anchoredPosition = pos;
            panel.sizeDelta = size;
        }

        private void UpdateBorderEdges(Vector2 size)
        {
            // 四条边框 edge 的 anchor 已设为覆盖全矩形
            // 更新 sizeDelta 来实现边框宽度
            foreach (Transform child in _highlightBorder)
            {
                RectTransform edge = child as RectTransform;
                if (edge == null) continue;

                // 重新设置四条边的大小
                if (edge.name == "Edge_Top" || edge.name == "Edge_Bottom")
                {
                    edge.sizeDelta = new Vector2(0f, _highlightBorderWidth);
                    edge.anchoredPosition = edge.name == "Edge_Top"
                        ? new Vector2(0f, size.y - _highlightBorderWidth)
                        : Vector2.zero;
                }
                else // Edge_Left / Edge_Right
                {
                    edge.sizeDelta = new Vector2(_highlightBorderWidth, 0f);
                    edge.anchoredPosition = edge.name == "Edge_Right"
                        ? new Vector2(size.x - _highlightBorderWidth, 0f)
                        : Vector2.zero;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 提示气泡布局
        // ══════════════════════════════════════════════════════

        private void UpdateBubble(TutorialStep step)
        {
            _bubbleText.text = step.TipText;

            // 计算气泡大小
            float textWidth = Mathf.Min(_bubbleText.preferredWidth + 32f, _bubbleMaxWidth);
            float textHeight = _bubbleText.preferredHeight + 20f;
            _bubbleRoot.sizeDelta = new Vector2(textWidth, textHeight);

            // 获取高亮区域
            Rect highlightRect = GetHighlightRect(step);
            Vector2 canvasSize = _canvasRect.rect.size;

            // 将局部坐标转换为以 Canvas 中心为原点的标准 anchoredPosition
            Vector2 highlightCenter = new Vector2(
                highlightRect.center.x - canvasSize.x * 0.5f,
                highlightRect.center.y - canvasSize.y * 0.5f);

            // 气泡位置 = 高亮中心 + 偏移
            Vector2 bubblePos = highlightCenter + step.TipOffset;
            _bubbleRoot.anchoredPosition = bubblePos;

            // 箭头位置
            float arrowOffsetX = 0f;
            float arrowOffsetY = 0f;
            switch (step.TipArrow)
            {
                case TipArrowDirection.Top:
                    arrowOffsetY = textHeight * 0.5f + 4f;
                    _bubbleArrow.rotation = Quaternion.Euler(0f, 0f, 0f);
                    break;
                case TipArrowDirection.Bottom:
                    arrowOffsetY = -textHeight * 0.5f - 4f;
                    _bubbleArrow.rotation = Quaternion.Euler(0f, 0f, 180f);
                    break;
                case TipArrowDirection.Left:
                    arrowOffsetX = -textWidth * 0.5f - 4f;
                    _bubbleArrow.rotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case TipArrowDirection.Right:
                    arrowOffsetX = textWidth * 0.5f + 4f;
                    _bubbleArrow.rotation = Quaternion.Euler(0f, 0f, -90f);
                    break;
            }
            _bubbleArrow.anchoredPosition = new Vector2(arrowOffsetX, arrowOffsetY);
        }

        // ══════════════════════════════════════════════════════
        // 进度条
        // ══════════════════════════════════════════════════════

        private void UpdateProgressBar(int currentIndex)
        {
            int total = _manager != null ? _manager.TotalSteps : 0;
            if (total <= 0)
            {
                _progressRoot.gameObject.SetActive(false);
                return;
            }

            _progressRoot.gameObject.SetActive(true);

            // 清除旧圆点
            foreach (Image dot in _progressDots)
            {
                if (dot != null) Destroy(dot.gameObject);
            }
            _progressDots.Clear();

            // 创建新圆点
            float totalWidth = total * _dotDiameter + (total - 1) * _dotSpacing;
            float startX = -totalWidth * 0.5f + _dotDiameter * 0.5f;

            for (int i = 0; i < total; i++)
            {
                GameObject dotObj = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image));
                dotObj.transform.SetParent(_progressRoot, false);
                RectTransform dotRect = dotObj.GetComponent<RectTransform>();
                dotRect.sizeDelta = new Vector2(_dotDiameter, _dotDiameter);
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = new Vector2(startX + i * (_dotDiameter + _dotSpacing), 0f);

                Image dotImg = dotObj.GetComponent<Image>();
                dotImg.raycastTarget = false;

                if (i < currentIndex)
                {
                    dotImg.color = _dotCompletedColor;
                }
                else if (i == currentIndex)
                {
                    dotImg.color = _dotCurrentColor;
                    // 当前圆点稍大
                    dotRect.sizeDelta = new Vector2(_dotDiameter * 1.5f, _dotDiameter * 1.5f);
                }
                else
                {
                    dotImg.color = _dotPendingColor;
                }

                _progressDots.Add(dotImg);
            }
        }

        // ══════════════════════════════════════════════════════
        // 点击处理
        // ══════════════════════════════════════════════════════

        private void HandleClick()
        {
            if (_manager == null || _activeStep == null) return;

            TutorialStep step = _activeStep;

            // 如果有指定点击目标，检查是否点击在目标上
            if (step.ClickTarget != null)
            {
                if (!IsPointerOverRect(step.ClickTarget))
                {
                    return; // 点击不在目标区域，忽略
                }
            }
            else
            {
                // 无指定目标，检查是否点击在高亮区域内
                if (!IsPointerOverHighlight(step))
                {
                    // 点击在遮罩区域但不在高亮区域 — 可呈现提示反馈
                    return;
                }
            }

            _manager.Advance();
        }

        private bool IsPointerOverRect(RectTransform target)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(
                target, Input.mousePosition, _canvas.worldCamera);
        }

        private bool IsPointerOverHighlight(TutorialStep step)
        {
            Rect highlightRect = GetHighlightRect(step);
            Vector2 canvasSize = _canvasRect.rect.size;

            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, Input.mousePosition, _canvas.worldCamera, out mousePos);

            // Convert from center-anchored to bottom-left origin
            mousePos += canvasSize * 0.5f;

            return highlightRect.Contains(mousePos);
        }

        // ══════════════════════════════════════════════════════
        // 静态颜色辅助类
        // ══════════════════════════════════════════════════════

        private static class ThemeRef
        {
            public static readonly Color NeonCyan   = new Color(0.102f, 0.933f, 1f,     1f);
            public static readonly Color TitleGold   = new Color(0.941f, 0.902f, 0.549f, 1f);
            public static readonly Color TextPrimary = new Color(0.961f, 0.949f, 0.922f, 1f);
            public static readonly Color TextMuted   = new Color(0.478f, 0.478f, 0.541f, 1f);
        }
    }
}
