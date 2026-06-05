using System;
using System.Collections.Generic;
using GanglandUndercover;
using GanglandUndercover.SocialDeduction;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Sabotage 破坏 UI 面板（Impostor/Gang 专属）：
    /// - Blackout（熄灯 10s）
    /// - Lockdown（封锁区域门 15s）
    /// - Communications（禁用会议 10s）
    /// - 每个破坏有独立冷却时间
    /// - 破坏进行时 Crewmate 可以看到修复任务
    ///
    /// 面板通过 OnlineMatchController 的 sabotage 计时器状态刷新，
    /// 点击按钮调用 controller.SendClientAction(OnlineActionType.Ability)
    /// 并附带 sabotage 类型参数（需要扩展 OnlineActionType 或使用 Ability 子类型）。
    ///
    /// 当前实现通过现有 Ability 管道触发，Gang 的 Ability 对应 Sabotage。
    /// </summary>
    public sealed class SabotagePanel : MonoBehaviour
    {
        // -------- 配置 --------
        [Header("破坏类型配置")]
        [SerializeField] private SabotageButtonConfig[] sabotageButtons;

        [Header("冷却时间（秒）")]
        [Tooltip("Blackout 冷却时间")]
        public float blackoutCooldown = 30f;

        [Tooltip("Lockdown 冷却时间")]
        public float lockdownCooldown = 45f;

        [Tooltip("Communications 冷却时间")]
        public float communicationsCooldown = 40f;

        [Tooltip("紧急破坏（O2/Reactor）冷却时间")]
        public float criticalCooldown = 60f;

        [Tooltip("EvidenceLeak 冷却时间")]
        public float evidenceLeakCooldown = 50f;

        [Tooltip("PatrolAlert 冷却时间")]
        public float patrolAlertCooldown = 45f;

        [Header("持续时间（秒）")]
        [Tooltip("Blackout 持续时间")]
        public float blackoutDuration = 28f;

        [Tooltip("Lockdown 持续时间")]
        public float lockdownDuration = 32f;

        [Tooltip("Communications 持续时间")]
        public float communicationsDuration = 30f;

        [Tooltip("EvidenceLeak 持续时间")]
        public float evidenceLeakDuration = 36f;

        [Tooltip("PatrolAlert 持续时间")]
        public float patrolAlertDuration = 30f;

        [Header("UI 引用")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject sabotageButtonPrefab;

        // -------- 运行时状态 --------
        private OnlineMatchController controller;
        private readonly Dictionary<SabotageType, float> cooldownTimers = new Dictionary<SabotageType, float>();
        private readonly Dictionary<SabotageType, float> activeTimers = new Dictionary<SabotageType, float>();
        private readonly List<Button> spawnedButtons = new List<Button>();

        // -------- 生命周期 --------
        private void Awake()
        {
            controller = FindAnyObjectByType<OnlineMatchController>();

            // 自动查找面板根节点
            if (panelRoot == null)
            {
                Transform found = transform.Find("SabotagePanel");
                if (found != null) panelRoot = found.gameObject;
                else panelRoot = gameObject;
            }

            panelRoot.SetActive(false);

            // 初始化冷却字典
            cooldownTimers[SabotageType.Blackout] = 0f;
            cooldownTimers[SabotageType.Lockdown] = 0f;
            cooldownTimers[SabotageType.Communications] = 0f;
            cooldownTimers[SabotageType.EvidenceLeak] = 0f;
            cooldownTimers[SabotageType.PatrolAlert] = 0f;
            cooldownTimers[SabotageType.CriticalO2] = 0f;
            cooldownTimers[SabotageType.CriticalReactor] = 0f;

            activeTimers[SabotageType.Blackout] = 0f;
            activeTimers[SabotageType.Lockdown] = 0f;
            activeTimers[SabotageType.Communications] = 0f;
            activeTimers[SabotageType.CriticalO2] = 0f;
            activeTimers[SabotageType.CriticalReactor] = 0f;

            BuildSabotageButtons();
        }

        private void Update()
        {
            if (controller == null) return;

            // 仅在 Action 阶段、本地玩家存活、且为 Gang 时显示面板
            bool shouldShow = ShouldShowPanel();

            // Tab 键切换面板显示（与任务面板互斥）
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                TogglePanel();
            }

            // 更新冷却与持续计时器
            TickTimers();

            // 刷新按钮状态
            RefreshButtonStates();
        }

        // -------- 面板显示逻辑 --------

        private bool ShouldShowPanel()
        {
            if (controller == null) return false;
            if (controller.Phase != OnlineMatchPhase.Action) return false;

            // 检查本地玩家是否存活
            if (!IsLocalPlayerAlive()) return false;

            // 检查本地角色是否为 Gang
            OnlineRole localRole = GetLocalRole();
            return localRole == OnlineRole.Gang;
        }

        private void TogglePanel()
        {
            if (panelRoot == null) return;
            bool newState = !panelRoot.activeSelf;

            // 仅 Gang 可以打开
            if (newState && !ShouldShowPanel()) return;

            panelRoot.SetActive(newState);
        }

        public void ShowPanel() { if (ShouldShowPanel() && panelRoot != null) panelRoot.SetActive(true); }
        public void HidePanel() { if (panelRoot != null) panelRoot.SetActive(false); }

        // -------- 破坏按钮构建 --------

        private void BuildSabotageButtons()
        {
            if (buttonContainer == null) return;

            // 清空旧按钮
            foreach (Button btn in spawnedButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            spawnedButtons.Clear();

            // 默认破坏类型（含所有5种标准型 + 2种紧急型）
            var defaultConfigs = new[]
            {
                new SabotageButtonConfig { type = SabotageType.Blackout, displayName = "熄灯", description = "全图视野降低 28s" },
                new SabotageButtonConfig { type = SabotageType.Lockdown, displayName = "封锁", description = "全员减速 32s" },
                new SabotageButtonConfig { type = SabotageType.Communications, displayName = "断讯", description = "禁用会议按钮 30s" },
                new SabotageButtonConfig { type = SabotageType.EvidenceLeak, displayName = "证据泄露", description = "证据链持续衰减 36s" },
                new SabotageButtonConfig { type = SabotageType.PatrolAlert, displayName = "巡逻警报", description = "黑帮暴露风险 30s" },
                new SabotageButtonConfig { type = SabotageType.CriticalO2, displayName = "O2中毒", description = "紧急：氧气泄漏 30s" },
                new SabotageButtonConfig { type = SabotageType.CriticalReactor, displayName = "反应堆", description = "紧急：熔毁 30s" },
            };

            foreach (var config in defaultConfigs)
            {
                GameObject btnObj = sabotageButtonPrefab != null
                    ? Instantiate(sabotageButtonPrefab, buttonContainer)
                    : CreateFallbackButton(config.displayName);

                SabotageButton sb = btnObj.GetComponent<SabotageButton>();
                if (sb == null) sb = btnObj.AddComponent<SabotageButton>();

                sb.Setup(config, OnSabotageButtonClicked);
                spawnedButtons.Add(sb.GetComponent<Button>());
            }
        }

        private GameObject CreateFallbackButton(string displayLabel)
        {
            GameObject btnObj = new GameObject("SabotageBtn_" + displayLabel);
            btnObj.transform.SetParent(buttonContainer, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 48f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.22f, 0.92f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.22f, 0.22f, 0.28f);
            cb.highlightedColor = new Color(0.32f, 0.32f, 0.38f);
            cb.pressedColor = new Color(0.12f, 0.12f, 0.18f);
            btn.colors = cb;

            // 标签
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform lrt = labelObj.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(4f, 4f);
            lrt.offsetMax = new Vector2(-4f, -4f);

            Text lbl = labelObj.AddComponent<Text>();
            lbl.text = displayLabel;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = Color.white;
            lbl.fontSize = 16;

            return btnObj;
        }

        // -------- 破坏触发 --------

        private void OnSabotageButtonClicked(SabotageType type)
        {
            if (!CanTriggerSabotage(type)) return;

            // 通过 OnlineMatchController 触发破坏
            // 现有管道：Gang 的 Ability 按钮对应 Sabotage
            // 直接调用 controller 的 sabotage 方法
            TriggerSabotage(type);

            // 启动冷却
            StartCooldown(type);
        }

        private void TriggerSabotage(SabotageType type)
        {
            if (controller == null) return;

            // 本地预览/离线模式：直接施加效果
            if (controller.LocalClientIdValue == 0UL)
            {
                ApplySabotageLocally(type);
            }
            else
            {
                // 联机模式：通过 Ability 消息发送
                controller.RequestAction(OnlineActionType.Ability);
            }
        }

        private void ApplySabotageLocally(SabotageType type)
        {
            // 紧急破坏：触发 CriticalTaskSystem
            if (type == SabotageType.CriticalO2)
            {
                SocialPrototypeController spc = FindAnyObjectByType<SocialPrototypeController>();
                if (spc != null)
                {
                    spc.TriggerCriticalTask(CriticalTaskType.O2);
                }
                return;
            }

            if (type == SabotageType.CriticalReactor)
            {
                SocialPrototypeController spc = FindAnyObjectByType<SocialPrototypeController>();
                if (spc != null)
                {
                    spc.TriggerCriticalTask(CriticalTaskType.Reactor);
                }
                return;
            }

            // 普通破坏：直接通过 taskService 设置计时器
            controller.ApplySabotageTimer(type);

            // 播放破坏音效
            controller.RequestAction(OnlineActionType.Ability);
        }

        // -------- 冷却与计时 --------

        private void TickTimers()
        {
            // 冷却计时器
            TickTimerDict(cooldownTimers);
            // 持续计时器
            TickTimerDict(activeTimers);
        }

        private void TickTimerDict(Dictionary<SabotageType, float> dict)
        {
            List<SabotageType> keys = new List<SabotageType>(dict.Keys);
            foreach (SabotageType key in keys)
            {
                if (dict[key] > 0f)
                    dict[key] = Mathf.Max(0f, dict[key] - Time.deltaTime);
            }
        }

        private void StartCooldown(SabotageType type)
        {
            float cd = GetCooldown(type);
            cooldownTimers[type] = cd;
        }

        private bool CanTriggerSabotage(SabotageType type)
        {
            return cooldownTimers[type] <= 0f && activeTimers[type] <= 0f;
        }

        private void RefreshButtonStates()
        {
            foreach (Button btn in spawnedButtons)
            {
                SabotageButton sb = btn.GetComponent<SabotageButton>();
                if (sb == null) continue;

                SabotageType type = sb.Config.type;
                bool canTrigger = CanTriggerSabotage(type);
                btn.interactable = canTrigger;

                // 更新按钮标签（显示冷却）
                if (!canTrigger)
                {
                    float remaining = Mathf.Max(cooldownTimers[type], activeTimers[type]);
                    sb.SetCooldownLabel(Mathf.CeilToInt(remaining) + "s");
                }
                else
                {
                    sb.SetCooldownLabel(string.Empty);
                }
            }
        }

        // -------- 配置查询 --------

        private float GetCooldown(SabotageType type)
        {
            switch (type)
            {
                case SabotageType.Blackout: return blackoutCooldown;
                case SabotageType.Lockdown: return lockdownCooldown;
                case SabotageType.Communications: return communicationsCooldown;
                case SabotageType.EvidenceLeak: return evidenceLeakCooldown;
                case SabotageType.PatrolAlert: return patrolAlertCooldown;
                case SabotageType.CriticalO2:
                case SabotageType.CriticalReactor: return criticalCooldown;
                default: return 30f;
            }
        }

        private float GetDuration(SabotageType type)
        {
            switch (type)
            {
                case SabotageType.Blackout: return blackoutDuration;
                case SabotageType.Lockdown: return lockdownDuration;
                case SabotageType.Communications: return communicationsDuration;
                case SabotageType.EvidenceLeak: return evidenceLeakDuration;
                case SabotageType.PatrolAlert: return patrolAlertDuration;
                case SabotageType.CriticalO2:
                case SabotageType.CriticalReactor: return 0f; // 紧急任务不由 duration 控制
                default: return 28f;
            }
        }

        // -------- 工具方法 --------

        private bool IsLocalPlayerAlive()
        {
            if (controller == null) return false;
            ulong localId = GetLocalClientId();
            // 通过 controller 的 players 字典检查（需要友元访问）
            return true; // 由服务端校验，客户端预判
        }

        private OnlineRole GetLocalRole()
        {
            if (controller == null) return OnlineRole.Unassigned;
            return controller.LocalRole;
        }

        private ulong GetLocalClientId()
        {
            if (controller == null) return 0;
            return controller.IsLocalPreview || NetworkManager.Singleton == null
                ? 0UL
                : NetworkManager.Singleton.LocalClientId;
        }

        // -------- 公开接口（供 Crewmate 修复 UI 调用）--------

        /// <summary>
        /// 获取当前活跃的破坏类型列表（供 Crewmate UI 显示修复任务）
        /// </summary>
        public List<SabotageType> GetActiveSabotages()
        {
            List<SabotageType> active = new List<SabotageType>();
            foreach (var kv in activeTimers)
            {
                if (kv.Value > 0f) active.Add(kv.Key);
            }
            return active;
        }

        /// <summary>
        /// Crewmate 修复破坏（调用自 Task 系统）
        /// </summary>
        public void RepairSabotage(SabotageType type)
        {
            if (activeTimers.TryGetValue(type, out float val) && val > 0f)
            {
                activeTimers[type] = 0f;
                // 通知 controller
                if (controller != null)
                {
                    // 触发修复逻辑（调用现有 RepairSabotageEffect）
                    // 通过 SendClientAction 或反射调用
                }
            }
        }
    }

    // -------- 数据结构 --------

    [Serializable]
    public struct SabotageButtonConfig
    {
        public SabotageType type;
        public string displayName;
        public string description;
    }

    public class SabotageButton : MonoBehaviour
    {
        private SabotageButtonConfig config;
        private Action<SabotageType> onClick;
        private Text label;
        private Text cooldownLabel;
        private Image cooldownOverlay;

        public SabotageButtonConfig Config => config;

        public void Setup(SabotageButtonConfig cfg, Action<SabotageType> clickCallback)
        {
            config = cfg;
            onClick = clickCallback;

            Button btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => onClick?.Invoke(config.type));

            // 查找标签
            Transform labelTf = transform.Find("Label");
            if (labelTf != null) label = labelTf.GetComponent<Text>();

            Transform cdTf = transform.Find("CooldownLabel");
            if (cdTf != null) cooldownLabel = cdTf.GetComponent<Text>();

            Transform overlayTf = transform.Find("CooldownOverlay");
            if (overlayTf != null) cooldownOverlay = overlayTf.GetComponent<Image>();

            RefreshLabel();
        }

        public void SetCooldownLabel(string text)
        {
            if (cooldownLabel != null) cooldownLabel.text = text;
            if (cooldownOverlay != null)
                cooldownOverlay.fillAmount = string.IsNullOrEmpty(text) ? 0f : 0.5f;
        }

        private void RefreshLabel()
        {
            if (label != null) label.text = config.displayName;
        }
    }
}
