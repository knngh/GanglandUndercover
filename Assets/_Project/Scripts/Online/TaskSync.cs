using System;
using System.Collections.Generic;
using GanglandUndercover;
using System.Linq;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机任务同步机制：Host 分配任务 → 所有客户端通过 BroadcastSnapshot 同步。
    /// 包裹 OnlineMatchController 中的任务数据，提供分配分发与进度校验。
    ///
    /// 使用方式：OnlineMatchController 在 StartOnlineMatchCore 中调用
    /// taskSync.AssignTasks(...) 分配初始任务，后续 OnTaskProgress/OnTaskSabotaged 记录进展。
    /// 快照同步由 BroadcastSnapshot 自带，本类提供语义层包装。
    /// </summary>
    public sealed class TaskSync
    {
        public const int MaxTasksPerPlayer = 8;

        private readonly Action<string> addCaseLog;
        private readonly HashSet<int> completableTasks;

        /// <summary> playerId → 该玩家被分配的任务 ID 集合 </summary>
        private readonly Dictionary<ulong, HashSet<int>> playerTaskAssignments;
        /// <summary> playerId → taskId → 该玩家自己的任务进度。 </summary>
        private readonly Dictionary<ulong, Dictionary<int, PlayerTaskProgress>> playerTaskProgress;
        private int taskQuota = 4;

        private struct PlayerTaskProgress
        {
            public int Progress;
            public int RequiredProgress;
            public bool Completed;
        }

        public TaskSync(Action<string> addCaseLog)
        {
            this.addCaseLog = addCaseLog ?? (_ => { });
            completableTasks = new HashSet<int>();
            playerTaskAssignments = new Dictionary<ulong, HashSet<int>>();
            playerTaskProgress = new Dictionary<ulong, Dictionary<int, PlayerTaskProgress>>();
        }

        public IReadOnlyDictionary<ulong, HashSet<int>> PlayerAssignments => playerTaskAssignments;
        public bool HasAssignments => playerTaskAssignments.Count > 0;

        public bool IsTaskAssignedTo(ulong playerId, int taskId)
        {
            return playerTaskAssignments.TryGetValue(playerId, out HashSet<int> assigned)
                && assigned.Contains(taskId);
        }

        public OnlineTaskState TaskForPlayer(ulong playerId, OnlineTaskState facilityTask)
        {
            if (!HasAssignments || !IsTaskAssignedTo(playerId, facilityTask.Id))
                return facilityTask;

            PlayerTaskProgress state = GetOrCreateProgress(playerId, facilityTask.Id,
                facilityTask.Progress, facilityTask.RequiredProgress, facilityTask.Completed);
            facilityTask.Progress = state.Progress;
            facilityTask.RequiredProgress = state.RequiredProgress;
            facilityTask.Completed = state.Completed;
            return facilityTask;
        }

        public bool IsTaskCompletedBy(ulong playerId, OnlineTaskState facilityTask)
        {
            return TaskForPlayer(playerId, facilityTask).Completed;
        }

        public bool TryAdvanceTask(ulong playerId, OnlineTaskState facilityTask, int amount,
            out OnlineTaskState playerTask)
        {
            playerTask = TaskForPlayer(playerId, facilityTask);
            if (!HasAssignments || !IsTaskAssignedTo(playerId, facilityTask.Id) || playerTask.Completed)
                return false;

            playerTask.Progress = Math.Min(playerTask.RequiredProgress,
                Math.Max(0, playerTask.Progress + Math.Max(0, amount)));
            playerTask.Completed = playerTask.Progress >= playerTask.RequiredProgress;
            SetProgress(playerId, playerTask);
            return true;
        }

        public int AssignedCountFor(ulong playerId)
        {
            return playerTaskAssignments.TryGetValue(playerId, out HashSet<int> assigned)
                ? assigned.Count : 0;
        }

        public int CompletedCountFor(ulong playerId)
        {
            if (!playerTaskProgress.TryGetValue(playerId, out Dictionary<int, PlayerTaskProgress> progress))
                return 0;
            return progress.Values.Count(state => state.Completed);
        }

        public int TotalAssignedCount()
        {
            return playerTaskAssignments.Values.Sum(assigned => assigned.Count);
        }

        public List<GameStateSnapshot.SnapshotTaskAssignmentEntry> ExportAssignments()
        {
            var result = new List<GameStateSnapshot.SnapshotTaskAssignmentEntry>();
            foreach (KeyValuePair<ulong, HashSet<int>> pair in playerTaskAssignments)
            {
                foreach (int taskId in pair.Value)
                {
                    PlayerTaskProgress state = GetProgress(pair.Key, taskId);
                    result.Add(new GameStateSnapshot.SnapshotTaskAssignmentEntry
                    {
                        ClientId = pair.Key,
                        TaskId = taskId,
                        Progress = state.Progress,
                        RequiredProgress = state.RequiredProgress,
                        Completed = state.Completed,
                    });
                }
            }

            result.Sort((a, b) =>
            {
                int clientCompare = a.ClientId.CompareTo(b.ClientId);
                return clientCompare != 0 ? clientCompare : a.TaskId.CompareTo(b.TaskId);
            });
            return result;
        }

        public void LoadAssignments(IReadOnlyList<GameStateSnapshot.SnapshotTaskAssignmentEntry> assignments)
        {
            playerTaskAssignments.Clear();
            playerTaskProgress.Clear();
            if (assignments == null) return;

            for (int i = 0; i < assignments.Count; i++)
            {
                GameStateSnapshot.SnapshotTaskAssignmentEntry entry = assignments[i];
                if (!playerTaskAssignments.TryGetValue(entry.ClientId, out HashSet<int> assigned))
                {
                    assigned = new HashSet<int>();
                    playerTaskAssignments.Add(entry.ClientId, assigned);
                }

                if (assigned.Count < MaxTasksPerPlayer)
                {
                    assigned.Add(entry.TaskId);
                    if (!playerTaskProgress.TryGetValue(entry.ClientId, out Dictionary<int, PlayerTaskProgress> progress))
                    {
                        progress = new Dictionary<int, PlayerTaskProgress>();
                        playerTaskProgress.Add(entry.ClientId, progress);
                    }

                    int required = entry.RequiredProgress > 0
                        ? entry.RequiredProgress
                        : OnlineMatchUtils.TaskRequiredProgress(entry.TaskId);
                    required = Math.Max(1, Math.Min(128, required));
                    int current = Math.Max(0, Math.Min(required, entry.Progress));
                    progress[entry.TaskId] = new PlayerTaskProgress
                    {
                        Progress = entry.Completed ? required : current,
                        RequiredProgress = required,
                        Completed = entry.Completed || current >= required,
                    };
                }
            }
        }

        // ------ 任务分配 (Host 调用) ------

        /// <summary>
        /// 按公开伪装身份分配任务：Gang 外观接收 sabotage 池，Police 外观接收调查/维修池。
        /// </summary>
        public void AssignTasks(
            IList<ulong> gangPlayerIds,
            IList<ulong> nonGangPlayerIds,
            IReadOnlyList<OnlineTaskState> allTasks,
            System.Random random = null,
            int tasksPerPlayer = 4)
        {
            random ??= new System.Random();
            taskQuota = Math.Max(2, Math.Min(MaxTasksPerPlayer, tasksPerPlayer));
            playerTaskAssignments.Clear();
            playerTaskProgress.Clear();

            List<int> sabotagePool = new List<int>();
            List<int> investigatePool = new List<int>();
            List<int> hybridPool = new List<int>();

            for (int i = 0; i < allTasks.Count; i++)
            {
                SabotageType type = SabotageForTask(allTasks[i].Id);
                if (type == SabotageType.None)
                    investigatePool.Add(allTasks[i].Id);
                else
                {
                    sabotagePool.Add(allTasks[i].Id);
                    if (random.Next(0, 3) == 0)
                        hybridPool.Add(allTasks[i].Id);
                }
            }

            Shuffle(sabotagePool, random);
            Shuffle(investigatePool, random);
            Shuffle(hybridPool, random);

            // 每名玩家独立抽取，允许不同玩家共享任务站；先保证至少一个公开身份基础任务，
            // 再用基础池与 hybrid 池补足当前规则配额。
            AssignToPlayers(gangPlayerIds, sabotagePool, hybridPool, random);
            AssignToPlayers(nonGangPlayerIds, investigatePool, hybridPool, random);
            InitializeProgress(allTasks);

            addCaseLog($"TaskSync: 已为 {gangPlayerIds.Count} 名黑帮 / {nonGangPlayerIds.Count} 名警员分配任务。");
        }

        private void AssignToPlayers(
            IList<ulong> playerIds,
            List<int> primaryPool,
            List<int> supplementalPool,
            System.Random random)
        {
            if (playerIds.Count == 0 || primaryPool.Count == 0) return;

            for (int p = 0; p < playerIds.Count; p++)
            {
                ulong playerId = playerIds[p];
                var assigned = new HashSet<int>();
                playerTaskAssignments[playerId] = assigned;

                List<int> primaryCandidates = new List<int>(primaryPool);
                Shuffle(primaryCandidates, random);
                assigned.Add(primaryCandidates[0]);

                List<int> candidates = new List<int>(primaryCandidates.Count + supplementalPool.Count);
                candidates.AddRange(primaryCandidates);
                candidates.AddRange(supplementalPool);
                Shuffle(candidates, random);

                for (int i = 0; i < candidates.Count && assigned.Count < taskQuota; i++)
                {
                    assigned.Add(candidates[i]);
                }
            }
        }

        // ------ 进度追踪 ------

        public void MarkCompletable(IEnumerable<int> taskIds)
        {
            completableTasks.Clear();
            foreach (int id in taskIds) completableTasks.Add(id);
        }

        public bool CanComplete(int taskId) => completableTasks.Contains(taskId);

        public void OnTaskCompleted(ulong playerId, int taskId)
        {
            if (playerTaskAssignments.TryGetValue(playerId, out HashSet<int> assigned)
                && assigned.Contains(taskId))
            {
                PlayerTaskProgress state = GetOrCreateProgress(playerId, taskId, 0, 1, false);
                state.Progress = state.RequiredProgress;
                state.Completed = true;
                SetProgress(playerId, taskId, state);
            }

            addCaseLog($"TaskSync: 玩家 {playerId} 完成任务 {taskId}。");
        }

        public void OnTaskSabotaged(ulong playerId, int taskId, SabotageType sabotageType)
        {
            addCaseLog($"TaskSync: 黑帮 {playerId} 破坏任务 {taskId} ({sabotageType})。");
            completableTasks.Remove(taskId);
        }

        public void OnTaskRepaired(int taskId)
        {
            completableTasks.Add(taskId);
        }

        // ------ 统计 ------

        public int CompletedCount(IReadOnlyList<OnlineTaskState> tasks)
        {
            return HasAssignments
                ? playerTaskProgress.Values.Sum(progress => progress.Values.Count(state => state.Completed))
                : tasks.Count(t => t.Completed);
        }
        public int SabotagedCount(IReadOnlyList<OnlineTaskState> tasks) => tasks.Count(t => t.Sabotaged);
        public int RemainingCompletable() => completableTasks.Count;

        public bool AllCompletableDone(IReadOnlyList<OnlineTaskState> tasks)
        {
            return completableTasks.All(id =>
            {
                foreach (var t in tasks)
                    if (t.Id == id && t.Completed) return true;
                return false;
            });
        }

        // ------ Helpers ------

        private static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void InitializeProgress(IReadOnlyList<OnlineTaskState> allTasks)
        {
            foreach (KeyValuePair<ulong, HashSet<int>> pair in playerTaskAssignments)
            {
                foreach (int taskId in pair.Value)
                {
                    OnlineTaskState task = default;
                    for (int i = 0; i < allTasks.Count; i++)
                    {
                        if (allTasks[i].Id == taskId)
                        {
                            task = allTasks[i];
                            break;
                        }
                    }

                    int required = task.RequiredProgress > 0
                        ? task.RequiredProgress
                        : OnlineMatchUtils.TaskRequiredProgress(taskId);
                    GetOrCreateProgress(pair.Key, taskId, task.Progress, required, task.Completed);
                }
            }
        }

        private PlayerTaskProgress GetProgress(ulong playerId, int taskId)
        {
            return playerTaskProgress.TryGetValue(playerId, out Dictionary<int, PlayerTaskProgress> progress)
                && progress.TryGetValue(taskId, out PlayerTaskProgress state)
                ? state
                : new PlayerTaskProgress { Progress = 0, RequiredProgress = 1, Completed = false };
        }

        private PlayerTaskProgress GetOrCreateProgress(ulong playerId, int taskId,
            int defaultProgress, int defaultRequiredProgress, bool defaultCompleted)
        {
            if (!playerTaskProgress.TryGetValue(playerId, out Dictionary<int, PlayerTaskProgress> progress))
            {
                progress = new Dictionary<int, PlayerTaskProgress>();
                playerTaskProgress.Add(playerId, progress);
            }

            if (!progress.TryGetValue(taskId, out PlayerTaskProgress state))
            {
                int required = Math.Max(1, Math.Min(128, defaultRequiredProgress));
                int current = Math.Max(0, Math.Min(required, defaultProgress));
                state = new PlayerTaskProgress
                {
                    Progress = defaultCompleted ? required : current,
                    RequiredProgress = required,
                    Completed = defaultCompleted || current >= required,
                };
                progress.Add(taskId, state);
            }

            return state;
        }

        private void SetProgress(ulong playerId, OnlineTaskState playerTask)
        {
            SetProgress(playerId, playerTask.Id, new PlayerTaskProgress
            {
                Progress = playerTask.Progress,
                RequiredProgress = playerTask.RequiredProgress,
                Completed = playerTask.Completed,
            });
        }

        private void SetProgress(ulong playerId, int taskId, PlayerTaskProgress state)
        {
            if (!playerTaskProgress.TryGetValue(playerId, out Dictionary<int, PlayerTaskProgress> progress))
            {
                progress = new Dictionary<int, PlayerTaskProgress>();
                playerTaskProgress.Add(playerId, progress);
            }

            progress[taskId] = state;
        }

        private static SabotageType SabotageForTask(int taskId)
        {
            switch (taskId)
            {
                case 2: case 14: return SabotageType.Blackout;
                case 7: case 12: return SabotageType.Lockdown;
                case 6: case 13: case 20: case 21: case 27: return SabotageType.Communications;
                case 3: case 11: case 16: case 22: case 23: case 25: return SabotageType.EvidenceLeak;
                case 4: case 10: case 17: case 24: case 26: return SabotageType.PatrolAlert;
                default: return SabotageType.None;
            }
        }
    }
}
