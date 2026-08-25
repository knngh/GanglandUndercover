using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Short-lived runtime feedback for profession abilities.
    /// The visual is intentionally self-contained so it can be spawned by the
    /// host and cleaned up without adding state to the match snapshot.
    /// </summary>
    public sealed class AbilityFeedbackVfx : MonoBehaviour
    {
        private SpriteRenderer[] _renderers;
        private Vector3 _initialScale;
        private float _duration;
        private float _elapsed;

        public void Configure(float duration)
        {
            _duration = Mathf.Max(0.2f, duration);
            _elapsed = 0f;
            _initialScale = transform.localScale;
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void Update()
        {
            if (_renderers == null)
            {
                Configure(1.1f);
            }

            _elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(_elapsed / _duration);
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            transform.localScale = _initialScale * (0.72f + pulse * 0.5f);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                Color color = _renderers[i].color;
                color.a *= 1f - normalized;
                _renderers[i].color = color;
            }

            if (normalized >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
