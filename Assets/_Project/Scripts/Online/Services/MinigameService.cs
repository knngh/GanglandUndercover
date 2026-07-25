using System;
using UnityEngine;
using GanglandUndercover.SocialDeduction.MiniGames;

namespace GanglandUndercover.Online.Services
{
    /// <summary>
    /// MinigameService — 现场任务迷你游戏状态机。
    /// 统一管理活动任务（activeTask）的生命周期：开始、步骤校验、蓄力、完成、取消。
    ///
    /// 职责：
    /// - 持有迷你游戏全部运行时状态（9 字段 + submittingActiveTask + activeMiniGame）
    /// - 纯逻辑判定：步骤校验（CorrectTaskStepInput）、蓄力累积、完成条件检测
    /// - 迷你游戏对象生命周期（创建 / 销毁 MiniGameBase 实例）
    /// - 通过事件通知 controller 执行副作用（status、网络提交、音频）
    ///
    /// 设计约束：
    /// - 不直接读写 controller 字段（通过事件 / 返回值）
    /// - 不参与网络同步（活动任务状态为本地玩家私有）
    /// - 静态数据（TaskChargeRate / CorrectTaskStepInput）仍由 OnlineMatchUtils 提供
    /// </summary>
    public sealed class MinigameService : MonoBehaviour
    {
        // ─── 运行时状态 ──────────────────────────────────────────

        private int activeTaskId = -1;
        private int activeTaskStep;
        private int activeTaskMistakes;
        private float activeTaskCharge;
        private float activeTaskFeedbackTimer;
        private bool activeTaskStepOneDone;
        private bool activeTaskStepTwoDone;
        private bool activeTaskStepThreeDone;
        private bool activeTaskFeedbackPositive;
        private bool submittingActiveTask;

        /// <summary>Task#7：当前激活的 Among Us 风格小游戏实例（非空时接管交互）。</summary>
        private MiniGameBase activeMiniGame;

        // ─── 引用 ──────────────────────────────────────────────

        private OnlineMatchController controller;

        // ─── 事件（controller 订阅以执行副作用） ─────────────────

        /// <summary>迷你游戏开始（taskId）。</summary>
        public event Action<int> OnTaskStarted;

        /// <summary>步骤校验结果（step, success）。</summary>
        public event Action<int, bool> OnStepResolved;

        /// <summary>迷你游戏完成（taskId）— controller 应发送 Interact Action。</summary>
        public event Action<int> OnTaskCompleted;

        /// <summary>迷你游戏取消。</summary>
        public event Action OnTaskCancelled;

        /// <summary>状态描述变化（供 controller 写入 status / AddCaseLog）。</summary>
        public event Action<string> OnStatusChanged;

        // ─── 属性 ──────────────────────────────────────────────

        public int ActiveTaskId => activeTaskId;
        public bool HasActiveTask => activeTaskId >= 0;
        public int ActiveTaskStep => activeTaskStep;
        public float ActiveTaskCharge => activeTaskCharge;
        public int ActiveTaskMistakes => activeTaskMistakes;
        public float ActiveTaskFeedbackTimer => activeTaskFeedbackTimer;
        public bool ActiveTaskStepOneDone => activeTaskStepOneDone;
        public bool ActiveTaskStepTwoDone => activeTaskStepTwoDone;
        public bool ActiveTaskStepThreeDone => activeTaskStepThreeDone;
        public bool ActiveTaskFeedbackPositive => activeTaskFeedbackPositive;
        public bool SubmittingActiveTask => submittingActiveTask;
        public MiniGameBase ActiveMiniGame => activeMiniGame;
        public bool HasActiveMiniGame => activeMiniGame != null;
        public string ActiveMiniGameName => activeMiniGame != null ? activeMiniGame.GetType().Name : string.Empty;

        // ─── 生命周期 ──────────────────────────────────────────

        private void Awake()
        {
            controller = GetComponent<OnlineMatchController>();
        }

        // ─── 公开 API ──────────────────────────────────────────

        /// <summary>
        /// 开始一个任务的迷你游戏。重置所有状态并尝试打开小游戏实例。
        /// </summary>
        public void Begin(int taskId)
        {
            activeTaskId = taskId;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskStepOneDone = false;
            activeTaskStepTwoDone = false;
            activeTaskStepThreeDone = false;
            activeTaskMistakes = 0;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;

            TryOpenMiniGame(taskId);
            OnTaskStarted?.Invoke(taskId);
        }

        /// <summary>
        /// 取消当前迷你游戏，重置状态。
        /// </summary>
        public void Cancel()
        {
            ResetState();
            OnTaskCancelled?.Invoke();
            OnStatusChanged?.Invoke("已退出任务面板。");
        }

