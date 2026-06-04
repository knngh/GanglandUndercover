using System;
using System.Collections.Generic;
using UnityEngine;

using GanglandUndercover.Audio;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机 Bot 控制器。
    /// 负责 AI 玩家的创建、行为决策、投票和移动目标选择。
    /// 所有决策仅在 Host 端执行（权威模型）。
    /// </summary>
    public sealed class OnlineBotController
    {
        // ── 常量 ──
        public const ulong BotClientIdBase = 900000UL;
        private const float BotThinkMinSeconds = 1.2f;
        private const float BotThinkMaxSeconds = 3.4f;
        private const float BotInteractDistance = 0.45f;
        private const ulong SkipVoteTarget = ulong.MaxValue;

        // ── Bot 内部状态 ──
        private readonly Dictionary<ulong, float> _thinkTimers = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _voteTimers = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, Vector3> _targets = new Dictionary<ulong, Vector3>();

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
            OnlineProfession[] professions =
            {
                OnlineProfession.Inspector,
                OnlineProfession.Forensics,
                OnlineProfession.Tech,
                OnlineProfession.UndercoverAgent,
                OnlineProfession.Enforcer,
                OnlineProfession.Fixer,
                OnlineProfession.Driver
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
        }

        /// <summary>
        /// 清理所有 Bot 状态。
        /// </summary>
        public void Clear()
        {
            _thinkTimers.Clear();
            _voteTimers.Clear();
            _targets.Clear();
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
        /// 驱动 Bot 行动：移动、交互、击杀、破坏、报案。
        /// </summary>
        public void TickBotAction(float deltaTime)
        {
            List<ulong> botIds = new List<ulong>();

            foreach (OnlinePlayerState state in _ctrl.Players.Values)
            {
                if (state.IsBot && state.Alive)
                {
                    botIds.Add(state.ClientId);
                }
            }

            foreach (ulong botId in botIds)
            {
                OnlinePlayerState bot = _ctrl.Players[botId];
                _thinkTimers[botId] = _thinkTimers.TryGetValue(botId, out float timer) ? timer - deltaTime : 0f;

                if (_ctrl.TryFindNearestBody(bot.Position, out int bodyIndex) && UnityEngine.Random.value < 0.45f)
                {
                    OnlineBodyState body = _ctrl.Bodies[bodyIndex];
                    body.Reported = true;
                    _ctrl.Bodies[bodyIndex] = body;
                    _ctrl.Players[botId] = bot;
                    AudioManager.Instance?.PlaySFX(SoundEffect.BodyReport);
                    _ctrl.BeginMeeting(bot.DisplayName + " 发现尸体并报案");
                    return;
                }

                OnlineRole role = _ctrl.GetPrivateRole(botId);

                if (_thinkTimers[botId] <= 0f)
                {
                    _thinkTimers[botId] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);

                    if (role == OnlineRole.Gang || role == OnlineRole.Mole)
                    {
                        if (UnityEngine.Random.value < 0.08f)
                        {
                            _ctrl.TryUseProfessionAbility(botId, bot);
                            return;
                        }

                        if (_ctrl.TryGetKillCooldown(botId, out float cooldown) && cooldown <= 0f && _ctrl.TryFindNearestVictim(bot.Position, out ulong victimClientId, out OnlinePlayerState victim))
                        {
                            victim.Alive = false;
                            victim.Input = Vector2.zero;
                            _ctrl.Players[victimClientId] = victim;
                            _ctrl.Bodies.Add(new OnlineBodyState(_ctrl.NextBodyId, victimClientId, victim.Position, false));
                            _ctrl.IncrementNextBodyId();
                            _ctrl.SetKillCooldown(botId, _ctrl.RuleSet.KillCooldownSeconds);
                            _ctrl.Status = bot.DisplayName + " 在黑灯巷口击倒了 " + victim.DisplayName + "。";
                            _ctrl.AddCaseLog(_ctrl.Status);
                            _ctrl.EvaluateWinConditions();
                            _ctrl.BroadcastSnapshot();
                            return;
                        }

                        if (UnityEngine.Random.value < 0.36f)
                        {
                            _targets[botId] = PickSabotageTarget();
                        }
                    }
                    else if (UnityEngine.Random.value < 0.2f && _ctrl.TaskService.CommunicationJamTimer <= 0f && _ctrl.EmergencyMeetingsLeft > 0 && Vector3.Distance(bot.Position, _ctrl.MapService.ScaleMapPosition(Vector3.zero)) <= _ctrl.RuleSet.ReportRange)
                    {
                        _ctrl.DecrementEmergencyMeetings();
                        _ctrl.EmergencyCooldownTimer = _ctrl.RuleSet.EmergencyCooldownSeconds;
                        _ctrl.BeginMeeting(bot.DisplayName + " 按下警署紧急铃");
                        _ctrl.BroadcastSnapshot();
                        return;
                    }
                    else
                    {
                        if (UnityEngine.Random.value < 0.1f)
                        {
                            _ctrl.TryUseProfessionAbility(botId, bot);
                            return;
                        }

                        _targets[botId] = PickEvidenceTarget();
                    }
                }

                Vector3 target = _targets.TryGetValue(botId, out Vector3 currentTarget) ? currentTarget : PickBotTarget(botId);
                Vector3 delta = target - bot.Position;
                Vector2 direction = new Vector2(delta.x, delta.y);

                if (direction.magnitude <= BotInteractDistance)
                {
                    bot.Input = Vector2.zero;
                    _ctrl.Players[botId] = bot;

                    if (role == OnlineRole.Gang || UnityEngine.Random.value < 0.76f)
                    {
                        _ctrl.TryInteractWithTask(botId, bot);
                    }

                    _targets[botId] = PickBotTarget(botId);
                    _thinkTimers[botId] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);
                }
                else
                {
                    bot.Input = direction.normalized;
                    _ctrl.Players[botId] = bot;
                }
            }
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

        // ── 投票目标 ──

        public ulong PickBotVoteTarget(ulong voterClientId)
        {
            List<ulong> suspects = new List<ulong>();
            OnlineRole voterRole = _ctrl.GetPrivateRole(voterClientId);

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in _ctrl.Players)
            {
                if (!pair.Value.Alive || pair.Key == voterClientId)
                {
                    continue;
                }

                OnlineRole targetRole = _ctrl.GetPrivateRole(pair.Key);

                if (voterRole == OnlineRole.Gang && targetRole != OnlineRole.Gang)
                {
                    suspects.Add(pair.Key);
                }
                else if (voterRole != OnlineRole.Gang && targetRole == OnlineRole.Gang && UnityEngine.Random.value < 0.62f)
                {
                    suspects.Add(pair.Key);
                }
                else if (UnityEngine.Random.value < 0.28f)
                {
                    suspects.Add(pair.Key);
                }
            }

            if (suspects.Count == 0 || UnityEngine.Random.value < 0.18f)
            {
                return SkipVoteTarget;
            }

            return suspects[UnityEngine.Random.Range(0, suspects.Count)];
        }
    }
}
