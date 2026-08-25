using System;
using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online.Services
{
    /// <summary>
    /// VotingService — 投票系统服务（纯逻辑层）。
    ///
    /// 职责：
    /// - 接收并记录玩家投票（含 skip vote）
    /// - 投票合法性验证
    /// - 计票并计算淘汰结果（含 tie / majority / 证据链权重加成）
    /// - 投票记录管理（清除、断连清理、快照恢复）
    ///
    /// 注意：本服务不执行任何游戏状态变更（如淘汰玩家、切换阶段）。
    /// 所有副作用由 OnlineMatchController 在收到 VoteResolution 后处理。
    /// </summary>
    public sealed class VotingService : MonoBehaviour
    {
        // ─── 结果类型 ──────────────────────────────────────────

        /// <summary>投票结算结果（纯数据，无副作用）。</summary>
        public struct VoteResolution
        {
            /// <summary>被淘汰玩家的 ClientId（SkipVoteTarget = 无人淘汰）。</summary>
            public ulong EjectedClientId;
            /// <summary>是否平局。</summary>
            public bool IsTie;
            /// <summary>每位被投玩家的得票数。</summary>
            public Dictionary<ulong, int> Tally;
            /// <summary>跳过票数。</summary>
            public int SkipVotes;
        }

        // ─── 配置引用 ──────────────────────────────────────────

        [Header("── 依赖引用 ──")]
        [Tooltip("OnlineMatchController 引用，用于访问共享状态（players / ruleSet 等）")]
        [SerializeField] private OnlineMatchController controller;

        [Tooltip("事件总线引用，用于发布/订阅游戏事件")]
        [SerializeField] private SimpleGameEventBus eventBus;

        private SimpleGameEventBus subscribedEventBus;

        // ─── 内部状态 ──────────────────────────────────────────

        /// <summary>本轮投票记录（voter → target，ulong.MaxValue 表示 skip）。</summary>
        private Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>();

        /// <summary>投票记录只读访问（供 OnlineMatchController 序列化快照）。</summary>
        public IReadOnlyDictionary<ulong, ulong> Votes => votes;

        /// <summary>投票目标为 skip 的哨兵值。</summary>
        public const ulong SkipVoteTarget = ulong.MaxValue;

        /// <summary>是否所有存活玩家都已投票。</summary>
        public bool AllVoted => controller != null && votes.Count >= CountAlivePlayers();

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
        /// 初始化服务引用。由 OnlineMatchController.Awake() 或 Start() 调用。
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

            SubscribeToEventBus();
        }

        /// <summary>
        /// 应用投票（纯逻辑，无副作用）。
        /// 验证投票合法性后记录投票，发布 VoteSubmittedEvent。
        /// 返回 true 表示投票被接受。
        /// 注意：不自动触发 ResolveVotes，由 Controller 决定是否结算。
        /// </summary>
        public bool ApplyVote(ulong voterClientId, ulong targetClientId)
        {
            if (controller == null) return false;

            // 讨论阶段只允许发言与查证，选票必须等到独立投票阶段。
            if (controller.Phase != OnlineMatchPhase.Voting)
            {
                return false;
            }

            // 验证投票者合法性
            if (!controller.Players.TryGetValue(voterClientId, out OnlinePlayerState voter) || !voter.Alive)
            {
                return false;
            }

            // 投票提交后锁定，客户端重发或伪造请求不能改票。
            if (votes.ContainsKey(voterClientId))
            {
                return false;
            }

            // 验证目标合法性（skip 除外）
            if (targetClientId != SkipVoteTarget)
            {
                if (!controller.Players.TryGetValue(targetClientId, out OnlinePlayerState target) || !target.Alive)
                {
                    return false;
                }
            }

            // 记录投票
            votes.Add(voterClientId, targetClientId);

            // 发布投票提交事件
            eventBus?.Publish(new VoteSubmittedEvent
            {
                ClientId = voterClientId,
                TargetId = targetClientId,
                IsSkip = targetClientId == SkipVoteTarget,
            });

            return true;
        }

        /// <summary>
        /// 计票并计算淘汰结果（纯计算，无副作用）。
        /// 包含证据链权重加成（来自 EvidenceService 的指控数据）。
        /// 返回 VoteResolution 结构体，由 Controller 执行实际淘汰。
        /// 调用后自动清除投票记录。
        /// </summary>
        public VoteResolution ResolveVotes()
        {
            var empty = new VoteResolution
            {
                EjectedClientId = SkipVoteTarget,
                IsTie = false,
                Tally = new Dictionary<ulong, int>(),
                SkipVotes = 0,
            };

            if (controller == null) return empty;

            if (controller.Phase != OnlineMatchPhase.Voting)
            {
                return empty;
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

            var result = new VoteResolution
            {
                EjectedClientId = ejectedClientId,
                IsTie = tied,
                Tally = new Dictionary<ulong, int>(tally),
                SkipVotes = skipVotes,
            };

            // 清除投票记录（新一轮）
            votes.Clear();

            return result;
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

        /// <summary>
        /// 绑定外部投票字典（由 OnlineMatchController 调用）。
        /// 使 VotingService 与 Controller 共享同一份 votes 引用，
        /// 确保反射访问 controller.votes 和 VotingService 读取到相同数据。
        /// </summary>
        internal void BindVotes(Dictionary<ulong, ulong> controllerVotes)
        {
            if (controllerVotes != null)
            {
                votes = controllerVotes;
            }
        }

        /// <summary>
        /// 从快照数据恢复投票记录（主机迁移 / 客户端同步用）。
        /// </summary>
        public void LoadVotes(IEnumerable<KeyValuePair<ulong, ulong>> snapshotVotes)
        {
            votes.Clear();
            foreach (var kv in snapshotVotes)
            {
                votes[kv.Key] = kv.Value;
            }
        }

        // ─── 内部方法 ──────────────────────────────────────────

        /// <summary>会议开始时清空投票记录。</summary>
        private void OnMeetingCalled(MeetingCalledEvent evt)
        {
            ClearVotes();
        }

        private void SubscribeToEventBus()
        {
            if (eventBus == null || subscribedEventBus == eventBus)
            {
                return;
            }

            eventBus.Subscribe<MeetingCalledEvent>(OnMeetingCalled);
            subscribedEventBus = eventBus;
        }

        private void UnsubscribeFromEventBus()
        {
            if (subscribedEventBus == null)
            {
                return;
            }

            subscribedEventBus.Unsubscribe<MeetingCalledEvent>(OnMeetingCalled);
            subscribedEventBus = null;
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
