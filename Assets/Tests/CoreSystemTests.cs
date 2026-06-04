using NUnit.Framework;
using System.Collections.Generic;
using GanglandUndercover.Online;

namespace GanglandUndercover.Tests
{
    /// <summary>
    /// M1 收尾：核心系统最小测试覆盖网。
    /// 覆盖 OnlineRuleSet / OnlineVictoryBridge / ChatSystem 的关键纯逻辑路径。
    /// 运行方式：Unity Editor → Window → General → Test Runner → Run All
    /// </summary>
    public class CoreSystemTests
    {
        // ═══════════════════════════════════════════════════════════════
        // OnlineRuleSet
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void EmergencyMeetingLimit_ClampsWithinRange()
        {
            var ruleSet = new OnlineRuleSet();
            // 3 players → min(3/3=1, Max=3) → 1
            Assert.AreEqual(1, ruleSet.EmergencyMeetingLimitFor(3));
            // 6 players → min(6/3=2, Max=3) → 2
            Assert.AreEqual(2, ruleSet.EmergencyMeetingLimitFor(6));
            // 9 players → min(9/3=3, Max=3) → 3
            Assert.AreEqual(3, ruleSet.EmergencyMeetingLimitFor(9));
            // 15 players → min(15/3=5, Max=3) → 3 (clamped)
            Assert.AreEqual(3, ruleSet.EmergencyMeetingLimitFor(15));
        }

        [Test]
        public void EmergencyMeetingLimit_FloorIsOne()
        {
            var ruleSet = new OnlineRuleSet();
            // 1 player → min(1/3=0, Max=3) → 0 → clamped to 1
            Assert.AreEqual(1, ruleSet.EmergencyMeetingLimitFor(1));
            // 2 players → min(2/3=0, Max=3) → 0 → clamped to 1
            Assert.AreEqual(1, ruleSet.EmergencyMeetingLimitFor(2));
        }

        // ═══════════════════════════════════════════════════════════════
        // OnlineVictoryBridge — TryTimeLimitEvaluation
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void TimeLimit_NotReached_ReturnsFalse()
        {
            var bridge = new OnlineVictoryBridge();
            var tasks = new List<OnlineTaskState>
            {
                new OnlineTaskState(0, "任务A", UnityEngine.Vector3.zero, 1, 1, true, false),
                new OnlineTaskState(1, "任务B", UnityEngine.Vector3.zero, 1, 1, false, false)
            };

            // 100s elapsed, 300s limit — 未到时间
            bool hasResult = bridge.TryTimeLimitEvaluation(
                100f, 300f, evidenceScore: 5, evidenceTarget: 10, tasks, out string result);

            Assert.IsFalse(hasResult);
            Assert.IsEmpty(result);
        }

