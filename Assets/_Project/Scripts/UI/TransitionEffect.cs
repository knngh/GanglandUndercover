using System;
using System.Collections;
using GanglandUndercover.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// Among Us 风格场景切换全屏过渡控制器。
    /// 支持黑幕淡入淡出和圆形开合扩散效果。
    /// </summary>
    public sealed class TransitionEffect : MonoBehaviour
    {
        private enum TransitionType
        {
            None,
            FadeToBlack,
            FadeFromBlack,
            CircleOpen,           // 从中心扩散打开
            CircleClose            // 向中心收缩关闭
        }

        // ─── 实例化引用 ────────────────────────────────
        [Header("组件引用")]
        [SerializeField] private Image  _overlayImage;
        [SerializeField] private Canvas _canvas;

        // ─── 配置 ─────────────────────────────────────
        [Header("动画参数")]
        [Tooltip("淡入/淡出默认时长（秒）")]
        [SerializeField] private float _defaultDuration = 0.5f;

        [Tooltip("圆形扩散时覆盖屏宽最远角距离")]
        [SerializeField] private float _circleMaxRadius = 1800f;

        // ─── 运行时状态 ──────────────────────────────
        private Material     _circleMaterial;
        private RectTransform _overlayRect;
        private float         _transitionProgress;   // 0–1
        private TransitionType _currentType = TransitionType.None;
        private Action        _onComplete;
        private Coroutine     _transitionCoroutine;

        // Shader 属性 ID
        private static readonly int ShaderProgress  = Shader.PropertyToID("_Progress");
        private static readonly int ShaderColor     = Shader.PropertyToID("_Color");
        private static readonly int ShaderCircleMax = Shader.PropertyToID("_CircleMaxRadius");

        private const string CircleShaderName = "GanglandUndercover/UI/CircleTransition";

        // ══════════════════════════════════════════════════════
        // 公开 API
        // ══════════════════════════════════════════════════════

        /// <summary>黑幕淡入（逐渐遮黑）</summary>
        public void FadeToBlack(float duration = -1f, Action onComplete = null)
        {
            RunTransition(TransitionType.FadeToBlack,
                duration > 0f ? duration : _defaultDuration, onComplete);
        }

        /// <summary>黑幕淡出（逐渐透明）</summary>
        public void FadeFromBlack(float duration = -1f, Action onComplete = null)
        {
            RunTransition(TransitionType.FadeFromBlack,
                duration > 0f ? duration : _defaultDuration, onComplete);
        }

        /// <summary>圆形扩散打开 — 从中心扩散到全屏</summary>
        public void CircleOpen(float duration = -1f, Action onComplete = null)
        {
            RunTransition(TransitionType.CircleOpen,
                duration > 0f ? duration : _defaultDuration, onComplete);
        }

        /// <summary>圆形收缩关闭 — 从全屏收缩到中心</summary>
        public void CircleClose(float duration = -1f, Action onComplete = null)
        {
            RunTransition(TransitionType.CircleClose,
                duration > 0f ? duration : _defaultDuration, onComplete);
        }

        /// <summary>场景切换标准流程：先闭黑幕（CircleClose）→ 加载目标 → 再开黑幕（CircleOpen）</summary>
        public Coroutine SwitchScene(string targetSceneName,
            Action loadAction,
            float closeDuration = -1f,
            float openDuration = -1f)
        {
            return StartCoroutine(SwitchSceneRoutine(targetSceneName, loadAction,
                closeDuration > 0f ? closeDuration : _defaultDuration,
                openDuration  > 0f ? openDuration  : _defaultDuration));
        }

        /// <summary>停止当前过渡并立即完成</summary>
        public void CompleteImmediately()
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            _currentType = TransitionType.None;
            if (_overlayImage != null) _overlayImage.color = Color.clear;
            if (_circleMaterial != null) _circleMaterial.SetFloat(ShaderProgress, 0f);
            _onComplete?.Invoke();
            _onComplete = null;
        }

        /// <summary>是否正在过渡</summary>
        public bool IsTransitioning => _currentType != TransitionType.None;

        // ══════════════════════════════════════════════════════
        // Unity 生命周期
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            BuildUI();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_currentType == TransitionType.None) return;
        }

        // ══════════════════════════════════════════════════════
        // 过渡执行
        // ══════════════════════════════════════════════════════

        private void RunTransition(TransitionType type, float duration, Action onComplete)
        {
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            _onComplete = onComplete;
            _currentType = type;
            gameObject.SetActive(true);

            _transitionCoroutine = StartCoroutine(TransitionRoutine(type, duration));
        }

        private IEnumerator TransitionRoutine(TransitionType type, float duration)
        {
            bool isCircle = type == TransitionType.CircleOpen || type == TransitionType.CircleClose;

            if (isCircle)
            {
                // 切换到圆形材质模式
                EnsureCircleMaterial();
                _overlayImage.material = _circleMaterial;
            }
            else
            {
                _overlayImage.material = null;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 使用 ease-in-out
                float eased = EaseInOutQuad(t);

                ApplyProgress(type, eased);
                yield return null;
            }

            // 确保到达终态
            ApplyProgress(type, 1f);

            _currentType = TransitionType.None;
            if (type == TransitionType.FadeFromBlack || type == TransitionType.CircleOpen)
            {
                // 过渡完全结束，隐藏overlay
                if (_overlayImage != null) _overlayImage.color = Color.clear;
                gameObject.SetActive(false);
            }

            _onComplete?.Invoke();
            _onComplete = null;
            _transitionCoroutine = null;
        }

        private void ApplyProgress(TransitionType type, float t)
        {
            switch (type)
            {
                case TransitionType.FadeToBlack:
                    _overlayImage.color = new Color(0f, 0f, 0f, t);
                    break;

                case TransitionType.FadeFromBlack:
                    _overlayImage.color = new Color(0f, 0f, 0f, 1f - t);
                    break;

                case TransitionType.CircleOpen:
                    // 从 1（全黑）扩散到 0（全透明）
                    // 在shader中：progress=1 是圆形半径=0（收缩到中心），progress=0 是完全扩散开
                    _circleMaterial.SetFloat(ShaderProgress, 1f - t);
                    _overlayImage.color = Color.black;  // 保持全黑底色
                    break;

                case TransitionType.CircleClose:
                    // 从 0（扩散开）收缩到 1（中心无）
                    _circleMaterial.SetFloat(ShaderProgress, t);
                    _overlayImage.color = Color.black;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 场景切换组合流程
        // ══════════════════════════════════════════════════════

        private IEnumerator SwitchSceneRoutine(string targetSceneName,
            Action loadAction, float closeDuration, float openDuration)
        {
            // Step 1：收缩关闭
            bool closeDone = false;
            CircleClose(closeDuration, () => closeDone = true);
            yield return new WaitUntil(() => closeDone);

            // Step 2：加载目标（由调用方控制）
            loadAction?.Invoke();

            // 等待至少 1 帧让加载开始
            yield return null;

#if !UNITY_EDITOR
            // 等待场景加载
            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetSceneName);
            if (op != null)
            {
                op.allowSceneActivation = true;
                while (!op.isDone) yield return null;
            }
#endif

            // Step 3：扩散打开
            bool openDone = false;
            CircleOpen(openDuration, () => openDone = true);
            yield return new WaitUntil(() => openDone);
        }

        // ══════════════════════════════════════════════════════
        // 缓动函数
        // ══════════════════════════════════════════════════════

        private static float EaseInOutQuad(float t)
        {
            return t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        // ══════════════════════════════════════════════════════
        // UI 构建
        // ══════════════════════════════════════════════════════

        private void BuildUI()
        {
            // Canvas
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 9998;
                _canvas.planeDistance = 0f;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 全屏黑色遮罩
            if (_overlayImage == null)
            {
                var imgObj = new GameObject("TransitionOverlay", typeof(RectTransform), typeof(Image));
                imgObj.transform.SetParent(transform, false);
                _overlayImage = imgObj.GetComponent<Image>();
                _overlayImage.color = Color.clear;
                _overlayImage.raycastTarget = true;  // 阻挡点击穿透
                _overlayImage.sprite = CreateWhitePixelSprite();
                _overlayImage.type = Image.Type.Simple;

                _overlayRect = imgObj.GetComponent<RectTransform>();
                _overlayRect.anchorMin = Vector2.zero;
                _overlayRect.anchorMax = Vector2.one;
                _overlayRect.offsetMin  = Vector2.zero;
                _overlayRect.offsetMax  = Vector2.zero;
            }

            // 圆形 Shader
            EnsureCircleMaterial();
        }

        private void EnsureCircleMaterial()
        {
            if (_circleMaterial != null) return;

            var shader = Shader.Find(CircleShaderName);
            if (shader == null)
            {
                // 回退到内置 unlit
                shader = Shader.Find("UI/Default");
            }

            _circleMaterial = new Material(shader);
            _circleMaterial.SetFloat(ShaderCircleMax, _circleMaxRadius);
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
    }
}
