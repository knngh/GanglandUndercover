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
        public const int MaxTasksPerPlayer = 5;

        private readonly Action<string> addCaseLog;
        private readonly HashSet<int> completableTasks;

        /// <summary> playerId → 该玩家被分配的任务 ID 集合 </summary>
        private readonly Dictionary<ulong, HashSet<int>> playerTaskAssignments;

        public TaskSync(Action<string> addCaseLog)
        {
            this.addCaseLog = addCaseLog ?? (_ => { });
            completableTasks = new HashSet<int>();
            playerTaskAssignments = new Dictionary<ulong, HashSet<int>>();
        }

        public IReadOnlyDictionary<ulong, HashSet<int>> PlayerAssignments => playerTaskAssignments;

        // ------ 任务分配 (Host 调用) ------

        /// <summary>
        /// 按阵营分配任务：Gang 接收 sabotage 池，非 Gang 接收调查/维修池，双方共享少量 hybrid 任务。
        /// </summary>
        public void AssignTasks(
            IList<ulong> gangPlayerIds,
            IList<ulong> nonGangPlayerIds,
            IReadOnlyList<OnlineTaskState> allTasks,
            System.Random random = null)
        {
            random ??= new System.Random();
            playerTaskAssignments.Clear();

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

            DistributeToPlayers(gangPlayerIds, sabotagePool.Concat(hybridPool).Distinct().ToList(), random);
            DistributeToPlayers(nonGangPlayerIds, investigatePool.Concat(hybridPool).Distinct().ToList(), random);

            addCaseLog($"TaskSync: 已为 {gangPlayerIds.Count} 名黑帮 / {nonGangPlayerIds.Count} 名警员分配任务。");
        }

        private void DistributeToPlayers(IList<ulong> playerIds, List<int> taskPool, System.Random random)
        {
            if (playerIds.Count == 0 || taskPool.Count == 0) return;

            int perPlayer = Mathf.CeilToInt((float)taskPool.Count / playerIds.Count);
            Shuffle(playerIds, random); // 轮转分配

            for (int p = 0; p < playerIds.Count; p++)
            {
                HashSet<int> assigned = new HashSet<int>();
                int start = p * perPlayer;
                for (int t = start; t < start + perPlayer && t < taskPool.Count; t++)
                {
                    assigned.Add(taskPool[t]);
                }

                if (assigned.Count > 0)
                {
                    playerTaskAssignments[playerIds[p]] = assigned;
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
            addCaseLog($"TaskSync: 玩家 {playerId} 完成任务 {taskId}。");
            completableTasks.Remove(taskId);
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

        public int CompletedCount(IReadOnlyList<OnlineTaskState> tasks) => tasks.Count(t => t.Completed);
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
