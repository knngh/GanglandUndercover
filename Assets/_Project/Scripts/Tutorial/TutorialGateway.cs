using System.Collections.Generic;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.Tutorial
{
    /// <summary>
    /// M9.1 教程网关 — 桥接 TutorialManager 与 OnlineMatchController。
    /// 运行时创建 8 步骤教程（不依赖 ScriptableObject 资源），
    /// 首次启动自动触发，大厅可重看/跳过。
    ///
    /// 挂载到场景中的 TutorialManager 所在的 GameObject 上，
    /// 或由 PrototypeBootstrap 动态添加。
    /// </summary>
    [RequireComponent(typeof(TutorialManager))]
    public sealed class TutorialGateway : MonoBehaviour
    {
        private TutorialManager _manager;
        private Online.OnlineMatchController _matchController;

        private void Awake()
        {
            _manager = GetComponent<TutorialManager>();
            PopulateSteps();

            // 监听完成/跳过
            _manager.OnTutorialCompleted.AddListener(() => Debug.Log("[TutorialGateway] Tutorial completed."));
            _manager.OnTutorialSkipped.AddListener(() => Debug.Log("[TutorialGateway] Tutorial skipped."));
        }

        private void Start()
        {
            _matchController = FindAnyObjectByType<Online.OnlineMatchController>();
        }

        // ══════════════════════════════════════════════════════
        // 公开 API
        // ══════════════════════════════════════════════════════

        /// <summary>重看教程（清除完成标记后重启）</summary>
        public void RestartTutorial()
        {
            _manager.ResetCompletion();
            PopulateSteps();
            _manager.StartTutorial();
        }

        /// <summary>当前是否在运行教程</summary>
        public bool IsTutorialActive => _manager.IsRunning;

        // ══════════════════════════════════════════════════════
        // 步骤生成 — 运行时创建 8 个 TutorialStep
        // ══════════════════════════════════════════════════════

        private void PopulateSteps()
        {
            var steps = new List<TutorialStep>
            {
                CreateStep("tut_welcome", "欢迎", "欢迎来到九龙港城。你将扮演卧底、黑帮或警察，在港城展开暗战。\n\n点击任意位置继续。",
                    TutorialWaitCondition.WaitForClick, 3f, Vector2.zero, true),

                CreateStep("tut_move", "移动", "使用 WASD 键移动角色。走到地图上的任务点开始行动。\n\n请移动一段距离…",
                    TutorialWaitCondition.AutoAdvance, 3f, Vector2.zero, true),

                CreateStep("tut_task", "接取任务", "走到蓝色任务点旁按 E 键接取任务。完成小游戏来推进警察的证据进度。\n\n是时候行动了！",
                    TutorialWaitCondition.AutoAdvance, 4f, Vector2.zero, true),

                CreateStep("tut_report", "报告与会议", "发现尸体时按 R 键报案。会议中查看证据、投票淘汰嫌疑人。\n\n留意地图上的尸体标记。",
                    TutorialWaitCondition.AutoAdvance, 4f, Vector2.zero, true),

                CreateStep("tut_vote", "投票", "投票阶段选择你怀疑的玩家。可以投票或跳过。\n\n高嫌疑值的玩家会被优先调查。",
                    TutorialWaitCondition.AutoAdvance, 4f, Vector2.zero, true),

                CreateStep("tut_minigame", "小游戏", "任务小游戏：按住扫描键（空格）不放，按顺序校验按键。\n\n按住扫描，看到校验通过就成功了。",
                    TutorialWaitCondition.AutoAdvance, 4f, Vector2.zero, true),

                CreateStep("tut_chat", "沟通", "在会议中可以与队友沟通。注意保护自己的身份。\n\n语音或文字，合理表达你的推理。",
                    TutorialWaitCondition.AutoAdvance, 3f, Vector2.zero, true),

                CreateStep("tut_done", "准备就绪", "你已经掌握了核心玩法！\n\n可以创建房间开始真正的对局了。\n\n祝你好运！",
                    TutorialWaitCondition.AutoAdvance, 4f, Vector2.zero, true),
            };

            // 通过反射设置 TutorialManager 的 _steps（因为它是 SerializeField 私有列表）
            var field = typeof(TutorialManager).GetField("_steps",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(_manager, steps);
            }
            else
            {
                Debug.LogWarning("[TutorialGateway] Cannot set steps via reflection — field '_steps' not found.");
            }
        }

        private static TutorialStep CreateStep(
            string id, string name, string tip,
            TutorialWaitCondition condition, float delay,
            Vector2 tipOffset, bool skippable)
        {
            var step = ScriptableObject.CreateInstance<TutorialStep>();
            SetPrivateField(step, "_stepId", id);
            SetPrivateField(step, "_stepName", name);
            SetPrivateField(step, "_tipText", tip);
            SetPrivateField(step, "_waitCondition", condition);
            SetPrivateField(step, "_autoDelay", delay);
            SetPrivateField(step, "_tipOffset", tipOffset);
            SetPrivateField(step, "_skippable", skippable);
            return step;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
