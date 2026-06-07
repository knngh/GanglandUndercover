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
            return true;
        }

        /// <summary>检查卧底独立获胜（Gang 胜时卧底存活且已背叛 → 独赢）。</summary>
        public bool CheckUndercoverSoloWin(ulong undercoverId)
        {
            if (!IsUndercover(undercoverId)) return false;
            if (!HasBetrayed(undercoverId)) return false;
            return players.TryGetValue(undercoverId, out var s) && s.Alive;
        }

        public int GetUndercoverIntel(ulong undercoverId)
        {
            return _undercoverIntel.TryGetValue(undercoverId, out int v) ? v : 0;
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
            }
        }

        /// <summary>为 Mole 分配暗杀目标（Intel 满 5 时触发）。</summary>
        public ulong? AssignMoleHit(ulong moleId)
        {
            if (!IsMole(moleId)) return null;
            int intel = GetMoleIntel(moleId);
            if (intel < MoleIntelWinThreshold) return null;

            // 选择一个活着的警察作为目标
            foreach (var kv in players)
            {
                if (GetPrivateRole(kv.Key) == OnlineRole.Police && kv.Value.Alive && kv.Key != moleId)
                {
                    _moleHitList[moleId] = kv.Key;
                    return kv.Key;
                }
            }
            return null;
        }

        public ulong? GetMoleHitTarget(ulong moleId)
        {
            return _moleHitList.TryGetValue(moleId, out ulong t) ? t : null;
        }

        /// <summary>检查 Mole 独立获胜条件。</summary>
        public bool CheckMoleSoloWin(ulong moleId)
        {
            if (!IsMole(moleId)) return false;
            int intel = GetMoleIntel(moleId);
            if (intel < MoleIntelWinThreshold) return false;
            if (!players.TryGetValue(moleId, out var ms) || !ms.Alive) return false;

            // Mole 获胜: Intel ≥ 5 + 关键警察已被淘汰
            ulong? target = GetMoleHitTarget(moleId);
            if (target.HasValue && players.TryGetValue(target.Value, out var ts) && !ts.Alive)
                return true;

            return false;
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
        }
    }
}
