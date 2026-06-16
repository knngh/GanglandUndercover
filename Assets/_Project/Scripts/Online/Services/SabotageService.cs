using System;
using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online.Services
{
    /// <summary>
    /// SabotageService — 破坏系统服务。
    /// 整合 SabotageSync 逻辑与 OnlineMatchController 中的破坏相关方法。
    /// 
    /// 职责：
    /// - 破坏触发（5 种类型：Blackout / Lockdown / Communications / EvidenceLeak / PatrolAlert）
    /// - 冷却管理（每种破坏的冷却计时器）
    /// - 修复检测（Crewmate 修复任务完成后清除效果）
    /// - 紧急任务倒计时联动
    /// - 与 OnlineRuleSet 的数值联动
    /// - 通过 IGameEventBus 发布 SabotageTriggeredEvent / SabotageResolvedEvent
    /// </summary>
    public sealed class SabotageService : MonoBehaviour
    {
        // ─── 配置引用 ──────────────────────────────────────────

        [Header("── 依赖引用 ──")]
        [Tooltip("OnlineMatchController 引用，用于访问共享状态")]
        [SerializeField] private OnlineMatchController controller;

        [Tooltip("事件总线引用")]
        [SerializeField] private SimpleGameEventBus eventBus;

        // ─── 冷却状态 ──────────────────────────────────────────

        /// <summary>各类型破坏的剩余活跃时间（秒）。> 0 表示该破坏正在生效。</summary>
        private readonly Dictionary<SabotageType, float> activeTimers = new Dictionary<SabotageType, float>
        {
            { SabotageType.Blackout, 0f },
            { SabotageType.Lockdown, 0f },
            { SabotageType.Communications, 0f },
            { SabotageType.EvidenceLeak, 0f },
            { SabotageType.PatrolAlert, 0f },
        };

        /// <summary>破坏冷却（Gang 触发后需等待冷却才能再次触发同类型）。</summary>
        private readonly Dictionary<SabotageType, float> cooldownTimers = new Dictionary<SabotageType, float>
        {
            { SabotageType.Blackout, 0f },
            { SabotageType.Lockdown, 0f },
            { SabotageType.Communications, 0f },
            { SabotageType.EvidenceLeak, 0f },
            { SabotageType.PatrolAlert, 0f },
        };

        // ─── 公开只读属性 ──────────────────────────────────────

        /// <summary>停电计时器（供 SabotageSync / HUD 读取）。</summary>
        public float BlackoutTimer => GetTimer(SabotageType.Blackout);

        /// <summary>封锁计时器。</summary>
        public float LockdownTimer => GetTimer(SabotageType.Lockdown);

        /// <summary>通讯干扰计时器。</summary>
        public float CommunicationJamTimer => GetTimer(SabotageType.Communications);

        /// <summary>证据泄露计时器。</summary>
        public float EvidenceLeakTimer => GetTimer(SabotageType.EvidenceLeak);

        /// <summary>巡逻警报计时器。</summary>
        public float PatrolAlertTimer => GetTimer(SabotageType.PatrolAlert);

        // ─── 生命周期 ──────────────────────────────────────────

        private void Awake()
        {
            if (eventBus == null)
            {
                eventBus = SimpleGameEventBus.Instance;
            }
        }

        private void OnEnable()
        {
            if (eventBus != null)
            {
                eventBus.Subscribe<TaskCompletedEvent>(OnTaskCompleted);
            }
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.Unsubscribe<TaskCompletedEvent>(OnTaskCompleted);
            }
        }

        // ─── 公开 API ──────────────────────────────────────────

        /// <summary>
        /// 初始化服务引用。由 OnlineMatchController 调用。
        /// </summary>
        public void Initialize(OnlineMatchController matchController, IGameEventBus bus)
        {
            controller = matchController;
            eventBus = bus as SimpleGameEventBus ?? SimpleGameEventBus.Instance;
        }

        /// <summary>
        /// 每帧 tick，递减活跃计时器和冷却计时器。
        /// 由 OnlineMatchController.TickHostSimulation() 调用。
        /// </summary>
        public void Tick(float deltaTime)
        {
            foreach (SabotageType type in Enum.GetValues(typeof(SabotageType)))
            {
                if (type == SabotageType.None) continue;

                // 递减活跃计时器
                if (activeTimers.TryGetValue(type, out float activeTime) && activeTime > 0f)
                {
                    activeTimers[type] = Mathf.Max(0f, activeTime - deltaTime);

                    // 计时器归零 → 破坏自动结束
                    if (activeTimers[type] <= 0f)
                    {
                        OnSabotageExpired(type);
                    }
                }

                // 递减冷却计时器
                if (cooldownTimers.TryGetValue(type, out float coolTime) && coolTime > 0f)
                {
                    cooldownTimers[type] = Mathf.Max(0f, coolTime - deltaTime);
                }
            }
        }

        /// <summary>
        /// 触发破坏效果。仅服务器端调用。
        /// 检查冷却、应用效果、发布 SabotageTriggeredEvent。
        /// </summary>
        /// <param name="type">破坏类型。</param>
        /// <param name="initiatorId">发起者 ClientId。</param>
        /// <param name="taskName">关联的任务名称（用于日志）。</param>
        public void TriggerSabotage(SabotageType type, ulong initiatorId, string taskName)
        {
            if (type == SabotageType.None) return;
            if (controller == null) return;

            // 冷却检查
            if (cooldownTimers.TryGetValue(type, out float cool) && cool > 0f)
            {
                return;
            }

            // 已在生效中则不重复触发
            if (activeTimers.TryGetValue(type, out float active) && active > 0f)
            {
                return;
            }

            // 获取持续时间（从 RuleSet）
            float duration = GetSabotageDuration(type);
            activeTimers[type] = duration;

            // 设置冷却（触发后冷却 = 持续时间 + 额外冷却）
            float cooldown = duration + GetSabotageExtraCooldown(type);
            cooldownTimers[type] = cooldown;

            // 发布事件
            eventBus?.Publish(new SabotageTriggeredEvent
            {
                Type = type,
                InitiatorId = initiatorId,
            });
        }

        /// <summary>
        /// 修复指定类型的破坏。由 Crewmate 完成任务或能力触发。
        /// </summary>
        public void RepairSabotage(SabotageType type)
        {
            if (type == SabotageType.None) return;

            if (activeTimers.TryGetValue(type, out float timer) && timer > 0f)
            {
                activeTimers[type] = 0f;

                eventBus?.Publish(new SabotageResolvedEvent { Type = type });
            }
        }

        /// <summary>
        /// 重置所有破坏计时器和冷却。对局开始或结束时调用。
        /// </summary>
        public void ResetAll()
        {
            foreach (SabotageType type in activeTimers.Keys)
            {
                activeTimers[type] = 0f;
            }

            foreach (SabotageType type in cooldownTimers.Keys)
            {
                cooldownTimers[type] = 0f;
            }
        }

        /// <summary>
        /// 获取指定类型的活跃计时器值。
        /// </summary>
        public float GetTimer(SabotageType type)
        {
            return activeTimers.TryGetValue(type, out float value) ? value : 0f;
        }

        /// <summary>
        /// 判断是否有任何活跃破坏。
        /// </summary>
        public bool HasActiveSabotage()
        {
            foreach (var kv in activeTimers)
            {
                if (kv.Value > 0f) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有活跃破坏及其剩余时间。
        /// </summary>
        public Dictionary<SabotageType, float> GetActiveSabotages()
        {
            var result = new Dictionary<SabotageType, float>();
            foreach (var kv in activeTimers)
            {
                if (kv.Value > 0f) result[kv.Key] = kv.Value;
            }
            return result;
        }

        /// <summary>
        /// 从快照数据恢复计时器（主机迁移用）。
        /// </summary>
        public void LoadFromSnapshot(float blackout, float lockdown, float commJam,
            float evidenceLeak, float patrolAlert)
        {
            activeTimers[SabotageType.Blackout] = blackout;
            activeTimers[SabotageType.Lockdown] = lockdown;
            activeTimers[SabotageType.Communications] = commJam;
            activeTimers[SabotageType.EvidenceLeak] = evidenceLeak;
            activeTimers[SabotageType.PatrolAlert] = patrolAlert;
        }

        // ─── 内部方法 ──────────────────────────────────────────

        /// <summary>破坏计时器归零时的回调。</summary>
        private void OnSabotageExpired(SabotageType type)
        {
            eventBus?.Publish(new SabotageResolvedEvent { Type = type });
        }

        /// <summary>任务完成时检查是否修复对应变化的破坏。</summary>
        private void OnTaskCompleted(TaskCompletedEvent evt)
        {
            // TODO: 根据 taskId 映射到 SabotageType，调用 RepairSabotage
            // 映射逻辑来自 OnlineMatchController.SabotageForTask()
        }

        /// <summary>
        /// 获取破坏持续时间（从 RuleSet 读取，回退到默认值）。
        /// </summary>
        private float GetSabotageDuration(SabotageType type)
        {
            // TODO: 接入 OnlineRuleSet 的具体配置字段
            // 当前回退值与 OnlineTaskService 中的计时器逻辑保持一致
            switch (type)
            {
                case SabotageType.Blackout: return 30f;
                case SabotageType.Lockdown: return 45f;
                case SabotageType.Communications: return 40f;
                case SabotageType.EvidenceLeak: return 35f;
                case SabotageType.PatrolAlert: return 25f;
                default: return 30f;
            }
        }

        /// <summary>
        /// 获取破坏结束后的额外冷却时间。
        /// </summary>
        private float GetSabotageExtraCooldown(SabotageType type)
        {
            // TODO: 接入 OnlineRuleSet 的具体配置字段
            switch (type)
            {
                case SabotageType.Blackout: return 15f;
                case SabotageType.Lockdown: return 20f;
                case SabotageType.Communications: return 15f;
                case SabotageType.EvidenceLeak: return 10f;
                case SabotageType.PatrolAlert: return 10f;
                default: return 15f;
            }
        }
    }
}
