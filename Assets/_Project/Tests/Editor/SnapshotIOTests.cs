using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using GanglandUndercover.Online;

namespace GanglandUndercover.Tests
{
    /// <summary>
    /// SnapshotIO 序列化往返测试。
    /// 验证 Write → Read 产生完全一致的数据，防止格式错位导致客户端 desync。
    /// </summary>
    [TestFixture]
    public class SnapshotIOTests
    {
        private FastBufferWriter CreateWriter(int capacity = 4096)
        {
            return new FastBufferWriter(capacity, Allocator.Temp);
        }

        // ─── Task (Broadcast 格式) ─────────────────────────────

        [Test]
        public void WriteTasks_ThenReadTasks_RoundTrips()
        {
            var tasks = new List<OnlineTaskState>
            {
                new OnlineTaskState(0, "调取监控", new Vector3(1f, 2f, 0f), 1, 3, false, false),
                new OnlineTaskState(5, "盘问线人", new Vector3(-3f, 4f, 0f), 3, 3, true, false),
                new OnlineTaskState(12, "解除卷闸", new Vector3(0f, 0f, 0f), 0, 3, false, true),
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteTasks(writer, tasks);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(3, count);

                    var result = SnapshotIO.ReadTasks(reader, count);

                    Assert.AreEqual(tasks.Count, result.Count);
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        Assert.AreEqual(tasks[i].Id, result[i].Id);
                        Assert.AreEqual(tasks[i].Position, result[i].Position);
                        Assert.AreEqual(tasks[i].Progress, result[i].Progress);
                        Assert.AreEqual(tasks[i].RequiredProgress, result[i].RequiredProgress);
                        Assert.AreEqual(tasks[i].Completed, result[i].Completed);
                        Assert.AreEqual(tasks[i].Sabotaged, result[i].Sabotaged);
                    }
                }
            }
        }

        // ─── Body ──────────────────────────────────────────────

        [Test]
        public void WriteBodies_ThenReadBodies_RoundTrips()
        {
            var bodies = new List<OnlineBodyState>
            {
                new OnlineBodyState(0, 100UL, new Vector3(5f, 5f, 0f), false),
                new OnlineBodyState(1, 200UL, new Vector3(-2f, 3f, 0f), true),
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteBodies(writer, bodies);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(2, count);

                    var result = SnapshotIO.ReadBodies(reader, count);

                    Assert.AreEqual(bodies.Count, result.Count);
                    for (int i = 0; i < bodies.Count; i++)
                    {
                        Assert.AreEqual(bodies[i].Id, result[i].Id);
                        Assert.AreEqual(bodies[i].VictimClientId, result[i].VictimClientId);
                        Assert.AreEqual(bodies[i].Position, result[i].Position);
                        Assert.AreEqual(bodies[i].Reported, result[i].Reported);
                    }
                }
            }
        }

        // ─── Votes (Dictionary 格式) ───────────────────────────

        [Test]
        public void WriteVotes_ThenReadVotes_RoundTrips()
        {
            var votes = new Dictionary<ulong, ulong>
            {
                { 1UL, 2UL },
                { 3UL, 2UL },
                { 4UL, 999UL }, // skip vote
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteVotes(writer, votes);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(3, count);

                    var result = SnapshotIO.ReadVotes(reader, count);

                    Assert.AreEqual(votes.Count, result.Count);
                    foreach (var kv in votes)
                    {
                        Assert.IsTrue(result.ContainsKey(kv.Key));
                        Assert.AreEqual(kv.Value, result[kv.Key]);
                    }
                }
            }
        }

        // ─── Votes (Migration List 格式) ───────────────────────

        [Test]
        public void WriteVotesMigration_ThenReadVotesAsEntries_RoundTrips()
        {
            var votes = new List<GameStateSnapshot.SnapshotVoteEntry>
            {
                new GameStateSnapshot.SnapshotVoteEntry { VoterClientId = 10UL, TargetClientId = 20UL },
                new GameStateSnapshot.SnapshotVoteEntry { VoterClientId = 30UL, TargetClientId = 999UL },
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteVotes(writer, votes);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(2, count);

                    var result = SnapshotIO.ReadVotesAsEntries(reader, count);

                    Assert.AreEqual(votes.Count, result.Count);
                    for (int i = 0; i < votes.Count; i++)
                    {
                        Assert.AreEqual(votes[i].VoterClientId, result[i].VoterClientId);
                        Assert.AreEqual(votes[i].TargetClientId, result[i].TargetClientId);
                    }
                }
            }
        }

        // ─── CaseLog ───────────────────────────────────────────

        [Test]
        public void WriteCaseLog_ThenReadCaseLog_RoundTrips()
        {
            var caseLog = new List<string>
            {
                "专案启动，证据链待闭合。",
                "发现尸体：码头区。",
                "召开紧急会议。",
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteCaseLog(writer, caseLog);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(3, count);

                    var result = SnapshotIO.ReadCaseLog(reader, count);

                    Assert.AreEqual(caseLog.Count, result.Count);
                    for (int i = 0; i < caseLog.Count; i++)
                    {
                        Assert.AreEqual(caseLog[i], result[i]);
                    }
                }
            }
        }

        [Test]
        public void WriteCaseLog_WithNullEntry_WritesEmptyString()
        {
            var caseLog = new List<string> { "entry1", null, "entry3" };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteCaseLog(writer, caseLog);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    var result = SnapshotIO.ReadCaseLog(reader, count);

                    Assert.AreEqual("entry1", result[0]);
                    Assert.AreEqual(string.Empty, result[1]);
                    Assert.AreEqual("entry3", result[2]);
                }
            }
        }

        // ─── Player (Broadcast 格式) ───────────────────────────

        [Test]
        public void WritePlayerBroadcast_ThenReadPlayerBroadcast_RoundTrips()
        {
            var state = new OnlinePlayerState(
                42UL, "测试玩家", new Vector3(10f, 20f, 0f),
                true, true, OnlineRole.Police, OnlineProfession.Inspector, 5, false);

            using (var writer = CreateWriter())
            {
                SnapshotIO.WritePlayerBroadcast(writer, state, 12.5f, 8.0f, 3.2f);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    SnapshotIO.ReadPlayerBroadcast(reader,
                        out ulong clientId, out string displayName, out Vector3 position,
                        out bool ready, out bool alive, out bool isBot,
                        out int roleValue, out int professionValue,
                        out int suspicion, out float killCooldown, out float abilityCooldown, out float ventCooldown);

                    Assert.AreEqual(42UL, clientId);
                    Assert.AreEqual("测试玩家", displayName);
                    Assert.AreEqual(new Vector3(10f, 20f, 0f), position);
                    Assert.IsTrue(ready);
                    Assert.IsTrue(alive);
                    Assert.IsFalse(isBot);
                    Assert.AreEqual((int)OnlineRole.Police, roleValue);
                    Assert.AreEqual((int)OnlineProfession.Inspector, professionValue);
                    Assert.AreEqual(5, suspicion);
                    Assert.AreEqual(12.5f, killCooldown, 0.001f);
                    Assert.AreEqual(8.0f, abilityCooldown, 0.001f);
                    Assert.AreEqual(3.2f, ventCooldown, 0.001f);
                }
            }
        }

        // ─── Player (Migration 格式) ───────────────────────────

        [Test]
        public void WritePlayerMigration_ThenReadPlayerMigration_RoundTrips()
        {
            var entry = new GameStateSnapshot.SnapshotPlayerEntry
            {
                ClientId = 99UL,
                DisplayName = "迁移玩家",
                Position = new Vector3(-5f, 15f, 0f),
                Input = new Vector2(0.5f, -0.3f),
                Ready = true,
                Alive = false,
                IsBot = true,
                PublicRole = OnlineRole.Gang,
                Profession = OnlineProfession.Enforcer,
                KillCooldown = 18.0f,
                AbilityCooldown = 0f,
                Suspicion = 12,
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WritePlayerMigration(writer, entry);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var result = SnapshotIO.ReadPlayerMigration(reader);

                    Assert.AreEqual(entry.ClientId, result.ClientId);
                    Assert.AreEqual(entry.DisplayName, result.DisplayName);
                    Assert.AreEqual(entry.Position, result.Position);
                    Assert.AreEqual(entry.Input, result.Input);
                    Assert.AreEqual(entry.Ready, result.Ready);
                    Assert.AreEqual(entry.Alive, result.Alive);
                    Assert.AreEqual(entry.IsBot, result.IsBot);
                    Assert.AreEqual(entry.PublicRole, result.PublicRole);
                    Assert.AreEqual(entry.Profession, result.Profession);
                    Assert.AreEqual(entry.KillCooldown, result.KillCooldown, 0.001f);
                    Assert.AreEqual(entry.AbilityCooldown, result.AbilityCooldown, 0.001f);
                    Assert.AreEqual(entry.Suspicion, result.Suspicion);
                }
            }
        }

        // ─── Task (Migration 格式) ─────────────────────────────

        [Test]
        public void WriteTasksMigration_ThenReadTasksAsEntries_RoundTrips()
        {
            var tasks = new List<GameStateSnapshot.SnapshotTaskEntry>
            {
                new GameStateSnapshot.SnapshotTaskEntry
                {
                    Id = 3, Name = "扫描证物", Position = new Vector3(7f, 8f, 0f),
                    Progress = 2, RequiredProgress = 3, Completed = false, Sabotaged = false
                },
                new GameStateSnapshot.SnapshotTaskEntry
                {
                    Id = 14, Name = "备用发电", Position = new Vector3(-1f, -2f, 0f),
                    Progress = 3, RequiredProgress = 3, Completed = true, Sabotaged = false
                },
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteTasksMigration(writer, tasks);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(2, count);

                    var result = SnapshotIO.ReadTasksAsEntries(reader, count);

                    Assert.AreEqual(tasks.Count, result.Count);
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        Assert.AreEqual(tasks[i].Id, result[i].Id);
                        Assert.AreEqual(tasks[i].Name, result[i].Name);
                        Assert.AreEqual(tasks[i].Position, result[i].Position);
                        Assert.AreEqual(tasks[i].Progress, result[i].Progress);
                        Assert.AreEqual(tasks[i].RequiredProgress, result[i].RequiredProgress);
                        Assert.AreEqual(tasks[i].Completed, result[i].Completed);
                        Assert.AreEqual(tasks[i].Sabotaged, result[i].Sabotaged);
                    }
                }
            }
        }

        // ─── Body (Migration 格式) ─────────────────────────────

        [Test]
        public void WriteBodiesMigration_ThenReadBodiesAsEntries_RoundTrips()
        {
            var bodies = new List<GameStateSnapshot.SnapshotBodyEntry>
            {
                new GameStateSnapshot.SnapshotBodyEntry { Id = 0, VictimClientId = 50UL, Position = new Vector3(1f, 1f, 0f), Reported = false },
                new GameStateSnapshot.SnapshotBodyEntry { Id = 1, VictimClientId = 60UL, Position = new Vector3(2f, 2f, 0f), Reported = true },
            };

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteBodies(writer, bodies);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(2, count);

                    var result = SnapshotIO.ReadBodiesAsEntries(reader, count);

                    Assert.AreEqual(bodies.Count, result.Count);
                    for (int i = 0; i < bodies.Count; i++)
                    {
                        Assert.AreEqual(bodies[i].Id, result[i].Id);
                        Assert.AreEqual(bodies[i].VictimClientId, result[i].VictimClientId);
                        Assert.AreEqual(bodies[i].Position, result[i].Position);
                        Assert.AreEqual(bodies[i].Reported, result[i].Reported);
                    }
                }
            }
        }

        // ─── 空集合边界测试 ────────────────────────────────────

        [Test]
        public void WriteTasks_EmptyList_RoundTrips()
        {
            var tasks = new List<OnlineTaskState>();

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteTasks(writer, tasks);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(0, count);
                    var result = SnapshotIO.ReadTasks(reader, count);
                    Assert.AreEqual(0, result.Count);
                }
            }
        }

        [Test]
        public void WriteVotes_EmptyDictionary_RoundTrips()
        {
            var votes = new Dictionary<ulong, ulong>();

            using (var writer = CreateWriter())
            {
                SnapshotIO.WriteVotes(writer, votes);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadValueSafe(out int count);
                    Assert.AreEqual(0, count);
                    var result = SnapshotIO.ReadVotes(reader, count);
                    Assert.AreEqual(0, result.Count);
                }
            }
        }
    }
}
