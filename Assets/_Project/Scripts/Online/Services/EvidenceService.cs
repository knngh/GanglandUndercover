using System;
using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.Core;
using GanglandUndercover.Online;

namespace GanglandUndercover.Online.Services
{
    /// <summary>
    /// EvidenceService — 证据系统服务。
    /// 从 OnlineMatchController.Evidence.cs 中提取，管理证据收集、证据链、证据目标追踪。
    /// 
    /// 职责：
    /// - 证据收集（任务完成 / 击杀 / 监控 / 能力使用等多种来源）
    /// - 证据链管理（注册、查询、强度计算）
    /// - 证据目标追踪（里程碑判定、达成通知）
    /// - 与任务联动（完成任务增加证据分）
    /// - 会议指证（指控目标、投票权重加成）
    /// - 通过 IGameEventBus 发布 EvidenceCollectedEvent / EvidenceTargetReachedEvent
    /// </summary>
    public sealed class EvidenceService : MonoBehaviour
    {
        // ─── 配置引用 ──────────────────────────────────────────

        [Header("── 依赖引用 ──")]
        [Tooltip("OnlineMatchController 引用")]
        [SerializeField] private OnlineMatchController controller;

        [Tooltip("事件总线引用")]
        [SerializeField] private SimpleGameEventBus eventBus;

        private SimpleGameEventBus subscribedEventBus;

        // ─── 内部状态 ──────────────────────────────────────────

        /// <summary>当前证据分数（警方累计）。</summary>
        private int evidenceScore;

        /// <summary>证据目标值（达到即触发胜利条件）。</summary>
        private int evidenceTarget = 10;

        /// <summary>证据里程碑索引（0 = 未达成, 1-4 = 25%/50%/75%/100%）。</summary>
        private int evidenceMilestoneIndex;

        /// <summary>证据链管理器。</summary>
        private EvidenceChain evidenceChain;

        /// <summary>指控目标记录（accuserId → targetId）。</summary>
        private readonly Dictionary<ulong, ulong> accusationTargets = new Dictionary<ulong, ulong>();

        // ─── 公开只读属性 ──────────────────────────────────────

        /// <summary>当前证据分数。</summary>
        public int EvidenceScore => ActiveEvidenceScore;

        /// <summary>证据目标值。</summary>
        public int EvidenceTarget
        {
            get => ActiveEvidenceTarget;
            set => SetActiveEvidenceTarget(value);
        }

        /// <summary>证据里程碑索引。</summary>
        public int EvidenceMilestoneIndex => evidenceMilestoneIndex;

        /// <summary>证据链管理器实例。</summary>
        public EvidenceChain EvidenceManager => evidenceChain;

        /// <summary>指控目标只读访问。</summary>
        public IReadOnlyDictionary<ulong, ulong> AccusationTargets => accusationTargets;

        // ─── 生命周期 ──────────────────────────────────────────

        private void Awake()
        {
            if (eventBus == null)
            {
                eventBus = SimpleGameEventBus.Instance;
            }

            EnsureEvidenceChain();
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
        public void Initialize(OnlineMatchController matchController, IGameEventBus bus)
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

            EnsureEvidenceChain();
            SubscribeToEventBus();
        }

        /// <summary>
        /// 增加证据分数。自动触发里程碑检查和目标达成判定。
        /// </summary>
        /// <param name="amount">增加量（正数）。</param>
        /// <param name="collectorId">收集者 ClientId（0 表示系统来源）。</param>
        public void AddEvidence(int amount, ulong collectorId = 0)
        {
            if (amount <= 0) return;

            OnlineTaskService activeTaskService = ActiveTaskService;
            if (activeTaskService != null)
            {
                activeTaskService.AddEvidence(amount, "证据收集");
                SyncEvidenceFromTaskService(activeTaskService);
            }
            else
            {
                evidenceScore = Mathf.Min(evidenceTarget, evidenceScore + amount);
            }

            eventBus?.Publish(new EvidenceCollectedEvent
            {
                CollectorId = collectorId,
                EvidenceIndex = ActiveEvidenceScore,
            });

            UpdateEvidenceMilestone();
            EvaluateControllerState();
        }

        /// <summary>
        /// 扣减证据分数（Gang 破坏 / Mole 能力导致）。
        /// </summary>
        /// <param name="amount">扣减量（正数）。</param>
        public void SubtractEvidence(int amount)
        {
            if (amount <= 0) return;

            OnlineTaskService activeTaskService = ActiveTaskService;
            if (activeTaskService != null)
            {
                activeTaskService.ReduceEvidence(amount);
                SyncEvidenceFromTaskService(activeTaskService);
                return;
            }

            evidenceScore = Mathf.Max(0, evidenceScore - amount);
        }

