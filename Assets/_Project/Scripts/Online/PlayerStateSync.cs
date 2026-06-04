using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机玩家状态同步：存活/死亡/位置/角色在所有客户端间同步。
    /// 通过对 OnlineMatchController 的 players 字典添加语义层事件通知。
    ///
    /// 核心同步管道由 BroadcastSnapshot（12.5Hz）承载，
    /// 本类负责状态变更检测、通知回调、数据一致性校验。
    /// </summary>
    public sealed class PlayerStateSync
    {
        public const float RespawnDelaySeconds = 3f;
        public const float PositionDriftThreshold = 0.05f;

        private readonly Action<string> addCaseLog;
        private readonly Dictionary<ulong, OnlinePlayerState> previousStates = new Dictionary<ulong, OnlinePlayerState>();
        private readonly Dictionary<ulong, ulong> killRecords = new Dictionary<ulong, ulong>(); // victim → killer
        private int totalDeaths;

        public event Action<ulong, OnlinePlayerState> PlayerSpawned;       // 新玩家加入/重生
        public event Action<ulong, OnlinePlayerState> PlayerAliveChanged;  // 存活状态变更
        public event Action<ulong, OnlineRole, OnlineRole> PlayerRoleChanged; // (playerId, oldRole, newRole)
        public event Action<ulong, Vector3, Vector3> PlayerPositionMoved;   // 位置显著变动

        public int TotalDeaths => totalDeaths;

        public PlayerStateSync(Action<string> addCaseLog)
        {
            this.addCaseLog = addCaseLog ?? (_ => { });
        }

        // ------ 帧同步检测 (Host 每帧调用) ------

        /// <summary>
        /// 在 BroadcastSnapshot 之前或之后调用，检测状态变更并触发事件。
        /// </summary>
        public void DetectChanges(IReadOnlyDictionary<ulong, OnlinePlayerState> currentStates)
        {
            HashSet<ulong> currentIds = new HashSet<ulong>(currentStates.Keys);

            // 新玩家
            foreach (var kv in currentStates)
            {
                if (!previousStates.TryGetValue(kv.Key, out OnlinePlayerState prev))
                {
                    previousStates[kv.Key] = CloneState(kv.Value);
                    PlayerSpawned?.Invoke(kv.Key, kv.Value);
                    addCaseLog($"PlayerStateSync: 新玩家 {kv.Value.DisplayName} ({kv.Key}) 加入。");
                    continue;
                }

                OnlinePlayerState cur = kv.Value;

                // 存活变更
                if (prev.Alive != cur.Alive)
                {
                    previousStates[kv.Key] = CloneState(cur);
                    PlayerAliveChanged?.Invoke(kv.Key, cur);
                    if (!cur.Alive) totalDeaths++;
                    addCaseLog($"PlayerStateSync: {cur.DisplayName} {(cur.Alive ? "复活" : "死亡")}。");
                    continue;
                }

                // 角色变更
                if (prev.PublicRole != cur.PublicRole)
                {
                    PlayerRoleChanged?.Invoke(kv.Key, prev.PublicRole, cur.PublicRole);
                    addCaseLog($"PlayerStateSync: {cur.DisplayName} 公开角色 {prev.PublicRole} → {cur.PublicRole}。");
                }

                // 位置显著变动
                if (Vector3.Distance(prev.Position, cur.Position) > PositionDriftThreshold)
                {
                    PlayerPositionMoved?.Invoke(kv.Key, prev.Position, cur.Position);
                }

                previousStates[kv.Key] = CloneState(cur);
            }

            // 离线玩家
            foreach (ulong oldId in previousStates.Keys.ToList())
            {
                if (!currentIds.Contains(oldId))
                {
                    previousStates.Remove(oldId);
                }
            }
        }

        // ------ 击杀记录 ------

        public void RecordKill(ulong victimId, ulong killerId)
        {
            killRecords[victimId] = killerId;
            addCaseLog($"PlayerStateSync: {killerId} 击倒 {victimId}。");
        }

        public bool TryGetKiller(ulong victimId, out ulong killerId)
        {
            return killRecords.TryGetValue(victimId, out killerId);
        }

        // ------ 统计 ------

        public int AliveCount(IReadOnlyDictionary<ulong, OnlinePlayerState> states)
        {
            return states.Values.Count(s => s.Alive);
        }

        public int AliveGangCount(IReadOnlyDictionary<ulong, OnlinePlayerState> states, Func<ulong, OnlineRole> getRole)
        {
            return states.Count(kv => kv.Value.Alive && getRole(kv.Key) == OnlineRole.Gang);
        }

        public int AliveNonGangCount(IReadOnlyDictionary<ulong, OnlinePlayerState> states, Func<ulong, OnlineRole> getRole)
        {
            return states.Count(kv => kv.Value.Alive && getRole(kv.Key) != OnlineRole.Gang);
        }

        // ------ Helpers ------

        private static OnlinePlayerState CloneState(OnlinePlayerState source)
        {
            return new OnlinePlayerState(
                source.ClientId,
                source.DisplayName,
                source.Position,
                source.Ready,
                source.Alive,
                source.PublicRole,
                source.Profession,
                source.Suspicion,
                source.IsBot
            );
        }
    }
}
