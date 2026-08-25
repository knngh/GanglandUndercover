using System;
using System.Collections;
using System.Collections.Generic;
using GanglandUndercover.Audio;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 紧急任务类型。
    /// </summary>
    public enum CriticalTaskType : byte
    {
        None,
        /// <summary>O2 修复：进度条 + 连点按钮，30 秒限时</summary>
        O2,
        /// <summary>反应堆熔毁：两个同时按下的按钮，需双手操作，30 秒限时</summary>
        Reactor,
        /// <summary>Phase 2.4: 证据销毁 — Police 证据达 75%时，2 处同时修复，60 秒限时</summary>
        EvidenceDestruction,
        /// <summary>Phase 2.4: 警方增援 — Gang 人数 ≤ Police 50%时，黑帮单人破坏通讯塔，45 秒限时</summary>
        PoliceReinforcement
    }

    /// <summary>
    /// 紧急任务状态。
    /// </summary>
    public enum CriticalTaskState : byte
    {
        Inactive,
        Active,
        Completed,
        Failed
    }

    /// <summary>
    /// 紧急任务系统。
    /// 由 SocialPrototypeController 创建和管理，处理 O2 修复 + Reactor 熔毁两种紧急任务。
    /// - 触发时全局警报闪烁 + 音效
    /// - 所有 Crewmate/Police 必须停止当前任务参与修复
    /// - 未完成 → 对应阵营自动失败
    ///
    /// 挂载在 SocialPrototypeController 的 GameObject 上。
    /// </summary>
    public sealed class CriticalTaskSystem : MonoBehaviour
    {
        // ─── 配置 ────────────────────────────────────

        [Header("Timing")]
        [Tooltip("O2 修复限时（秒）")]
        public float O2TimeLimit = 30f;

        [Tooltip("反应堆熔毁限时（秒）")]
        public float ReactorTimeLimit = 30f;

        [Tooltip("证据销毁限时（秒）")]
        public float EvidenceDestructionTimeLimit = 60f;

        [Tooltip("警方增援限时（秒）")]
        public float PoliceReinforcementTimeLimit = 45f;

        [Tooltip("反应堆同时按键窗口（秒）")]
        public float ReactorSimultaneousWindow = 0.6f;

        [Tooltip("反应堆需要的同时按键成功次数")]
        public int ReactorRequiredPresses = 3;

        [Header("Alarm")]
        [Tooltip("警报闪烁周期（秒）")]
        public float AlarmFlashPeriod = 0.5f;

        [Tooltip("警报闪烁最大 alpha")]
        public float AlarmFlashAlpha = 0.35f;

        // ─── 运行状态 ────────────────────────────────

        public CriticalTaskType ActiveType { get; private set; } = CriticalTaskType.None;
        public CriticalTaskState State { get; private set; } = CriticalTaskState.Inactive;
        public float TimeRemaining { get; private set; }
        public float TotalTime { get; private set; }

        // ─── O2 状态 ──────────────────────────────────
        public float O2Progress { get; private set; }

        // ─── 在线紧急任务状态 ─────────────────────────
        private readonly HashSet<int> evidenceRepairStations = new HashSet<int>();
        public int EvidenceRepairStationCount => evidenceRepairStations.Count;

        // ─── Reactor 状态 ─────────────────────────────
        private bool reactorButtonAHeld;
        private bool reactorButtonBHeld;
        private float reactorSimultaneousTimer;
        private int reactorSuccessCount;

        // ─── 警报 UI ──────────────────────────────────
        private GameObject alarmOverlay;
        private Material alarmMaterial;
        private float alarmFlashTimer;
        private bool alarmFlashState;

        // ─── 回调 ─────────────────────────────────────

        /// <summary>紧急任务开始 (type)。GameController 应暂停 AI 操作。</summary>
        public event Action<CriticalTaskType> OnCriticalTaskStarted;

        /// <summary>紧急任务完成 (type)。恢复正常流程。</summary>
        public event Action<CriticalTaskType> OnCriticalTaskCompleted;

        /// <summary>紧急任务失败 (type)。触发对应阵营失败。</summary>
        public event Action<CriticalTaskType> OnCriticalTaskFailed;

        // ─── 公共 API ─────────────────────────────────

        /// <summary>触发紧急任务。</summary>
        public void Trigger(CriticalTaskType type)
        {
            if (State != CriticalTaskState.Inactive)
            {
                Debug.LogWarning($"[CriticalTaskSystem] 已有紧急任务进行中 ({ActiveType})，忽略 {type}。");
                return;
            }

            ActivateTask(type);
            AudioManager.Instance?.PlaySFX(SoundEffect.Emergency);

            OnCriticalTaskStarted?.Invoke(type);
            Debug.Log($"[CriticalTaskSystem] 紧急任务已触发: {type}，限时 {TotalTime}s");
        }

        /// <summary>玩家代理点击 O2 修复按钮。每次点击推进进度。</summary>
        public void ClickO2Repair(float amount = 0.0625f)
        {
            if (State != CriticalTaskState.Active || ActiveType != CriticalTaskType.O2) return;
            O2Progress += amount;

            if (O2Progress >= 1f)
            {
                Complete();
            }
        }

        /// <summary>
        /// 在线证据销毁修复入口。每个独立站点只能计入一次，两个站点都完成后任务成功。
        /// 服务器负责校验玩家身份、距离和站点合法性，本组件只维护紧急任务状态。
        /// </summary>
        public bool SubmitEvidenceRepair(int stationId)
        {
            if (State != CriticalTaskState.Active || ActiveType != CriticalTaskType.EvidenceDestruction
                || stationId < 0 || !evidenceRepairStations.Add(stationId))
            {
                return false;
            }

            O2Progress = Mathf.Clamp01(evidenceRepairStations.Count / 2f);
            if (evidenceRepairStations.Count >= 2)
            {
                Complete();
            }

            return true;
        }

        /// <summary>
        /// 在线警方增援入口。通讯塔破坏成功后由服务器调用。
        /// </summary>
        public bool SubmitPoliceReinforcementSabotage()
        {
            if (State != CriticalTaskState.Active || ActiveType != CriticalTaskType.PoliceReinforcement)
            {
                return false;
            }

            Complete();
            return true;
        }

        /// <summary>
        /// 恢复主机迁移快照中的活动紧急任务，不重复触发开始事件。
        /// </summary>
        public void RestoreActive(CriticalTaskType type, float remaining)
        {
            if (type == CriticalTaskType.None || remaining <= 0f)
            {
                Cancel();
                return;
            }

            if (State != CriticalTaskState.Inactive)
            {
                Cancel();
            }

            ActivateTask(type);
            TimeRemaining = Mathf.Clamp(remaining, 0f, TotalTime);
        }

        /// <summary>Reactor 按钮 A 按住/松开。</summary>
        public void HoldReactorButtonA(bool hold)
        {
            if (State != CriticalTaskState.Active || ActiveType != CriticalTaskType.Reactor) return;
            reactorButtonAHeld = hold;
            CheckReactorSimultaneous();
        }

        /// <summary>Reactor 按钮 B 按住/松开。</summary>
        public void HoldReactorButtonB(bool hold)
        {
            if (State != CriticalTaskState.Active || ActiveType != CriticalTaskType.Reactor) return;
            reactorButtonBHeld = hold;
            CheckReactorSimultaneous();
        }

        /// <summary>中止当前紧急任务（仅用于调试或游戏重置）。</summary>
        public void Cancel()
        {
            if (State == CriticalTaskState.Inactive) return;
            State = CriticalTaskState.Inactive;
            ActiveType = CriticalTaskType.None;
            evidenceRepairStations.Clear();
            O2Progress = 0f;
            HideAlarmOverlay();
            Debug.Log("[CriticalTaskSystem] 紧急任务已取消。");
        }

        // ─── 内部逻辑 ────────────────────────────────

        private void ActivateTask(CriticalTaskType type)
        {
            ActiveType = type;
            State = CriticalTaskState.Active;
            evidenceRepairStations.Clear();
            O2Progress = 0f;

            switch (type)
            {
                case CriticalTaskType.O2:
                    TotalTime = O2TimeLimit;
                    break;
                case CriticalTaskType.Reactor:
                    TotalTime = ReactorTimeLimit;
                    ResetReactorState();
                    break;
                case CriticalTaskType.EvidenceDestruction:
                    TotalTime = EvidenceDestructionTimeLimit;
                    break;
                case CriticalTaskType.PoliceReinforcement:
                    TotalTime = PoliceReinforcementTimeLimit;
                    ResetReactorState();
                    break;
                default:
                    TotalTime = 0f;
                    break;
            }

            TimeRemaining = TotalTime;
            ShowAlarmOverlay();
        }

        private void ResetReactorState()
        {
            reactorButtonAHeld = false;
            reactorButtonBHeld = false;
            reactorSimultaneousTimer = 0f;
            reactorSuccessCount = 0;
        }

        private void CheckReactorSimultaneous()
        {
            if (reactorButtonAHeld && reactorButtonBHeld)
            {
                reactorSimultaneousTimer += Time.deltaTime;
                if (reactorSimultaneousTimer >= ReactorSimultaneousWindow)
                {
                    reactorSuccessCount++;
                    reactorSimultaneousTimer = 0f;
                    reactorButtonAHeld = false;
                    reactorButtonBHeld = false;
                    AudioManager.Instance?.PlaySFX(SoundEffect.TaskComplete);

                    if (reactorSuccessCount >= ReactorRequiredPresses)
                    {
                        Complete();
                    }
                }
            }
            else
            {
                reactorSimultaneousTimer = 0f;
            }
        }

        private void Complete()
        {
            State = CriticalTaskState.Completed;
            HideAlarmOverlay();
            AudioManager.Instance?.PlaySFX(SoundEffect.TaskComplete);
            OnCriticalTaskCompleted?.Invoke(ActiveType);
            Debug.Log($"[CriticalTaskSystem] 紧急任务已完成: {ActiveType}");
        }

        private void Fail()
        {
            State = CriticalTaskState.Failed;
            HideAlarmOverlay();
            AudioManager.Instance?.PlaySFX(SoundEffect.Defeat);
            OnCriticalTaskFailed?.Invoke(ActiveType);
            Debug.Log($"[CriticalTaskSystem] 紧急任务失败: {ActiveType}");
        }

        // ─── MonoBehaviour ────────────────────────────

        private void Update()
        {
            if (State != CriticalTaskState.Active) return;

            // 倒计时
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Fail();
                return;
            }

            // 警报闪烁
            TickAlarmFlash();

            // Reactor 同时按住检测
            if (ActiveType == CriticalTaskType.Reactor)
            {
                CheckReactorSimultaneous();
            }
        }

        // ─── 警报 UI ──────────────────────────────────

        private void ShowAlarmOverlay()
        {
            if (alarmOverlay != null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // 创建一个全屏 Quad 作为闪烁叠加层
            alarmOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            alarmOverlay.name = "CriticalAlarmOverlay";
            alarmOverlay.transform.SetParent(cam.transform, false);
            alarmOverlay.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.05f);
            alarmOverlay.transform.localRotation = Quaternion.identity;
            alarmOverlay.transform.localScale = new Vector3(20f, 20f, 1f);

            MeshRenderer mr = alarmOverlay.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Color");

            alarmMaterial = new Material(shader);
            alarmMaterial.color = new Color(1f, 0.05f, 0.05f, 0f);
            alarmMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            alarmMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            alarmMaterial.renderQueue = 4000;
            mr.sharedMaterial = alarmMaterial;

            alarmFlashTimer = 0f;
            alarmFlashState = false;
        }

        private void TickAlarmFlash()
        {
            if (alarmMaterial == null) return;

            alarmFlashTimer += Time.deltaTime;
            if (alarmFlashTimer >= AlarmFlashPeriod)
            {
                alarmFlashTimer = 0f;
                alarmFlashState = !alarmFlashState;
                alarmMaterial.color = new Color(1f, 0.05f, 0.05f,
                    alarmFlashState ? AlarmFlashAlpha : 0.05f);
            }
        }

        private void HideAlarmOverlay()
        {
            if (alarmOverlay != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(alarmOverlay);
                }
                else
                {
                    DestroyImmediate(alarmOverlay);
                }
                alarmOverlay = null;
                alarmMaterial = null;
            }
        }

        /// <summary>获取 Reactor 进度描述（成功次数/所需次数）。</summary>
        public string GetReactorProgress() => $"{reactorSuccessCount}/{ReactorRequiredPresses}";

        /// <summary>Reactor 按钮 A 当前是否被按住。</summary>
        public bool IsReactorAHeld => reactorButtonAHeld;

        /// <summary>Reactor 按钮 B 当前是否被按住。</summary>
        public bool IsReactorBHeld => reactorButtonBHeld;
    }
}
