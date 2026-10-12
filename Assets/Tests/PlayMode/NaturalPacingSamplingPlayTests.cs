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
    /// D-2-R3: production-clock Bot pacing samples.
    ///
    /// These are opt-in because each case can run for several real-time minutes.
    /// Set GANGLAND_RUN_NATURAL_PACING=1 when collecting a release-facing sample.
    /// No rule timer is changed and no smoke-test result hook is used.
    /// </summary>
    public sealed class NaturalPacingSamplingPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private const int TestTimeoutMilliseconds = 1500000;

        private GameObject _host;
        private GameObject _cameraHost;
        private MonoBehaviour _controller;
        private Type _controllerType;

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType(ControllerTypeName + ", " + RuntimeAssemblyName);
            Assert.IsNotNull(_controllerType, "找不到 OnlineMatchController。");

            _cameraHost = new GameObject("NaturalPacing_MainCamera", typeof(Camera), typeof(AudioListener));
            _cameraHost.tag = "MainCamera";
            Camera camera = _cameraHost.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 13.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 1f);
            _cameraHost.transform.position = new Vector3(0f, 0f, -16.2f);

            _host = new GameObject("NaturalPacing_Controller");
            _controller = (MonoBehaviour)_host.AddComponent(_controllerType);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_host != null) UnityEngine.Object.Destroy(_host);
            if (_cameraHost != null) UnityEngine.Object.Destroy(_cameraHost);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator NaturalPacing_Sample4Players()
        {
            yield return RunNaturalSample(4);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator NaturalPacing_Sample6Players()
        {
            yield return RunNaturalSample(6);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator NaturalPacing_Sample8Players()
        {
            yield return RunNaturalSample(8);
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator NaturalPacing_Sample10Players()
        {
            yield return RunNaturalSample(10);
        }

        /// <summary>
        /// P0-2 诊断：4P 快进局（timeScale 加速），周期 dump bot 位置可走性、任务点可走性、
        /// bot→任务直线路径的 walkable 采样率，用于定位"bot 集体卡死/任务不可达"的几何断点。
        /// </summary>
        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator BotPacingDiag_Inspect4Players()
        {
            RequireOptIn();
            yield return null;

            UnityEngine.Random.InitState(20260824 + 4);
            Invoke("SetRoomMinPlayers", 4);
            Invoke("SetRoomMaxPlayers", 4);
            SetControllerField("localPreviewMode", true);
            Invoke("EditorSimulateNaturalPacingSample");

            Assert.IsTrue(GetBoolProperty("MatchStarted"), "诊断局未开始。");
            Assert.AreEqual("Opening", GetEnumProperty("Phase"));
            Time.timeScale = 12f;

            float startedAt = Time.realtimeSinceStartup;
            float nextDump = 0f;
            while (GetEnumProperty("Phase") != "Result" && Time.realtimeSinceStartup - startedAt < 150f)
            {
                if (Time.realtimeSinceStartup >= nextDump)
                {
                    nextDump = Time.realtimeSinceStartup + 15f;
                    Debug.LogWarning("[BotDiag2] " + BuildWalkabilitySnapshot());
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }

            Debug.LogWarning("[BotDiag2][final] " + BuildWalkabilitySnapshot());
            Time.timeScale = 1f;
        }

        private object GetControllerField(string fieldName)
        {
            FieldInfo field = _controllerType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "找不到控制器字段 " + fieldName);
            return field.GetValue(_controller);
        }

        private bool InvokeWalkable(Vector3 position)
        {
            MethodInfo method = _controllerType.GetMethod("IsWalkable",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "找不到 IsWalkable。");
            return Convert.ToBoolean(method.Invoke(_controller, new object[] { position }));
        }

        private string BuildWalkabilitySnapshot()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("phase=").Append(GetEnumProperty("Phase"))
                .Append(" elapsed=").Append(GetFloatProperty("MatchElapsedSeconds").ToString("F0"))
                .Append(" walkRects=").Append(CountRects("walkableRects"))
                .Append(" obstacles=").Append(CountRects("solidObstacleRects"))
                .Append('\n');

            IDictionary players = (IDictionary)GetProperty("Players");
            Vector3 firstBotPos = Vector3.zero;
            bool hasBot = false;
            foreach (DictionaryEntry entry in players)
            {
                ulong clientId = Convert.ToUInt64(entry.Key);
                if (clientId < 900000L) continue;
                object state = entry.Value;
                Vector3 pos = (Vector3)state.GetType().GetField("Position").GetValue(state);
                if (!hasBot) { firstBotPos = pos; hasBot = true; }

                sb.Append("bot ").Append(clientId % 1000)
                    .Append(" pos=").Append(pos.x.ToString("F1")).Append(',').Append(pos.y.ToString("F1"))
                    .Append(" walk=").Append(InvokeWalkable(pos));
                for (int i = 0; i < 8; i++)
                {
                    float angle = i / 8f * Mathf.PI * 2f;
                    Vector3 step = pos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.2f;
                    sb.Append(' ').Append((char)('0' + i)).Append(InvokeWalkable(step) ? '+' : '-');
                }

                sb.Append('\n');
            }

            IEnumerable tasks = GetProperty("Tasks") as IEnumerable;
            if (tasks != null && hasBot)
            {
                int index = 0;
                foreach (object task in tasks)
                {
                    if (index >= 6) { sb.Append("(tasks truncated)\n"); break; }
                    Vector3 pos = (Vector3)task.GetType().GetField("Position").GetValue(task);
                    string name = (string)task.GetType().GetField("Name").GetValue(task);
                    bool completed = (bool)task.GetType().GetField("Completed").GetValue(task);
                    bool sabotaged = (bool)task.GetType().GetField("Sabotaged").GetValue(task);
                    int blocked = 0, samples = 0;
                    for (float t = 0.1f; t < 1f; t += 0.1f)
                    {
                        Vector3 p = Vector3.Lerp(firstBotPos, pos, t);
                        samples++;
                        if (!InvokeWalkable(p)) blocked++;
                    }

                    sb.Append("task ").Append(index).Append('(').Append(name).Append(')')
                        .Append(" pos=").Append(pos.x.ToString("F1")).Append(',').Append(pos.y.ToString("F1"))
                        .Append(" walk=").Append(InvokeWalkable(pos))
                        .Append(completed ? " done" : sabotaged ? " sab" : " open")
                        .Append(" pathBlocked=").Append(blocked).Append('/').Append(samples)
                        .Append('\n');
                    index++;
                }
            }

            return sb.ToString();
        }

        private int CountRects(string fieldName)
        {
            IEnumerable rects = GetControllerField(fieldName) as IEnumerable;
            if (rects == null) return 0;
            int count = 0;
            foreach (object _ in rects) count++;
            return count;
        }

        private IEnumerator RunNaturalSample(int targetPlayers)
        {
            RequireOptIn();
            yield return null;

            int logsBefore = LoadMatchLogs().Count;
            UnityEngine.Random.InitState(20260824 + targetPlayers);
            Invoke("SetRoomMinPlayers", targetPlayers);
            Invoke("SetRoomMaxPlayers", targetPlayers);
            SetControllerField("localPreviewMode", true);
            Invoke("EditorSimulateNaturalPacingSample");

            Assert.IsTrue(GetBoolProperty("MatchStarted"), targetPlayers + " 人自然采样未开始。");
            Assert.AreEqual(targetPlayers, GetIntProperty("PlayerCount"));
            Assert.AreEqual(1, GetIntProperty("HumanPlayerCount"));
            Assert.AreEqual("Opening", GetEnumProperty("Phase"));
            AssertNaturalRosterHasBotKiller(targetPlayers);
            Time.timeScale = 1f;

            float startedAt = Time.realtimeSinceStartup;
            while (GetEnumProperty("Phase") != "Result")
            {
                if (Time.realtimeSinceStartup - startedAt > TestTimeoutMilliseconds / 1000f - 30f)
                {
                    Assert.Fail(targetPlayers + " 人自然采样未在有界时间内进入 Result。阶段=" + GetEnumProperty("Phase")
                        + " elapsed=" + GetFloatProperty("MatchElapsedSeconds"));
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            yield return null;
            IList logs = LoadMatchLogs();
            Assert.Greater(logs.Count, logsBefore,
                targetPlayers + " 人自然采样进入 Result 但未生成 MatchStats 日志。");

            object latest = GetRecentMatchLog();
            Assert.IsNotNull(latest, "对局已落盘，但控制器近期统计为空。");
            Debug.Log("[D2-R3] scale=" + targetPlayers
                + " duration=" + GetLogField<string>(latest, "DurationFormatted")
                + " meetings=" + GetLogField<int>(latest, "MeetingCount")
                + " kills=" + GetLogField<int>(latest, "KillCount")
                + " tasks=" + GetLogField<int>(latest, "CompletedTasks") + "/" + GetLogField<int>(latest, "TotalTasks")
                + " taskRate=" + GetLogProperty<float>(latest, "TaskCompletionRate").ToString("F3")
                + " winner=" + GetLogField<string>(latest, "WinningFaction")
                + " result=" + GetLogField<string>(latest, "ResultText"));

            Assert.AreEqual(targetPlayers, GetLogField<int>(latest, "TotalPlayers"));
            Assert.GreaterOrEqual(GetLogField<float>(latest, "DurationSeconds"), 0f);
            Assert.IsFalse(string.IsNullOrWhiteSpace(GetLogField<string>(latest, "WinningFaction")));
        }

        private static void RequireOptIn()
        {
            bool envOptIn = string.Equals(Environment.GetEnvironmentVariable("GANGLAND_RUN_NATURAL_PACING"), "1", StringComparison.Ordinal);
            bool argOptIn = false;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-runNaturalPacing", StringComparison.Ordinal))
                {
                    argOptIn = true;
                    break;
                }
            }

            if (!envOptIn && !argOptIn)
            {
                Assert.Ignore("D-2-R3 自然节奏采样默认跳过；设置 GANGLAND_RUN_NATURAL_PACING=1 或传入 -runNaturalPacing 后运行。");
            }
        }

        private void Invoke(string methodName, params object[] args)
        {
            MethodInfo method = _controllerType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "找不到方法 " + methodName);
            method.Invoke(_controller, args);
        }

        private void SetControllerField(string fieldName, object value)
        {
            FieldInfo field = _controllerType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "找不到控制器字段 " + fieldName);
            field.SetValue(_controller, value);
        }

        private object GetProperty(string propertyName)
        {
            PropertyInfo property = _controllerType.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, "找不到属性 " + propertyName);
            return property.GetValue(_controller);
        }

        private string GetEnumProperty(string propertyName) => GetProperty(propertyName)?.ToString() ?? string.Empty;
        private int GetIntProperty(string propertyName) => Convert.ToInt32(GetProperty(propertyName));
        private bool GetBoolProperty(string propertyName) => Convert.ToBoolean(GetProperty(propertyName));
        private float GetFloatProperty(string propertyName) => Convert.ToSingle(GetProperty(propertyName));

        private static IList LoadMatchLogs()
        {
            Type collectorType = Type.GetType("GanglandUndercover.Online.MatchStatsCollector, Assembly-CSharp");
            Assert.IsNotNull(collectorType, "找不到 MatchStatsCollector。");
            return (IList)collectorType.GetMethod("LoadAllLogs", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
        }

        private object GetRecentMatchLog()
        {
            FieldInfo collectorField = _controllerType.GetField("_statsCollector",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(collectorField, "找不到控制器统计采集器字段。");
            object collector = collectorField.GetValue(_controller);
            Assert.IsNotNull(collector, "统计采集器未初始化。");
            IList recent = (IList)collector.GetType().GetProperty("RecentEntries").GetValue(collector);
            return recent.Count > 0 ? recent[recent.Count - 1] : null;
        }

        private void AssertNaturalRosterHasBotKiller(int targetPlayers)
        {
            MethodInfo roleMethod = _controllerType.GetMethod("GetPrivateRole",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(roleMethod, "找不到 OnlineMatchController.GetPrivateRole。");

            PropertyInfo playersProperty = _controllerType.GetProperty("Players",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(playersProperty, "自然采样需要读取玩家 roster。");

            IDictionary players = (IDictionary)playersProperty.GetValue(_controller);
            int botGangCount = 0;
            List<string> roster = new List<string>();
            foreach (DictionaryEntry entry in players)
            {
                object state = entry.Value;
                FieldInfo clientIdField = state.GetType().GetField("ClientId", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo isBotField = state.GetType().GetField("IsBot", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(clientIdField, "自然采样 roster 缺少 ClientId 字段。");
                Assert.IsNotNull(isBotField, "自然采样 roster 缺少 IsBot 字段。");
                ulong clientId = Convert.ToUInt64(clientIdField.GetValue(state));
                bool isBot = Convert.ToBoolean(isBotField.GetValue(state));
                string role = roleMethod.Invoke(_controller, new[] { entry.Key })?.ToString() ?? string.Empty;
                roster.Add(clientId + ":" + role + (isBot ? "[Bot]" : "[Human]"));
                if (isBot && string.Equals(role, "Gang", StringComparison.Ordinal))
                {
                    botGangCount++;
                }
            }

            Debug.Log("[D2-R3] scale=" + targetPlayers + " roster=" + string.Join(",", roster));
            Assert.GreaterOrEqual(botGangCount, 1,
                targetPlayers + " 人自然采样必须至少有一个 Bot 黑帮承担自动击杀，避免唯一真人闲置导致死局。");
        }

        private static T GetLogField<T>(object entry, string fieldName)
        {
            FieldInfo field = entry.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "找不到 MatchLogEntry 字段 " + fieldName);
            return (T)Convert.ChangeType(field.GetValue(entry), typeof(T));
        }

        private static T GetLogProperty<T>(object entry, string propertyName)
        {
            PropertyInfo property = entry.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, "找不到 MatchLogEntry 属性 " + propertyName);
            return (T)Convert.ChangeType(property.GetValue(entry), typeof(T));
        }
    }
}
