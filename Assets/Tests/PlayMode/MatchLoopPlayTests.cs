using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        private void Invoke(string method, params object[] args)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {method}");
            mi.Invoke(_controller, args);
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

        private static Type RuntimeType(string fullName)
            => Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);

        private int GetInt(string name) => Convert.ToInt32(GetProp(name));
        private bool GetBool(string name) => (bool)GetProp(name);
        private string GetString(string name) => (string)GetProp(name);
        private string GetPhaseName() => GetProp("Phase").ToString();

        private void AssertPhase(string expected, string because)
            => Assert.AreEqual(expected, GetPhaseName(), because);
    }
}
