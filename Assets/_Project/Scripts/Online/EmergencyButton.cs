using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// M4.3：紧急按钮组件。挂载到世界中的紧急铃 GameObject 上，
    /// 负责触发紧急会议流程并遵守冷却/次数限制。
    ///
    /// 交互由 OnlineMatchController 通过 TryCallEmergencyMeeting 驱动；
    /// 本组件仅负责世界表现（按钮视觉效果、交互范围可视化）。
    /// </summary>
    public sealed class EmergencyButton : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("按钮实体 SpriteRenderer。")]
        [SerializeField]
        private SpriteRenderer buttonRenderer;

        [Tooltip("可用时颜色。")]
        public Color AvailableColor = new Color(1f, 0.25f, 0.2f, 1f);

        [Tooltip("冷却/不可用时颜色。")]
        public Color CooldownColor = new Color(0.4f, 0.25f, 0.25f, 0.6f);

        [Tooltip("高亮光环 SpriteRenderer。")]
        [SerializeField]
        private SpriteRenderer haloRenderer;

        [Header("Interaction")]
        [Tooltip("交互触发半径（世界单位）。")]
        public float InteractionRadius = 0.85f;

        // ─── 运行时引用 ────────────────────────────────────
        private OnlineMatchController _controller;
        private bool _initialized;

        private void Start()
        {
            if (buttonRenderer == null)
                buttonRenderer = GetComponent<SpriteRenderer>();
            if (haloRenderer == null && transform.childCount > 0)
                haloRenderer = transform.GetChild(0).GetComponent<SpriteRenderer>();
        }

        /// <summary>由 OnlineMatchController 在 EnsureWorld 时注入引用。</summary>
        public void BindController(OnlineMatchController controller)
        {
            _controller = controller;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || _controller == null) return;
            RefreshVisual(_controller.EmergencyMeetingsLeft, _controller.EmergencyCooldownTimer);
        }

        /// <summary>根据剩余次数与冷却更新按钮颜色/光环。</summary>
        private void RefreshVisual(int meetingsLeft, float cooldownTimer)
        {
            bool available = meetingsLeft > 0 && cooldownTimer <= 0f;
            if (buttonRenderer != null)
                buttonRenderer.color = available ? AvailableColor : CooldownColor;
            if (haloRenderer != null)
                haloRenderer.enabled = available;
        }

        /// <summary>尝试触发紧急会议。返回操作结果描述。</summary>
        public string TryCallMeeting(string playerDisplayName)
        {
            if (_controller == null) return "未连接到对局。";
            if (_controller.EmergencyMeetingsLeft <= 0) return "紧急会议次数已用完。";
            if (_controller.EmergencyCooldownTimer > 0f)
                return "紧急会议冷却中：" + Mathf.CeilToInt(_controller.EmergencyCooldownTimer) + "s";

            _controller.CallEmergencyMeeting(playerDisplayName);
            return "已按下紧急铃。";
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.15f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, InteractionRadius);
        }
#endif
    }
}