        /// <summary>
        /// 注册任务完成时的证据。
        /// </summary>
        public void RegisterTaskEvidence(int taskId, Vector2 position, ulong discovererId)
        {
            EnsureEvidenceChain();
            EvidenceType type = (EvidenceType)((taskId % 5) + 1);
            float elapsed = controller != null ? controller.MatchElapsedSeconds : 0f;
            evidenceChain.Register(type, position, elapsed, discovererId,
                chainId: taskId,
                customDesc: $"任务 #{taskId} 完成时发现{EvidenceChain.EvidenceTypeName(type)}");
        }

        /// <summary>
        /// 注册击杀时的血迹证据。
        /// </summary>
        public void RegisterKillEvidence(Vector2 position)
        {
            EnsureEvidenceChain();
            float elapsed = controller != null ? controller.MatchElapsedSeconds : 0f;
            evidenceChain.Register(EvidenceType.Bloodstain, position, elapsed, 0, chainId: -1);
        }

        /// <summary>
        /// 注册尸体检验证据。
        /// </summary>
        public void RegisterCorpseExamineEvidence(Vector2 position, ulong examinerId)
        {
            EnsureEvidenceChain();
            float elapsed = controller != null ? controller.MatchElapsedSeconds : 0f;
            evidenceChain.Register(EvidenceType.WeaponTrace, position, elapsed, examinerId, chainId: -2);
        }

        /// <summary>
        /// 注册监控摄像头捕获证据。
        /// </summary>
        public void RegisterSurveillanceEvidence(Vector2 position, ulong watcherId)
        {
            EnsureEvidenceChain();
            float elapsed = controller != null ? controller.MatchElapsedSeconds : 0f;
            evidenceChain.Register(EvidenceType.SurveillanceFootage, position, elapsed, watcherId, chainId: -3);
        }

        /// <summary>
        /// 玩家指证目标（会议中使用）。
        /// </summary>
        public void AccusePlayer(ulong accuserId, ulong targetId)
        {
            accusationTargets[accuserId] = targetId;
        }

        /// <summary>
        /// 清除所有指证（新会议开始时调用）。
        /// </summary>
        public void ClearAccusations()
        {
            accusationTargets.Clear();
        }

        /// <summary>
        /// 获取指证的投票权重加成。
        /// 有中等强度以上证据链指证 → 被指证者投票权重 +2。
        /// </summary>
        public int GetAccusationWeightBonus(ulong targetId)
        {
            if (evidenceChain == null) return 0;

            int bonus = 0;
            foreach (var kv in accusationTargets)
            {
                if (kv.Value == targetId)
                {
                    var nodes = evidenceChain.GetDiscovererNodes(kv.Key);
                    int strength = evidenceChain.TotalChainStrength(nodes);
                    if (strength >= 3)
                    {
                        bonus += 2;
                    }
                }
            }
            return bonus;
        }

        /// <summary>
        /// 获取会议证据摘要（供 UI 展示）。
        /// </summary>
        public string MeetingEvidenceDigest(ulong localPlayerId)
        {
            EnsureEvidenceChain();
            return evidenceChain.MeetingEvidenceSummary(localPlayerId);
        }

        /// <summary>
        /// 清空证据链和指证（新局开始时调用）。
        /// </summary>
        public void ClearAll()
        {
            evidenceChain?.Clear();
            accusationTargets.Clear();
            SetActiveEvidenceScore(0);
            evidenceMilestoneIndex = 0;
        }

        /// <summary>设置证据里程碑索引（快照恢复用）。</summary>
        public void SetEvidenceMilestoneIndex(int value)
        {
            evidenceMilestoneIndex = value;
        }

        /// <summary>设置证据分数（快照恢复用），不触发里程碑检查。</summary>
        public void SetEvidenceScore(int value)
        {
            SetActiveEvidenceScore(value);
        }

        /// <summary>完全重置证据状态（网络断开 / 回到大厅用）。</summary>
        public void ResetState()
        {
            evidenceChain?.Clear();
            accusationTargets.Clear();
            SetActiveEvidenceScore(0);
            evidenceMilestoneIndex = 0;
        }

        // ─── 内部方法 ──────────────────────────────────────────

        private void EnsureEvidenceChain()
        {
            if (evidenceChain == null)
            {
                evidenceChain = new EvidenceChain();
            }
        }

        private OnlineTaskService ActiveTaskService => controller != null ? controller.taskService : null;

