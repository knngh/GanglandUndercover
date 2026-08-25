using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

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

        /// <summary>
        /// 预热静态资源缓存并屏蔽初始化 Debug.Log，避免 Unity 测试框架
        /// 将 Sprite2DAssetCache / UIStyle / OnlineSyncManager 等系统的
        /// 运行时日志误判为测试失败。
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // 项目运行时系统会在 AddComponent → Reset() 链中产生 Debug.Log
            // （sprite 缓存加载、字体加载、HUD 构建等），这些是正常的初始化
            // 行为，不应导致测试失败。
            LogAssert.ignoreFailingMessages = true;

            var asm = Assembly.Load(RuntimeAssemblyName);
            var cacheType = asm.GetType("GanglandUndercover.Art.Sprite2DAssetCache");
            if (cacheType != null)
            {
                var ensure = cacheType.GetMethod("Ensure",
                    BindingFlags.Public | BindingFlags.Static);
                ensure?.Invoke(null, null);
            }
        }

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
        public void RoleDistribution_4PlayerPreview_StillIncludesUndercover()
        {
            object dist = GetRoleDistribution(4);

            Assert.AreEqual(1, FieldInt(dist, "gang"));
            Assert.AreEqual(1, FieldInt(dist, "undercover"));
            Assert.AreEqual(0, FieldInt(dist, "mole"));
            Assert.AreEqual(2, PropertyInt(dist, "PoliceCount"));
        }

        [Test]
        public void RoleDistribution_OutOfRange_UsesNearestPreset()
        {
            object low = GetRoleDistribution(3);
            object high = GetRoleDistribution(12);

            Assert.AreEqual(1, FieldInt(low, "gang"));
            Assert.AreEqual(1, FieldInt(low, "undercover"));
            Assert.AreEqual(3, FieldInt(high, "gang"));
            Assert.AreEqual(2, FieldInt(high, "undercover"));
        }

        [Test]
        public void RuleSet_DefaultsKeepIdentityHiddenAndUseDesignedKillRange()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(1.1f, FieldFloat(ruleSet, "KillRange"), 0.001f);
            Assert.IsFalse((bool)ruleSet.GetType().GetField("RevealRoleOnEject").GetValue(ruleSet),
                "双面身份博弈要求身份只在结算时统一公开。");
        }

        [Test]
        public void RolePermissions_MatchDoubleAgentFactionMatrix()
        {
            Type utilsType = RuntimeType("GanglandUndercover.Online.OnlineMatchUtils");
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");

            object gang = Enum.Parse(roleType, "Gang");
            object undercover = Enum.Parse(roleType, "Undercover");
            object mole = Enum.Parse(roleType, "Mole");
            object police = Enum.Parse(roleType, "Police");

            Assert.IsTrue((bool)InvokeStatic(utilsType, "CanSabotage", gang));
            Assert.IsTrue((bool)InvokeStatic(utilsType, "CanSabotage", undercover));
            Assert.IsTrue((bool)InvokeStatic(utilsType, "CanSabotage", mole));
            Assert.IsFalse((bool)InvokeStatic(utilsType, "CanSabotage", police));

            Assert.IsTrue((bool)InvokeStatic(utilsType, "CanUseUnderworldPassage", gang));
            Assert.IsTrue((bool)InvokeStatic(utilsType, "CanUseUnderworldPassage", undercover));
            Assert.IsFalse((bool)InvokeStatic(utilsType, "CanUseUnderworldPassage", mole));
            Assert.IsFalse((bool)InvokeStatic(utilsType, "CanUseUnderworldPassage", police));
        }

        [Test]
        public void PublicPresentation_MapsDoubleAgentsToCoverProfessions()
        {
            Type utilsType = RuntimeType("GanglandUndercover.Online.OnlineMatchUtils");
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");

            object gangCover = InvokeStatic(utilsType, "PublicProfessionFor", Enum.Parse(roleType, "Gang"));
            object policeCover = InvokeStatic(utilsType, "PublicProfessionFor", Enum.Parse(roleType, "Police"));

            Assert.AreEqual("Enforcer", gangCover.ToString());
            Assert.AreEqual("Inspector", policeCover.ToString());
        }

        [Test]
        public void ChatPresentation_UsesCoverUntilDeathOrResult()
        {
            Type utilsType = RuntimeType("GanglandUndercover.Online.OnlineMatchUtils");
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            Type phaseType = RuntimeType("GanglandUndercover.Online.OnlineMatchPhase");
            object undercover = Enum.Parse(roleType, "Undercover");
            object gangCover = Enum.Parse(roleType, "Gang");
            object action = Enum.Parse(phaseType, "Action");
            object result = Enum.Parse(phaseType, "Result");

            Assert.AreEqual("Gang", InvokeStatic(utilsType, "ChatPresentationRole", undercover, gangCover, true, action).ToString());
            Assert.AreEqual("Undercover", InvokeStatic(utilsType, "ChatPresentationRole", undercover, gangCover, false, action).ToString());
            Assert.AreEqual("Undercover", InvokeStatic(utilsType, "ChatPresentationRole", undercover, gangCover, true, result).ToString());
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
        public void AlphaPacing_ProvidesPlayableSixEightTenPlayerEnvelope()
        {
            object ruleSet = CreateRuleSet();

            Assert.AreEqual(600f, FieldFloat(ruleSet, "MatchTargetMinSeconds"), 0.001f,
                "Alpha 节奏目标下限必须保持 10 分钟。");
            Assert.AreEqual(1200f, FieldFloat(ruleSet, "MatchHardLimitSeconds"), 0.001f,
                "Alpha 节奏硬上限必须保持 20 分钟。");

            AssertAlphaPacingEnvelope(ruleSet, 6, expectedGang: 1, expectedUndercover: 1, expectedMole: 0,
                expectedTasks: 20, expectedEvidenceTarget: 34, expectedKillCooldown: 25f,
                expectedMeetingWindow: 75f, expectedEmergencyCooldown: 75f);
            AssertAlphaPacingEnvelope(ruleSet, 8, expectedGang: 2, expectedUndercover: 1, expectedMole: 1,
                expectedTasks: 24, expectedEvidenceTarget: 44, expectedKillCooldown: 22f,
                expectedMeetingWindow: 95f, expectedEmergencyCooldown: 90f);
            AssertAlphaPacingEnvelope(ruleSet, 10, expectedGang: 3, expectedUndercover: 2, expectedMole: 1,
                expectedTasks: 28, expectedEvidenceTarget: 55, expectedKillCooldown: 22f,
                expectedMeetingWindow: 95f, expectedEmergencyCooldown: 90f);
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
        public void StartMatch_AssignsTasksByPublicCoverIdentity()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.ConfigureRoom(minPlayers: 8, maxPlayers: 8, autoFillAi: false);
                fixture.AttachSyncManager();
                for (ulong clientId = 1UL; clientId <= 8UL; clientId++)
                {
                    fixture.SetPlayer(clientId, Vector3.zero, alive: true);
                }

                fixture.StartOnlineMatchCore();

                ulong undercoverId = fixture.FindClientIdByPrivateRole("Undercover");
                ulong moleId = fixture.FindClientIdByPrivateRole("Mole");
                int[] undercoverTasks = fixture.AssignedTaskIds(undercoverId);
                int[] moleTasks = fixture.AssignedTaskIds(moleId);
                CollectionAssert.IsNotEmpty(undercoverTasks);
                CollectionAssert.IsNotEmpty(moleTasks);
                Assert.AreEqual(4, undercoverTasks.Length, "默认规则应给卧底分配 4 个黑帮伪装任务。");
                Assert.AreEqual(4, moleTasks.Length, "默认规则应给内鬼分配 4 个警方伪装任务。");
                for (ulong clientId = 1UL; clientId <= 8UL; clientId++)
                {
                    Assert.AreEqual(4, fixture.AssignedTaskIds(clientId).Length,
                        "默认规则应给每位玩家分配恰好 4 个个人任务，玩家 " + clientId + " 数量不符。");
                }

                foreach (int taskId in undercoverTasks)
                {
                    Assert.IsTrue(fixture.IsSabotageTask(taskId),
                        "卧底公开为黑帮，必须领取黑帮伪装任务池。");
                }

                bool moleHasInvestigationTask = false;
                foreach (int taskId in moleTasks)
                {
                    if (!fixture.IsSabotageTask(taskId))
                    {
                        moleHasInvestigationTask = true;
                        break;
                    }
                }

                Assert.IsTrue(moleHasInvestigationTask,
                    "内鬼公开为警察，必须领取警察调查任务池以窃取情报。");
            }
        }

        [Test]
        public void StartMatch_ClearsDoubleAgentProgressFromPreviousRound()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.ConfigureRoom(minPlayers: 8, maxPlayers: 8, autoFillAi: false);
                for (ulong clientId = 1UL; clientId <= 8UL; clientId++)
                {
                    fixture.SetPlayer(clientId, Vector3.zero, alive: true);
                }

                fixture.StartOnlineMatchCore();
                ulong moleId = fixture.FindClientIdByPrivateRole("Mole");
                ulong undercoverId = fixture.FindClientIdByPrivateRole("Undercover");
                fixture.AccumulateMoleIntel(moleId, 5);
                fixture.AccumulateUndercoverIntel(undercoverId, 3);
                fixture.AssignMoleHit(moleId);
                Assert.Greater(fixture.GetMoleIntel(moleId), 0);
                Assert.Greater(fixture.GetUndercoverIntel(undercoverId), 0);

                fixture.StartOnlineMatchCore();

                Assert.AreEqual(0, fixture.GetMoleIntel(moleId));
                Assert.AreEqual(0, fixture.GetUndercoverIntel(undercoverId));
                Assert.AreEqual(0, fixture.CollectionCount("_moleObjectives"));
                Assert.IsFalse(fixture.HasMoleHitTarget(moleId));
            }
        }

        [Test]
        public void TaskStart_RejectsUnassignedTaskAndAcceptsAssignedTask()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.ConfigureRoom(minPlayers: 8, maxPlayers: 8, autoFillAi: false);
                fixture.AttachSyncManager();
                for (ulong clientId = 1UL; clientId <= 8UL; clientId++)
                {
                    fixture.SetPlayer(clientId, Vector3.zero, alive: true);
                }

                fixture.StartOnlineMatchCore();
                fixture.SetPhase("Action");

                ulong policeId = fixture.FindClientIdByPrivateRole("Police");
                int[] assignedTaskIds = fixture.AssignedTaskIds(policeId);
                CollectionAssert.IsNotEmpty(assignedTaskIds);

                var assignedSet = new HashSet<int>(assignedTaskIds);
                int unassignedTaskId = -1;
                for (int taskId = 0; taskId < 28; taskId++)
                {
                    if (!assignedSet.Contains(taskId))
                    {
                        unassignedTaskId = taskId;
                        break;
                    }
                }

                Assert.GreaterOrEqual(unassignedTaskId, 0, "测试需要至少一个未分配任务。");
                fixture.SetPlayerPosition(policeId, fixture.TaskPosition(unassignedTaskId));
                Assert.IsFalse(fixture.InvokeBoolOutString("ValidateTaskStart", policeId, unassignedTaskId, out string rejectedReason));
                StringAssert.Contains("未分配", rejectedReason);

                int assignedTaskId = assignedTaskIds[0];
                fixture.SetPlayerPosition(policeId, fixture.TaskPosition(assignedTaskId));
                Assert.IsTrue(fixture.InvokeBoolOutString("ValidateTaskStart", policeId, assignedTaskId, out string acceptedReason), acceptedReason);
            }
        }

        [Test]
        public void TaskList_ShowsLocalAssignmentsAndGlobalRepairCrisesOnly()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.ConfigureRoom(minPlayers: 8, maxPlayers: 8, autoFillAi: false);
                fixture.AttachSyncManager();
                for (ulong clientId = 0UL; clientId < 8UL; clientId++)
                {
                    fixture.SetPlayer(clientId, Vector3.zero, alive: true);
                }

                fixture.StartOnlineMatchCore();
                int[] assignedTaskIds = fixture.AssignedTaskIds(0UL);
                CollectionAssert.IsNotEmpty(assignedTaskIds);

                var assignedSet = new HashSet<int>(assignedTaskIds);
                int unassignedTaskId = -1;
                for (int taskId = 0; taskId < 28; taskId++)
                {
                    if (!assignedSet.Contains(taskId))
                    {
                        unassignedTaskId = taskId;
                        break;
                    }
                }

                Assert.GreaterOrEqual(unassignedTaskId, 0);
                string taskList = fixture.PropertyString("TaskListText");
                StringAssert.Contains("[" + fixture.TaskMapCode(assignedTaskIds[0]) + "]", taskList);
                StringAssert.DoesNotContain("[" + fixture.TaskMapCode(unassignedTaskId) + "]", taskList);

                fixture.SetExistingTaskSabotaged(unassignedTaskId, true);
                StringAssert.Contains("[" + fixture.TaskMapCode(unassignedTaskId) + "]", fixture.PropertyString("TaskListText"),
                    "未分配设施进入破坏状态后，应作为全局修复危机显示给玩家。");
            }
        }

        [Test]
        public void TaskInteraction_DoesNotAdvanceUnassignedTask()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.ConfigureRoom(minPlayers: 8, maxPlayers: 8, autoFillAi: false);
                fixture.AttachSyncManager();
                for (ulong clientId = 1UL; clientId <= 8UL; clientId++)
                {
                    fixture.SetPlayer(clientId, Vector3.zero, alive: true);
                }

                fixture.StartOnlineMatchCore();
                fixture.SetPhase("Action");
                ulong policeId = fixture.FindClientIdByPrivateRole("Police");
                var assignedSet = new HashSet<int>(fixture.AssignedTaskIds(policeId));
                int unassignedTaskId = -1;
                for (int taskId = 0; taskId < 28; taskId++)
                {
                    if (!assignedSet.Contains(taskId))
                    {
                        unassignedTaskId = taskId;
                        break;
                    }
                }

                fixture.SetPlayerPosition(policeId, fixture.TaskPosition(unassignedTaskId));
                int progressBefore = fixture.TaskProgress(unassignedTaskId);
                fixture.InteractWithTask(policeId);

                Assert.AreEqual(progressBefore, fixture.TaskProgress(unassignedTaskId));
                StringAssert.Contains("未分配", fixture.PropertyString("Status"));
            }
        }

        [Test]
        public void SharedTaskStation_EachAssignedPlayerCompletesIndependently()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.AttachSyncManager();
                fixture.AssignTask(1UL, 0);
                fixture.AssignTask(2UL, 0);

                fixture.MarkTaskActive(1UL, 0);
                Assert.IsTrue(
                    fixture.InvokeBoolOutString("ValidateTaskStart", 2UL, 0, out string concurrentStartError),
                    "共享任务站应允许不同玩家同时处理各自任务：" + concurrentStartError);
                fixture.MarkTaskActive(2UL, 0);
                Assert.IsTrue(
                    fixture.InvokeBoolOutString("ValidateAndCompleteTask", 1UL, 0, out string firstError),
                    firstError);

                Assert.IsTrue(
                    fixture.InvokeBoolOutString("ValidateAndCompleteTask", 2UL, 0, out string secondError),
                    secondError);
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
        public void TimeLimit_EvidenceHigh_StillEndsInDraw()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 9, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("平局", result);
        }

        [Test]
        public void TimeLimit_EvidenceLow_StillEndsInDraw()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((false, false), (false, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 2, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("平局", result);
        }

        [Test]
        public void TimeLimit_TasksHigh_StillEndsInDraw()
        {
            object bridge = CreateVictoryBridge();
            Array tasks = MakeTasks((true, false), (true, false));

            bool hasResult = TryTimeLimit(bridge, 350f, 300f, 3, 10, tasks, out string result);

            Assert.IsTrue(hasResult);
            StringAssert.Contains("平局", result);
        }

        [Test]
        public void TimeLimit_ControllerDoesNotResolveBeforeHardLimit()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetMatchStarted(true);
                fixture.SetPhase("Action");
                fixture.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                fixture.SetTaskServiceEvidence(score: 38, targetValue: 44);
                fixture.SetGlobalTimers(phaseTimer: 0f, emergencyCooldown: 0f, aiGrace: 0f, elapsed: 1199f);

                fixture.ResolveTimeLimitOutcome();

                Assert.AreEqual("Action", fixture.PhaseName(),
                    "控制器内部超时结算入口不能在 20 分钟硬上限前提前结束长局。");

                fixture.SetGlobalTimers(phaseTimer: 0f, emergencyCooldown: 0f, aiGrace: 0f, elapsed: 1200f);
                fixture.ResolveTimeLimitOutcome();

                Assert.AreEqual("Result", fixture.PhaseName());
                StringAssert.Contains("平局", fixture.PropertyString("Status"));
            }
        }

        [Test]
        public void MatchClock_AdvancesDuringMeetingAndVoting()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetMatchStarted(true);
                fixture.SetPhase("Meeting");
                fixture.EnsureServices();
                fixture.SetGlobalTimers(phaseTimer: 20f, emergencyCooldown: 0f, aiGrace: 0f, elapsed: 1199f);

                fixture.AdvanceMatchClock(1f);
                Assert.AreEqual(1200f, fixture.MatchElapsed(), 0.001f);
                Assert.AreEqual("Result", fixture.PhaseName(),
                    "20 分钟硬上限必须在讨论阶段生效，不能因为尚未回到 Action 而继续超时。");
            }
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
        public void MainMenu_UsesHarbourArtAndReadablePrimaryActions()
        {
            Type menuType = RuntimeType("GanglandUndercover.UI.MainMenuController");
            GameObject menuHost = new GameObject("MainMenuVisualRegressionHost");

            try
            {
                menuHost.AddComponent(menuType);
                object menu = menuHost.GetComponent(menuType);
                menuType.GetMethod("Initialize").Invoke(menu, new object[] { null });

                GameObject backdrop = FindObjectNamedIncludingInactive("HarbourBackdrop");
                Assert.IsNotNull(backdrop, "主菜单必须以港区场景建立第一眼的游戏身份。");
                Assert.IsNotNull(backdrop.GetComponent<UnityEngine.UI.Image>().sprite);

                GameObject mapPreview = FindObjectNamedIncludingInactive("MapPreview");
                Assert.IsNotNull(mapPreview, "主菜单必须显示当前行动区域预览。");
                Assert.AreEqual(
                    "gangland-harbour-map-preview-v1",
                    mapPreview.GetComponent<UnityEngine.UI.Image>().sprite.texture.name,
                    "九龙港区必须优先使用与登录背景同方向的 AI 审阅地点预览。");

                for (int i = 0; i < 4; i++)
                {
                    GameObject portrait = FindObjectNamedIncludingInactive("RolePortrait_" + i);
                    Assert.IsNotNull(portrait, "每个可选身份都必须显示实际角色头像，而不是字母占位。第 " + i + " 项缺失。");
                    Assert.IsNotNull(portrait.GetComponent<UnityEngine.UI.Image>().sprite);
                }

                Assert.IsNotNull(FindObjectNamedIncludingInactive("ConnectionState"));

                GameObject offlineAction = FindObjectNamedIncludingInactive("StartButton");
                GameObject onlineAction = FindObjectNamedIncludingInactive("EnterLobbyButton");
                Assert.GreaterOrEqual(offlineAction.GetComponent<RectTransform>().sizeDelta.y, 60f);
                Assert.GreaterOrEqual(onlineAction.GetComponent<RectTransform>().sizeDelta.y, 60f);
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
        public void RelayMigrationLobbySessionOptions_CarryHostMigrationDiscoveryMarker()
        {
            object options = BuildRelayMigrationLobbySessionOptions(
                roomName: " 九龙港区夜局 ",
                maxPlayers: 8,
                relayCode: " new999 ",
                mapName: "港区",
                ruleSummary: "AI补位");
            IDictionary properties = (IDictionary)options.GetType().GetProperty("SessionProperties").GetValue(options);

            Assert.AreEqual("NEW999", PropertyString(properties["relayCode"], "Value"));
            Assert.AreEqual("relay-replacement", PropertyString(properties["hostMigration"], "Value"),
                "Host migration replacement Relay 房必须带可被旧客户端发现的标记。");
            Assert.AreEqual("Public", PropertyValueText(properties["hostMigration"], "Visibility"));
        }

        [Test]
        public void HostMigrationRelayCandidate_MatchesOnlyMarkedJoinableSameRoom()
        {
            Assert.IsTrue(IsHostMigrationRelayCandidate(
                expectedRoomName: "九龙港区夜局",
                candidateRoomName: "九龙港区夜局",
                relayCode: "new999",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                migrationValue: "relay-replacement"));

            Assert.IsFalse(IsHostMigrationRelayCandidate(
                expectedRoomName: "九龙港区夜局",
                candidateRoomName: "其他房间",
                relayCode: "new999",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                migrationValue: "relay-replacement"));

            Assert.IsFalse(IsHostMigrationRelayCandidate(
                expectedRoomName: "九龙港区夜局",
                candidateRoomName: "九龙港区夜局",
                relayCode: string.Empty,
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                migrationValue: "relay-replacement"));

            Assert.IsFalse(IsHostMigrationRelayCandidate(
                expectedRoomName: "九龙港区夜局",
                candidateRoomName: "九龙港区夜局",
                relayCode: "new999",
                playerCount: 8,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                migrationValue: "relay-replacement"));

            Assert.IsFalse(IsHostMigrationRelayCandidate(
                expectedRoomName: "九龙港区夜局",
                candidateRoomName: "九龙港区夜局",
                relayCode: "new999",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                migrationValue: string.Empty));
        }

        [Test]
        public void HostMigrationRelayRoomJoinIntent_AllowsOnlyDisconnectedMarkedSameRoom()
        {
            object allowed = BuildHostMigrationRelayRoomSessionJoin(
                hasDisconnectedNetworkSession: true,
                expectedRoomName: "九龙港区夜局",
                sessionId: "session-new",
                candidateRoomName: "九龙港区夜局",
                relayCode: " new999 ",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                isHostMigration: true,
                allowLocalPreview: false);

            object notDisconnected = BuildHostMigrationRelayRoomSessionJoin(
                hasDisconnectedNetworkSession: false,
                expectedRoomName: "九龙港区夜局",
                sessionId: "session-new",
                candidateRoomName: "九龙港区夜局",
                relayCode: "new999",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                isHostMigration: true,
                allowLocalPreview: false);

            object wrongRoom = BuildHostMigrationRelayRoomSessionJoin(
                hasDisconnectedNetworkSession: true,
                expectedRoomName: "九龙港区夜局",
                sessionId: "session-new",
                candidateRoomName: "其他房间",
                relayCode: "new999",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                isHostMigration: true,
                allowLocalPreview: false);

            object unmarked = BuildHostMigrationRelayRoomSessionJoin(
                hasDisconnectedNetworkSession: true,
                expectedRoomName: "九龙港区夜局",
                sessionId: "session-new",
                candidateRoomName: "九龙港区夜局",
                relayCode: "new999",
                playerCount: 2,
                maxPlayers: 8,
                isLocked: false,
                hasPassword: false,
                isHostMigration: false,
                allowLocalPreview: false);

            Assert.IsTrue(FieldBool(allowed, "CanJoinRelay"));
            Assert.IsTrue(FieldBool(allowed, "CanJoinSession"));
            Assert.AreEqual("NEW999", FieldString(allowed, "RelayCode"));
            Assert.IsFalse(FieldBool(notDisconnected, "CanJoinRelay"));
            StringAssert.Contains("断线恢复", FieldString(notDisconnected, "StatusText"));
            Assert.IsFalse(FieldBool(wrongRoom, "CanJoinRelay"));
            Assert.IsFalse(FieldBool(unmarked, "CanJoinRelay"));
            StringAssert.Contains("Host migration", FieldString(wrongRoom, "StatusText"));
            StringAssert.Contains("Host migration", FieldString(unmarked, "StatusText"));
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
        public void CharacterCustomPayload_RejectsMalformedAndEmptyPayloads()
        {
            Type customizerType = RuntimeType("GanglandUndercover.SocialDeduction.CharacterCustomizer");

            using (FastBufferWriter oversizedLengthWriter = new FastBufferWriter(32, Allocator.Temp))
            {
                oversizedLengthWriter.WriteValueSafe(42UL);
                oversizedLengthWriter.WriteValueSafe(4096);

                using (FastBufferReader reader = new FastBufferReader(oversizedLengthWriter, Allocator.Temp))
                {
                    object[] readArgs = { reader, 0UL, null };
                    bool decoded = (bool)StaticNonPublic(customizerType, "TryReadCustomMessagePayload").Invoke(null, readArgs);

                    Assert.IsFalse(decoded, "声明超长 JSON 的 CharacterCustom payload 必须被拒绝。");
                    Assert.AreEqual(string.Empty, readArgs[2]);
                }
            }

            using (FastBufferWriter emptyJsonWriter = new FastBufferWriter(32, Allocator.Temp))
            {
                object[] writeArgs = { emptyJsonWriter, 42UL, string.Empty };
                bool encoded = (bool)StaticNonPublic(customizerType, "TryWriteCustomMessagePayload").Invoke(null, writeArgs);

                Assert.IsFalse(encoded, "空 JSON 不应被编码为有效 CharacterCustom payload。");
            }

            using (FastBufferWriter truncatedWriter = new FastBufferWriter(32, Allocator.Temp))
            {
                truncatedWriter.WriteValueSafe(42UL);
                truncatedWriter.WriteValueSafe(12);
                truncatedWriter.WriteValueSafe((byte)123);
                truncatedWriter.WriteValueSafe((byte)125);

                using (FastBufferReader reader = new FastBufferReader(truncatedWriter, Allocator.Temp))
                {
                    object[] readArgs = { reader, 0UL, null };
                    bool decoded = (bool)StaticNonPublic(customizerType, "TryReadCustomMessagePayload").Invoke(null, readArgs);

                    Assert.IsFalse(decoded, "截断 JSON 字节流必须被拒绝，不能抛出到消息循环外。");
                }
            }
        }

        [Test]
        public void CharacterCustom_AuthorizationRejectsUnspawnedOrNonOwnerSender()
        {
            Type customizerType = RuntimeType("GanglandUndercover.SocialDeduction.CharacterCustomizer");
            GameObject host = new GameObject("CharacterCustom_AuthorizationTest");

            try
            {
                object customizer = host.AddComponent(customizerType);
                MethodInfo canAccept = customizerType.GetMethod(
                    "CanAcceptCustomDataFrom",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNotNull(canAccept);
                Assert.IsFalse((bool)canAccept.Invoke(customizer, new object[] { 0UL }),
                    "未 Spawn 的 CharacterCustomizer 不能接受任意 sender 的外观数据。");
                Assert.IsFalse((bool)canAccept.Invoke(customizer, new object[] { 99UL }),
                    "未 Spawn 的 CharacterCustomizer 不能接受伪造 ownerId 的外观数据。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CharacterCustom_RemoteAuthorizationPolicyAcceptsOnlyOwnerOnServerAndServerOnClient()
        {
            Type customizerType = RuntimeType("GanglandUndercover.SocialDeduction.CharacterCustomizer");
            MethodInfo canApply = StaticNonPublic(customizerType, "CanApplyRemoteCustomData");

            Assert.IsFalse((bool)canApply.Invoke(null, new object[] { true, false, 7UL, 7UL }),
                "服务器不能接受未 Spawn 的外观对象消息。");
            Assert.IsTrue((bool)canApply.Invoke(null, new object[] { true, true, 7UL, 7UL }),
                "服务器只接受对象 owner 提交自己的外观数据。");
            Assert.IsFalse((bool)canApply.Invoke(null, new object[] { true, true, 7UL, 8UL }),
                "服务器必须拒绝非 owner 伪造其他 NetworkObject 的外观数据。");

            Assert.IsTrue((bool)canApply.Invoke(null, new object[] { false, true, 7UL, 0UL }),
                "客户端只接受 Server 广播的外观数据。");
            Assert.IsFalse((bool)canApply.Invoke(null, new object[] { false, true, 7UL, 7UL }),
                "客户端必须拒绝 peer 伪装成对象 owner 的外观广播。");
        }

        [Test]
        public void CharacterCustom_ApplyCustomDataRejectsWrongPartIds()
        {
            Type customizerType = RuntimeType("GanglandUndercover.SocialDeduction.CharacterCustomizer");
            Type wardrobeDataType = RuntimeType("GanglandUndercover.SocialDeduction.WardrobeData");
            Type wardrobeItemType = RuntimeType("GanglandUndercover.SocialDeduction.WardrobeItem");
            Type wardrobePartType = RuntimeType("GanglandUndercover.SocialDeduction.WardrobePart");
            Type wardrobeRarityType = RuntimeType("GanglandUndercover.SocialDeduction.WardrobeRarity");

            GameObject host = new GameObject("CharacterCustom_WrongPartIdsTest");
            ScriptableObject wardrobeData = ScriptableObject.CreateInstance(wardrobeDataType);

            try
            {
                object customizer = host.AddComponent(customizerType);
                IList items = (IList)wardrobeDataType.GetField("items").GetValue(wardrobeData);
                items.Clear();
                items.Add(CreateWardrobeItem(wardrobeItemType, wardrobePartType, wardrobeRarityType,
                    "hat_none", "Hat", "Common"));
                items.Add(CreateWardrobeItem(wardrobeItemType, wardrobePartType, wardrobeRarityType,
                    "top_jacket", "Top", "Common"));
                items.Add(CreateWardrobeItem(wardrobeItemType, wardrobePartType, wardrobeRarityType,
                    "height_l", "Height", "Common"));

                customizerType.GetField("wardrobeData", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(customizer, wardrobeData);
                customizerType.GetMethod("InitializeDefaults", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(customizer, null);

                customizerType.GetMethod("ApplyCustomDataJson")
                    .Invoke(customizer, new object[]
                    {
                        "{\"hat\":\"height_l\",\"top\":\"top_jacket\",\"height\":\"hat_none\"}"
                    });

                IDictionary selection = (IDictionary)customizerType
                    .GetField("currentSelection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(customizer);

                Assert.AreEqual("hat_none", selection[Enum.Parse(wardrobePartType, "Hat")],
                    "Hat 部位不能接受 Height 物品 ID。");
                Assert.AreEqual("top_jacket", selection[Enum.Parse(wardrobePartType, "Top")],
                    "合法且部位匹配的 Top 物品应继续生效。");
                Assert.AreEqual("height_l", selection[Enum.Parse(wardrobePartType, "Height")],
                    "Height 部位不能接受 Hat 物品 ID。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wardrobeData);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HostMigration_ElectsLowestRemainingClientAndExcludesOldHost()
        {
            Type migrationType = RuntimeType("GanglandUndercover.Online.HostMigrationManager");
            MethodInfo elect = migrationType.GetMethod(
                "ElectNewHostId",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(elect, "HostMigrationManager 应暴露可测的选举策略。");

            Assert.AreEqual(1UL, elect.Invoke(null, new object[] { new ulong[] { 0UL, 3UL, 1UL, 2UL }, 0UL }),
                "旧 Host 0 断开时，应从剩余客户端中选择最小 clientId。");
            Assert.AreEqual(2UL, elect.Invoke(null, new object[] { new ulong[] { 4UL, 2UL, 8UL }, 4UL }),
                "选举必须排除旧 Host，即使旧 Host 在候选列表中。");
            Assert.AreEqual(0UL, elect.Invoke(null, new object[] { new ulong[] { 0UL }, 0UL }),
                "没有剩余客户端时不应选出新 Host。");
            Assert.AreEqual(0UL, elect.Invoke(null, new object[] { Array.Empty<ulong>(), 0UL }),
                "空候选列表必须安全返回无新 Host。");
        }

        [Test]
        public void HostMigration_TryElectionKeepsCandidateZeroDistinctFromNoCandidate()
        {
            Type migrationType = RuntimeType("GanglandUndercover.Online.HostMigrationManager");
            MethodInfo tryElect = migrationType.GetMethod(
                "TryElectNewHostId",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(tryElect, "HostMigrationManager 应暴露可区分候选存在性的选举策略。");

            object[] candidateZeroArgs = { new ulong[] { 4UL, 0UL, 2UL }, 4UL, 999UL };
            Assert.IsTrue((bool)tryElect.Invoke(null, candidateZeroArgs),
                "当旧 Host 不是 0 时，clientId 0 仍可能是合法候选，不能被当作无候选。");
            Assert.AreEqual(0UL, candidateZeroArgs[2]);

            object[] noCandidateArgs = { new ulong[] { 4UL }, 4UL, 999UL };
            Assert.IsFalse((bool)tryElect.Invoke(null, noCandidateArgs),
                "只有旧 Host 存在时必须明确返回无候选。");
            Assert.AreEqual(0UL, noCandidateArgs[2]);
        }

        [Test]
        public void HostMigration_OnlyServerSenderCanDeliverHostMessages()
        {
            Type migrationType = RuntimeType("GanglandUndercover.Online.HostMigrationManager");
            MethodInfo policy = migrationType.GetMethod(
                "IsTrustedHostMessageSender",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(policy, "HostMigrationManager 应集中判定迁移消息的发送者权限。");

            Assert.IsTrue((bool)policy.Invoke(null, new object[] { 0UL, 0UL, false }),
                "客户端收到当前服务器发送的迁移消息时应接受。");
            Assert.IsFalse((bool)policy.Invoke(null, new object[] { 7UL, 0UL, false }),
                "客户端不得接受普通客户端伪造的主机迁移消息。");
            Assert.IsFalse((bool)policy.Invoke(null, new object[] { 0UL, 0UL, true }),
                "主机本机不得把自己广播的迁移快照再次恢复。");
        }

        [Test]
        public void HostMigration_ReplacementHostStartPolicyBlocksUnsafePromotion()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            MethodInfo canAttempt = controllerType.GetMethod(
                "CanAttemptReplacementHostStart",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(canAttempt, "OnlineMatchController 应集中判定 Host migration 接管启动策略。");

            object[] directStoppedArgs = { false, false, string.Empty, string.Empty };
            Assert.IsTrue((bool)canAttempt.Invoke(null, directStoppedArgs),
                "直连客户端旧连接已关闭时，允许尝试启动新 Host。");
            Assert.AreEqual(string.Empty, directStoppedArgs[3]);

            object[] relayArgs = { false, false, "6kb6dh", string.Empty };
            Assert.IsFalse((bool)canAttempt.Invoke(null, relayArgs),
                "Relay 客户端不能复用旧房间码直接接管 Host。");
            StringAssert.Contains("Relay", (string)relayArgs[3]);

            object[] stillListeningArgs = { false, true, string.Empty, string.Empty };
            Assert.IsFalse((bool)canAttempt.Invoke(null, stillListeningArgs),
                "旧客户端连接仍在监听时，不能同步启动 replacement Host。");
            StringAssert.Contains("旧客户端连接", (string)stillListeningArgs[3]);

            object[] alreadyServerArgs = { true, true, "6kb6dh", string.Empty };
            Assert.IsTrue((bool)canAttempt.Invoke(null, alreadyServerArgs),
                "如果本机已经是 server/host，应允许完成迁移收尾。");
            Assert.AreEqual(string.Empty, alreadyServerArgs[3]);
        }

        [Test]
        public void HostMigration_RelayReplacementRouteDetectsOldRelayCode()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            MethodInfo shouldUseRelay = controllerType.GetMethod(
                "ShouldUseRelayReplacementHostForMigration",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(shouldUseRelay, "OnlineMatchController 应暴露 Relay replacement Host 路由判定。");

            Assert.IsFalse((bool)shouldUseRelay.Invoke(null, new object[] { string.Empty }),
                "没有旧 Relay 房间码时应走直连 replacement Host。");
            Assert.IsTrue((bool)shouldUseRelay.Invoke(null, new object[] { " 6kb6dh " }),
                "存在旧 Relay 房间码时必须创建新 Relay allocation，而不是复用旧码直接接管。");
        }

        [Test]
        public void CameraAuthorization_RequiresActionAliveRangeOrRemoteSurveillance()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                Vector2 cameraCenter = Vector2.zero;

                fixture.SetPhase("Meeting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Inspector");
                Assert.IsFalse(fixture.CanClientWatchCamera(1UL, cameraCenter),
                    "非行动阶段不能观看监控。");

                fixture.SetPhase("Action");
                fixture.SetPlayer(2UL, Vector3.zero, alive: false, roleName: "Police", professionName: "Inspector");
                Assert.IsFalse(fixture.CanClientWatchCamera(2UL, cameraCenter),
                    "死亡玩家不能通过普通监控权限观看。");

                fixture.SetPlayer(3UL, new Vector3(100f, 0f, 0f), alive: true, roleName: "Police", professionName: "Inspector");
                Assert.IsFalse(fixture.CanClientWatchCamera(3UL, cameraCenter),
                    "无远程监控能力且距离过远时不能观看。");

                fixture.SetPlayer(4UL, new Vector3(1f, 0f, 0f), alive: true, roleName: "Police", professionName: "Inspector");
                Assert.IsTrue(fixture.CanClientWatchCamera(4UL, cameraCenter),
                    "无远程监控能力但靠近摄像头时可以观看。");

                fixture.SetPlayer(5UL, new Vector3(100f, 0f, 0f), alive: true, roleName: "Police", professionName: "Tech");
                Assert.IsTrue(fixture.CanClientWatchCamera(5UL, cameraCenter),
                    "Tech 的 RemoteSurveillance 能力应允许远程观看监控。");

                Assert.IsFalse(fixture.CanClientWatchCamera(999UL, cameraCenter),
                    "未知 clientId 不能观看监控。");
            }
        }

        [Test]
        public void SecurityCamera_StartWatchingRequestMaintainsAuthorizedWatcherSet()
        {
            Type cameraType = RuntimeType("GanglandUndercover.Online.Surveillance.OnlineSecurityCamera");
            GameObject host = new GameObject("SecurityCamera_RpcAuthorizationTest");

            try
            {
                using (ControllerFixture fixture = new ControllerFixture())
                {
                    object camera = host.AddComponent(cameraType);
                    cameraType.GetMethod("BindController").Invoke(camera, new[] { fixture.Controller });

                    fixture.SetPhase("Action");
                    fixture.SetPlayer(3UL, new Vector3(100f, 0f, 0f), alive: true, roleName: "Police", professionName: "Inspector");
                    InvokeStartWatching(cameraType, camera, 3UL);
                    Assert.IsFalse(CameraWatcherContains(camera, 3UL),
                        "远距离普通玩家伪造观看请求不应进入 watcher 集合。");

                    fixture.SetPlayer(4UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Inspector");
                    InvokeStartWatching(cameraType, camera, 4UL);
                    Assert.IsTrue(CameraWatcherContains(camera, 4UL),
                        "行动阶段、存活且在范围内的玩家应能观看摄像头。");

                    fixture.SetPhase("Meeting");
                    InvokeStartWatching(cameraType, camera, 4UL);
                    Assert.IsFalse(CameraWatcherContains(camera, 4UL),
                        "已在 watcher 集合中的玩家一旦不再满足授权条件，应被请求路径移除。");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
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
                players: MakePlayers((0, "Gang"), (3, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "警方胜利");
        }

        [Test]
        public void Victory_NumericParityAloneDoesNotEndMatch()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayers((3, "Gang"), (2, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            Assert.IsFalse(PropertyBool(result, "HasResult"));
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
        public void Victory_UndercoverLastAliveMeansGangSideEliminated()
        {
            object result = EvaluateVictory(
                evidenceScore: 5,
                evidenceTarget: 44,
                players: MakePlayers((0, "Gang"), (0, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "警方胜利");
        }

        [Test]
        public void Victory_EvidenceClosureWithoutLivingUndercover_GangWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 44,
                evidenceTarget: 44,
                players: MakePlayersWithEliminatedUndercover((1, "Gang"), (3, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "黑帮胜利");
        }

        [Test]
        public void Victory_UndercoverEliminated_GangWins()
        {
            object result = EvaluateVictory(
                evidenceScore: 10,
                evidenceTarget: 44,
                players: MakePlayersWithEliminatedUndercover((1, "Gang"), (3, "Police"), (1, "Undercover"), (0, "Mole")),
                tasks: MakeTasks(),
                matchStarted: true,
                phaseName: "Action");

            AssertResult(result, true, "黑帮胜利");
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
        public void KillPermissions_GangCanTargetUndercoverAndMoleNeedsAssignedHit()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Mole", professionName: "Mole");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                fixture.SetPlayer(4UL, Vector3.up, alive: true, roleName: "Gang");

                fixture.AccumulateMoleIntel(1UL, 5);
                Assert.AreEqual(3UL, fixture.AssignMoleHit(1UL));

                Assert.IsTrue(fixture.CanKillTarget(4UL, 3UL), "真黑帮必须能拔除伪装成黑帮的卧底。");
                Assert.IsTrue(fixture.CanKillTarget(4UL, 1UL), "真黑帮必须承担误伤内鬼的风险，否则击杀按钮会变成身份探测器。");
                Assert.IsTrue(fixture.CanKillTarget(1UL, 3UL), "内鬼只能执行已经解锁的卧底暗杀目标。");
                Assert.IsFalse(fixture.CanKillTarget(1UL, 2UL), "内鬼不能像普通黑帮一样任意击杀警察。");
            }
        }

        [Test]
        public void MoleIntel_LocksTargetButDoesNotWinBeforeTargetIsEliminated()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Mole", professionName: "Mole");
                fixture.SetPlayer(2UL, new Vector3(0.1f, 0f, 0f), alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Police");

                fixture.AccumulateMoleIntel(1UL, 5);

                Assert.AreEqual(2UL, fixture.AssignMoleHit(1UL));
                Assert.IsFalse(fixture.CheckMoleWinCondition(1UL), "只识别卧底不能直接结束对局，必须完成清除。 ");

                fixture.SetPlayerAlive(2UL, false);
                Assert.IsFalse(fixture.CheckMoleWinCondition(1UL), "卧底被其他人淘汰时，不能误判为内鬼特殊胜利。");

                fixture.SetPlayerAlive(2UL, true);
                fixture.ApplyClientAction(1UL, "Kill", 2UL);
                Assert.IsTrue(fixture.CheckMoleWinCondition(1UL), "只有内鬼亲手清除锁定目标后，才满足特殊胜利。");
            }
        }

        [Test]
        public void KillRequest_MoleUsesAssignedNearbyTarget()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Mole", professionName: "Mole");
                fixture.SetPlayer(2UL, new Vector3(0.1f, 0f, 0f), alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                fixture.SetPlayer(3UL, Vector3.right, alive: true, roleName: "Police");
                fixture.AccumulateMoleIntel(0UL, 5);
                Assert.AreEqual(2UL, fixture.AssignMoleHit(0UL));

                fixture.RequestAction("Kill");

                Assert.IsFalse(fixture.PlayerAlive(2UL), "Mole 的 Q/击倒按钮必须提交客户端选中的锁定目标。");
                Assert.IsTrue(fixture.CheckMoleWinCondition(0UL));
            }
        }

        [Test]
        public void KillAction_UsesExplicitTargetInsteadOfNearestDifferentPlayer()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Gang");
                fixture.SetPlayer(2UL, new Vector3(0.6f, 0f, 0f), alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, new Vector3(0.1f, 0f, 0f), alive: true, roleName: "Police");

                fixture.ApplyClientAction(0UL, "Kill", 2UL);

                Assert.IsFalse(fixture.PlayerAlive(2UL), "服务端必须击杀客户端明确选择且仍在范围内的目标。");
                Assert.IsTrue(fixture.PlayerAlive(3UL), "不能在服务端重新选择另一个更近目标。");
            }
        }

        [Test]
        public void KillAction_AppliesProfessionCooldownMultiplier()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Gang", professionName: "Enforcer");
                fixture.SetPlayer(1UL, new Vector3(0.1f, 0f, 0f), alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");

                fixture.ApplyClientAction(0UL, "Kill", 1UL);

                Assert.AreEqual(22.5f, fixture.KillCooldown(0UL), 0.001f,
                    "4 人档基础冷却 30 秒，打手 0.75 倍后应为 22.5 秒。");
            }
        }

        [Test]
        public void ProfessionAbilities_ProduceStateEvidenceAndWorldFeedback()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Inspector");
                fixture.SetPlayer(1UL, new Vector3(1f, 0f, 0f), alive: true, roleName: "Police", professionName: "Tech");
                fixture.ApplyClientAction(0UL, "Ability", 0UL);

                Assert.AreEqual("FootprintTrack", fixture.PropertyString("LastProfessionAbilityFeedback"));
                Assert.GreaterOrEqual(fixture.VisibleFootprintCount(0UL), 1,
                    "Inspector 技能应留下附近玩家的可追踪足迹。");
                Assert.GreaterOrEqual(fixture.PropertyInt("ProfessionAbilityVfxCount"), 1,
                    "Inspector 技能应生成可见世界反馈。");
            }
        }

        [Test]
        public void ProfessionAbilities_CorpseExamineAddsBonusAndSurveillanceFeedsEvidence()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Forensics");
                fixture.AddBody(4, 1UL, Vector3.zero, reported: false);
                fixture.ApplyClientAction(0UL, "Ability", 0UL);

                Assert.AreEqual("CorpseExamine", fixture.PropertyString("LastProfessionAbilityFeedback"));
                StringAssert.Contains("+3", fixture.PropertyString("Status"),
                    "法证在尸体旁应获得基础线索与 CorpseExamine 加成。");
                Assert.GreaterOrEqual(fixture.PropertyInt("ProfessionAbilityVfxCount"), 1);
            }

            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, new Vector3(100f, 0f, 0f), alive: true, roleName: "Police", professionName: "Tech");
                fixture.ApplyClientAction(0UL, "Ability", 0UL);

                Assert.AreEqual("RemoteSurveillance", fixture.PropertyString("LastProfessionAbilityFeedback"));
                Assert.GreaterOrEqual(fixture.PropertyInt("ProfessionAbilityVfxCount"), 1,
                    "Tech 远程监控技能应生成可见监控反馈。");
            }
        }

        [Test]
        public void ProfessionAbilities_DarkVisionIsTemporaryAndOverridesBlackoutVisionPenalty()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Enforcer");
                fixture.SetBlackoutVisionReduced(true);
                Assert.AreEqual(0.4f, fixture.PropertyFloat("BlackoutVisionMultiplier"), 0.001f);

                fixture.ApplyClientAction(0UL, "Ability", 0UL);

                Assert.IsTrue(fixture.IsDarkVisionActive(0UL));
                Assert.AreEqual(1f, fixture.PropertyFloat("BlackoutVisionMultiplier"), 0.001f);
                Assert.AreEqual("DarkVision", fixture.PropertyString("LastProfessionAbilityFeedback"));
                Assert.GreaterOrEqual(fixture.PropertyInt("ProfessionAbilityVfxCount"), 1);

                fixture.TickProfessionAbilities(6.1f);
                Assert.IsFalse(fixture.IsDarkVisionActive(0UL), "暗视技能应在持续时间结束后自动失效。");
                Assert.AreEqual(0.4f, fixture.PropertyFloat("BlackoutVisionMultiplier"), 0.001f);
            }
        }

        [Test]
        public void SelectedSabotage_UsesRequestedTypeAndAllowsHiddenRoles()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                fixture.SetPlayer(1UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.EnsureServices();

                fixture.ApplyClientAction(0UL, "Sabotage", fixture.SabotageValue("Communications"));

                Assert.Greater(fixture.PropertyFloat("CommunicationJamTimer"), 0f,
                    "指定破坏未生效。当前状态：" + fixture.PropertyString("Status")
                    + "；技能冷却：" + fixture.AbilityCooldown(0UL));
                Assert.AreEqual(0f, fixture.PropertyFloat("BlackoutTimer"));
            }
        }

        [Test]
        public void SelectedSabotage_RejectedTypeCooldownDoesNotSpendResources()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                fixture.SetPlayer(1UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.EnsureServices();
                fixture.SetTaskServiceEvidence(score: 20, targetValue: 44);

                ulong communications = fixture.SabotageValue("Communications");
                fixture.ApplyClientAction(0UL, "Sabotage", communications);
                int evidenceAfterFirstSabotage = fixture.PropertyInt("EvidenceScore");

                fixture.TickSabotageService(31f);
                fixture.SetAbilityCooldownRaw(0UL, 0f);
                Assert.AreEqual(0f, fixture.PropertyFloat("CommunicationJamTimer"), 0.001f,
                    "破坏持续时间应先结束，而同类型服务冷却仍在继续。");

                fixture.ApplyClientAction(0UL, "Sabotage", communications);

                Assert.AreEqual(evidenceAfterFirstSabotage, fixture.PropertyInt("EvidenceScore"),
                    "服务拒绝冷却中的破坏时不能再次扣除证据。");
                Assert.AreEqual(0f, fixture.AbilityCooldown(0UL), 0.001f,
                    "服务拒绝冷却中的破坏时不能启动玩家技能冷却。");
            }
        }

        [Test]
        public void TaskInteraction_MoleUsesPoliceCoverTaskInsteadOfSabotaging()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Mole", professionName: "Mole", isBot: true);
                fixture.SetPlayer(1UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.SetTaskState(0, Vector3.zero, progress: 0, requiredProgress: 3, completed: false, sabotaged: false);

                fixture.InteractWithTask(0UL);

                Assert.AreEqual(1, fixture.TaskProgress(0),
                    "内鬼表面是警察，应通过警察任务窃取情报，不能把普通互动强制变成破坏。");
            }
        }

        [Test]
        public void GhostInteraction_PoliceCanContinueTasksButGangCannotSabotage()
        {
            using (ControllerFixture policeGhost = new ControllerFixture())
            using (ControllerFixture gangGhost = new ControllerFixture())
            {
                policeGhost.SetLocalPreviewMode(true);
                policeGhost.SetPhase("Action");
                policeGhost.SetPlayer(0UL, Vector3.zero, alive: false, roleName: "Police", isBot: true);
                policeGhost.SetTaskState(0, Vector3.zero, progress: 0, requiredProgress: 3, completed: false, sabotaged: false);

                policeGhost.ApplyClientAction(0UL, "Interact", 0UL);

                Assert.AreEqual(1, policeGhost.TaskProgress(0),
                    "警方鬼魂应能继续完成任务帮助存活队友。");

                gangGhost.SetLocalPreviewMode(true);
                gangGhost.SetPhase("Action");
                gangGhost.SetPlayer(0UL, Vector3.zero, alive: false, roleName: "Gang", isBot: true);
                gangGhost.SetPlayer(1UL, Vector3.right, alive: true, roleName: "Police");
                gangGhost.SetTaskState(0, Vector3.zero, progress: 0, requiredProgress: 3, completed: false, sabotaged: false);

                gangGhost.ApplyClientAction(0UL, "Interact", 0UL);

                Assert.IsFalse(gangGhost.TaskSabotaged(0),
                    "死亡黑帮不能通过普通互动继续制造破坏。");
            }
        }

        [Test]
        public void CriticalTaskSystem_OnlineVariantsSupportCompletionAndRestore()
        {
            Type systemType = RuntimeType("GanglandUndercover.SocialDeduction.CriticalTaskSystem");
            Type taskType = RuntimeType("GanglandUndercover.SocialDeduction.CriticalTaskType");
            Type stateType = RuntimeType("GanglandUndercover.SocialDeduction.CriticalTaskState");
            object evidenceDestruction = Enum.Parse(taskType, "EvidenceDestruction");
            object policeReinforcement = Enum.Parse(taskType, "PoliceReinforcement");
            object active = Enum.Parse(stateType, "Active");
            object completed = Enum.Parse(stateType, "Completed");

            GameObject evidenceHost = new GameObject("CriticalTaskEvidenceTest");
            GameObject reinforcementHost = new GameObject("CriticalTaskReinforcementTest");
            GameObject restoreHost = new GameObject("CriticalTaskRestoreTest");
            try
            {
                object evidenceSystem = evidenceHost.AddComponent(systemType);
                object reinforcementSystem = reinforcementHost.AddComponent(systemType);
                object restoreSystem = restoreHost.AddComponent(systemType);

                Invoke(evidenceSystem, "Trigger", evidenceDestruction);
                Assert.AreEqual(active, Property(evidenceSystem, "State"));
                Assert.IsTrue((bool)Invoke(evidenceSystem, "SubmitEvidenceRepair", 1));
                Assert.AreEqual(active, Property(evidenceSystem, "State"),
                    "证据销毁必须等两处独立修复都提交后才完成。");
                Assert.IsTrue((bool)Invoke(evidenceSystem, "SubmitEvidenceRepair", 2));
                Assert.AreEqual(completed, Property(evidenceSystem, "State"));

                Invoke(reinforcementSystem, "Trigger", policeReinforcement);
                Assert.IsTrue((bool)Invoke(reinforcementSystem, "SubmitPoliceReinforcementSabotage"));
                Assert.AreEqual(completed, Property(reinforcementSystem, "State"));

                Invoke(restoreSystem, "Trigger", evidenceDestruction);
                Invoke(restoreSystem, "RestoreActive", evidenceDestruction, 17f);
                Assert.AreEqual(active, Property(restoreSystem, "State"));
                Assert.AreEqual(17f, Convert.ToSingle(Property(restoreSystem, "TimeRemaining")), 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(evidenceHost);
                UnityEngine.Object.DestroyImmediate(reinforcementHost);
                UnityEngine.Object.DestroyImmediate(restoreHost);
            }
        }

        [Test]
        public void CriticalTaskSystem_RestoreActiveDoesNotReplayStartedEvent()
        {
            Type systemType = RuntimeType("GanglandUndercover.SocialDeduction.CriticalTaskSystem");
            Type taskType = RuntimeType("GanglandUndercover.SocialDeduction.CriticalTaskType");
            object evidenceDestruction = Enum.Parse(taskType, "EvidenceDestruction");
            GameObject host = new GameObject("CriticalTaskRestoreEventTest");
            try
            {
                object system = host.AddComponent(systemType);
                int startedEvents = 0;
                Action<object> handler = _ => startedEvents++;
                EventInfo startedEvent = systemType.GetEvent("OnCriticalTaskStarted");
                Type eventHandlerType = startedEvent.EventHandlerType;
                Delegate callback = Delegate.CreateDelegate(eventHandlerType, handler.Target, handler.Method);
                startedEvent.AddEventHandler(system, callback);

                Invoke(system, "RestoreActive", evidenceDestruction, 17f);

                Assert.AreEqual(0, startedEvents,
                    "主机迁移恢复紧急任务不能重复播放开始事件或重复触发警报。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SnapshotRestore_PreservesCriticalEvidenceRepairStations()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetLocalPreviewMode(true);
                source.SetPhase("Action");
                source.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                source.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                source.SetSingleTask(4, Vector3.zero, completed: false, sabotaged: false);
                source.TriggerCriticalTask("EvidenceDestruction");
                source.RecordCriticalRepair(1UL, 4);

                object snapshot = source.CaptureSnapshot();
                target.RestoreFromSnapshot(snapshot);

                Assert.AreEqual(1, target.CriticalEvidenceRepairStationCount(),
                    "主机迁移必须保留已完成的证据修复站点，不能让同一站点重复计入。");
            }
        }

        [Test]
        public void CriticalTaskHudSummary_ExplainsTypeTimerAndRepairProgress()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetSingleTask(4, Vector3.zero, completed: false, sabotaged: false);
                fixture.TriggerCriticalTask("EvidenceDestruction");
                fixture.RecordCriticalRepair(0UL, 4);

                string summary = fixture.PropertyString("HazardSummary");
                StringAssert.Contains("证据销毁", summary);
                StringAssert.Contains("1/2", summary);
                StringAssert.Contains("s", summary);
            }
        }

        [Test]
        public void MapPositions_DefaultToSelfAndRevealSurfaceGangOnlyDuringExposure()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(1UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.SetPlayer(2UL, Vector3.up, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");

                Assert.IsTrue(fixture.ShouldRevealPlayerPosition(0UL, 0UL));
                Assert.IsFalse(fixture.ShouldRevealPlayerPosition(0UL, 1UL));
                Assert.IsFalse(fixture.ShouldRevealPlayerPosition(0UL, 2UL));

                fixture.SetGangPositionRevealTimer(30f);
                Assert.IsTrue(fixture.ShouldRevealPlayerPosition(0UL, 1UL));
                Assert.IsFalse(fixture.ShouldRevealPlayerPosition(1UL, 3UL),
                    "位置公开只应由警方侧看到，不能让黑帮获得额外的全图追踪能力。");
            }
        }

        [Test]
        public void UndercoverSoloWin_LegacyApiNeverOverridesPoliceSideRules()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                fixture.AccumulateUndercoverIntel(1UL, 4);
                fixture.AccumulateUndercoverIntel(1UL, 4);
                Assert.IsTrue(fixture.ExecuteBetrayal(1UL));
                Assert.IsFalse(fixture.CheckUndercoverSoloWin(1UL),
                    "正式规则中卧底属于警方侧，不应通过旧接口产生独赢结果。");
            }
        }

        [Test]
        public void RepairTask_GangCannotSubmitServerRepair()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPhase("Action");
                fixture.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Gang", isBot: true);
                fixture.SetTaskState(0, Vector3.zero, progress: 0, requiredProgress: 3, completed: false, sabotaged: true);
                fixture.MarkTaskActive(0UL, 0);

                Assert.IsFalse(fixture.InvokeBoolOutString("ValidateAndRepairTask", 0UL, 0, out string error));
                StringAssert.Contains("黑帮", error);
            }
        }

        [Test]
        public void TaskCompletion_DoubleAgentsGainIntelWithoutAdvancingPoliceEvidence()
        {
            using (ControllerFixture undercover = new ControllerFixture())
            using (ControllerFixture mole = new ControllerFixture())
            {
                undercover.SetPhase("Action");
                undercover.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                undercover.SetPlayer(3UL, Vector3.right, alive: true, roleName: "Gang");
                undercover.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                undercover.SetTaskServiceEvidence(score: 20, targetValue: 44);
                undercover.MarkTaskActive(1UL, 0);

                Assert.IsTrue(undercover.InvokeBoolOutString("ValidateAndCompleteTask", 1UL, 0, out string undercoverError), undercoverError);
                Assert.AreEqual(20, undercover.PropertyInt("EvidenceScore"));
                Assert.AreEqual(1, undercover.GetUndercoverIntel(1UL));
                Assert.IsFalse(undercover.CaseLogContains("个人情报"), "公开案卷不能泄露卧底的私密情报进度。");
                Assert.IsFalse(undercover.CaseLogContains("伪装任务"), "公开案卷不能用任务文案暴露卧底身份。");

                mole.SetPhase("Action");
                mole.SetPlayer(2UL, Vector3.zero, alive: true, roleName: "Mole", professionName: "Mole");
                mole.SetPlayer(4UL, Vector3.right, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                mole.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                mole.SetTaskServiceEvidence(score: 20, targetValue: 44);
                mole.MarkTaskActive(2UL, 0);

                Assert.IsTrue(mole.InvokeBoolOutString("ValidateAndCompleteTask", 2UL, 0, out string moleError), moleError);
                Assert.AreEqual(20, mole.PropertyInt("EvidenceScore"));
                Assert.AreEqual(1, mole.GetMoleIntel(2UL));
                Assert.IsFalse(mole.CaseLogContains("个人情报"), "公开案卷不能泄露内鬼的私密情报进度。");
                Assert.IsFalse(mole.CaseLogContains("伪装任务"), "公开案卷不能用任务文案暴露内鬼身份。");
            }
        }

        [Test]
        public void Voting_MajoritySkipDoesNotEjectSingleAccusedPlayer()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Voting");
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
                fixture.SetPhase("Voting");

                fixture.ApplyVote(1UL, 2UL);
                fixture.ApplyVote(2UL, 2UL);

                Assert.AreEqual("Result", fixture.PhaseName(), "投出最后黑帮应进入结算。");
                Assert.AreEqual(1, endedCount, "即使投票后直接结算，也必须发出会议结束事件。");
            }
        }

        [Test]
        public void BeginMeeting_ClearsAbandonedTaskLocks()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                fixture.MarkTaskActive(1UL, 0);
                Assert.AreEqual(1, fixture.CollectionCount("activeTaskByPlayer"));

                fixture.BeginMeeting("任务中断会议");

                Assert.AreEqual(0, fixture.CollectionCount("activeTaskByPlayer"),
                    "会议关闭小游戏时必须同步释放服务端任务锁。");
            }
        }

        [Test]
        public void BeginMeeting_ClearsControllerVotesWhenVotingServiceIsUnavailable()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.AddVoteRaw(1UL, 2UL);
                Assert.AreEqual(1, fixture.VoteCount(), "测试夹具应先存在一张上一轮票。");
                fixture.DisableVotingService();

                fixture.BeginMeeting("服务恢复中的新会议");

                Assert.AreEqual(0, fixture.VoteCount(), "新会议必须清空上一轮控制器票据，即使 VotingService 暂未就绪。");
            }
        }

        [Test]
        public void Voting_RejectsDeadVoterAndInvalidTarget()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Voting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: false, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");

                fixture.ApplyVote(2UL, 3UL);
                fixture.ApplyVote(1UL, 99UL);

                Assert.AreEqual(0, fixture.VoteCount(), "死亡玩家和不存在目标的投票必须被 VotingService 拒绝。");
                Assert.IsFalse(fixture.HasVoted(1UL));
                Assert.IsFalse(fixture.HasVoted(2UL));
                Assert.AreEqual("Voting", fixture.PhaseName(), "无效投票不能改变投票阶段。");
            }
        }

        [Test]
        public void Voting_AllLivingFactionsCanParticipateInMeeting()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Meeting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Undercover");
                fixture.SetPlayer(4UL, Vector3.up, alive: true, roleName: "Mole");
                fixture.SetPlayer(5UL, Vector3.down, alive: true, roleName: "Police");

                fixture.ApplyVote(1UL, 5UL);
                Assert.AreEqual(0, fixture.VoteCount(), "讨论阶段只能发言和查证，不能提前提交选票。");
                Assert.AreEqual("Meeting", fixture.PhaseName());

                fixture.SetPhase("Voting");
                fixture.ApplyVote(1UL, 5UL);
                fixture.ApplyVote(2UL, 5UL);
                fixture.ApplyVote(3UL, 5UL);
                fixture.ApplyVote(4UL, 5UL);

                Assert.IsTrue(fixture.HasVoted(1UL));
                Assert.IsTrue(fixture.HasVoted(2UL));
                Assert.IsTrue(fixture.HasVoted(3UL));
                Assert.IsTrue(fixture.HasVoted(4UL));
                Assert.AreEqual(4, fixture.VoteCount());
            }
        }

        [Test]
        public void Voting_HidesLiveTargetsUntilResolution()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Voting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Undercover");

                fixture.ApplyVote(1UL, 2UL);

                string summary = fixture.PropertyString("VoteTallySummary");
                StringAssert.Contains("已提交 1/3", summary);
                StringAssert.DoesNotContain("玩家2", summary);
            }
        }

        [Test]
        public void Voting_RejectsAttemptToChangeSubmittedVote()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Voting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Undercover");

                fixture.ApplyVote(1UL, 2UL);
                fixture.ApplyVote(1UL, 3UL);

                Assert.AreEqual(1, fixture.VoteCount());
                Assert.AreEqual(2UL, fixture.VoteTarget(1UL));
            }
        }

        [Test]
        public void Voting_ServiceSharesControllerVotesAndSnapshotCapture()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Voting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");

                fixture.ApplyVote(1UL, 3UL);

                Assert.AreEqual(1, fixture.VoteCount());
                Assert.IsTrue(fixture.HasVoted(1UL));
                object snapshot = fixture.CaptureSnapshot();
                Assert.AreEqual(1, fixture.SnapshotListCount(snapshot, "Votes"));
            }
        }

        [Test]
        public void VotingService_InitializeSubscribesAfterEnableWithoutEventBus()
        {
            Type busType = RuntimeType("GanglandUndercover.Online.SimpleGameEventBus");
            Type votingType = RuntimeType("GanglandUndercover.Online.Services.VotingService");
            Type meetingCalledType = RuntimeType("GanglandUndercover.Online.MeetingCalledEvent");
            GameObject host = new GameObject("VotingServiceLifecycleRegression");

            try
            {
                object bus = host.AddComponent(busType);
                object service = host.AddComponent(votingType);

                InvokeNonPublicInstance(service, "OnDisable");
                SetNonPublicField(service, "eventBus", null);
                InvokeNonPublicInstance(service, "OnEnable");
                votingType.GetMethod("Initialize").Invoke(service, new object[] { null, bus });

                var snapshotVotes = new Dictionary<ulong, ulong> { { 1UL, 2UL } };
                votingType.GetMethod("LoadVotes").Invoke(service, new object[] { snapshotVotes });

                Assert.AreEqual(1, ServiceVoteCount(service));

                object meetingCalled = Activator.CreateInstance(meetingCalledType);
                meetingCalledType.GetField("CallerId").SetValue(meetingCalled, 9UL);
                meetingCalledType.GetField("IsEmergency").SetValue(meetingCalled, true);
                busType.GetMethod("Publish").MakeGenericMethod(meetingCalledType)
                    .Invoke(bus, new[] { meetingCalled });

                Assert.AreEqual(0, ServiceVoteCount(service), "Initialize 绑定事件总线后必须补订阅会议事件，否则新会议不会清空旧票。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EvidenceService_InitializeDoesNotDuplicateEventSubscriptions()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Inspector");
                fixture.InitializeEvidenceServiceTwice();
                fixture.PublishTaskCompleted(1UL, 1);

                Assert.AreEqual(1, fixture.EvidenceServiceScore(),
                    "重复 Initialize 不能造成同一事件被处理多次。");
            }
        }

        [Test]
        public void EvidenceService_TaskCompletedEventUpdatesControllerEvidenceScore()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetTaskServiceEvidence(score: 0, targetValue: 44);
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Inspector");

                fixture.PublishTaskCompleted(1UL, 1);

                Assert.AreEqual(1, fixture.PropertyInt("EvidenceScore"),
                    "事件驱动任务完成必须写入 controller/taskService 使用的证据分，否则胜负、HUD、快照会读到旧值。");
            }
        }

        [Test]
        public void EvidenceService_TaskCompletedEventAtTargetEvaluatesWinImmediately()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetTaskServiceEvidence(score: 43, targetValue: 44);
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police", professionName: "Inspector");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");

                fixture.PublishTaskCompleted(1UL, 1);

                Assert.AreEqual("Result", fixture.PhaseName(),
                    "事件驱动证据闭合后必须立即进入结算，不能等下一次 controller 动作。");
                StringAssert.Contains("警方胜利", fixture.PropertyString("Status"));
            }
        }

        [Test]
        public void EvidenceService_SnapshotRestoreSynchronizesLastEvidenceEvent()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetTaskServiceEvidence(score: 12, targetValue: 44);
                fixture.SetLastEvidenceEventRaw("快照证据事件");
                fixture.SyncEvidenceServiceFromController();

                Assert.AreEqual("快照证据事件", fixture.EvidenceServiceString("LastEvidenceEvent"),
                    "快照恢复必须把最近证据事件恢复到 EvidenceService，否则下一次 HUD/快照同步会回滚成旧文案。");
            }
        }

        [Test]
        public void MeetingService_OnMatchStartedSynchronizesControllerEmergencyState()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Police");
                fixture.SetPlayer(4UL, Vector3.up, alive: true, roleName: "Police");
                fixture.SetPlayer(5UL, Vector3.down, alive: true, roleName: "Police");
                fixture.SetPlayer(6UL, Vector3.one, alive: true, roleName: "Gang");

                fixture.MeetingServiceOnMatchStarted(6);

                Assert.AreEqual(2, fixture.PropertyInt("EmergencyMeetingsLeft"),
                    "MeetingService 初始化会议次数时必须同步 controller/HUD/快照读取的状态。");
                Assert.AreEqual(0f, fixture.PropertyFloat("EmergencyCooldownTimer"), 0.001f);
            }
        }

        [Test]
        public void MeetingService_CallEmergencyMeetingSynchronizesControllerState()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.MeetingServiceOnMatchStarted(2);

                fixture.MeetingServiceCallEmergencyMeeting("玩家1", 1UL);

                Assert.AreEqual("Meeting", fixture.PhaseName(),
                    "MeetingService 发起紧急会议必须驱动 controller 进入会议阶段。");
                Assert.AreEqual(0, fixture.PropertyInt("EmergencyMeetingsLeft"));
                Assert.Greater(fixture.PropertyFloat("EmergencyCooldownTimer"), 0f);
                Assert.AreEqual(1, fixture.PropertyInt("MeetingCount"));
                StringAssert.Contains("玩家1 按下警署紧急铃", fixture.PropertyString("Status"));
            }
        }

        [Test]
        public void Controller_CallEmergencyMeetingSynchronizesMeetingServiceState()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.MeetingServiceOnMatchStarted(2);

                fixture.ControllerCallEmergencyMeeting("玩家1");

                Assert.AreEqual("Meeting", fixture.PhaseName());
                Assert.AreEqual(0, fixture.PropertyInt("EmergencyMeetingsLeft"));
                Assert.AreEqual(0, fixture.MeetingServiceInt("EmergencyMeetingsLeft"),
                    "Controller 公开紧急会议入口扣次数后必须同步 MeetingService，避免后续服务入口读到旧次数。");
                Assert.Greater(fixture.PropertyFloat("EmergencyCooldownTimer"), 0f);
                Assert.AreEqual(
                    fixture.PropertyFloat("EmergencyCooldownTimer"),
                    fixture.MeetingServiceFloat("EmergencyCooldownTimer"),
                    0.001f);
                Assert.AreEqual(1, fixture.PropertyInt("MeetingCount"));
                Assert.AreEqual(1, fixture.MeetingServiceInt("MeetingCount"));
            }
        }

        [Test]
        public void Controller_CallEmergencyMeetingDoesNotBypassServiceCooldown()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.MeetingServiceOnMatchStarted(2);
                fixture.SetMeetingServiceCooldown(9f);

                fixture.ControllerCallEmergencyMeeting("玩家1");

                Assert.AreEqual("Action", fixture.PhaseName(),
                    "Controller 公开入口必须尊重 MeetingService 冷却，不能在 Consume 失败后仍然 BeginMeeting。");
                Assert.AreEqual(0, fixture.PropertyInt("MeetingCount"));
                Assert.AreEqual(0, fixture.MeetingServiceInt("MeetingCount"));
            }
        }

        [Test]
        public void Controller_TryReportOrEmergencySynchronizesMeetingServiceState()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.MeetingServiceOnMatchStarted(2);

                fixture.ControllerTryReportOrEmergency(1UL);

                Assert.AreEqual("Meeting", fixture.PhaseName());
                Assert.AreEqual(0, fixture.PropertyInt("EmergencyMeetingsLeft"));
                Assert.AreEqual(0, fixture.MeetingServiceInt("EmergencyMeetingsLeft"),
                    "行动输入触发的紧急铃路径也必须同步 MeetingService，不能只更新 controller 字段。");
                Assert.Greater(fixture.PropertyFloat("EmergencyCooldownTimer"), 0f);
                Assert.AreEqual(
                    fixture.PropertyFloat("EmergencyCooldownTimer"),
                    fixture.MeetingServiceFloat("EmergencyCooldownTimer"),
                    0.001f);
                Assert.AreEqual(1, fixture.PropertyInt("MeetingCount"));
                Assert.AreEqual(1, fixture.MeetingServiceInt("MeetingCount"));
            }
        }

        [Test]
        public void Controller_ReportIsRejectedOutsideActionPhase()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Meeting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: false, roleName: "Gang");
                fixture.AddBody(9, 2UL, Vector3.zero, reported: false);
                fixture.MeetingServiceOnMatchStarted(2);
                EventProbe probe = fixture.AttachEventProbe();

                fixture.ControllerTryReportOrEmergency(1UL);

                Assert.AreEqual("Meeting", fixture.PhaseName());
                Assert.AreEqual(0, fixture.PropertyInt("MeetingCount"));
                Assert.AreEqual(0, probe.BodyReportedCount);
            }
        }

        [Test]
        public void Controller_EmergencyCallIsRejectedOutsideActionPhase()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Meeting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.MeetingServiceOnMatchStarted(2);

                fixture.ControllerCallEmergencyMeeting("玩家1");

                Assert.AreEqual("Meeting", fixture.PhaseName());
                Assert.AreEqual(0, fixture.PropertyInt("MeetingCount"));
            }
        }

        [Test]
        public void Controller_CallEmergencyMeetingPublishesMeetingCalledEvent()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
                fixture.MeetingServiceOnMatchStarted(2);
                EventProbe probe = fixture.AttachEventProbe();

                fixture.ControllerCallEmergencyMeeting("玩家1");

                Assert.AreEqual(1, probe.MeetingCalledCount,
                    "Controller 公开紧急会议入口必须发布 MeetingCalledEvent，和 MeetingService 入口保持一致。");
                Assert.IsTrue(probe.LastMeetingCalledIsEmergency);
                Assert.AreEqual(1UL, probe.LastMeetingCallerId);
                Assert.AreEqual(0, probe.BodyReportedCount);
            }
        }

        [Test]
        public void Controller_BodyReportPublishesBodyReportedAndMeetingCalledEvents()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: false, roleName: "Gang");
                fixture.AddBody(9, 2UL, Vector3.zero, reported: false);
                fixture.MeetingServiceOnMatchStarted(2);
                EventProbe probe = fixture.AttachEventProbe();

                fixture.ControllerTryReportOrEmergency(1UL);

                Assert.AreEqual(1, probe.BodyReportedCount,
                    "Controller 尸体报告路径必须发布 BodyReportedEvent，避免事件驱动系统漏掉报案。");
                Assert.AreEqual(1UL, probe.LastBodyReporterId);
                Assert.AreEqual(2UL, probe.LastBodyVictimId);
                Assert.AreEqual(1, probe.MeetingCalledCount);
                Assert.IsFalse(probe.LastMeetingCalledIsEmergency);
                Assert.AreEqual(1UL, probe.LastMeetingCallerId);
            }
        }

        [Test]
        public void Voting_EvidenceWeightCanBreakSkipTieAndEjectAccused()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Voting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");
                fixture.SeedEvidenceChainForAccusation(1UL, 3UL);

                fixture.ApplyVote(1UL, 3UL);
                fixture.ApplySkipVote(2UL);
                fixture.ApplySkipVote(3UL);

                Assert.AreEqual("Result", fixture.PhaseName(), "证据链权重应让被指证黑帮出局并触发胜负结算。");
                Assert.IsFalse(fixture.PlayerAlive(3UL));
                StringAssert.Contains("警方胜利", fixture.PropertyString("Status"));
            }
        }

        [Test]
        public void Voting_DoesNotAutoSubmitEvidenceChainFromOrdinaryVote()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Voting");
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");
                fixture.SeedEvidenceNodes(1UL);

                fixture.ApplyVote(1UL, 3UL);
                fixture.ApplySkipVote(2UL);
                fixture.ApplySkipVote(3UL);

                Assert.AreEqual("Action", fixture.PhaseName());
                Assert.IsTrue(fixture.PlayerAlive(3UL));
                StringAssert.Contains("无人出局", fixture.PropertyString("LastVoteOutcome"));
            }
        }

        [Test]
        public void SnapshotService_RestoresPlayersTasksBodiesVotesAndTimers()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetPhase("Voting");
                source.SetMatchStarted(true);
                source.SetPlayer(1UL, new Vector3(1f, 2f, 0f), alive: true, roleName: "Police", professionName: "Inspector");
                source.SetPlayer(2UL, new Vector3(3f, 4f, 0f), alive: false, roleName: "Gang", professionName: "Enforcer");
                source.SetSingleTask(7, new Vector3(5f, 6f, 0f), completed: true, sabotaged: true);
                source.AddBody(4, 2UL, new Vector3(7f, 8f, 0f), reported: false);
                source.SetTaskServiceEvidence(score: 33, targetValue: 44);
                source.SetSabotageTimers(blackout: 12f, lockdown: 8f, commJam: 6f, evidenceLeak: 4f, evidenceLeakAccumulator: 1.5f, patrolAlert: 2f);
                source.SetGlobalTimers(phaseTimer: 19f, emergencyCooldown: 11f, aiGrace: 3f, elapsed: 123f);
                source.AddVoteRaw(1UL, 2UL);
                source.AddCaseLogRaw("快照案卷");
                source.SetKillCooldownRaw(2UL, 9f);
                source.SetAbilityCooldownRaw(1UL, 5f);
                source.SetVentCooldownRaw(1UL, 4f);
                source.SetBotTimerRaw(9001UL, think: 1.25f, vote: 2.5f, targetPosition: new Vector3(9f, 9f, 0f));

                object snapshot = source.CaptureSnapshot();

                target.RestoreFromSnapshot(snapshot);

                Assert.IsTrue(target.MatchStarted());
                Assert.AreEqual("Voting", target.PhaseName());
                Assert.AreEqual(new Vector3(1f, 2f, 0f), target.PlayerPosition(1UL));
                Assert.IsFalse(target.PlayerAlive(2UL));
                Assert.AreEqual(1, target.TaskCount());
                Assert.AreEqual(1, target.BodyCount());
                Assert.AreEqual(1, target.VoteCount());
                Assert.GreaterOrEqual(target.CaseLogCount(), 2);
                Assert.IsTrue(target.CaseLogContains("快照案卷"));
                Assert.IsTrue(target.CaseLogContains("主机迁移完成"));
                Assert.AreEqual(33, target.PropertyInt("EvidenceScore"));
                Assert.AreEqual(44, target.PropertyInt("EvidenceTarget"));
                Assert.AreEqual(12f, target.PropertyFloat("BlackoutTimer"), 0.001f);
                Assert.AreEqual(8f, target.PropertyFloat("LockdownTimer"), 0.001f);
                Assert.AreEqual(6f, target.PropertyFloat("CommunicationJamTimer"), 0.001f);
                Assert.AreEqual(4f, target.PropertyFloat("EvidenceLeakTimer"), 0.001f);
                Assert.AreEqual(2f, target.PropertyFloat("PatrolAlertTimer"), 0.001f);
                Assert.AreEqual(19f, target.PropertyFloat("PhaseTimer"), 0.001f);
                Assert.AreEqual("主机迁移完成，对局已恢复。", target.PropertyString("Status"));
            }
        }

        [Test]
        public void SnapshotRestore_NullOptionalCollectionsAreTreatedAsEmpty()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetMatchStarted(true);
                source.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                source.SetSingleTask(1, Vector3.one, completed: false, sabotaged: false);
                object snapshot = source.CaptureSnapshot();
                Type snapshotType = snapshot.GetType();

                string[] optionalCollections =
                {
                    "PrivateRoles", "UndercoverStates", "MoleStates", "TaskAssignments",
                    "Bodies", "Votes", "CaseLog", "KillCooldowns", "AbilityCooldowns",
                    "VentCooldowns", "BotThinkTimers", "BotVoteTimers", "BotTargets"
                };
                foreach (string fieldName in optionalCollections)
                {
                    snapshotType.GetField(fieldName).SetValue(snapshot, null);
                }

                Assert.DoesNotThrow(() => target.RestoreFromSnapshot(snapshot),
                    "迁移快照的可选列表缺失时，恢复应退化为空列表而不是崩溃。");
                Assert.AreEqual(1, target.PlayerCount());
                Assert.AreEqual(1, target.TaskCount());
                Assert.AreEqual(0, target.VoteCount());
            }
        }

        [Test]
        public void SnapshotRestore_PreservesMeetingAccusations()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetLocalPreviewMode(true);
                source.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                source.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                source.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");
                source.SetSingleTask(1, Vector3.up, completed: false, sabotaged: false);
                source.SetPhase("Meeting");
                source.ApplyAction(1UL, "Accuse", 3UL);

                object snapshot = source.CaptureSnapshot();
                target.RestoreFromSnapshot(snapshot);

                Assert.AreEqual(1, target.AccusationCount(),
                    "主机迁移快照必须保留当前会议的公开指证记录。");
                Assert.AreEqual(3UL, target.AccusationTarget(1UL));
            }
        }

        [Test]
        public void MeetingAccusation_IsServerAuthoritativeAndDiscussionOnly()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalPreviewMode(true);
                fixture.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                fixture.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                fixture.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");

                fixture.SetPhase("Action");
                fixture.ApplyAction(1UL, "Accuse", 3UL);
                Assert.AreEqual(0, fixture.AccusationCount(), "行动阶段不得提交会议指证。");

                fixture.SetPhase("Meeting");
                fixture.ApplyAction(1UL, "Accuse", 3UL);
                Assert.AreEqual(1, fixture.AccusationCount(), "会议阶段应由服务端记录一次指证。");
                Assert.AreEqual(3UL, fixture.AccusationTarget(1UL));

                fixture.ApplyAction(1UL, "Accuse", 2UL);
                Assert.AreEqual(3UL, fixture.AccusationTarget(1UL), "同一玩家本轮只能提交一次指证。");

                fixture.SetPhase("Voting");
                fixture.ApplyAction(2UL, "Accuse", 3UL);
                Assert.AreEqual(1, fixture.AccusationCount(), "投票阶段不得新增会议指证。");
            }
        }

        [Test]
        public void SnapshotRestore_PreservesMeetingServiceAndAllowsVotingContinuation()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetMatchStarted(true);
                source.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                source.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                source.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");
                source.SetSingleTask(8, Vector3.up, completed: false, sabotaged: false);
                source.BeginMeeting("迁移中的会议");
                source.SetPhase("Voting");
                source.ApplyVote(1UL, 3UL);

                object snapshot = source.CaptureSnapshot();

                target.RestoreFromSnapshot(snapshot);

                Assert.AreEqual("Voting", target.PhaseName());
                Assert.AreEqual(1, target.PropertyInt("MeetingCount"));
                Assert.AreEqual(1, target.MeetingServiceInt("MeetingCount"),
                    "Host migration 快照恢复必须同步 MeetingService 会议次数，避免迁移后服务层读到第 0 场会议。");
                Assert.AreEqual("迁移中的会议", target.MeetingServiceString("CurrentMeetingReason"),
                    "Host migration 快照恢复必须同步 MeetingService 当前会议原因。");
                Assert.IsTrue(target.HasVoted(1UL),
                    "Host migration 快照恢复必须保留已投票状态，后续玩家投票才能完成本轮会议。");

                target.ApplyVote(2UL, 3UL);
                target.ApplyVote(3UL, 3UL);

                Assert.AreEqual("Result", target.PhaseName(), "迁移恢复后剩余玩家继续投票应能正常结算。");
                Assert.IsFalse(target.PlayerAlive(3UL));
                StringAssert.Contains("警方胜利", target.PropertyString("Status"));
            }
        }

        [Test]
        public void SnapshotRestore_ThreeClientPostMigrationTaskMeetingAndVotingFlow()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetMatchStarted(true);
                source.SetPhase("Action");
                source.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                source.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
                source.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");
                source.SetTaskState(9, Vector3.zero, progress: 2, requiredProgress: 3, completed: false, sabotaged: false);
                source.SetTaskServiceEvidence(score: 10, targetValue: 99);

                object snapshot = source.CaptureSnapshot();

                target.RestoreFromSnapshot(snapshot);

                Assert.AreEqual("Action", target.PhaseName());
                Assert.AreEqual(3, target.PlayerCount());
                Assert.AreEqual(2, target.TaskProgress(9));
                Assert.IsFalse(target.TaskCompleted(9));

                target.MarkTaskActive(1UL, 9);
                Assert.IsTrue(target.InvokeBoolOutString("ValidateAndCompleteTask", 1UL, 9, out string taskError), taskError);
                Assert.IsTrue(target.TaskCompleted(9), "迁移后应允许继续完成迁移前已推进的任务。");
                Assert.Greater(target.PropertyInt("EvidenceScore"), 10,
                    "迁移后任务完成仍应推进证据链。");

                target.BeginMeeting("迁移后任务会议");
                target.SetPhase("Voting");
                target.ApplyVote(1UL, 3UL);
                target.ApplyVote(2UL, 3UL);
                target.ApplyVote(3UL, 3UL);

                Assert.AreEqual("Result", target.PhaseName(),
                    "3 客户端迁移恢复后，任务完成、会议和投票应能连续推进到结算。");
                Assert.IsFalse(target.PlayerAlive(3UL));
                StringAssert.Contains("警方胜利", target.PropertyString("Status"));
            }
        }

        [Test]
        public void SnapshotRestore_PreservesPerPlayerTaskAssignments()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetLocalPreviewMode(true);
                source.ConfigureRoom(minPlayers: 8, maxPlayers: 8, autoFillAi: false);
                source.AttachSyncManager();
                target.AttachSyncManager();
                for (ulong clientId = 1UL; clientId <= 8UL; clientId++)
                {
                    source.SetPlayer(clientId, Vector3.zero, alive: true);
                }

                source.StartOnlineMatchCore();
                ulong policeId = source.FindClientIdByPrivateRole("Police");
                int[] expectedTaskIds = source.AssignedTaskIds(policeId);
                CollectionAssert.IsNotEmpty(expectedTaskIds);

                target.RestoreFromSnapshot(source.CaptureSnapshot());

                CollectionAssert.AreEquivalent(expectedTaskIds, target.AssignedTaskIds(policeId));
            }
        }

        [Test]
        public void SnapshotRestore_PreservesIndependentProgressAtSharedTaskStation()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetPhase("Action");
                source.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                source.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
                source.SetPlayer(2UL, Vector3.zero, alive: true, roleName: "Police");
                source.SetPlayer(3UL, Vector3.right, alive: true, roleName: "Gang");
                source.AttachSyncManager();
                target.AttachSyncManager();
                source.AssignTask(1UL, 0);
                source.AssignTask(2UL, 0);

                source.MarkTaskActive(1UL, 0);
                Assert.IsTrue(
                    source.InvokeBoolOutString("ValidateAndCompleteTask", 1UL, 0, out string completeError),
                    completeError);

                target.RestoreFromSnapshot(source.CaptureSnapshot());

                Assert.IsTrue(target.PlayerTaskCompleted(1UL, 0));
                Assert.IsFalse(target.PlayerTaskCompleted(2UL, 0),
                    "主机迁移不能把共享任务站上其他玩家的个人任务一并完成。");
            }
        }

        [Test]
        public void SnapshotRestore_PreservesDoubleAgentIdentityProgress()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                source.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Mole", professionName: "Mole");
                source.SetPlayer(3UL, Vector3.left, alive: true, roleName: "Police");
                source.SetPlayer(4UL, Vector3.up, alive: true, roleName: "Gang");
                source.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                source.AccumulateUndercoverIntel(1UL, 2);
                source.AccumulateUndercoverIntel(1UL, 2);
                Assert.IsTrue(source.ExecuteBetrayal(1UL));
                source.AccumulateMoleIntel(2UL, 5);
                Assert.AreEqual(1UL, source.AssignMoleHit(2UL));
                source.SetMoleObjective(2UL, kills: 1, sabotages: 2, survivedTilLate: true);

                target.RestoreFromSnapshot(source.CaptureSnapshot());

                Assert.AreEqual(4, target.GetUndercoverIntel(1UL));
                Assert.IsTrue(target.HasBetrayed(1UL));
                Assert.AreEqual(5, target.GetMoleIntel(2UL));
                Assert.AreEqual(1UL, target.MoleHitTarget(2UL));
                Assert.AreEqual(1, target.MoleObjectiveInt(2UL, "Kills"));
                Assert.AreEqual(2, target.MoleObjectiveInt(2UL, "Sabotages"));
                Assert.IsTrue(target.MoleObjectiveBool(2UL, "SurvivedTilLate"));
            }
        }

        [Test]
        public void SnapshotRestore_PreservesLocalIdentityWhenMigrationSnapshotOmitsIt()
        {
            using (ControllerFixture source = new ControllerFixture())
            using (ControllerFixture target = new ControllerFixture())
            {
                source.SetMatchStarted(true);
                source.SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);
                source.SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");

                target.SetLocalRole("Undercover");
                target.SetPlayer(0UL, Vector3.zero, alive: true, roleName: "Undercover", professionName: "UndercoverAgent");
                target.AccumulateUndercoverIntel(0UL, 3);

                target.RestoreFromSnapshot(source.CaptureSnapshot());

                Assert.AreEqual(3, target.GetUndercoverIntel(0UL),
                    "迁移快照未包含本机私密条目时，客户端仍必须保留本机卧底情报。");
                Assert.AreEqual("Undercover", target.LocalRoleName(),
                    "恢复公开快照不能覆盖客户端已经收到的本机隐藏身份。");
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
        public void ClientState_DeadPlayerCanSendGhostMovementDuringAction()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetPhase("Action");
                fixture.SetPlayer(7UL, new Vector3(2f, 3f, 0f), alive: false);

                fixture.ApplyClientState(7UL, new Vector3(50f, 50f, 0f), new Vector2(12f, 0f), true);

                Assert.AreEqual(new Vector2(1f, 0f), fixture.PlayerInput(7UL),
                    "鬼魂移动输入应进入服务器模拟，位置仍由服务器权威推进。");
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
        public void IdentityProgress_UpdatesOnlyMatchingHiddenRoleFromServer()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalRole("Undercover");
                fixture.ReceiveIdentityProgress(
                    7UL, "Undercover", intel: 9, missionsDone: 9, betrayed: true,
                    exposed: false, kills: 0, sabotages: 0, survivedTilLate: false);
                Assert.AreEqual(0, fixture.GetUndercoverIntel(NetworkManager.ServerClientId));
                Assert.IsFalse(fixture.HasBetrayed(NetworkManager.ServerClientId));

                fixture.ReceiveIdentityProgress(
                    NetworkManager.ServerClientId, "Mole", intel: 5, missionsDone: 0, betrayed: false,
                    exposed: true, kills: 1, sabotages: 2, survivedTilLate: true);
                Assert.AreEqual(0, fixture.GetUndercoverIntel(NetworkManager.ServerClientId));

                fixture.ReceiveIdentityProgress(
                    NetworkManager.ServerClientId, "Undercover", intel: 4, missionsDone: 2, betrayed: true,
                    exposed: false, kills: 0, sabotages: 0, survivedTilLate: false);
                Assert.AreEqual(4, fixture.GetUndercoverIntel(NetworkManager.ServerClientId));
                Assert.IsTrue(fixture.HasBetrayed(NetworkManager.ServerClientId));
            }
        }

        [Test]
        public void IdentityProgress_UpdatesMolePrivateObjectivesFromServer()
        {
            using (ControllerFixture fixture = new ControllerFixture())
            {
                fixture.SetLocalRole("Mole");

                fixture.ReceiveIdentityProgress(
                    NetworkManager.ServerClientId, "Mole", intel: 5, missionsDone: 0, betrayed: false,
                    exposed: true, kills: 1, sabotages: 2, survivedTilLate: true);

                Assert.AreEqual(5, fixture.GetMoleIntel(NetworkManager.ServerClientId));
                Assert.IsTrue(fixture.IsMoleExposed(NetworkManager.ServerClientId));
                Assert.AreEqual(1, fixture.MoleObjectiveInt(NetworkManager.ServerClientId, "Kills"));
                Assert.AreEqual(2, fixture.MoleObjectiveInt(NetworkManager.ServerClientId, "Sabotages"));
                Assert.IsTrue(fixture.MoleObjectiveBool(NetworkManager.ServerClientId, "SurvivedTilLate"));
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
        public void WorldBuilder_UsesRuntimeMapPropSpritesForServiceDressing()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            Type mapServiceType = RuntimeType("GanglandUndercover.Online.OnlineMapService");
            GameObject worldRoot = new GameObject("WorldBuilderRuntimeMapPropRegressionRoot");
            GameObject mapHost = new GameObject("WorldBuilderRuntimeMapPropRegressionMapService");

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

                Assert.GreaterOrEqual(PropertyInt(builder, "RuntimeMapPropSpriteElementCount"), 20);
                Assert.GreaterOrEqual(CountChildrenStartingWith(worldRoot.transform, "地图小件 "), 20);
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
        public void Sprite2DAssetCache_LoadsRuntimeMapPropSpritesByExplicitPath()
        {
            Type cacheType = RuntimeType("GanglandUndercover.Art.Sprite2DAssetCache");

            InvokeStatic(cacheType, "Ensure");

            Assert.AreEqual("Sprites/Tilesets/Harbour/props/tile_crate_wood", StaticPropertyString(cacheType, "PropCrateResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/Harbour/props/tile_barrel_oil", StaticPropertyString(cacheType, "PropBarrelResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/Harbour/props/tile_vent_backalley", StaticPropertyString(cacheType, "VentIconResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/KowloonWalledCity/props/tile_crate_old", StaticPropertyString(cacheType, "KowloonPropCrateResourcePath"));
            Assert.AreEqual("Sprites/Tilesets/KowloonWalledCity/props/tile_vent_rust", StaticPropertyString(cacheType, "KowloonVentIconResourcePath"));
        }

        [Test]
        public void Sprite2DAssetCache_CropsLimeZu16AtlasesToOneCell()
        {
            Type cacheType = RuntimeType("GanglandUndercover.Art.Sprite2DAssetCache");
            InvokeStatic(cacheType, "Ensure");

            string[] spriteFields = { "FloorTileAlt", "WallBlock", "PropDesk", "PropCabinet", "PropEvidenceBox" };
            foreach (string fieldName in spriteFields)
            {
                Sprite sprite = (Sprite)cacheType.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Assert.IsNotNull(sprite, fieldName + " should be loaded from the LimeZu atlas");
                Assert.AreEqual(16f, sprite.rect.width, 0.001f, fieldName + " must use one 16px atlas cell");
                Assert.AreEqual(16f, sprite.rect.height, 0.001f, fieldName + " must use one 16px atlas cell");
                Assert.AreEqual(16f, sprite.pixelsPerUnit, 0.001f, fieldName + " PPU must match the cell size");
            }
        }

        [Test]
        public void WorldBuilder_SortsPositiveZInFrontOfNegativeZ()
        {
            Type builderType = RuntimeType("GanglandUndercover.Online.OnlineWorldBuilder");
            MethodInfo sorting = builderType.GetMethod("SortingOrderForZ",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo localSorting = builderType.GetMethod("SortingOrderForLocalZ",
                BindingFlags.Public | BindingFlags.Static);

            Assert.AreEqual(-500, Convert.ToInt32(sorting.Invoke(null, new object[] { -0.5f })));
            Assert.AreEqual(500, Convert.ToInt32(sorting.Invoke(null, new object[] { 0.5f })));
            Assert.AreEqual(-250, Convert.ToInt32(localSorting.Invoke(null, new object[] { -0.25f })));
            Assert.AreEqual(250, Convert.ToInt32(localSorting.Invoke(null, new object[] { 0.25f })));
        }

        [Test]
        public void VFXSheetPlayer_LoadsEveryRuntimeSheetWithExpectedFirstFrameSize()
        {
            Type playerType = RuntimeType("GanglandUndercover.Art.VFXSheetPlayer");
            Type playModeType = RuntimeType("GanglandUndercover.Art.VFXSheetPlayer+PlayMode");
            object oneShotMode = Enum.Parse(playModeType, "OneShot");
            string[] effects =
            {
                "blackout",
                "comms_jam",
                "door_lock",
                "emergency_light",
                "evidence_leak",
                "hit",
                "kill",
                "patrol_alert"
            };
            int[] expectedSizes = { 96, 64, 48, 48, 48, 32, 128, 64 };

            for (int i = 0; i < effects.Length; i++)
            {
                GameObject host = new GameObject("VFXSheetPlayerRegression_" + effects[i]);
                try
                {
                    object player = host.AddComponent(playerType);
                    bool initialized = (bool)playerType.GetMethod("Init")
                        .Invoke(player, new object[] { effects[i], oneShotMode, 12f });
                    playerType.GetMethod("Play").Invoke(player, null);

                    SpriteRenderer renderer = host.GetComponent<SpriteRenderer>();
                    Assert.IsTrue(initialized, "Missing runtime VFX sheet: " + effects[i]);
                    Assert.IsNotNull(renderer.sprite, effects[i] + " should assign the first frame on Play.");
                    Assert.AreEqual(expectedSizes[i], renderer.sprite.texture.width, effects[i] + " width.");
                    Assert.AreEqual(expectedSizes[i], renderer.sprite.texture.height, effects[i] + " height.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        [Test]
        public void SabotageVFX_UsesAuthoredEmergencyLightSheet()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            Type vfxType = RuntimeType("GanglandUndercover.Art.SabotageVFX");
            Type playerType = RuntimeType("GanglandUndercover.Art.VFXSheetPlayer");
            GameObject controllerHost = new GameObject("SabotageVFXControllerRegressionHost");
            GameObject vfxHost = new GameObject("SabotageVFXRegressionHost");

            try
            {
                object controller = controllerHost.AddComponent(controllerType);
                object vfx = vfxHost.AddComponent(vfxType);

                vfxType.GetMethod("Bind").Invoke(vfx, new[] { controller });

                SpriteRenderer blackout = (SpriteRenderer)vfxType.GetField("blackoutOverlay").GetValue(vfx);
                Assert.IsNotNull(blackout, "Blackout overlay should be created when the VFX system is bound.");

                Transform emergencyLight = blackout.transform.Find("EmergencyRedLight");
                Assert.IsNotNull(emergencyLight, "Blackout overlay should include an emergency light child.");

                Component sheetPlayer = emergencyLight.GetComponent(playerType);
                SpriteRenderer emergencyRenderer = emergencyLight.GetComponent<SpriteRenderer>();

                Assert.IsNotNull(sheetPlayer, "Emergency light should use the authored runtime frame sheet.");
                Assert.IsTrue(PropertyBool(sheetPlayer, "HasFrames"), "Emergency light should load emergency_light frames.");
                Assert.IsNotNull(emergencyRenderer.sprite, "Emergency light should assign its first authored frame.");
                Assert.AreEqual(48, emergencyRenderer.sprite.texture.width);
                Assert.AreEqual(48, emergencyRenderer.sprite.texture.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vfxHost);
                UnityEngine.Object.DestroyImmediate(controllerHost);
            }
        }

        [Test]
        public void SabotageVFX_LoadsTunedOverlayMotionProfiles()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            Type vfxType = RuntimeType("GanglandUndercover.Art.SabotageVFX");
            Type playerType = RuntimeType("GanglandUndercover.Art.VFXSheetPlayer");
            GameObject controllerHost = new GameObject("SabotageVFXProfilesControllerRegressionHost");
            GameObject vfxHost = new GameObject("SabotageVFXProfilesRegressionHost");

            try
            {
                object controller = controllerHost.AddComponent(controllerType);
                object vfx = vfxHost.AddComponent(vfxType);

                vfxType.GetMethod("Bind").Invoke(vfx, new[] { controller });

                SpriteRenderer blackout = (SpriteRenderer)vfxType.GetField("blackoutOverlay").GetValue(vfx);
                SpriteRenderer lockdown = (SpriteRenderer)vfxType.GetField("lockdownOverlay").GetValue(vfx);
                SpriteRenderer commJam = (SpriteRenderer)vfxType.GetField("commJamOverlay").GetValue(vfx);
                SpriteRenderer evidenceLeak = (SpriteRenderer)vfxType.GetField("evidenceLeakOverlay").GetValue(vfx);
                SpriteRenderer patrolAlert = (SpriteRenderer)vfxType.GetField("patrolAlertOverlay").GetValue(vfx);
                SpriteRenderer emergencyLight = blackout.transform.Find("EmergencyRedLight").GetComponent<SpriteRenderer>();

                AssertVfxProfile(blackout, playerType, expectedFrames: 12, expectedFps: 6f, expectedSorting: 500);
                AssertVfxProfile(lockdown, playerType, expectedFrames: 6, expectedFps: 10f, expectedSorting: 501);
                AssertVfxProfile(commJam, playerType, expectedFrames: 8, expectedFps: 14f, expectedSorting: 502);
                AssertVfxProfile(evidenceLeak, playerType, expectedFrames: 12, expectedFps: 9f, expectedSorting: 499);
                AssertVfxProfile(patrolAlert, playerType, expectedFrames: 4, expectedFps: 6f, expectedSorting: 503);
                AssertVfxProfile(emergencyLight, playerType, expectedFrames: 8, expectedFps: 12f, expectedSorting: 505);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vfxHost);
                UnityEngine.Object.DestroyImmediate(controllerHost);
            }
        }

        [Test]
        public void SabotageVFX_TriggerKillBloodAddsAuthoredHitImpactSheet()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            Type vfxType = RuntimeType("GanglandUndercover.Art.SabotageVFX");
            Type playerType = RuntimeType("GanglandUndercover.Art.VFXSheetPlayer");
            GameObject controllerHost = new GameObject("SabotageVFXHitControllerRegressionHost");
            GameObject vfxHost = new GameObject("SabotageVFXHitRegressionHost");

            try
            {
                object controller = controllerHost.AddComponent(controllerType);
                object vfx = vfxHost.AddComponent(vfxType);

                vfxType.GetMethod("Bind").Invoke(vfx, new[] { controller });
                vfxType.GetMethod("TriggerKillBlood").Invoke(vfx, new object[] { new Vector3(2f, 3f, 0f) });

                GameObject kill = (GameObject)vfxType.GetField("killBloodFX").GetValue(vfx);
                GameObject hit = FindObjectNamedIncludingInactive("HitImpactFX");

                Assert.IsNotNull(kill, "Kill trigger should create the authored kill sheet object.");
                Assert.IsNotNull(hit, "Kill trigger should add a separate authored hit impact sheet.");
                Assert.IsNotNull(kill.GetComponent(playerType), "KillBloodFX should use the kill sheet.");
                Assert.IsNotNull(hit.GetComponent(playerType), "HitImpactFX should use the hit sheet.");
                Assert.AreEqual(10, PropertyInt(kill.GetComponent(playerType), "FrameCount"));
                Assert.AreEqual(4, PropertyInt(hit.GetComponent(playerType), "FrameCount"));
                Assert.AreEqual(15f, PropertyFloat(kill.GetComponent(playerType), "FramesPerSecond"), 0.001f);
                Assert.AreEqual(18f, PropertyFloat(hit.GetComponent(playerType), "FramesPerSecond"), 0.001f);
                Assert.AreEqual(new Vector3(2f, 3f, -1f), kill.transform.position);
                Assert.AreEqual(new Vector3(2f, 3f, -1.05f), hit.transform.position);
            }
            finally
            {
                DestroyAllObjectsNamed("HitImpactFX");
                UnityEngine.Object.DestroyImmediate(vfxHost);
                UnityEngine.Object.DestroyImmediate(controllerHost);
            }
        }

        [Test]
        public void KillSystem_PlayKillEffectsUsesAuthoredSabotageVFXSheets()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            Type vfxType = RuntimeType("GanglandUndercover.Art.SabotageVFX");
            Type playerType = RuntimeType("GanglandUndercover.Art.VFXSheetPlayer");
            GameObject controllerHost = new GameObject("KillSystemVFXIntegrationRegressionHost");

            try
            {
                object controller = controllerHost.AddComponent(controllerType);
                controllerType
                    .GetMethod("EnsureVFX", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                object killSystem = controllerType
                    .GetField("killSystem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetValue(controller);
                object vfx = controllerType
                    .GetField("sabotageVFX", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetValue(controller);

                Assert.IsNotNull(killSystem, "OnlineMatchController should bind KillSystem during Awake.");
                Assert.IsNotNull(vfx, "OnlineMatchController should bind SabotageVFX during Awake.");

                killSystem.GetType()
                    .GetMethod("PlayKillEffects", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(killSystem, new object[] { new Vector3(4f, 5f, 0f), 9001UL });

                GameObject kill = (GameObject)vfxType.GetField("killBloodFX").GetValue(vfx);
                GameObject hit = FindObjectNamedIncludingInactive("HitImpactFX");

                Assert.IsNotNull(kill, "KillSystem should route kill blood through the authored SabotageVFX sheet.");
                Assert.IsNotNull(hit, "KillSystem should add the authored hit impact sheet through SabotageVFX.");
                Assert.IsNull(FindObjectNamedIncludingInactive("KillBloodEffect"),
                    "KillSystem should not create the old procedural blood fallback when authored VFX is available.");
                Assert.IsNotNull(kill.GetComponent(playerType), "KillBloodFX should use the authored kill sheet.");
                Assert.IsNotNull(hit.GetComponent(playerType), "HitImpactFX should use the authored hit sheet.");
                Assert.AreEqual(10, PropertyInt(kill.GetComponent(playerType), "FrameCount"));
                Assert.AreEqual(4, PropertyInt(hit.GetComponent(playerType), "FrameCount"));
                Assert.AreEqual(new Vector3(4f, 5f, -1f), kill.transform.position);
                Assert.AreEqual(new Vector3(4f, 5f, -1.05f), hit.transform.position);
            }
            finally
            {
                DestroyAllObjectsNamed("HitImpactFX");
                DestroyAllObjectsNamed("KillBloodFX");
                DestroyAllObjectsNamed("KillBloodEffect");
                DestroyAllObjectsNamed("KillFlashCanvas");
                UnityEngine.Object.DestroyImmediate(controllerHost);
            }
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
        public void OnlineMatchHud_ActionPreviewUsesCompactMissionAndCommandLayout()
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            GameObject host = new GameObject("OnlineHudCompactActionRegression_OnlineMatchController");

            try
            {
                object controller = host.AddComponent(controllerType);

                Assert.IsTrue((bool)Invoke(controller, "EditorForceActionPreviewForSmokeTest"));
                Assert.IsTrue(PropertyBool(controller, "CanvasHudLayoutComplete"));
                Assert.IsTrue(PropertyBool(controller, "CompactActionHudActive"));
                Assert.AreEqual(6, PropertyInt(controller, "CompactActionCommandSlotCount"));
                Assert.GreaterOrEqual(PropertyInt(controller, "ChatPanelCanvasElementCount"), 3);
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
                Assert.GreaterOrEqual(PropertyInt(controller, "MeetingAccusationButtonCount"), PropertyInt(controller, "AlivePlayerCount") - 1,
                    "讨论阶段必须提供面向每位其他存活玩家的显式指证入口。");
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

        private static object CreateWardrobeItem(
            Type itemType,
            Type partType,
            Type rarityType,
            string id,
            string partName,
            string rarityName)
        {
            object item = Activator.CreateInstance(itemType);
            itemType.GetField("id").SetValue(item, id);
            itemType.GetField("displayName").SetValue(item, id);
            itemType.GetField("part").SetValue(item, Enum.Parse(partType, partName));
            itemType.GetField("iconPath").SetValue(item, string.Empty);
            itemType.GetField("rarity").SetValue(item, Enum.Parse(rarityType, rarityName));
            itemType.GetField("unlockedByDefault").SetValue(item, true);
            itemType.GetField("scaleFactor").SetValue(item, 1f);
            return item;
        }

        private static void InvokeStartWatching(Type cameraType, object camera, ulong senderClientId)
        {
            RpcParams rpcParams = new RpcParams
            {
                Receive = new RpcReceiveParams
                {
                    SenderClientId = senderClientId
                }
            };

            cameraType.GetMethod("HandleStartWatchingRequest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(camera, new object[] { rpcParams.Receive.SenderClientId });
        }

        private static bool CameraWatcherContains(object camera, ulong clientId)
        {
            object watchers = camera.GetType()
                .GetField("_watchingPlayers", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(camera);

            return (bool)watchers.GetType()
                .GetMethod("Contains")
                .Invoke(watchers, new object[] { clientId });
        }

        private static object GetRoleDistribution(int playerCount)
        {
            return Invoke(CreateRuleSet(), "GetRoleDistribution", playerCount);
        }

        private static void AssertAlphaPacingEnvelope(
            object ruleSet,
            int playerCount,
            int expectedGang,
            int expectedUndercover,
            int expectedMole,
            int expectedTasks,
            int expectedEvidenceTarget,
            float expectedKillCooldown,
            float expectedMeetingWindow,
            float expectedEmergencyCooldown)
        {
            object dist = Invoke(ruleSet, "GetRoleDistribution", playerCount);
            int gang = FieldInt(dist, "gang");
            int undercover = FieldInt(dist, "undercover");
            int mole = FieldInt(dist, "mole");

            Assert.AreEqual(expectedGang, gang, playerCount + " 人局黑帮人数应符合 Alpha 预设。");
            Assert.AreEqual(expectedUndercover, undercover, playerCount + " 人局卧底人数应符合 Alpha 预设。");
            Assert.AreEqual(expectedMole, mole, playerCount + " 人局内鬼人数应符合 Alpha 预设。");
            Assert.AreEqual(expectedTasks, InvokeInt(ruleSet, "TotalTaskCount", playerCount, gang),
                playerCount + " 人局总任务数应随非黑帮人数缩放。");
            Assert.AreEqual(expectedEvidenceTarget, InvokeInt(ruleSet, "ScaledEvidenceTarget", playerCount),
                playerCount + " 人局证据目标应保持 10-20 分钟节奏。");
            Assert.AreEqual(expectedKillCooldown, InvokeFloat(ruleSet, "KillCooldownFor", playerCount), 0.001f,
                playerCount + " 人局击杀冷却应落在 Alpha 节奏窗口。");

            float meetingWindow = InvokeFloat(ruleSet, "MeetingIntroSecondsFor", playerCount)
                + InvokeFloat(ruleSet, "VotingSecondsFor", playerCount);
            Assert.AreEqual(expectedMeetingWindow, meetingWindow, 0.001f,
                playerCount + " 人局会议+投票总窗口应受控。");
            Assert.LessOrEqual(meetingWindow, 95f,
                playerCount + " 人局单次会议窗口不能超过 95 秒。");

            Assert.AreEqual(expectedEmergencyCooldown, InvokeFloat(ruleSet, "EmergencyCooldownSecondsFor", playerCount), 0.001f,
                playerCount + " 人局紧急会议冷却应符合节奏预设。");
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

        private static object BuildRelayMigrationLobbySessionOptions(
            string roomName,
            int maxPlayers,
            string relayCode,
            string mapName,
            string ruleSummary)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return StaticNonPublic(controllerType, "BuildRelayMigrationLobbySessionOptions").Invoke(null, new object[]
            {
                roomName,
                maxPlayers,
                relayCode,
                mapName,
                ruleSummary
            });
        }

        private static bool IsHostMigrationRelayCandidate(
            string expectedRoomName,
            string candidateRoomName,
            string relayCode,
            int playerCount,
            int maxPlayers,
            bool isLocked,
            bool hasPassword,
            string migrationValue)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return (bool)StaticNonPublic(controllerType, "IsHostMigrationRelayCandidate").Invoke(null, new object[]
            {
                expectedRoomName,
                candidateRoomName,
                relayCode,
                playerCount,
                maxPlayers,
                isLocked,
                hasPassword,
                migrationValue
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

        private static object BuildHostMigrationRelayRoomSessionJoin(
            bool hasDisconnectedNetworkSession,
            string expectedRoomName,
            string sessionId,
            string candidateRoomName,
            string relayCode,
            int playerCount,
            int maxPlayers,
            bool isLocked,
            bool hasPassword,
            bool isHostMigration,
            bool allowLocalPreview)
        {
            Type controllerType = RuntimeType("GanglandUndercover.Online.OnlineMatchController");
            return StaticNonPublic(controllerType, "BuildHostMigrationRelayRoomSessionJoin").Invoke(null, new object[]
            {
                hasDisconnectedNetworkSession,
                expectedRoomName,
                sessionId,
                candidateRoomName,
                relayCode,
                playerCount,
                maxPlayers,
                isLocked,
                hasPassword,
                isHostMigration,
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

        private static object MakePlayersWithEliminatedUndercover(params (int Count, string RoleName)[] groups)
        {
            object players = MakePlayers(groups);
            Type dictType = players.GetType();
            PropertyInfo item = dictType.GetProperty("Item");

            foreach (KeyValuePair<ulong, int> role in RoleLookup)
            {
                if (role.Value != Convert.ToInt32(Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Undercover")))
                {
                    continue;
                }

                object state = item.GetValue(players, new object[] { role.Key });
                state.GetType().GetField("Alive").SetValue(state, false);
                item.SetValue(players, state, new object[] { role.Key });
            }

            return players;
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

        private static float FieldFloat(object target, string fieldName)
        {
            return Convert.ToSingle(target.GetType().GetField(fieldName).GetValue(target));
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

        private static float PropertyFloat(object target, string propertyName)
        {
            return Convert.ToSingle(target.GetType().GetProperty(propertyName).GetValue(target));
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

        private static void AssertVfxProfile(SpriteRenderer renderer, Type playerType, int expectedFrames, float expectedFps, int expectedSorting)
        {
            Assert.IsNotNull(renderer, "Expected a SpriteRenderer for the VFX profile.");
            Component player = renderer.GetComponent(playerType);
            Assert.IsNotNull(player, renderer.name + " should use VFXSheetPlayer.");
            Assert.AreEqual(expectedFrames, PropertyInt(player, "FrameCount"), renderer.name + " frame count.");
            Assert.AreEqual(expectedFps, PropertyFloat(player, "FramesPerSecond"), 0.001f, renderer.name + " FPS.");
            Assert.AreEqual(expectedSorting, renderer.sortingOrder, renderer.name + " sorting order.");
            Assert.IsNotNull(renderer.sprite, renderer.name + " should have an authored first frame assigned.");
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

        private static void InvokeNonPublicInstance(object target, string methodName)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }

        private static void SetNonPublicField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static int ServiceVoteCount(object service)
        {
            object votes = service.GetType().GetProperty("Votes").GetValue(service);
            return Convert.ToInt32(votes.GetType().GetProperty("Count").GetValue(votes));
        }

        private sealed class EventProbe
        {
            public int MeetingCalledCount { get; private set; }
            public ulong LastMeetingCallerId { get; private set; }
            public bool LastMeetingCalledIsEmergency { get; private set; }
            public int BodyReportedCount { get; private set; }
            public ulong LastBodyReporterId { get; private set; }
            public ulong LastBodyVictimId { get; private set; }

            public Delegate CreateHandler(string methodName, Type eventType)
            {
                MethodInfo method = GetType().GetMethod(methodName).MakeGenericMethod(eventType);
                Type actionType = typeof(Action<>).MakeGenericType(eventType);
                return Delegate.CreateDelegate(actionType, this, method);
            }

            public void OnMeetingCalled<T>(T evt) where T : struct
            {
                object boxedEvent = evt;
                Type eventType = boxedEvent.GetType();
                MeetingCalledCount++;
                LastMeetingCallerId = Convert.ToUInt64(eventType.GetField("CallerId").GetValue(boxedEvent));
                LastMeetingCalledIsEmergency = Convert.ToBoolean(eventType.GetField("IsEmergency").GetValue(boxedEvent));
            }

            public void OnBodyReported<T>(T evt) where T : struct
            {
                object boxedEvent = evt;
                Type eventType = boxedEvent.GetType();
                BodyReportedCount++;
                LastBodyReporterId = Convert.ToUInt64(eventType.GetField("ReporterId").GetValue(boxedEvent));
                LastBodyVictimId = Convert.ToUInt64(eventType.GetField("VictimId").GetValue(boxedEvent));
            }
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
            private readonly Type actionType = RuntimeType("GanglandUndercover.Online.OnlineActionType");
            private readonly Type sabotageType = RuntimeType("GanglandUndercover.SabotageType");
            private readonly Type mapType = RuntimeType("GanglandUndercover.Online.OnlineMapService+OnlineMapType");
            private readonly Type bodyStateType = RuntimeType("GanglandUndercover.Online.OnlineBodyState");
            private readonly Type snapshotType = RuntimeType("GanglandUndercover.Online.GameStateSnapshot");

            private readonly GameObject host;
            private readonly object controller;

            public object Controller => controller;

            public ControllerFixture()
            {
                host = new GameObject("SecurityRegression_OnlineMatchController");
                // Sprite2DAssetCache 已在 [OneTimeSetUp] 中预热，
                // AddComponent → Reset() → Ensure() 不会产生 Debug.Log。
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

            public void EnsureServices()
            {
                EnsureSnapshotDependencies();
            }

            public void DisableVotingService()
            {
                Behaviour service = GetField("votingService") as Behaviour;
                if (service != null)
                {
                    service.enabled = false;
                }
                SetField("votingService", null);
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

            public void SetPlayer(
                ulong clientId,
                Vector3 position,
                bool alive,
                string roleName = "Police",
                string professionName = null,
                bool isBot = false)
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
                    isBot);

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

            public void SetPlayerAlive(ulong clientId, bool alive)
            {
                object state = GetPlayerState(clientId);
                playerStateType.GetField("Alive").SetValue(state, alive);
                SetPlayerState(clientId, state);
            }

            public void SetPlayerPosition(ulong clientId, Vector3 position)
            {
                object state = GetPlayerState(clientId);
                playerStateType.GetField("Position").SetValue(state, position);
                SetPlayerState(clientId, state);
            }

            public bool CanClientWatchCamera(ulong clientId, Vector2 cameraCenter)
            {
                return (bool)controllerType.GetMethod("CanClientWatchCamera")
                    .Invoke(controller, new object[] { clientId, cameraCenter });
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
                    .Invoke(controller, new object[] { reason, 0UL, false });
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

            public void ApplyAction(ulong senderClientId, string actionName, ulong targetClientId)
            {
                object action = Enum.Parse(actionType, actionName);
                controllerType.GetMethod("ApplyClientAction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new[] { (object)senderClientId, action, (object)targetClientId });
            }

            public int AccusationCount()
            {
                object accusationTargets = controllerType.GetProperty("AccusationTargets").GetValue(controller);
                return Convert.ToInt32(accusationTargets.GetType().GetProperty("Count").GetValue(accusationTargets));
            }

            public ulong AccusationTarget(ulong accuserClientId)
            {
                object accusationTargets = controllerType.GetProperty("AccusationTargets").GetValue(controller);
                return (ulong)accusationTargets.GetType().GetProperty("Item").GetValue(
                    accusationTargets, new object[] { accuserClientId });
            }

            public ulong VoteTarget(ulong voterClientId)
            {
                object voteMap = GetField("votes");
                return (ulong)voteMap.GetType().GetProperty("Item").GetValue(voteMap, new object[] { voterClientId });
            }

            public void AccumulateMoleIntel(ulong moleId, int amount)
            {
                controllerType.GetMethod("AccumulateMoleIntel").Invoke(controller, new object[] { moleId, amount });
            }

            public ulong AssignMoleHit(ulong moleId)
            {
                object value = controllerType.GetMethod("AssignMoleHit").Invoke(controller, new object[] { moleId });
                Assert.IsNotNull(value, "内鬼情报达标后必须获得卧底目标。");
                return Convert.ToUInt64(value);
            }

            public bool CheckMoleWinCondition(ulong moleId)
            {
                return (bool)controllerType.GetMethod("CheckMoleWinCondition")
                    .Invoke(controller, new object[] { moleId });
            }

            public bool CanKillTarget(ulong attackerId, ulong targetId)
            {
                return (bool)controllerType.GetMethod("CanKillTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { attackerId, targetId });
            }

            public float KillCooldown(ulong clientId)
            {
                EnsureSnapshotDependencies();
                object killSystem = GetField("killSystem");
                object cooldowns = killSystem.GetType().GetField("killCooldowns", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(killSystem);
                bool contains = (bool)cooldowns.GetType().GetMethod("ContainsKey").Invoke(cooldowns, new object[] { clientId });
                return contains
                    ? Convert.ToSingle(cooldowns.GetType().GetProperty("Item").GetValue(cooldowns, new object[] { clientId }))
                    : 0f;
            }

            public void InteractWithTask(ulong clientId)
            {
                object state = GetPlayerState(clientId);
                controllerType.GetMethod("TryInteractWithTask", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .Invoke(controller, new[] { (object)clientId, state });
            }

            public int GetUndercoverIntel(ulong clientId)
            {
                return Convert.ToInt32(controllerType.GetMethod("GetUndercoverIntel").Invoke(controller, new object[] { clientId }));
            }

            public int GetMoleIntel(ulong clientId)
            {
                return Convert.ToInt32(controllerType.GetMethod("GetMoleIntel").Invoke(controller, new object[] { clientId }));
            }

            public void AccumulateUndercoverIntel(ulong clientId, int amount)
            {
                controllerType.GetMethod("AccumulateUndercoverIntel")
                    .Invoke(controller, new object[] { clientId, amount });
            }

            public bool ExecuteBetrayal(ulong clientId)
            {
                return (bool)controllerType.GetMethod("ExecuteBetrayal")
                    .Invoke(controller, new object[] { clientId });
            }

            public bool HasBetrayed(ulong clientId)
            {
                return (bool)controllerType.GetMethod("HasBetrayed")
                    .Invoke(controller, new object[] { clientId });
            }

            public ulong MoleHitTarget(ulong clientId)
            {
                object target = controllerType.GetMethod("GetMoleHitTarget")
                    .Invoke(controller, new object[] { clientId });
                return Convert.ToUInt64(target);
            }

            public void SetMoleObjective(ulong clientId, int kills, int sabotages, bool survivedTilLate)
            {
                Type objectiveType = RuntimeType("GanglandUndercover.Online.MoleObjective");
                object objective = Activator.CreateInstance(objectiveType);
                objectiveType.GetField("Kills").SetValue(objective, kills);
                objectiveType.GetField("Sabotages").SetValue(objective, sabotages);
                objectiveType.GetField("SurvivedTilLate").SetValue(objective, survivedTilLate);
                object objectives = GetField("_moleObjectives");
                objectives.GetType().GetProperty("Item").SetValue(objectives, objective, new object[] { clientId });
            }

            public int MoleObjectiveInt(ulong clientId, string fieldName)
            {
                object objective = MoleObjective(clientId);
                return Convert.ToInt32(objective.GetType().GetField(fieldName).GetValue(objective));
            }

            public bool MoleObjectiveBool(ulong clientId, string fieldName)
            {
                object objective = MoleObjective(clientId);
                return Convert.ToBoolean(objective.GetType().GetField(fieldName).GetValue(objective));
            }

            private object MoleObjective(ulong clientId)
            {
                object objectives = GetField("_moleObjectives");
                return objectives.GetType().GetProperty("Item").GetValue(objectives, new object[] { clientId });
            }

            public bool HasMoleHitTarget(ulong clientId)
            {
                object target = controllerType.GetMethod("GetMoleHitTarget")
                    .Invoke(controller, new object[] { clientId });
                return target != null;
            }

            public int CollectionCount(string fieldName)
            {
                object collection = GetField(fieldName);
                return Convert.ToInt32(collection.GetType().GetProperty("Count").GetValue(collection));
            }

            public void ApplyClientAction(ulong senderClientId, string actionName, ulong targetClientId)
            {
                controllerType.GetMethod("ApplyClientAction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new[]
                    {
                        (object)senderClientId,
                        Enum.Parse(actionType, actionName),
                        targetClientId,
                    });
            }

            public void RequestAction(string actionName)
            {
                controllerType.GetMethod("RequestAction", new[] { actionType })
                    .Invoke(controller, new[] { Enum.Parse(actionType, actionName) });
            }

            public ulong SabotageValue(string sabotageName)
            {
                return Convert.ToUInt64(Enum.Parse(sabotageType, sabotageName));
            }

            public float AbilityCooldown(ulong clientId)
            {
                object cooldowns = GetField("abilityCooldowns");
                bool contains = (bool)cooldowns.GetType().GetMethod("ContainsKey").Invoke(cooldowns, new object[] { clientId });
                return contains
                    ? Convert.ToSingle(cooldowns.GetType().GetProperty("Item").GetValue(cooldowns, new object[] { clientId }))
                    : 0f;
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

            public ulong FindClientIdByPrivateRole(string roleName)
            {
                object roles = GetField("privateRoles");
                foreach (object entry in (System.Collections.IEnumerable)roles)
                {
                    Type entryType = entry.GetType();
                    object role = entryType.GetProperty("Value").GetValue(entry);
                    if (role.ToString() == roleName)
                    {
                        return Convert.ToUInt64(entryType.GetProperty("Key").GetValue(entry));
                    }
                }

                Assert.Fail("未找到身份：" + roleName);
                return 0UL;
            }

            public int[] AssignedTaskIds(ulong clientId)
            {
                object sync = GetField("syncManager");
                object taskSync = sync.GetType().GetProperty("TaskSync").GetValue(sync);
                object assignments = taskSync.GetType().GetProperty("PlayerAssignments").GetValue(taskSync);
                bool contains = (bool)assignments.GetType().GetMethod("ContainsKey").Invoke(assignments, new object[] { clientId });
                if (!contains)
                {
                    return Array.Empty<int>();
                }

                object assigned = assignments.GetType().GetProperty("Item").GetValue(assignments, new object[] { clientId });
                var taskIds = new List<int>();
                foreach (object taskId in (System.Collections.IEnumerable)assigned)
                {
                    taskIds.Add(Convert.ToInt32(taskId));
                }

                return taskIds.ToArray();
            }

            public void AssignTask(ulong clientId, int taskId)
            {
                object sync = GetField("syncManager");
                object taskSync = sync.GetType().GetProperty("TaskSync").GetValue(sync);
                object assignments = taskSync.GetType().GetProperty("PlayerAssignments").GetValue(taskSync);
                object assigned = new HashSet<int> { taskId };
                assignments.GetType().GetMethod("Add").Invoke(assignments, new[] { (object)clientId, assigned });
            }

            public bool IsSabotageTask(int taskId)
            {
                object sabotage = RuntimeType("GanglandUndercover.Online.OnlineMatchUtils")
                    .GetMethod("SabotageForTask", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { taskId });
                return sabotage.ToString() != "None";
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
                    writer.WriteValueSafe((int)Enum.Parse(professionType, "Inspector"));
                    InvokeReceive("ReceiveRoleAssign", senderClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }

            public void ReceiveIdentityProgress(
                ulong senderClientId,
                string roleName,
                int intel,
                int missionsDone,
                bool betrayed,
                bool exposed,
                int kills,
                int sabotages,
                bool survivedTilLate)
            {
                FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);

                try
                {
                    writer.WriteValueSafe(RoleValue(roleName));
                    writer.WriteValueSafe(intel);
                    writer.WriteValueSafe(missionsDone);
                    writer.WriteValueSafe(betrayed);
                    writer.WriteValueSafe(exposed);
                    writer.WriteValueSafe(kills);
                    writer.WriteValueSafe(sabotages);
                    writer.WriteValueSafe(survivedTilLate);
                    InvokeReceive("ReceiveIdentityProgress", senderClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }

            public bool IsMoleExposed(ulong clientId)
            {
                return (bool)controllerType.GetMethod("IsMoleExposed")
                    .Invoke(controller, new object[] { clientId });
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

            public float PropertyFloat(string propertyName)
            {
                return Convert.ToSingle(controllerType.GetProperty(propertyName).GetValue(controller));
            }

            public int VisibleFootprintCount(ulong viewerId)
            {
                object footprints = controllerType.GetMethod("VisibleFootprints")
                    .Invoke(controller, new object[] { viewerId });
                return Convert.ToInt32(footprints.GetType().GetProperty("Count").GetValue(footprints));
            }

            public bool IsDarkVisionActive(ulong clientId)
            {
                return (bool)controllerType.GetMethod("IsDarkVisionActive")
                    .Invoke(controller, new object[] { clientId });
            }

            public void TickProfessionAbilities(float deltaTime)
            {
                controllerType.GetMethod("TickProfessionAbilities")
                    .Invoke(controller, new object[] { deltaTime });
            }

            public void SetBlackoutVisionReduced(bool value)
            {
                SetField("_blackoutVisionReduced", value);
            }

            public float FieldFloat(string fieldName)
            {
                return Convert.ToSingle(GetField(fieldName));
            }

            public void SetSingleTask(int taskId, Vector3 position, bool completed, bool sabotaged)
            {
                SetTaskState(taskId, position, completed ? 1 : 0, 1, completed, sabotaged);
            }

            public void SetTaskState(
                int taskId,
                Vector3 position,
                int progress,
                int requiredProgress,
                bool completed,
                bool sabotaged)
            {
                object task = Activator.CreateInstance(
                    taskStateType,
                    taskId,
                    "Task" + taskId,
                    position,
                    progress,
                    requiredProgress,
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

            public int VoteCount()
            {
                object votes = GetField("votes");
                return Convert.ToInt32(votes.GetType().GetProperty("Count").GetValue(votes));
            }

            public bool HasVoted(ulong clientId)
            {
                return (bool)controllerType
                    .GetMethod("HasVoted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { clientId });
            }

            public object CaptureSnapshot()
            {
                EnsureSnapshotDependencies();
                return controllerType.GetMethod("CaptureSnapshot").Invoke(controller, null);
            }

            public void RestoreFromSnapshot(object snapshot)
            {
                EnsureSnapshotDependencies();
                controllerType.GetMethod("RestoreFromSnapshot").Invoke(controller, new[] { snapshot });
            }

            public int SnapshotListCount(object snapshot, string fieldName)
            {
                object list = snapshotType.GetField(fieldName).GetValue(snapshot);
                return Convert.ToInt32(list.GetType().GetProperty("Count").GetValue(list));
            }

            public int TaskCount()
            {
                object tasks = GetField("tasks");
                return Convert.ToInt32(tasks.GetType().GetProperty("Count").GetValue(tasks));
            }

            public int PlayerCount()
            {
                object players = GetField("players");
                return Convert.ToInt32(players.GetType().GetProperty("Count").GetValue(players));
            }

            public int TaskProgress(int taskId)
            {
                object task = FindTask(taskId);
                return Convert.ToInt32(taskStateType.GetField("Progress").GetValue(task));
            }

            public Vector3 TaskPosition(int taskId)
            {
                object task = FindTask(taskId);
                return (Vector3)taskStateType.GetField("Position").GetValue(task);
            }

            public string TaskName(int taskId)
            {
                object task = FindTask(taskId);
                return (string)taskStateType.GetField("Name").GetValue(task);
            }

            public string TaskMapCode(int taskId)
            {
                return Convert.ToString(controllerType.GetMethod("TaskMapCodeDisplayName")
                    .Invoke(controller, new object[] { taskId }));
            }

            public void SetExistingTaskSabotaged(int taskId, bool sabotaged)
            {
                object task = FindTask(taskId);
                taskStateType.GetField("Sabotaged").SetValue(task, sabotaged);
                controllerType.GetMethod("SetTask", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new[] { task });
            }

            public bool TaskCompleted(int taskId)
            {
                object task = FindTask(taskId);
                return Convert.ToBoolean(taskStateType.GetField("Completed").GetValue(task));
            }

            public bool PlayerTaskCompleted(ulong clientId, int taskId)
            {
                object task = FindTask(taskId);
                object playerTask = controllerType
                    .GetMethod("TaskForPlayer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new[] { (object)clientId, task });
                return Convert.ToBoolean(taskStateType.GetField("Completed").GetValue(playerTask));
            }

            public bool TaskSabotaged(int taskId)
            {
                object task = FindTask(taskId);
                return Convert.ToBoolean(taskStateType.GetField("Sabotaged").GetValue(task));
            }

            public int BodyCount()
            {
                object killSystem = GetField("killSystem");
                object bodies = killSystem.GetType().GetField("bodies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(killSystem);
                return Convert.ToInt32(bodies.GetType().GetProperty("Count").GetValue(bodies));
            }

            public int CaseLogCount()
            {
                object caseLog = GetField("caseLog");
                return Convert.ToInt32(caseLog.GetType().GetProperty("Count").GetValue(caseLog));
            }

            public bool CaseLogContains(string fragment)
            {
                object caseLog = GetField("caseLog");
                foreach (object entry in (IEnumerable)caseLog)
                {
                    if (entry != null && entry.ToString().Contains(fragment))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void AddBody(int bodyId, ulong victimClientId, Vector3 position, bool reported)
            {
                EnsureSnapshotDependencies();
                object body = Activator.CreateInstance(bodyStateType, bodyId, victimClientId, position, reported);
                object killSystem = GetField("killSystem");
                object bodies = killSystem.GetType().GetField("bodies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(killSystem);
                bodies.GetType().GetMethod("Add").Invoke(bodies, new[] { body });
            }

            public void AddVoteRaw(ulong voterClientId, ulong targetClientId)
            {
                object votes = GetField("votes");
                votes.GetType().GetMethod("Add").Invoke(votes, new object[] { voterClientId, targetClientId });
            }

            public void AddCaseLogRaw(string entry)
            {
                object caseLog = GetField("caseLog");
                caseLog.GetType().GetMethod("Add").Invoke(caseLog, new object[] { entry });
            }

            public void SetTaskServiceEvidence(int score, int targetValue)
            {
                EnsureSnapshotDependencies();
                object taskService = GetField("taskService");
                taskService.GetType().GetProperty("EvidenceScore").SetValue(taskService, score);
                taskService.GetType().GetProperty("EvidenceTarget").SetValue(taskService, targetValue);
            }

            public int EvidenceServiceScore()
            {
                EnsureSnapshotDependencies();
                object service = GetField("evidenceService");
                return Convert.ToInt32(service.GetType().GetProperty("EvidenceScore").GetValue(service));
            }

            public string EvidenceServiceString(string propertyName)
            {
                EnsureSnapshotDependencies();
                object service = GetField("evidenceService");
                return Convert.ToString(service.GetType().GetProperty(propertyName).GetValue(service));
            }

            public void SetLastEvidenceEventRaw(string value)
            {
                SetField("lastEvidenceEvent", value);
            }

            public void SyncEvidenceServiceFromController()
            {
                EnsureSnapshotDependencies();
                controllerType.GetMethod("SyncEvidenceServiceFromController", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
            }

            public void InitializeEvidenceServiceTwice()
            {
                EnsureSnapshotDependencies();
                object service = GetField("evidenceService");
                object bus = GetField("gameEventBus");
                MethodInfo initialize = service.GetType().GetMethod("Initialize");
                initialize.Invoke(service, new[] { controller, bus });
                initialize.Invoke(service, new[] { controller, bus });
            }

            public void PublishTaskCompleted(ulong playerId, int taskIndex)
            {
                EnsureSnapshotDependencies();
                Type eventType = RuntimeType("GanglandUndercover.Online.TaskCompletedEvent");
                object evt = Activator.CreateInstance(eventType);
                eventType.GetField("PlayerId").SetValue(evt, playerId);
                eventType.GetField("TaskIndex").SetValue(evt, taskIndex);
                object bus = GetField("gameEventBus");
                bus.GetType().GetMethod("Publish").MakeGenericMethod(eventType)
                    .Invoke(bus, new[] { evt });
            }

            public void MeetingServiceOnMatchStarted(int playerCount)
            {
                EnsureSnapshotDependencies();
                object service = GetField("meetingService");
                service.GetType().GetMethod("OnMatchStarted").Invoke(service, new object[] { playerCount });
            }

            public void MeetingServiceCallEmergencyMeeting(string callerDisplayName, ulong callerId)
            {
                EnsureSnapshotDependencies();
                object service = GetField("meetingService");
                service.GetType().GetMethod("CallEmergencyMeeting")
                    .Invoke(service, new object[] { callerDisplayName, callerId });
            }

            public void SetMeetingServiceCooldown(float value)
            {
                EnsureSnapshotDependencies();
                object service = GetField("meetingService");
                service.GetType().GetMethod("SetEmergencyCooldownTimer").Invoke(service, new object[] { value });
            }

            public EventProbe AttachEventProbe()
            {
                EnsureSnapshotDependencies();
                object bus = GetField("gameEventBus");
                var probe = new EventProbe();
                Type meetingCalledType = RuntimeType("GanglandUndercover.Online.MeetingCalledEvent");
                Type bodyReportedType = RuntimeType("GanglandUndercover.Online.BodyReportedEvent");
                MethodInfo subscribe = bus.GetType().GetMethod("Subscribe");

                subscribe.MakeGenericMethod(meetingCalledType)
                    .Invoke(bus, new[] { probe.CreateHandler(nameof(EventProbe.OnMeetingCalled), meetingCalledType) });
                subscribe.MakeGenericMethod(bodyReportedType)
                    .Invoke(bus, new[] { probe.CreateHandler(nameof(EventProbe.OnBodyReported), bodyReportedType) });
                return probe;
            }

            public int MeetingServiceInt(string propertyName)
            {
                EnsureSnapshotDependencies();
                object service = GetField("meetingService");
                return Convert.ToInt32(service.GetType().GetProperty(propertyName).GetValue(service));
            }

            public float MeetingServiceFloat(string propertyName)
            {
                EnsureSnapshotDependencies();
                object service = GetField("meetingService");
                return Convert.ToSingle(service.GetType().GetProperty(propertyName).GetValue(service));
            }

            public string MeetingServiceString(string propertyName)
            {
                EnsureSnapshotDependencies();
                object service = GetField("meetingService");
                return Convert.ToString(service.GetType().GetProperty(propertyName).GetValue(service));
            }

            public void ControllerCallEmergencyMeeting(string callerDisplayName)
            {
                EnsureSnapshotDependencies();
                controllerType.GetMethod("CallEmergencyMeeting")
                    .Invoke(controller, new object[] { callerDisplayName });
            }

            public void ControllerTryReportOrEmergency(ulong senderClientId)
            {
                EnsureSnapshotDependencies();
                object player = GetPlayerState(senderClientId);
                controllerType.GetMethod("TryReportOrEmergency", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { senderClientId, player });
            }

            public void SetSabotageTimers(float blackout, float lockdown, float commJam, float evidenceLeak, float evidenceLeakAccumulator, float patrolAlert)
            {
                EnsureSnapshotDependencies();
                object taskService = GetField("taskService");
                taskService.GetType().GetMethod("LoadSabotageTimersFromSnapshot")
                    .Invoke(taskService, new object[] { blackout, lockdown, commJam, evidenceLeak, evidenceLeakAccumulator, patrolAlert });
            }

            public void TickSabotageService(float deltaTime)
            {
                EnsureSnapshotDependencies();
                object service = GetField("sabotageService");
                service.GetType().GetMethod("Tick").Invoke(service, new object[] { deltaTime });
            }

            public void SetGlobalTimers(float phaseTimer, float emergencyCooldown, float aiGrace, float elapsed)
            {
                SetField("phaseTimer", phaseTimer);
                SetField("emergencyCooldownTimer", emergencyCooldown);
                SetField("aiActionGraceTimer", aiGrace);
                SetField("matchElapsedSeconds", elapsed);
            }

            public void ResolveTimeLimitOutcome()
            {
                InvokeNonPublic("ResolveTimeLimitOutcome");
            }

            public void AdvanceMatchClock(float deltaTime)
            {
                controllerType.GetMethod("AdvanceMatchClock", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { deltaTime });
            }

            public float MatchElapsed()
            {
                return Convert.ToSingle(GetField("matchElapsedSeconds"));
            }

            public void TriggerCriticalTask(string taskTypeName)
            {
                EnsureSnapshotDependencies();
                Type criticalTaskType = RuntimeType("GanglandUndercover.SocialDeduction.CriticalTaskType");
                controllerType.GetMethod("TriggerCriticalTask", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new[] { Enum.Parse(criticalTaskType, taskTypeName) });
            }

            public void RecordCriticalRepair(ulong clientId, int taskId)
            {
                controllerType.GetMethod("RecordCriticalTaskRepair", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { clientId, taskId });
            }

            public void SetGangPositionRevealTimer(float value)
            {
                SetField("_gangPositionRevealTimer", value);
            }

            public bool ShouldRevealPlayerPosition(ulong viewerClientId, ulong targetClientId)
            {
                return (bool)controllerType.GetMethod("ShouldRevealPlayerPosition")
                    .Invoke(controller, new object[] { viewerClientId, targetClientId });
            }

            public int CriticalEvidenceRepairStationCount()
            {
                return Convert.ToInt32(controllerType
                    .GetProperty("CriticalEvidenceRepairStationCount", BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(controller));
            }

            public bool CheckUndercoverSoloWin(ulong clientId)
            {
                return (bool)controllerType.GetMethod("CheckUndercoverSoloWin")
                    .Invoke(controller, new object[] { clientId });
            }

            public void SetKillCooldownRaw(ulong clientId, float value)
            {
                EnsureSnapshotDependencies();
                object killSystem = GetField("killSystem");
                object cooldowns = killSystem.GetType().GetField("killCooldowns", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(killSystem);
                cooldowns.GetType().GetProperty("Item").SetValue(cooldowns, value, new object[] { clientId });
            }

            public void SetAbilityCooldownRaw(ulong clientId, float value)
            {
                EnsureSnapshotDependencies();
                object cooldowns = GetField("abilityCooldowns");
                cooldowns.GetType().GetProperty("Item").SetValue(cooldowns, value, new object[] { clientId });
            }

            public void SetVentCooldownRaw(ulong clientId, float value)
            {
                EnsureSnapshotDependencies();
                object cooldowns = GetField("ventCooldowns");
                cooldowns.GetType().GetProperty("Item").SetValue(cooldowns, value, new object[] { clientId });
            }

            public void SetBotTimerRaw(ulong clientId, float think, float vote, Vector3 targetPosition)
            {
                EnsureSnapshotDependencies();
                InvokeNonPublic("EnsureBotController");
                object botController = GetField("_botController");
                Assert.IsNotNull(botController, "Bot controller must exist before writing snapshot bot timers.");
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                botController.GetType().GetMethod("SetThinkTimer", flags).Invoke(botController, new object[] { clientId, think });
                botController.GetType().GetMethod("SetVoteTimer", flags).Invoke(botController, new object[] { clientId, vote });
                botController.GetType().GetMethod("SetTarget", flags).Invoke(botController, new object[] { clientId, targetPosition });
            }

            public void SeedEvidenceChainForAccusation(ulong discovererId, ulong targetId)
            {
                SeedEvidenceNodes(discovererId);
                controllerType.GetMethod("AccusePlayer").Invoke(controller, new object[] { discovererId, targetId });
            }

            public void SeedEvidenceNodes(ulong discovererId)
            {
                EnsureSnapshotDependencies();
                controllerType.GetMethod("RegisterTaskEvidence").Invoke(controller, new object[] { 0, Vector2.zero, discovererId });
                controllerType.GetMethod("RegisterTaskEvidence").Invoke(controller, new object[] { 1, Vector2.zero, discovererId });
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
                writer.WriteValueSafe(0);
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

            private object FindTask(int taskId)
            {
                object tasks = GetField("tasks");
                foreach (object task in (IEnumerable)tasks)
                {
                    if (Convert.ToInt32(taskStateType.GetField("Id").GetValue(task)) == taskId)
                    {
                        return task;
                    }
                }

                Assert.Fail("找不到任务 " + taskId);
                return null;
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
