using System;
using System.Collections.Generic;
using GanglandUndercover;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机同步管理器：在 OnlineMatchController 旁运行，统筹 TaskSync / MeetingSync /
    /// PlayerStateSync / OnlineVictoryBridge，串联起联机模式的任务分配、会议流程、
    /// 状态同步与胜负判定。
    ///
    /// 使用方式：由 PrototypeBootstrap.BuildOnlinePrototype 自动挂载到
    /// "Port Undercover Online" GameObject，在 OnlineMatchController 之后。
    /// </summary>
    [RequireComponent(typeof(OnlineMatchController))]
    public sealed class OnlineSyncManager : MonoBehaviour
    {
        private OnlineMatchController matchController;
        private TaskSync taskSync;
        private MeetingSync meetingSync;
        private PlayerStateSync playerStateSync;
        private OnlineVictoryBridge victoryBridge;

        private float syncTickTimer;

        public TaskSync TaskSync => taskSync;
        public MeetingSync MeetingSync => meetingSync;
        public PlayerStateSync PlayerStateSync => playerStateSync;
        public OnlineVictoryBridge VictoryBridge => victoryBridge;

        // ------ Unity lifecycle ------

        private void Awake()
        {
            matchController = GetComponent<OnlineMatchController>();
            if (matchController == null)
            {
                Debug.LogError("[OnlineSyncManager] 未找到 OnlineMatchController，同步系统不可用。");
                enabled = false;
                return;
            }

            taskSync = new TaskSync(msg => { /* caseLog 写入由控制器负责 */ });
            meetingSync = new MeetingSync(msg => { });
            playerStateSync = new PlayerStateSync(msg => { });
            victoryBridge = new OnlineVictoryBridge();

            // 订阅会议事件
            meetingSync.MeetingStarted += OnMeetingStarted;
            meetingSync.VoteResolved += OnVoteResolved;

            // 订阅状态变更事件
            playerStateSync.PlayerAliveChanged += OnPlayerAliveChanged;
            playerStateSync.PlayerRoleChanged += OnPlayerRoleChanged;

            syncTickTimer = 0f;
        }

        private void Update()
        {
            if (matchController == null || !matchController.enabled) return;

            // 周期性检测状态变更（比 Snapshot 频率低的轻量巡检）
            syncTickTimer += Time.deltaTime;
            if (syncTickTimer >= 1.5f)
            {
                syncTickTimer = 0f;
                // 状态检测（需访问内部 players 字典，由反射或公开 API 完成）
                // playerStateSync.DetectChanges(...);
            }
        }

        // ------ 公开 API：由 OnlineMatchController 或外部调用 ------

        /// <summary>
        /// 在 StartOnlineMatchCore 中调用：分配任务池。
        /// </summary>
        public void OnMatchStarted(
            IList<ulong> gangIds,
            IList<ulong> nonGangIds,
            IReadOnlyList<OnlineTaskState> tasks)
        {
            int taskQuota = matchController != null && matchController.RuleSet != null
                ? matchController.RuleSet.TasksPerNonGangPlayer
                : 4;
            taskSync.AssignTasks(gangIds, nonGangIds, tasks, tasksPerPlayer: taskQuota);
            taskSync.MarkCompletable(GetNonSabotageTaskIds(tasks));
            victoryBridge.ClearEliminations();
        }

        /// <summary>
        /// 任务完成时调用。
        /// </summary>
        public void OnTaskCompletedLocally(ulong playerId, int taskId)
        {
            taskSync.OnTaskCompleted(playerId, taskId);
        }

        /// <summary>
        /// 任务破坏时调用。
        /// </summary>
        public void OnTaskSabotagedLocally(ulong playerId, int taskId, SabotageType type)
        {
            taskSync.OnTaskSabotaged(playerId, taskId, type);
        }

        /// <summary>
        /// 会议开始：记录原因并通知监听者。
        /// </summary>
        public void OnMeetingBegan(string reason, OnlineMatchPhase currentPhase)
        {
            meetingSync.Begin(reason, currentPhase);
        }

        /// <summary>
        /// 投票寄存器（Host 侧在 ApplyVote 中调用）。
        /// </summary>
        public void OnVoteCast(ulong voterId, ulong targetId)
        {
            meetingSync.RegisterVote(voterId, targetId);
        }

        /// <summary>
        /// 投票结算（Host 侧在 ResolveVotes 中调用）。
        /// </summary>
        public void OnMeetingResolved(ulong ejectedClientId, bool tied, Dictionary<ulong, int> tally)
        {
            meetingSync.Resolve(ejectedClientId, tied, tally);
        }

        /// <summary>
        /// 会议结束（在 ResolveVotes 清理后调用）。
        /// </summary>
        public void OnMeetingEnded()
        {
            meetingSync.End();
        }

        /// <summary>
        /// 击杀记录。
        /// </summary>
        public void OnKilled(ulong victimId, ulong killerId)
        {
            playerStateSync.RecordKill(victimId, killerId);
        }

        /// <summary>
        /// 完整的胜负判定（原生在线 + 离线 VictoryEvaluator 双重）。
        /// </summary>
        public EvaluateResult EvaluateVictory(
            int evidenceScore,
            int evidenceTarget,
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            Func<ulong, OnlineRole> getPrivateRole,
            IReadOnlyList<OnlineTaskState> tasks,
            bool matchStarted,
            OnlineMatchPhase phase,
            OnlineRole localRole = OnlineRole.Police)
        {
            return victoryBridge.Evaluate(
                evidenceScore, evidenceTarget, players,
                getPrivateRole, tasks, matchStarted, phase, localRole);
        }

        /// <summary>
        /// 超时判定。
        /// </summary>
        public bool TryTimeLimitEvaluation(
            float elapsed,
            float limit,
            int evidenceScore,
            int evidenceTarget,
            IReadOnlyList<OnlineTaskState> tasks,
            out string result)
        {
            return victoryBridge.TryTimeLimitEvaluation(elapsed, limit, evidenceScore, evidenceTarget, tasks, out result);
        }

        /// <summary>
        /// 注册投票淘汰到 VictoryBridge。
        /// </summary>
        public void RegisterElimination(ulong ejectedClientId, Func<ulong, OnlineRole> getPrivateRole)
        {
            victoryBridge.RegisterMeetingElimination(ejectedClientId, getPrivateRole);
        }

        // ------ 事件处理 ------

        private void OnMeetingStarted(string reason, string phase)
        {
            Debug.Log($"[OnlineSyncManager] 会议开始: {reason}");
        }

        private void OnVoteResolved(string outcome, ulong ejectedClientId)
        {
            Debug.Log($"[OnlineSyncManager] 投票结果: {outcome}");
        }

        private void OnPlayerAliveChanged(ulong playerId, OnlinePlayerState state)
        {
            Debug.Log($"[OnlineSyncManager] 玩家 {state.DisplayName} 存活变更: {state.Alive}");
        }

        private void OnPlayerRoleChanged(ulong playerId, OnlineRole oldRole, OnlineRole newRole)
        {
            Debug.Log($"[OnlineSyncManager] 玩家 {playerId} 角色变更: {oldRole} → {newRole}");
        }

        // ------ Helpers ------

        private static List<int> GetNonSabotageTaskIds(IReadOnlyList<OnlineTaskState> tasks)
        {
            List<int> ids = new List<int>();
            foreach (var t in tasks)
            {
                if (!IsSabotageTask(t.Id))
                    ids.Add(t.Id);
            }
            return ids;
        }

        private static bool IsSabotageTask(int taskId)
        {
            switch (taskId)
            {
                case 2: case 14: case 7: case 12: case 6: case 13:
                case 20: case 21: case 27: case 3: case 11: case 16:
                case 22: case 23: case 25: case 4: case 10: case 17:
                case 24: case 26:
                    return true;
                default:
                    return false;
            }
        }
    }
}
