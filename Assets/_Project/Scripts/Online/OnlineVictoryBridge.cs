using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// VictoryEvaluator 联机适配器：将 OnlineMatchController 的状态映射到
    /// GameState 概念，使离线模式的 VictoryEvaluator 能在联机模式下复用。
    ///
    /// 映射关系：
    ///   evidenceScore          → GameState.Evidence
    ///   suspicion (local)      → GameState.Suspicion
    ///   task sabotage ratio    → GameState.Cover（类比）
    ///   task completion ratio  → GameState.PoliceHeat（类比）
    ///   存活阵营比              → GameState.GangControlledDistricts（类比）
    ///   meeting eliminated     → GameState.VotedOut / EliminateFaction
    /// </summary>
    public sealed class OnlineVictoryBridge
    {
        private readonly VictoryEvaluator evaluator = new VictoryEvaluator();
        private readonly GameState gameState = new GameState();

        // 映射状态缓存
        private string lastOnlineResult = string.Empty;
        private string lastOfflineResult = string.Empty;
        private Faction lastOfflineFaction = Faction.Police;

        public string LastOnlineResult => lastOnlineResult;
        public string LastOfflineResult => lastOfflineResult;
        public GameState State => gameState;

        // ------ 状态同步 ------

        /// <summary>
        /// 从 OnlineMatchController 的实时状态映射到 GameState，然后运行双重判定。
        /// </summary>
        public EvaluateResult Evaluate(
            int evidenceScore,
            int evidenceTarget,
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            Func<ulong, OnlineRole> getPrivateRole,
            IReadOnlyList<OnlineTaskState> tasks,
            bool matchStarted,
            OnlineMatchPhase phase,
            OnlineRole localRole = OnlineRole.Police)
        {
            if (!matchStarted || phase == OnlineMatchPhase.Result)
            {
                return EvaluateResult.NoChange;
            }

            // 1. 映射到 GameState
            MapToGameState(evidenceScore, evidenceTarget, players, getPrivateRole, tasks, localRole);

            // 2. 离线 VictoryEvaluator 判定
            bool offlineHasResult = evaluator.TryEvaluate(gameState, out string offlineResult);
            lastOfflineResult = offlineResult;
            lastOfflineFaction = gameState.PlayerFaction;

            // 3. 在线原生判定
            string onlineResult = EvaluateNativeOnline(evidenceScore, evidenceTarget, players, getPrivateRole, tasks);
            lastOnlineResult = onlineResult;

            // 4. 联机对局只采用权威联机规则；离线映射仅保留为诊断数据。
            if (!string.IsNullOrEmpty(onlineResult))
            {
                return new EvaluateResult(true, onlineResult, offlineResult, EvaluateSource.NativeOnline);
            }

            return EvaluateResult.NoChange;
        }

        /// <summary>
        /// 检查联机特有胜利条件（超时判定）。
        /// </summary>
        public bool TryTimeLimitEvaluation(
            float matchElapsedSeconds,
            float timeLimitSeconds,
            int evidenceScore,
            int evidenceTarget,
            IReadOnlyList<OnlineTaskState> tasks,
            out string result)
        {
            result = string.Empty;
            if (matchElapsedSeconds < timeLimitSeconds) return false;

            result = "平局：行动窗口结束，双方均未完成决定性目标。";
            return true;
        }

        // ------ 映射引擎 ------

        private void MapToGameState(
            int evidenceScore,
            int evidenceTarget,
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            Func<ulong, OnlineRole> getPrivateRole,
            IReadOnlyList<OnlineTaskState> tasks,
            OnlineRole localRole)
        {
            // 基础值
            gameState.AddEvidence(-gameState.Evidence); // 归零
            gameState.AddEvidence(Clamp(evidenceScore, 0, 10));

            // 存活阵营映射
            int aliveGang = players.Count(kv => kv.Value.Alive && getPrivateRole(kv.Key) == OnlineRole.Gang);
            int alivePolice = players.Count(kv => kv.Value.Alive && getPrivateRole(kv.Key) == OnlineRole.Police);
            int aliveUndercover = players.Count(kv => kv.Value.Alive && getPrivateRole(kv.Key) == OnlineRole.Undercover);
            int aliveMole = players.Count(kv => kv.Value.Alive && getPrivateRole(kv.Key) == OnlineRole.Mole);
            int totalPlayers = players.Count;

            // Cover: 反映破坏比率（任务完整度类比掩护）
            int sabotageCount = tasks.Count(t => t.Sabotaged);
            int completedCount = tasks.Count(t => t.Completed);
            int totalTasks = tasks.Count;
            int coverValue = totalTasks > 0
                ? Mathf.RoundToInt(70f * (1f - (float)sabotageCount / totalTasks))
                : 70;
            gameState.AddCover(-gameState.Cover);
            gameState.AddCover(Clamp(coverValue, 0, 100));

            // Suspicion: 取本地玩家嫌疑值
            int suspicionValue = 15;
            foreach (var kv in players)
            {
                if (getPrivateRole(kv.Key) == OnlineRole.Undercover && kv.Value.Suspicion > suspicionValue)
                {
                    suspicionValue = kv.Value.Suspicion;
                }
            }
            gameState.AddSuspicion(-gameState.Suspicion);
            gameState.AddSuspicion(Clamp(suspicionValue, 0, 100));

            // PoliceHeat: 类比于已收集证据 + 完成任务进度
            int heatValue = totalTasks > 0
                ? Mathf.RoundToInt(5f * (float)completedCount / totalTasks + 3f * (float)evidenceScore / Mathf.Max(evidenceTarget, 1))
                : 2;
            gameState.AddPoliceHeat(-gameState.PoliceHeat);
            gameState.AddPoliceHeat(Clamp(heatValue, 0, 10));

            // GangControlledDistricts: 类比黑帮存活率（黑帮侧 = Gang + Mole，警方侧 = Police + Undercover）
            int gangDistrictEquivalent = totalPlayers > 0
                ? Mathf.RoundToInt((float)(aliveGang + aliveMole) / totalPlayers * 6f)
                : 0;
            // 直接更新 district state（模拟）
            foreach (DistrictType type in Enum.GetValues(typeof(DistrictType)))
            {
                gameState.GetDistrict(type).SetControl(type switch
                {
                    DistrictType.Dockyard => (aliveGang + aliveMole) > (alivePolice + aliveUndercover) ? Faction.Gang : Faction.Police,
                    DistrictType.WarehouseRow => sabotageCount > completedCount ? Faction.Gang : Faction.Police,
                    DistrictType.NightMarket => Faction.Gang,
                    DistrictType.PolicePrecinct => Faction.Police,
                    DistrictType.Clinic => aliveUndercover > 0 ? Faction.Police : Faction.Police,
                    DistrictType.TenementBlock => (aliveGang + aliveMole) >= (alivePolice + aliveUndercover) ? Faction.Gang : Faction.Police,
                    _ => Faction.Police
                });
            }

            // Map local role to faction
            gameState.SelectFaction(MapRole(localRole));

            // ShipmentProgress: 0 unless evidence is very low and gang dominates
            int shipmentVal = (evidenceScore <= 3 && (aliveGang + aliveMole) >= (alivePolice + aliveUndercover)) ? 2 : 0;
            gameState.AddShipmentProgress(-gameState.ShipmentProgress);
            gameState.AddShipmentProgress(shipmentVal);
        }

        // ------ 在线原生判定 ------

        private static string EvaluateNativeOnline(
            int evidenceScore,
            int evidenceTarget,
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            Func<ulong, OnlineRole> getPrivateRole,
            IReadOnlyList<OnlineTaskState> tasks)
        {
            int aliveGang = 0, alivePolice = 0, aliveUndercover = 0, aliveMole = 0, totalAlive = 0, totalUndercover = 0;

            foreach (var kv in players)
            {
                OnlineRole role = getPrivateRole(kv.Key);
                if (role == OnlineRole.Undercover) totalUndercover++;
                if (!kv.Value.Alive) continue;
                totalAlive++;
                switch (role)
                {
                    case OnlineRole.Gang:       aliveGang++; break;
                    case OnlineRole.Police:     alivePolice++; break;
                    case OnlineRole.Undercover: aliveUndercover++; break;
                    case OnlineRole.Mole:       aliveMole++; break;
                }
            }

            // 阵营归属：黑帮侧 = Gang + Mole，警方侧 = Police + Undercover
            int gangSide = aliveGang + aliveMole;
            int policeSide = alivePolice + aliveUndercover;

            // 卧底是警方收网的必要条件；卧底出局立即触发黑帮胜利。
            if (totalUndercover > 0 && aliveUndercover == 0)
                return "黑帮胜利：卧底已被拔除。";

            if (evidenceScore >= evidenceTarget)
                return "警方胜利：卧底存活，证据链闭合，收网成功。";

            if (gangSide == 0 && totalAlive >= 1)
                return "警方胜利：黑帮全部出局。";

            if (policeSide == 0 && totalAlive >= 1)
                return "黑帮胜利：警方阵营全部出局。";

            return string.Empty;
        }

        // ------ Public API for OnlineMatchController integration ------

        /// <summary>
        /// 注册会议淘汰：将联机投票淘汰结果记录到 GameState。
        /// </summary>
        public void RegisterMeetingElimination(ulong ejectedClientId, Func<ulong, OnlineRole> getPrivateRole)
        {
            if (ejectedClientId == MeetingSync.SkipVoteTarget) return;

            OnlineRole role = getPrivateRole(ejectedClientId);
            Faction faction = MapRole(role);
            gameState.EliminateFaction(faction);
        }

        /// <summary>
        /// 清除会议淘汰记录（新一局开始时调用）。
        /// </summary>
        public void ClearEliminations()
        {
            gameState.ClearEliminations();
            lastOnlineResult = string.Empty;
            lastOfflineResult = string.Empty;
        }

        // ------ Helpers ------

        private static Faction MapRole(OnlineRole role) => role switch
        {
            OnlineRole.Gang => Faction.Gang,
            OnlineRole.Mole => Faction.Gang,       // 内鬼属于黑帮侧
            OnlineRole.Police => Faction.Police,
            OnlineRole.Undercover => Faction.Police, // 卧底属于警方侧
            _ => Faction.Police
        };

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// 胜负判定结果。
    /// </summary>
    public readonly struct EvaluateResult
    {
        public static readonly EvaluateResult NoChange = new EvaluateResult(false, string.Empty, string.Empty, EvaluateSource.NativeOnline);

        public bool HasResult { get; }
        public string ResultText { get; }
        public string OfflineResult { get; }
        public EvaluateSource Source { get; }

        public EvaluateResult(bool hasResult, string resultText, string offlineResult, EvaluateSource source)
        {
            HasResult = hasResult;
            ResultText = resultText;
            OfflineResult = offlineResult;
            Source = source;
        }
    }

    public enum EvaluateSource
    {
        NativeOnline,
        OfflineEvaluator
    }
}
