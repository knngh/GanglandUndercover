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
        /// 尝试报告尸体或触发紧急会议。
        /// 由 OnlineMatchController.TryReportOrEmergency() 调用。
        /// </summary>
        /// <param name="senderClientId">触发者 ClientId。</param>
        /// <param name="player">触发者状态。</param>
        /// <returns>是否成功触发会议。</returns>
        public bool TryReportOrEmergency(ulong senderClientId, OnlinePlayerState player)
        {
            if (controller == null) return false;

            // 尸体报告
            if (TryFindNearestBody(player.Position, out int bodyIndex, out ulong victimId))
            {
                // 标记尸体已报告
                MarkBodyReported(bodyIndex);

                // 发布尸体报告事件
                eventBus?.Publish(new BodyReportedEvent
                {
                    ReporterId = senderClientId,
                    VictimId = victimId,
                });

                // 开始会议
                string reason = player.DisplayName + " 发现尸体并报案";
                BeginMeeting(reason, senderClientId, isEmergency: false);
                return true;
            }

            // 通讯干扰中不能开紧急会议
            if (controller.CommunicationJamTimer > 0f)
            {
                return false;
            }

            // 次数检查
            if (emergencyMeetingsLeft <= 0)
            {
                return false;
            }

            // 冷却检查
            if (emergencyCooldownTimer > 0f)
            {
                return false;
            }

            // 范围检查（需在紧急铃范围内）
            if (!IsInEmergencyRange(player.Position))
            {
                return false;
            }

            // 扣除次数并设冷却
            emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
            float cooldown = GetEmergencyCooldown();
            emergencyCooldownTimer = cooldown;
            SyncControllerMeetingState();

            string emergencyReason = player.DisplayName + " 按下警署紧急铃";
            BeginMeeting(emergencyReason, senderClientId, isEmergency: true);
            return true;
        }

        /// <summary>
        /// 公开调用紧急会议（供 EmergencyButton / HUD 使用）。
        /// </summary>
        public void CallEmergencyMeeting(string callerDisplayName, ulong callerId = 0)
        {
            if (emergencyMeetingsLeft <= 0 || emergencyCooldownTimer > 0f) return;

            emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
            emergencyCooldownTimer = GetEmergencyCooldown();
            SyncControllerMeetingState();

            string reason = callerDisplayName + " 按下警署紧急铃";
            BeginMeeting(reason, callerId, isEmergency: true);
        }

        /// <summary>
        /// 开始会议。设置阶段、计时器，重置投票；会议事件由 controller 的统一入口发布。
        /// </summary>
        /// <param name="reason">会议原因文本。</param>
        /// <param name="callerId">触发者 ClientId。</param>
        /// <param name="isEmergency">是否为紧急会议。</param>
        public void BeginMeeting(string reason, ulong callerId = 0, bool isEmergency = false)
        {
            if (controller == null) return;

            currentMeetingReason = reason;
            currentMeetingIsEmergency = isEmergency;
            currentMeetingCallerId = callerId;

            controller.BeginMeeting(reason, callerId, isEmergency);
            meetingCount = controller.MeetingCount;
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

        /// <summary>由旧 controller 入口反向同步可见会议状态，避免双状态源漂移。</summary>
        internal void SyncStateFromController(int meetingsLeft, float cooldownTimer, int controllerMeetingCount)
        {
            emergencyMeetingsLeft = Mathf.Max(0, meetingsLeft);
            emergencyCooldownTimer = Mathf.Max(0f, cooldownTimer);
            meetingCount = Mathf.Max(0, controllerMeetingCount);
        }

        /// <summary>由旧 controller 会议入口同步新会议元数据和计数。</summary>
        internal void SyncMeetingStartedFromController(
            int meetingsLeft,
            float cooldownTimer,
            int controllerMeetingCount,
            string reason,
            ulong callerId,
            bool isEmergency)
        {
            SyncStateFromController(meetingsLeft, cooldownTimer, controllerMeetingCount);
            currentMeetingReason = reason ?? string.Empty;
            currentMeetingCallerId = callerId;
            currentMeetingIsEmergency = isEmergency;
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

        /// <summary>查找指定位置附近的最近未报告尸体。</summary>
        private bool TryFindNearestBody(Vector3 position, out int bodyIndex, out ulong victimId)
        {
            bodyIndex = -1;
            victimId = 0;
            if (controller == null || controller.Bodies == null) return false;

            float bestDistance = controller.RuleSet != null
                ? controller.RuleSet.ReportRangeFor(controller.PlayerCount)
                : 1.25f;
            for (int i = 0; i < controller.Bodies.Count; i++)
            {
                var body = controller.Bodies[i];
                if (body.Reported) continue;

                float distance = Vector3.Distance(position, body.Position);
                if (distance <= bestDistance)
                {
                    bodyIndex = i;
                    victimId = body.VictimClientId;
                    bestDistance = distance;
                }
            }
            return bodyIndex >= 0;
        }

        /// <summary>标记尸体已报告。</summary>
        private void MarkBodyReported(int bodyIndex)
        {
            if (controller?.Bodies == null) return;
            if (bodyIndex < 0 || bodyIndex >= controller.Bodies.Count) return;

            var body = controller.Bodies[bodyIndex];
            body.Reported = true;
            controller.Bodies[bodyIndex] = body;
        }

        /// <summary>检查是否在紧急铃范围内。</summary>
        private bool IsInEmergencyRange(Vector3 position)
        {
            if (controller?.MapService == null) return false;
            Vector2 center = controller.MapService.CurrentMeetingCenter;
            float range = controller.RuleSet != null
                ? controller.RuleSet.InteractionRange * 2f
                : 2.16f;
            return Vector2.Distance(position, center) <= range;
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
