using System;
using System.Collections.Generic;
using GanglandUndercover;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机任务状态服务：集中管理任务状态、完成、破坏、修复、证据增减。
    /// 负责本地规则判定，不参与网络同步（同步由 BroadcastSnapshot + TaskSync 负责）。
    ///
    /// 事件订阅方：OnlineMatchHud（面板更新）、AudioManager（音效）、
    /// OnlineMatchController（胜负判定级联）、TaskSync（进度记录）。
    /// </summary>
    public class OnlineTaskService : MonoBehaviour
    {
        // ====== 任务状态 ======
        private readonly List<OnlineTaskState> tasks = new List<OnlineTaskState>();

        // ====== 证据链状态（委托到 EvidenceService） ======
        // evidenceScore / evidenceTarget / evidenceMilestoneIndex / lastEvidenceEvent
        // 现在由 Services.EvidenceService 统一管理。本类通过 evidenceServiceRef 代理访问。
        private Services.EvidenceService evidenceServiceRef;
        private string lastSabotageEvent = "尚未发生破坏。";

        // ====== 破坏效果计时器（委托到 SabotageService） ======
        // blackoutTimer / lockdownTimer / communicationJamTimer / evidenceLeakTimer /
        // evidenceLeakAccumulator / patrolAlertTimer 现在由 Services.SabotageService 统一管理。
        private Services.SabotageService sabotageServiceRef;

        // ====== 引用 ======
        private OnlineMatchController controller;
        private OnlineRuleSet ruleSet;
        private OnlineMapService mapService;

        // ====== 事件（供 HUD / 音频 / 日志 / 胜负判定订阅） ======
        public event Action<int, ulong> OnTaskCompleted;
        public event Action<int, SabotageType> OnTaskSabotaged;
        public event Action<int> OnTaskRepaired;
        public event Action<int, int> OnEvidenceChanged;              // (score, target)
        public event Action<int> OnEvidenceMilestone;                 // (milestone index)
        public event Action<string> OnSabotageEffectApplied;          // (effect description)

        // ====== 属性 ======
        public IReadOnlyList<OnlineTaskState> Tasks => tasks;
        public int EvidenceScore
        {
            get => evidenceServiceRef != null ? evidenceServiceRef.EvidenceScore : 0;
            set => evidenceServiceRef?.SetEvidenceScore(value);
        }

        public int EvidenceTarget
        {
            get => evidenceServiceRef != null ? evidenceServiceRef.EvidenceTarget : 0;
            set => evidenceServiceRef?.SetEvidenceTarget(value);
        }
        public int EvidenceMilestoneIndex => evidenceServiceRef != null ? evidenceServiceRef.EvidenceMilestoneIndex : 0;
        public string LastEvidenceEvent => evidenceServiceRef != null ? evidenceServiceRef.LastEvidenceEvent : "尚未取得关键证据。";
        public string LastSabotageEvent => lastSabotageEvent;
        public float BlackoutTimer => sabotageServiceRef != null ? sabotageServiceRef.BlackoutTimer : 0f;
        public float LockdownTimer => sabotageServiceRef != null ? sabotageServiceRef.LockdownTimer : 0f;
        public float CommunicationJamTimer => sabotageServiceRef != null ? sabotageServiceRef.CommunicationJamTimer : 0f;
        public float EvidenceLeakTimer => sabotageServiceRef != null ? sabotageServiceRef.EvidenceLeakTimer : 0f;
        public float PatrolAlertTimer => sabotageServiceRef != null ? sabotageServiceRef.PatrolAlertTimer : 0f;
        public float EvidenceLeakAccumulator => sabotageServiceRef != null ? sabotageServiceRef.EvidenceLeakAccumulator : 0f;

        // ====== 生命周期 ======

        private void Awake()
        {
            controller = GetComponent<OnlineMatchController>();
        }

        /// <summary>
        /// 延迟初始化引用（在 OnlineMatchController.Awake 之后调用）。
        /// </summary>
        public void Initialize(OnlineRuleSet ruleSetRef, OnlineMapService mapServiceRef,
            Services.EvidenceService evidenceService = null)
        {
            ruleSet = ruleSetRef;
            mapService = mapServiceRef;
            if (evidenceService != null)
            {
                evidenceServiceRef = evidenceService;
            }

            // 尚未设定证据目标时，初始化为规则集默认值（保证 lobby 阶段即有合理的 10-20 分钟局时目标，
            // 不覆盖快照恢复或开局缩放后的值）。
            if (evidenceServiceRef != null && evidenceServiceRef.EvidenceTarget <= 0)
            {
                evidenceServiceRef.SetEvidenceTarget(ruleSet != null ? ruleSet.DefaultEvidenceTarget : 44);
            }
        }

        // ====== 公开 API：任务状态管理 ======

        /// <summary>
        /// 构建默认任务列表（共 28 个任务，id 0-27）。
        /// </summary>
        public void BuildDefaultTasks()
        {
            tasks.Clear();
            for (int id = 0; id <= 19; id++)
            {
                tasks.Add(new OnlineTaskState(id, TaskNameFor(id), mapService.TaskPositionFor(id),
                    0, TaskRequiredProgress(id), false, false));
            }

            for (int id = 20; id <= 27; id++)
            {
                tasks.Add(new OnlineTaskState(id, TaskNameFor(id), mapService.TaskPositionFor(id),
                    0, TaskRequiredProgress(id), false, false));
            }
        }

        /// <summary>
        /// 从快照重建任务列表。
        /// </summary>
        public void LoadFromSnapshots(List<GameStateSnapshot.SnapshotTaskEntry> snapshotTasks)
        {
            tasks.Clear();
            foreach (var t in snapshotTasks)
            {
                tasks.Add(new OnlineTaskState(t.Id, t.Name, t.Position, t.Progress,
                    t.RequiredProgress, t.Completed, t.Sabotaged));
            }
        }

        /// <summary>
        /// 从反序列化数据逐项重建任务列表。
        /// </summary>
        public void LoadFromDeserialized(List<OnlineTaskState> deserializedTasks)
        {
            tasks.Clear();
            tasks.AddRange(deserializedTasks);
        }

        /// <summary>
        /// 查找最近的任务。Id == -1 表示范围内无任务。
        /// </summary>
        public OnlineTaskState FindNearestTask(Vector3 position)
        {
            OnlineTaskState best = new OnlineTaskState(-1, string.Empty, Vector3.zero, 0, 1, false, false);
            float bestDistance = ruleSet.InteractionRange;

            foreach (OnlineTaskState task in tasks)
            {
                float distance = Vector3.Distance(position, task.Position);
                if (distance <= bestDistance)
                {
                    best = task;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public OnlineTaskState GetTask(int taskId)
        {
            foreach (OnlineTaskState task in tasks)
            {
                if (task.Id == taskId)
                    return task;
            }

            return new OnlineTaskState(-1, "未知任务", Vector3.zero, 0, 1, false, false);
        }

        /// <summary>
        /// 原地更新任务列表中的指定任务状态。
        /// </summary>
        public void SetTask(OnlineTaskState updated)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].Id == updated.Id)
                {
                    tasks[i] = updated;
                    return;
                }
            }
        }

        // ====== 公开 API：证据链（委托到 EvidenceService） ======

        /// <summary>延迟绑定 EvidenceService 引用（EnsureT1Services 创建后调用）。</summary>
        public void BindEvidenceService(Services.EvidenceService evidenceService)
        {
            evidenceServiceRef = evidenceService;
        }

        /// <summary>延迟绑定 SabotageService 引用（EnsureT1Services 创建后调用）。</summary>
        public void BindSabotageService(Services.SabotageService sabotageService)
        {
            sabotageServiceRef = sabotageService;
        }

        public void SetEvidenceTarget(int value)
        {
            evidenceServiceRef?.SetEvidenceTarget(value);
        }

        public void ResetEvidence()
        {
            int defaultTarget = ruleSet != null ? ruleSet.DefaultEvidenceTarget : 42;
            evidenceServiceRef?.ResetEvidence(defaultTarget);
            lastSabotageEvent = "尚未发生破坏。";
        }

        public void SetEvidenceFromSnapshot(int score, int target, int milestoneIndex,
            string evidenceEvent, string sabotageEvent)
        {
            evidenceServiceRef?.LoadSnapshotState(score, target, milestoneIndex, evidenceEvent);
            lastSabotageEvent = sabotageEvent;
        }

        /// <summary>
        /// 增加证据分数，触发里程碑检查和事件。
        /// </summary>
        public void AddEvidence(int gain, string eventDescription)
        {
            int previousMilestone = EvidenceMilestoneIndex;
            evidenceServiceRef?.AddEvidence(gain, eventDescription);
            OnEvidenceChanged?.Invoke(EvidenceScore, EvidenceTarget);
            if (EvidenceMilestoneIndex > previousMilestone)
            {
                OnEvidenceMilestone?.Invoke(EvidenceMilestoneIndex);
            }
        }

        /// <summary>
        /// 减少证据分数（破坏扣减）。
        /// </summary>
        public void ReduceEvidence(int penalty)
        {
            evidenceServiceRef?.SubtractEvidence(penalty);
            OnEvidenceChanged?.Invoke(EvidenceScore, EvidenceTarget);
        }

        /// <summary>
        /// 证据泄露 tick 扣减。
        /// 注意：实际 tick 逻辑已迁移到 SabotageService.Tick()，此方法保留为兼容空壳。
        /// </summary>
        public void TickEvidenceLeak(float deltaTime)
        {
            // SabotageService.Tick() 已处理证据泄露累积和扣减
        }

        /// <summary>
        /// 证据泄露计时器归零（回合结束时清理）。
        /// 注意：实际逻辑已迁移到 SabotageService.ResetAll()。
        /// </summary>
        public void ResetEvidenceLeakAccumulator()
        {
            // SabotageService.ResetAll() 已处理
        }

        // ====== 公开 API：任务互动（核心流程） ======

        /// <summary>
        /// 任务交互主入口。根据角色执行完成/破坏/修复逻辑。
        /// 返回操作描述字符串，供 BroadcastSnapshot 写入 status。
        /// </summary>
        public string TryInteractWithTask(ulong playerId, OnlinePlayerState player,
            OnlineRole role, OnlineProfession profession)
        {
            OnlineTaskState nearestTask = FindNearestTask(player.Position);

            if (nearestTask.Id < 0)
            {
                return "附近没有可互动任务。";
            }

            if (role == OnlineRole.Gang)
            {
                // ---- 破坏 ----
                SabotageType sabotageType = OnlineMatchUtils.SabotageForTask(nearestTask.Id);
                if (!ApplySabotageEffect(sabotageType, nearestTask.Name, playerId))
                {
                    return "该类破坏仍在生效或冷却中。";
                }

                nearestTask.Sabotaged = true;
                nearestTask.Completed = false;
                nearestTask.Progress = Mathf.Max(0, nearestTask.Progress - 1);
                int penalty = OnlineMatchUtils.SabotageEvidencePenalty(sabotageType);
                evidenceServiceRef?.SubtractEvidence(penalty);

                string status = "有人秘密破坏了 " + nearestTask.Name + "。";
                lastSabotageEvent = status + " 影响: " + OnlineMatchUtils.SabotageName(sabotageType);

                OnEvidenceChanged?.Invoke(EvidenceScore, EvidenceTarget);
                OnTaskSabotaged?.Invoke(nearestTask.Id, sabotageType);
                OnSabotageEffectApplied?.Invoke(sabotageType == SabotageType.Blackout ? "黑灯" :
                    sabotageType == SabotageType.Lockdown ? "封锁" :
                    sabotageType == SabotageType.Communications ? "断讯" :
                    sabotageType == SabotageType.EvidenceLeak ? "泄证" :
                    sabotageType == SabotageType.PatrolAlert ? "巡逻" : "未知");

                SetTask(nearestTask);
                return status;
            }
            else
            {
                // ---- 警员 / 卧底操作 ----
                if (nearestTask.Sabotaged)
                {
                    // 修复
                    nearestTask.Sabotaged = false;
                    SabotageType sabotageType = OnlineMatchUtils.SabotageForTask(nearestTask.Id);
                    RepairSabotageEffect(sabotageType);
                    string status = nearestTask.Name + " 的破坏已修复，危机效果下降。";
                    lastSabotageEvent = status;
                    OnTaskRepaired?.Invoke(nearestTask.Id);
                    SetTask(nearestTask);
                    return status;
                }
                else if (!nearestTask.Completed)
                {
                    // 推进进度
                    int progressGain = 1;
                    nearestTask.Progress = Mathf.Min(nearestTask.RequiredProgress,
                        nearestTask.Progress + progressGain);

                    if (nearestTask.Progress >= nearestTask.RequiredProgress)
                    {
                        nearestTask.Completed = true;
                        int gain = EvidenceGainFor(nearestTask.Id, profession, role);
                        string status = gain > 0
                            ? nearestTask.Name + " 完成，证据链推进。"
                            : nearestTask.Name + " 的伪装任务完成，个人情报增加。";
                        int previousMilestone = EvidenceMilestoneIndex;
                        evidenceServiceRef?.AddEvidence(gain, status);
                        OnEvidenceChanged?.Invoke(EvidenceScore, EvidenceTarget);
                        if (EvidenceMilestoneIndex > previousMilestone)
                        {
                            OnEvidenceMilestone?.Invoke(EvidenceMilestoneIndex);
                        }
                        OnTaskCompleted?.Invoke(nearestTask.Id, playerId);
                        SetTask(nearestTask);
                        return status;
                    }
                    else
                    {
                        string status = nearestTask.Name + " 进度 " + nearestTask.Progress
                            + "/" + nearestTask.RequiredProgress + "。";
                        SetTask(nearestTask);
                        return status;
                    }
                }
            }

            return string.Empty;
        }

        // ====== 公开 API：破坏效果（委托到 SabotageService） ======

        public void TickSabotageTimers(float deltaTime)
        {
            // SabotageService.Tick() 已处理所有计时器递减和证据泄露
        }

        public void ResetAllSabotageTimers()
        {
            sabotageServiceRef?.ResetAll();
        }

        /// <summary>
        /// 从快照/反序列化恢复所有破坏计时器（Host迁移/快照恢复路径）。
        /// </summary>
        public void LoadSabotageTimersFromSnapshot(float blackout, float lockdown, float commJam,
            float evidenceLeak, float evidenceLeakAccum, float patrolAlert)
        {
            sabotageServiceRef?.LoadFromSnapshot(blackout, lockdown, commJam,
                evidenceLeak, patrolAlert, evidenceLeakAccum);
        }

        public bool ApplySabotageEffect(SabotageType sabotageType, string taskName, ulong initiatorId = 0)
        {
            return sabotageServiceRef != null
                && sabotageServiceRef.TryTriggerSabotage(sabotageType, initiatorId, taskName);
        }

        public void RepairSabotageEffect(SabotageType sabotageType)
        {
            sabotageServiceRef?.RepairSabotage(sabotageType);
        }

        /// <summary>
        /// 批量修复被破坏的任务（最多 maxCount 个）。
        /// </summary>
        public int RepairSabotagedTasks(int maxCount)
        {
            int repaired = 0;
            for (int i = 0; i < tasks.Count && repaired < maxCount; i++)
            {
                OnlineTaskState task = tasks[i];
                if (!task.Sabotaged) continue;

                task.Sabotaged = false;
                RepairSabotageEffect(OnlineMatchUtils.SabotageForTask(task.Id));
                tasks[i] = task;
                OnTaskRepaired?.Invoke(task.Id);
                repaired++;
            }

            return repaired;
        }

        // ====== 公开 API：Bot 目标选取 ======

        public Vector3 PickEvidenceTarget()
        {
            List<OnlineTaskState> options = new List<OnlineTaskState>();
            foreach (OnlineTaskState task in tasks)
            {
                if (!task.Completed || task.Sabotaged)
                    options.Add(task);
            }

            if (options.Count == 0)
                return mapService.ScaleMapPosition(Vector3.zero);

            return options[UnityEngine.Random.Range(0, options.Count)].Position;
        }

        // ====== 任务静态数据（仅保留任务身份 / 进度相关） ======
        // UI 面板数据（TaskPanelTemplateTitle / Subtitle / Accent / Footer / Instruction / TaskMapCode）
        // 小游戏机制数据（TaskTemplateMode / CorrectTaskStepInput / TaskChargeRate）
        // 破坏映射（SabotageForTask / SabotageEvidencePenalty / SabotageName）
        // 均已统一到 OnlineMatchUtils，本类不再重复。

        public int EvidenceGainFor(int taskId, OnlineProfession profession, OnlineRole role)
        {
            return Services.EvidenceService.EvidenceGainFor(taskId, profession, role);
        }

        public static string TaskNameFor(int id)
        {
            switch (id)
            {
                case 0: return "调取监控";
                case 1: return "查封货柜";
                case 2: return "修复电闸";
                case 3: return "扫描证物";
                case 4: return "上传档案";
                case 5: return "盘问线人";
                case 6: return "无线电监听";
                case 7: return "门禁取证";
                case 8: return "天台目击";
                case 9: return "诊所搜查";
                case 10: return "码头巡线";
                case 11: return "财务追踪";
                case 12: return "解除卷闸";
                case 13: return "恢复通讯";
                case 14: return "备用发电";
                case 15: return "整理弹道";
                case 16: return "清点赃款";
                case 17: return "巡逻打卡";
                case 18: return "追踪车牌";
                case 19: return "核对病历";
                case 20: return "追查鱼档暗号";
                case 21: return "比对电话录音";
                case 22: return "封存黑钱袋";
                case 23: return "检查码头冷柜";
                case 24: return "恢复警用无人机";
                case 25: return "排查后巷摩托";
                case 26: return "核验巡逻路线";
                case 27: return "加固证人安全屋";
                default: return "未知任务";
            }
        }

        public static string TaskDistrictName(int id)
        {
            switch (id)
            {
                case 1: case 10: case 23: return "西码头货柜场";
                case 0: case 21: return "监控中心";
                case 2: case 14: return "港区电房";
                case 3: case 15: case 26: return "证物库";
                case 4: case 24: return "警队指挥车棚";
                case 5: return "茶餐厅骑楼";
                case 6: case 13: case 20: return "庙街夜市棚群";
                case 7: case 11: case 16: case 22: return "黑钱金融楼";
                case 8: case 27: return "天台机房";
                case 9: case 19: return "地下诊所唐楼";
                case 12: case 25: return "后巷排档楼";
                case 17: case 18: return "中环主干道";
                default: return "九龙港城";
            }
        }

        public static int TaskRequiredProgress(int taskId) => 3;
    }
}
