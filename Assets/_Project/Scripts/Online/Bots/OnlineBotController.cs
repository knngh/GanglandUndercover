using System;
using System.Collections.Generic;
using UnityEngine;

using GanglandUndercover.Audio;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// M8.3 升级版联机 Bot 控制器。
    ///
    /// 新增能力：
    /// - 任务完成追踪（Bot 可系统完成任务推进对局）
    /// - 通风管使用（黑帮/卧底 Bot 利用暗线机动）
    /// - 破坏修复（警察 Bot 修复被破坏的任务）
    /// - 职业能力使用（根据 OnlineProfession 使用专属能力）
    /// - 基于嫌疑值的投票（更智能的会议投票）
    ///
    /// 所有决策仅在 Host 端执行（权威模型）。
    /// </summary>
    public sealed class OnlineBotController
    {
        // ── 常量 ──
        public const ulong BotClientIdBase = 900000UL;
        internal const float BotThinkMinSeconds = 1.2f;
        internal const float BotThinkMaxSeconds = 3.4f;
        internal const float BotInteractDistance = 0.45f;
        private const float BotTaskCompleteSeconds = 2.5f;   // Bot 任务完成耗时
        private const float BotRepairSeconds = 2.0f;          // Bot 修复耗时
        private const ulong SkipVoteTarget = ulong.MaxValue;

        // ── Bot 内部状态 ──
        private readonly Dictionary<ulong, float> _thinkTimers = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _voteTimers = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, Vector3> _targets = new Dictionary<ulong, Vector3>();
        // 卡住检测：记录 Bot 上次位置和卡住计时
        private readonly Dictionary<ulong, Vector3> _lastPositions = new Dictionary<ulong, Vector3>();
        private readonly Dictionary<ulong, float> _stuckTimers = new Dictionary<ulong, float>();
        private const float StuckDistanceThreshold = 0.03f;
        private const float StuckTimeBeforeReroute = 5.0f;
        /// <summary>P0-2: 进度停滞重规划阈值——距目标超过此时长未缩短 0.4 单位即重规划。</summary>
        private const float NoProgressTimeBeforeReplan = 12f;
        /// <summary>P0-2: 路点消化停滞阈值——与队首路点距离超过此时长未缩短 0.2 即按阻塞处理。</summary>
        private const float WaypointStallSeconds = 8f;
        private const float DiagnosticsIntervalSeconds = 15f;   // P0-2 节奏诊断输出间隔
        private const float RescueCooldownSeconds = 30f;       // P0-2 深度卡死自救冷却

        /// <summary>
        /// P0-2 节奏修复：bot 任务/修复/破坏的进入半径。
        /// 旧值 BotInteractDistance*1.5 = 0.675 单位，比玩家 InteractionRange(≈2.5) 苛刻 4 倍；
        /// 诊断采样显示 bot 常年在任务点 1.0~1.5 单位徘徊、prog 恒 0（任务率 1/28 的主因）。
        /// 取 InteractionRange*0.6：进入即蓄力，蓄力完成后 TryInteractWithTask 在
        /// InteractionRange 内必然找到该任务。
        /// </summary>
        private float TaskChargeDistance => _ctrl.RuleSet != null
            ? _ctrl.RuleSet.InteractionRange * 0.6f
            : 1.5f;

        // M8.3 新增状态
        private readonly Dictionary<ulong, float> _taskProgress = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _repairProgress = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _ventCooldowns = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, int> _currentTaskId = new Dictionary<ulong, int>();
        // P0-2 房间中转导航：卡墙时先经房间中心走房间网络；同一目标最多中转 2 次
        private readonly Dictionary<ulong, List<Vector3>> _waypoints = new Dictionary<ulong, List<Vector3>>();
        private readonly Dictionary<ulong, int> _relayCounts = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, float> _rescueCooldowns = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _lastGoalDistances = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _noProgressTimers = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _lastWaypointDistances = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _waypointStallTimers = new Dictionary<ulong, float>();
        /// <summary>P0-2: 每 bot 的矩形图不可达任务黑名单（防"弃目标→think 重选同一任务"死循环）。</summary>
        private readonly Dictionary<ulong, HashSet<int>> _unreachableTasks = new Dictionary<ulong, HashSet<int>>();
        /// <summary>P0-2: 黑帮追击同一目标的持续时长（追击疲劳用）。</summary>
        private readonly Dictionary<ulong, float> _chaseTimers = new Dictionary<ulong, float>();
        /// <summary>P0-2: 黑帮追击冷却——疲劳换活动后多久不再主动追杀。</summary>
        private readonly Dictionary<ulong, float> _chaseCooldowns = new Dictionary<ulong, float>();
        private float _diagnosticsTimer;
        private int _completedTaskCount;

        // ── 控制器引用 ──
        private readonly OnlineMatchController _ctrl;

        public OnlineBotController(OnlineMatchController ctrl)
        {
            _ctrl = ctrl ?? throw new ArgumentNullException(nameof(ctrl));
        }

        // ── 只读状态暴露（供快照序列化） ──
        public IReadOnlyDictionary<ulong, float> ThinkTimers => _thinkTimers;
        public IReadOnlyDictionary<ulong, float> VoteTimers => _voteTimers;
        public IReadOnlyDictionary<ulong, Vector3> Targets => _targets;

        /// <summary>快照恢复：设置思考计时器</summary>
        internal void SetThinkTimer(ulong clientId, float value) => _thinkTimers[clientId] = value;
        /// <summary>快照恢复：设置投票计时器</summary>
        internal void SetVoteTimer(ulong clientId, float value) => _voteTimers[clientId] = value;
        /// <summary>快照恢复：设置目标位置</summary>
        internal void SetTarget(ulong clientId, Vector3 target) => _targets[clientId] = target;
        /// <summary>快照恢复：清除所有目标</summary>
        internal void ClearTargets() => _targets.Clear();

        /// <summary>Bot 已完成的任务总数</summary>
        public int CompletedTaskCount => _completedTaskCount;

        // ── 公开属性 ──
        public int BotCount
        {
            get
            {
                int count = 0;
                foreach (OnlinePlayerState state in _ctrl.Players.Values)
                {
                    if (state.IsBot) count++;
                }
                return count;
            }
        }

        // ── 静态工具方法 ──

        public static bool IsBotClient(ulong clientId)
        {
            return clientId >= BotClientIdBase;
        }

        public static OnlineProfession BotProfession(int index)
        {
            // M8.3: 加入 Mole
            OnlineProfession[] professions =
            {
                OnlineProfession.Inspector,
                OnlineProfession.Forensics,
                OnlineProfession.Tech,
                OnlineProfession.UndercoverAgent,
                OnlineProfession.Enforcer,
                OnlineProfession.Fixer,
                OnlineProfession.Driver,
                OnlineProfession.Mole,
            };
            return professions[(index - 1) % professions.Length];
        }

        public static string BotName(int index)
        {
            string[] names =
            {
                "巡警陈",
                "技侦周",
                "线人林",
                "便衣何",
                "阿泰",
                "疤脸",
                "码头辉",
                "诊所梁"
            };
            return names[(index - 1) % names.Length];
        }

        /// <summary>对局开始时初始化 Bot 思考计时器和目标。</summary>
        internal void InitBotState(ulong clientId)
        {
            _targets[clientId] = PickBotTarget(clientId);
            _thinkTimers[clientId] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);
        }

        // ── Bot 生命周期 ──

        /// <summary>
        /// 补齐 AI 玩家至最低人数要求。
        /// </summary>
        public void EnsureMinimumBots()
        {
            int targetCount = Mathf.Clamp(_ctrl.RoomMinPlayers, _ctrl.RuleSet.MinimumPlayablePlayers, _ctrl.RoomMaxPlayers);
            int index = 0;

            while (_ctrl.Players.Count < targetCount)
            {
                ulong clientId = BotClientIdBase + (ulong)index;
                index++;

                if (_ctrl.Players.ContainsKey(clientId))
                {
                    continue;
                }

                string displayName = BotName(index);
                Vector3 spawn = _ctrl.MapService.SpawnPosition(_ctrl.Players.Count);
                _ctrl.AddBotPlayer(clientId, displayName, spawn, BotProfession(index));
                _ctrl.SetKillCooldown(clientId, 0f);
                _ctrl.SetAbilityCooldown(clientId, 0f);
                _thinkTimers[clientId] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);
                _targets[clientId] = PickBotTarget(clientId);
            }

            _ctrl.Status = "已补齐 AI 玩家，可直接开始完整本地局。";
            _ctrl.BroadcastSnapshot();
        }

        /// <summary>
        /// 补齐 Bot 并开始对局。
        /// </summary>
        public void FillBotsAndStart()
        {
            if ((!_ctrl.IsLocalPreview && (_ctrl.NetworkManager == null || !_ctrl.NetworkManager.IsServer)) || _ctrl.Phase != OnlineMatchPhase.Lobby)
            {
                return;
            }

            EnsureMinimumBots();
            _ctrl.StartOnlineMatch();
        }

        /// <summary>
        /// 移除指定 Bot 的状态。
        /// </summary>
        public void RemoveBot(ulong clientId)
        {
            _thinkTimers.Remove(clientId);
            _voteTimers.Remove(clientId);
            _targets.Remove(clientId);
            _taskProgress.Remove(clientId);
            _repairProgress.Remove(clientId);
            _ventCooldowns.Remove(clientId);
            _currentTaskId.Remove(clientId);
            _rescueCooldowns.Remove(clientId);
            _lastGoalDistances.Remove(clientId);
            _noProgressTimers.Remove(clientId);
            _lastWaypointDistances.Remove(clientId);
            _waypointStallTimers.Remove(clientId);
            _unreachableTasks.Remove(clientId);
            _chaseTimers.Remove(clientId);
            _chaseCooldowns.Remove(clientId);
            _lastGoalDistances.Remove(clientId);
            _noProgressTimers.Remove(clientId);
        }

        /// <summary>
        /// 清理所有 Bot 状态。
        /// </summary>
        public void Clear()
        {
            _thinkTimers.Clear();
            _voteTimers.Clear();
            _targets.Clear();
            _taskProgress.Clear();
            _repairProgress.Clear();
            _ventCooldowns.Clear();
            _currentTaskId.Clear();
            _waypoints.Clear();
            _relayCounts.Clear();
            _rescueCooldowns.Clear();
            _lastGoalDistances.Clear();
            _noProgressTimers.Clear();
            _lastWaypointDistances.Clear();
            _waypointStallTimers.Clear();
            _unreachableTasks.Clear();
            _chaseTimers.Clear();
            _chaseCooldowns.Clear();
            _lastGoalDistances.Clear();
            _noProgressTimers.Clear();
            _completedTaskCount = 0;
        }

        /// <summary>
        /// 对局开始时清理投票计时器。
        /// </summary>
        public void ClearVoteTimers()
        {
            _voteTimers.Clear();
        }

        // ── Bot AI 每帧决策 ──

        /// <summary>
        /// M8.3 升级：驱动 Bot 行动——移动、交互、击杀、破坏、报案、任务完成、通风管、修复。
        /// </summary>
        public void TickBotAction(float deltaTime)
        {
            List<ulong> botIds = new List<ulong>();

            foreach (OnlinePlayerState state in _ctrl.Players.Values)
            {
                if (state.IsBot && state.Alive)
                    botIds.Add(state.ClientId);
            }

            // P0-2: 自救/追击冷却递减
            if (_rescueCooldowns.Count > 0)
            {
                List<ulong> rescueKeys = new List<ulong>(_rescueCooldowns.Keys);
                foreach (ulong key in rescueKeys)
                {
                    float remaining = _rescueCooldowns[key] - deltaTime;
                    if (remaining <= 0f) _rescueCooldowns.Remove(key);
                    else _rescueCooldowns[key] = remaining;
                }
            }

            if (_chaseCooldowns.Count > 0)
            {
                List<ulong> chaseKeys = new List<ulong>(_chaseCooldowns.Keys);
                foreach (ulong key in chaseKeys)
                {
                    float remaining = _chaseCooldowns[key] - deltaTime;
                    if (remaining <= 0f) _chaseCooldowns.Remove(key);
                    else _chaseCooldowns[key] = remaining;
                }
            }

            foreach (ulong botId in botIds)
            {
                TickSingleBot(botId, deltaTime);
            }

            // P0-2 诊断：周期性输出 bot 状态汇总，用于节奏采样后定位断点。
            _diagnosticsTimer -= deltaTime;
            if (_diagnosticsTimer <= 0f)
            {
                _diagnosticsTimer = DiagnosticsIntervalSeconds;
                Debug.LogWarning("[BotDiag] " + BuildBotDiagnostics());
            }
        }

        /// <summary>每 bot 一行诊断（目标/距离/任务/蓄力/卡墙/中转），采样后从 log 直接读。</summary>
        internal string BuildBotDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in _ctrl.Players)
            {
                if (pair.Key < 900000L) continue; // 只看 bot
                OnlinePlayerState bot = pair.Value;
                Vector3 target = _targets.TryGetValue(pair.Key, out Vector3 t) ? t : bot.Position;
                float dist = Vector3.Distance(bot.Position, target);
                int taskId = _currentTaskId.TryGetValue(pair.Key, out int tid) ? tid : -1;
                float prog = _taskProgress.TryGetValue(pair.Key, out float tp) ? tp : -1f;
                float stuck = _stuckTimers.TryGetValue(pair.Key, out float st) ? st : -1f;
                int relay = _relayCounts.TryGetValue(pair.Key, out int rc) ? rc : 0;
                bool hasWaypoint = _waypoints.ContainsKey(pair.Key);
                float nearestTaskDist = -1f;
                string nearestTaskName = "-";
                if (_ctrl.Tasks != null)
                {
                    foreach (var task in _ctrl.Tasks)
                    {
                        float d = Vector3.Distance(bot.Position, task.Position);
                        if (nearestTaskDist < 0f || d < nearestTaskDist)
                        {
                            nearestTaskDist = d;
                            nearestTaskName = task.Name;
                        }
                    }
                }

                sb.Append(pair.Key % 1000).Append(':')
                    .Append(bot.Alive ? "活" : "死")
                    .Append(" pos=").Append(bot.Position.x.ToString("F1")).Append(',').Append(bot.Position.y.ToString("F1"))
                    .Append(" tgt=").Append(target.x.ToString("F1")).Append(',').Append(target.y.ToString("F1"))
                    .Append(" dist=").Append(dist.ToString("F2"))
                    .Append(" task=").Append(taskId).Append('(').Append(taskId >= 0 && taskId < (_ctrl.Tasks != null ? _ctrl.Tasks.Count : 0) ? _ctrl.Tasks[taskId].Name : "?").Append(')')
                    .Append(" prog=").Append(prog.ToString("F1"))
                    .Append(" stuck=").Append(stuck.ToString("F1"))
                    .Append(" stall=").Append(_noProgressTimers.TryGetValue(pair.Key, out float stallT) ? stallT.ToString("F1") : "0")
                    .Append(" relay=").Append(relay)
                    .Append(hasWaypoint
                        ? " [wp" + (_waypoints.TryGetValue(pair.Key, out List<Vector3> wps) ? wps.Count.ToString() : "?") + "]"
                        : string.Empty)
                    .Append(" nearTask=").Append(nearestTaskName).Append('@').Append(nearestTaskDist.ToString("F2"))
                    .Append(" | ");
            }

            return sb.Length > 0 ? sb.ToString(0, sb.Length - 3) : "无 bot";
        }

        private void TickSingleBot(ulong botId, float deltaTime)
        {
            OnlinePlayerState bot = _ctrl.Players[botId];
            OnlineRole role = _ctrl.GetPrivateRole(botId);
            OnlineProfession profession = bot.Profession;

            _thinkTimers.TryGetValue(botId, out float thinkTimer);
            thinkTimer -= deltaTime;

            // ── 0) 卧底避险（每帧，P0-2 平衡）──
            // 避险在 think 之外每帧检查：威胁从 3 单位圈压进 KillRange 只要 ~2 秒，
            // think 间隔 1.2~3.4s 会错过窗口（34s 被 1:0 拔除的直接原因）。
            if (role == OnlineRole.Undercover && ShouldEvadeThreat(botId, bot.Position, out Vector3 safeSpot))
            {
                Vector3 currentTarget = _targets.TryGetValue(botId, out Vector3 existingTarget) ? existingTarget : bot.Position;
                if (Vector3.Distance(currentTarget, safeSpot) > 0.5f)
                {
                    // P0-2: 撤离锚点（第三方活人）通常稳定，逃命也走矩形链——
                    // 直线逃撞墙会被追兵在 KillRange 内截杀（采样 1:42 秒杀的直接原因）。
                    SetBotGoal(botId, safeSpot, planPath: true);
                    _currentTaskId.Remove(botId);
                }

                _ctrl.Players[botId] = bot;
            }

            // ── 1) 尸体发现与报案 ──
            if (_ctrl.TryFindNearestBody(bot.Position, out int bodyIndex) &&
                Vector3.Distance(bot.Position, _ctrl.Bodies[bodyIndex].Position) < BotInteractDistance)
            {
                if (UnityEngine.Random.value < _ctrl.RuleSet.BotBodyReportProbability &&
                    _ctrl.Bodies[bodyIndex].Reported == false)
                {
                    var body = _ctrl.Bodies[bodyIndex];
                    body.Reported = true;
                    _ctrl.Bodies[bodyIndex] = body;
                    AudioManager.Instance?.PlaySFX(SoundEffect.BodyReport);
                    _ctrl.BeginMeeting(bot.DisplayName + " 发现尸体并报案");
                    return;
                }
            }

            // ── 2) 通风管冷却递减 ──
            if (_ventCooldowns.TryGetValue(botId, out float ventCd))
            {
                ventCd -= deltaTime;
                _ventCooldowns[botId] = Mathf.Max(0f, ventCd);
            }

            // ── 3) 任务/修复进度 ──
            // 如果在任务附近，推进进度
            TryProgressTaskOrRepair(botId, bot, role, profession, deltaTime);

            // ── 4) 思考决策 ──
            if (thinkTimer <= 0f)
            {
                thinkTimer = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);
                MakeBotDecision(botId, bot, role, profession);
            }
            _thinkTimers[botId] = thinkTimer;

            // ── 5) 移动 ──
            MoveBotTowardTarget(botId, bot, role, profession);
        }

        /// <summary>
        /// M8.3: 推进任务或修复进度。
        /// Bot 到达目标附近后，持续加法计数器完成操作。
        /// </summary>
        private void TryProgressTaskOrRepair(ulong botId, OnlinePlayerState bot, OnlineRole role, OnlineProfession profession, float deltaTime)
        {
            Vector3 target = _targets.TryGetValue(botId, out Vector3 t) ? t : bot.Position;
            float dist = Vector3.Distance(bot.Position, target);

            if (dist > TaskChargeDistance)
            {
                // 远离目标，重置进度
                _taskProgress[botId] = 0f;
                _repairProgress[botId] = 0f;
                return;
            }

            // 只有真黑帮的普通互动才是破坏；双面身份按公开身份完成伪装任务。
            bool isGangSide = role == OnlineRole.Gang;

            // 警察方：推进任务或修复
            if (!isGangSide)
            {
                // 检查是否有被破坏的任务需要修复
                foreach (var task in _ctrl.Tasks)
                {
                    if (task.Sabotaged && !task.Completed &&
                        Vector3.Distance(bot.Position, task.Position) < TaskChargeDistance)
                    {
                        float repairProg = _repairProgress.TryGetValue(botId, out float rp) ? rp : 0f;
                        repairProg += deltaTime;
                        _repairProgress[botId] = repairProg;

                        if (repairProg >= BotRepairSeconds)
                        {
                            _repairProgress[botId] = 0f;
                            _ctrl.TryInteractWithTask(botId, bot); // 触发修复
                        }
                        return;
                    }
                }

                // 推进任务完成
                if (_currentTaskId.TryGetValue(botId, out int taskId) &&
                    taskId >= 0 && taskId < _ctrl.Tasks.Count)
                {
                    OnlineTaskState task = _ctrl.TaskForPlayer(botId, _ctrl.Tasks[taskId]);
                    if (!task.Completed && !task.Sabotaged &&
                        Vector3.Distance(bot.Position, task.Position) < TaskChargeDistance)
                    {
                        float prog = _taskProgress.TryGetValue(botId, out float tp) ? tp : 0f;
                        prog += deltaTime * GetTaskSpeedMultiplier(profession);
                        _taskProgress[botId] = prog;

                        if (prog >= BotTaskCompleteSeconds)
                        {
                            _taskProgress[botId] = 0f;
                            _ctrl.TryInteractWithTask(botId, bot);
                            OnlineTaskState updated = _ctrl.TaskForPlayer(botId, _ctrl.Tasks[taskId]);
                            if (updated.Completed)
                            {
                                _completedTaskCount++;
                                _currentTaskId.Remove(botId);
                            }
                        }
                        return;
                    }
                }
            }
            else
            {
                // 黑帮方：可进行破坏
                foreach (var task in _ctrl.Tasks)
                {
                    if (!task.Completed && !task.Sabotaged &&
                        Vector3.Distance(bot.Position, task.Position) < TaskChargeDistance &&
                        UnityEngine.Random.value < 0.03f)
                    {
                        _ctrl.TryInteractWithTask(botId, bot); // 触发破坏
                        return;
                    }
                }
            }

            // 不在任何任务附近，重置进度
            _taskProgress[botId] = 0f;
            _repairProgress[botId] = 0f;
        }

        /// <summary>
        /// M8.3: Bot 决策——根据角色和职业选择合适的行动。
        /// </summary>
        private void MakeBotDecision(ulong botId, OnlinePlayerState bot, OnlineRole role, OnlineProfession profession)
        {
            // ──  真黑帮：追杀 + 破坏 + 通风管 ──
            if (role == OnlineRole.Gang)
            {
                // 击杀逻辑（P0-2 节奏修复：判定窗口从 BotInteractDistance 0.45 对齐
                // 服务端 RuleSet.KillRange，与真人玩家同权；此前 0.45 窗口配合直线追踪
                // 移动目标，4 人自然采样 20 分钟 0 杀）
                if (_ctrl.TryGetKillCooldown(botId, out float kcd) && kcd <= 0f &&
                    _ctrl.TryFindNearestVictim(botId, bot.Position, out ulong victimId, out OnlinePlayerState victim))
                {
                    if (Vector3.Distance(bot.Position, victim.Position) < _ctrl.RuleSet.KillRange
                        && !HasNearbyWitness(botId, victimId, bot.Position))
                    {
                        PerformKill(botId, bot, victimId);
                        return;
                    }
                }

                // 使用职业能力
                if (UnityEngine.Random.value < 0.12f)
                {
                    TryUseProfessionAbility(botId, bot, profession);
                    return;
                }

                // 使用通风管（非 Mole，Mole 要低调）
                if (role == OnlineRole.Gang && UnityEngine.Random.value < 0.25f)
                {
                    TryUseVent(botId, bot);
                    return;
                }

                // 寻找猎物或破坏目标
                // P0-2 平衡：追击疲劳——死追同一目标 25s 未得手（UC 每帧避险后
                // think 间隔击杀窗口永远差一点，采样出现 20 分钟贴身对峙）→
                // 30s 内不再主动追杀，转破坏/游荡。
                _lastGoalDistances.Remove(botId);
                _noProgressTimers[botId] = 0f;
                bool chaseFatigued = false;
                if (_chaseTimers.TryGetValue(botId, out float chaseTime))
                {
                    chaseTime += 2.3f; // think 平均间隔近似累计
                    if (chaseTime > ChaseFatigueSeconds)
                    {
                        chaseFatigued = true;
                        _chaseTimers.Remove(botId);
                        _chaseCooldowns[botId] = ChaseCooldownSeconds;
                    }
                    else
                    {
                        _chaseTimers[botId] = chaseTime;
                    }
                }

                float chaseCooldown = _chaseCooldowns.TryGetValue(botId, out float chaseCd) ? chaseCd : 0f;
                if (!chaseFatigued && chaseCooldown <= 0f && UnityEngine.Random.value < 0.55f)
                {
                    SetBotGoal(botId, PickNearestLivingNonGang(botId), planPath: false); // 追杀目标会动，直线追
                    if (!_chaseTimers.ContainsKey(botId)) _chaseTimers[botId] = 0f;
                }
                else
                {
                    _chaseTimers.Remove(botId);
                    SetBotGoal(botId, PickSabotageTarget(), planPath: true);
                }

                return;
            }

            // 内鬼只有完成情报目标后才有一名指定卧底可击杀，平时必须维持警察伪装。
            if (role == OnlineRole.Mole
                && _ctrl.TryGetKillCooldown(botId, out float moleCooldown)
                && moleCooldown <= 0f
                && _ctrl.TryFindNearestVictim(botId, bot.Position, out ulong moleVictimId, out OnlinePlayerState moleVictim)
                && Vector3.Distance(bot.Position, moleVictim.Position) < _ctrl.RuleSet.KillRange
                && !HasNearbyWitness(botId, moleVictimId, bot.Position))
            {
                PerformKill(botId, bot, moleVictimId);
                return;
            }

            // ── P0-2 平衡：卧底避险 ──
            // 卧底是警方必要条件（被拔除即全局败）。此前 UC bot 埋头做任务，
            // 黑帮 bot 34 秒抓到即终局。这里不透视身份——任何人逼近到 3 单位内
            // 就中断任务撤向"最近的第三方活人"（黑帮有目击约束不敢当众动手），
            // 没有第三方时直线远离威胁。脱险后 think 正常重新领任务。
            if (role == OnlineRole.Undercover && ShouldEvadeThreat(botId, bot.Position, out Vector3 safeSpot))
            {
                SetBotGoal(botId, safeSpot, planPath: true);
                _currentTaskId.Remove(botId);
                _ctrl.Players[botId] = bot;
                return;
            }

            // ── 警察方：做任务 + 修复 + 报案 ──
            // 紧急会议
            if (UnityEngine.Random.value < _ctrl.RuleSet.BotEmergencyMeetingProbability &&
                _ctrl.EmergencyMeetingsLeft > 0 &&
                _ctrl.TaskService.CommunicationJamTimer <= 0f &&
                Vector3.Distance(bot.Position, _ctrl.MapService.ScaleMapPosition(Vector3.zero)) <= _ctrl.RuleSet.ReportRangeFor(_ctrl.Players.Count))
            {
                _ctrl.CallEmergencyMeeting(bot.DisplayName);
                return;
            }

            // 使用职业能力
            if (UnityEngine.Random.value < 0.1f)
            {
                TryUseProfessionAbility(botId, bot, profession);
                return;
            }

            // 粘性任务目标（P0-2 节奏修复）：当前任务未完成时不换目标。
            // 此前每次 think（1.2~3.4s）随机重选，bot 到达后平均 ~2.3s 即被改派，
            // 2.5s 的任务进度被反复清零，自然采样任务率因此≈0。
            bool keepCurrentTask = false;
            if (_currentTaskId.TryGetValue(botId, out int stickyId) &&
                stickyId >= 0 && stickyId < _ctrl.Tasks.Count)
            {
                OnlineTaskState sticky = _ctrl.TaskForPlayer(botId, _ctrl.Tasks[stickyId]);
                if (!sticky.Completed && !sticky.Sabotaged && _ctrl.IsTaskVisibleToPlayer(botId, sticky))
                {
                    keepCurrentTask = true;
                    _targets[botId] = sticky.Position;
                }
                else
                {
                    _currentTaskId.Remove(botId);
                }
            }

            if (!keepCurrentTask)
            {
                // 选择任务目标并主动寻路（新目标作废旧路点与进度追踪基线）
                SetBotGoal(botId, PickEvidenceTarget(botId), planPath: true);

                // 记录当前任务
                foreach (var task in _ctrl.Tasks)
                {
                    OnlineTaskState playerTask = _ctrl.TaskForPlayer(botId, task);
                    if (!playerTask.Completed && !playerTask.Sabotaged &&
                        Vector3.Distance(playerTask.Position, _targets[botId]) < 0.01f)
                    {
                        _currentTaskId[botId] = playerTask.Id;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// P0-2: 统一目标设定——清进度追踪并（可选）主动 BFS 规划矩形链路点。
        /// 主动寻路取代"直线走到卡死再救"：贴墙蹭动+距离震荡会骗过全部停滞检测，
        /// 采样实测 bot 在半堵位置冻结十几分钟而 BFS 从未获得执行机会。
        /// 避险（planPath=false）走直线+滑动，威胁目标高频变化不宜每次规划。
        /// </summary>
        private void SetBotGoal(ulong botId, Vector3 goal, bool planPath)
        {
            _targets[botId] = goal;
            _lastGoalDistances.Remove(botId);
            _noProgressTimers[botId] = 0f;
            _waypointStallTimers.Remove(botId);
            _lastWaypointDistances.Remove(botId);
            _waypoints.Remove(botId);
            _relayCounts.Remove(botId);

            if (planPath && _ctrl.Players.TryGetValue(botId, out OnlinePlayerState self))
            {
                List<Vector3> planned = PlanPathThroughWalkableRects(self.Position, goal);
                if (planned != null && planned.Count > 0)
                {
                    _waypoints[botId] = planned;
                }
            }
        }

        // ── 击杀 ──

        private void PerformKill(ulong botId, OnlinePlayerState bot, ulong victimId)
        {
            _ctrl.TryKill(botId, bot, victimId);
        }

        // ── 职业能力 ──

        private void TryUseProfessionAbility(ulong botId, OnlinePlayerState bot, OnlineProfession profession)
        {
            // BodyDrag: 寻找附近尸体拖动到暗处
            if (_ctrl.RuleSet.HasAbility(profession, AbilityType.BodyDrag))
            {
                for (int i = 0; i < _ctrl.Bodies.Count; i++)
                {
                    if (!_ctrl.Bodies[i].Reported &&
                        Vector3.Distance(bot.Position, _ctrl.Bodies[i].Position) < BotInteractDistance * 1.5f)
                    {
                        // 拖动尸体到最近的暗线节点
                        Vector3 ventPos = _ctrl.MapService.UnderworldPassagePosition(0, _ctrl.RuleSet.UnderworldPassageCount);
                        var body = _ctrl.Bodies[i];
                        body.Position = ventPos;
                        _ctrl.Bodies[i] = body;
                        return;
                    }
                }
            }

            // DarkVision: 已在黑灯逻辑中处理（无需额外操作）
            // RemoteSurveillance: Bot 自动获取附近玩家位置
            if (_ctrl.RuleSet.HasAbility(profession, AbilityType.RemoteSurveillance))
            {
                // Bot 只能按公开嫌疑判断，不能读取其他玩家的私密身份。
                ulong suspectId = 0UL;
                int bestSuspicion = int.MinValue;
                foreach (var pair in _ctrl.Players)
                {
                    if (pair.Value.Alive && pair.Key != botId && pair.Value.Suspicion > bestSuspicion)
                    {
                        suspectId = pair.Key;
                        bestSuspicion = pair.Value.Suspicion;
                    }
                }
                if (suspectId != 0UL) _ctrl.AddSuspicion(suspectId, 1);
                return;
            }

            // FootprintTrack: 标记最近的非警玩家
            if (_ctrl.RuleSet.HasAbility(profession, AbilityType.FootprintTrack))
            {
                ulong suspectId = 0UL;
                int bestSuspicion = int.MinValue;
                foreach (var pair in _ctrl.Players)
                {
                    if (pair.Value.Alive && pair.Key != botId && pair.Value.Suspicion > bestSuspicion)
                    {
                        suspectId = pair.Key;
                        bestSuspicion = pair.Value.Suspicion;
                    }
                }
                if (suspectId != 0UL) _ctrl.AddSuspicion(suspectId, 1);
                return;
            }
        }

        // ── 通风管 ──

        private void TryUseVent(ulong botId, OnlinePlayerState bot)
        {
            float cd = _ventCooldowns.TryGetValue(botId, out float vcd) ? vcd : 0f;
            if (cd > 0f) return;

            // 寻找最近的暗线节点
            float bestDist = float.MaxValue;
            int bestVent = -1;
            for (int i = 0; i < _ctrl.RuleSet.UnderworldPassageCount; i++)
            {
                float dist = Vector3.Distance(bot.Position,
                    _ctrl.MapService.UnderworldPassagePosition(i, _ctrl.RuleSet.UnderworldPassageCount));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestVent = i;
                }
            }

            if (bestVent >= 0 && bestDist < BotInteractDistance * 3f)
            {
                // 传送到随机另一个节点
                int targetVent = (bestVent + 1 + UnityEngine.Random.Range(0, _ctrl.RuleSet.UnderworldPassageCount - 1))
                    % _ctrl.RuleSet.UnderworldPassageCount;
                bot.Position = _ctrl.MapService.UnderworldPassagePosition(targetVent, _ctrl.RuleSet.UnderworldPassageCount);
                _ctrl.Players[botId] = bot;
                _ventCooldowns[botId] = _ctrl.RuleSet.VentCooldownSeconds;
            }
        }

        /// <summary>
        /// M8.3: 移动 Bot 向目标，应用职业速度倍率。
        /// </summary>
        private void MoveBotTowardTarget(ulong botId, OnlinePlayerState bot, OnlineRole role, OnlineProfession profession)
        {
            // ── 卡住/停滞检测（两种，互补）──
            // A) 位置卡死：长时间位移 < 阈值（完全堵死的硬卡），累计计时。
            float stuckTimer = _stuckTimers.TryGetValue(botId, out float existing) ? existing : 0f;
            bool barelyMoved = _lastPositions.TryGetValue(botId, out Vector3 lastPos)
                && Vector3.Distance(bot.Position, lastPos) < StuckDistanceThreshold;
            stuckTimer = barelyMoved ? stuckTimer + Time.deltaTime : 0f;
            _stuckTimers[botId] = stuckTimer;

            // B) 进度停滞：bot 还能小幅蹭动（贴墙滑动骗过 A），但距目标长期不缩短。
            //    诊断证明这是主要形态：stuck 从未达 5s、bot 却整局到不了任务点。
            Vector3 progressTarget = _targets.TryGetValue(botId, out Vector3 trackedTarget) ? trackedTarget : bot.Position;
            float currentGoalDistance = Vector3.Distance(bot.Position, progressTarget);
            float lastGoalDistance = _lastGoalDistances.TryGetValue(botId, out float remembered) ? remembered : currentGoalDistance;
            bool makingProgress = currentGoalDistance < lastGoalDistance - 0.4f;
            _lastGoalDistances[botId] = makingProgress ? currentGoalDistance : Mathf.Min(lastGoalDistance, currentGoalDistance);
            float noProgressTimer = _noProgressTimers.TryGetValue(botId, out float np) ? np : 0f;
            noProgressTimer = makingProgress ? 0f : noProgressTimer + Time.deltaTime;
            _noProgressTimers[botId] = noProgressTimer;

            if (stuckTimer > StuckTimeBeforeReroute || noProgressTimer > NoProgressTimeBeforeReplan)
            {
                _stuckTimers[botId] = 0f;
                _noProgressTimers[botId] = 0f;
                if (HandleBotBlocked(botId, bot))
                {
                    return;
                }
            }

            _lastPositions[botId] = bot.Position;

            Vector3 target = _targets.TryGetValue(botId, out Vector3 t) ? t : bot.Position;

            // P0-2: 寻路路点队列优先——依次走到每个矩形链路点，走完再奔最终目标。
            // 路点消化停滞监控：队首路点是固定的，与它的距离只减不增才正常；
            // 震荡不前（贴墙蹭动骗过位置/进度检测的形态）累计 WaypointStallSeconds
            // 即按阻塞处理（HandleBotBlocked 会重规划或直接传送到队首路点）。
            if (_waypoints.TryGetValue(botId, out List<Vector3> pathWaypoints) && pathWaypoints.Count > 0)
            {
                Vector3 waypoint = pathWaypoints[0];
                float waypointDistance = Vector3.Distance(bot.Position, waypoint);
                if (waypointDistance <= BotInteractDistance * 1.6f)
                {
                    pathWaypoints.RemoveAt(0);
                    _waypointStallTimers.Remove(botId);
                    if (pathWaypoints.Count == 0)
                    {
                        _waypoints.Remove(botId);
                    }
                }
                else
                {
                    float lastWaypointDistance = _lastWaypointDistances.TryGetValue(botId, out float lwd) ? lwd : waypointDistance;
                    bool approachingWaypoint = waypointDistance < lastWaypointDistance - 0.2f;
                    _lastWaypointDistances[botId] = approachingWaypoint ? waypointDistance : Mathf.Min(lastWaypointDistance, waypointDistance);
                    float waypointStall = (_waypointStallTimers.TryGetValue(botId, out float ws) ? ws : 0f) + (approachingWaypoint ? 0f : Time.deltaTime);
                    if (approachingWaypoint)
                    {
                        _waypointStallTimers.Remove(botId);
                    }
                    else
                    {
                        _waypointStallTimers[botId] = waypointStall;
                        if (waypointStall > WaypointStallSeconds && HandleBotBlocked(botId, bot))
                        {
                            return;
                        }
                    }

                    target = waypoint;
                }
            }
            else if (_targets.ContainsKey(botId) && Vector3.Distance(bot.Position, target) <= BotInteractDistance)
            {
                // 到达最终目标：清中转计数，为下一个目标重置
                _relayCounts.Remove(botId);
            }

            Vector3 delta = target - bot.Position;
            Vector2 direction = new Vector2(delta.x, delta.y);

            if (direction.magnitude <= BotInteractDistance)
            {
                bot.Input = Vector2.zero;
                _ctrl.Players[botId] = bot;
                return;
            }

            bot.Input = direction.normalized;

            // Driver 移动速度微增
            if (profession == OnlineProfession.Driver)
                bot.Input *= 1.08f;

            bot.Input *= Mathf.Clamp(_ctrl.RuleSet.BotMoveSpeedMultiplier, 0.5f, 4f);

            _ctrl.Players[botId] = bot;
        }

        /// <summary>获取职业任务速度倍率</summary>
        private float GetTaskSpeedMultiplier(OnlineProfession profession)
        {
            ProfessionAbilitySet? abs = _ctrl.RuleSet.GetProfessionAbilities(profession);
            if (abs?.HasAbility(AbilityType.TaskSpeedBonus) == true)
                return abs.Value.GetMultiplier(AbilityType.TaskSpeedBonus);
            return 1f;
        }

        /// <summary>
        /// P0-2 平衡：卧底/关键角色避险判定。任何人逼近到 3 单位内即视为威胁；
        /// 撤离点优先取"离威胁者 4 单位外最近的第三方活人"（目击约束反利用），
        /// 没有可用第三方时直线远离威胁 6 单位。
        /// </summary>
        private bool ShouldEvadeThreat(ulong selfId, Vector3 position, out Vector3 safeSpot)
        {
            safeSpot = position;
            bool threatened = false;
            float threatDistance = float.MaxValue;
            Vector3 threatPosition = Vector3.zero;
            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in _ctrl.Players)
            {
                if (pair.Key == selfId || !pair.Value.Alive)
                {
                    continue;
                }

                float distance = Vector3.Distance(pair.Value.Position, position);
                if (distance < 3f && distance < threatDistance)
                {
                    threatened = true;
                    threatDistance = distance;
                    threatPosition = pair.Value.Position;
                }
            }

            if (!threatened)
            {
                return false;
            }

            float best = float.MaxValue;
            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in _ctrl.Players)
            {
                if (pair.Key == selfId || !pair.Value.Alive)
                {
                    continue;
                }

                if (Vector3.Distance(pair.Value.Position, threatPosition) < 4f)
                {
                    continue; // 离威胁者太近的同伴不算安全锚点
                }

                float distanceToSelf = Vector3.Distance(pair.Value.Position, position);
                if (distanceToSelf < best)
                {
                    best = distanceToSelf;
                    safeSpot = pair.Value.Position;
                }
            }

            if (best < float.MaxValue)
            {
                return true;
            }

            Vector3 away = position + (position - threatPosition).normalized * 6f;
            safeSpot = away;
            return true;
        }

        /// <summary>
        /// P0-2 平衡：击杀目击者检查——攻击者 7 单位内存在第三方活人时不动手
        /// （模拟真人黑帮"有人看着就收手"，同时给会议留下位置信息博弈；
        /// 无此约束时 bot 会 39 秒无差别锁定击杀关键角色直接终局）。
        /// </summary>
        private bool HasNearbyWitness(ulong attackerId, ulong victimId, Vector3 attackerPosition)
        {
            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in _ctrl.Players)
            {
                if (pair.Key == attackerId || pair.Key == victimId || !pair.Value.Alive)
                {
                    continue;
                }

                if (Vector3.Distance(pair.Value.Position, attackerPosition) < 7f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// P0-2: 卡墙统一处理。分级：①已有路点链但走不动（路点停滞触发）→ 冷却允许时
        /// 直接传送到队首路点（矩形重叠中心，必在可行区，比传送最近开阔点更对症）；
        /// ②无路点 → BFS 规划新路径；③矩形图不可达 → 放弃目标；④8 方向全堵死角 →
        /// FindNearestOpenPosition 传送自救。返回 true 表示已处理（本帧结束移动）。
        /// </summary>
        private bool HandleBotBlocked(ulong botId, OnlinePlayerState bot)
        {
            // ①: 已在走路点链却长期停滞——传送跳过卡点（服务端权威位置，客户端经快照同步）
            if (_waypoints.TryGetValue(botId, out List<Vector3> existing) && existing.Count > 0)
            {
                float cooldown = _rescueCooldowns.TryGetValue(botId, out float cd) ? cd : 0f;
                if (cooldown <= 0f)
                {
                    bot.Position = existing[0];
                    _ctrl.Players[botId] = bot;
                    _rescueCooldowns[botId] = RescueCooldownSeconds;
                    _waypointStallTimers.Remove(botId);
                    _lastWaypointDistances.Remove(botId);
                    return true;
                }

                // 冷却中：清停滞计时，等冷却结束后再试
                _waypointStallTimers.Remove(botId);
                return false;
            }

            Vector3 stuckTarget = _targets.TryGetValue(botId, out Vector3 originalTarget)
                ? originalTarget
                : bot.Position;

            // ②: walkableRects BFS 寻路——卡墙说明直线不可达，
            // 沿可行走矩形链走到目标（取代此前"最近房间中心"单点中转）。
            List<Vector3> path = PlanPathThroughWalkableRects(bot.Position, stuckTarget);
            if (path != null && path.Count > 0)
            {
                _waypoints[botId] = path;
                _ctrl.Players[botId] = bot;
                return true;
            }

            _targets.Remove(botId);
            // 与粘性任务目标联动：卡墙放弃当前任务，避免 think 把目标设回原位死循环
            if (_currentTaskId.TryGetValue(botId, out int abandonedTaskId))
            {
                // P0-2: 弃任务时记入个人不可达黑名单——否则 think 随机重选
                // 又可能选回同一任务，形成"直线卡死→弃→重选→再卡死"整局循环。
                if (!_unreachableTasks.TryGetValue(botId, out HashSet<int> blocked))
                {
                    blocked = new HashSet<int>();
                    _unreachableTasks[botId] = blocked;
                }

                blocked.Add(abandonedTaskId);
                _currentTaskId.Remove(botId);
            }

            _waypoints.Remove(botId);
            _relayCounts.Remove(botId);
            _lastGoalDistances.Remove(botId);
            bot.Input = Vector2.zero;

            // ④: 深度卡死自救——8 方向大面积堵死（几何死角/出生点异常）时，
            // 传送到最近开阔点脱困，带冷却防弹跳。
            if (IsPositionDeeplyBlocked(bot.Position) && TryRescueBot(botId, ref bot))
            {
                return true;
            }

            _ctrl.Players[botId] = bot;
            return true;
        }

        /// <summary>P0-2: 判定位置是否被几何深度堵死（8 方向 1.2 单位步进 ≤2 个可走）。</summary>
        private bool IsPositionDeeplyBlocked(Vector3 position)
        {
            int open = 0;
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                Vector3 step = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.2f;
                if (_ctrl.IsWalkable(step)) open++;
            }

            return open <= 2;
        }

        // ── P0-2: walkableRects 粗寻路（BFS 邻接图） ──

        /// <summary>矩形邻接余量：两矩形（各扩此值后）相交即视为 bot 可通行。</summary>
        private const float RectAdjacencyPadding = 0.5f;
        /// <summary>P0-2 平衡：黑帮追击同一目标超过此时长未得手即"追击疲劳"换活动。</summary>
        private const float ChaseFatigueSeconds = 25f;
        /// <summary>P0-2 平衡：追击疲劳后的再追杀冷却。</summary>
        private const float ChaseCooldownSeconds = 30f;

        /// <summary>
        /// 在可行走矩形上规划 from→to 的路点链（相邻矩形重叠区中心）。
        /// 返回 null 表示不可达（或矩形图未覆盖）；空链表示同矩形直达。
        /// 近似寻路：假边（角对角接触但实际被障碍挡）由卡墙→重新规划的循环兜底。
        /// </summary>
        private List<Vector3> PlanPathThroughWalkableRects(Vector3 from, Vector3 to)
        {
            var rects = _ctrl.WalkableRects;
            if (rects == null || rects.Count == 0)
            {
                return null;
            }

            int start = FindRectIndexContaining(rects, from);
            int goal = FindRectIndexContaining(rects, to);
            if (goal < 0)
            {
                return null; // 目标不在可行走区（任务点未 snap 等异常）
            }

            if (start < 0)
            {
                // bot 在矩形外（贴边/被挤出）：从离 from 最近的矩形出发
                start = FindNearestRectIndex(rects, from);
                if (start < 0)
                {
                    return null;
                }
            }

            if (start == goal)
            {
                return new List<Vector3>(); // 同矩形，直线可达（卡墙另由滑动/自救兜底）
            }

            // BFS
            var prev = new int[rects.Count];
            for (int i = 0; i < prev.Length; i++) prev[i] = -1;
            var queue = new Queue<int>();
            prev[start] = start;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == goal) break;
                for (int i = 0; i < rects.Count; i++)
                {
                    if (prev[i] >= 0 || !RectsAdjacent(rects[current], rects[i])) continue;
                    prev[i] = current;
                    queue.Enqueue(i);
                }
            }

            if (prev[goal] < 0)
            {
                return null; // 矩形图上不连通
            }

            // 回溯矩形序列 → 相邻矩形重叠区中心作路点
            var rectPath = new List<int>();
            int node = goal;
            while (node != start)
            {
                rectPath.Add(node);
                node = prev[node];
            }

            rectPath.Reverse();
            var waypoints = new List<Vector3>();
            int previous = start;
            for (int i = 0; i < rectPath.Count; i++)
            {
                Vector3 hinge = RectOverlapCenter(rects[previous], rects[rectPath[i]]);
                // 过滤离当前位置过近的路点（减少绕行）
                if (waypoints.Count == 0 || Vector3.Distance(waypoints[waypoints.Count - 1], hinge) > 0.5f)
                {
                    waypoints.Add(hinge);
                }

                previous = rectPath[i];
            }

            return waypoints;
        }

        private static int FindRectIndexContaining(IReadOnlyList<Rect> rects, Vector3 position)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Contains(new Vector2(position.x, position.y)))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindNearestRectIndex(IReadOnlyList<Rect> rects, Vector3 position)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            Vector2 point = new Vector2(position.x, position.y);
            for (int i = 0; i < rects.Count; i++)
            {
                float cx = Mathf.Clamp(point.x, rects[i].xMin, rects[i].xMax);
                float cy = Mathf.Clamp(point.y, rects[i].yMin, rects[i].yMax);
                float distance = Vector2.Distance(new Vector2(cx, cy), point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static bool RectsAdjacent(Rect a, Rect b)
        {
            Rect expandedA = new Rect(
                a.xMin - RectAdjacencyPadding, a.yMin - RectAdjacencyPadding,
                a.width + RectAdjacencyPadding * 2f, a.height + RectAdjacencyPadding * 2f);
            return expandedA.Overlaps(b);
        }

        private static Vector3 RectOverlapCenter(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax >= xMin && yMax >= yMin)
            {
                return new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);
            }

            // 间隙邻接（padding 相交）：取两矩形中心连线中点
            return new Vector3((a.center.x + b.center.x) * 0.5f, (a.center.y + b.center.y) * 0.5f, 0f);
        }

        /// <summary>P0-2: 深度卡死自救——传送到最近开阔点（服务端权威位置，客户端经快照同步）。
        /// 30s 冷却防止在死角与开阔点之间弹跳。返回 true 表示已脱困。</summary>
        private bool TryRescueBot(ulong botId, ref OnlinePlayerState bot)
        {
            float cooldown = _rescueCooldowns.TryGetValue(botId, out float cd) ? cd : 0f;
            if (cooldown > 0f)
            {
                return false;
            }

            Vector3 rescued = _ctrl.FindNearestOpenPosition(bot.Position, bot.Position);
            if (rescued == bot.Position)
            {
                return false;
            }

            bot.Position = rescued;
            _ctrl.Players[botId] = bot;
            _rescueCooldowns[botId] = RescueCooldownSeconds;
            return true;
        }

        /// <summary>
        /// P0-2: 选一个卡墙中转用的房间中心——不选脚下的（<1 单位）和太远的（>12 单位），
        /// 按"离目标近优先、离自己近微加成"打分。走房间网络绕开直线穿不过的墙。
        /// </summary>
        private Vector3? FindNearestRoomRelay(Vector3 position, Vector3 target)
        {            OnlineMapService.OnlineMapType mapType = _ctrl.MapService.ActiveMapType;
            OnlineMapService.ShipRoomSpec[] rooms = mapType switch
            {
                OnlineMapService.OnlineMapType.PoliceStation => _ctrl.MapService.PoliceStationRooms(),
                OnlineMapService.OnlineMapType.KowloonWalledCity => _ctrl.MapService.KowloonWalledCityRooms(),
                _ => _ctrl.MapService.HarbourDistrictRooms(),
            };

            Vector3? best = null;
            float bestScore = float.MaxValue;
            foreach (OnlineMapService.ShipRoomSpec room in rooms)
            {
                Vector3 center = room.Center;
                float distToSelf = Vector3.Distance(center, position);
                if (distToSelf < 1.0f || distToSelf > 12f)
                {
                    continue;
                }

                float score = Vector3.Distance(center, target) - 0.3f * distToSelf;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = center;
                }
            }

            return best;
        }

        /// <summary>
        /// 驱动 Bot 会议投票。
        /// </summary>
        public void TickBotVoting(float deltaTime)
        {
            List<ulong> botIds = new List<ulong>();

            foreach (OnlinePlayerState state in _ctrl.Players.Values)
            {
                if (state.IsBot && state.Alive && !_ctrl.HasVoted(state.ClientId))
                {
                    botIds.Add(state.ClientId);
                }
            }

            foreach (ulong botId in botIds)
            {
                _voteTimers[botId] = _voteTimers.TryGetValue(botId, out float timer)
                    ? timer - deltaTime
                    : UnityEngine.Random.Range(1.2f, 4.5f);

                if (_voteTimers[botId] > 0f)
                {
                    continue;
                }

                _ctrl.ApplyVote(botId, PickBotVoteTarget(botId));
                _voteTimers[botId] = UnityEngine.Random.Range(2f, 5f);
            }
        }

        // ── 目标选择 ──

        /// <summary>
        /// 为 Bot 选择行走目标。
        /// </summary>
        public Vector3 PickBotTarget(ulong botId)
        {
            if (_ctrl.GetPrivateRole(botId) == OnlineRole.Gang)
            {
                return UnityEngine.Random.value < 0.55f ? PickNearestLivingNonGang(botId) : PickSabotageTarget();
            }

            return PickEvidenceTarget(botId);
        }

        private Vector3 PickEvidenceTarget(ulong botId)
        {
            List<OnlineTaskState> options = new List<OnlineTaskState>();
            HashSet<int> unreachable = null;
            if (_unreachableTasks.TryGetValue(botId, out HashSet<int> blocked))
            {
                unreachable = blocked;
            }

            foreach (OnlineTaskState task in _ctrl.Tasks)
            {
                if (unreachable != null && unreachable.Contains(task.Id))
                {
                    continue; // P0-2: BFS 判定过不可达的任务不再死循环重选
                }

                OnlineTaskState playerTask = _ctrl.TaskForPlayer(botId, task);
                if (_ctrl.IsTaskVisibleToPlayer(botId, playerTask) && (!playerTask.Completed || playerTask.Sabotaged))
                {
                    options.Add(playerTask);
                }
            }

            if (options.Count == 0)
            {
                return _ctrl.MapService.ScaleMapPosition(Vector3.zero);
            }

            return options[UnityEngine.Random.Range(0, options.Count)].Position;
        }

        private Vector3 PickSabotageTarget()
        {
            if (_ctrl.Tasks.Count == 0)
            {
                return _ctrl.MapService.ScaleMapPosition(Vector3.zero);
            }

            if (UnityEngine.Random.value < 0.4f)
            {
                foreach (OnlineTaskState task in _ctrl.Tasks)
                {
                    if (task.Id == 2)
                    {
                        return task.Position;
                    }
                }
            }

            return _ctrl.Tasks[UnityEngine.Random.Range(0, _ctrl.Tasks.Count)].Position;
        }

        private Vector3 PickNearestLivingNonGang(ulong botId)
        {
            if (!_ctrl.Players.TryGetValue(botId, out OnlinePlayerState bot))
            {
                return PickSabotageTarget();
            }

            Vector3 best = PickSabotageTarget();
            float bestDistance = float.MaxValue;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in _ctrl.Players)
            {
                if (!_ctrl.CanKillTarget(botId, pair.Key))
                {
                    continue;
                }

                float distance = Vector3.Distance(bot.Position, pair.Value.Position);

                if (distance < bestDistance)
                {
                    best = pair.Value.Position;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // ── 投票目标（M8.3 升级：基于嫌疑值+角色） ──

        public ulong PickBotVoteTarget(ulong voterClientId)
        {
            OnlineRole voterRole = _ctrl.GetPrivateRole(voterClientId);

            // 收集存活的其他玩家及其嫌疑值
            var candidates = new List<(ulong clientId, float weight)>();
            foreach (var pair in _ctrl.Players)
            {
                if (!pair.Value.Alive || pair.Key == voterClientId)
                    continue;

                OnlineRole targetRole = pair.Value.PublicRole;
                float weight = 0f;

                // 阵营基础权重
                if (voterRole == OnlineRole.Gang || voterRole == OnlineRole.Mole)
                {
                    // 黑帮 Bot 只按表面身份和公开嫌疑判断。
                    if (targetRole == OnlineRole.Police)
                        weight = 30f;
                    else
                        weight = 4f;
                }
                else
                {
                    // 警察投黑帮
                    if (targetRole == OnlineRole.Gang)
                        weight = 25f;
                    else
                        weight = 5f + UnityEngine.Random.Range(0f, 10f);
                }

                // 嫌疑值加成
                weight += pair.Value.Suspicion * 4f;

                // 随机噪声
                weight += UnityEngine.Random.Range(-5f, 5f);

                if (weight > 0f)
                    candidates.Add((pair.Key, weight));
            }

            // 有时跳票
            if (candidates.Count == 0 || UnityEngine.Random.value < 0.15f)
                return SkipVoteTarget;

            // 加权随机选择
            float totalWeight = 0f;
            foreach (var c in candidates) totalWeight += c.weight;
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float acc = 0f;
            foreach (var c in candidates)
            {
                acc += c.weight;
                if (roll <= acc) return c.clientId;
            }

            return candidates[candidates.Count - 1].clientId;
        }
    }
}
