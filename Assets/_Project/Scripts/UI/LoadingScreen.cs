using System.Collections;
using System.Collections.Generic;
using GanglandUndercover.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 场景加载画面 — Among Us 风格深色背景 + 旋转加载图标 + 随机提示文字。
    /// 挂载在 LoadingCanvas 预制体上，通过 <see cref="LoadSceneAsync"/> 驱动。
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        // ─── 提示文字池 ─────────────────────────────────────
        private static readonly string[] Hints =
        {
            "卧底就在你们中间……",
            "收集证据，但别暴露自己。",
            "通风管里藏着秘密。",
            "每一次会议都是一场赌博。",
            "黑帮正在暗中破坏。",
            "报告尸体，召集会议。",
            "完成所有任务才能获胜。",
            "信任是一种奢侈品。",
            "有人在暗中观察。",
            "断电之后，危险悄然降临。",
            "卧底的身份一旦暴露，就完了。",
            "每一次投票，都可能改变局势。",
        };

        // ─── 主题色 ───────────────────────────────────────
        private static Color BgDark   => ThemeManager.BackgroundDark;
        private static Color NeonCyan => ThemeManager.NeonCyan;
        private static Color TextPrimary => ThemeManager.TextPrimary;
        private static Color TextMuted  => ThemeManager.TextMuted;

        [Header("引用（自动查找）")]
        [SerializeField] private Image      _backgroundImage;
        [SerializeField] private Image      _spinnerImage;
        [SerializeField] private Text       _hintText;
        [SerializeField] private Text       _progressText;
        [SerializeField] private Slider     _progressBar;

        [Header("配置")]
        [Tooltip("旋转速度（度/秒）")]
        [SerializeField] private float _spinSpeed = 180f;

        [Tooltip("提示文字切换间隔（秒）")]
        [SerializeField] private float _hintIntervalSeconds = 3.5f;

        [Tooltip("进度条平滑速度")]
        [SerializeField] private float _progressSmoothSpeed = 2.5f;

        // ─── 运行时状态 ─────────────────────────────────
        private Canvas   _canvas;
        private float    _currentProgress;
        private float    _targetProgress;
        private int      _hintIndex;
        private float    _hintTimer;
        private Coroutine _loadCoroutine;

        // ══════════════════════════════════════════════════════
        // 公开 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 异步加载目标场景，并显示本加载画面。
        /// 由 <see cref="SceneController"/> 或 Bootstrap 调用。
        /// </summary>
        public void LoadSceneAsync(string sceneName)
        {
            gameObject.SetActive(true);
            _loadCoroutine = StartCoroutine(LoadRoutine(sceneName));
        }

        /// <summary>
        /// 直接设置进度（0–1），用于手动驱动加载进度。
        /// </summary>
        public void SetProgress(float value)
        {
            _targetProgress = Mathf.Clamp01(value);
        }

        /// <summary>
        /// 隐藏加载画面。
        /// </summary>
        public void Dismiss()
        {
            if (_loadCoroutine != null)
            {
                StopCoroutine(_loadCoroutine);
                _loadCoroutine = null;
            }
            gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════════════════
        // Unity 生命周期
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            BuildUI();
            PickRandomHint();
            _hintTimer = 0f;
            _currentProgress = 0f;
            _targetProgress = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // 旋转图标
            if (_spinnerImage != null)
            {
                _spinnerImage.transform.Rotate(0f, 0f, -_spinSpeed * Time.deltaTime, Space.Self);
            }

            // 平滑进度条
            if (Mathf.Abs(_currentProgress - _targetProgress) > 0.001f)
            {
                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress,
                    _progressSmoothSpeed * Time.deltaTime);
                if (_progressBar != null)
                    _progressBar.value = _currentProgress;
                if (_progressText != null)
                    _progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100f)}%";
            }

            // 提示文字轮换
            _hintTimer += Time.deltaTime;
            if (_hintTimer >= _hintIntervalSeconds)
            {
                _hintTimer = 0f;
                PickRandomHint();
            }
        }

        // ══════════════════════════════════════════════════════
        // 加载协程
        // ══════════════════════════════════════════════════════

        private IEnumerator LoadRoutine(string sceneName)
        {
#if UNITY_EDITOR
            // 编辑器模式：模拟加载
            _targetProgress = 0f;
            while (_currentProgress < 0.9f)
            {
                _targetProgress += Time.deltaTime * 0.25f;
                yield return null;
            }
            _targetProgress = 1f;
            yield return new WaitForSeconds(0.3f);
            _currentProgress = 1f;
#else
            // 正式构建：使用 SceneManager
            AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (!op.isDone)
            {
                // 进度 0–0.9 对应加载中，0.9+ 等待激活
                float raw = op.progress;
                _targetProgress = Mathf.Clamp01(raw / 0.9f);

                if (Mathf.Approximately(raw, 0.9f))
                {
                    _targetProgress = 1f;
                    // 等待一小段时间后激活场景
                    yield return new WaitForSeconds(0.25f);
                    op.allowSceneActivation = true;
                }

                yield return null;
            }
#endif
            // 场景切换后隐藏
            yield return new WaitForSeconds(0.2f);
            Dismiss();
        }

        // ══════════════════════════════════════════════════════
        // 提示文字
        // ══════════════════════════════════════════════════════

        private void PickRandomHint()
        {
            int next = _hintIndex;
            while (Hints.Length > 1 && next == _hintIndex)
            {
                next = Random.Range(0, Hints.Length);
            }
            _hintIndex = next;

            if (_hintText != null)
            {
                _hintText.text = Hints[_hintIndex];
            }
        }

        // ══════════════════════════════════════════════════════
        // UI 构建（当引用为空时自动构建）
        // ══════════════════════════════════════════════════════

        private void BuildUI()
        {
            // Canvas
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 9999;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 背景
            if (_backgroundImage == null)
            {
                GameObject bgObj = new GameObject("LoadingBackground", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(transform, false);
                _backgroundImage = bgObj.GetComponent<Image>();
                _backgroundImage.color = BgDark;
                StretchFull(_backgroundImage.rectTransform);
            }

            // 旋转图标（用 Image + 圆形 Sprite 模拟）
            if (_spinnerImage == null)
            {
                GameObject spinObj = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
                spinObj.transform.SetParent(transform, false);
                _spinnerImage = spinObj.GetComponent<Image>();
                _spinnerImage.color = NeonCyan;
                _spinnerImage.sprite = CreateCircleSprite(64, NeonCyan);
                _spinnerImage.raycastTarget = false;

                var rt = spinObj.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot      = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 40f);
                rt.sizeDelta      = new Vector2(72f, 72f);
            }

            // 提示文字
            if (_hintText == null)
            {
                _hintText = CreateText("HintText", transform,
                    Hints[0], ThemeManager.FontSizeBody, TextMuted,
                    FontStyle.Normal, TextAnchor.MiddleCenter);
                var rt = _hintText.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot      = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -60f);
                rt.sizeDelta      = new Vector2(800f, 36f);
            }

            // 进度条
            if (_progressBar == null)
            {
                GameObject pbObj = new GameObject("ProgressBar", typeof(RectTransform), typeof(Slider));
                pbObj.transform.SetParent(transform, false);
                _progressBar = pbObj.GetComponent<Slider>();
                _progressBar.minValue = 0f;
                _progressBar.maxValue = 1f;
                _progressBar.value     = 0f;
                _progressBar.wholeNumbers = false;

                // 背景
                GameObject pbBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                pbBg.transform.SetParent(pbObj.transform, false);
                var pbBgImg = pbBg.GetComponent<Image>();
                pbBgImg.color = ThemeManager.InputBackground;
                StretchFull(pbBg.GetComponent<RectTransform>());

                // 填充
                GameObject pbFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                pbFill.transform.SetParent(pbBg.transform, false);
                var pbFillImg = pbFill.GetComponent<Image>();
                pbFillImg.color = NeonCyan;
                var fillRt = pbFill.GetComponent<RectTransform>();
                fillRt.anchorMin = new Vector2(0f, 0f);
                fillRt.anchorMax = new Vector2(0f, 1f);
                fillRt.pivot      = new Vector2(0f, 0.5f);
                fillRt.offsetMin  = Vector2.zero;
                fillRt.offsetMax  = Vector2.zero;

                _progressBar.fillRect = fillRt;

                var pbRt = pbObj.GetComponent<RectTransform>();
                pbRt.anchorMin = pbRt.anchorMax = new Vector2(0.5f, 0.5f);
                pbRt.pivot      = new Vector2(0.5f, 0.5f);
                pbRt.anchoredPosition = new Vector2(0f, -120f);
                pbRt.sizeDelta      = new Vector2(520f, 10f);
            }

            // 进度百分比文字
            if (_progressText == null)
            {
                _progressText = CreateText("ProgressText", transform,
                    "0%", ThemeManager.FontSizeFooter, TextMuted,
                    FontStyle.Normal, TextAnchor.MiddleCenter);
                var rt = _progressText.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot      = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -150f);
                rt.sizeDelta      = new Vector2(120f, 24f);
            }
        }

        // ══════════════════════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════════════════════

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin  = Vector2.zero;
            rt.offsetMax  = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent,
            string content, int fontSize, Color color,
            FontStyle fontStyle, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var txt = obj.GetComponent<Text>();
            txt.text          = content;
            txt.font          = LoadFont();
            txt.fontSize      = fontSize;
            txt.color         = color;
            txt.fontStyle     = fontStyle;
            txt.alignment    = alignment;
            txt.raycastTarget = false;
            return txt;
        }

        private static Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Sprite CreateCircleSprite(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.45f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float a = d <= radius ? color.a : 0f;
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
