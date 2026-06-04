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

        private int GetInt(string name) => Convert.ToInt32(GetProp(name));
        private bool GetBool(string name) => (bool)GetProp(name);
        private string GetString(string name) => (string)GetProp(name);
        private string GetPhaseName() => GetProp("Phase").ToString();

        private void AssertPhase(string expected, string because)
            => Assert.AreEqual(expected, GetPhaseName(), because);
    }
}