        [Test]
        public void TimeLimit_EvidenceHigh_PoliceWins()
        {
            var bridge = new OnlineVictoryBridge();
            var tasks = new List<OnlineTaskState>
            {
                new OnlineTaskState(0, "任务A", UnityEngine.Vector3.zero, 1, 1, true, false)
            };

            // 时间到，证据 9/10 (90%) ≥ 82% → 警方胜利
            bool hasResult = bridge.TryTimeLimitEvaluation(
                350f, 300f, evidenceScore: 9, evidenceTarget: 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            Assert.IsTrue(result.Contains("警方胜利"));
        }

        [Test]
        public void TimeLimit_EvidenceLow_GangWins()
        {
            var bridge = new OnlineVictoryBridge();
            var tasks = new List<OnlineTaskState>
            {
                new OnlineTaskState(0, "任务A", UnityEngine.Vector3.zero, 1, 1, false, false),
                new OnlineTaskState(1, "任务B", UnityEngine.Vector3.zero, 1, 1, false, false)
            };

            // 时间到，证据 2/10 (20%) < 82%，任务完成 0/2 (0%) < 72% → 黑帮胜利
            bool hasResult = bridge.TryTimeLimitEvaluation(
            350f, 300f, evidenceScore: 2, evidenceTarget: 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            Assert.IsTrue(result.Contains("黑帮胜利"));
        }

        [Test]
        public void TimeLimit_TasksHigh_PoliceWins()
        {
            var bridge = new OnlineVictoryBridge();
            var tasks = new List<OnlineTaskState>
            {
                new OnlineTaskState(0, "任务A", UnityEngine.Vector3.zero, 1, 1, true, false),
                new OnlineTaskState(1, "任务B", UnityEngine.Vector3.zero, 1, 1, true, false)
            };

            // 时间到，证据 3/10 (30%) < 82%，但任务完成 2/2 (100%) ≥ 72% → 警方胜利
            bool hasResult = bridge.TryTimeLimitEvaluation(
                350f, 300f, evidenceScore: 3, evidenceTarget: 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            Assert.IsTrue(result.Contains("警方胜利"));
        }

        // ═══════════════════════════════════════════════════════════════
        // ChatSystem — 通道判定
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void DetermineChannel_DeadPlayer_Ghost()
        {
            Assert.AreEqual(ChatChannel.Ghost, ChatSystem.DetermineChannel(OnlineMatchPhase.Action, isAlive: false));
            Assert.AreEqual(ChatChannel.Ghost, ChatSystem.DetermineChannel(OnlineMatchPhase.Meeting, isAlive: false));
            Assert.AreEqual(ChatChannel.Ghost, ChatSystem.DetermineChannel(OnlineMatchPhase.Voting, isAlive: false));
            Assert.AreEqual(ChatChannel.Ghost, ChatSystem.DetermineChannel(OnlineMatchPhase.Lobby, isAlive: false));
        }

        [Test]
        public void DetermineChannel_AliveInMeeting_Meeting()
        {
            Assert.AreEqual(ChatChannel.Meeting, ChatSystem.DetermineChannel(OnlineMatchPhase.Meeting, isAlive: true));
            Assert.AreEqual(ChatChannel.Meeting, ChatSystem.DetermineChannel(OnlineMatchPhase.Voting, isAlive: true));
        }

        [Test]
        public void DetermineChannel_AliveInAction_Proximity()
        {
            Assert.AreEqual(ChatChannel.Proximity, ChatSystem.DetermineChannel(OnlineMatchPhase.Action, isAlive: true));
        }

        [Test]
        public void DetermineChannel_AliveInLobby_Proximity()
        {
            // Lobby阶段存活玩家也走Proximity（fallback）
            Assert.AreEqual(ChatChannel.Proximity, ChatSystem.DetermineChannel(OnlineMatchPhase.Lobby, isAlive: true));
        }

        // ═══════════════════════════════════════════════════════════════
        // ChatSystem — 消息净化
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Sanitize_RemovesHtmlTags()
        {
            Assert.AreEqual("hello", ChatSystem.Sanitize("<b>hello</b>"));
            Assert.AreEqual("text", ChatSystem.Sanitize("<script>alert('xss')</script>text"));
        }

        [Test]
        public void Sanitize_TruncatesLongMessages()
        {
            string longMsg = new string('a', 600); // 600 chars > MaxMessageLength(500)
            string result = ChatSystem.Sanitize(longMsg);
            Assert.AreEqual(500, result.Length);
        }

        [Test]
        public void Sanitize_PreservesNormalText()
        {
            Assert.AreEqual("你好，警察！", ChatSystem.Sanitize("你好，警察！"));
            Assert.AreEqual("Report body at Dockyard", ChatSystem.Sanitize("Report body at Dockyard"));
        }

        [Test]
        public void Sanitize_EmptyOrNull_ReturnsSame()
        {
            Assert.IsNull(ChatSystem.Sanitize(null));
            Assert.AreEqual(string.Empty, ChatSystem.Sanitize(string.Empty));
        }
    }
}
