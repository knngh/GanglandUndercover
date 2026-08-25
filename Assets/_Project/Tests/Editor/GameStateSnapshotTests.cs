using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using GanglandUndercover.Online;

namespace GanglandUndercover.Tests
{
    /// <summary>
    /// GameStateSnapshot 序列化往返测试。
    /// 验证 ToBytes → FromBytes 产生等价快照，防止主机迁移时状态损坏。
    /// </summary>
    [TestFixture]
    public class GameStateSnapshotTests
    {
        private static GameStateSnapshot CreateTestSnapshot()
        {
            return new GameStateSnapshot
            {
                Version = GameStateSnapshot.SNAPSHOT_VERSION,
                MatchStarted = true,
                Phase = OnlineMatchPhase.Action,
                EvidenceScore = 15,
                EvidenceTarget = 42,
                EmergencyMeetingsLeft = 2,
                MeetingCount = 3,
                EvidenceMilestoneIndex = 1,
                NextBodyId = 5,
                RoomMinPlayers = 4,
                RoomMaxPlayers = 10,
                RoomAutoFillAi = true,
                RevealRoleOnEject = false,
                ProximityVoiceEnabled = true,
                RoomName = "测试房间",
                ResultSummary = "",
                LastMeetingReason = "发现尸体",
                LastVoteOutcome = "驱逐了一名玩家",
                LastEvidenceEvent = "取得关键证据",
                LastSabotageEvent = "暂未发现破坏",
                PhaseTimer = 45.5f,
                BlackoutTimer = 0f,
                LockdownTimer = 10.2f,
                CommunicationJamTimer = 0f,
                EvidenceLeakTimer = 5.0f,
                EvidenceLeakAccumulator = 2.5f,
                PatrolAlertTimer = 0f,
                EmergencyCooldownTimer = 15.0f,
                ReportCooldownTimer = 3.0f,
                AiActionGraceTimer = 0f,
                MatchElapsedSeconds = 120.5f,
                CriticalTaskActive = true,
                CriticalTaskType = 1,
                CriticalTaskTimeRemaining = 30.0f,
                CriticalEvidenceRepairStations = new List<int> { 4 },
                GangPositionRevealTimeRemaining = 23.5f,
                Players = new List<GameStateSnapshot.SnapshotPlayerEntry>
                {
                    new GameStateSnapshot.SnapshotPlayerEntry
                    {
                        ClientId = 1UL, DisplayName = "玩家A",
                        Position = new Vector3(1f, 2f, 0f), Input = new Vector2(0.5f, 0f),
                        Ready = true, Alive = true, IsBot = false,
                        PublicRole = OnlineRole.Police, Profession = OnlineProfession.Inspector,
                        KillCooldown = 0f, AbilityCooldown = 5f, Suspicion = 3
                    },
                    new GameStateSnapshot.SnapshotPlayerEntry
                    {
                        ClientId = 2UL, DisplayName = "玩家B",
                        Position = new Vector3(-3f, 4f, 0f), Input = Vector2.zero,
                        Ready = true, Alive = false, IsBot = true,
                        PublicRole = OnlineRole.Gang, Profession = OnlineProfession.Enforcer,
                        KillCooldown = 12f, AbilityCooldown = 0f, Suspicion = 8
                    },
                },
                PrivateRoles = new List<GameStateSnapshot.SnapshotRoleEntry>
                {
                    new GameStateSnapshot.SnapshotRoleEntry { ClientId = 1UL, Role = OnlineRole.Undercover },
                    new GameStateSnapshot.SnapshotRoleEntry { ClientId = 2UL, Role = OnlineRole.Gang },
                },
                UndercoverStates = new List<GameStateSnapshot.SnapshotUndercoverStateEntry>
                {
                    new GameStateSnapshot.SnapshotUndercoverStateEntry
                    {
                        ClientId = 1UL, Intel = 4, MissionsDone = 2, Betrayed = true,
                    },
                },
                MoleStates = new List<GameStateSnapshot.SnapshotMoleStateEntry>
                {
                    new GameStateSnapshot.SnapshotMoleStateEntry
                    {
                        ClientId = 3UL, Intel = 5, HasHitTarget = true, HitTargetClientId = 1UL,
                        Exposed = true, Kills = 1, Sabotages = 2, SurvivedTilLate = true,
                    },
                },
                Tasks = new List<GameStateSnapshot.SnapshotTaskEntry>
                {
                    new GameStateSnapshot.SnapshotTaskEntry
                    {
                        Id = 0, Name = "调取监控", Position = new Vector3(5f, 5f, 0f),
                        Progress = 2, RequiredProgress = 3, Completed = false, Sabotaged = false
                    },
                    new GameStateSnapshot.SnapshotTaskEntry
                    {
                        Id = 7, Name = "门禁取证", Position = new Vector3(-2f, 3f, 0f),
                        Progress = 3, RequiredProgress = 3, Completed = true, Sabotaged = false
                    },
                },
                TaskAssignments = new List<GameStateSnapshot.SnapshotTaskAssignmentEntry>
                {
                    new GameStateSnapshot.SnapshotTaskAssignmentEntry
                        { ClientId = 1UL, TaskId = 0, Progress = 2, RequiredProgress = 3, Completed = false },
                    new GameStateSnapshot.SnapshotTaskAssignmentEntry
                        { ClientId = 1UL, TaskId = 7, Progress = 3, RequiredProgress = 3, Completed = true },
                    new GameStateSnapshot.SnapshotTaskAssignmentEntry
                        { ClientId = 2UL, TaskId = 12, Progress = 0, RequiredProgress = 3, Completed = false },
                },
                Bodies = new List<GameStateSnapshot.SnapshotBodyEntry>
                {
                    new GameStateSnapshot.SnapshotBodyEntry { Id = 0, VictimClientId = 2UL, Position = new Vector3(1f, 1f, 0f), Reported = false },
                },
                Votes = new List<GameStateSnapshot.SnapshotVoteEntry>
                {
                    new GameStateSnapshot.SnapshotVoteEntry { VoterClientId = 1UL, TargetClientId = 2UL },
                },
                Accusations = new List<GameStateSnapshot.SnapshotAccusationEntry>
                {
                    new GameStateSnapshot.SnapshotAccusationEntry { AccuserClientId = 1UL, TargetClientId = 2UL },
                },
                CaseLog = new List<string> { "专案启动", "发现尸体" },
                KillCooldowns = new List<GameStateSnapshot.SnapshotCooldownEntry>
                {
                    new GameStateSnapshot.SnapshotCooldownEntry { ClientId = 2UL, Value = 12f },
                },
                AbilityCooldowns = new List<GameStateSnapshot.SnapshotCooldownEntry>
                {
                    new GameStateSnapshot.SnapshotCooldownEntry { ClientId = 1UL, Value = 5f },
                },
                VentCooldowns = new List<GameStateSnapshot.SnapshotCooldownEntry>(),
                BotThinkTimers = new List<GameStateSnapshot.SnapshotCooldownEntry>
                {
                    new GameStateSnapshot.SnapshotCooldownEntry { ClientId = 2UL, Value = 1.5f },
                },
                BotVoteTimers = new List<GameStateSnapshot.SnapshotCooldownEntry>(),
                BotTargets = new List<GameStateSnapshot.SnapshotTargetEntry>
                {
                    new GameStateSnapshot.SnapshotTargetEntry { ClientId = 2UL, Target = new Vector3(0f, 0f, 0f) },
                },
            };
        }

