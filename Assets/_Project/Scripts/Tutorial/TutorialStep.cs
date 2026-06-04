using UnityEngine;

namespace GanglandUndercover.Tutorial
{
    /// <summary>
    /// 教程步骤等待条件类型。
    /// </summary>
    public enum TutorialWaitCondition
    {
        /// <summary>自动推进（延迟后自动进入下一步）</summary>
        AutoAdvance,

        /// <summary>等待玩家点击高亮区域</summary>
        WaitForClick,

        /// <summary>等待玩家移动角色</summary>
        WaitForMove,

        /// <summary>等待玩家接取/完成任务</summary>
        WaitForTask,

        /// <summary>等待玩家提交报告</summary>
        WaitForReport,

        /// <summary>等待玩家参与投票</summary>
        WaitForVote,

        /// <summary>等待玩家完成小游戏</summary>
        WaitForMinigame,

        /// <summary>等待玩家发送/接收聊天消息</summary>
        WaitForChat,

        /// <summary>等待指定时间后自动推进</summary>
        WaitForTime,

        /// <summary>等待外部脚本手动调用 Advance()</summary>
        Manual
    }

    /// <summary>
    /// 教程步骤定义 — ScriptableObject 数据载体。
    /// 每个步骤描述一个高亮区域、提示文字和完成条件。
    /// 在编辑器中通过 Create Asset Menu 创建步骤资源，
    /// 然后在 TutorialManager 的 steps 列表中按顺序引用。
    /// </summary>
    [CreateAssetMenu(
        fileName = "TutorialStep_New",
        menuName = "GanglandUndercover/Tutorial/Tutorial Step",
        order = 100)]
    public sealed class TutorialStep : ScriptableObject
    {
        // ══════════════════════════════════════════════════════
        // 基本信息
        // ══════════════════════════════════════════════════════

        [Header("基本信息")]
        [Tooltip("步骤唯一标识，用于事件分发和日志。")]
        [SerializeField] private string _stepId = "step_001";

        [Tooltip("步骤显示名称，用于进度条提示。")]
        [SerializeField] private string _stepName = "欢迎";

        [Tooltip("步骤描述（编辑器注释，非运行时显示）。")]
        [SerializeField, TextArea(2, 4)]
        private string _description = string.Empty;

        // ══════════════════════════════════════════════════════
        // 高亮与提示
        // ══════════════════════════════════════════════════════

        [Header("高亮与提示")]
        [Tooltip("需要高亮的 UI RectTransform。若为空则高亮屏幕中央区域。")]
        [SerializeField] private RectTransform _highlightTarget;

        [Tooltip("高亮区域额外内边距（像素），扩大镂空范围。")]
        [SerializeField, Range(0f, 60f)]
        private float _highlightPadding = 12f;

        [Tooltip("提示文字，显示在高亮区域旁边的气泡中。")]
        [SerializeField, TextArea(2, 5)]
        private string _tipText = "请点击此处继续。";

        [Tooltip("提示气泡相对于高亮区域的位置偏移。")]
        [SerializeField] private Vector2 _tipOffset = new Vector2(0f, 120f);

        [Tooltip("提示气泡箭头方向。")]
        [SerializeField] private TipArrowDirection _tipArrow = TipArrowDirection.Bottom;

        // ══════════════════════════════════════════════════════
        // 完成条件
        // ══════════════════════════════════════════════════════

        [Header("完成条件")]
        [Tooltip("步骤完成条件类型。")]
        [SerializeField] private TutorialWaitCondition _waitCondition = TutorialWaitCondition.WaitForClick;

        [Tooltip("当条件为 WaitForClick 时，需要点击的目标 UI。若为空则点击任意位置即可推进。")]
        [SerializeField] private RectTransform _clickTarget;

        [Tooltip("自动推进前的等待秒数（WaitForTime / AutoAdvance 时生效）。")]
        [SerializeField, Range(0.5f, 30f)]
        private float _autoDelay = 2.5f;

        [Tooltip("是否允许跳过此步骤（SkipStep）。")]
        [SerializeField] private bool _skippable = true;

        // ══════════════════════════════════════════════════════
        // 公共属性
        // ══════════════════════════════════════════════════════

        public string StepId => _stepId;
        public string StepName => _stepName;
        public string Description => _description;
        public RectTransform HighlightTarget => _highlightTarget;
        public float HighlightPadding => _highlightPadding;
        public string TipText => _tipText;
        public Vector2 TipOffset => _tipOffset;
        public TipArrowDirection TipArrow => _tipArrow;
        public TutorialWaitCondition WaitCondition => _waitCondition;
        public RectTransform ClickTarget => _clickTarget;
        public float AutoDelay => _autoDelay;
        public bool Skippable => _skippable;
    }

    /// <summary>
    /// 提示气泡箭头指向方向。
    /// </summary>
    public enum TipArrowDirection
    {
        Top,
        Bottom,
        Left,
        Right
    }
}
