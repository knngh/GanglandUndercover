using System;
using UnityEngine;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online.Services
{
    /// <summary>
    /// MeetingService — 会议系统服务。
    /// 从 OnlineMatchController.Gameplay.cs 中提取会议相关逻辑。
    /// 
    /// 职责：
    /// - 紧急会议 / 尸体报告触发
    /// - 会议阶段管理（intro → discussion → voting → result）
    /// - 会议冷却管理
    /// - 与 VotingService 衔接（会议 discussion 结束 → 进入 voting → voting 结束 → 结果）
    /// - 通过 IGameEventBus 发布 MeetingCalledEvent / MeetingEndedEvent / BodyReportedEvent
    /// </summary>
    public sealed class MeetingService : MonoBehaviour
    {
        // ─── 配置引用 ──────────────────────────────────────────

        [Header("── 依赖引用 ──")]
        [Tooltip("OnlineMatchController 引用")]
        [SerializeField] private OnlineMatchController controller;

        [Tooltip("事件总线引用")]
        [SerializeField] private SimpleGameEventBus eventBus;

        [Tooltip("投票服务引用（会议结束后衔接投票）")]
        [SerializeField] private VotingService votingService;

        private SimpleGameEventBus subscribedEventBus;

        // ─── 内部状态 ──────────────────────────────────────────

        /// <summary>剩余紧急会议次数。</summary>
        private int emergencyMeetingsLeft;

        /// <summary>紧急会议冷却计时器（秒）。</summary>
        private float emergencyCooldownTimer;

        /// <summary>累计会议次数。</summary>
        private int meetingCount;

        /// <summary>当前会议原因文本。</summary>
        private string currentMeetingReason = string.Empty;

        /// <summary>当前会议是否为紧急会议。</summary>
        private bool currentMeetingIsEmergency;

        /// <summary>当前会议触发者 ClientId。</summary>
        private ulong currentMeetingCallerId;

        // ─── 公开只读属性 ──────────────────────────────────────

        /// <summary>剩余紧急会议次数。</summary>
        public int EmergencyMeetingsLeft => emergencyMeetingsLeft;

        /// <summary>紧急会议冷却计时器。</summary>
        public float EmergencyCooldownTimer => emergencyCooldownTimer;

        /// <summary>累计会议次数。</summary>
        public int MeetingCount => meetingCount;

        /// <summary>当前会议原因。</summary>
        public string CurrentMeetingReason => currentMeetingReason;

        /// <summary>当前会议是否为紧急会议。</summary>
        public bool CurrentMeetingIsEmergency => currentMeetingIsEmergency;

        /// <summary>是否处于会议阶段。</summary>
        public bool IsMeetingPhase
        {
            get
            {
                if (controller == null) return false;
                return controller.Phase == OnlineMatchPhase.Meeting ||
                       controller.Phase == OnlineMatchPhase.Voting;
            }
        }

        // ─── 生命周期 ──────────────────────────────────────────

        private void Awake()
        {
            if (eventBus == null)
            {
                eventBus = SimpleGameEventBus.Instance;
            }
        }

        private void OnEnable()
        {
            SubscribeToEventBus();
        }

        private void OnDisable()
        {
            UnsubscribeFromEventBus();
        }

        // ─── 公开 API ──────────────────────────────────────────

        /// <summary>
        /// 初始化服务引用。由 OnlineMatchController 调用。
        /// </summary>
        public void Initialize(OnlineMatchController matchController, IGameEventBus bus, VotingService voting = null)
        {
            controller = matchController;
            SimpleGameEventBus nextBus = bus as SimpleGameEventBus ?? SimpleGameEventBus.Instance;
            if (eventBus != nextBus)
            {
                UnsubscribeFromEventBus();
                eventBus = nextBus;
            }
            else
            {
                eventBus = nextBus;
            }

            votingService = voting;
            SubscribeToEventBus();
        }

        /// <summary>
        /// 每帧 tick，递减冷却计时器。
        /// 由 OnlineMatchController.TickHostSimulation() 调用。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (emergencyCooldownTimer > 0f)
            {
                emergencyCooldownTimer = Mathf.Max(0f, emergencyCooldownTimer - deltaTime);
                SyncControllerMeetingState();
            }
        }

        /// <summary>
        /// 尝试触发紧急会议（仅紧急会议，不含尸体报告）。
        /// 由 OnlineMatchController.TryReportOrEmergency() 在尸体报告检查后调用。
        /// 仅校验条件并更新次数/冷却，不触发会议阶段切换。
        /// </summary>
        /// <param name="senderClientId">触发者 ClientId。</param>
        /// <param name="player">触发者状态。</param>
        /// <returns>是否通过校验（可触发紧急会议）。</returns>
        public bool TryReportOrEmergency(ulong senderClientId, OnlinePlayerState player)
        {
            if (controller == null) return false;

            // 通讯干扰中不能开紧急会议
            if (controller.CommunicationJamTimer > 0f) return false;

            // 次数检查
            if (emergencyMeetingsLeft <= 0) return false;

            // 冷却检查
            if (emergencyCooldownTimer > 0f) return false;

            // 范围检查（需在紧急铃范围内）
            if (!IsInEmergencyRange(player.Position)) return false;

            // 扣除次数并设冷却（meetingCount 由 controller.BeginMeeting 统一递增）
            emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
            emergencyCooldownTimer = GetEmergencyCooldown();
            SyncControllerMeetingState();

            return true;
        }

        /// <summary>
        /// 扣除紧急会议次数并设置冷却（纯状态变更，不触发会议阶段切换）。
        /// 由 OnlineMatchController.CallEmergencyMeeting() 调用。
        /// </summary>
        public bool ConsumeEmergencyMeeting(string callerDisplayName, ulong callerId = 0)
        {
            if (emergencyMeetingsLeft <= 0 || emergencyCooldownTimer > 0f) return false;

            emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
            emergencyCooldownTimer = GetEmergencyCooldown();

            currentMeetingReason = callerDisplayName + " 按下警署紧急铃";
            currentMeetingIsEmergency = true;
            currentMeetingCallerId = callerId;
            // meetingCount 由 controller.BeginMeeting 统一递增，通过 SyncMeetingStartedFromController 回写

            SyncControllerMeetingState();
            return true;
        }

        /// <summary>
        /// 服务层公开入口：消费紧急会议并通过 controller 统一进入会议流程。
        /// </summary>
        public bool CallEmergencyMeeting(string callerDisplayName, ulong callerId = 0)
        {
            if (!ConsumeEmergencyMeeting(callerDisplayName, callerId))
            {
                return false;
            }

            controller?.BeginMeeting(callerDisplayName + " 按下警署紧急铃", callerId, isEmergency: true);
            return true;
        }

        /// <summary>
        /// 记录会议元数据（原因/类型/调用者）。不触发阶段切换或投票重置。
        /// </summary>
        public void SetMeetingMetadata(string reason, ulong callerId = 0, bool isEmergency = false)
        {
            currentMeetingReason = reason;
            currentMeetingIsEmergency = isEmergency;
            currentMeetingCallerId = callerId;
        }

        /// <summary>
        /// 对局开始时初始化紧急会议次数和冷却。
        /// </summary>
        /// <param name="playerCount">当前玩家数。</param>
        public void OnMatchStarted(int playerCount)
        {
            if (controller == null) return;

            emergencyMeetingsLeft = GetEmergencyMeetingLimit(playerCount);
            emergencyCooldownTimer = 0f;
            meetingCount = 0;
            currentMeetingReason = string.Empty;
            currentMeetingIsEmergency = false;
            currentMeetingCallerId = 0;
            controller.ResetMeetingCountFromService();
            SyncControllerMeetingState();
        }

        /// <summary>
        /// 对局重置时清除所有会议状态。
        /// </summary>
        public void OnMatchReset()
        {
            emergencyMeetingsLeft = 0;
            emergencyCooldownTimer = 0f;
            meetingCount = 0;
            currentMeetingReason = string.Empty;
            currentMeetingIsEmergency = false;
            currentMeetingCallerId = 0;
            controller?.ResetMeetingCountFromService();
            SyncControllerMeetingState();
        }

        /// <summary>设置剩余紧急会议次数（快照恢复 / 对局初始化用）。</summary>
        public void SetEmergencyMeetingsLeft(int value)
        {
            emergencyMeetingsLeft = Mathf.Max(0, value);
            SyncControllerMeetingState();
        }

        /// <summary>设置紧急会议冷却计时器（快照恢复 / 对局初始化用）。</summary>
        public void SetEmergencyCooldownTimer(float value)
        {
            emergencyCooldownTimer = Mathf.Max(0f, value);
            SyncControllerMeetingState();
        }

        /// <summary>同步 meetingsLeft 和 cooldownTimer（controller → service 方向）。</summary>
        internal void SyncMeetingsAndCooldown(int meetingsLeft, float cooldownTimer)
        {
            emergencyMeetingsLeft = Mathf.Max(0, meetingsLeft);
            emergencyCooldownTimer = Mathf.Max(0f, cooldownTimer);
        }

        /// <summary>快照恢复时同步会议次数、冷却和当前会议元数据。</summary>
        internal void SyncSnapshotStateFromController(
            int meetingsLeft,
            float cooldownTimer,
            int snapshotMeetingCount,
            string reason)
        {
            SyncMeetingsAndCooldown(meetingsLeft, cooldownTimer);
            meetingCount = Mathf.Max(0, snapshotMeetingCount);
            currentMeetingReason = reason ?? string.Empty;
            currentMeetingCallerId = 0;
            currentMeetingIsEmergency = false;
        }

        /// <summary>同步会议元数据（controller 的 BeginMeeting 调用后同步）。</summary>
        internal void SyncMeetingStartedFromController(
            int meetingsLeft,
            float cooldownTimer,
            string reason,
            ulong callerId,
            bool isEmergency)
        {
            SyncMeetingsAndCooldown(meetingsLeft, cooldownTimer);
            currentMeetingReason = reason ?? string.Empty;
            currentMeetingCallerId = callerId;
            currentMeetingIsEmergency = isEmergency;
            meetingCount++;
        }

        /// <summary>完全重置会议状态（网络断开 / 回到大厅用）。</summary>
        public void ResetState()
        {
            emergencyMeetingsLeft = 0;
            emergencyCooldownTimer = 0f;
            meetingCount = 0;
            currentMeetingReason = string.Empty;
            currentMeetingIsEmergency = false;
            currentMeetingCallerId = 0;
            controller?.ResetMeetingCountFromService();
            SyncControllerMeetingState();
        }

        // ─── 内部方法 ──────────────────────────────────────────

        private void SyncControllerMeetingState()
        {
            controller?.SyncMeetingStateFromService(emergencyMeetingsLeft, emergencyCooldownTimer);
        }

        /// <summary>投票结果回调 → 发布会议结束事件。</summary>
        private void OnVoteResult(VoteResultEvent evt)
        {
            eventBus?.Publish(new MeetingEndedEvent
            {
                EjectedId = evt.EjectedId,
                WasEmergency = currentMeetingIsEmergency,
            });
        }

        private void SubscribeToEventBus()
        {
            if (eventBus == null || subscribedEventBus == eventBus)
            {
                return;
            }

            eventBus.Subscribe<VoteResultEvent>(OnVoteResult);
            subscribedEventBus = eventBus;
        }

        private void UnsubscribeFromEventBus()
        {
            if (subscribedEventBus == null)
            {
                return;
            }

            subscribedEventBus.Unsubscribe<VoteResultEvent>(OnVoteResult);
            subscribedEventBus = null;
        }

        /// <summary>检查是否在紧急铃范围内。</summary>
        private bool IsInEmergencyRange(Vector3 position)
        {
            if (controller?.MapService == null) return false;
            float range = controller.RuleSet != null
                ? controller.RuleSet.ReportRangeFor(controller.PlayerCount)
                : 1.25f;
            Vector3 bellPos = controller.MapService.ScaleMapPosition(Vector3.zero);
            return Vector3.Distance(position, bellPos) <= range;
        }

        /// <summary>获取紧急会议冷却时间。</summary>
        private float GetEmergencyCooldown()
        {
            if (controller?.RuleSet == null) return 75f;
            return controller.RuleSet.EmergencyCooldownSecondsFor(controller.PlayerCount);
        }

        /// <summary>获取紧急会议次数上限。</summary>
        private int GetEmergencyMeetingLimit(int playerCount)
        {
            if (controller?.RuleSet == null) return 3;
            return controller.RuleSet.EmergencyMeetingLimitFor(playerCount);
        }
    }
}
