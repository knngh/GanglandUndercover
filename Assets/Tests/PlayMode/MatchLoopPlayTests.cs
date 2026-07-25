using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GanglandUndercover.PlayTests
{
    /// <summary>
    /// 第二波 PlayMode 自动化：在真实的 PlayMode 生命周期（Awake/Update/LateUpdate 都会执行）下，
    /// 驱动 OnlineMatchController 跑完整一局循环：
    /// Lobby → Opening → Action → 击杀/报案 → Meeting → Voting → 结算(Result) → 重开。
    ///
    /// 运行时代码仍位于预定义的 Assembly-CSharp，故通过反射访问 public 入口，
    /// 与 EditMode 的 CoreSystemTests 反射风格一致。
    /// </summary>
    public class MatchLoopPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";

        private GameObject _host;
        private MonoBehaviour _controller;
        private Type _controllerType;

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(_controllerType,
                $"找不到运行时类型 {ControllerTypeName}（Assembly-CSharp 未编译？）");

            _host = new GameObject("PlayTest_OnlineMatchHost");
            _controller = (MonoBehaviour)_host.AddComponent(_controllerType);
            Assert.IsNotNull(_controller, "无法在 PlayMode 下挂载 OnlineMatchController。");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }
        }

        [UnityTest]
        public IEnumerator FullMatchLoop_RunsThroughEveryPhaseAndRestarts()
        {
            // Awake 已在 AddComponent 当帧排队；让它执行一帧确保核心服务就绪。
            yield return null;

            // ── 1. 开局前应处于 Lobby ──
            AssertPhase("Lobby", "对局应从 Lobby 开始");
            Assert.AreEqual(0, GetInt("BotCount"), "开局前不应有 Bot");
            Assert.GreaterOrEqual(GetInt("TaskCount"), 28, "大地图固定任务站点应铺满 28 个");

            // ── 2. 启动本地对局 → Opening，并自动补满 Bot ──
            Invoke("EditorSimulateLocalMatch");
            yield return null;

            Assert.IsTrue(GetBool("MatchStarted"), "EditorSimulateLocalMatch 后对局应已开始");
            AssertPhase("Opening", "启动后应进入开局简报 Opening");
            Assert.GreaterOrEqual(GetInt("BotCount"), 7, "本地局应自动补满至少 7 个 AI");

            int playerCount = GetInt("PlayerCount");
            Assert.GreaterOrEqual(playerCount, 8, "总人数（含本地玩家）应 >= 8");

            // ── 3. Opening → Action（用确定性跳过，避免依赖计时器时长）──
            Invoke("EditorSkipOpeningForSmokeTest");
            yield return null;
            AssertPhase("Action", "跳过简报后应进入 Action 行动阶段");

            // 让 host 模拟跑几帧（Update→TickHostSimulation 推进 AI/移动/计时），证明运行时循环活着。
            yield return RunFrames(5);
            AssertPhase("Action", "数帧模拟后应仍稳定在 Action（不应在无事件时异常切相）");
            Assert.GreaterOrEqual(GetInt("CaseLogCount"), 1, "对局应记录了案卷事件");

            // ── 4. 击杀/报案：制造一具尸体 ──
            int aliveBefore = GetInt("AlivePlayerCount");
            bool downed = InvokeBool("EditorForceDownedStateForSmokeTest");
            yield return null;
            Assert.IsTrue(downed, "应成功制造一名倒地玩家与法证现场");
            Assert.GreaterOrEqual(GetInt("BodyCount"), 1, "场上应至少有一具尸体");
            Assert.Less(GetInt("AlivePlayerCount"), aliveBefore, "存活人数应在击杀后下降");

            // ── 5. 召开会议 → Meeting ──
            Invoke("EditorForceMeetingForSmokeTest");
            yield return null;
            AssertPhase("Meeting", "报案/紧急会议后应进入 Meeting");

            // ── 6. 投票反馈可见（驱动 Meeting/Voting 投票路径）──
            bool voteVisible = InvokeBool("EditorForceVoteStateForSmokeTest");
            yield return null;
            Assert.IsTrue(voteVisible, "投票应产生可见反馈");

            // ── 7. 推进到结算 Result（确定性：淘汰全部黑帮并评估胜负）──
            bool reachedResult = InvokeBool("EditorForceResultForSmokeTest");
            yield return null;
            Assert.IsTrue(reachedResult, "淘汰全部黑帮后应判定出胜负并进入 Result");
            AssertPhase("Result", "胜负判定后应进入 Result 结算阶段");

            string summary = GetString("ResultSummary");
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary), "结算应给出非空的胜负文案");
            Assert.AreNotEqual("尚未结算。", summary, "结算文案应被真实结果覆盖");

            // ── 8. 重开 → 回到一个干净的新开局 ──
            Invoke("EditorForceRestartForSmokeTest");
            yield return null;
            Assert.IsTrue(GetBool("MatchStarted"), "重开后对局应再次开始");
            AssertPhase("Opening", "重开后应回到 Opening 新开局");
            Assert.GreaterOrEqual(GetInt("BotCount"), 7, "重开后应仍保有 AI 补位");
        }

        [UnityTest]
        public IEnumerator Character2DAnimator_UpdatesLocalAndRemoteWalkFrames()
        {
            yield return null;

            Invoke("EditorSimulateLocalMatch");
            yield return null;
            Invoke("EditorSkipOpeningForSmokeTest");
            yield return null;

            Assert.IsTrue(InvokeBool("EditorForceLocal2DWalkAnimationForSmokeTest"),
                "本地玩家应创建 2D 动画控制器、身体 Sprite 和方向箭头。");
            yield return new WaitForSeconds(0.36f);

            Assert.GreaterOrEqual(GetInt("Character2DAnimationControllerCount"), GetInt("PlayerCount"),
                "每名玩家都应有 CharacterAnimController。");
            Assert.GreaterOrEqual(GetInt("Character2DReadyRendererCount"), GetInt("PlayerCount"),
                "每名玩家都应绑定身体和方向 SpriteRenderer。");
            Assert.Greater(GetInt("Character2DVisibleDirectionCount"), 0,
                "移动中角色应显示方向箭头。");
            Assert.Greater(GetInt("Character2DWalkingFrameCount"), 0,
                "本地玩家移动时应推进到非 idle 行走帧。");

            Assert.IsTrue(InvokeBool("EditorForceRemote2DWalkAnimationForSmokeTest"),
                "远端/AI 玩家也应创建 2D 动画控制器、身体 Sprite 和方向箭头。");
            yield return new WaitForSeconds(0.36f);

            Assert.Greater(GetInt("Character2DVisibleDirectionCount"), 0,
                "远端/AI 移动中角色应显示方向箭头。");
            Assert.Greater(GetInt("Character2DWalkingFrameCount"), 0,
                "远端/AI 移动时应推进到非 idle 行走帧。");
        }

        [UnityTest]
        public IEnumerator ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable()
        {
            yield return null;

            Invoke("EditorSimulateLocalMatch");
            yield return null;

            Type playerStateType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            Type bodyStateType = RuntimeType("GanglandUndercover.Online.OnlineBodyState");
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            Type professionType = RuntimeType("GanglandUndercover.Online.OnlineProfession");
            Type phaseType = RuntimeType("GanglandUndercover.Online.OnlineMatchPhase");

            object connectedPlayer = Activator.CreateInstance(playerStateType, 12UL, "断线玩家",
                Vector3.zero, true, true, Enum.Parse(roleType, "Police"), Enum.Parse(professionType, "Inspector"), 0, false);
            object otherPlayer = Activator.CreateInstance(playerStateType, 13UL, "留场玩家",
                Vector3.right, true, true, Enum.Parse(roleType, "Police"), Enum.Parse(professionType, "Inspector"), 0, false);
            object body = Activator.CreateInstance(bodyStateType, 1, 12UL, Vector3.one, false);

            object players = GetField("players");
            Invoke(players, "Clear");
            Invoke(players, "Add", 12UL, connectedPlayer);
            Invoke(players, "Add", 13UL, otherPlayer);

            object bodies = GetProp("Bodies");
            Invoke(bodies, "Clear");
            Invoke(bodies, "Add", body);

            object votes = GetField("votes");
            Invoke(votes, "Clear");
            Invoke(votes, "Add", 12UL, 13UL);
            Invoke(votes, "Add", 13UL, 12UL);

            Invoke("MarkTaskActive", 12UL, 7);
            Invoke("MarkTaskActive", 13UL, 8);
            SetField("phase", Enum.Parse(phaseType, "Voting"));
            SetField("matchStarted", false);

            InvokePrivate("HandleClientDisconnected", 12UL);
            yield return null;

            Assert.IsFalse(DictionaryContainsKey(players, 12UL), "断线玩家应从玩家表移除");
            Assert.IsTrue(DictionaryContainsKey(players, 13UL), "未断线玩家必须保留");
            Assert.IsFalse(DictionaryContainsKey(votes, 12UL), "断线玩家已投的票应移除");
            Assert.IsFalse(DictionaryContainsValue(votes, 12UL), "投给断线玩家的票应移除，避免会议票型卡住");
            Assert.IsTrue(TaskLockOwnedBy(8, 13UL), "其他玩家正在处理的任务锁不能被误清");
            Assert.IsFalse(TaskLockOwnedBy(7, 12UL), "断线玩家占用的任务/修复锁必须释放");

            Assert.AreEqual(1, GetCollectionCount(bodies), "断线玩家的尸体应保留，仍可被其他玩家报案");
        }

        [UnityTest]
        public IEnumerator HostDisconnect_ShowsVisibleRecoveryGuidance()
        {
            yield return null;

            SetField("localPreviewMode", false);
            SetField("relayJoinCode", "6kb6dh");
            SetField("relayStatus", "Relay 已加入 6KB6DH。");

            InvokePrivate("HandleClientDisconnected", 0UL);
            yield return null;

            StringAssert.Contains("Host 已断开", GetString("Status"));
            StringAssert.Contains("6KB6DH", GetString("RelayStatus"));
            StringAssert.Contains("Host 已断开", GetString("RelayLobbySummary"));
            StringAssert.Contains("重新开房", GetString("RelayLobbySummary"));
            Assert.IsTrue(GetBool("HasDisconnectedNetworkSession"), "Host 断开后应保留一个可见的断线状态，允许 UI 提供离开/重试入口。");
            Assert.IsTrue(CollectionContainsString(GetProp("CaseLog"), "Host 已断开"), "案卷日志应记录 Host 断开，方便晚测回溯。");

            Button shutdownButton = FindRuntimeButton("离开房间 Button");
            Assert.IsNotNull(shutdownButton, "正式 HUD 应保留离开房间按钮，不能只靠 OnGUI 兜底。");
            Assert.IsTrue(shutdownButton.interactable, "Host 断开后离开房间按钮仍应可点，用于清理旧会话并返回主菜单。");
        }

        [UnityTest]
        public IEnumerator HostMigration_ClientHostDisconnectFallsBackWhenNoRemainingPeers()
        {
            yield return null;

            GameObject networkObject = new GameObject("HostMigration_ClientNetworkManager");
            try
            {
                NetworkManager clientNetworkManager = networkObject.AddComponent<NetworkManager>();
                SetField("localPreviewMode", false);
                SetField("networkManager", clientNetworkManager);
                SetField("matchStarted", true);
                SetPhase("Action");
                SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");

                InvokePrivate("EnsureMigrationManager");
                object migrationManager = GetField("migrationManager");
                SetObjectField(migrationManager, "networkManager", clientNetworkManager);

                InvokeObject(migrationManager, "OnClientDisconnected", NetworkManager.ServerClientId);
                yield return null;

                AssertPhase("Result", "非 Host 客户端检测到 Host 断线且无剩余 peer 时应降级结算。");
                StringAssert.Contains("主机已离线", GetString("Status"));
                Assert.IsFalse((bool)GetObjectProp(migrationManager, "MigrationInProgress"),
                    "降级结算后迁移流程不应继续挂起。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(networkObject);
            }
        }

        [UnityTest]
        public IEnumerator HostMigration_DirectReplacementHostStartsNetworkManager()
        {
            yield return null;

            NetworkManager manager = (NetworkManager)GetField("networkManager");
            Assert.IsNotNull(manager, "PlayMode Awake 应创建或绑定 NetworkManager。");
            if (manager.IsListening)
            {
                manager.Shutdown();
                yield return null;
            }

            SetField("relayJoinCode", string.Empty);
            object[] args = { string.Empty };
            MethodInfo mi = _controllerType.GetMethod(
                "TryStartReplacementHostForMigration",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "OnlineMatchController 应提供 Host migration replacement Host 启动入口。");

            bool started = (bool)mi.Invoke(_controller, args);
            yield return null;

            Assert.IsTrue(started, (string)args[0]);
            Assert.IsTrue(manager.IsHost || manager.IsServer,
                "直连旧连接已关闭时，replacement Host 应真实启动 NetworkManager。");
            StringAssert.Contains("已接管 Host", GetString("Status"));

            if (manager.IsListening)
            {
                manager.Shutdown();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MeetingEvents_PublishDuringPlayModeEmergencyAndBodyReportPaths()
        {
            yield return null;

            SetField("localPreviewMode", true);
            SetPhase("Action");
            SetField("emergencyMeetingsLeft", 1);
            SetField("emergencyCooldownTimer", 0f);
            SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
            SetPlayer(2UL, Vector3.right, alive: true, roleName: "Gang");
            InvokePrivate("SyncMeetingServiceFromController");
            EventProbe emergencyProbe = AttachEventProbe();

            InvokePublic("CallEmergencyMeeting", "玩家1");
            yield return null;

            Assert.AreEqual(1, emergencyProbe.MeetingCalledCount,
                "PlayMode 下公开紧急会议路径也必须发布 MeetingCalledEvent。");
            Assert.IsTrue(emergencyProbe.LastMeetingCalledIsEmergency);
            Assert.AreEqual(0, emergencyProbe.BodyReportedCount);

            SetPhase("Action");
            SetField("emergencyCooldownTimer", 0f);
            SetField("emergencyMeetingsLeft", 1);
            SetKillSystemField("reportCooldownTimer", 0f);
            ClearBodies();
            SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
            SetPlayer(2UL, Vector3.right, alive: false, roleName: "Gang");
            AddBody(7, 2UL, Vector3.zero, reported: false);
            EventProbe reportProbe = AttachEventProbe();

            object player = GetPlayerState(1UL);
            InvokePrivate("TryReportOrEmergency", 1UL, player);
            yield return null;

            Assert.AreEqual(1, reportProbe.BodyReportedCount,
                "PlayMode 下尸体报告路径必须发布 BodyReportedEvent。");
            Assert.AreEqual(1UL, reportProbe.LastBodyReporterId);
            Assert.AreEqual(2UL, reportProbe.LastBodyVictimId);
            Assert.AreEqual(1, reportProbe.MeetingCalledCount);
            Assert.IsFalse(reportProbe.LastMeetingCalledIsEmergency);
            Assert.AreEqual(1UL, reportProbe.LastMeetingCallerId);
        }

        [UnityTest]
        public IEnumerator SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle()
        {
            yield return null;

            SetField("matchStarted", true);
            SetPhase("Voting");
            SetPlayer(1UL, new Vector3(1f, 2f, 0f), alive: true, roleName: "Police");
            SetPlayer(2UL, new Vector3(3f, 4f, 0f), alive: false, roleName: "Gang");
            AddBody(4, 2UL, new Vector3(5f, 6f, 0f), reported: false);
            AddVote(1UL, 2UL);
            SetField("phaseTimer", 17f);
            SetField("emergencyCooldownTimer", 9f);
            SetKillSystemField("reportCooldownTimer", 3f);

            object snapshot = InvokePublicWithResult("CaptureSnapshot");

            ClearPlayers();
            ClearBodies();
            ClearVotes();
            SetField("matchStarted", false);
            SetPhase("Lobby");
            SetField("phaseTimer", 0f);
            SetField("emergencyCooldownTimer", 0f);
            SetKillSystemField("reportCooldownTimer", 0f);

            InvokePublic("RestoreFromSnapshot", snapshot);
            yield return null;

            Assert.IsTrue(GetBool("MatchStarted"), "PlayMode 生命周期下恢复快照后对局应保持已开始。");
            AssertPhase("Voting", "PlayMode 生命周期下恢复快照后阶段应恢复。");
            Assert.AreEqual(new Vector3(1f, 2f, 0f), GetPlayerPosition(1UL));
            Assert.IsFalse(GetPlayerAlive(2UL), "死亡状态必须随快照恢复。");
            Assert.AreEqual(1, GetCollectionCount(GetProp("Bodies")), "尸体列表必须随快照恢复。");
            Assert.AreEqual(1, GetCollectionCount(GetField("votes")), "投票表必须随快照恢复。");
            Assert.AreEqual(17f, GetFloat("PhaseTimer"), 0.001f);
            Assert.AreEqual(9f, GetFloat("EmergencyCooldownTimer"), 0.001f);
            Assert.AreEqual(3f, GetFloat("ReportCooldownTimer"), 0.001f);
            Assert.IsTrue(CollectionContainsString(GetProp("CaseLog"), "主机迁移完成"),
                "快照恢复应写入主机迁移完成案卷，便于断线恢复回溯。");
        }

        [UnityTest]
        public IEnumerator EvidenceChain_TaskEvidenceFeedsMeetingDigestAndVoteClosure()
        {
            yield return null;

            SetField("localPreviewMode", false);
            SetField("matchStarted", true);
            SetPhase("Action");
            SetPlayer(1UL, Vector3.zero, alive: true, roleName: "Police");
            SetPlayer(2UL, Vector3.right, alive: true, roleName: "Police");
            SetPlayer(3UL, Vector3.left, alive: true, roleName: "Gang");
            SetSingleTask(0, Vector3.zero, completed: false, sabotaged: false);

            InvokePublic("MarkTaskActive", 1UL, 0);
            bool completed = InvokeBoolOutString("ValidateAndCompleteTask", 1UL, 0, out string taskError);
            yield return null;

            Assert.IsTrue(completed, taskError);
            Assert.Greater(GetInt("EvidenceScore"), 0, "完成任务应推进证据分。");

            InvokePublic("RegisterTaskEvidence", 1, Vector2.one, 1UL);
            string digest = (string)InvokePublicWithResult("MeetingEvidenceDigest", 1UL);
            StringAssert.Contains("你的证据链", digest);
            StringAssert.Contains("强度3", digest);
            StringAssert.Contains("共 2 条证据", digest);

            SetPhase("Meeting");
            InvokePrivate("ApplyVote", 1UL, 3UL);
            yield return null;
            InvokePrivate("ApplyVote", 2UL, ulong.MaxValue);
            yield return null;
            InvokePrivate("ApplyVote", 3UL, ulong.MaxValue);
            yield return null;

            AssertPhase("Result", "证据链指证权重应打破跳过票并闭合胜负。");
            Assert.IsFalse(GetPlayerAlive(3UL), "被证据链指证的黑帮应被投出局。");
            StringAssert.Contains("警方胜利", GetString("Status"));
        }

        // ──────────────────────────────────────────────────────────
        //  帧驱动辅助
        // ──────────────────────────────────────────────────────────

        private IEnumerator RunFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        // ──────────────────────────────────────────────────────────
        //  反射访问辅助
        // ──────────────────────────────────────────────────────────

        private void Invoke(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}()");
            mi.Invoke(_controller, null);
        }

        private bool InvokeBool(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}()");
            return (bool)mi.Invoke(_controller, null);
        }

        private object GetProp(string name)
        {
            PropertyInfo pi = _controllerType.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(pi, $"找不到属性 {name}");
            return pi.GetValue(_controller);
        }

        private object GetField(string name)
        {
            FieldInfo fi = _controllerType.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            return fi.GetValue(_controller);
        }

        private void SetField(string name, object value)
        {
            FieldInfo fi = _controllerType.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            fi.SetValue(_controller, value);
        }

        private static void SetObjectField(object target, string name, object value)
        {
            FieldInfo fi = target.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            fi.SetValue(target, value);
        }

        private static object GetObjectProp(object target, string name)
        {
            PropertyInfo pi = target.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(pi, $"找不到属性 {name}");
            return pi.GetValue(target);
        }

        private static void InvokeObject(object target, string method, params object[] args)
        {
            MethodInfo mi = target.GetType().GetMethod(method,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {method}");
            mi.Invoke(target, args);
        }

        private void SetPhase(string phaseName)
        {
            Type phaseType = RuntimeType("GanglandUndercover.Online.OnlineMatchPhase");
            SetField("phase", Enum.Parse(phaseType, phaseName));
        }

        private EventProbe AttachEventProbe()
        {
            object bus = GetField("gameEventBus");
            Assert.IsNotNull(bus, "gameEventBus 应在 PlayMode Awake 中初始化。");

            EventProbe probe = new EventProbe();
            Type meetingCalledType = RuntimeType("GanglandUndercover.Online.MeetingCalledEvent");
            Type bodyReportedType = RuntimeType("GanglandUndercover.Online.BodyReportedEvent");
            MethodInfo subscribe = bus.GetType().GetMethod("Subscribe");

            subscribe.MakeGenericMethod(meetingCalledType)
                .Invoke(bus, new[] { probe.CreateHandler(nameof(EventProbe.OnMeetingCalled), meetingCalledType) });
            subscribe.MakeGenericMethod(bodyReportedType)
                .Invoke(bus, new[] { probe.CreateHandler(nameof(EventProbe.OnBodyReported), bodyReportedType) });
            return probe;
        }

        private void SetPlayer(ulong clientId, Vector3 position, bool alive, string roleName)
        {
            Type playerType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            object player = Activator.CreateInstance(
                playerType,
                clientId,
                "玩家" + clientId,
                position,
                true,
                alive,
                Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), roleName),
                Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineProfession"), "Inspector"),
                0,
                false);

            IDictionary players = (IDictionary)GetField("players");
            players[clientId] = player;

            IDictionary privateRoles = (IDictionary)GetField("privateRoles");
            privateRoles[clientId] = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), roleName);
        }

        private void SetSingleTask(int taskId, Vector3 position, bool completed, bool sabotaged)
        {
            object task = Activator.CreateInstance(
                RuntimeType("GanglandUndercover.Online.OnlineTaskState"),
                taskId,
                "Task" + taskId,
                position,
                completed ? 1 : 0,
                1,
                completed,
                sabotaged);

            IList tasks = (IList)GetField("tasks");
            tasks.Clear();
            tasks.Add(task);
        }

        private void ClearPlayers()
        {
            ((IDictionary)GetField("players")).Clear();
            ((IDictionary)GetField("privateRoles")).Clear();
        }

        private object GetPlayerState(ulong clientId)
        {
            object players = GetField("players");
            MethodInfo tryGetValue = players.GetType().GetMethod("TryGetValue");
            object[] args = { clientId, null };
            bool found = (bool)tryGetValue.Invoke(players, args);
            Assert.IsTrue(found, $"找不到玩家 {clientId}");
            return args[1];
        }

        private Vector3 GetPlayerPosition(ulong clientId)
        {
            object player = GetPlayerState(clientId);
            return (Vector3)player.GetType().GetField("Position").GetValue(player);
        }

        private bool GetPlayerAlive(ulong clientId)
        {
            object player = GetPlayerState(clientId);
            return Convert.ToBoolean(player.GetType().GetField("Alive").GetValue(player));
        }

        private void AddBody(int bodyId, ulong victimClientId, Vector3 position, bool reported)
        {
            object bodies = GetBodiesList();
            object body = Activator.CreateInstance(
                RuntimeType("GanglandUndercover.Online.OnlineBodyState"),
                bodyId,
                victimClientId,
                position,
                reported);
            Invoke(bodies, "Add", body);
        }

        private void ClearBodies()
        {
            object bodies = GetBodiesList();
            Invoke(bodies, "Clear");
        }

        private object GetBodiesList()
        {
            InvokePrivate("EnsureRuntimeDependencies");
            object killSystem = GetField("killSystem");
            Assert.IsNotNull(killSystem, "killSystem 应在 PlayMode 中初始化。");
            FieldInfo fi = killSystem.GetType().GetField(
                "bodies",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "找不到 killSystem.bodies");
            object bodies = fi.GetValue(killSystem);
            Assert.IsNotNull(bodies, "killSystem.bodies 不应为 null。");
            return bodies;
        }

        private void AddVote(ulong voterClientId, ulong targetClientId)
        {
            IDictionary votes = (IDictionary)GetField("votes");
            votes[voterClientId] = targetClientId;
        }

        private void ClearVotes()
        {
            IDictionary votes = (IDictionary)GetField("votes");
            votes.Clear();
        }

        private void SetKillSystemField(string name, object value)
        {
            object killSystem = GetField("killSystem");
            Assert.IsNotNull(killSystem, "killSystem 应在 PlayMode Awake 中初始化。");
            FieldInfo fi = killSystem.GetType().GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到 killSystem 字段 {name}");
            fi.SetValue(killSystem, value);
        }

        private void Invoke(string method, params object[] args)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {method}");
            mi.Invoke(_controller, args);
        }

        private void InvokePublic(string method, params object[] args)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {method}");
            mi.Invoke(_controller, args);
        }

        private object InvokePublicWithResult(string method, params object[] args)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {method}");
            return mi.Invoke(_controller, args);
        }

        private bool InvokeBoolOutString(string methodName, ulong clientId, int taskId, out string message)
        {
            object[] args = { clientId, taskId, string.Empty };
            MethodInfo mi = _controllerType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {methodName}");
            bool accepted = (bool)mi.Invoke(_controller, args);
            message = (string)args[2];
            return accepted;
        }

        private void InvokePrivate(string method, params object[] args)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到私有方法 {method}");
            mi.Invoke(_controller, args);
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo mi = target.GetType().GetMethod(method);
            Assert.IsNotNull(mi, $"找不到方法 {target.GetType().Name}.{method}");
            mi.Invoke(target, args);
        }

        private bool TaskLockOwnedBy(int taskId, ulong ownerId)
        {
            object locks = GetField("activeTaskUsers");
            if (locks == null)
            {
                return false;
            }

            MethodInfo tryGetValue = locks.GetType().GetMethod("TryGetValue");
            object[] args = { taskId, null };
            bool found = (bool)tryGetValue.Invoke(locks, args);
            return found && Convert.ToUInt64(args[1]) == ownerId;
        }

        private static bool DictionaryContainsKey(object dictionary, ulong key)
        {
            MethodInfo containsKey = dictionary.GetType().GetMethod("ContainsKey");
            return (bool)containsKey.Invoke(dictionary, new object[] { key });
        }

        private static bool DictionaryContainsValue(object dictionary, ulong value)
        {
            foreach (object entry in (System.Collections.IEnumerable)dictionary)
            {
                object entryValue = entry.GetType().GetProperty("Value").GetValue(entry);
                if (Convert.ToUInt64(entryValue) == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetCollectionCount(object collection)
        {
            PropertyInfo count = collection.GetType().GetProperty("Count");
            return Convert.ToInt32(count.GetValue(collection));
        }

        private static bool CollectionContainsString(object collection, string expected)
        {
            foreach (object item in (System.Collections.IEnumerable)collection)
            {
                if (item != null && item.ToString().Contains(expected))
                {
                    return true;
                }
            }

            return false;
        }

        private Button FindRuntimeButton(string objectName)
        {
            Button[] buttons = _host.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.name == objectName)
                {
                    return button;
                }
            }

            return null;
        }

        private static Type RuntimeType(string fullName)
            => Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);

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

        private int GetInt(string name) => Convert.ToInt32(GetProp(name));
        private float GetFloat(string name) => Convert.ToSingle(GetProp(name));
        private bool GetBool(string name) => (bool)GetProp(name);
        private string GetString(string name) => (string)GetProp(name);
        private string GetPhaseName() => GetProp("Phase").ToString();

        private void AssertPhase(string expected, string because)
            => Assert.AreEqual(expected, GetPhaseName(), because);
    }
}