        [Test]
        public void ToBytes_ThenFromBytes_RoundTrips_GlobalState()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);

                    Assert.AreEqual(original.Version, restored.Version);
                    Assert.AreEqual(original.MatchStarted, restored.MatchStarted);
                    Assert.AreEqual(original.Phase, restored.Phase);
                    Assert.AreEqual(original.EvidenceScore, restored.EvidenceScore);
                    Assert.AreEqual(original.EvidenceTarget, restored.EvidenceTarget);
                    Assert.AreEqual(original.EmergencyMeetingsLeft, restored.EmergencyMeetingsLeft);
                    Assert.AreEqual(original.MeetingCount, restored.MeetingCount);
                    Assert.AreEqual(original.EvidenceMilestoneIndex, restored.EvidenceMilestoneIndex);
                    Assert.AreEqual(original.NextBodyId, restored.NextBodyId);
                    Assert.AreEqual(original.RoomMinPlayers, restored.RoomMinPlayers);
                    Assert.AreEqual(original.RoomMaxPlayers, restored.RoomMaxPlayers);
                    Assert.AreEqual(original.RoomAutoFillAi, restored.RoomAutoFillAi);
                    Assert.AreEqual(original.RevealRoleOnEject, restored.RevealRoleOnEject);
                    Assert.AreEqual(original.ProximityVoiceEnabled, restored.ProximityVoiceEnabled);
                    Assert.AreEqual(original.RoomName, restored.RoomName);
                    Assert.AreEqual(original.LastMeetingReason, restored.LastMeetingReason);
                    Assert.AreEqual(original.LastVoteOutcome, restored.LastVoteOutcome);
                    Assert.AreEqual(original.PhaseTimer, restored.PhaseTimer, 0.001f);
                    Assert.AreEqual(original.LockdownTimer, restored.LockdownTimer, 0.001f);
                    Assert.AreEqual(original.EvidenceLeakTimer, restored.EvidenceLeakTimer, 0.001f);
                    Assert.AreEqual(original.EmergencyCooldownTimer, restored.EmergencyCooldownTimer, 0.001f);
                    Assert.AreEqual(original.MatchElapsedSeconds, restored.MatchElapsedSeconds, 0.001f);
                    Assert.AreEqual(original.CriticalTaskActive, restored.CriticalTaskActive);
                    Assert.AreEqual(original.CriticalTaskType, restored.CriticalTaskType);
                    Assert.AreEqual(original.CriticalTaskTimeRemaining, restored.CriticalTaskTimeRemaining, 0.001f);
                    CollectionAssert.AreEquivalent(
                        original.CriticalEvidenceRepairStations,
                        restored.CriticalEvidenceRepairStations);
                    Assert.AreEqual(
                        original.GangPositionRevealTimeRemaining,
                        restored.GangPositionRevealTimeRemaining,
                        0.001f);
                }
            }
        }

        [Test]
        public void ToBytes_ThenFromBytes_RoundTrips_Players()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);

                    Assert.AreEqual(original.Players.Count, restored.Players.Count);
                    for (int i = 0; i < original.Players.Count; i++)
                    {
                        var a = original.Players[i];
                        var b = restored.Players[i];
                        Assert.AreEqual(a.ClientId, b.ClientId);
                        Assert.AreEqual(a.DisplayName, b.DisplayName);
                        Assert.AreEqual(a.Position, b.Position);
                        Assert.AreEqual(a.Input, b.Input);
                        Assert.AreEqual(a.Ready, b.Ready);
                        Assert.AreEqual(a.Alive, b.Alive);
                        Assert.AreEqual(a.IsBot, b.IsBot);
                        Assert.AreEqual(a.PublicRole, b.PublicRole);
                        Assert.AreEqual(a.Profession, b.Profession);
                        Assert.AreEqual(a.KillCooldown, b.KillCooldown, 0.001f);
                        Assert.AreEqual(a.AbilityCooldown, b.AbilityCooldown, 0.001f);
                        Assert.AreEqual(a.Suspicion, b.Suspicion);
                    }
                }
            }
        }

        [Test]
        public void ToBytes_ThenFromBytes_RoundTrips_Tasks()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);

                    Assert.AreEqual(original.Tasks.Count, restored.Tasks.Count);
                    for (int i = 0; i < original.Tasks.Count; i++)
                    {
                        Assert.AreEqual(original.Tasks[i].Id, restored.Tasks[i].Id);
                        Assert.AreEqual(original.Tasks[i].Name, restored.Tasks[i].Name);
                        Assert.AreEqual(original.Tasks[i].Position, restored.Tasks[i].Position);
                        Assert.AreEqual(original.Tasks[i].Progress, restored.Tasks[i].Progress);
                        Assert.AreEqual(original.Tasks[i].Completed, restored.Tasks[i].Completed);
                        Assert.AreEqual(original.Tasks[i].Sabotaged, restored.Tasks[i].Sabotaged);
                    }
                }
            }
        }

        [Test]
        public void ToBytes_ThenFromBytes_RoundTrips_TaskAssignments()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);

                    Assert.AreEqual(original.TaskAssignments.Count, restored.TaskAssignments.Count);
                    for (int i = 0; i < original.TaskAssignments.Count; i++)
                    {
                        Assert.AreEqual(original.TaskAssignments[i].ClientId, restored.TaskAssignments[i].ClientId);
                        Assert.AreEqual(original.TaskAssignments[i].TaskId, restored.TaskAssignments[i].TaskId);
                        Assert.AreEqual(original.TaskAssignments[i].Progress, restored.TaskAssignments[i].Progress);
                        Assert.AreEqual(original.TaskAssignments[i].RequiredProgress, restored.TaskAssignments[i].RequiredProgress);
                        Assert.AreEqual(original.TaskAssignments[i].Completed, restored.TaskAssignments[i].Completed);
                    }
                }
            }
        }

        [Test]
        public void ToBytes_ThenFromBytes_RoundTrips_DoubleAgentStates()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);

                    Assert.AreEqual(1, restored.UndercoverStates.Count);
                    Assert.AreEqual(original.UndercoverStates[0].ClientId, restored.UndercoverStates[0].ClientId);
                    Assert.AreEqual(original.UndercoverStates[0].Intel, restored.UndercoverStates[0].Intel);
                    Assert.AreEqual(original.UndercoverStates[0].MissionsDone, restored.UndercoverStates[0].MissionsDone);
                    Assert.AreEqual(original.UndercoverStates[0].Betrayed, restored.UndercoverStates[0].Betrayed);

                    Assert.AreEqual(1, restored.MoleStates.Count);
                    Assert.AreEqual(original.MoleStates[0].ClientId, restored.MoleStates[0].ClientId);
                    Assert.AreEqual(original.MoleStates[0].Intel, restored.MoleStates[0].Intel);
                    Assert.AreEqual(original.MoleStates[0].HasHitTarget, restored.MoleStates[0].HasHitTarget);
                    Assert.AreEqual(original.MoleStates[0].HitTargetClientId, restored.MoleStates[0].HitTargetClientId);
                    Assert.AreEqual(original.MoleStates[0].Exposed, restored.MoleStates[0].Exposed);
                    Assert.AreEqual(original.MoleStates[0].Kills, restored.MoleStates[0].Kills);
                    Assert.AreEqual(original.MoleStates[0].Sabotages, restored.MoleStates[0].Sabotages);
                    Assert.AreEqual(original.MoleStates[0].SurvivedTilLate, restored.MoleStates[0].SurvivedTilLate);
                }
            }
        }

        [Test]
        public void ToBytes_ThenFromBytes_RoundTrips_BodiesVotesCaseLog()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);

                    // Bodies
                    Assert.AreEqual(original.Bodies.Count, restored.Bodies.Count);
                    for (int i = 0; i < original.Bodies.Count; i++)
                    {
                        Assert.AreEqual(original.Bodies[i].Id, restored.Bodies[i].Id);
                        Assert.AreEqual(original.Bodies[i].VictimClientId, restored.Bodies[i].VictimClientId);
                        Assert.AreEqual(original.Bodies[i].Reported, restored.Bodies[i].Reported);
                    }

                    // Votes
                    Assert.AreEqual(original.Votes.Count, restored.Votes.Count);
                    for (int i = 0; i < original.Votes.Count; i++)
                    {
                        Assert.AreEqual(original.Votes[i].VoterClientId, restored.Votes[i].VoterClientId);
                        Assert.AreEqual(original.Votes[i].TargetClientId, restored.Votes[i].TargetClientId);
                    }

                    // Accusations
                    Assert.AreEqual(original.Accusations.Count, restored.Accusations.Count);
                    for (int i = 0; i < original.Accusations.Count; i++)
                    {
                        Assert.AreEqual(original.Accusations[i].AccuserClientId, restored.Accusations[i].AccuserClientId);
                        Assert.AreEqual(original.Accusations[i].TargetClientId, restored.Accusations[i].TargetClientId);
                    }

                    // CaseLog
                    Assert.AreEqual(original.CaseLog.Count, restored.CaseLog.Count);
                    for (int i = 0; i < original.CaseLog.Count; i++)
                    {
                        Assert.AreEqual(original.CaseLog[i], restored.CaseLog[i]);
                    }
                }
            }
        }

        [Test]
        public void ToBytes_ThenFromBytes_RoundTrips_CooldownsAndBotTargets()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);

                    Assert.AreEqual(original.KillCooldowns.Count, restored.KillCooldowns.Count);
                    Assert.AreEqual(original.AbilityCooldowns.Count, restored.AbilityCooldowns.Count);
                    Assert.AreEqual(original.VentCooldowns.Count, restored.VentCooldowns.Count);
                    Assert.AreEqual(original.BotThinkTimers.Count, restored.BotThinkTimers.Count);
                    Assert.AreEqual(original.BotVoteTimers.Count, restored.BotVoteTimers.Count);
                    Assert.AreEqual(original.BotTargets.Count, restored.BotTargets.Count);

                    for (int i = 0; i < original.KillCooldowns.Count; i++)
                    {
                        Assert.AreEqual(original.KillCooldowns[i].ClientId, restored.KillCooldowns[i].ClientId);
                        Assert.AreEqual(original.KillCooldowns[i].Value, restored.KillCooldowns[i].Value, 0.001f);
                    }
                }
            }
        }

        [Test]
        public void ValidateEquivalence_IdenticalSnapshots_ReturnsEmpty()
        {
            var original = CreateTestSnapshot();

            using (var writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                original.ToBytes(writer);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var restored = GameStateSnapshot.FromBytes(reader);
                    var mismatches = original.ValidateEquivalence(restored);

                    Assert.AreEqual(0, mismatches.Count,
                        "Identical snapshots should have no mismatches. Found: " + string.Join("; ", mismatches));
                }
            }
        }

        [Test]
        public void IsValid_ValidSnapshot_ReturnsTrue()
        {
            var snapshot = CreateTestSnapshot();
            Assert.IsTrue(snapshot.IsValid());
        }

        [Test]
        public void IsValid_NullPlayers_ReturnsFalse()
        {
            var snapshot = CreateTestSnapshot();
            snapshot.Players = null;
            LogAssert.Expect(LogType.Error, "[GameStateSnapshot] IsValid 失败: Players 为 null");
            Assert.IsFalse(snapshot.IsValid());
        }

        [Test]
        public void IsValid_StartedWithNoPlayers_ReturnsFalse()
        {
            var snapshot = CreateTestSnapshot();
            snapshot.MatchStarted = true;
            snapshot.Players = new List<GameStateSnapshot.SnapshotPlayerEntry>();
            LogAssert.Expect(LogType.Error, "[GameStateSnapshot] IsValid 失败: 对局已开始但玩家数为 0");
            Assert.IsFalse(snapshot.IsValid());
        }
    }
}
