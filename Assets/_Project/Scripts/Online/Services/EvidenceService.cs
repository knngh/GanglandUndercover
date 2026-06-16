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
        public int EvidenceScore => evidenceScore;

        /// <summary>证据目标值。</summary>
        public int EvidenceTarget
        {
            get => evidenceTarget;
            set => evidenceTarget = Mathf.Max(1, value);
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
            if (eventBus != null)
            {
                eventBus.Subscribe<TaskCompletedEvent>(OnTaskCompleted);
                eventBus.Subscribe<PlayerKilledEvent>(OnPlayerKilled);
            }
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.Unsubscribe<TaskCompletedEvent>(OnTaskCompleted);
                eventBus.Unsubscribe<PlayerKilledEvent>(OnPlayerKilled);
            }
        }

        // ─── 公开 API ──────────────────────────────────────────

        /// <summary>
        /// 初始化服务引用。由 OnlineMatchController 调用。
        /// </summary>
        public void Initialize(OnlineMatchController matchController, IGameEventBus bus)
        {
            controller = matchController;
            eventBus = bus as SimpleGameEventBus ?? SimpleGameEventBus.Instance;
            EnsureEvidenceChain();
        }

        /// <summary>
        /// 增加证据分数。自动触发里程碑检查和目标达成判定。
        /// </summary>
        /// <param name="amount">增加量（正数）。</param>
        /// <param name="collectorId">收集者 ClientId（0 表示系统来源）。</param>
        public void AddEvidence(int amount, ulong collectorId = 0)
        {
            if (amount <= 0) return;

            evidenceScore = Mathf.Min(evidenceTarget, evidenceScore + amount);

            eventBus?.Publish(new EvidenceCollectedEvent
            {
                CollectorId = collectorId,
                EvidenceIndex = evidenceScore,
            });

            UpdateEvidenceMilestone();
        }

        /// <summary>
        /// 扣减证据分数（Gang 破坏 / Mole 能力导致）。
        /// </summary>
        /// <param name="amount">扣减量（正数）。</param>
        public void SubtractEvidence(int amount)
        {
            if (amount <= 0) return;
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
            evidenceScore = 0;
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

        /// <summary>检查证据里程碑，触发达成事件。</summary>
        private void UpdateEvidenceMilestone()
        {
            int milestone = CalculateMilestone(evidenceScore, evidenceTarget);
            if (milestone <= evidenceMilestoneIndex) return;

            evidenceMilestoneIndex = milestone;

            // 达成 100% 时发布 EvidenceTargetReachedEvent
            if (milestone >= 4)
            {
                eventBus?.Publish(new EvidenceTargetReachedEvent
                {
                    Score = evidenceScore,
                    Target = evidenceTarget,
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
            // TODO: 根据 TaskIndex 计算证据增益量，调用 AddEvidence
            // 增益量逻辑来自 OnlineMatchController.EvidenceGainFor()
        }

        /// <summary>玩家被击杀事件回调 → 注册血迹证据。</summary>
        private void OnPlayerKilled(PlayerKilledEvent evt)
        {
            // TODO: 从 controller 获取击杀位置，调用 RegisterKillEvidence
        }
    }
}
