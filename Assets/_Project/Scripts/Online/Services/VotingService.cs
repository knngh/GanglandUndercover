using System;
using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online.Services
{
    /// <summary>
    /// VotingService — 投票系统服务。
    /// 负责投票发起、计票、淘汰判定、skip vote 处理、投票结果广播。
    /// 从 OnlineMatchController.Gameplay.cs 的投票逻辑中提取。
    /// 
    /// 职责：
    /// - 接收并记录玩家投票（含 skip vote）
    /// - 计票并判定淘汰结果（含 tie / majority）
    /// - 通过 IGameEventBus 发布 VoteSubmittedEvent / VoteResultEvent
    /// - 与 MeetingService 衔接（会议结束 → 进入投票 → 投票结束 → 会议结束）
    /// </summary>
    public sealed class VotingService : MonoBehaviour
    {
        // ─── 配置引用 ──────────────────────────────────────────

        [Header("── 依赖引用 ──")]
        [Tooltip("OnlineMatchController 引用，用于访问共享状态（players / ruleSet / votes 等）")]
        [SerializeField] private OnlineMatchController controller;

        [Tooltip("事件总线引用，用于发布/订阅游戏事件")]
        [SerializeField] private SimpleGameEventBus eventBus;

        // ─── 内部状态 ──────────────────────────────────────────

        /// <summary>本轮投票记录（voter → target，ulong.MaxValue 表示 skip）。</summary>
        private readonly Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>();

        /// <summary>投票记录只读访问（供 OnlineMatchController 序列化快照）。</summary>
        public IReadOnlyDictionary<ulong, ulong> Votes => votes;

        /// <summary>投票目标为 skip 的哨兵值（与 OnlineMatchController.SkipVoteTarget 保持一致）。</summary>
        public const ulong SkipVoteTarget = ulong.MaxValue;

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
            if (eventBus != null)
            {
                eventBus.Subscribe<MeetingCalledEvent>(OnMeetingCalled);
            }
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.Unsubscribe<MeetingCalledEvent>(OnMeetingCalled);
            }
        }

        // ─── 公开 API ──────────────────────────────────────────

        /// <summary>
        /// 初始化服务引用。由 OnlineMatchController.Awake() 或 Start() 调用。
        /// </summary>
        public void Initialize(OnlineMatchController matchController, IGameEventBus bus)
        {
            controller = matchController;
            eventBus = bus as SimpleGameEventBus ?? SimpleGameEventBus.Instance;
        }

        /// <summary>
        /// 应用投票。由 OnlineMatchController.ApplyVote 或网络消息处理器调用。
        /// 验证投票合法性后记录，并通过事件总线发布 VoteSubmittedEvent。
        /// 若所有存活玩家已投票，自动触发 ResolveVotes。
        /// </summary>
        /// <param name="voterClientId">投票者 ClientId。</param>
        /// <param name="targetClientId">目标 ClientId（ulong.MaxValue = skip）。</param>
        public void ApplyVote(ulong voterClientId, ulong targetClientId)
        {
            if (controller == null) return;

            // 仅在会议或投票阶段接受投票
            if (controller.Phase != OnlineMatchPhase.Meeting && controller.Phase != OnlineMatchPhase.Voting)
            {
                return;
            }

            // 验证投票者合法性
            if (!controller.Players.TryGetValue(voterClientId, out OnlinePlayerState voter) || !voter.Alive)
            {
                return;
            }

            // 验证目标合法性（skip 除外）
            if (targetClientId != SkipVoteTarget)
            {
                if (!controller.Players.TryGetValue(targetClientId, out OnlinePlayerState target) || !target.Alive)
                {
                    return;
                }
            }

            // 记录投票
            votes[voterClientId] = targetClientId;

            // 发布投票提交事件
            eventBus?.Publish(new VoteSubmittedEvent
            {
                ClientId = voterClientId,
                TargetId = targetClientId,
                IsSkip = targetClientId == SkipVoteTarget,
            });

            // 检查是否所有存活玩家已投票
            int aliveCount = CountAlivePlayers();
            if (votes.Count >= aliveCount)
            {
                ResolveVotes();
            }
        }

        /// <summary>
        /// 计票并判定淘汰结果。
        /// 包含证据链权重加成（来自 EvidenceService 的指控数据）。
        /// 结果通过 VoteResultEvent 发布。
        /// </summary>
        public void ResolveVotes()
        {
            if (controller == null) return;

            // 仅在会议或投票阶段结算
            if (controller.Phase != OnlineMatchPhase.Voting && controller.Phase != OnlineMatchPhase.Meeting)
            {
                return;
            }

            Dictionary<ulong, int> tally = new Dictionary<ulong, int>();
            int skipVotes = 0;

            foreach (ulong targetClientId in votes.Values)
            {
                if (targetClientId == SkipVoteTarget)
                {
                    skipVotes++;
                    continue;
                }

                tally[targetClientId] = tally.TryGetValue(targetClientId, out int count) ? count + 1 : 1;
            }

            // 应用证据链指控权重加成
            var accusedList = new List<ulong>(tally.Keys);
            foreach (var accused in accusedList)
            {
                int bonus = controller.GetAccusationWeightBonus(accused);
                if (bonus > 0)
                {
                    tally[accused] += bonus;
                }
            }

            ulong ejectedClientId = SkipVoteTarget;
            int bestVotes = 0;
            bool tied = false;

            if (skipVotes > 0)
            {
                bestVotes = skipVotes;
            }

            foreach (KeyValuePair<ulong, int> pair in tally)
            {
                if (pair.Value > bestVotes)
                {
                    ejectedClientId = pair.Key;
                    bestVotes = pair.Value;
                    tied = false;
                }
                else if (pair.Value == bestVotes)
                {
                    tied = true;
                }
            }

            // Tie → no ejection
            if (tied)
            {
                ejectedClientId = SkipVoteTarget;
            }

            // 构建投票计数数组
            int[] voteCounts = new int[tally.Count + 1]; // +1 for skip
            int idx = 0;
            foreach (var kv in tally)
            {
                voteCounts[idx++] = kv.Value;
            }
            voteCounts[idx] = skipVotes;

            // 淘汰被选中玩家
            if (ejectedClientId != SkipVoteTarget)
            {
                if (controller.Players.TryGetValue(ejectedClientId, out OnlinePlayerState ejected))
                {
                    ejected.Alive = false;
                    ejected.Input = Vector2.zero;
                    controller.Players[ejectedClientId] = ejected;
                }
            }

            // 发布投票结果事件
            eventBus?.Publish(new VoteResultEvent
            {
                EjectedId = ejectedClientId,
                IsTie = tied,
                VoteCounts = voteCounts,
            });
        }

        /// <summary>
        /// 清除所有投票记录。在新一轮会议开始时调用。
        /// </summary>
        public void ClearVotes()
        {
            votes.Clear();
        }

        /// <summary>
        /// 检查指定玩家是否已投票。
        /// </summary>
        public bool HasVoted(ulong clientId) => votes.ContainsKey(clientId);

        /// <summary>
        /// 移除断连玩家的投票（投票者断连 + 投给断连者的票都要清）。
        /// </summary>
        public void RemoveDisconnectedPlayerVotes(ulong clientId)
        {
            votes.Remove(clientId);

            List<ulong> votersToClear = new List<ulong>();
            foreach (KeyValuePair<ulong, ulong> vote in votes)
            {
                if (vote.Value == clientId)
                {
                    votersToClear.Add(vote.Key);
                }
            }

            for (int i = 0; i < votersToClear.Count; i++)
            {
                votes.Remove(votersToClear[i]);
            }
        }

        // ─── 内部方法 ──────────────────────────────────────────

        /// <summary>会议开始时清空投票记录。</summary>
        private void OnMeetingCalled(MeetingCalledEvent evt)
        {
            ClearVotes();
        }

        /// <summary>统计存活玩家数。</summary>
        private int CountAlivePlayers()
        {
            if (controller == null) return 0;
            int count = 0;
            foreach (var p in controller.Players.Values)
            {
                if (p.Alive) count++;
            }
            return count;
        }
    }
}
