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

        // ====== 证据链状态 ======
        private int evidenceScore;
        private int evidenceTarget;
        private int evidenceMilestoneIndex;
        private string lastEvidenceEvent = "尚未取得关键证据。";
        private string lastSabotageEvent = "尚未发生破坏。";

        // ====== 破坏效果计时器 ======
        private float blackoutTimer;
        private float lockdownTimer;
        private float communicationJamTimer;
        private float evidenceLeakTimer;
        private float evidenceLeakAccumulator;
        private float patrolAlertTimer;

        // ====== 本地活动任务（迷你游戏状态） ======
        private int activeTaskId = -1;
        private int activeTaskStep;
        private int activeTaskMistakes;
        private float activeTaskCharge;
        private float activeTaskFeedbackTimer;
        private bool activeTaskStepOneDone;
        private bool activeTaskStepTwoDone;
        private bool activeTaskStepThreeDone;
        private bool activeTaskFeedbackPositive;

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
        public event Action<int> OnActiveTaskStarted;                 // (taskId)
        public event Action<int, bool> OnActiveTaskStepResolved;      // (step, success)
        public event Action OnActiveTaskCompleted;
        public event Action OnActiveTaskCancelled;
        public event Action<string> OnSabotageEffectApplied;          // (effect description)

        // ====== 属性 ======
        public IReadOnlyList<OnlineTaskState> Tasks => tasks;
        public int EvidenceScore => evidenceScore;
        public int EvidenceTarget => evidenceTarget;
        public int EvidenceMilestoneIndex => evidenceMilestoneIndex;
        public string LastEvidenceEvent => lastEvidenceEvent;
        public string LastSabotageEvent => lastSabotageEvent;
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
        public float BlackoutTimer => blackoutTimer;
        public float LockdownTimer => lockdownTimer;
        public float CommunicationJamTimer => communicationJamTimer;
        public float EvidenceLeakTimer => evidenceLeakTimer;
        public float PatrolAlertTimer => patrolAlertTimer;
        public float EvidenceLeakAccumulator => evidenceLeakAccumulator;
        public string ActiveTaskName => activeTaskId >= 0 ? GetTask(activeTaskId).Name : string.Empty;

        // ====== 生命周期 ======

        private void Awake()
        {
            controller = GetComponent<OnlineMatchController>();
        }

        /// <summary>
        /// 延迟初始化引用（在 OnlineMatchController.Awake 之后调用）。
        /// </summary>
        public void Initialize(OnlineRuleSet ruleSetRef, OnlineMapService mapServiceRef)
        {
            ruleSet = ruleSetRef;
            mapService = mapServiceRef;
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

        // ====== 公开 API：证据链 ======

        public void SetEvidenceTarget(int value)
        {
            evidenceTarget = Mathf.Clamp(value, 34, 56);
        }

        public void ResetEvidence()
        {
            evidenceScore = 0;
            evidenceTarget = ruleSet != null ? ruleSet.DefaultEvidenceTarget : 42;
            evidenceMilestoneIndex = 0;
            lastEvidenceEvent = "尚未取得关键证据。";
            lastSabotageEvent = "尚未发生破坏。";
        }

        public void SetEvidenceFromSnapshot(int score, int target, int milestoneIndex,
            string evidenceEvent, string sabotageEvent)
        {
            evidenceScore = score;
            evidenceTarget = target;
            evidenceMilestoneIndex = milestoneIndex;
            lastEvidenceEvent = evidenceEvent;
            lastSabotageEvent = sabotageEvent;
        }

        /// <summary>
        /// 增加证据分数，触发里程碑检查和事件。
        /// </summary>
        public void AddEvidence(int gain, string eventDescription)
        {
            evidenceScore = Mathf.Min(evidenceTarget, evidenceScore + gain);
            lastEvidenceEvent = eventDescription + " 当前 " + evidenceScore + "/" + evidenceTarget;
            OnEvidenceChanged?.Invoke(evidenceScore, evidenceTarget);
            UpdateEvidenceMilestone();
        }

        /// <summary>
        /// 减少证据分数（破坏扣减）。
        /// </summary>
        public void ReduceEvidence(int penalty)
        {
            evidenceScore = Mathf.Max(0, evidenceScore - penalty);
            OnEvidenceChanged?.Invoke(evidenceScore, evidenceTarget);
        }

        /// <summary>
        /// 证据泄露 tick 扣减。
        /// </summary>
        public void TickEvidenceLeak(float deltaTime)
        {
            if (evidenceLeakTimer <= 0f) return;

            evidenceLeakTimer = Mathf.Max(0f, evidenceLeakTimer - deltaTime);
            evidenceLeakAccumulator += deltaTime;

            if (evidenceLeakAccumulator >= 5f)
            {
                evidenceScore = Mathf.Max(0, evidenceScore - 1);
                evidenceLeakAccumulator = 0f;
                OnEvidenceChanged?.Invoke(evidenceScore, evidenceTarget);
            }
        }

        /// <summary>
        /// 证据泄露计时器归零（回合结束时清理）。
        /// </summary>
        public void ResetEvidenceLeakAccumulator()
        {
            evidenceLeakAccumulator = 0f;
        }

        private void UpdateEvidenceMilestone()
        {
            int milestone = EvidenceMilestoneFor(evidenceScore, evidenceTarget);
            if (milestone <= evidenceMilestoneIndex) return;

            evidenceMilestoneIndex = milestone;

            switch (milestone)
            {
                case 1:
                    lastEvidenceEvent = "证据链达成 25%，已锁定第一批路线。";
                    break;
                case 2:
                    lastEvidenceEvent = "证据链达成 50%，会议可重点追问高嫌疑目标。";
                    break;
                case 3:
                    lastEvidenceEvent = "证据链达成 75%，警方接近结案，黑帮必须制造破坏。";
                    break;
                default:
                    lastEvidenceEvent = "证据链闭合，进入结案判定。";
                    break;
            }

            OnEvidenceMilestone?.Invoke(milestone);
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

            if (role == OnlineRole.Gang || role == OnlineRole.Mole)
            {
                // ---- 破坏 ----
                SabotageType sabotageType = SabotageForTask(nearestTask.Id);
                nearestTask.Sabotaged = true;
                nearestTask.Completed = false;
                nearestTask.Progress = Mathf.Max(0, nearestTask.Progress - 1);
                evidenceScore = Mathf.Max(0, evidenceScore - SabotageEvidencePenalty(sabotageType));

                string actorLabel = role == OnlineRole.Mole ? "线人" : "黑帮";
                string status = actorLabel + "秘密破坏了 " + nearestTask.Name + "。";
                lastSabotageEvent = status + " 影响: " + SabotageName(sabotageType);

                OnEvidenceChanged?.Invoke(evidenceScore, evidenceTarget);
                OnTaskSabotaged?.Invoke(nearestTask.Id, sabotageType);
                OnSabotageEffectApplied?.Invoke(sabotageType == SabotageType.Blackout ? "黑灯" :
                    sabotageType == SabotageType.Lockdown ? "封锁" :
                    sabotageType == SabotageType.Communications ? "断讯" :
                    sabotageType == SabotageType.EvidenceLeak ? "泄证" :
                    sabotageType == SabotageType.PatrolAlert ? "巡逻" : "未知");

                SetTask(nearestTask);
                ApplySabotageEffect(sabotageType, nearestTask.Name);
                return status;
            }
            else
            {
                // ---- 警员 / 卧底操作 ----
                if (nearestTask.Sabotaged)
                {
                    // 修复
                    nearestTask.Sabotaged = false;
                    SabotageType sabotageType = SabotageForTask(nearestTask.Id);
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
                    int progressGain = (role == OnlineRole.Undercover) ? 2 : 1;
                    nearestTask.Progress = Mathf.Min(nearestTask.RequiredProgress,
                        nearestTask.Progress + progressGain);

                    if (nearestTask.Progress >= nearestTask.RequiredProgress)
                    {
                        nearestTask.Completed = true;
                        int gain = EvidenceGainFor(nearestTask.Id, profession, role);
                        evidenceScore = Mathf.Min(evidenceTarget, evidenceScore + gain);
                        string status = nearestTask.Name + " 完成，证据链推进。";
                        lastEvidenceEvent = status + " 当前 " + evidenceScore + "/" + evidenceTarget;
                        OnEvidenceChanged?.Invoke(evidenceScore, evidenceTarget);
                        UpdateEvidenceMilestone();
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

        // ====== 公开 API：活动任务迷你游戏（本地玩家） ======

        /// <summary>
        /// 判断本地玩家是否应弹出任务面板。
        /// </summary>
        public bool ShouldOpenLocalTaskPanel(Vector3 localPosition, OnlineRole role,
            OnlineMatchPhase phase, bool alive)
        {
            if (phase != OnlineMatchPhase.Action || !alive) return false;
            if (role == OnlineRole.Gang || role == OnlineRole.Mole) return false;

            OnlineTaskState nearestTask = FindNearestTask(localPosition);
            return nearestTask.Id >= 0 && (!nearestTask.Completed || nearestTask.Sabotaged);
        }

        public void BeginActiveTask(int taskId)
        {
            OnlineTaskState task = GetTask(taskId);
            if (task.Id < 0) return;

            activeTaskId = taskId;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskStepOneDone = false;
            activeTaskStepTwoDone = false;
            activeTaskStepThreeDone = false;
            activeTaskMistakes = 0;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;

            OnActiveTaskStarted?.Invoke(taskId);
        }

        public void CancelActiveTask()
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

            OnActiveTaskCancelled?.Invoke();
        }

        public void ReadActiveTaskInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelActiveTask();
                return;
            }

            if (Input.GetKey(KeyCode.Space))
            {
                activeTaskCharge = Mathf.Min(1f, activeTaskCharge
                    + Time.deltaTime * TaskChargeRate(activeTaskId));
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) ResolveActiveTaskStep(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ResolveActiveTaskStep(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ResolveActiveTaskStep(3);

            if (activeTaskCharge >= 1f
                && activeTaskStepOneDone
                && activeTaskStepTwoDone
                && activeTaskStepThreeDone)
            {
                CompleteActiveTask();
            }
        }

        private void ResolveActiveTaskStep(int input)
        {
            if (input == CorrectTaskStepInput(activeTaskId, activeTaskStep))
            {
                activeTaskStep++;
                activeTaskCharge = Mathf.Min(1f, activeTaskCharge + 0.28f);

                if (activeTaskStep == 1) activeTaskStepOneDone = true;
                else if (activeTaskStep == 2) activeTaskStepTwoDone = true;
                else activeTaskStepThreeDone = true;

                activeTaskFeedbackTimer = 0.42f;
                activeTaskFeedbackPositive = true;
                OnActiveTaskStepResolved?.Invoke(activeTaskStep, true);
                return;
            }

            activeTaskCharge = Mathf.Max(0f, activeTaskCharge - 0.18f);
            activeTaskMistakes++;
            activeTaskFeedbackTimer = 0.55f;
            activeTaskFeedbackPositive = false;
            OnActiveTaskStepResolved?.Invoke(activeTaskStep + 1, false);

            if (activeTaskMistakes >= 3)
            {
                activeTaskMistakes = 0;
                activeTaskCharge = 0f;
            }
        }

        /// <summary>
        /// 完成任务迷你游戏，返回 true 表示玩家已提交（调用方需发送 Interact Action）。
        /// </summary>
        public bool CompleteActiveTask()
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

            OnActiveTaskCompleted?.Invoke();
            return true;
        }

        // ====== 公开 API：破坏效果 ======

        public void TickSabotageTimers(float deltaTime)
        {
            if (blackoutTimer > 0f)
                blackoutTimer = Mathf.Max(0f, blackoutTimer - deltaTime);

            if (lockdownTimer > 0f)
                lockdownTimer = Mathf.Max(0f, lockdownTimer - deltaTime);

            if (communicationJamTimer > 0f)
                communicationJamTimer = Mathf.Max(0f, communicationJamTimer - deltaTime);

            TickEvidenceLeak(deltaTime);

            if (patrolAlertTimer > 0f)
                patrolAlertTimer = Mathf.Max(0f, patrolAlertTimer - deltaTime);
        }

        public void ResetAllSabotageTimers()
        {
            blackoutTimer = 0f;
            lockdownTimer = 0f;
            communicationJamTimer = 0f;
            evidenceLeakTimer = 0f;
            evidenceLeakAccumulator = 0f;
            patrolAlertTimer = 0f;
        }

        public void ApplySabotageEffect(SabotageType sabotageType, string taskName)
        {
            switch (sabotageType)
            {
                case SabotageType.Blackout:
                    blackoutTimer = ruleSet.BlackoutSeconds;
                    break;
                case SabotageType.Lockdown:
                    lockdownTimer = ruleSet.LockdownSeconds;
                    break;
                case SabotageType.Communications:
                    communicationJamTimer = ruleSet.CommunicationJamSeconds;
                    break;
                case SabotageType.EvidenceLeak:
                    evidenceLeakTimer = ruleSet.EvidenceLeakSeconds;
                    break;
                case SabotageType.PatrolAlert:
                    patrolAlertTimer = ruleSet.PatrolAlertSeconds;
                    break;
            }
        }

        public void RepairSabotageEffect(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.Blackout:   blackoutTimer = 0f; break;
                case SabotageType.Lockdown:   lockdownTimer = 0f; break;
                case SabotageType.Communications: communicationJamTimer = 0f; break;
                case SabotageType.EvidenceLeak:   evidenceLeakTimer = 0f; break;
                case SabotageType.PatrolAlert:    patrolAlertTimer = 0f; break;
            }
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
                RepairSabotageEffect(SabotageForTask(task.Id));
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

        // ====== 证据/任务静态数据 ======

        public int EvidenceGainFor(int taskId, OnlineProfession profession, OnlineRole role)
        {
            int gain = TaskEvidenceValue(taskId);

            if (profession == OnlineProfession.Forensics) gain++;
            if (role == OnlineRole.Undercover || profession == OnlineProfession.UndercoverAgent) gain++;

            return Mathf.Clamp(gain, 1, 4);
        }

        public static int TaskEvidenceValue(int taskId)
        {
            switch (taskId)
            {
                case 0: case 3: case 11: case 15: case 16: case 21: case 22: case 26: return 2;
                case 4: case 8: case 18: case 24: case 27: return 3;
                default: return 1;
            }
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

        public static int TaskTemplateMode(int taskId)
        {
            switch (taskId)
            {
                case 0: case 6: case 13: case 21: return 0;
                case 1: case 10: case 20: case 23: return 1;
                case 2: case 7: case 12: case 14: case 24: return 2;
                case 3: case 9: case 15: case 19: return 3;
                case 4: case 11: case 16: case 22: return 4;
                default: return taskId % 5;
            }
        }

        public static string TaskPanelTemplateTitle(int taskId)
        {
            switch (taskId)
            {
                case 0: return "监控追踪";
                case 1: case 10: case 23: return "货柜查验";
                case 2: case 14: case 24: return "电力修复";
                case 3: case 15: return "证物鉴证";
                case 4: case 11: case 16: case 22: return "档案账本";
                case 5: case 27: return "接头安全";
                case 6: case 13: case 21: return "通讯监听";
                case 7: case 12: return "门禁封控";
                case 8: case 18: case 26: return "巡线取证";
                case 9: case 19: return "诊所搜查";
                case 17: return "街口执勤";
                case 20: return "鱼档暗号";
                case 25: return "后巷排查";
                default: return "现场任务";
            }
        }

        public static string TaskPanelTemplateSubtitle(int taskId)
        {
            switch (taskId)
            {
                case 0: return "多屏比对 / 导出线索";
                case 1: case 10: case 23: return "封条核验 / 货单比对";
                case 2: case 14: case 24: return "断路恢复 / 电网重启";
                case 3: case 15: return "样本扫描 / 证据归档";
                case 4: case 11: case 16: case 22: return "账目追踪 / 异常冻结";
                case 5: case 27: return "短接传递 / 风险控制";
                case 6: case 13: case 21: return "锁频过滤 / 信号回收";
                case 7: case 12: return "刷卡开闸 / 通道清理";
                case 8: case 18: case 26: return "路线校验 / 目击补强";
                case 9: case 19: return "现场搜查 / 痕迹比对";
                case 17: return "巡逻打卡 / 风险压制";
                case 20: return "暗号识别 / 交易追踪";
                case 25: return "摩托排查 / 后路封锁";
                default: return "证据推进 / 风险判断";
            }
        }

        public static Color TaskPanelAccent(int taskId)
        {
            switch (taskId)
            {
                case 0: return new Color(0.12f, 0.7f, 0.94f, 1f);
                case 1: case 10: case 23: return new Color(0.92f, 0.72f, 0.16f, 1f);
                case 2: case 14: case 24: return new Color(0.14f, 0.82f, 0.32f, 1f);
                case 3: case 15: return new Color(0.82f, 0.84f, 0.92f, 1f);
                case 4: case 11: case 16: case 22: return new Color(0.86f, 0.6f, 0.12f, 1f);
                case 5: case 27: return new Color(0.72f, 0.2f, 0.82f, 1f);
                case 6: case 13: case 21: return new Color(0.72f, 0.86f, 0.18f, 1f);
                case 7: case 12: return new Color(0.92f, 0.42f, 0.12f, 1f);
                case 8: case 18: case 26: return new Color(0.42f, 0.76f, 0.94f, 1f);
                case 9: case 19: return new Color(0.92f, 0.48f, 0.74f, 1f);
                case 17: return new Color(0.58f, 0.9f, 0.36f, 1f);
                case 20: return new Color(0.94f, 0.84f, 0.2f, 1f);
                case 25: return new Color(0.8f, 0.34f, 0.26f, 1f);
                default: return new Color(0.08f, 0.62f, 0.82f, 1f);
            }
        }

        public static string TaskPanelFooter(int taskId)
        {
            switch (taskId)
            {
                case 0: return "监控面板优先看路线";
                case 1: case 23: return "货柜越多，假线索越容易藏";
                case 2: case 14: return "电力恢复会重开部分视野";
                case 4: case 16: case 22: return "账本任务更容易拉高证据链";
                case 6: case 13: case 21: return "通讯越乱，黑帮越容易行动";
                case 7: case 12: return "门禁任务适合配合追捕";
                case 8: case 18: case 26: return "巡线任务会给路线压力";
                default: return "完成后会推进整局节奏";
            }
        }

        public static string TaskPanelInstruction(int taskId)
        {
            switch (taskId)
            {
                case 0: return "监控追踪：依次按 1-3-2 确认画面编号";
                case 1: case 10: case 23: return "货柜查验：依次按 2-1-3 输入货单号码";
                case 2: case 14: case 24: return "电力修复：依次按 3-2-1 接通回路";
                case 3: case 15: return "证物鉴证：依次按 1-2-3 扫描条码";
                case 4: case 11: case 16: case 22: return "档案账本：依次按 2-3-1 定位冻结";
                case 5: case 27: return "接头安全：依次按 3-1-2 核对暗号";
                default: return "按 1/2/3 键选择校验步骤，空格蓄能，Esc 退出。";
            }
        }

        public static string TaskMapCode(int taskId)
        {
            switch (taskId)
            {
                case 0: return "A1";
                case 1: return "B2";
                case 2: return "C1";
                case 3: return "D3";
                case 4: return "E2";
                case 5: return "F1";
                case 6: return "G3";
                case 7: return "H2";
                case 8: return "I1";
                case 9: return "J3";
                case 10: return "K2";
                case 11: return "L1";
                case 12: return "M3";
                case 13: return "N2";
                case 14: return "O1";
                case 15: return "P2";
                case 16: return "Q3";
                case 17: return "R1";
                case 18: return "S2";
                case 19: return "T3";
                case 20: return "U1";
                case 21: return "V2";
                case 22: return "W3";
                case 23: return "X1";
                case 24: return "Y2";
                case 25: return "Z1";
                case 26: return "AA";
                case 27: return "BB";
                default: return "??";
            }
        }

        public static int CorrectTaskStepInput(int taskId, int step)
        {
            switch (TaskTemplateMode(taskId))
            {
                case 0: return new[] { 1, 3, 2 }[Mathf.Clamp(step, 0, 2)];
                case 1: return new[] { 2, 1, 3 }[Mathf.Clamp(step, 0, 2)];
                case 2: return new[] { 3, 2, 1 }[Mathf.Clamp(step, 0, 2)];
                case 3: return new[] { 1, 2, 3 }[Mathf.Clamp(step, 0, 2)];
                case 4: return new[] { 2, 3, 1 }[Mathf.Clamp(step, 0, 2)];
                default: return new[] { 3, 1, 2 }[Mathf.Clamp(step, 0, 2)];
            }
        }

        public static float TaskChargeRate(int taskId)
        {
            switch (TaskTemplateMode(taskId))
            {
                case 0: return 0.58f;
                case 1: return 0.72f;
                case 2: return 0.68f;
                case 3: return 0.56f;
                case 4: return 0.76f;
                default: return 0.62f;
            }
        }

        public static int TaskRequiredProgress(int taskId) => 3;

        // ====== Sabotage 类型映射 ======

        public static SabotageType SabotageForTask(int taskId)
        {
            switch (taskId)
            {
                case 2: case 14: return SabotageType.Blackout;
                case 7: case 12: return SabotageType.Lockdown;
                case 6: case 13: case 20: case 21: case 27: return SabotageType.Communications;
                case 3: case 11: case 16: case 22: case 23: case 25: return SabotageType.EvidenceLeak;
                case 4: case 10: case 17: case 24: case 26: return SabotageType.PatrolAlert;
                default: return SabotageType.None;
            }
        }

        public static int SabotageEvidencePenalty(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.EvidenceLeak: return 2;
                case SabotageType.Blackout:
                case SabotageType.Lockdown:
                case SabotageType.Communications: return 1;
                default: return 0;
            }
        }

        public static string SabotageName(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.Blackout: return "黑灯";
                case SabotageType.Lockdown: return "封锁";
                case SabotageType.Communications: return "断讯";
                case SabotageType.EvidenceLeak: return "泄证";
                case SabotageType.PatrolAlert: return "巡逻";
                default: return "未知";
            }
        }

        private static int EvidenceMilestoneFor(int score, int target)
        {
            if (target <= 0) return 0;

            float ratio = score / (float)target;
            if (ratio >= 1f) return 4;
            if (ratio >= 0.75f) return 3;
            if (ratio >= 0.5f) return 2;
            if (ratio >= 0.25f) return 1;
            return 0;
        }
    }
}
