using System.Collections.Generic;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 3.3+3.4: 卧底双身份 + 内鬼机制。
    /// Undercover: 潜伏→情报→背叛→结局
    /// Mole: 窃取→暗杀→翻盘→暴露
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        // ============================================================
        //  卧底系统 (Undercover)
        // ============================================================

        /// <summary>卧底情报值（key: clientId, value: intel points）</summary>
        private readonly Dictionary<ulong, int> _undercoverIntel = new Dictionary<ulong, int>();

        private const int UndercoverIntelBetrayalThreshold = 4; // 满4点可背叛
        private const int UndercoverMissionsRequired = 2;       // 需先完成2个伪装任务

        /// <summary>卧底已完成的 Gang 阵营伪装任务计数</summary>
        private readonly Dictionary<ulong, int> _undercoverMissionsDone = new Dictionary<ulong, int>();

        /// <summary>是否已背叛</summary>
        private readonly HashSet<ulong> _undercoverBetrayed = new HashSet<ulong>();

        public bool IsUndercover(ulong clientId)
        {
            return GetPrivateRole(clientId) == OnlineRole.Undercover;
        }

        public bool HasBetrayed(ulong clientId) => _undercoverBetrayed.Contains(clientId);

        /// <summary>卧底完成 Gang 任务时积累情报。</summary>
        public void AccumulateUndercoverIntel(ulong undercoverId, int amount = 1)
        {
            if (!IsUndercover(undercoverId)) return;
            if (HasBetrayed(undercoverId)) return;

            if (!_undercoverIntel.ContainsKey(undercoverId))
                _undercoverIntel[undercoverId] = 0;
            _undercoverIntel[undercoverId] += amount;

            if (!_undercoverMissionsDone.ContainsKey(undercoverId))
                _undercoverMissionsDone[undercoverId] = 0;
            _undercoverMissionsDone[undercoverId]++;
            SendIdentityProgress(undercoverId);
        }

        /// <summary>卧底是否可以背叛。</summary>
        public bool CanBetray(ulong undercoverId)
        {
            if (!IsUndercover(undercoverId)) return false;
            if (HasBetrayed(undercoverId)) return false;
            int intel = _undercoverIntel.TryGetValue(undercoverId, out int v) ? v : 0;
            int missions = _undercoverMissionsDone.TryGetValue(undercoverId, out int m) ? m : 0;
            return intel >= UndercoverIntelBetrayalThreshold && missions >= UndercoverMissionsRequired;
        }

        /// <summary>卧底执行背叛：公开身份切换为 Police 阵营。</summary>
        public bool ExecuteBetrayal(ulong undercoverId)
        {
            if (!CanBetray(undercoverId)) return false;
            _undercoverBetrayed.Add(undercoverId);

            if (players.TryGetValue(undercoverId, out var state))
            {
                state.PublicRole = OnlineRole.Police; // 公开身份切换
                players[undercoverId] = state;
            }

            status = $"⚠ 卧底 {undercoverId} 已背叛黑帮！";
            AddCaseLog(status);
            SendIdentityProgress(undercoverId);
            return true;
        }

        /// <summary>
        /// 兼容旧 UI/工具的卧底独赢查询。正式双渗透规则中卧底属于警方侧，
        /// 没有独立胜利条件，因此该旧接口始终返回 false。
        /// </summary>
        public bool CheckUndercoverSoloWin(ulong undercoverId)
        {
            return false;
        }

        public int GetUndercoverIntel(ulong undercoverId)
        {
            return _undercoverIntel.TryGetValue(undercoverId, out int v) ? v : 0;
        }

        internal List<GameStateSnapshot.SnapshotUndercoverStateEntry> UndercoverStatesSnapshot()
        {
            var states = new List<GameStateSnapshot.SnapshotUndercoverStateEntry>();
            foreach (KeyValuePair<ulong, OnlineRole> role in privateRoles)
            {
                if (role.Value != OnlineRole.Undercover)
                    continue;

                states.Add(new GameStateSnapshot.SnapshotUndercoverStateEntry
                {
                    ClientId = role.Key,
                    Intel = GetUndercoverIntel(role.Key),
                    MissionsDone = _undercoverMissionsDone.TryGetValue(role.Key, out int missions) ? missions : 0,
                    Betrayed = HasBetrayed(role.Key),
                });
            }
            return states;
        }

        // ============================================================
        //  内鬼系统 (Mole)
        // ============================================================

        /// <summary>内鬼暗杀目标列表</summary>
        private readonly Dictionary<ulong, ulong> _moleHitList = new Dictionary<ulong, ulong>();

        /// <summary>内鬼是否已暴露</summary>
        private readonly HashSet<ulong> _moleExposed = new HashSet<ulong>();

        public bool IsMole(ulong clientId)
        {
            return GetPrivateRole(clientId) == OnlineRole.Mole;
        }

        public bool IsMoleExposed(ulong clientId) => _moleExposed.Contains(clientId);

        /// <summary>Inspector 足迹追踪可能暴露内鬼。</summary>
        public void CheckMoleExposure(ulong inspectorId, ulong targetId)
        {
            if (!IsMole(targetId)) return;
            if (IsMoleExposed(targetId)) return;

            // Inspector 的 FootprintTrack 有几率暴露内鬼（30% 基础概率）
            if (UnityEngine.Random.value <= 0.3f)
            {
                _moleExposed.Add(targetId);
                if (players.TryGetValue(targetId, out var state))
                {
                    state.Suspicion = Mathf.Max(state.Suspicion, 60);
                    players[targetId] = state;
                }
                SendIdentityProgress(targetId);
            }
        }

        /// <summary>为 Mole 分配暗杀目标（Intel 满 5 时触发）。</summary>
        public ulong? AssignMoleHit(ulong moleId)
        {
            if (!IsMole(moleId)) return null;
            if (_moleHitList.TryGetValue(moleId, out ulong existingTarget)
                && players.TryGetValue(existingTarget, out OnlinePlayerState existingState)
                && existingState.Alive)
            {
                return existingTarget;
            }

            int intel = GetMoleIntel(moleId);
            if (intel < MoleIntelWinThreshold) return null;

            // 情报达标后识别仍在伪装的卧底，形成唯一暗杀目标。
            foreach (var kv in players)
            {
                if (GetPrivateRole(kv.Key) == OnlineRole.Undercover && kv.Value.Alive)
                {
                    _moleHitList[moleId] = kv.Key;
                    SendMoleTarget(moleId, kv.Key);
                    return kv.Key;
                }
            }
            return null;
        }

        public ulong? GetMoleHitTarget(ulong moleId)
        {
            return _moleHitList.TryGetValue(moleId, out ulong t) ? t : null;
        }

        /// <summary>检查内鬼是否已完成“锁定并清除卧底”的阵营目标。</summary>
        public bool CheckMoleSoloWin(ulong moleId)
        {
            if (!IsMole(moleId)) return false;
            int intel = GetMoleIntel(moleId);
            if (intel < MoleIntelWinThreshold) return false;
            if (!players.TryGetValue(moleId, out var ms) || !ms.Alive) return false;
            if (!_moleObjectives.TryGetValue(moleId, out MoleObjective objective) || objective.Kills <= 0)
                return false;

            // Mole 获胜: Intel ≥ 5 + 亲手清除已锁定的卧底目标。
            ulong? target = GetMoleHitTarget(moleId);
            if (target.HasValue && players.TryGetValue(target.Value, out var ts) && !ts.Alive)
                return true;

            return false;
        }

        internal List<GameStateSnapshot.SnapshotMoleStateEntry> MoleStatesSnapshot()
        {
            var states = new List<GameStateSnapshot.SnapshotMoleStateEntry>();
            foreach (KeyValuePair<ulong, OnlineRole> role in privateRoles)
            {
                if (role.Value != OnlineRole.Mole)
                    continue;

                bool hasHitTarget = _moleHitList.TryGetValue(role.Key, out ulong hitTargetClientId);
                _moleObjectives.TryGetValue(role.Key, out MoleObjective objective);
                states.Add(new GameStateSnapshot.SnapshotMoleStateEntry
                {
                    ClientId = role.Key,
                    Intel = GetMoleIntel(role.Key),
                    HasHitTarget = hasHitTarget,
                    HitTargetClientId = hitTargetClientId,
                    Exposed = IsMoleExposed(role.Key),
                    Kills = objective.Kills,
                    Sabotages = objective.Sabotages,
                    SurvivedTilLate = objective.SurvivedTilLate,
                });
            }
            return states;
        }

        internal void LoadIdentityStates(
            List<GameStateSnapshot.SnapshotUndercoverStateEntry> undercoverStates,
            List<GameStateSnapshot.SnapshotMoleStateEntry> moleStates)
        {
            ClearIdentityState();

            MergeIdentityStates(undercoverStates, moleStates);
        }

        internal void RestoreLocalIdentityIfMissing(
            ulong clientId,
            OnlineRole role,
            List<GameStateSnapshot.SnapshotUndercoverStateEntry> undercoverStates,
            List<GameStateSnapshot.SnapshotMoleStateEntry> moleStates)
        {
            if (role != OnlineRole.Undercover && role != OnlineRole.Mole)
                return;

            if (!privateRoles.ContainsKey(clientId))
                privateRoles[clientId] = role;

            MergeIdentityStates(undercoverStates, moleStates);
        }

        private void MergeIdentityStates(
            List<GameStateSnapshot.SnapshotUndercoverStateEntry> undercoverStates,
            List<GameStateSnapshot.SnapshotMoleStateEntry> moleStates)
        {

            if (undercoverStates != null)
            {
                foreach (GameStateSnapshot.SnapshotUndercoverStateEntry state in undercoverStates)
                {
                    if (!IsUndercover(state.ClientId))
                        continue;

                    _undercoverIntel[state.ClientId] = Mathf.Max(0, state.Intel);
                    _undercoverMissionsDone[state.ClientId] = Mathf.Max(0, state.MissionsDone);
                    if (state.Betrayed)
                        _undercoverBetrayed.Add(state.ClientId);
                }
            }

            if (moleStates == null)
                return;

            foreach (GameStateSnapshot.SnapshotMoleStateEntry state in moleStates)
            {
                if (!IsMole(state.ClientId))
                    continue;

                _moleIntel[state.ClientId] = Mathf.Max(0, state.Intel);
                if (state.HasHitTarget && players.ContainsKey(state.HitTargetClientId))
                    _moleHitList[state.ClientId] = state.HitTargetClientId;
                if (state.Exposed)
                    _moleExposed.Add(state.ClientId);
                _moleObjectives[state.ClientId] = new MoleObjective
                {
                    Kills = Mathf.Max(0, state.Kills),
                    Sabotages = Mathf.Max(0, state.Sabotages),
                    SurvivedTilLate = state.SurvivedTilLate,
                };
            }
        }

        // ============================================================
        //  清理
        // ============================================================

        public void ClearIdentityState()
        {
            _undercoverIntel.Clear();
            _undercoverMissionsDone.Clear();
            _undercoverBetrayed.Clear();
            _moleHitList.Clear();
            _moleExposed.Clear();
            _moleObjectives.Clear();
        }
    }
}
