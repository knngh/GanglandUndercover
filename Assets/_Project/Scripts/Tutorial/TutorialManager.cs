using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GanglandUndercover.Tutorial
{
    /// <summary>
    /// 教程管理器 — 多步骤线性教程流程控制器。
    /// 管理 Welcome → 移动 → 任务 → 报告 → 投票 → 小游戏 → 聊天 → 完成
    /// 八个阶段的完整生命周期。支持跳过全部、单步跳过和进度查询。
    ///
    /// 首次启动检测：通过 PlayerPrefs 记录首次完成标记，
    /// 配合场景入口在 Awake 中自动触发教程。
    /// </summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════
        // 序列化配置
        // ══════════════════════════════════════════════════════

        [Header("步骤配置")]
        [Tooltip("教程步骤列表，按顺序执行。每个元素为 TutorialStep ScriptableObject。")]
        [SerializeField] private List<TutorialStep> _steps = new List<TutorialStep>();

        [Header("首次启动")]
        [Tooltip("PlayerPrefs 键名，用于记录教程是否已完成。")]
        [SerializeField] private string _completedKey = "Tutorial_Completed";

        [Tooltip("是否在 Start 时自动检测并启动教程（首次启动）。")]
        [SerializeField] private bool _autoStartOnFirstLaunch = true;

        // ══════════════════════════════════════════════════════
        // 事件
        // ══════════════════════════════════════════════════════

        [Header("事件")]
        [Tooltip("步骤切换事件，参数为新的步骤索引和步骤定义。")]
        public UnityEvent<int, TutorialStep> OnStepEntered;

        [Tooltip("步骤退出事件，参数为刚刚完成的步骤索引。")]
        public UnityEvent<int> OnStepExited;

        [Tooltip("教程全部完成事件。")]
        public UnityEvent OnTutorialCompleted;

        [Tooltip("教程被跳过事件。")]
        public UnityEvent OnTutorialSkipped;

        // ══════════════════════════════════════════════════════
        // 运行时状态
        // ══════════════════════════════════════════════════════

        private int _currentStepIndex = -1;
        private bool _isRunning;
        private bool _hasCompleted;
        private Coroutine _autoAdvanceCoroutine;
        private Coroutine _waitConditionCoroutine;

        // 用于外部脚本通知条件达成的委托
        private Func<bool> _activeConditionChecker;

        // ══════════════════════════════════════════════════════
        // 公共属性
        // ══════════════════════════════════════════════════════

        /// <summary>当前步骤索引（0-based），未启动时为 -1。</summary>
        public int CurrentStepIndex => _currentStepIndex;

        /// <summary>教程总步骤数。</summary>
        public int TotalSteps => _steps.Count;

        /// <summary>教程是否正在运行中。</summary>
        public bool IsRunning => _isRunning;

        /// <summary>教程是否已完成（含跳过）。</summary>
        public bool HasCompleted => _hasCompleted;

        /// <summary>当前步骤定义，未启动或已完成时返回 null。</summary>
        public TutorialStep CurrentStep =>
            (_isRunning && _currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
                ? _steps[_currentStepIndex]
                : null;

        // ══════════════════════════════════════════════════════
        // 生命周期
        // ══════════════════════════════════════════════════════

        private void Start()
        {
            if (_autoStartOnFirstLaunch && !IsTutorialCompleted())
            {
                StartTutorial();
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        // ══════════════════════════════════════════════════════
        // 公开 API — 生命周期控制
        // ══════════════════════════════════════════════════════

        /// <summary>启动教程，从第一步开始。</summary>
        public void StartTutorial()
        {
            if (_steps == null || _steps.Count == 0)
            {
                Debug.LogWarning("[TutorialManager] 教程步骤列表为空，无法启动。");
                return;
            }

            if (_isRunning)
            {
                Debug.LogWarning("[TutorialManager] 教程已在运行中，忽略重复启动。");
                return;
            }

            _isRunning = true;
            _hasCompleted = false;
            _currentStepIndex = -1;
            AdvanceToStep(0);
        }

        /// <summary>停止教程并清理状态。</summary>
        public void StopTutorial()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _hasCompleted = false;
            _currentStepIndex = -1;
            _activeConditionChecker = null;

            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = null;
            }

            if (_waitConditionCoroutine != null)
            {
                StopCoroutine(_waitConditionCoroutine);
                _waitConditionCoroutine = null;
            }
        }

        /// <summary>手动推进到下一步。外部脚本在条件达成后调用。</summary>
        public void Advance()
        {
            if (!_isRunning) return;
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            // 当前步骤不可跳过时忽略手动推进
            TutorialStep current = _steps[_currentStepIndex];
            if (current.WaitCondition == TutorialWaitCondition.Manual)
            {
                CompleteCurrentStep();
            }
        }

        /// <summary>跳过当前步骤，直接进入下一步。</summary>
        public void SkipCurrentStep()
        {
            if (!_isRunning) return;
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            TutorialStep current = _steps[_currentStepIndex];
            if (!current.Skippable)
            {
                Debug.Log($"[TutorialManager] 步骤 '{current.StepName}' 不允许跳过。");
                return;
            }

            CompleteCurrentStep();
        }

        /// <summary>跳过全部教程。</summary>
        public void SkipAll()
        {
            if (!_isRunning) return;

            // 检查是否有不可跳过的步骤
            foreach (TutorialStep step in _steps)
            {
                if (!step.Skippable)
                {
                    Debug.Log($"[TutorialManager] 步骤 '{step.StepName}' 不可跳过，无法跳过全部教程。");
                    return;
                }
            }

            StopTutorial();
            MarkTutorialCompleted();
            _hasCompleted = true;
            OnTutorialSkipped?.Invoke();
        }

        /// <summary>教程是否已完成（PlayerPrefs 持久化标记）。</summary>
        public bool IsTutorialCompleted()
        {
            return PlayerPrefs.GetInt(_completedKey, 0) == 1;
        }

        // ══════════════════════════════════════════════════════
        // 公开 API — 条件注册
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 注册一个外部条件检查器。
        /// 用于 WaitForMove / WaitForTask 等需要外部脚本通知完成的条件类型。
        /// 调用者在 Update 中检查此委托。
        /// </summary>
        public void RegisterConditionChecker(Func<bool> checker)
        {
            _activeConditionChecker = checker;
        }

        /// <summary>清除当前条件检查器。</summary>
        public void ClearConditionChecker()
        {
            _activeConditionChecker = null;
        }

        // ══════════════════════════════════════════════════════
        // 内部 — 步骤推进
        // ══════════════════════════════════════════════════════

        private void CompleteCurrentStep()
        {
            int completedIndex = _currentStepIndex;

            // 清理当前步骤的协程
            ClearStepCoroutines();

            // 触发退出事件
            OnStepExited?.Invoke(completedIndex);

            // 推进到下一步
            int nextIndex = completedIndex + 1;
            if (nextIndex >= _steps.Count)
            {
                FinishTutorial();
                return;
            }

            AdvanceToStep(nextIndex);
        }

        private void AdvanceToStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= _steps.Count) return;

            _currentStepIndex = stepIndex;
            TutorialStep step = _steps[stepIndex];

            // 触发进入事件
            OnStepEntered?.Invoke(stepIndex, step);

            // 根据完成条件启动对应的等待逻辑
            StartStepWaitCondition(step);
        }

        private void StartStepWaitCondition(TutorialStep step)
        {
            ClearStepCoroutines();

            switch (step.WaitCondition)
            {
                case TutorialWaitCondition.AutoAdvance:
                case TutorialWaitCondition.WaitForTime:
                    _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine(step.AutoDelay));
                    break;

                case TutorialWaitCondition.WaitForClick:
                    // 由 TutorialUI 处理点击，完成后调用 Advance()
                    _waitConditionCoroutine = StartCoroutine(WaitForClickRoutine(step));
                    break;

                case TutorialWaitCondition.WaitForMove:
                case TutorialWaitCondition.WaitForTask:
                case TutorialWaitCondition.WaitForReport:
                case TutorialWaitCondition.WaitForVote:
                case TutorialWaitCondition.WaitForMinigame:
                case TutorialWaitCondition.WaitForChat:
                    // 由外部游戏逻辑调用 Advance()
                    _waitConditionCoroutine = StartCoroutine(PollConditionRoutine(step));
                    break;

                case TutorialWaitCondition.Manual:
                    // 等待外部脚本调用 Advance()
                    break;
            }
        }

        private void ClearStepCoroutines()
        {
            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = null;
            }

            if (_waitConditionCoroutine != null)
            {
                StopCoroutine(_waitConditionCoroutine);
                _waitConditionCoroutine = null;
            }
        }

        private void FinishTutorial()
        {
            StopTutorial();
            MarkTutorialCompleted();
            _hasCompleted = true;
            OnTutorialCompleted?.Invoke();
            Debug.Log("[TutorialManager] 教程完成。");
        }

        private void MarkTutorialCompleted()
        {
            PlayerPrefs.SetInt(_completedKey, 1);
            PlayerPrefs.Save();
        }

        // ══════════════════════════════════════════════════════
        // 协程 — 等待逻辑
        // ══════════════════════════════════════════════════════

        private IEnumerator AutoAdvanceRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            CompleteCurrentStep();
        }

        private IEnumerator WaitForClickRoutine(TutorialStep step)
        {
            // 点击由 TutorialUI 中的 Input 检测驱动，
            // 检测到有效点击后调用 Advance()。
            // 此处仅作为保底：如果 30 秒无操作则自动推进。
            float elapsed = 0f;
            while (elapsed < 30f)
            {
                yield return null;
                elapsed += Time.deltaTime;

                // 如果在等待期间步骤已推进，则退出
                if (!_isRunning || _currentStepIndex >= _steps.Count ||
                    CurrentStep != step)
                {
                    yield break;
                }
            }

            // 超时自动推进
            Debug.Log($"[TutorialManager] 步骤 '{step.StepName}' 点击等待超时，自动推进。");
            CompleteCurrentStep();
        }

        private IEnumerator PollConditionRoutine(TutorialStep step)
        {
            while (_isRunning)
            {
                yield return null;

                if (_activeConditionChecker != null && _activeConditionChecker.Invoke())
                {
                    _activeConditionChecker = null;
                    CompleteCurrentStep();
                    yield break;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 调试
        // ══════════════════════════════════════════════════════

        /// <summary>重置教程完成状态（调试用，清除 PlayerPrefs 记录）。</summary>
        [ContextMenu("Reset Tutorial Completion")]
        public void ResetCompletion()
        {
            PlayerPrefs.DeleteKey(_completedKey);
            PlayerPrefs.Save();
            Debug.Log("[TutorialManager] 教程完成状态已重置。");
        }
    }
}