        private int ActiveEvidenceScore
        {
            get
            {
                OnlineTaskService activeTaskService = ActiveTaskService;
                return activeTaskService != null ? activeTaskService.EvidenceScore : evidenceScore;
            }
        }

        private int ActiveEvidenceTarget
        {
            get
            {
                OnlineTaskService activeTaskService = ActiveTaskService;
                if (activeTaskService != null && activeTaskService.EvidenceTarget > 0)
                {
                    return activeTaskService.EvidenceTarget;
                }

                return evidenceTarget;
            }
        }

        private void SetActiveEvidenceTarget(int value)
        {
            evidenceTarget = Mathf.Max(1, value);

            OnlineTaskService activeTaskService = ActiveTaskService;
            if (activeTaskService == null)
            {
                return;
            }

            activeTaskService.EvidenceTarget = evidenceTarget;
            SyncEvidenceFromTaskService(activeTaskService);
        }

        private void SetActiveEvidenceScore(int value)
        {
            evidenceScore = Mathf.Max(0, value);

            OnlineTaskService activeTaskService = ActiveTaskService;
            if (activeTaskService == null)
            {
                return;
            }

            activeTaskService.EvidenceScore = evidenceScore;
            SyncEvidenceFromTaskService(activeTaskService);
        }

        private void SyncEvidenceFromTaskService(OnlineTaskService activeTaskService)
        {
            if (activeTaskService == null)
            {
                return;
            }

            evidenceScore = activeTaskService.EvidenceScore;
            if (activeTaskService.EvidenceTarget > 0)
            {
                evidenceTarget = activeTaskService.EvidenceTarget;
            }
        }

        private void EvaluateControllerState()
        {
            if (controller == null)
            {
                return;
            }

            controller.EvaluateWinConditions();
            controller.BroadcastSnapshot();
        }

        private void SubscribeToEventBus()
        {
            if (eventBus == null || subscribedEventBus == eventBus)
            {
                return;
            }

            eventBus.Subscribe<TaskCompletedEvent>(OnTaskCompleted);
            eventBus.Subscribe<PlayerKilledEvent>(OnPlayerKilled);
            subscribedEventBus = eventBus;
        }

        private void UnsubscribeFromEventBus()
        {
            if (subscribedEventBus == null)
            {
                return;
            }

            subscribedEventBus.Unsubscribe<TaskCompletedEvent>(OnTaskCompleted);
            subscribedEventBus.Unsubscribe<PlayerKilledEvent>(OnPlayerKilled);
            subscribedEventBus = null;
        }

        /// <summary>检查证据里程碑，触发达成事件。</summary>
        private void UpdateEvidenceMilestone()
        {
            int score = ActiveEvidenceScore;
            int target = ActiveEvidenceTarget;
            int milestone = CalculateMilestone(score, target);
            if (milestone <= evidenceMilestoneIndex) return;

            evidenceMilestoneIndex = milestone;

            // 达成 100% 时发布 EvidenceTargetReachedEvent
            if (milestone >= 4)
            {
                eventBus?.Publish(new EvidenceTargetReachedEvent
                {
                    Score = score,
                    Target = target,
                });
            }
        }

        /// <summary>根据分数/目标比例计算里程碑等级。</summary>
        private static int CalculateMilestone(int score, int target)
        {
            if (target <= 0) return 0;
            float ratio = score / (float)target;
            if (ratio >= 1f) return 4;
            if (ratio >= 0.75f) return 3;
            if (ratio >= 0.5f) return 2;
            if (ratio >= 0.25f) return 1;
            return 0;
        }

        /// <summary>任务完成事件回调 → 增加证据。</summary>
        private void OnTaskCompleted(TaskCompletedEvent evt)
        {
            if (controller == null) return;

            // 查找完成任务的玩家以获取职业/角色加成
            OnlineProfession profession = default;
            OnlineRole role = OnlineRole.Unassigned;
            if (controller.players.TryGetValue(evt.PlayerId, out OnlinePlayerState player))
            {
                profession = player.Profession;
                role = player.PublicRole;
            }

            int gain = controller.taskService != null
                ? controller.taskService.EvidenceGainFor(evt.TaskIndex, profession, role)
                : 3;

            AddEvidence(gain, evt.PlayerId);
        }

        /// <summary>玩家被击杀事件回调 → 注册血迹证据。</summary>
        private void OnPlayerKilled(PlayerKilledEvent evt)
        {
            if (controller == null) return;

            Vector2 killPos = Vector2.zero;
            if (controller.players.TryGetValue(evt.VictimId, out OnlinePlayerState victim))
            {
                killPos = victim.Position;
            }

            RegisterKillEvidence(killPos);
        }
    }
}
