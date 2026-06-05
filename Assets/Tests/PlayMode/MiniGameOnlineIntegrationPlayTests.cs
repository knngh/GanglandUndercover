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
    /// Task#7 PlayMode 自动化：验证离线小游戏（连线/刷卡/记忆/扫描…）已真正接入联机现场任务。
    ///
    /// 真实运行流程：玩家走到任务点按 E → OnlineMatchController.BeginActiveTask(taskId)
    /// → 创建对应的 Among Us 风格小游戏并自建 ScreenSpaceOverlay Canvas 接管交互
    /// → 完成后走与经典面板一致的 CompleteActiveTask（服务器提交）路径。
    ///
    /// 无头批处理无法点击小游戏 UI，故通过 Editor* 驱动钩子打开/强制完成，
    /// 断言：①不同任务点能开出 >= 6 种不同小游戏（对标 Among Us 多样性）；
    ///       ②完成后任务句柄归零、前台小游戏被回收（闭环成立）。
    ///
    /// 运行时类型仍在预定义的 Assembly-CSharp，通过反射访问，与 MatchLoopPlayTests 风格一致。
    /// </summary>
    public class MiniGameOnlineIntegrationPlayTests
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

            _host = new GameObject("MiniGameTest_OnlineMatchHost");
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
        public IEnumerator OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath()
        {
            // Awake 在 AddComponent 当帧排队；让它跑一帧，BuildDefaultTasks 铺满任务站点。
            yield return null;

            int taskCount = GetInt("TaskCount");
            Assert.GreaterOrEqual(taskCount, 28, "大地图固定任务站点应铺满 28 个（接入小游戏的前提）。");

            // 遍历前 13 个任务点（其 Id 恰好覆盖小游戏工厂的 13 个轮转桶 → 11 种类型）。
            HashSet<string> distinctMinigames = new HashSet<string>(StringComparer.Ordinal);
            int probed = Math.Min(13, taskCount);

            for (int taskId = 0; taskId < probed; taskId++)
            {
                // ① 打开任务点对应的小游戏。
                string minigameName = InvokeStringWithInt("EditorOpenTaskMiniGameForSmokeTest", taskId);

                // 个别任务点若退回经典面板（类型名为空）不算硬失败：那是设计内的降级路径。
                // 真正要证明的是整体多样性（>= 6 种）与单局闭环（开→完成→回收）成立。
                if (string.IsNullOrEmpty(minigameName))
                {
                    continue;
                }

                Assert.IsTrue(GetBool("HasActiveMiniGame"),
                    $"任务点 {taskId} 开出小游戏后应有前台小游戏在运行。");
                Assert.AreEqual(taskId, GetInt("ActiveTaskId"),
                    $"激活任务句柄应指向任务点 {taskId}。");

                distinctMinigames.Add(minigameName);

                // ② 强制完成 → 走 CompleteActiveTask 服务器提交路径。
                bool completed = InvokeBool("EditorForceCompleteActiveMiniGameForSmokeTest");
                Assert.IsTrue(completed, $"任务点 {taskId} 的小游戏应能被完成。");

                // CompleteActiveTask 把 activeTaskId 置 -1；Update 下一帧回收前台小游戏对象。
                yield return null;
                Assert.AreEqual(-1, GetInt("ActiveTaskId"),
                    $"任务点 {taskId} 完成后任务句柄应归零。");
                Assert.IsFalse(GetBool("HasActiveMiniGame"),
                    $"任务点 {taskId} 完成后前台小游戏应被回收。");
            }

            // ③ 多样性断言：对标 Among Us，至少 6 种不同小游戏接入联机现场任务。
            Assert.GreaterOrEqual(distinctMinigames.Count, 6,
                "联机现场任务应至少接入 6 种不同的小游戏（实际开出："
                    + string.Join("/", distinctMinigames) + "）。");

            Debug.Log("[MiniGameTest] 联机现场任务已接入小游戏种类（" + distinctMinigames.Count + "）："
                + string.Join("/", distinctMinigames));
        }

        // ──────────────────────────────────────────────────────────
        //  反射访问辅助（与 MatchLoopPlayTests 一致）
        // ──────────────────────────────────────────────────────────

        private bool InvokeBool(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}()");
            return (bool)mi.Invoke(_controller, null);
        }

        private string InvokeStringWithInt(string method, int arg)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}(int)");
            return (string)mi.Invoke(_controller, new object[] { arg });
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
    }
}
