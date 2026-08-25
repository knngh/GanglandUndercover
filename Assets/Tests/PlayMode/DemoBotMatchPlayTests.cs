using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GanglandUndercover.PlayTests
{
    /// <summary>
    /// Demo D-1：本地 Host + Bot 的自然对局门禁。
    ///
    /// 这个用例不调用 EditorForceMeetingForSmokeTest 或 EditorForceResultForSmokeTest，
    /// 只通过本地自动补位、真实 Update/AI 决策和规则硬时限完成一局，避免把烟测钩子
    /// 当成 Bot 可玩性的证据。规则时长在测试内缩短，保持行为路径不变。
    /// </summary>
    public sealed class DemoBotMatchPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private const int TestTimeoutMilliseconds = 120000;

        private GameObject _host;
        private GameObject _cameraHost;
        private MonoBehaviour _controller;
        private Type _controllerType;
        private UnityEngine.Random.State _randomState;
        private bool _randomStateCaptured;

        [SetUp]
        public void SetUp()
        {
            _randomState = UnityEngine.Random.state;
            _randomStateCaptured = true;

            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(_controllerType,
                $"找不到运行时类型 {ControllerTypeName}（Assembly-CSharp 未编译？）");

            _cameraHost = new GameObject("PlayTest_DemoBot_MainCamera", typeof(Camera), typeof(AudioListener));
            _cameraHost.tag = "MainCamera";
            Camera camera = _cameraHost.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 13.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 1f);
            _cameraHost.transform.position = new Vector3(0f, 0f, -16.2f);

            _host = new GameObject("PlayTest_DemoBotMatch");
            _controller = (MonoBehaviour)_host.AddComponent(_controllerType);
            Assert.IsNotNull(_controller, "无法在 PlayMode 下挂载 OnlineMatchController。");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            if (_randomStateCaptured)
            {
                UnityEngine.Random.state = _randomState;
            }

            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }

            if (_cameraHost != null)
            {
                UnityEngine.Object.Destroy(_cameraHost);
            }
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator BotMatch_CompletesNaturalLoopWithinDemoBudget()
        {
            yield return null;

            // Fix the scenario seed so the Demo gate is stable when the full
            // PlayMode suite runs before it.
            UnityEngine.Random.InitState(20260822);

            // Keep the real bot path, but compress the test-only rule budget so the
            // regression test remains bounded and does not claim to be a 10-minute soak.
            SetRuleField("PreviewAiActionGraceSeconds", 0f);
            SetRuleField("AiActionGraceSeconds", 0f);
            SetRuleField("FirstKillMinDelaySeconds", 60f);
            SetRuleField("MatchTargetMinSeconds", 0f);
            SetRuleField("MatchHardLimitSeconds", 180f);
            SetRuleField("MeetingIntroSeconds", 2f);
            SetRuleField("VotingSeconds", 2f);
            SetRuleField("KillCooldownSeconds", 60f);
            SetRuleField("ReportCooldownSeconds", 0f);
            SetRuleField("EmergencyCooldownSeconds", 0f);
            SetRuleField("BotBodyReportProbability", 1f);
            SetRuleField("BotEmergencyMeetingProbability", 1f);
            SetRuleField("ReportRange", 100f);
            SetRuleField("BotMoveSpeedMultiplier", 4f);
            SetRuleField("TasksPerNonGangPlayer", 2);

            SetControllerField("localPreviewMode", true);
            Invoke("EditorSimulateLocalMatch");
            Assert.IsTrue(GetBoolProperty("MatchStarted"), "本地 Bot 局应已开始。");
            Assert.AreEqual(7, GetIntProperty("BotCount"), "Demo Bot 局应为 1 Host + 7 Bot。");
            Assert.AreEqual(8, GetIntProperty("PlayerCount"), "Demo Bot 局总人数应为 8。");
            Assert.AreEqual("Opening", GetEnumProperty("Phase"), "对局应先进入身份简报。");
            ClusterPlayersForDemoMeeting();

            string previousPhase = GetEnumProperty("Phase");
            bool sawAction = false;
            bool sawMeeting = false;
            int phaseChanges = 0;
            const int maxFrames = 2400;

            // Time.deltaTime remains the production clock; timeScale only compresses
            // the wall-clock duration of this bounded integration test.
            Time.timeScale = 20f;
            string phaseTrace = GetEnumProperty("Phase");
            for (int frame = 0; frame < maxFrames; frame++)
            {
                // Batchmode can advance yielded-null frames faster than wall clock,
                // producing an effectively zero Time.deltaTime. A tiny realtime
                // slice keeps the production clock and Bot Update loop observable.
                yield return new WaitForSecondsRealtime(0.01f);

                string phase = GetEnumProperty("Phase");
                if (!string.Equals(phase, previousPhase, StringComparison.Ordinal))
                {
                    phaseChanges++;
                    phaseTrace += " -> " + phase;
                    previousPhase = phase;
                }

                sawAction |= string.Equals(phase, "Action", StringComparison.Ordinal);
                sawMeeting |= string.Equals(phase, "Meeting", StringComparison.Ordinal)
                    || string.Equals(phase, "Voting", StringComparison.Ordinal);

                if (string.Equals(phase, "Result", StringComparison.Ordinal))
                {
                    break;
                }
            }

            Time.timeScale = 1f;

            Debug.Log($"[DemoBot] phaseTrace={phaseTrace} phase={GetEnumProperty("Phase")} elapsed={GetFloatProperty("MatchElapsedSeconds")} meetings={GetIntProperty("MeetingCount")} kills={GetIntProperty("KillCount")} bodies={GetIntProperty("BodyCount")} bots={GetIntProperty("BotCount")} botTasks={GetNestedIntProperty("BotController", "CompletedTaskCount")} result={GetStringProperty("ResultSummary")}");

            Assert.IsTrue(sawAction, "Bot 局必须实际进入 Action 行动阶段。阶段轨迹: " + phaseTrace);
            Assert.IsTrue(sawMeeting, "Bot 局必须由真实 AI 行为触发至少一次会议或投票。阶段轨迹: " + phaseTrace);
            Assert.GreaterOrEqual(phaseChanges, 2, "Bot 局应至少发生 Opening → Action → 会议/结算的阶段变化。阶段轨迹: " + phaseTrace);
            Assert.AreEqual("Result", GetEnumProperty("Phase"),
                "Bot 局在有界测试预算内应自然进入 Result，而非依赖强制结算钩子。阶段轨迹: " + phaseTrace);
            Assert.IsFalse(string.IsNullOrWhiteSpace(GetStringProperty("ResultSummary")),
                "自然 Bot 局结算文案应非空。");
            Assert.GreaterOrEqual(GetIntProperty("CaseLogCount"), 3,
                "自然 Bot 局应记录开局、行动和结算等案卷事件。");
            Assert.GreaterOrEqual(GetIntProperty("BotCount"), 1,
                "结算前 Bot roster 应保持可读，不能被测试生命周期误清理。");
            Assert.GreaterOrEqual(GetIntProperty("MeetingCount"), 1,
                "自然 Bot 局应至少召开一次真实会议。");
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator BotRoster_SupportsFourPlayerDemoScale()
        {
            yield return VerifyBotScale(4, 1, 1, 0);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator BotRoster_SupportsSixPlayerDemoScale()
        {
            yield return VerifyBotScale(6, 1, 1, 0);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator BotRoster_SupportsEightPlayerDemoScale()
        {
            yield return VerifyBotScale(8, 2, 1, 1);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator BotRoster_SupportsTenPlayerDemoScale()
        {
            yield return VerifyBotScale(10, 3, 2, 1);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator OnboardingBriefing_ExposesIdentityObjectiveAndActionPrompt()
        {
            yield return null;

            SetControllerField("localPreviewMode", true);
            Invoke("EditorSimulateLocalMatch");

            Assert.AreEqual("Opening", GetEnumProperty("Phase"), "身份简报门禁应从 Opening 阶段开始。");
            Assert.IsTrue(GetBoolProperty("HasOnboardingGuidance"),
                "Opening 阶段必须同时提供标题、正文和操作提示。");

            string openingTitle = GetStringProperty("OnboardingBriefingTitle");
            string openingBody = GetStringProperty("OnboardingBriefingBody");
            string openingPrompt = GetStringProperty("OnboardingActionPrompt");
            StringAssert.Contains("身份简报", openingTitle, "身份简报标题应明确告诉玩家当前是开局简报。");
            StringAssert.Contains("身份", openingBody, "身份简报正文必须包含真实身份字段。");
            StringAssert.Contains("目标", openingBody, "身份简报正文必须包含胜利/当前目标字段。");
            Assert.IsFalse(string.IsNullOrWhiteSpace(openingPrompt), "Opening 操作提示不能为空。");

            Time.timeScale = 12f;
            string actionPrompt = openingPrompt;
            string phaseTrace = "Opening";
            for (int frame = 0; frame < 120; frame++)
            {
                yield return new WaitForSecondsRealtime(0.01f);
                string phase = GetEnumProperty("Phase");
                if (phase == "Action")
                {
                    actionPrompt = GetStringProperty("OnboardingActionPrompt");
                    phaseTrace += " -> Action";
                    break;
                }
            }
            Time.timeScale = 1f;

            Assert.AreEqual("Action", GetEnumProperty("Phase"),
                "身份简报倒计时结束后应进入第一行动阶段。阶段轨迹: " + phaseTrace);
            Assert.IsFalse(string.IsNullOrWhiteSpace(actionPrompt), "Action 阶段操作提示不能为空。");
            Assert.AreNotEqual(openingPrompt, actionPrompt,
                "进入 Action 后提示应从记忆身份切换为当前角色可执行目标。");
            Assert.IsTrue(GetBoolProperty("HasOnboardingGuidance"),
                "Action 阶段仍必须保留可读的身份/目标引导。");
        }

        private void Invoke(string methodName, params object[] args)
        {
            MethodInfo method = _controllerType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"找不到方法 {methodName}");
            method.Invoke(_controller, args);
        }

        private IEnumerator VerifyBotScale(int target, int expectedGang, int expectedUndercover, int expectedMole)
        {
            yield return null;

            Invoke("SetRoomMinPlayers", target);
            Invoke("SetRoomMaxPlayers", target);
            SetControllerField("localPreviewMode", true);
            Invoke("EditorSimulateLocalMatch");

            Assert.AreEqual(target, GetIntProperty("RoomMinPlayers"),
                target + " 人规模门禁应保留目标最少人数配置。");
            Assert.AreEqual(target, GetIntProperty("RoomMaxPlayers"),
                target + " 人规模门禁应保留目标最多人数配置。");
            Assert.IsTrue(GetBoolProperty("MatchStarted"), target + " 人 Demo 局应已开始。");
            Assert.AreEqual(target, GetIntProperty("PlayerCount"),
                target + " 人 Demo 局应由真实 Bot 补位到目标人数。");
            Assert.AreEqual(target - 1, GetIntProperty("BotCount"),
                target + " 人 Demo 局应保持 1 个本地 Host + Bot 补位。");
            Assert.AreEqual("Opening", GetEnumProperty("Phase"),
                target + " 人 Demo 局应先进入身份简报阶段。");
            AssertRoleCount("Gang", expectedGang, target);
            AssertRoleCount("Undercover", expectedUndercover, target);
            AssertRoleCount("Mole", expectedMole, target);
        }

        private void AssertRoleCount(string roleName, int expected, int playerCount)
        {
            MethodInfo roleMethod = _controllerType.GetMethod("GetPrivateRole",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(roleMethod, "找不到 OnlineMatchController.GetPrivateRole。");

            int actual = 0;
            System.Collections.IDictionary players = GetProperty("Players") as System.Collections.IDictionary;
            Assert.IsNotNull(players, "规模矩阵需要读取玩家 roster。");
            foreach (System.Collections.DictionaryEntry entry in players)
            {
                object role = roleMethod.Invoke(_controller, new[] { entry.Key });
                if (string.Equals(role?.ToString(), roleName, StringComparison.Ordinal))
                {
                    actual++;
                }
            }

            Assert.AreEqual(expected, actual,
                playerCount + " 人局的 " + roleName + " 数量应匹配规则集。");
        }

        private object GetRuleSet()
        {
            PropertyInfo property = _controllerType.GetProperty("RuleSet",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, "OnlineMatchController.RuleSet 应可供 Demo 规则门禁读取。");
            return property.GetValue(_controller);
        }

        private void SetControllerField(string fieldName, object value)
        {
            FieldInfo field = _controllerType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"找不到控制器字段 {fieldName}");
            field.SetValue(_controller, value);
        }

        private void SetRuleField(string fieldName, object value)
        {
            object ruleSet = GetRuleSet();
            FieldInfo field = ruleSet.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"找不到 Demo 规则字段 {fieldName}");
            field.SetValue(ruleSet, value);
        }

        private object GetProperty(string propertyName)
        {
            PropertyInfo property = _controllerType.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, $"找不到属性 {propertyName}");
            return property.GetValue(_controller);
        }

        private string GetEnumProperty(string propertyName)
        {
            return GetProperty(propertyName)?.ToString() ?? string.Empty;
        }

        private int GetIntProperty(string propertyName)
        {
            return Convert.ToInt32(GetProperty(propertyName));
        }

        private bool GetBoolProperty(string propertyName)
        {
            return Convert.ToBoolean(GetProperty(propertyName));
        }

        private string GetStringProperty(string propertyName)
        {
            return GetProperty(propertyName) as string ?? string.Empty;
        }

        private float GetFloatProperty(string propertyName)
        {
            return Convert.ToSingle(GetProperty(propertyName));
        }

        private int GetNestedIntProperty(string propertyName, string nestedPropertyName)
        {
            object target = GetProperty(propertyName);
            Assert.IsNotNull(target, $"属性 {propertyName} 不应为空。");
            PropertyInfo property = target.GetType().GetProperty(nestedPropertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, $"找不到嵌套属性 {nestedPropertyName}");
            return Convert.ToInt32(property.GetValue(target));
        }

        private void ClusterPlayersForDemoMeeting()
        {
            System.Collections.IDictionary players = GetProperty("Players") as System.Collections.IDictionary;
            Assert.IsNotNull(players, "Demo Bot 局需要读取玩家状态字典。");
            FieldInfo positionField = null;
            List<object> entries = new List<object>();
            foreach (System.Collections.DictionaryEntry entry in players)
            {
                if (positionField == null)
                {
                    positionField = entry.Value.GetType().GetField("Position",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                object state = entry.Value;
                positionField.SetValue(state, Vector3.zero);
                entries.Add(new object[] { entry.Key, state });
            }

            for (int i = 0; i < entries.Count; i++)
            {
                object[] pair = (object[])entries[i];
                players[pair[0]] = pair[1];
            }
        }
    }
}