        /// <summary>
        /// 校验步骤输入。返回 true 表示全部完成（调用方应提交网络动作）。
        /// </summary>
        public bool ResolveStep(int input)
        {
            if (activeTaskId < 0) return false;

            if (input == OnlineMatchUtils.CorrectTaskStepInput(activeTaskId, activeTaskStep))
            {
                activeTaskStep++;
                activeTaskCharge = Mathf.Min(1f, activeTaskCharge + 0.28f);

                if (activeTaskStep == 1) activeTaskStepOneDone = true;
                else if (activeTaskStep == 2) activeTaskStepTwoDone = true;
                else activeTaskStepThreeDone = true;

                activeTaskFeedbackTimer = 0.42f;
                activeTaskFeedbackPositive = true;
                OnStepResolved?.Invoke(activeTaskStep, true);
                OnStatusChanged?.Invoke("任务校验 " + Mathf.Min(activeTaskStep, 3) + "/3 通过。");

                return CheckAndComplete();
            }

            // 错误
            activeTaskCharge = Mathf.Max(0f, activeTaskCharge - 0.18f);
            activeTaskMistakes++;
            activeTaskFeedbackTimer = 0.55f;
            activeTaskFeedbackPositive = false;
            OnStepResolved?.Invoke(activeTaskStep + 1, false);
            OnStatusChanged?.Invoke("校验不匹配，进度回退。");

            if (activeTaskMistakes >= 3)
            {
                activeTaskMistakes = 0;
                activeTaskCharge = 0f;
                OnStatusChanged?.Invoke("连续错误触发复核，任务进度清零重校。");
            }

            return false;
        }

        /// <summary>
        /// 蓄力增量（空格键持续按住时由 controller 每帧调用）。
        /// </summary>
        public void AddCharge(float deltaTime)
        {
            if (activeTaskId < 0) return;
            activeTaskCharge = Mathf.Min(1f, activeTaskCharge
                + deltaTime * OnlineMatchUtils.TaskChargeRate(activeTaskId));
        }

        /// <summary>
        /// 检测是否满足完成条件（蓄力满 + 三步全通过），满足则完成并返回 true。
        /// </summary>
        public bool CheckAndComplete()
        {
            if (activeTaskId < 0) return false;
            if (activeTaskCharge < 1f) return false;
            if (!activeTaskStepOneDone || !activeTaskStepTwoDone || !activeTaskStepThreeDone)
                return false;

            int completedTaskId = activeTaskId;
            ResetState();
            OnTaskCompleted?.Invoke(completedTaskId);
            OnStatusChanged?.Invoke("任务操作完成，已提交现场结果。");
            return true;
        }

        /// <summary>
        /// 每帧 tick：衰减反馈计时器。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (activeTaskFeedbackTimer > 0f)
            {
                activeTaskFeedbackTimer = Mathf.Max(0f, activeTaskFeedbackTimer - deltaTime);
            }
        }

        /// <summary>
        /// 硬重置（阶段切换 / 死亡 / 会议等场景）。不触发事件。
        /// </summary>
        public void Reset()
        {
            ResetState();
        }

        /// <summary>
        /// 硬重置含 submittingActiveTask（用于断线 / 回合结束）。
        /// </summary>
        public void ResetFull()
        {
            ResetState();
            submittingActiveTask = false;
        }

        /// <summary>
        /// 标记正在提交（controller 发送 Interact 前后设置）。
        /// </summary>
        public void SetSubmitting(bool value)
        {
            submittingActiveTask = value;
        }

        /// <summary>
        /// 销毁当前小游戏实例。
        /// </summary>
        public void DestroyMiniGame()
        {
            if (activeMiniGame == null) return;

            MiniGameBase mini = activeMiniGame;
            activeMiniGame = null;

            try { mini.Hide(); }
            catch (Exception) { /* Hide 清理失败不阻断销毁 */ }

            if (mini != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(mini.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(mini.gameObject);
            }
        }

        /// <summary>
        /// 处理小游戏被外部回收（activeTaskId 已置 -1 但 miniGame 仍存在）。
        /// 返回 true 表示确实有悬挂的小游戏被销毁。
        /// </summary>
        public bool CollectOrphanedMiniGame()
        {
            if (activeTaskId >= 0 || activeMiniGame == null) return false;
            DestroyMiniGame();
            return true;
        }

        // ─── 内部 ──────────────────────────────────────────────

        private void ResetState()
        {
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskStepOneDone = false;
            activeTaskStepTwoDone = false;
            activeTaskStepThreeDone = false;
            activeTaskMistakes = 0;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;
        }

        private void TryOpenMiniGame(int taskId)
        {
            DestroyMiniGame();

            try
            {
                MiniGameBase mini = Online.MiniGames.OnlineMiniGameBridge.CreateDefaultMinigame(taskId, transform);
                if (mini == null) return;

                mini.OnComplete = _ => OnMiniGameComplete();
                mini.OnCancel = _ => OnMiniGameCancel();
                mini.Show();
                activeMiniGame = mini;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MinigameService] 小游戏打开失败，回退经典任务面板：" + e.Message);
                DestroyMiniGame();
            }
        }

        private void OnMiniGameComplete()
        {
            int completedTaskId = activeTaskId;
            ResetState();
            OnTaskCompleted?.Invoke(completedTaskId);
            OnStatusChanged?.Invoke("任务操作完成，已提交现场结果。");
        }

        private void OnMiniGameCancel()
        {
            ResetState();
            OnTaskCancelled?.Invoke();
            OnStatusChanged?.Invoke("已退出任务面板。");
        }
    }
}
