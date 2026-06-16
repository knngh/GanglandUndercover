using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GanglandUndercover.Tests
{
    /// <summary>
    /// M1-M4 regression tests. The project runtime still lives in Unity's
    /// predefined Assembly-CSharp assembly, so this test asmdef accesses it
    /// through reflection instead of creating a large runtime asmdef migration.
    /// </summary>
    public class CoreSystemTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private static readonly Dictionary<ulong, int> RoleLookup = new Dictionary<ulong, int>();

        [Test]
        public void EmergencyMeetingLimit_ClampsWithinRange()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(1, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 3));
            Assert.AreEqual(2, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 6));
            Assert.AreEqual(3, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 9));
            Assert.AreEqual(3, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 15));
        }

        [Test]
        public void EmergencyMeetingLimit_FloorIsOne()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(1, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 1));
            Assert.AreEqual(1, InvokeInt(ruleSet, "EmergencyMeetingLimitFor", 2));
        }

        [Test]
        public void RoleDistribution_5Players_Returns1Gang1Undercover()
        {
            object dist = GetRoleDistribution(5);

            Assert.AreEqual(1, FieldInt(dist, "gang"));
            Assert.AreEqual(1, FieldInt(dist, "undercover"));
            Assert.AreEqual(0, FieldInt(dist, "mole"));
            Assert.AreEqual(3, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_8Players_Returns2Gang1Undercover1Mole()
        {
            object dist = GetRoleDistribution(8);

            Assert.AreEqual(2, FieldInt(dist, "gang"));
            Assert.AreEqual(1, FieldInt(dist, "undercover"));
            Assert.AreEqual(1, FieldInt(dist, "mole"));
            Assert.AreEqual(4, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_10Players_Returns3Gang2Undercover1Mole()
        {
            object dist = GetRoleDistribution(10);

            Assert.AreEqual(3, FieldInt(dist, "gang"));
            Assert.AreEqual(2, FieldInt(dist, "undercover"));
            Assert.AreEqual(1, FieldInt(dist, "mole"));
            Assert.AreEqual(4, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_4Players_Returns1GangNoUndercover()
        {
            object dist = GetRoleDistribution(4);

            Assert.AreEqual(1, FieldInt(dist, "gang"));
            Assert.AreEqual(0, FieldInt(dist, "undercover"));
            Assert.AreEqual(0, FieldInt(dist, "mole"));
            Assert.AreEqual(3, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_OutOfRange_UsesNearestPreset()
        {
            object low = GetRoleDistribution(3);
            object high = GetRoleDistribution(12);

            Assert.AreEqual(1, FieldInt(low, "gang"));
            Assert.AreEqual(0, FieldInt(low, "undercover"));
            Assert.AreEqual(3, FieldInt(high, "gang"));
            Assert.AreEqual(2, FieldInt(high, "undercover"));
        }

        [Test]
        public void TotalTaskCount_AndEvidenceTarget_ScaleByPlayerCount()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(24, InvokeInt(ruleSet, "TotalTaskCount", 8, 2));
            Assert.AreEqual(28, FieldInt(ruleSet, "MinEvidenceTarget"));
            Assert.AreEqual(28, InvokeInt(ruleSet, "ScaledEvidenceTarget", 4));
            Assert.AreEqual(34, InvokeInt(ruleSet, "ScaledEvidenceTarget", 6));
            Assert.AreEqual(44, InvokeInt(ruleSet, "ScaledEvidenceTarget", 8));
            Assert.AreEqual(55, InvokeInt(ruleSet, "ScaledEvidenceTarget", 10));
        }

        [Test]
        public void EvidenceTarget_SettingClampsToRuleSetRange()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                Assert.AreEqual(28, fixture.PropertyInt("MinEvidenceTargetValue"));
                Assert.AreEqual(56, fixture.PropertyInt("MaxEvidenceTargetValue"));

                fixture.SetEvidenceTarget(12);
                Assert.AreEqual(28, fixture.PropertyInt("EvidenceTarget"));

                fixture.SetEvidenceTarget(99);
                Assert.AreEqual(56, fixture.PropertyInt("EvidenceTarget"));
            }
        }

        [Test]
        public void MatchPacing_ScalesKillAndMeetingTimersByPlayerCount()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(30f, InvokeFloat(ruleSet, "KillCooldownFor", 4), 0.001f);
            Assert.AreEqual(25f, InvokeFloat(ruleSet, "KillCooldownFor", 6), 0.001f);
            Assert.AreEqual(22f, InvokeFloat(ruleSet, "KillCooldownFor", 8), 0.001f);

            Assert.AreEqual(30f, InvokeFloat(ruleSet, "MeetingIntroSecondsFor", 4), 0.001f);
            Assert.AreEqual(35f, InvokeFloat(ruleSet, "MeetingIntroSecondsFor", 6), 0.001f);
            Assert.AreEqual(45f, InvokeFloat(ruleSet, "MeetingIntroSecondsFor", 8), 0.001f);

            Assert.AreEqual(30f, InvokeFloat(ruleSet, "VotingSecondsFor", 4), 0.001f);
            Assert.AreEqual(40f, InvokeFloat(ruleSet, "VotingSecondsFor", 6), 0.001f);
            Assert.AreEqual(50f, InvokeFloat(ruleSet, "VotingSecondsFor", 8), 0.001f);

            Assert.AreEqual(60f, InvokeFloat(ruleSet, "EmergencyCooldownSecondsFor", 4), 0.001f);
            Assert.AreEqual(75f, InvokeFloat(ruleSet, "EmergencyCooldownSecondsFor", 6), 0.001f);
            Assert.AreEqual(90f, InvokeFloat(ruleSet, "EmergencyCooldownSecondsFor", 8), 0.001f);

            Assert.AreEqual(1.25f, InvokeFloat(ruleSet, "ReportRangeFor", 4), 0.001f);
            Assert.AreEqual(1.35f, InvokeFloat(ruleSet, "ReportRangeFor", 6), 0.001f);
            Assert.AreEqual(1.5f, InvokeFloat(ruleSet, "ReportRangeFor", 8), 0.001f);

            Assert.AreEqual(5f, InvokeFloat(ruleSet, "ReportCooldownSecondsFor", 4), 0.001f);
            Assert.AreEqual(5f, InvokeFloat(ruleSet, "ReportCooldownSecondsFor", 6), 0.001f);
            Assert.AreEqual(6f, InvokeFloat(ruleSet, "ReportCooldownSecondsFor", 8), 0.001f);

            Assert.AreEqual(12f, InvokeFloat(ruleSet, "FirstKillMinDelaySecondsFor", 4), 0.001f);
            Assert.AreEqual(10f, InvokeFloat(ruleSet, "FirstKillMinDelaySecondsFor", 6), 0.001f);
            Assert.AreEqual(8f, InvokeFloat(ruleSet, "FirstKillMinDelaySecondsFor", 8), 0.001f);

            Assert.AreEqual(3f, InvokeFloat(ruleSet, "PostMeetingKillGraceSecondsFor", 4), 0.001f);
            Assert.AreEqual(3f, InvokeFloat(ruleSet, "PostMeetingKillGraceSecondsFor", 6), 0.001f);
            Assert.AreEqual(4f, InvokeFloat(ruleSet, "PostMeetingKillGraceSecondsFor", 8), 0.001f);
        }

        [Test]
        public void BeginMeeting_UsesPlayerCountScaledDiscussionTimer()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Police");
                fixture.SetPlayer(4UL, Vector3.up, alive: true, roleName: "Gang");

                fixture.BeginMeeting("4P会议");

                Assert.AreEqual(30f, fixture.FieldFloat("phaseTimer"), 0.001f);
            }
        }

        [Test]
        public void StartMatch_AppliesPlayerCountScaledFirstKillDelayToGangSide()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.ConfigureRoom(minPlayers: 4, maxPlayers: 4, autoFillAi: false);
                fixture.SetPlayer(1UL, Vector3.zero, alive: true);
                fixture.SetPlayer(2UL, Vector3.right, alive: true);
                fixture.SetPlayer(3UL, Vector3.left, alive: true);
                fixture.SetPlayer(4UL, Vector3.up, alive: true);

                fixture.StartOnlineMatchCore();

                Assert.IsTrue(fixture.TryFindRoleWithKillCooldown("Gang", out float cooldown));
                Assert.AreEqual(12f, cooldown, 0.001f);
            }
        }

        [Test]
        public void TimeLimit_NotReached_ReturnsFalse()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false), (false, false));

            bool hasResult = TryTimeLimit(bridge, 100f, 300f, 5, 10, tasks, out string result);

            Assert.IsFalse(hasResult);
            Assert.IsEmpty(result);
        }

        [Test]
        public void TimeLimit_EvidenceHigh_PoliceWins()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 9, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("警方胜利", result);
        }

        [Test]
        public void TimeLimit_EvidenceLow_GangWins()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((false, false), (false, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 2, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("黑帮胜利", result);
        }

        [Test]
        public void TimeLimit_TasksHigh_PoliceWins()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false), (true, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 3, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("警方胜利", result);
        }

        [Test]
        public void DetermineChannel_DeadPlayer_Ghost()
        {
            AssertChannel("Action", false, "Ghost");
            AssertChannel("Meeting", false, "Ghost");
            AssertChannel("Voting", false, "Ghost");
            AssertChannel("Lobby", false, "Ghost");
        }

        [Test]
        public void DetermineChannel_AliveMeetingAndAction()
        {
            AssertChannel("Meeting", true, "Meeting");
            AssertChannel("Voting", true, "Meeting");
            AssertChannel("Action", true, "Proximity");
            AssertChannel("Lobby", true, "Proximity");
        }

        [Test]
        public void Sanitize_RemovesTagsAndTruncates()
        {
            Assert.AreEqual("hello", Sanitize("<b>hello</b>"));
            Assert.AreEqual("text", Sanitize("<script>alert('xss')</script>text"));
            Assert.AreEqual(256, Sanitize(new string('a', 600)).Length);
            Assert.AreEqual("Report body at Dockyard", Sanitize("Report body at Dockyard"));
            Assert.IsNull(Sanitize(null));
            Assert.AreEqual(string.Empty, Sanitize(string.Empty));
        }

        [Test]
        public void ChatSystem_BlocksMessagesFromMutedSender()
        {
            object chat = CreateChatSystem();
            Invoke(chat, "BlockSender", "7");

            ReceiveChatMessage(chat, "7", "刷屏玩家", "不应出现");
            ReceiveChatMessage(chat, "8", "正常玩家", "应该出现");

            Assert.AreEqual(1, PropertyInt(chat, "MessageCount"));
            object message = First((IEnumerable)Property(chat, "Messages"));
            Assert.AreEqual("8", FieldString(message, "SenderId"));
            Assert.IsTrue((bool)Invoke(chat, "IsSenderBlocked", "7"));
        }

        [Test]
        public void ChatSystem_UnblockSenderRestoresMessages()
        {
            object chat = CreateChatSystem();
            Invoke(chat, "BlockSender", "7");
            Invoke(chat, "UnblockSender", "7");

            ReceiveChatMessage(chat, "7", "恢复玩家", "可以显示");

            Assert.AreEqual(1, PropertyInt(chat, "MessageCount"));
            Assert.IsFalse((bool)Invoke(chat, "IsSenderBlocked", "7"));
        }

        [Test]
        public void ChatSystem_BlockLatestSenderBlocksFollowUpMessages()
        {
            object chat = CreateChatSystem();
            ReceiveChatMessage(chat, "7", "可疑玩家", "第一条");

            bool blocked = (bool)Invoke(chat, "BlockLatestSender");
            ReceiveChatMessage(chat, "7", "可疑玩家", "第二条");

            Assert.IsTrue(blocked);
            Assert.AreEqual(1, PropertyInt(chat, "MessageCount"));
            Assert.AreEqual(1, PropertyInt(chat, "BlockedSenderCount"));
        }

        [Test]
        public void ChatSystem_ReportLatestMessageStoresSanitizedSnapshot()
        {
            object chat = CreateChatSystem();
            ReceiveChatMessage(chat, "7", "<b>可疑玩家</b>", "<script>alert('x')</script>码头集合", channelName: "Ghost");

            bool reported = (bool)Invoke(chat, "ReportLatestMessage", "<b>辱骂/作弊</b>");

            Assert.IsTrue(reported);
            Assert.AreEqual(1, PropertyInt(chat, "ReportCount"));
            object report = First((IEnumerable)Property(chat, "Reports"));
            Assert.AreEqual("7", FieldString(report, "SenderId"));
            Assert.AreEqual("可疑玩家", FieldString(report, "SenderName"));
            Assert.AreEqual("码头集合", FieldString(report, "Content"));
            Assert.AreEqual("辱骂/作弊", FieldString(report, "Reason"));
            Assert.AreEqual("Ghost", FieldValueText(report, "Channel"));
        }

        [Test]
        public void ChatSystem_BuildsCanvasMessageFeed()
        {
            object chat = CreateChatSystem();
            ReceiveChatMessage(chat, "7", "警员甲", "码头安全", channelName: "Meeting");
            ReceiveChatMessage(chat, "8", "线人乙", "后巷有人", channelName: "Proximity");

            string feed = (string)Invoke(chat, "BuildMessageFeedText", 4);

            StringAssert.Contains("[会] 警员甲: 码头安全", feed);
            StringAssert.Contains("[近] 线人乙: 后巷有人", feed);
        }

        [Test]
        public void ChatSystem_ChannelDisplayNamesUseTextChatTerms()
        {
            Type chatType = RuntimeType("GanglandUndercover.Online.ChatSystem");
            Type channelType = RuntimeType("GanglandUndercover.Online.ChatChannel");
            object global = Enum.Parse(channelType, "Global");

            string display = (string)InvokeStatic(chatType, "ChannelDisplayName", global);

            Assert.AreEqual("全局频道", display);
        }

        [Test]
        public void MeetingSync_BeginClearsPreviousVotesAndRecordsReason()
        {
            List<string> caseLog = new List<string>();
            object meeting = CreateMeetingSync(caseLog);
            object actionPhase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), "Action");
            object meetingPhase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), "Meeting");

            Invoke(meeting, "Begin", "码头发现尸体", actionPhase);
            Invoke(meeting, "RegisterVote", 1UL, 2UL);

            Assert.AreEqual(1, InvokeInt(meeting, "VoteCount"));
            Assert.IsTrue(PropertyBool(meeting, "IsActive"));

            Invoke(meeting, "Begin", "警署紧急铃", meetingPhase);

            Assert.AreEqual(0, InvokeInt(meeting, "VoteCount"), "新会议必须清理上轮未结算票，避免票型污染。");
            Assert.AreEqual(2, PropertyInt(meeting, "MeetingCount"));
            Assert.AreEqual("警署紧急铃", PropertyString(meeting, "LastReason"));
            Assert.IsTrue(caseLog.Exists(line => line.Contains("警署紧急铃")));
        }

        [Test]
        public void MeetingSync_IgnoresVotesAndResolveWhenInactive()
        {
            List<string> caseLog = new List<string>();
            object meeting = CreateMeetingSync(caseLog);
            Dictionary<ulong, int> tally = new Dictionary<ulong, int> { { 2UL, 1 } };

            Invoke(meeting, "RegisterVote", 1UL, 2UL);
            Invoke(meeting, "Resolve", 2UL, false, tally);

            Assert.AreEqual(0, InvokeInt(meeting, "VoteCount"));
            Assert.AreEqual(string.Empty, PropertyString(meeting, "LastOutcome"));
            Assert.IsFalse(PropertyBool(meeting, "IsActive"));
            Assert.AreEqual(0, caseLog.Count, "未激活会议不应写案卷，避免客户端误显示假投票。");
        }

        [Test]
        public void MeetingSync_HasAllVotedRequiresActiveMeetingAndAlivePlayers()
        {
            object meeting = CreateMeetingSync(new List<string>());
            object meetingPhase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), "Meeting");

            Assert.IsFalse((bool)Invoke(meeting, "HasAllVoted", 0), "零存活/未激活不能被判为全员已投。");

            Invoke(meeting, "Begin", "会议开始", meetingPhase);

            Assert.IsFalse((bool)Invoke(meeting, "HasAllVoted", 0), "零存活不能触发自动结票。");
            Assert.IsFalse((bool)Invoke(meeting, "HasAllVoted", 2));

            Invoke(meeting, "RegisterVote", 1UL, 2UL);
            Assert.IsFalse((bool)Invoke(meeting, "HasAllVoted", 2));

            Invoke(meeting, "RegisterVote", 2UL, 1UL);
            Assert.IsTrue((bool)Invoke(meeting, "HasAllVoted", 2));
        }

        [Test]
        public void MeetingSync_EndFiresOnceAfterResolve()
        {
            List<string> caseLog = new List<string>();
            object meeting = CreateMeetingSync(caseLog);
            object meetingPhase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), "Meeting");
            int endedCount = 0;
            EventInfo ended = meeting.GetType().GetEvent("MeetingEnded");
            ended.AddEventHandler(meeting, new Action(() => endedCount++));

            Invoke(meeting, "Begin", "会议开始", meetingPhase);
            Invoke(meeting, "RegisterVote", 1UL, ulong.MaxValue);
            Invoke(meeting, "Resolve", ulong.MaxValue, false, new Dictionary<ulong, int>());

            Assert.IsFalse(PropertyBool(meeting, "IsActive"));

            Invoke(meeting, "End");
            Invoke(meeting, "End");

            Assert.AreEqual(1, endedCount, "Resolve 后仍必须发出一次会议结束事件，但重复 End 不能重复广播。");
            Assert.AreEqual(1, caseLog.FindAll(line => line.Contains("会议结束")).Count);
        }

        [Test]
        public void MainMenuLoginStatus_NoServiceExplainsAnonymousInitialization()
        {
            string status = BuildMainMenuLoginStatus(null);

            StringAssert.Contains("匿名账号", status);
            StringAssert.Contains("进入大厅", status);
            StringAssert.Contains("Cloud/Auth/Lobby/Relay", status);
        }

        [Test]
        public void MainMenuSettingsStatus_UsesCurrentSettingsValues()
        {
            object settings = InvokeStatic(RuntimeType("GanglandUndercover.UI.SettingsData"), "CreateDefault");
            settings.GetType().GetProperty("MasterVolume").SetValue(settings, 0.6f);
            settings.GetType().GetProperty("QualityPreset").SetValue(settings, 3);
            settings.GetType().GetProperty("WindowMode").SetValue(settings, 2);
            settings.GetType().GetProperty("ColorBlindMode").SetValue(settings, 1);
            settings.GetType().GetProperty("FrameRateCap").SetValue(settings, 120);
            settings.GetType().GetProperty("VSync").SetValue(settings, false);
            settings.GetType().GetProperty("VoiceMode").SetValue(settings, 1);

            string status = BuildMainMenuSettingsStatus(settings);

            StringAssert.Contains("音量 60%", status);
            StringAssert.Contains("画质 极致", status);
            StringAssert.Contains("无边框", status);
            StringAssert.Contains("120 FPS", status);
            StringAssert.Contains("VSync 关", status);
            StringAssert.Contains("自由发送", status);
            StringAssert.Contains("色盲 1", status);
        }

        [Test]
        public void MainMenuPlayerNameInput_TrimsFallbackAndCapsLength()
        {
            Type menuType = RuntimeType("GanglandUndercover.UI.MainMenuController");
            MethodInfo limitText = StaticNonPublic(menuType, "LimitText");

            Assert.AreEqual("港区玩家", limitText.Invoke(null, new object[] { "   ", 16, "港区玩家" }));
            Assert.AreEqual("九龙玩家", limitText.Invoke(null, new object[] { "  九龙玩家  ", 16, "港区玩家" }));
            Assert.AreEqual("abcdefghijklmnop", limitText.Invoke(null, new object[] { "abcdefghijklmnopq", 16, "港区玩家" }));
        }

        [Test]
        public void MainMenuSettingsPanel_BuildsClosedOverlay()
        {
            Type menuType = RuntimeType("GanglandUndercover.UI.MainMenuController");
            GameObject menuHost = new GameObject("MainMenuSettingsPanelHost");

            try
            {
                object menu = menuHost.AddComponent(menuType);

                menuType.GetMethod("Initialize").Invoke(menu, new object[] { null });

                Assert.IsFalse(PropertyBool(menu, "SettingsPanelVisible"));
                Assert.IsNotNull(FindObjectNamedIncludingInactive("SettingsPanel"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(menuHost);
                DestroyAllObjectsNamed("Settings Manager");
                DestroyAllObjectsNamed("UICanvas_Fallback");
            }
        }

        [Test]
        public void RelayLobbySummary_EmptyStateGuidesCreateOrJoin()
        {
            string summary = BuildRelayLobbySummary(
                "Relay 房间码未创建。",
                string.Empty,
                string.Empty,
                operationInProgress: false,
                isOnline: false,
                isHost: false,
                isClientConnected: false,
                connectedClientCount: 0);

            StringAssert.Contains("Relay 房间码未创建", summary);
            StringAssert.Contains("Relay 开房", summary);
            StringAssert.Contains("输入房间码", summary);
            StringAssert.Contains("晚测", summary);
            StringAssert.Contains("Host", summary);
            StringAssert.Contains("Client", summary);
        }

        [Test]
        public void RelayLobbySummary_InputGuidesRelayJoin()
        {
            string summary = BuildRelayLobbySummary(
                "Relay 房间码未创建。",
                string.Empty,
                " 6kb6dh ",
                operationInProgress: false,
                isOnline: false,
                isHost: false,
                isClientConnected: false,
                connectedClientCount: 0);

            StringAssert.Contains("已输入房间码 6KB6DH", summary);
            StringAssert.Contains("Relay 加入", summary);
            StringAssert.Contains("6 位大写", summary);
        }

        [Test]
        public void RelayLobbySummary_HostShowsShareCodeAndConnectedCount()
        {
            string summary = BuildRelayLobbySummary(
                "Relay 房间码: 6KB6DH",
                "6kb6dh",
                string.Empty,
                operationInProgress: false,
                isOnline: true,
                isHost: true,
                isClientConnected: true,
                connectedClientCount: 2);

            StringAssert.Contains("分享房间码 6KB6DH", summary);
            StringAssert.Contains("已连接 2 人", summary);
            StringAssert.Contains("截图房间码和人数", summary);
        }

        [Test]
        public void RelayLobbySummary_ClientJoinedGuidesWaitingAndScreenshot()
        {
            string summary = BuildRelayLobbySummary(
                "Relay 已加入 6KB6DH。",
                "6kb6dh",
                string.Empty,
                operationInProgress: false,
                isOnline: true,
                isHost: false,
                isClientConnected: true,
                connectedClientCount: 1);

            StringAssert.Contains("已加入房间码 6KB6DH", summary);
            StringAssert.Contains("等待 Host 开局", summary);
            StringAssert.Contains("截图玩家列表", summary);
        }

        [Test]
        public void RelayLobbySummary_OperationInProgressShowsJoinTarget()
        {
            string summary = BuildRelayLobbySummary(
                "Relay 正在加入 6KB6DH。",
                string.Empty,
                "6kb6dh",
                operationInProgress: true,
                isOnline: false,
                isHost: false,
                isClientConnected: false,
                connectedClientCount: 0);

            StringAssert.Contains("正在加入 6KB6DH", summary);
            StringAssert.Contains("请稍候", summary);
            StringAssert.Contains("超过 20 秒", summary);
        }

        [Test]
        public void RelayLobbySummary_DisconnectedHostGuidesReturnAndScreenshot()
        {
            string summary = BuildRelayLobbySummary(
                "Host 已断开，房间码 6KB6DH 已失效。",
                "6kb6dh",
                string.Empty,
                operationInProgress: false,
                isOnline: false,
                isHost: false,
                isClientConnected: false,
                connectedClientCount: 0);

            StringAssert.Contains("Host 已断开", summary);
            StringAssert.Contains("已失效", summary);
            StringAssert.Contains("返回主菜单", summary);
            StringAssert.Contains("重新开房", summary);
            StringAssert.Contains("截图", summary);
        }

        [Test]
        public void RelayLobbySummary_DisconnectedHostOverridesStaleClientConnectedState()
        {
            string summary = BuildRelayLobbySummary(
                "Host 已断开，房间码 6KB6DH 已失效。",
                "6kb6dh",
                string.Empty,
                operationInProgress: false,
                isOnline: true,
                isHost: false,
                isClientConnected: true,
                connectedClientCount: 1);

            StringAssert.Contains("旧房间码已失效", summary);
            StringAssert.Contains("重新开房", summary);
            Assert.IsFalse(summary.Contains("等待 Host 开局"), "Host 断开回调瞬间不能继续提示 Client 等待旧房间。");
        }

        [Test]
        public void LobbyBrowserSummary_RefreshInProgressMentionsRoomCount()
        {
            string summary = BuildLobbyBrowserSummary(
                "Lobby 正在刷新房间列表。",
                refreshInProgress: true,
                visibleRoomCount: 3,
                selectedIndex: 0);

            StringAssert.Contains("正在刷新", summary);
            StringAssert.Contains("3 间", summary);
            StringAssert.Contains("选中第 1 间", summary);
        }

        [Test]
        public void LobbyBrowserSummary_EmptyStateGuidesRelayFallback()
        {
            string summary = BuildLobbyBrowserSummary(
                "Lobby 房间列表为空。",
                refreshInProgress: false,
                visibleRoomCount: 0,
                selectedIndex: -1);

            StringAssert.Contains("房间列表为空", summary);
            StringAssert.Contains("Relay 房间码", summary);
        }

        [Test]
        public void LobbyRoomLine_ShowsJoinableRelayCodeAndRules()
        {
            string line = BuildLobbyRoomLine(
                displayIndex: 1,
                roomName: "九龙港区夜局",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                mapName: "Harbour",
                ruleSummary: "AI补位",
                relayCode: "6kb6dh");

            StringAssert.Contains("1. 九龙港区夜局", line);
            StringAssert.Contains("2/8", line);
            StringAssert.Contains("可加入", line);
            StringAssert.Contains("6KB6DH", line);
            StringAssert.Contains("AI补位", line);
        }

        [Test]
        public void LobbyRoomLine_MissingRelayCodeIsVisibleButNotJoinable()
        {
            string line = BuildLobbyRoomLine(
                displayIndex: 2,
                roomName: "公开测试房",
                playerCount: 1,
                maxPlayers: 6,
                isLocked: false,
                hasPassword: false,
                mapName: "Harbour",
                ruleSummary: string.Empty,
                relayCode: string.Empty);

            StringAssert.Contains("2. 公开测试房", line);
            StringAssert.Contains("待发布 Relay", line);
        }

        [Test]
        public void LobbySessionProperties_MatchBrowserQueryIndex()
        {
            IDictionary properties = BuildRelayLobbySessionProperties(
                relayCode: " 6kb6dh ",
                mapName: "警署",
                ruleSummary: "AI补位");

            object gameProperty = properties["game"];
            object relayProperty = properties["relayCode"];
            object mapProperty = properties["map"];
            object queryOptions = BuildLobbyQueryOptions();
            IEnumerable filters = (IEnumerable)queryOptions.GetType().GetProperty("FilterOptions").GetValue(queryOptions);
            object firstFilter = First(filters);

            Assert.AreEqual("gangland-undercover", PropertyString(gameProperty, "Value"));
            Assert.AreEqual("Public", PropertyValueText(gameProperty, "Visibility"));
            Assert.AreEqual("String1", PropertyValueText(gameProperty, "Index"));
            Assert.AreEqual("6KB6DH", PropertyString(relayProperty, "Value"));
            Assert.AreEqual("String2", PropertyValueText(relayProperty, "Index"));
            Assert.AreEqual("警署", PropertyString(mapProperty, "Value"));
            Assert.AreEqual("String3", PropertyValueText(mapProperty, "Index"));
            Assert.AreEqual("StringIndex1", PropertyValueText(firstFilter, "Field"));
            Assert.AreEqual("gangland-undercover", PropertyString(firstFilter, "Value"));
        }

        [Test]
        public void RelayLobbySessionOptions_ArePublicSearchableAndClamped()
        {
            object options = BuildRelayLobbySessionOptions(
                roomName: " 公开测试房 ",
                maxPlayers: 0,
                relayCode: " abc123 ",
                mapName: string.Empty,
                ruleSummary: string.Empty);
            IDictionary properties = (IDictionary)options.GetType().GetProperty("SessionProperties").GetValue(options);

            Assert.AreEqual("gangland-undercover", PropertyString(options, "Type"));
            Assert.AreEqual("公开测试房", PropertyString(options, "Name"));
            Assert.AreEqual(1, PropertyInt(options, "MaxPlayers"));
            Assert.IsFalse(PropertyBool(options, "IsPrivate"));
            Assert.IsFalse(PropertyBool(options, "IsLocked"));
            Assert.AreEqual("ABC123", PropertyString(properties["relayCode"], "Value"));
            Assert.AreEqual("地图待定", PropertyString(properties["map"], "Value"));
            Assert.AreEqual("默认规则", PropertyString(properties["rules"], "Value"));
        }

        [Test]
        public void LobbyPublishStatus_ShowsProgressAndSessionCode()
        {
            string publishing = BuildLobbyPublishStatus(publishInProgress: true, published: false, sessionCode: string.Empty);
            string published = BuildLobbyPublishStatus(publishInProgress: false, published: true, sessionCode: " ab12cd ");

            StringAssert.Contains("正在发布", publishing);
            StringAssert.Contains("已发布", published);
            StringAssert.Contains("AB12CD", published);
        }

        [Test]
        public void LobbyRoomSessionJoin_CanUseSessionWhenIdAndRelayCodeExist()
        {
            object result = BuildLobbyRoomSessionJoin(
                sessionId: "session-123",
                relayCode: " 6kb6dh ",
                playerCount: 2,
                maxPlayers: 6,
                isLocked: false,
                hasPassword: false,
                allowLocalPreview: false);

            Assert.IsTrue(FieldBool(result, "CanJoinRelay"));
            Assert.IsTrue(FieldBool(result, "CanJoinSession"));
            Assert.AreEqual("session-123", FieldString(result, "SessionId"));
            Assert.AreEqual("6KB6DH", FieldString(result, "RelayCode"));
            StringAssert.Contains("Session", FieldString(result, "StatusText"));
        }

        [Test]
        public void LobbyRoomSessionJoin_FallsBackForLocalPreviewOrMissingSessionId()
        {
            object localPreview = BuildLobbyRoomSessionJoin(
                sessionId: "local-relay-host",
                relayCode: "6kb6dh",
                playerCount: 1,
                maxPlayers: 6,
                isLocked: false,
                hasPassword: false,
                allowLocalPreview: true);
            object missingSession = BuildLobbyRoomSessionJoin(
                sessionId: string.Empty,
                relayCode: "6kb6dh",
                playerCount: 1,
                maxPlayers: 6,
                isLocked: false,
                hasPassword: false,
                allowLocalPreview: false);

            Assert.IsTrue(FieldBool(localPreview, "CanJoinRelay"));
            Assert.IsTrue(FieldBool(missingSession, "CanJoinRelay"));
            Assert.IsFalse(FieldBool(localPreview, "CanJoinSession"));
            Assert.IsFalse(FieldBool(missingSession, "CanJoinSession"));
            Assert.AreEqual("6KB6DH", FieldString(localPreview, "RelayCode"));
            StringAssert.Contains("Relay", FieldString(localPreview, "StatusText"));
            StringAssert.Contains("Relay", FieldString(missingSession, "StatusText"));
        }

        [Test]
        public void LobbyRoomSessionJoin_BlocksLockedRooms()
        {
            object result = BuildLobbyRoomSessionJoin(
                sessionId: "session-123",
                relayCode: "6kb6dh",
                playerCount: 2,
                maxPlayers: 6,
                isLocked: true,
                hasPassword: false,
                allowLocalPreview: false);

            Assert.IsFalse(FieldBool(result, "CanJoinRelay"));
            Assert.IsFalse(FieldBool(result, "CanJoinSession"));
            StringAssert.Contains("锁定", FieldString(result, "StatusText"));
        }

        [Test]
        public void LobbyRoomSessionJoin_BlocksPasswordRooms()
        {
            object result = BuildLobbyRoomSessionJoin(
                sessionId: "session-123",
                relayCode: "6kb6dh",
                playerCount: 2,
                maxPlayers: 6,
                isLocked: false,
                hasPassword: true,
                allowLocalPreview: false);

            Assert.IsFalse(FieldBool(result, "CanJoinRelay"));
            Assert.IsFalse(FieldBool(result, "CanJoinSession"));
            StringAssert.Contains("密码", FieldString(result, "StatusText"));
        }

        [Test]
        public void LobbyRoomSessionJoin_BlocksFullRooms()
        {
            object result = BuildLobbyRoomSessionJoin(
                sessionId: "session-123",
                relayCode: "6kb6dh",
                playerCount: 6,
                maxPlayers: 6,
                isLocked: false,
                hasPassword: false,
                allowLocalPreview: false);

            Assert.IsFalse(FieldBool(result, "CanJoinRelay"));
            Assert.IsFalse(FieldBool(result, "CanJoinSession"));
            StringAssert.Contains("已满", FieldString(result, "StatusText"));
        }

        [Test]
        public void LobbyRoomSessionJoin_BlocksMissingRelayCode()
        {
            object result = BuildLobbyRoomSessionJoin(
                sessionId: "session-123",
                relayCode: string.Empty,
                playerCount: 2,
                maxPlayers: 6,
                isLocked: false,
                hasPassword: false,
                allowLocalPreview: false);

            Assert.IsFalse(FieldBool(result, "CanJoinRelay"));
            Assert.IsFalse(FieldBool(result, "CanJoinSession"));
            StringAssert.Contains("Relay 房间码", FieldString(result, "StatusText"));
        }

        [Test]
        public void ChatSendPayload_RoundTripsContentWithPipes()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            const string expected = "码头|口供|A区";
            FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp);

            try
            {
                object[] writeArgs = { writer, expected };
                StaticNonPublic(controllerType, "WriteChatSendPayload").Invoke(null, writeArgs);
                writer = (FastBufferWriter)writeArgs[0];

                FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);

                try
                {
                    object[] readArgs = { reader, null };
                    bool decoded = (bool)StaticNonPublic(controllerType, "TryReadChatSendPayload").Invoke(null, readArgs);

                    Assert.IsTrue(decoded);
                    Assert.AreEqual(expected, readArgs[1]);
                }
                finally
                {
                    reader.Dispose();
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        [Test]
        public void ChatBroadcastPayload_RoundTripsContentWithPipes()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            object faction = Enum.Parse(RuntimeType("GanglandUndercover.Core.Faction"), "Gang");
            object channel = Enum.Parse(RuntimeType("GanglandUndercover.Online.ChatChannel"), "Ghost");
            const string expectedSenderId = "7";
            const string expectedSenderName = "卧底甲";
            const string expectedContent = "证据|灭口|后巷";
            FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp);

            try
            {
                object[] writeArgs = { writer, expectedSenderId, expectedSenderName, expectedContent, true, faction, channel };
                StaticNonPublic(controllerType, "WriteChatBroadcastPayload").Invoke(null, writeArgs);
                writer = (FastBufferWriter)writeArgs[0];

                FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);

                try
                {
                    object[] readArgs = { reader, null, null, null, null, null, null };
                    bool decoded = (bool)StaticNonPublic(controllerType, "TryReadChatBroadcastPayload").Invoke(null, readArgs);

                    Assert.IsTrue(decoded);
                    Assert.AreEqual(expectedSenderId, readArgs[1]);
                    Assert.AreEqual(expectedSenderName, readArgs[2]);
                    Assert.AreEqual(expectedContent, readArgs[3]);
                    Assert.AreEqual(true, readArgs[4]);
                    Assert.AreEqual(faction, readArgs[5]);
                    Assert.AreEqual(channel, readArgs[6]);
                }
                finally
                {
                    reader.Dispose();
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        [Test]
        public void CharacterCustomPayload_RoundTripsObjectIdAndJson()
        {
            Type customizerType = RuntimeType("GanglandUndercover.SocialDeduction.CharacterCustomizer");
            const ulong expectedObjectId = 42UL;
            const string expectedJson = "{\"hat\":\"cap_basic\",\"top\":\"jacket|noir\"}";
            FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp);

            try
            {
                object[] writeArgs = { writer, expectedObjectId, expectedJson };
                bool encoded = (bool)StaticNonPublic(customizerType, "TryWriteCustomMessagePayload").Invoke(null, writeArgs);
                writer = (FastBufferWriter)writeArgs[0];

                Assert.IsTrue(encoded);

                FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);

                try
                {
                    object[] readArgs = { reader, 0UL, null };
                    bool decoded = (bool)StaticNonPublic(customizerType, "TryReadCustomMessagePayload").Invoke(null, readArgs);

                    Assert.IsTrue(decoded);
                    Assert.AreEqual(expectedObjectId, readArgs[1]);
                    Assert.AreEqual(expectedJson, readArgs[2]);
                }
                finally
                {
                    reader.Dispose();
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        [Test]
        public void CharacterCustomPayload_RejectsOversizedJson()
        {
            Type customizerType = RuntimeType("GanglandUndercover.SocialDeduction.CharacterCustomizer");
            string oversizedJson = "{\"hat\":\"" + new string('a', 3000) + "\"}";
            FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp);

            try
            {
                object[] writeArgs = { writer, 42UL, oversizedJson };
                bool encoded = (bool)StaticNonPublic(customizerType, "TryWriteCustomMessagePayload").Invoke(null, writeArgs);

                Assert.IsFalse(encoded);
            }
            finally
            {
                writer.Dispose();
            }
        }

        [Test]
        public void Victory_EvidenceClosure_PoliceWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 50,
                evidenceTarget: 44,
                players: MakePlayers((2, "Gang"), (4, "Police"), (1, "Undercover"), (1, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "警方胜利");
        }

        [Test]
        public void Victory_AllGangDead_PoliceWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayers((0, "Gang"), (3, "Police"), (0, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "警方胜利");
        }

        [Test]
        public void Victory_GangOutnumber_GangWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayers((3, "Gang"), (2, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "黑帮胜利");
        }

        [Test]
        public void Victory_NoChange_WhenBalanced()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayers((2, "Gang"), (4, "Police"), (1, "Undercover"), (1, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            Assert.IsFalse(PropertyBool(result, "HasResult"));
        }

        [Test]
        public void Victory_UndercoverSoloWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 5,
                evidenceTarget: 44,
                players: MakePlayers((0, "Gang"), (0, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "卧底胜利");
        }

        [Test]
        public void Victory_NotStarted_ReturnsNoChange()
        {
            object result = EvaluateVictory(
                evidenceScore: 100,
                evidenceTarget: 44,
                players: MakePlayers((2, "Gang"), (4, "Police"), (1, "Undercover"), (1, "Mole")),
                tasks: MakeTasks(),
                matchStarted: false,
                phaseName: "Lobby");

            Assert.IsFalse(PropertyBool(result, "HasResult"));
        }

        [Test]
        public void Voting_MajoritySkipDoesNotEjectSingleAccusedPlayer()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Meeting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");

                fixture.ApplyVote(1UL, 3UL);
                fixture.ApplySkipVote(2UL);
                fixture.ApplySkipVote(3UL);

                Assert.AreEqual("Action", fixture.PhaseName(), "全员投票后应结束会议回到行动阶段。");
                Assert.IsTrue(fixture.PlayerAlive(3UL), "多数跳过时，少数被投玩家不能被淘汰。");
                StringAssert.Contains("无人出局", fixture.PropertyString("LastVoteOutcome"));
                StringAssert.Contains("跳过", fixture.PropertyString("LastVoteOutcome"));
            }
        }

        [Test]
        public void Voting_ResultAfterEjectionStillEndsMeetingSync()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                int endedCount = 0;
                fixture.AttachSyncManager();
                fixture.SubscribeMeetingEnded(() => endedCount++);
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.BeginMeeting("结果分支会议");

                fixture.ApplyVote(1UL, 2UL);
                fixture.ApplyVote(2UL, 2UL);

                Assert.AreEqual("Result", fixture.PhaseName(), "投出最后黑帮应进入结算。");
                Assert.AreEqual(1, endedCount, "即使投票后直接结算，也必须发出会议结束事件。");
            }
        }

        [Test]
        public void TaskCompletion_RejectsDirectSubmitWithoutActiveLock()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                fixture.SetPlayer(7UL, Vector3.zero, alive: true);

                bool accepted = fixture.InvokeBoolOutString("ValidateAndCompleteTask", 7UL, 0, out string error);

                Assert.IsFalse(accepted);
                StringAssert.Contains("任务未开始", error);
            }
        }

        [Test]
        public void TaskCompletion_RejectsDirectSubmitOutsideRange()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                fixture.SetPlayer(7UL, new Vector3(100f, 100f, 0f), alive: true);
                fixture.MarkTaskActive(7UL, 0);

                bool accepted = fixture.InvokeBoolOutString("ValidateAndCompleteTask", 7UL, 0, out string error);

                Assert.IsFalse(accepted);
                StringAssert.Contains("距离任务点太远", error);
            }
        }

        [Test]
        public void RepairStart_RejectsSabotagedTaskOutsideRange()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: true);
                fixture.SetPlayer(7UL, new Vector3(100f, 100f, 0f), alive: true);

                bool accepted = fixture.InvokeBoolOutString("ValidateRepairStart", 7UL, 0, out string reason);

                Assert.IsFalse(accepted);
                StringAssert.Contains("距离任务点太远", reason);
            }
        }

        [Test]
        public void RepairCompletion_RejectsDirectSubmitWithoutActiveLock()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: true);
                fixture.SetPlayer(7UL, Vector3.zero, alive: true);

                bool accepted = fixture.InvokeBoolOutString("ValidateAndRepairTask", 7UL, 0, out string error);

                Assert.IsFalse(accepted);
                StringAssert.Contains("任务未开始", error);
            }
        }

        [Test]
        public void ClientState_RejectsNonFinitePositionAndInput()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Lobby");
                fixture.SetPlayer(7UL, new Vector3(2f, 3f, 0f), alive: true);

                fixture.ApplyClientState(
                    7UL,
                    new Vector3(float.NaN, 9f, 0f),
                    new Vector2(float.PositiveInfinity, 1f),
                    false);

                Assert.AreEqual(new Vector3(2f, 3f, 0f), fixture.PlayerPosition(7UL));
                Assert.AreEqual(Vector2.zero, fixture.PlayerInput(7UL));
                Assert.IsTrue(fixture.PlayerReady(7UL));
            }
        }

        [Test]
        public void ClientState_ClampsActionInputAndIgnoresReadyChangesDuringMatch()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(7UL, new Vector3(2f, 3f, 0f), alive: true);
                fixture.SetPlayerReady(7UL, false);

                fixture.ApplyClientState(7UL, new Vector3(50f, 50f, 0f), new Vector2(12f, 0f), true);

                Assert.AreEqual(new Vector3(2f, 3f, 0f), fixture.PlayerPosition(7UL));
                Assert.AreEqual(new Vector2(1f, 0f), fixture.PlayerInput(7UL));
                Assert.IsFalse(fixture.PlayerReady(7UL));
            }
        }

        [Test]
        public void ClientState_DoesNotSpawnUnknownPlayersAfterActionStarts()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");

                fixture.ApplyClientState(99UL, new Vector3(4f, 5f, 0f), Vector2.right, true);

                Assert.IsFalse(fixture.HasPlayer(99UL));
            }
        }

        [Test]
        public void ClientAction_RejectsUndefinedActionValues()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            MethodInfo method = StaticNonPublic(controllerType, "IsDefinedOnlineAction");

            Assert.IsTrue((bool)method.Invoke(null, new object[] { 0 }));
            Assert.IsTrue((bool)method.Invoke(null, new object[] { 6 }));
            Assert.IsFalse((bool)method.Invoke(null, new object[] { -1 }));
            Assert.IsFalse((bool)method.Invoke(null, new object[] { 1000 }));
        }

        [Test]
        public void ServerSnapshot_IgnoresNonServerSender()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetMatchStarted(false);
                fixture.SetPhase("Lobby");

                fixture.ReceiveServerSnapshot(42UL, matchStarted: true, phaseName: "Action");

                Assert.IsFalse(fixture.MatchStarted());
                Assert.AreEqual("Lobby", fixture.PhaseName());
            }
        }

        [Test]
        public void ServerSnapshot_RejectsInvalidPhaseAndCounts()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetMatchStarted(false);
                fixture.SetPhase("Lobby");

                fixture.ReceiveServerSnapshotRaw(NetworkManager.ServerClientId, matchStarted: true, phaseValue: 999);
                Assert.IsFalse(fixture.MatchStarted());
                Assert.AreEqual("Lobby", fixture.PhaseName());

                fixture.ReceiveServerSnapshotRaw(
                    NetworkManager.ServerClientId,
                    matchStarted: true,
                    phaseValue: fixture.PhaseValue("Action"),
                    playerCount: -1);

                Assert.IsFalse(fixture.MatchStarted());
                Assert.AreEqual("Lobby", fixture.PhaseName());
            }
        }

        [Test]
        public void ServerSnapshot_DoesNotPartiallyApplyWhenLaterCountsAreInvalid()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetMatchStarted(false);
                fixture.SetPhase("Lobby");

                fixture.ReceiveServerSnapshotRaw(
                    NetworkManager.ServerClientId,
                    matchStarted: true,
                    phaseValue: fixture.PhaseValue("Action"),
                    taskCount: -1);

                Assert.IsFalse(fixture.MatchStarted());
                Assert.AreEqual("Lobby", fixture.PhaseName());
            }
        }

        [Test]
        public void RoleAssign_IgnoresNonServerSenderAndUndefinedRoles()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalRole("Unassigned");

                fixture.ReceiveRoleAssign(7UL, fixture.RoleValue("Gang"));
                Assert.AreEqual("Unassigned", fixture.LocalRoleName());

                fixture.ReceiveRoleAssign(NetworkManager.ServerClientId, 999);
                Assert.AreEqual("Unassigned", fixture.LocalRoleName());

                fixture.ReceiveRoleAssign(NetworkManager.ServerClientId, fixture.RoleValue("Police"));
                Assert.AreEqual("Police", fixture.LocalRoleName());
            }
        }

        [Test]
        public void MapSelect_IgnoresUndefinedMapType()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetActiveMapType("HarbourDistrict");

                fixture.ReceiveMapSelect(NetworkManager.ServerClientId, 999);
                Assert.AreEqual("HarbourDistrict", fixture.ActiveMapTypeName());

                fixture.ReceiveMapSelect(NetworkManager.ServerClientId, fixture.MapTypeValue("PoliceStation"));
                Assert.AreEqual("PoliceStation", fixture.ActiveMapTypeName());
            }
        }

        [Test]
        public void OnboardingGuidance_LobbyShowsIdentityObjectiveAndActionPrompt()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Lobby");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Inspector");
                fixture.SetSingleTask(2, new Vector3(1f, 0f, 0f), completed: false, sabotaged: false);

                Assert.IsTrue(fixture.PropertyBool("HasOnboardingGuidance"));
                StringAssert.Contains("身份", fixture.PropertyString("OnboardingBriefingTitle"));
                StringAssert.Contains("身份", fixture.PropertyString("OnboardingBriefingBody"));
                StringAssert.Contains("目标", fixture.PropertyString("OnboardingBriefingBody"));
                StringAssert.Contains("Ready", fixture.PropertyString("OnboardingActionPrompt"));
            }
        }

        [Test]
        public void OnboardingGuidance_GangActionPromptsSabotageAndVotingCover()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Gang", professionName: "Enforcer");
                fixture.SetSingleTask(3, new Vector3(1f, 0f, 0f), completed: false, sabotaged: false);

                Assert.IsTrue(fixture.PropertyBool("HasOnboardingGuidance"));
                StringAssert.Contains("黑帮", fixture.PropertyString("OnboardingBriefingBody"));
                StringAssert.Contains("误导", fixture.PropertyString("OnboardingBriefingBody"));
                StringAssert.Contains("破坏", fixture.PropertyString("OnboardingActionPrompt"));
                StringAssert.Contains("Q", fixture.PropertyString("OnboardingActionPrompt"));
            }
        }

        [Test]
        public void DistrictMap_UsesOperationalLightingInsteadOfRandomNeonSpots()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            Type mapServiceType = RuntimeType("GanglandUndercover.Online.OnlineMapService");
            GameObject worldRoot = new GameObject("WorldBuilderVisualRegressionRoot");
            GameObject mapHost = new GameObject("WorldBuilderVisualRegressionMapService");

            try
            {
                object builder = Activator.CreateInstance(builderType);
                object mapService = mapHost.AddComponent(mapServiceType);
                var solidObstacles = new List<Rect>();
                var walkableAreas = new List<Rect>();
                var labels = new List<TextMesh>();

                Invoke(builder, "Initialize", worldRoot, mapService, solidObstacles, walkableAreas, labels, 8);
                Invoke(builder, "EnsureRuntimeSprites");
                Invoke(builder, "BuildDistrictMap");

                Assert.GreaterOrEqual(PropertyInt(builder, "OperationalLightingElementCount"), 16);
                Assert.GreaterOrEqual(CountChildrenStartingWith(worldRoot.transform, "行动照明 "), 16);
                Assert.AreEqual(0, CountLegacyRandomNeonSpotNames(worldRoot.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldRoot);
                UnityEngine.Object.DestroyImmediate(mapHost);
            }
        }

        [Test]
        public void WorldBuilder_LoadsCuratedLimeZuTilesBeforeFallbackTiles()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            Type mapServiceType = RuntimeType("GanglandUndercover.Online.OnlineMapService");
            GameObject worldRoot = new GameObject("WorldBuilderLimeZuTileRegressionRoot");
            GameObject mapHost = new GameObject("WorldBuilderLimeZuTileRegressionMapService");

            try
            {
                object builder = Activator.CreateInstance(builderType);
                object mapService = mapHost.AddComponent(mapServiceType);
                var solidObstacles = new List<Rect>();
                var walkableAreas = new List<Rect>();
                var labels = new List<TextMesh>();

                Invoke(builder, "Initialize", worldRoot, mapService, solidObstacles, walkableAreas, labels, 8);
                Invoke(builder, "EnsureRuntimeSprites");
                Invoke(builder, "CreateTiledFloor", "LimeZuTileProbe", Vector3.zero, new Vector2(1f, 1f), Color.white);

                Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/floors/asphalt-48-a", PropertyString(builder, "FloorTileResourcePath"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldRoot);
                UnityEngine.Object.DestroyImmediate(mapHost);
            }
        }

        [Test]
        public void WorldBuilder_FirstScreenUsesLimeZuRuntimeSprites()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            Type mapServiceType = RuntimeType("GanglandUndercover.Online.OnlineMapService");
            GameObject worldRoot = new GameObject("WorldBuilderLimeZuFirstScreenRegressionRoot");
            GameObject mapHost = new GameObject("WorldBuilderLimeZuFirstScreenRegressionMapService");

            try
            {
                object builder = Activator.CreateInstance(builderType);
                object mapService = mapHost.AddComponent(mapServiceType);
                var solidObstacles = new List<Rect>();
                var walkableAreas = new List<Rect>();
                var labels = new List<TextMesh>();

                Invoke(builder, "Initialize", worldRoot, mapService, solidObstacles, walkableAreas, labels, 8);
                Invoke(builder, "EnsureRuntimeSprites");
                Invoke(builder, "BuildDistrictMap");

                Assert.GreaterOrEqual(PropertyInt(builder, "LimeZuFirstScreenSpriteElementCount"), 32);
                Assert.GreaterOrEqual(CountChildrenContaining(worldRoot.transform, "LimeZu"), 32);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldRoot);
                UnityEngine.Object.DestroyImmediate(mapHost);
            }
        }

        [Test]
        public void WorldBuilder_TaskStationsUseLimeZuRuntimeSprites()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            Type mapServiceType = RuntimeType("GanglandUndercover.Online.OnlineMapService");
            Type taskType = RuntimeType("GanglandUndercover.Online.OnlineTaskState");
            GameObject worldRoot = new GameObject("WorldBuilderLimeZuTaskStationRegressionRoot");
            GameObject mapHost = new GameObject("WorldBuilderLimeZuTaskStationRegressionMapService");

            try
            {
                object builder = Activator.CreateInstance(builderType);
                object mapService = mapHost.AddComponent(mapServiceType);
                var solidObstacles = new List<Rect>();
                var walkableAreas = new List<Rect>();
                var labels = new List<TextMesh>();
                object task = Activator.CreateInstance(taskType, 3, "CCTV", Vector3.zero, 0, 1, false, false);

                Invoke(builder, "Initialize", worldRoot, mapService, solidObstacles, walkableAreas, labels, 8);
                Invoke(builder, "EnsureRuntimeSprites");
                Invoke(builder, "CreateTaskVisual", task, worldRoot.transform);

                Assert.GreaterOrEqual(PropertyInt(builder, "LimeZuTaskStationSpriteElementCount"), 3);
                Assert.GreaterOrEqual(CountChildrenContaining(worldRoot.transform, "LimeZu TaskStation"), 3);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldRoot);
                UnityEngine.Object.DestroyImmediate(mapHost);
            }
        }

        [Test]
        public void WorldBuilder_AddsReadableLandmarksAndTaskEventFeedback()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            Type mapServiceType = RuntimeType("GanglandUndercover.Online.OnlineMapService");
            Type taskType = RuntimeType("GanglandUndercover.Online.OnlineTaskState");
            GameObject worldRoot = new GameObject("WorldBuilderLandmarkFeedbackRegressionRoot");
            GameObject mapHost = new GameObject("WorldBuilderLandmarkFeedbackRegressionMapService");

            try
            {
                object builder = Activator.CreateInstance(builderType);
                object mapService = mapHost.AddComponent(mapServiceType);
                var solidObstacles = new List<Rect>();
                var walkableAreas = new List<Rect>();
                var labels = new List<TextMesh>();
                object cleanTask = Activator.CreateInstance(taskType, 3, "CCTV", Vector3.zero, 0, 1, false, false);
                object sabotagedTask = Activator.CreateInstance(taskType, 3, "CCTV", Vector3.zero, 0, 1, false, true);

                Invoke(builder, "Initialize", worldRoot, mapService, solidObstacles, walkableAreas, labels, 8);
                Invoke(builder, "EnsureRuntimeSprites");
                Invoke(builder, "BuildDistrictMap");
                GameObject taskVisual = (GameObject)Invoke(builder, "CreateTaskVisual", cleanTask, worldRoot.transform);
                Invoke(builder, "SetTaskVisualState", taskVisual, sabotagedTask);

                Assert.GreaterOrEqual(CountChildrenStartingWith(worldRoot.transform, "关键地标"), 12);
                Assert.GreaterOrEqual(CountChildrenContaining(worldRoot.transform, "关键地标 LimeZu"), 12);
                Assert.GreaterOrEqual(PropertyInt(builder, "LimeZuLandmarkSpriteElementCount"), 12);
                Assert.GreaterOrEqual(CountChildrenStartingWith(taskVisual.transform, "事件反馈"), 4);
                Assert.GreaterOrEqual(CountChildrenContaining(taskVisual.transform, "事件反馈 LimeZu"), 2);
                Assert.GreaterOrEqual(PropertyInt(builder, "LimeZuTaskEventFeedbackSpriteElementCount"), 2);
                Assert.GreaterOrEqual(CountActiveChildrenStartingWith(taskVisual.transform, "事件反馈 破坏"), 3);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldRoot);
                UnityEngine.Object.DestroyImmediate(mapHost);
            }
        }

        [Test]
        public void WorldBuilder_UsesCuratedLimeZuRoomPropsAndBlackoutVfx()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            Type mapServiceType = RuntimeType("GanglandUndercover.Online.OnlineMapService");
            GameObject worldRoot = new GameObject("WorldBuilderRoomPropRegressionRoot");
            GameObject mapHost = new GameObject("WorldBuilderRoomPropRegressionMapService");

            try
            {
                object builder = Activator.CreateInstance(builderType);
                object mapService = mapHost.AddComponent(mapServiceType);
                var solidObstacles = new List<Rect>();
                var walkableAreas = new List<Rect>();
                var labels = new List<TextMesh>();

                Invoke(builder, "Initialize", worldRoot, mapService, solidObstacles, walkableAreas, labels, 8);
                Invoke(builder, "EnsureRuntimeSprites");
                Invoke(builder, "BuildDistrictMap");

                Assert.GreaterOrEqual(PropertyInt(builder, "LimeZuRoomPropSpriteElementCount"), 75);
                Assert.GreaterOrEqual(CountChildrenStartingWith(worldRoot.transform, "房间实物 LimeZu"), 75);
                Assert.GreaterOrEqual(CountChildrenStartingWith(worldRoot.transform, "Blackout VFX"), 5);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldRoot);
                UnityEngine.Object.DestroyImmediate(mapHost);
            }
        }

        [Test]
        public void Sprite2DAssetCache_LoadsCuratedLimeZuSpritesByExplicitPath()
        {
            Type cacheType = RuntimeType("GanglandUndercover.Art.Sprite2DAssetCache");

            InvokeStatic(cacheType, "Ensure");

            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/floors/room-builder-floors-16", StaticPropertyString(cacheType, "FloorTileAltResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/floors/asphalt-48-a", StaticPropertyString(cacheType, "CorridorTileResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/walls/room-builder-walls-16", StaticPropertyString(cacheType, "WallBlockResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/props/modern-office-16", StaticPropertyString(cacheType, "PropDeskResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/walls/office-walls-floors-16", StaticPropertyString(cacheType, "PropCabinetResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/floors/room-builder-office-16", StaticPropertyString(cacheType, "PropEvidenceBoxResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/ME_Singles_Office_48x48_Building_Sign_1", StaticPropertyString(cacheType, "LandmarkOfficeSign1ResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/ME_Singles_Office_48x48_Building_Sign_2", StaticPropertyString(cacheType, "LandmarkOfficeSign2ResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/22_Post_Office_48x48_Blue_Mailbox_1_Front", StaticPropertyString(cacheType, "LandmarkMailboxResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/22_Post_Office_48x48_Truck_Front", StaticPropertyString(cacheType, "LandmarkTruckFrontResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Umbrella_Right_48x48", StaticPropertyString(cacheType, "LandmarkUmbrellaResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Tiny_Table_With_Drinks_1_48x48", StaticPropertyString(cacheType, "LandmarkTinyTableResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/22_Post_Office_48x48_Big_Single_Package_1", StaticPropertyString(cacheType, "LandmarkPackageResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/ME_Singles_Office_48x48_Air_Duct_3_Roof_Prop", StaticPropertyString(cacheType, "LandmarkAirDuctResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Door_Open_48x48", StaticPropertyString(cacheType, "LandmarkDoorOpenResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Potted_Plant_48x48", StaticPropertyString(cacheType, "LandmarkPottedPlantResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/24_Additional_Houses_Post_Apocalyptic_House_Generator_2_48x48", StaticPropertyString(cacheType, "RoomPropGeneratorResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_Monitor", StaticPropertyString(cacheType, "RoomPropMonitorResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_Control_Big_Monitor", StaticPropertyString(cacheType, "RoomPropControlBigMonitorResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Worksite_48x48_Tool_Box_1", StaticPropertyString(cacheType, "RoomPropToolBoxResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Worksite_48x48_Light_Tower_1", StaticPropertyString(cacheType, "RoomPropLightTowerResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_SOS_Box", StaticPropertyString(cacheType, "RoomPropSosBoxResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Camping_48x48_Chair_1", StaticPropertyString(cacheType, "RoomPropChairResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Camping_48x48_Benched_Table_2", StaticPropertyString(cacheType, "RoomPropBenchedTableResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_City_Props_48x48_Black_Closed_Trash_Can", StaticPropertyString(cacheType, "RoomPropTrashCanResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_City_Props_48x48_Trash_Pile_1", StaticPropertyString(cacheType, "RoomPropTrashPileResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_170_Whiteboard", StaticPropertyString(cacheType, "OfficeRoomPropWhiteboardResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_172_ChartBoard", StaticPropertyString(cacheType, "OfficeRoomPropChartBoardResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_176_ServerRack", StaticPropertyString(cacheType, "OfficeRoomPropServerRackResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_178_Printer", StaticPropertyString(cacheType, "OfficeRoomPropPrinterResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_203_CornerDesk", StaticPropertyString(cacheType, "OfficeRoomPropCornerDeskResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_227_DualMonitorDesk", StaticPropertyString(cacheType, "OfficeRoomPropDualMonitorDeskResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_276_CctvCameraRig", StaticPropertyString(cacheType, "OfficeRoomPropCctvCameraRigResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_320_MedicalCart", StaticPropertyString(cacheType, "OfficeRoomPropMedicalCartResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalResonanceMachine", StaticPropertyString(cacheType, "InteriorRoomPropHospitalResonanceMachineResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalScreenColor", StaticPropertyString(cacheType, "InteriorRoomPropHospitalScreenColorResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalSink", StaticPropertyString(cacheType, "InteriorRoomPropHospitalSinkResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalXrayMachine", StaticPropertyString(cacheType, "InteriorRoomPropHospitalXrayMachineResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalXrayScreen", StaticPropertyString(cacheType, "InteriorRoomPropHospitalXrayScreenResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_MorgueFreezerCorpseDoor", StaticPropertyString(cacheType, "InteriorRoomPropMorgueFreezerCorpseDoorResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_SecurityCameraWallRight", StaticPropertyString(cacheType, "InteriorRoomPropSecurityCameraWallRightResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_SafeGold", StaticPropertyString(cacheType, "InteriorRoomPropSafeGoldResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_SafeBucks", StaticPropertyString(cacheType, "InteriorRoomPropSafeBucksResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_GroceryCheckoutRoller", StaticPropertyString(cacheType, "InteriorRoomPropGroceryCheckoutRollerResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_GroceryGlassFridge", StaticPropertyString(cacheType, "InteriorRoomPropGroceryGlassFridgeResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_ButcherCarcass", StaticPropertyString(cacheType, "InteriorRoomPropButcherCarcassResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_KitchenBbq", StaticPropertyString(cacheType, "InteriorRoomPropKitchenBbqResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_KitchenOven4Cookers", StaticPropertyString(cacheType, "InteriorRoomPropKitchenOven4CookersResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_KitchenSink", StaticPropertyString(cacheType, "InteriorRoomPropKitchenSinkResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_CanteenCakeFridge", StaticPropertyString(cacheType, "InteriorRoomPropCanteenCakeFridgeResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_ShoppingCartBlueFull", StaticPropertyString(cacheType, "InteriorRoomPropShoppingCartBlueFullResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_OldTv", StaticPropertyString(cacheType, "InteriorRoomPropOldTvResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_JailLockerFull", StaticPropertyString(cacheType, "InteriorRoomPropJailLockerFullResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_MuseumLaserHorizontal", StaticPropertyString(cacheType, "InteriorRoomPropMuseumLaserHorizontalResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_Trapdoor", StaticPropertyString(cacheType, "InteriorRoomPropTrapdoorResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_TicketMachine", StaticPropertyString(cacheType, "InteriorRoomPropTicketMachineResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalTvReportage", StaticPropertyString(cacheType, "InteriorRoomPropHospitalTvReportageResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_FishCuttingSink", StaticPropertyString(cacheType, "InteriorRoomPropFishCuttingSinkResourcePath"));
        }

        [Test]
        public void AudioManager_HasCuratedKenneyRuntimeSfxForEveryGameplayCue()
        {
            Type soundEffectType = RuntimeType("GanglandUndercover.Audio.SoundEffect");

            foreach (string effectName in Enum.GetNames(soundEffectType))
            {
                if (effectName == "Ambient")
                {
                    continue;
                }

                string resourcePath = "Audio/SFX/Kenney/SFX_" + effectName;
                AudioClip clip = Resources.Load<AudioClip>(resourcePath);

                Assert.IsNotNull(clip, "Missing curated Kenney SFX at " + resourcePath);
                Assert.Greater(clip.length, 0f, resourcePath);
            }
        }

        [Test]
        public void OnlineMatchController_MapsGameplayCuesToKenneySfx()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            Type soundEffectType = RuntimeType("GanglandUndercover.Audio.SoundEffect");

            AssertCueMapsTo(controllerType, soundEffectType, "task", "TaskComplete");
            AssertCueMapsTo(controllerType, soundEffectType, "kill", "Kill");
            AssertCueMapsTo(controllerType, soundEffectType, "meeting", "MeetingStart");
            AssertCueMapsTo(controllerType, soundEffectType, "vote", "VoteCast");
            AssertCueMapsTo(controllerType, soundEffectType, "eliminated", "PlayerEliminated");
            AssertCueMapsTo(controllerType, soundEffectType, "blackout", "Emergency");
            AssertCueMapsTo(controllerType, soundEffectType, "vent", "VentOpen");
        }

        [Test]
        public void OnlineMatchHud_AttachesHoverSfxToRuntimeButtons()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("OnlineHudButtonSfxRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);

                Assert.IsTrue((bool)Invoke(controller, "EditorForceActionPreviewForSmokeTest"));
                Assert.GreaterOrEqual(PropertyInt(controller, "HudButtonSfxFeedbackCount"), 18);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnlineMatchHud_ExposesCanvasChatSafetyActions()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("OnlineHudChatSafetyRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);

                Assert.IsTrue((bool)Invoke(controller, "EditorForceActionPreviewForSmokeTest"));
                Assert.GreaterOrEqual(PropertyInt(controller, "ChatSafetyCanvasActionCount"), 2);
                Assert.GreaterOrEqual(PropertyInt(controller, "ChatPanelCanvasElementCount"), 3);
                Assert.IsTrue((bool)Invoke(controller, "EditorSeedChatSafetyMessageForSmokeTest"));

                Invoke(controller, "RequestReportLatestChatMessage");
                StringAssert.Contains("举报 1", PropertyString(controller, "VoiceHudLine"));

                Invoke(controller, "RequestBlockLatestChatSender");
                StringAssert.Contains("屏蔽 1", PropertyString(controller, "VoiceHudLine"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnlineMatchHud_KeepsCanvasChatPanelInActionPreview()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("OnlineHudChatPanelRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);

                Assert.IsTrue((bool)Invoke(controller, "EditorForceActionPreviewForSmokeTest"));
                Assert.IsTrue(PropertyBool(controller, "CanvasHudLayoutComplete"));
                Assert.GreaterOrEqual(PropertyInt(controller, "ChatPanelCanvasElementCount"), 3);
                StringAssert.Contains("近距离聊天", PropertyString(controller, "ChatChannelDisplayName"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnlineMatchController_RequestSendChatMessageFeedsCanvasChat()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("OnlineHudChatSendRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);

                Assert.IsTrue((bool)Invoke(controller, "EditorForceActionPreviewForSmokeTest"));
                Assert.IsTrue((bool)Invoke(controller, "RequestSendChatMessage", " <b>码头集合</b> "));
                StringAssert.Contains("码头集合", PropertyString(controller, "ChatFeedText"));
                StringAssert.Contains("近距离聊天", PropertyString(controller, "ChatInputStatusLine"));
                Assert.AreEqual(1, PropertyInt(controller, "ChatMessageCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnlineMatchController_RequestSendChatMessageRejectsBlankInput()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("OnlineHudChatBlankRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);

                Assert.IsTrue((bool)Invoke(controller, "EditorForceActionPreviewForSmokeTest"));
                Assert.IsFalse((bool)Invoke(controller, "RequestSendChatMessage", "   "));
                StringAssert.Contains("内容为空", PropertyString(controller, "Status"));
                Assert.AreEqual(0, PropertyInt(controller, "ChatMessageCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnlineMatchController_DownedStateCreatesKillSceneVfx()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("KillSceneVfxRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);

                Assert.IsTrue((bool)Invoke(controller, "EditorForceDownedStateForSmokeTest"));
                Assert.GreaterOrEqual(PropertyInt(controller, "KillSceneVfxCount"), 4);
                Assert.GreaterOrEqual(PropertyInt(controller, "StageTwoForensicSceneCount"), 4);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MeetingOverlay_Uses2DAssetSkinnedVisualSlice()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("MeetingOverlayVisualRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);
                Invoke(controller, "EditorForceMeetingForSmokeTest");

                Assert.IsTrue(PropertyBool(controller, "CanvasHudLayoutComplete"));
                Assert.GreaterOrEqual(PropertyInt(controller, "MeetingSeatCanvasElementCount"), PropertyInt(controller, "PlayerCount") + 4);
                Assert.GreaterOrEqual(PropertyInt(controller, "MeetingOverlayVisualElementCount"), 12);
                Assert.GreaterOrEqual(PropertyInt(controller, "MeetingOverlay2DAssetElementCount"), 10);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TaskOverlay_Uses2DAssetSkinnedVisualSlice()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("TaskOverlayVisualRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);
                Invoke(controller, "EditorOpenTaskPanelForSmokeTest", 0);

                Assert.IsTrue(PropertyBool(controller, "CanvasHudLayoutComplete"));
                Assert.GreaterOrEqual(PropertyInt(controller, "TaskMiniGameCanvasElementCount"), 14);
                Assert.GreaterOrEqual(PropertyInt(controller, "TaskOverlayVisualElementCount"), 12);
                Assert.GreaterOrEqual(PropertyInt(controller, "TaskOverlay2DAssetElementCount"), 12);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static object CreateRuleSet()
        {
            return ScriptableObject.CreateInstance(RuntimeType("GanglandUndercover.Online.OnlineRuleSet"));
        }

        private static object CreateVictoryBridge()
        {
            return Activator.CreateInstance(RuntimeType("GanglandUndercover.Online.OnlineVictoryBridge"));
        }

        private static object GetRoleDistribution(int playerCount)
        {
            return Invoke(CreateRuleSet(), "GetRoleDistribution", playerCount);
        }

        private static bool TryTimeLimit(object bridge, float elapsed, float limit, int evidence, int target, Array tasks, out string result)
        {
            object[] args = { elapsed, limit, evidence, target, tasks, string.Empty };
            bool hasResult = (bool)Invoke(bridge, "TryTimeLimitEvaluation", args);
            result = (string)args[5];
            return hasResult;
        }

        private static void AssertChannel(string phaseName, bool alive, string expected)
        {
            object phase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), phaseName);
            object channel = InvokeStatic(RuntimeType("GanglandUndercover.Online.ChatSystem"), "DetermineChannel", phase, alive);
            Assert.AreEqual(expected, channel.ToString());
        }

        private static string Sanitize(string input)
        {
            return (string)InvokeStatic(RuntimeType("GanglandUndercover.Online.ChatSystem"), "Sanitize", input);
        }

        private static object CreateChatSystem()
        {
            Type chatType = RuntimeType("GanglandUndercover.Online.ChatSystem");
            return Activator.CreateInstance(chatType, new Action<string>(_ => { }));
        }

        private static object CreateMeetingSync(List<string> caseLog)
        {
            Type meetingType = RuntimeType("GanglandUndercover.Online.MeetingSync");
            Action<string> addCaseLog = message => caseLog.Add(message);
            return Activator.CreateInstance(meetingType, addCaseLog);
        }

        private static void ReceiveChatMessage(
            object chat,
            string senderId,
            string senderName,
            string content,
            string channelName = "Meeting")
        {
            object faction = Enum.Parse(RuntimeType("GanglandUndercover.Core.Faction"), "Police");
            object channel = Enum.Parse(RuntimeType("GanglandUndercover.Online.ChatChannel"), channelName);
            Invoke(chat, "ReceiveMessage", senderId, senderName, content, false, faction, channel);
        }

        private static string BuildMainMenuLoginStatus(object service)
        {
            Type menuType = RuntimeType("GanglandUndercover.UI.MainMenuController");
            return (string)StaticNonPublic(menuType, "BuildLoginStatusLine").Invoke(null, new[] { service });
        }

        private static string BuildMainMenuSettingsStatus(object settings)
        {
            Type menuType = RuntimeType("GanglandUndercover.UI.MainMenuController");
            return (string)StaticNonPublic(menuType, "BuildSettingsStatusLine").Invoke(null, new[] { settings });
        }

        private static string BuildRelayLobbySummary(
            string relayStatus,
            string relayJoinCode,
            string relayJoinInput,
            bool operationInProgress,
            bool isOnline,
            bool isHost,
            bool isClientConnected,
            int connectedClientCount)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return (string)StaticNonPublic(controllerType, "BuildRelayLobbySummary").Invoke(null, new object[]
            {
                relayStatus,
                relayJoinCode,
                relayJoinInput,
                operationInProgress,
                isOnline,
                isHost,
                isClientConnected,
                connectedClientCount
            });
        }

        private static string BuildLobbyBrowserSummary(
            string status,
            bool refreshInProgress,
            int visibleRoomCount,
            int selectedIndex)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return (string)StaticNonPublic(controllerType, "BuildLobbyBrowserSummary").Invoke(null, new object[]
            {
                status,
                refreshInProgress,
                visibleRoomCount,
                selectedIndex
            });
        }

        private static string BuildLobbyRoomLine(
            int displayIndex,
            string roomName,
            int playerCount,
            int maxPlayers,
            bool isLocked,
            bool hasPassword,
            string mapName,
            string ruleSummary,
            string relayCode)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return (string)StaticNonPublic(controllerType, "BuildLobbyRoomLine").Invoke(null, new object[]
            {
                displayIndex,
                roomName,
                playerCount,
                maxPlayers,
                isLocked,
                hasPassword,
                mapName,
                ruleSummary,
                relayCode
            });
        }

        private static IDictionary BuildRelayLobbySessionProperties(
            string relayCode,
            string mapName,
            string ruleSummary)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return (IDictionary)StaticNonPublic(controllerType, "BuildRelayLobbySessionProperties").Invoke(null, new object[]
            {
                relayCode,
                mapName,
                ruleSummary
            });
        }

        private static object BuildRelayLobbySessionOptions(
            string roomName,
            int maxPlayers,
            string relayCode,
            string mapName,
            string ruleSummary)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return StaticNonPublic(controllerType, "BuildRelayLobbySessionOptions").Invoke(null, new object[]
            {
                roomName,
                maxPlayers,
                relayCode,
                mapName,
                ruleSummary
            });
        }

        private static object BuildLobbyQueryOptions()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return StaticNonPublic(controllerType, "BuildLobbyQueryOptions").Invoke(null, Array.Empty<object>());
        }

        private static string BuildLobbyPublishStatus(
            bool publishInProgress,
            bool published,
            string sessionCode)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return (string)StaticNonPublic(controllerType, "BuildLobbyPublishStatus").Invoke(null, new object[]
            {
                publishInProgress,
                published,
                sessionCode
            });
        }

        private static object BuildLobbyRoomSessionJoin(
            string sessionId,
            string relayCode,
            int playerCount,
            int maxPlayers,
            bool isLocked,
            bool hasPassword,
            bool allowLocalPreview)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return StaticNonPublic(controllerType, "BuildLobbyRoomSessionJoin").Invoke(null, new object[]
            {
                sessionId,
                relayCode,
                playerCount,
                maxPlayers,
                isLocked,
                hasPassword,
                allowLocalPreview
            });
        }

        private static object EvaluateVictory(int evidenceScore, int evidenceTarget, object players, Array tasks, bool matchStarted, string phaseName)
        {
            object bridge = CreateVictoryBridge();
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            object phase = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), phaseName);

            return Invoke(bridge, "Evaluate",
                evidenceScore,
                evidenceTarget,
                players,
                MakeRoleResolver(),
                tasks,
                matchStarted,
                phase,
                Enum.Parse(roleType, "Police"));
        }

        private static object MakePlayers(params (int Count, string RoleName)[] groups)
        {
            RoleLookup.Clear();

            Type stateType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            Type professionType = RuntimeType("GanglandUndercover.Online.OnlineProfession");
            Type dictType = typeof(Dictionary<,>).MakeGenericType(typeof(ulong), stateType);
            object dict = Activator.CreateInstance(dictType);
            MethodInfo add = dictType.GetMethod("Add");
            ConstructorInfo stateCtor = stateType.GetConstructor(new[]
            {
                typeof(ulong), typeof(string), typeof(Vector3), typeof(bool), typeof(bool),
                roleType, professionType, typeof(int), typeof(bool)
            });

            ulong id = 0;
            foreach ((int count, string roleName) in groups)
            {
                for (int i = 0; i < count; i++)
                {
                    id++;
                    object role = Enum.Parse(roleType, roleName);
                    object profession = Enum.Parse(professionType, ProfessionFor(roleName));
                    object state = stateCtor.Invoke(new object[]
                    {
                        id, roleName + i, Vector3.zero, true, true, role, profession, 0, false
                    });

                    add.Invoke(dict, new object[] { id, state });
                    RoleLookup[id] = Convert.ToInt32(role);
                }
            }

            return dict;
        }

        private static string ProfessionFor(string roleName)
        {
            switch (roleName)
            {
                case "Gang": return "Enforcer";
                case "Undercover": return "UndercoverAgent";
                case "Mole": return "Enforcer";
                default: return "Inspector";
            }
        }

        private static Delegate MakeRoleResolver()
        {
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            Type delegateType = typeof(Func<,>).MakeGenericType(typeof(ulong), roleType);
            ParameterExpression id = Expression.Parameter(typeof(ulong), "id");
            MethodInfo resolver = typeof(CoreSystemTests).GetMethod(nameof(ResolveRoleValue), BindingFlags.Static | BindingFlags.NonPublic);
            UnaryExpression body = Expression.Convert(Expression.Call(resolver, id), roleType);
            return Expression.Lambda(delegateType, body, id).Compile();
        }

        private static int ResolveRoleValue(ulong clientId)
        {
            return RoleLookup.TryGetValue(clientId, out int role) ? role : 1;
        }

        private static Array MakeTasks(params (bool Completed, bool Sabotaged)[] states)
        {
            Type taskType = RuntimeType("GanglandUndercover.Online.OnlineTaskState");
            Array array = Array.CreateInstance(taskType, states.Length);
            ConstructorInfo ctor = taskType.GetConstructor(new[]
            {
                typeof(int), typeof(string), typeof(Vector3), typeof(int), typeof(int), typeof(bool), typeof(bool)
            });

            for (int i = 0; i < states.Length; i++)
            {
                object task = ctor.Invoke(new object[]
                {
                    i, "Task" + i, Vector3.zero, states[i].Completed ? 1 : 0, 1, states[i].Completed, states[i].Sabotaged
                });
                array.SetValue(task, i);
            }

            return array;
        }

        private static void AssertResult(object result, bool hasResult, string expectedText)
        {
            Assert.AreEqual(hasResult, PropertyBool(result, "HasResult"));
            StringAssert.Contains(expectedText, PropertyString(result, "ResultText"));
        }

        private static int FieldInt(object target, string fieldName)
        {
            return Convert.ToInt32(target.GetType().GetField(fieldName).GetValue(target));
        }

        private static bool FieldBool(object target, string fieldName)
        {
            return (bool)target.GetType().GetField(fieldName).GetValue(target);
        }

        private static string FieldString(object target, string fieldName)
        {
            return (string)target.GetType().GetField(fieldName).GetValue(target);
        }

        private static string FieldValueText(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName).GetValue(target).ToString();
        }

        private static object Property(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static int PropertyInt(object target, string propertyName)
        {
            return Convert.ToInt32(target.GetType().GetProperty(propertyName).GetValue(target));
        }

        private static bool PropertyBool(object target, string propertyName)
        {
            return (bool)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static string PropertyString(object target, string propertyName)
        {
            return (string)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static string PropertyValueText(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName).GetValue(target).ToString();
        }

        private static object First(IEnumerable values)
        {
            foreach (object value in values)
            {
                return value;
            }

            Assert.Fail("Expected at least one value.");
            return null;
        }

        private static string StaticPropertyString(Type type, string propertyName)
        {
            return (string)type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static int InvokeInt(object target, string methodName, params object[] args)
        {
            return Convert.ToInt32(Invoke(target, methodName, args));
        }

        private static float InvokeFloat(object target, string methodName, params object[] args)
        {
            return Convert.ToSingle(Invoke(target, methodName, args));
        }

        private static int CountChildrenStartingWith(Transform root, string prefix)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static GameObject FindObjectNamedIncludingInactive(string name)
        {
            foreach (GameObject obj in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (obj == null)
                {
                    continue;
                }

                if (obj.name == name)
                {
                    return obj;
                }
            }

            return null;
        }

        private static void DestroyAllObjectsNamed(string name)
        {
            List<GameObject> targets = new List<GameObject>();

            foreach (GameObject obj in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (obj == null)
                {
                    continue;
                }

                if (obj.name == name)
                {
                    targets.Add(obj);
                }
            }

            foreach (GameObject target in targets)
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static int CountChildrenContaining(Transform root, string text)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name.IndexOf(text, StringComparison.Ordinal) >= 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountActiveChildrenStartingWith(Transform root, string prefix)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root
                    && child.gameObject.activeInHierarchy
                    && child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountLegacyRandomNeonSpotNames(Transform root)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root || !child.name.StartsWith("霓虹", StringComparison.Ordinal))
                {
                    continue;
                }

                if (child.name.Length > 2 && char.IsDigit(child.name[2]))
                {
                    count++;
                }
            }

            return count;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType().GetMethod(methodName).Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }

        private static MethodInfo StaticNonPublic(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static void AssertCueMapsTo(Type controllerType, Type soundEffectType, string cueName, string expectedEffect)
        {
            object effect = Enum.Parse(soundEffectType, "UIClick");
            object[] args = { cueName, effect };

            bool mapped = (bool)StaticNonPublic(controllerType, "TryResolveSoundEffectCue").Invoke(null, args);

            Assert.IsTrue(mapped, "Expected " + cueName + " to map to a Kenney SoundEffect");
            Assert.AreEqual(expectedEffect, args[1].ToString());
        }

        private static Type RuntimeType(string fullName)
        {
            return Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);
        }

        private sealed class ControllerFixture : IDisposable
        {
            private readonly Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            private readonly Type playerStateType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            private readonly Type taskStateType = RuntimeType("GanglandUndercover.Online.OnlineTaskState");
            private readonly Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            private readonly Type professionType = RuntimeType("GanglandUndercover.Online.OnlineProfession");
            private readonly Type phaseType = RuntimeType("GanglandUndercover.Online.OnlineMatchPhase");
            private readonly Type mapType = RuntimeType("GanglandUndercover.Online.OnlineMapService+OnlineMapType");

            private readonly GameObject host;
            private readonly object controller;

            public ControllerFixture()
            {
                host = new GameObject("SecurityRegression_OnlineMatchController");
                controller = host.AddComponent(controllerType);
                ClearCollection("players");
                ClearCollection("privateRoles");
                ClearCollection("tasks");
                SetField("matchStarted", true);
                SetField("localPreviewMode", false);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            public void SetPhase(string phaseName)
            {
                SetField("phase", Enum.Parse(phaseType, phaseName));
            }

            public int PhaseValue(string phaseName)
            {
                return Convert.ToInt32(Enum.Parse(phaseType, phaseName));
            }

            public string PhaseName()
            {
                return GetField("phase").ToString();
            }

            public void SetMatchStarted(bool started)
            {
                SetField("matchStarted", started);
            }

            public bool MatchStarted()
            {
                return (bool)GetField("matchStarted");
            }

            public void SetLocalPreviewMode(bool enabled)
            {
                SetField("localPreviewMode", enabled);
            }

            public void SetLocalRole(string roleName)
            {
                SetField("localRole", Enum.Parse(roleType, roleName));
            }

            public int RoleValue(string roleName)
            {
                return Convert.ToInt32(Enum.Parse(roleType, roleName));
            }

            public string LocalRoleName()
            {
                return GetField("localRole").ToString();
            }

            public void SetPlayer(ulong clientId, Vector3 position, bool alive, string roleName = "Police", string professionName = null)
            {
                object role = Enum.Parse(roleType, roleName);
                object profession = Enum.Parse(professionType, professionName ?? ProfessionFor(roleName));
                object state = Activator.CreateInstance(
                    playerStateType,
                    clientId,
                    "玩家" + clientId,
                    position,
                    true,
                    alive,
                    role,
                    profession,
                    0,
                    false);

                object players = GetField("players");
                players.GetType().GetMethod("Add").Invoke(players, new[] { (object)clientId, state });

                object privateRoles = GetField("privateRoles");
                privateRoles.GetType().GetMethod("Add").Invoke(privateRoles, new[] { (object)clientId, role });
            }

            public void SetPlayerReady(ulong clientId, bool ready)
            {
                object state = GetPlayerState(clientId);
                playerStateType.GetField("Ready").SetValue(state, ready);
                SetPlayerState(clientId, state);
            }

            public bool HasPlayer(ulong clientId)
            {
                object players = GetField("players");
                return (bool)players.GetType().GetMethod("ContainsKey").Invoke(players, new object[] { clientId });
            }

            public Vector3 PlayerPosition(ulong clientId)
            {
                return (Vector3)playerStateType.GetField("Position").GetValue(GetPlayerState(clientId));
            }

            public Vector2 PlayerInput(ulong clientId)
            {
                return (Vector2)playerStateType.GetField("Input").GetValue(GetPlayerState(clientId));
            }

            public bool PlayerReady(ulong clientId)
            {
                return (bool)playerStateType.GetField("Ready").GetValue(GetPlayerState(clientId));
            }

            public bool PlayerAlive(ulong clientId)
            {
                return (bool)playerStateType.GetField("Alive").GetValue(GetPlayerState(clientId));
            }

            public void AttachSyncManager()
            {
                Type syncType = RuntimeType("GanglandUndercover.Online.OnlineSyncManager");
                object sync = host.AddComponent(syncType);
                syncType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sync, null);
                SetField("syncManager", sync);
            }

            public void SubscribeMeetingEnded(Action handler)
            {
                object sync = GetField("syncManager");
                Assert.IsNotNull(sync, "AttachSyncManager must be called before subscribing to MeetingSync events.");
                object meetingSync = sync.GetType().GetProperty("MeetingSync").GetValue(sync);
                EventInfo ended = meetingSync.GetType().GetEvent("MeetingEnded");
                ended.AddEventHandler(meetingSync, handler);
            }

            public void BeginMeeting(string reason)
            {
                controllerType.GetMethod("BeginMeeting", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { reason });
            }

            public void ApplyVote(ulong voterClientId, ulong targetClientId)
            {
                controllerType.GetMethod("ApplyVote", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { voterClientId, targetClientId });
            }

            public void ApplySkipVote(ulong voterClientId)
            {
                ApplyVote(voterClientId, ulong.MaxValue);
            }

            public void SetEvidenceTarget(int value)
            {
                controllerType.GetMethod("SetEvidenceTarget").Invoke(controller, new object[] { value });
            }

            public void ConfigureRoom(int minPlayers, int maxPlayers, bool autoFillAi)
            {
                controllerType.GetMethod("SetRoomMinPlayers").Invoke(controller, new object[] { minPlayers });
                controllerType.GetMethod("SetRoomMaxPlayers").Invoke(controller, new object[] { maxPlayers });
                controllerType.GetMethod("SetAutoFillAi").Invoke(controller, new object[] { autoFillAi });
            }

            public void StartOnlineMatchCore()
            {
                controllerType.GetMethod("StartOnlineMatchCore", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { false });
            }

            public bool TryFindRoleWithKillCooldown(string roleName, out float cooldown)
            {
                object targetRole = Enum.Parse(roleType, roleName);
                object privateRoles = GetField("privateRoles");
                IEnumerable entries = (IEnumerable)privateRoles;

                foreach (object entry in entries)
                {
                    ulong clientId = (ulong)entry.GetType().GetProperty("Key").GetValue(entry);
                    object role = entry.GetType().GetProperty("Value").GetValue(entry);

                    if (!role.Equals(targetRole))
                    {
                        continue;
                    }

                    object state = GetPlayerState(clientId);
                    cooldown = Convert.ToSingle(playerStateType.GetField("KillCooldown").GetValue(state));
                    return true;
                }

                cooldown = 0f;
                return false;
            }

            public void ApplyClientState(ulong clientId, Vector3 position, Vector2 input, bool ready)
            {
                controllerType
                    .GetMethod("ApplyClientState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { clientId, position, input, ready });
            }

            public void ReceiveRoleAssign(ulong senderClientId, int roleValue)
            {
                FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);

                try
                {
                    writer.WriteValueSafe(roleValue);
                    InvokeReceive("ReceiveRoleAssign", senderClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }

            public void SetActiveMapType(string mapTypeName)
            {
                object service = EnsureMapService();
                service.GetType().GetProperty("ActiveMapType").SetValue(service, Enum.Parse(mapType, mapTypeName));
            }

            public int MapTypeValue(string mapTypeName)
            {
                return Convert.ToInt32(Enum.Parse(mapType, mapTypeName));
            }

            public string ActiveMapTypeName()
            {
                object service = EnsureMapService();
                return service.GetType().GetProperty("ActiveMapType").GetValue(service).ToString();
            }

            public void ReceiveMapSelect(ulong senderClientId, int mapTypeValue)
            {
                FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);

                try
                {
                    writer.WriteValueSafe(mapTypeValue);
                    InvokeReceive("ReceiveMapSelect", senderClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }

            public void ReceiveServerSnapshot(ulong senderClientId, bool matchStarted, string phaseName)
            {
                ReceiveServerSnapshotRaw(senderClientId, matchStarted, PhaseValue(phaseName));
            }

            public void ReceiveServerSnapshotRaw(
                ulong senderClientId,
                bool matchStarted,
                int phaseValue,
                int playerCount = 0,
                byte criticalTaskType = 0,
                int taskCount = 0,
                int bodyCount = 0,
                int voteCount = 0,
                int caseLogCount = 0)
            {
                EnsureSnapshotDependencies();
                FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp);

                try
                {
                    WriteEmptySnapshot(ref writer, matchStarted, phaseValue, playerCount, criticalTaskType, taskCount, bodyCount, voteCount, caseLogCount);
                    InvokeReceive("ReceiveServerSnapshot", senderClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }

            public string PropertyString(string propertyName)
            {
                return (string)controllerType.GetProperty(propertyName).GetValue(controller);
            }

            public bool PropertyBool(string propertyName)
            {
                return (bool)controllerType.GetProperty(propertyName).GetValue(controller);
            }

            public int PropertyInt(string propertyName)
            {
                return Convert.ToInt32(controllerType.GetProperty(propertyName).GetValue(controller));
            }

            public float FieldFloat(string fieldName)
            {
                return Convert.ToSingle(GetField(fieldName));
            }

            public void SetSingleTask(int taskId, Vector3 position, bool completed, bool sabotaged)
            {
                object task = Activator.CreateInstance(
                    taskStateType,
                    taskId,
                    "Task" + taskId,
                    position,
                    completed ? 1 : 0,
                    1,
                    completed,
                    sabotaged);

                object tasks = GetField("tasks");
                tasks.GetType().GetMethod("Clear").Invoke(tasks, null);
                tasks.GetType().GetMethod("Add").Invoke(tasks, new[] { task });
            }

            public void MarkTaskActive(ulong clientId, int taskId)
            {
                controllerType.GetMethod("MarkTaskActive").Invoke(controller, new object[] { clientId, taskId });
            }

            public bool InvokeBoolOutString(string methodName, ulong clientId, int taskId, out string message)
            {
                object[] args = { clientId, taskId, string.Empty };
                bool accepted = (bool)controllerType.GetMethod(methodName).Invoke(controller, args);
                message = (string)args[2];
                return accepted;
            }

            private object GetField(string fieldName)
            {
                return controllerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetValue(controller);
            }

            private object EnsureMapService()
            {
                object service = GetField("mapService");

                if (service == null)
                {
                    InvokeNonPublic("EnsureCoreServices");
                    service = GetField("mapService");
                }

                return service;
            }

            private void EnsureSnapshotDependencies()
            {
                InvokeNonPublic("EnsureCoreServices");
                InvokeNonPublic("EnsureRuntimeDependencies");
            }

            private void InvokeReceive(string methodName, ulong senderClientId, FastBufferWriter writer)
            {
                FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);

                try
                {
                    controllerType
                        .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(controller, new object[] { senderClientId, reader });
                }
                finally
                {
                    reader.Dispose();
                }
            }

            private void InvokeNonPublic(string methodName)
            {
                controllerType
                    .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
            }

            private static void WriteEmptySnapshot(
                ref FastBufferWriter writer,
                bool matchStarted,
                int phaseValue,
                int playerCount,
                byte criticalTaskType,
                int taskCount,
                int bodyCount,
                int voteCount,
                int caseLogCount)
            {
                writer.WriteValueSafe(matchStarted);
                writer.WriteValueSafe(phaseValue);
                writer.WriteValueSafe(0);
                writer.WriteValueSafe(10);
                writer.WriteValueSafe(1);
                writer.WriteValueSafe(1);
                writer.WriteValueSafe(10);
                writer.WriteValueSafe(false);
                writer.WriteValueSafe(false);
                writer.WriteValueSafe(false);
                writer.WriteValueSafe("Room");
                writer.WriteValueSafe("Result");
                writer.WriteValueSafe("Meeting");
                writer.WriteValueSafe("Vote");
                writer.WriteValueSafe("Evidence");
                writer.WriteValueSafe("Sabotage");
                writer.WriteValueSafe(0);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(false);
                writer.WriteValueSafe(criticalTaskType);
                writer.WriteValueSafe(0f);
                writer.WriteValueSafe(playerCount);
                writer.WriteValueSafe(taskCount);
                writer.WriteValueSafe(bodyCount);
                writer.WriteValueSafe(voteCount);
                writer.WriteValueSafe(caseLogCount);
            }

            private object GetPlayerState(ulong clientId)
            {
                object players = GetField("players");
                return players.GetType().GetProperty("Item").GetValue(players, new object[] { clientId });
            }

            private void SetPlayerState(ulong clientId, object state)
            {
                object players = GetField("players");
                players.GetType().GetProperty("Item").SetValue(players, state, new object[] { clientId });
            }

            private void SetField(string fieldName, object value)
            {
                controllerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .SetValue(controller, value);
            }

            private void ClearCollection(string fieldName)
            {
                object collection = GetField(fieldName);
                collection.GetType().GetMethod("Clear").Invoke(collection, null);
            }
        }
    }
}
