using System;
using System.Collections.Generic;
using GanglandUndercover;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Sabotage 破坏同步系统：
    /// - Sabotage 触发 → 所有客户端同步 → UI 提示 Crewmate 修复
    /// - 监听 OnlineMatchController 的 sabotage 计时器变化
    /// - Crewmate 端显示修复任务提示
    /// - 修复任务绑定到 TaskSync 现有任务系统
    /// </summary>
    public sealed class SabotageSync : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private GameObject repairHintPanel;
        [SerializeField] private Text repairHintText;
        [SerializeField] private Image repairHintIcon;
        [SerializeField] private GameObject crewmateSabotageIndicator;

        [Header("提示配置")]
        [Tooltip("修复提示显示时长")]
        public float hintDisplayDuration = 4f;

        [Tooltip("破坏状态刷新间隔")]
        public float refreshInterval = 0.5f;

        // -------- 运行时状态 --------
        private OnlineMatchController controller;
        private readonly Dictionary<SabotageType, float> previousTimers = new Dictionary<SabotageType, float>();
        private readonly Dictionary<SabotageType, string> previousStatus = new Dictionary<SabotageType, string>();
        private float hintTimer;
        private float refreshTimer;
        private SabotageType lastTriggeredSabotage;

        // -------- 颜色常量 --------
        private static readonly Color ColorBlackout = new Color(0.05f, 0.08f, 0.18f, 0.88f);
        private static readonly Color ColorLockdown = new Color(0.22f, 0.08f, 0.06f, 0.88f);
        private static readonly Color ColorCommunications = new Color(0.22f, 0.18f, 0.06f, 0.88f);

        // -------- 生命周期 --------
        private void Awake()
        {
            controller = FindAnyObjectByType<OnlineMatchController>();

            // 初始化状态快照
            previousTimers[SabotageType.Blackout] = 0f;
            previousTimers[SabotageType.Lockdown] = 0f;
            previousTimers[SabotageType.Communications] = 0f;
            previousTimers[SabotageType.EvidenceLeak] = 0f;
            previousTimers[SabotageType.PatrolAlert] = 0f;

            foreach (var key in previousTimers.Keys)
            {
                previousStatus[key] = "inactive";
            }

            // 自动查找 UI 元素
            if (crewmateSabotageIndicator == null)
            {
                GameObject found = GameObject.Find("SabotageIndicator");
                if (found != null) crewmateSabotageIndicator = found;
            }

            if (repairHintPanel == null)
            {
                GameObject found = GameObject.Find("RepairHintPanel");
                if (found != null) repairHintPanel = found;
            }

            // 初始隐藏
            if (repairHintPanel != null) repairHintPanel.SetActive(false);
            if (crewmateSabotageIndicator != null) crewmateSabotageIndicator.SetActive(false);
        }

        private void Update()
        {
            if (controller == null) return;

            // 定时刷新破坏状态
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = refreshInterval;
                CheckSabotageChanges();
            }

            // 修复提示计时器
            if (hintTimer > 0f)
            {
                hintTimer -= Time.deltaTime;
                if (hintTimer <= 0f && repairHintPanel != null)
                {
                    repairHintPanel.SetActive(false);
                }
            }

            // 更新 Crewmate 破坏指示器
            UpdateCrewmateSabotageIndicator();
        }

        // -------- 破坏状态检测（轮询 OnlineMatchController 的计时器字段） --------

        private void CheckSabotageChanges()
        {
            foreach (var kv in previousStatus)
            {
                SabotageType type = kv.Key;
                string prevStatus = kv.Value;

                // 获取当前计时器值（直接通过公开属性）
                float currentTimer = GetTimerValue(type);
                string currentStatus = currentTimer > 0f ? "active" : "inactive";

                // 检测状态变更
                if (prevStatus != currentStatus)
                {
                    previousStatus[type] = currentStatus;
                    previousTimers[type] = currentTimer;

                    if (currentStatus == "active")
                    {
                        OnSabotageTriggered(type);
                    }
                    else if (currentStatus == "inactive" && prevStatus == "active")
                    {
                        OnSabotageEnded(type);
                    }
                }
                else
                {
                    previousTimers[type] = currentTimer;
                }
            }
        }

        private float GetTimerValue(SabotageType type)
        {
            switch (type)
            {
                case SabotageType.Blackout: return controller.BlackoutTimer;
                case SabotageType.Lockdown: return controller.LockdownTimer;
                case SabotageType.Communications: return controller.CommunicationJamTimer;
                case SabotageType.EvidenceLeak: return controller.EvidenceLeakTimer;
                case SabotageType.PatrolAlert: return controller.PatrolAlertTimer;
                default: return 0f;
            }
        }

        // -------- Sabotage 事件回调 --------

        private void OnSabotageTriggered(SabotageType type)
        {
            lastTriggeredSabotage = type;
            Debug.Log($"SabotageSync: Sabotage {type} 已触发");

            // 判断本地玩家身份
            OnlineRole localRole = GetLocalRole();

            if (localRole == OnlineRole.Gang)
            {
                // Gang 端：提示破坏已生效
                ShowSabotageConfirmation(type);
            }
            else
            {
                // Crewmate 端：显示修复提示
                ShowRepairHint(type);
            }
        }

        private void OnSabotageEnded(SabotageType type)
        {
            Debug.Log($"SabotageSync: Sabotage {type} 已结束/修复");

            // 隐藏对应的修复提示
            if (repairHintPanel != null)
            {
                string hintText = GetRepairHintText(type);
                if (repairHintText != null && repairHintText.text.Contains(hintText))
                {
                    repairHintPanel.SetActive(false);
                    hintTimer = 0f;
                }
            }
        }

        // -------- Gang 端 Sabotage 确认 --------

        private void ShowSabotageConfirmation(SabotageType type)
        {
            // 通过 controller 的 AddCaseLog 记录
            // 直接调用（friend 或 public）
            string msg = type switch
            {
                SabotageType.Blackout => "破坏：已触发停电，Crewmate 视野受限。",
                SabotageType.Lockdown => "破坏：已封锁区域，Crewmate 移动受阻。",
                SabotageType.Communications => "破坏：已切断通讯，会议按钮禁用。",
                _ => $"破坏：{type} 已触发。"
            };

            // 添加日志
            controller.AddCaseLog(msg);
        }

        // -------- Crewmate 修复提示 --------

        private void ShowRepairHint(SabotageType type)
        {
            string hintText = GetRepairHintText(type);
            hintTimer = hintDisplayDuration;

            if (repairHintText != null)
            {
                repairHintText.text = hintText;
                repairHintText.color = GetSabotageColor(type);
            }

            if (repairHintPanel != null)
            {
                repairHintPanel.SetActive(true);
            }

            // 添加日志
            controller.AddCaseLog($"SabotageSync: Crewmate 收到破坏警报 — {hintText}");
        }

        private void UpdateCrewmateSabotageIndicator()
        {
            if (crewmateSabotageIndicator == null) return;
            if (controller == null) return;

            OnlineRole localRole = GetLocalRole();
            if (localRole == OnlineRole.Gang || localRole == OnlineRole.Unassigned)
            {
                crewmateSabotageIndicator.SetActive(false);
                return;
            }

            // 检查是否有活跃的破坏
            bool hasActive = false;
            foreach (SabotageType type in previousTimers.Keys)
            {
                if (previousTimers[type] > 0f)
                {
                    hasActive = true;
                    if (repairHintIcon != null)
                        repairHintIcon.color = GetSabotageColor(type);
                    break;
                }
            }

            crewmateSabotageIndicator.SetActive(hasActive);
        }

        // -------- 文本与颜色 --------

        private string GetRepairHintText(SabotageType type)
        {
            switch (type)
            {
                case SabotageType.Blackout:
                    return "⚠ 停电！前往配电室修复电闸（交互按E）";
                case SabotageType.Lockdown:
                    return "⚠ 封锁！前往被封锁区域修复门锁（交互按E）";
                case SabotageType.Communications:
                    return "⚠ 断讯！前往通讯室修复天线（交互按E）";
                case SabotageType.EvidenceLeak:
                    return "⚠ 证据泄露！前往档案室销毁敏感文件（交互按E）";
                case SabotageType.PatrolAlert:
                    return "⚠ 巡逻警报！前往哨站关闭警报（交互按E）";
                default:
                    return "⚠ 破坏警报！前往修复（交互按E）";
            }
        }

        private Color GetSabotageColor(SabotageType type)
        {
            switch (type)
            {
                case SabotageType.Blackout: return ColorBlackout;
                case SabotageType.Lockdown: return ColorLockdown;
                case SabotageType.Communications: return ColorCommunications;
                default: return new Color(0.25f, 0.12f, 0.12f, 0.88f);
            }
        }

        // -------- 工具方法 --------

        private OnlineRole GetLocalRole()
        {
            if (controller == null) return OnlineRole.Unassigned;
            return controller.LocalRole;
        }

        // -------- 公开接口 --------

        /// <summary>
        /// 获取当前活跃的破坏类型与剩余时间
        /// </summary>
        public Dictionary<SabotageType, float> GetActiveSabotages()
        {
            var result = new Dictionary<SabotageType, float>();
            foreach (var kv in previousTimers)
            {
                float timer = GetTimerValue(kv.Key);
                if (timer > 0f) result[kv.Key] = timer;
            }
            return result;
        }

        /// <summary>
        /// 判断是否有任何活跃的破坏
        /// </summary>
        public bool HasActiveSabotage()
        {
            foreach (SabotageType type in previousTimers.Keys)
            {
                if (GetTimerValue(type) > 0f) return true;
            }
            return false;
        }
    }
}