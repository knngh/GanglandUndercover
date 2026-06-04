using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// UIParticleEffect — 纯代码生成简单粒子效果（圆点 + 运动轨迹）。
    /// 用于背景星空、转场特效等，无需任何美术资源。
    /// 挂载到任意 UI GameObject 上即可生效。
    /// </summary>
    public sealed class UIParticleEffect : MonoBehaviour
    {
        [Header("粒子配置")]
        [SerializeField] private int _particleCount = 120;
        [SerializeField] private float _particleSize = 2.5f;
        [SerializeField] private float _speedMin = 0.05f;
        [SerializeField] private float _speedMax = 0.25f;
        [SerializeField] private float _twinkleSpeed = 1.5f;
        [SerializeField] private Color _particleColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color _particleColorAlt = new Color(0.6f, 0.8f, 1f, 0.5f);
        [SerializeField] private bool _driftEnabled = true;
        [SerializeField] private bool _twinkleEnabled = true;

        private readonly List<ParticleData> _particles = new List<ParticleData>();
        private Canvas _parentCanvas;
        private float _canvasW = 1920f;
        private float _canvasH = 1080f;

        private struct ParticleData
        {
            public GameObject go;
            public Image img;
            public RectTransform rt;
            public Vector2 velocity;
            public float twinklePhase;
            public float twinkleSpeed;
            public float baseAlpha;
            public Color baseColor;
        }

        private void Start()
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas == null)
            {
                // 自身创建 canvas
                _parentCanvas = gameObject.AddComponent<Canvas>();
                _parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                gameObject.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            CanvasScaler scaler = _parentCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                _canvasW = scaler.referenceResolution.x;
                _canvasH = scaler.referenceResolution.y;
            }

            SpawnParticles();
        }

        private void OnDestroy()
        {
            foreach (var p in _particles)
                if (p.go != null) Destroy(p.go);
            _particles.Clear();
        }

        private void SpawnParticles()
        {
            for (int i = 0; i < _particleCount; i++)
            {
                var pd = new ParticleData();

                pd.go = new GameObject("Particle_" + i, typeof(RectTransform), typeof(Image));
                pd.go.transform.SetParent(transform, false);
                pd.go.layer = gameObject.layer;

                pd.img = pd.go.GetComponent<Image>();
                pd.rt = pd.go.GetComponent<RectTransform>();
                pd.img.raycastTarget = false;

                // 随机大小
                float size = _particleSize * Random.Range(0.5f, 1.5f);
                pd.rt.sizeDelta = new Vector2(size, size);

                // 随机颜色
                pd.baseColor = Random.value > 0.6f ? _particleColorAlt : _particleColor;
                pd.baseAlpha = Random.Range(0.25f, 0.9f);
                pd.img.color = new Color(pd.baseColor.r, pd.baseColor.g, pd.baseColor.b, pd.baseAlpha);

                // 随机位置
                pd.rt.anchoredPosition = new Vector2(
                    Random.Range(0f, _canvasW),
                    Random.Range(0f, _canvasH));

                // 随机漂移速度
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(_speedMin, _speedMax);
                pd.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

                // 闪烁参数
                pd.twinklePhase = Random.Range(0f, Mathf.PI * 2f);
                pd.twinkleSpeed = _twinkleSpeed * Random.Range(0.5f, 1.5f);

                _particles.Add(pd);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                if (p.go == null) continue;

                // 漂移
                if (_driftEnabled)
                {
                    Vector2 pos = p.rt.anchoredPosition + p.velocity;
                    // 循环边界
                    if (pos.x < -10f) pos.x = _canvasW + 10f;
                    if (pos.x > _canvasW + 10f) pos.x = -10f;
                    if (pos.y < -10f) pos.y = _canvasH + 10f;
                    if (pos.y > _canvasH + 10f) pos.y = -10f;
                    p.rt.anchoredPosition = pos;
                }

                // 闪烁
                if (_twinkleEnabled)
                {
                    p.twinklePhase += p.twinkleSpeed * dt;
                    float alpha = p.baseAlpha * (0.5f + 0.5f * Mathf.Sin(p.twinklePhase));
                    p.img.color = new Color(
                        p.baseColor.r, p.baseColor.g, p.baseColor.b,
                        Mathf.Clamp01(alpha));
                }

                _particles[i] = p;
            }
        }

        // ─── 公共 API ────────────────────────────────────────

        /// <summary>修改粒子可见性。</summary>
        public void SetVisible(bool visible)
        {
            foreach (var p in _particles)
                if (p.go != null) p.go.SetActive(visible);
        }

        /// <summary>淡入淡出粒子。</summary>
        public System.Collections.IEnumerator FadeParticles(float target, float duration)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                p.baseAlpha = target;
                _particles[i] = p;
            }
            yield return null;
        }
    }
}
