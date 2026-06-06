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

        // M8.3 新增状态
        private readonly Dictionary<ulong, float> _taskProgress = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _repairProgress = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _ventCooldowns = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, int> _currentTaskId = new Dictionary<ulong, int>();
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

            foreach (ulong botId in botIds)
            {
                TickSingleBot(botId, deltaTime);
            }
        }

        private void TickSingleBot(ulong botId, float deltaTime)
        {
            OnlinePlayerState bot = _ctrl.Players[botId];
            OnlineRole role = _ctrl.GetPrivateRole(botId);
            OnlineProfession profession = bot.Profession;

            _thinkTimers.TryGetValue(botId, out float thinkTimer);
            thinkTimer -= deltaTime;

            // ── 1) 尸体发现与报案 ──
            if (_ctrl.TryFindNearestBody(bot.Position, out int bodyIndex) &&
                Vector3.Distance(bot.Position, _ctrl.Bodies[bodyIndex].Position) < BotInteractDistance)
            {
                if (UnityEngine.Random.value < 0.42f &&
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

            if (dist > BotInteractDistance * 2f)
            {
                // 远离目标，重置进度
                _taskProgress[botId] = 0f;
                _repairProgress[botId] = 0f;
                return;
            }

            bool isGangSide = (role == OnlineRole.Gang || role == OnlineRole.Mole);

            // 警察方：推进任务或修复
            if (!isGangSide)
            {
                // 检查是否有被破坏的任务需要修复
                foreach (var task in _ctrl.Tasks)
                {
                    if (task.Sabotaged && !task.Completed &&
                        Vector3.Distance(bot.Position, task.Position) < BotInteractDistance * 1.5f)
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
                    var task = _ctrl.Tasks[taskId];
                    if (!task.Completed && !task.Sabotaged &&
                        Vector3.Distance(bot.Position, task.Position) < BotInteractDistance * 1.5f)
                    {
                        float prog = _taskProgress.TryGetValue(botId, out float tp) ? tp : 0f;
                        prog += deltaTime * GetTaskSpeedMultiplier(profession);
                        _taskProgress[botId] = prog;

                        if (prog >= BotTaskCompleteSeconds)
                        {
                            _taskProgress[botId] = 0f;
                            _ctrl.TryInteractWithTask(botId, bot);
                            _completedTaskCount++;
                            _currentTaskId.Remove(botId);
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
                        Vector3.Distance(bot.Position, task.Position) < BotInteractDistance * 1.5f &&
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
            // ──  黑帮方：追杀 + 破坏 + 通风管 ──
            if (role == OnlineRole.Gang || role == OnlineRole.Mole)
            {
                // 击杀逻辑
                if (_ctrl.TryGetKillCooldown(botId, out float kcd) && kcd <= 0f &&
                    _ctrl.TryFindNearestVictim(bot.Position, out ulong victimId, out OnlinePlayerState victim))
                {
                    if (Vector3.Distance(bot.Position, victim.Position) < BotInteractDistance)
                    {
                        PerformKill(botId, bot, victimId, victim);
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
                if (UnityEngine.Random.value < 0.55f)
                    _targets[botId] = PickNearestLivingNonGang(botId);
                else
                    _targets[botId] = PickSabotageTarget();

                return;
            }

            // ── 警察方：做任务 + 修复 + 报案 ──
            // 紧急会议
            if (UnityEngine.Random.value < 0.12f &&
                _ctrl.EmergencyMeetingsLeft > 0 &&
                _ctrl.TaskService.CommunicationJamTimer <= 0f &&
                Vector3.Distance(bot.Position, _ctrl.MapService.ScaleMapPosition(Vector3.zero)) <= _ctrl.RuleSet.ReportRange)
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

            // 选择任务目标
            _targets[botId] = PickEvidenceTarget();

            // 记录当前任务
            foreach (var task in _ctrl.Tasks)
            {
                if (!task.Completed && !task.Sabotaged &&
                    Vector3.Distance(task.Position, _targets[botId]) < 0.01f)
                {
                    _currentTaskId[botId] = task.Id;
                    break;
                }
            }
        }

        // ── 击杀 ──

        private void PerformKill(ulong botId, OnlinePlayerState bot, ulong victimId, OnlinePlayerState victim)
        {
            float killRange = _ctrl.RuleSet.KillRange;
            ProfessionAbilitySet? abilities = _ctrl.RuleSet.GetProfessionAbilities(bot.Profession);
            if (abilities?.HasAbility(AbilityType.KillRangeBonus) == true)
                killRange += abilities.Value.GetBonus(AbilityType.KillRangeBonus);

            victim.Alive = false;
            victim.Input = Vector2.zero;
            _ctrl.Players[victimId] = victim;
            _ctrl.Bodies.Add(new OnlineBodyState(_ctrl.NextBodyId, victimId, victim.Position, false));
            _ctrl.IncrementNextBodyId();

            float cooldown = _ctrl.RuleSet.KillCooldownSeconds;
            if (abilities?.HasAbility(AbilityType.KillCooldownReduce) == true)
                cooldown *= abilities.Value.GetMultiplier(AbilityType.KillCooldownReduce);
            _ctrl.SetKillCooldown(botId, cooldown);

            _ctrl.Status = bot.DisplayName + " 击倒了 " + victim.DisplayName + "。";
            _ctrl.AddCaseLog(_ctrl.Status);
            _ctrl.EvaluateWinConditions();
            _ctrl.BroadcastSnapshot();
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
                // 自动标记最近的嫌疑人
                foreach (var pair in _ctrl.Players)
                {
                    if (pair.Value.Alive && pair.Key != botId &&
                        _ctrl.GetPrivateRole(pair.Key) == OnlineRole.Gang)
                    {
                        _ctrl.AddSuspicion(pair.Key, 1);
                        break;
                    }
                }
                return;
            }

            // FootprintTrack: 标记最近的非警玩家
            if (_ctrl.RuleSet.HasAbility(profession, AbilityType.FootprintTrack))
            {
                foreach (var pair in _ctrl.Players)
                {
                    if (pair.Value.Alive && pair.Key != botId)
                    {
                        OnlineRole targetRole = _ctrl.GetPrivateRole(pair.Key);
                        if (targetRole == OnlineRole.Gang || targetRole == OnlineRole.Mole)
                        {
                            _ctrl.AddSuspicion(pair.Key, 1);
                            break;
                        }
                    }
                }
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
            Vector3 target = _targets.TryGetValue(botId, out Vector3 t) ? t : bot.Position;
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

            return PickEvidenceTarget();
        }

        private Vector3 PickEvidenceTarget()
        {
            List<OnlineTaskState> options = new List<OnlineTaskState>();

            foreach (OnlineTaskState task in _ctrl.Tasks)
            {
                if (!task.Completed || task.Sabotaged)
                {
                    options.Add(task);
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
                if (!pair.Value.Alive || pair.Key == botId || _ctrl.GetPrivateRole(pair.Key) == OnlineRole.Gang)
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

                OnlineRole targetRole = _ctrl.GetPrivateRole(pair.Key);
                float weight = 0f;

                // 阵营基础权重
                if (voterRole == OnlineRole.Gang || voterRole == OnlineRole.Mole)
                {
                    // 黑帮投非黑帮
                    if (targetRole != OnlineRole.Gang && targetRole != OnlineRole.Mole)
                        weight = 30f;
                    else
                        weight = -10f; // 不会投同伙
                }
                else
                {
                    // 警察投黑帮
                    if (targetRole == OnlineRole.Gang || targetRole == OnlineRole.Mole)
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
