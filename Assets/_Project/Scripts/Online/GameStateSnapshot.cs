using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 游戏状态快照：完整序列化当前对局的全部状态，用于主机迁移时在新主机上重建。
    /// 覆盖玩家状态、任务、尸体、投票、案卷、全局计时器与配置等所有关键数据。
    /// </summary>
    public struct GameStateSnapshot
    {
        // ── 全局状态 ──
        public bool MatchStarted;
        public OnlineMatchPhase Phase;
        public int EvidenceScore;
        public int EvidenceTarget;
        public int EmergencyMeetingsLeft;
        public int EvidenceMilestoneIndex;
        public int NextBodyId;
        public int RoomMinPlayers;
        public int RoomMaxPlayers;
        public bool RoomAutoFillAi;
        public bool RevealRoleOnEject;
        public bool ProximityVoiceEnabled;
        public string RoomName;
        public string ResultSummary;
        public string LastMeetingReason;
        public string LastVoteOutcome;
        public string LastEvidenceEvent;
        public string LastSabotageEvent;
        public float PhaseTimer;
        public float BlackoutTimer;
        public float LockdownTimer;
        public float CommunicationJamTimer;
        public float EvidenceLeakTimer;
        public float EvidenceLeakAccumulator;
        public float PatrolAlertTimer;
        public float EmergencyCooldownTimer;
        public float AiActionGraceTimer;
        public float MatchElapsedSeconds;

        // ── 玩家状态 ──
        public List<SnapshotPlayerEntry> Players;

        // ── 私密角色 ──
        public List<SnapshotRoleEntry> PrivateRoles;

        // ── 任务 / 尸体 / 投票 / 案卷 ──
        public List<SnapshotTaskEntry> Tasks;
        public List<SnapshotBodyEntry> Bodies;
        public List<SnapshotVoteEntry> Votes;
        public List<string> CaseLog;

        // ── 冷却 ──
        public List<SnapshotCooldownEntry> KillCooldowns;
        public List<SnapshotCooldownEntry> AbilityCooldowns;
        public List<SnapshotCooldownEntry> VentCooldowns;

        // ── Bot 内部状态 ──
        public List<SnapshotCooldownEntry> BotThinkTimers;
        public List<SnapshotCooldownEntry> BotVoteTimers;
        public List<SnapshotTargetEntry> BotTargets;

        // ── 辅助结构 ──

        public struct SnapshotPlayerEntry
        {
            public ulong ClientId;
            public string DisplayName;
            public Vector3 Position;
            public Vector2 Input;
            public bool Ready;
            public bool Alive;
            public bool IsBot;
            public OnlineRole PublicRole;
            public OnlineProfession Profession;
            public float KillCooldown;
            public float AbilityCooldown;
            public int Suspicion;
        }

        public struct SnapshotTaskEntry
        {
            public int Id;
            public string Name;
            public Vector3 Position;
            public int Progress;
            public int RequiredProgress;
            public bool Completed;
            public bool Sabotaged;
        }

        public struct SnapshotBodyEntry
        {
            public int Id;
            public ulong VictimClientId;
            public Vector3 Position;
            public bool Reported;
        }

        public struct SnapshotRoleEntry
        {
            public ulong ClientId;
            public OnlineRole Role;
        }

        public struct SnapshotVoteEntry
        {
            public ulong VoterClientId;
            public ulong TargetClientId;
        }

        public struct SnapshotCooldownEntry
        {
            public ulong ClientId;
            public float Value;
        }

        public struct SnapshotTargetEntry
        {
            public ulong ClientId;
            public Vector3 Target;
        }

        // ── 工厂方法 ──

        /// <summary>
        /// 创建一个空的默认快照（所有列表初始化为空、字段为默认值）。
        /// </summary>
        public static GameStateSnapshot FromDefault()
        {
            return new GameStateSnapshot
            {
                Players = new List<SnapshotPlayerEntry>(),
                PrivateRoles = new List<SnapshotRoleEntry>(),
                Tasks = new List<SnapshotTaskEntry>(),
                Bodies = new List<SnapshotBodyEntry>(),
                Votes = new List<SnapshotVoteEntry>(),
                CaseLog = new List<string>(),
                KillCooldowns = new List<SnapshotCooldownEntry>(),
                AbilityCooldowns = new List<SnapshotCooldownEntry>(),
                VentCooldowns = new List<SnapshotCooldownEntry>(),
                BotThinkTimers = new List<SnapshotCooldownEntry>(),
                BotVoteTimers = new List<SnapshotCooldownEntry>(),
                BotTargets = new List<SnapshotTargetEntry>(),
            };
        }

        // ── 序列化 ──

        /// <summary>
        /// 将快照序列化到 FastBufferWriter。
        /// 写入顺序必须与 FromBytes 严格一致。
        /// </summary>
        public void ToBytes(FastBufferWriter writer)
        {
            // ── 全局状态 ──
            writer.WriteValueSafe(MatchStarted);
            writer.WriteValueSafe((int)Phase);
            writer.WriteValueSafe(EvidenceScore);
            writer.WriteValueSafe(EvidenceTarget);
            writer.WriteValueSafe(EmergencyMeetingsLeft);
            writer.WriteValueSafe(EvidenceMilestoneIndex);
            writer.WriteValueSafe(NextBodyId);
            writer.WriteValueSafe(RoomMinPlayers);
            writer.WriteValueSafe(RoomMaxPlayers);
            writer.WriteValueSafe(RoomAutoFillAi);
            writer.WriteValueSafe(RevealRoleOnEject);
            writer.WriteValueSafe(ProximityVoiceEnabled);
            writer.WriteValueSafe(RoomName ?? string.Empty);
            writer.WriteValueSafe(ResultSummary ?? string.Empty);
            writer.WriteValueSafe(LastMeetingReason ?? string.Empty);
            writer.WriteValueSafe(LastVoteOutcome ?? string.Empty);
            writer.WriteValueSafe(LastEvidenceEvent ?? string.Empty);
            writer.WriteValueSafe(LastSabotageEvent ?? string.Empty);
            writer.WriteValueSafe(PhaseTimer);
            writer.WriteValueSafe(BlackoutTimer);
            writer.WriteValueSafe(LockdownTimer);
            writer.WriteValueSafe(CommunicationJamTimer);
            writer.WriteValueSafe(EvidenceLeakTimer);
            writer.WriteValueSafe(EvidenceLeakAccumulator);
            writer.WriteValueSafe(PatrolAlertTimer);
            writer.WriteValueSafe(EmergencyCooldownTimer);
            writer.WriteValueSafe(AiActionGraceTimer);
            writer.WriteValueSafe(MatchElapsedSeconds);

            // ── 玩家列表 ──
            writer.WriteValueSafe(Players.Count);
            foreach (var p in Players)
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

            // ── 私密角色 ──
            writer.WriteValueSafe(PrivateRoles.Count);
            foreach (var r in PrivateRoles)
            {
                writer.WriteValueSafe(r.ClientId);
                writer.WriteValueSafe((int)r.Role);
            }

            // ── 任务 ──
            writer.WriteValueSafe(Tasks.Count);
            foreach (var t in Tasks)
            {
                writer.WriteValueSafe(t.Id);
                writer.WriteValueSafe(t.Name ?? string.Empty);
                writer.WriteValueSafe(t.Position);
                writer.WriteValueSafe(t.Progress);
                writer.WriteValueSafe(t.RequiredProgress);
                writer.WriteValueSafe(t.Completed);
                writer.WriteValueSafe(t.Sabotaged);
            }

            // ── 尸体 ──
            writer.WriteValueSafe(Bodies.Count);
            foreach (var b in Bodies)
            {
                writer.WriteValueSafe(b.Id);
                writer.WriteValueSafe(b.VictimClientId);
                writer.WriteValueSafe(b.Position);
                writer.WriteValueSafe(b.Reported);
            }

            // ── 投票 ──
            writer.WriteValueSafe(Votes.Count);
            foreach (var v in Votes)
            {
                writer.WriteValueSafe(v.VoterClientId);
                writer.WriteValueSafe(v.TargetClientId);
            }

            // ── 案卷 ──
            writer.WriteValueSafe(CaseLog.Count);
            foreach (var entry in CaseLog)
            {
                writer.WriteValueSafe(entry ?? string.Empty);
            }

            // ── 冷却字典 ──
            WriteCooldownList(writer, KillCooldowns);
            WriteCooldownList(writer, AbilityCooldowns);
            WriteCooldownList(writer, VentCooldowns);
            WriteCooldownList(writer, BotThinkTimers);
            WriteCooldownList(writer, BotVoteTimers);

            // ── Bot 目标 ──
            writer.WriteValueSafe(BotTargets.Count);
            foreach (var bt in BotTargets)
            {
                writer.WriteValueSafe(bt.ClientId);
                writer.WriteValueSafe(bt.Target);
            }
        }

        /// <summary>
        /// 从 FastBufferReader 反序列化快照。
        /// 读取顺序必须与 ToBytes 严格一致。
        /// </summary>
        public static GameStateSnapshot FromBytes(FastBufferReader reader)
        {
            var snap = new GameStateSnapshot();

            // ── 全局状态 ──
            reader.ReadValueSafe(out bool matchStarted);
            reader.ReadValueSafe(out int phaseValue);
            reader.ReadValueSafe(out int evidenceScore);
            reader.ReadValueSafe(out int evidenceTarget);
            reader.ReadValueSafe(out int emergencyMeetingsLeft);
            reader.ReadValueSafe(out int evidenceMilestoneIndex);
            reader.ReadValueSafe(out int nextBodyId);
            reader.ReadValueSafe(out int roomMinPlayers);
            reader.ReadValueSafe(out int roomMaxPlayers);
            reader.ReadValueSafe(out bool roomAutoFillAi);
            reader.ReadValueSafe(out bool revealRoleOnEject);
            reader.ReadValueSafe(out bool proximityVoiceEnabled);
            reader.ReadValueSafe(out string roomName);
            reader.ReadValueSafe(out string resultSummary);
            reader.ReadValueSafe(out string lastMeetingReason);
            reader.ReadValueSafe(out string lastVoteOutcome);
            reader.ReadValueSafe(out string lastEvidenceEvent);
            reader.ReadValueSafe(out string lastSabotageEvent);
            reader.ReadValueSafe(out float phaseTimer);
            reader.ReadValueSafe(out float blackoutTimer);
            reader.ReadValueSafe(out float lockdownTimer);
            reader.ReadValueSafe(out float communicationJamTimer);
            reader.ReadValueSafe(out float evidenceLeakTimer);
            reader.ReadValueSafe(out float evidenceLeakAccumulator);
            reader.ReadValueSafe(out float patrolAlertTimer);
            reader.ReadValueSafe(out float emergencyCooldownTimer);
            reader.ReadValueSafe(out float aiActionGraceTimer);
            reader.ReadValueSafe(out float matchElapsedSeconds);

            snap.MatchStarted = matchStarted;
            snap.Phase = (OnlineMatchPhase)phaseValue;
            snap.EvidenceScore = evidenceScore;
            snap.EvidenceTarget = evidenceTarget;
            snap.EmergencyMeetingsLeft = emergencyMeetingsLeft;
            snap.EvidenceMilestoneIndex = evidenceMilestoneIndex;
            snap.NextBodyId = nextBodyId;
            snap.RoomMinPlayers = roomMinPlayers;
            snap.RoomMaxPlayers = roomMaxPlayers;
            snap.RoomAutoFillAi = roomAutoFillAi;
            snap.RevealRoleOnEject = revealRoleOnEject;
            snap.ProximityVoiceEnabled = proximityVoiceEnabled;
            snap.RoomName = roomName;
            snap.ResultSummary = resultSummary;
            snap.LastMeetingReason = lastMeetingReason;
            snap.LastVoteOutcome = lastVoteOutcome;
            snap.LastEvidenceEvent = lastEvidenceEvent;
            snap.LastSabotageEvent = lastSabotageEvent;
            snap.PhaseTimer = phaseTimer;
            snap.BlackoutTimer = blackoutTimer;
            snap.LockdownTimer = lockdownTimer;
            snap.CommunicationJamTimer = communicationJamTimer;
            snap.EvidenceLeakTimer = evidenceLeakTimer;
            snap.EvidenceLeakAccumulator = evidenceLeakAccumulator;
            snap.PatrolAlertTimer = patrolAlertTimer;
            snap.EmergencyCooldownTimer = emergencyCooldownTimer;
            snap.AiActionGraceTimer = aiActionGraceTimer;
            snap.MatchElapsedSeconds = matchElapsedSeconds;

            // ── 玩家列表 ──
            reader.ReadValueSafe(out int playerCount);
            snap.Players = new List<SnapshotPlayerEntry>(playerCount);
            for (int i = 0; i < playerCount; i++)
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

                snap.Players.Add(new SnapshotPlayerEntry
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
                });
            }

            // ── 私密角色 ──
            reader.ReadValueSafe(out int roleCount);
            snap.PrivateRoles = new List<SnapshotRoleEntry>(roleCount);
            for (int i = 0; i < roleCount; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out int roleValue);
                snap.PrivateRoles.Add(new SnapshotRoleEntry { ClientId = clientId, Role = (OnlineRole)roleValue });
            }

            // ── 任务 ──
            reader.ReadValueSafe(out int taskCount);
            snap.Tasks = new List<SnapshotTaskEntry>(taskCount);
            for (int i = 0; i < taskCount; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out string name);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out int progress);
                reader.ReadValueSafe(out int requiredProgress);
                reader.ReadValueSafe(out bool completed);
                reader.ReadValueSafe(out bool sabotaged);
                snap.Tasks.Add(new SnapshotTaskEntry { Id = id, Name = name, Position = position, Progress = progress, RequiredProgress = requiredProgress, Completed = completed, Sabotaged = sabotaged });
            }

            // ── 尸体 ──
            reader.ReadValueSafe(out int bodyCount);
            snap.Bodies = new List<SnapshotBodyEntry>(bodyCount);
            for (int i = 0; i < bodyCount; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out ulong victimClientId);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out bool reported);
                snap.Bodies.Add(new SnapshotBodyEntry { Id = id, VictimClientId = victimClientId, Position = position, Reported = reported });
            }

            // ── 投票 ──
            reader.ReadValueSafe(out int voteCount);
            snap.Votes = new List<SnapshotVoteEntry>(voteCount);
            for (int i = 0; i < voteCount; i++)
            {
                reader.ReadValueSafe(out ulong voterClientId);
                reader.ReadValueSafe(out ulong targetClientId);
                snap.Votes.Add(new SnapshotVoteEntry { VoterClientId = voterClientId, TargetClientId = targetClientId });
            }

            // ── 案卷 ──
            reader.ReadValueSafe(out int caseLogCount);
            snap.CaseLog = new List<string>(caseLogCount);
            for (int i = 0; i < caseLogCount; i++)
            {
                reader.ReadValueSafe(out string entry);
                snap.CaseLog.Add(entry);
            }

            // ── 冷却字典 ──
            snap.KillCooldowns = ReadCooldownList(reader);
            snap.AbilityCooldowns = ReadCooldownList(reader);
            snap.VentCooldowns = ReadCooldownList(reader);
            snap.BotThinkTimers = ReadCooldownList(reader);
            snap.BotVoteTimers = ReadCooldownList(reader);

            // ── Bot 目标 ──
            reader.ReadValueSafe(out int targetCount);
            snap.BotTargets = new List<SnapshotTargetEntry>(targetCount);
            for (int i = 0; i < targetCount; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out Vector3 target);
                snap.BotTargets.Add(new SnapshotTargetEntry { ClientId = clientId, Target = target });
            }

            return snap;
        }

        // ── 辅助序列化方法 ──

        private static void WriteCooldownList(FastBufferWriter writer, List<SnapshotCooldownEntry> list)
        {
            writer.WriteValueSafe(list.Count);
            foreach (var entry in list)
            {
                writer.WriteValueSafe(entry.ClientId);
                writer.WriteValueSafe(entry.Value);
            }
        }

        private static List<SnapshotCooldownEntry> ReadCooldownList(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            var list = new List<SnapshotCooldownEntry>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out float value);
                list.Add(new SnapshotCooldownEntry { ClientId = clientId, Value = value });
            }
            return list;
        }
    }
}