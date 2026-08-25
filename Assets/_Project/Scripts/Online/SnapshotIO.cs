using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// SnapshotIO — 快照序列化共享读写 helper。
    /// 统一 BroadcastSnapshot（Path A）和 GameStateSnapshot.ToBytes（Path B）中
    /// 格式完全一致的实体段（Task / Body / Vote / CaseLog / Player-Broadcast），
    /// 消除"新增字段需改 3 处"的维护风险。
    ///
    /// 设计约束：
    /// - 纯序列化，不含游戏逻辑（enum 校验由调用方负责）
    /// - 写入顺序即权威格式定义，修改时需同时更新两条路径的调用方
    /// - Player 有两种格式：Broadcast（含 VentCooldown）和 Migration（含 Input）
    /// </summary>
    public static class SnapshotIO
    {
        // ─── Task（Broadcast 格式：不含 Name） ─────────────────

        public static void WriteTasks(FastBufferWriter writer, IReadOnlyList<OnlineTaskState> tasks)
        {
            writer.WriteValueSafe(tasks.Count);
            for (int i = 0; i < tasks.Count; i++)
            {
                OnlineTaskState task = tasks[i];
                writer.WriteValueSafe(task.Id);
                writer.WriteValueSafe(task.Position);
                writer.WriteValueSafe(task.Progress);
                writer.WriteValueSafe(task.RequiredProgress);
                writer.WriteValueSafe(task.Completed);
                writer.WriteValueSafe(task.Sabotaged);
            }
        }

        public static List<OnlineTaskState> ReadTasks(FastBufferReader reader, int count)
        {
            List<OnlineTaskState> tasks = new List<OnlineTaskState>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out int progress);
                reader.ReadValueSafe(out int requiredProgress);
                reader.ReadValueSafe(out bool completed);
                reader.ReadValueSafe(out bool sabotaged);
                tasks.Add(new OnlineTaskState(id, OnlineWorldBuilder.TaskNameFor(id), position,
                    progress, requiredProgress, completed, sabotaged));
            }
            return tasks;
        }

        public static void WriteTaskAssignments(FastBufferWriter writer, IReadOnlyList<GameStateSnapshot.SnapshotTaskAssignmentEntry> assignments)
        {
            writer.WriteValueSafe(assignments.Count);
            for (int i = 0; i < assignments.Count; i++)
            {
                writer.WriteValueSafe(assignments[i].ClientId);
                writer.WriteValueSafe(assignments[i].TaskId);
                writer.WriteValueSafe(assignments[i].Progress);
                writer.WriteValueSafe(assignments[i].RequiredProgress);
                writer.WriteValueSafe(assignments[i].Completed);
            }
        }

        public static List<GameStateSnapshot.SnapshotTaskAssignmentEntry> ReadTaskAssignments(FastBufferReader reader, int count)
        {
            var assignments = new List<GameStateSnapshot.SnapshotTaskAssignmentEntry>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out int taskId);
                reader.ReadValueSafe(out int progress);
                reader.ReadValueSafe(out int requiredProgress);
                reader.ReadValueSafe(out bool completed);
                assignments.Add(new GameStateSnapshot.SnapshotTaskAssignmentEntry
                {
                    ClientId = clientId,
                    TaskId = taskId,
                    Progress = progress,
                    RequiredProgress = requiredProgress,
                    Completed = completed,
                });
            }

            return assignments;
        }

        public static List<GameStateSnapshot.SnapshotTaskAssignmentEntry> ReadLegacyTaskAssignments(
            FastBufferReader reader, int count)
        {
            var assignments = new List<GameStateSnapshot.SnapshotTaskAssignmentEntry>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out int taskId);
                assignments.Add(new GameStateSnapshot.SnapshotTaskAssignmentEntry
                {
                    ClientId = clientId,
                    TaskId = taskId,
                    RequiredProgress = OnlineMatchUtils.TaskRequiredProgress(taskId),
                });
            }

            return assignments;
        }

        /// <summary>写入任务（Migration 格式，含 Name 字段）。</summary>
        public static void WriteTasksMigration(FastBufferWriter writer, IReadOnlyList<GameStateSnapshot.SnapshotTaskEntry> tasks)
        {
            writer.WriteValueSafe(tasks.Count);
            for (int i = 0; i < tasks.Count; i++)
            {
                var t = tasks[i];
                writer.WriteValueSafe(t.Id);
                writer.WriteValueSafe(t.Name ?? string.Empty);
                writer.WriteValueSafe(t.Position);
                writer.WriteValueSafe(t.Progress);
                writer.WriteValueSafe(t.RequiredProgress);
                writer.WriteValueSafe(t.Completed);
                writer.WriteValueSafe(t.Sabotaged);
            }
        }

        /// <summary>读取任务（Migration 格式，返回 SnapshotTaskEntry）。</summary>
        public static List<GameStateSnapshot.SnapshotTaskEntry> ReadTasksAsEntries(FastBufferReader reader, int count)
        {
            var tasks = new List<GameStateSnapshot.SnapshotTaskEntry>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out string name);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out int progress);
                reader.ReadValueSafe(out int requiredProgress);
                reader.ReadValueSafe(out bool completed);
                reader.ReadValueSafe(out bool sabotaged);
                tasks.Add(new GameStateSnapshot.SnapshotTaskEntry
                {
                    Id = id, Name = name, Position = position,
                    Progress = progress, RequiredProgress = requiredProgress,
                    Completed = completed, Sabotaged = sabotaged
                });
            }
            return tasks;
        }

        // ─── Body ──────────────────────────────────────────────

        public static void WriteBodies(FastBufferWriter writer, IReadOnlyList<OnlineBodyState> bodies)
        {
            writer.WriteValueSafe(bodies.Count);
            for (int i = 0; i < bodies.Count; i++)
            {
                OnlineBodyState body = bodies[i];
                writer.WriteValueSafe(body.Id);
                writer.WriteValueSafe(body.VictimClientId);
                writer.WriteValueSafe(body.Position);
                writer.WriteValueSafe(body.Reported);
            }
        }

        public static List<OnlineBodyState> ReadBodies(FastBufferReader reader, int count)
        {
            List<OnlineBodyState> bodies = new List<OnlineBodyState>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out ulong victimClientId);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out bool reported);
                bodies.Add(new OnlineBodyState(id, victimClientId, position, reported));
            }
            return bodies;
        }

        /// <summary>写入尸体（Migration 格式）。线格式与 Broadcast 一致。</summary>
        public static void WriteBodies(FastBufferWriter writer, IReadOnlyList<GameStateSnapshot.SnapshotBodyEntry> bodies)
        {
            writer.WriteValueSafe(bodies.Count);
            for (int i = 0; i < bodies.Count; i++)
            {
                var b = bodies[i];
                writer.WriteValueSafe(b.Id);
                writer.WriteValueSafe(b.VictimClientId);
                writer.WriteValueSafe(b.Position);
                writer.WriteValueSafe(b.Reported);
            }
        }

        /// <summary>读取尸体（Migration 格式，返回 SnapshotBodyEntry）。</summary>
        public static List<GameStateSnapshot.SnapshotBodyEntry> ReadBodiesAsEntries(FastBufferReader reader, int count)
        {
            var bodies = new List<GameStateSnapshot.SnapshotBodyEntry>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out ulong victimClientId);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out bool reported);
                bodies.Add(new GameStateSnapshot.SnapshotBodyEntry { Id = id, VictimClientId = victimClientId, Position = position, Reported = reported });
            }
            return bodies;
        }

        // ─── Votes ─────────────────────────────────────────────

        public static void WriteVotes(FastBufferWriter writer, IReadOnlyDictionary<ulong, ulong> votes)
        {
            writer.WriteValueSafe(votes.Count);
            foreach (KeyValuePair<ulong, ulong> vote in votes)
            {
                writer.WriteValueSafe(vote.Key);
                writer.WriteValueSafe(vote.Value);
            }
        }

        public static Dictionary<ulong, ulong> ReadVotes(FastBufferReader reader, int count)
        {
            Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong voterClientId);
                reader.ReadValueSafe(out ulong targetClientId);
                votes[voterClientId] = targetClientId;
            }
            return votes;
        }

        /// <summary>写入投票（Migration 格式）。线格式与 Broadcast 一致。</summary>
        public static void WriteVotes(FastBufferWriter writer, IReadOnlyList<GameStateSnapshot.SnapshotVoteEntry> votes)
        {
            writer.WriteValueSafe(votes.Count);
            for (int i = 0; i < votes.Count; i++)
            {
                writer.WriteValueSafe(votes[i].VoterClientId);
                writer.WriteValueSafe(votes[i].TargetClientId);
            }
        }

        /// <summary>读取投票（Migration 格式，返回 SnapshotVoteEntry）。</summary>
        public static List<GameStateSnapshot.SnapshotVoteEntry> ReadVotesAsEntries(FastBufferReader reader, int count)
        {
            var votes = new List<GameStateSnapshot.SnapshotVoteEntry>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong voterClientId);
                reader.ReadValueSafe(out ulong targetClientId);
                votes.Add(new GameStateSnapshot.SnapshotVoteEntry { VoterClientId = voterClientId, TargetClientId = targetClientId });
            }
            return votes;
        }

        // ─── Case Log ──────────────────────────────────────────

        public static void WriteCaseLog(FastBufferWriter writer, IReadOnlyList<string> caseLog)
        {
            writer.WriteValueSafe(caseLog.Count);
            for (int i = 0; i < caseLog.Count; i++)
            {
                writer.WriteValueSafe(caseLog[i] ?? string.Empty);
            }
        }

        public static List<string> ReadCaseLog(FastBufferReader reader, int count)
        {
            List<string> caseLog = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out string entry);
                caseLog.Add(entry);
            }
            return caseLog;
        }

        // ─── Player (Broadcast 格式) ───────────────────────────
        // 字段顺序：ClientId, DisplayName, Position, Ready, Alive, IsBot,
        //           PublicRole(int), Profession(int), Suspicion,
        //           KillCooldown, AbilityCooldown, VentCooldown

        public static void WritePlayerBroadcast(FastBufferWriter writer, OnlinePlayerState state,
            float killCooldown, float abilityCooldown, float ventCooldown)
        {
            writer.WriteValueSafe(state.ClientId);
            writer.WriteValueSafe(state.DisplayName);
            writer.WriteValueSafe(state.Position);
            writer.WriteValueSafe(state.Ready);
            writer.WriteValueSafe(state.Alive);
            writer.WriteValueSafe(state.IsBot);
            writer.WriteValueSafe((int)state.PublicRole);
            writer.WriteValueSafe((int)state.Profession);
            writer.WriteValueSafe(state.Suspicion);
            writer.WriteValueSafe(killCooldown);
            writer.WriteValueSafe(abilityCooldown);
            writer.WriteValueSafe(ventCooldown);
        }

        /// <summary>读取玩家（Broadcast 格式）。返回原始值，enum 校验由调用方负责。</summary>
        public static void ReadPlayerBroadcast(FastBufferReader reader,
            out ulong clientId, out string displayName, out Vector3 position,
            out bool ready, out bool alive, out bool isBot,
            out int roleValue, out int professionValue,
            out int suspicion, out float killCooldown, out float abilityCooldown, out float ventCooldown)
        {
            reader.ReadValueSafe(out clientId);
            reader.ReadValueSafe(out displayName);
            reader.ReadValueSafe(out position);
            reader.ReadValueSafe(out ready);
            reader.ReadValueSafe(out alive);
            reader.ReadValueSafe(out isBot);
            reader.ReadValueSafe(out roleValue);
            reader.ReadValueSafe(out professionValue);
            reader.ReadValueSafe(out suspicion);
            reader.ReadValueSafe(out killCooldown);
            reader.ReadValueSafe(out abilityCooldown);
            reader.ReadValueSafe(out ventCooldown);
        }

        // ─── Player (Migration 格式) ─────────────────────────────
        // 字段顺序：ClientId, DisplayName, Position, Input(Vector2), Ready, Alive, IsBot,
        //           PublicRole(int), Profession(int), KillCooldown, AbilityCooldown, Suspicion

        /// <summary>写入玩家（Migration 格式，含 Input，无 VentCooldown）。</summary>
        public static void WritePlayerMigration(FastBufferWriter writer, GameStateSnapshot.SnapshotPlayerEntry p)
        {
            writer.WriteValueSafe(p.ClientId);
            writer.WriteValueSafe(p.DisplayName ?? string.Empty);
            writer.WriteValueSafe(p.Position);
            writer.WriteValueSafe(p.Input);
            writer.WriteValueSafe(p.Ready);
            writer.WriteValueSafe(p.Alive);
            writer.WriteValueSafe(p.IsBot);
            writer.WriteValueSafe((int)p.PublicRole);
            writer.WriteValueSafe((int)p.Profession);
            writer.WriteValueSafe(p.KillCooldown);
            writer.WriteValueSafe(p.AbilityCooldown);
            writer.WriteValueSafe(p.Suspicion);
        }

        /// <summary>读取玩家（Migration 格式，返回 SnapshotPlayerEntry）。</summary>
        public static GameStateSnapshot.SnapshotPlayerEntry ReadPlayerMigration(FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out string displayName);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Vector2 input);
            reader.ReadValueSafe(out bool ready);
            reader.ReadValueSafe(out bool alive);
            reader.ReadValueSafe(out bool isBot);
            reader.ReadValueSafe(out int roleValue);
            reader.ReadValueSafe(out int professionValue);
            reader.ReadValueSafe(out float killCooldown);
            reader.ReadValueSafe(out float abilityCooldown);
            reader.ReadValueSafe(out int suspicion);

            return new GameStateSnapshot.SnapshotPlayerEntry
            {
                ClientId = clientId,
                DisplayName = displayName,
                Position = position,
                Input = input,
                Ready = ready,
                Alive = alive,
                IsBot = isBot,
                PublicRole = (OnlineRole)roleValue,
                Profession = (OnlineProfession)professionValue,
                KillCooldown = killCooldown,
                AbilityCooldown = abilityCooldown,
                Suspicion = suspicion,
            };
        }
    }
}
