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

            // 4. 综合判定：任一触发即返回结果
            if (!string.IsNullOrEmpty(onlineResult))
            {
                return new EvaluateResult(true, onlineResult, offlineResult, EvaluateSource.NativeOnline);
            }

            if (offlineHasResult)
            {
                return new EvaluateResult(true, offlineResult, offlineResult, EvaluateSource.OfflineEvaluator);
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

            int completedCount = tasks.Count(t => t.Completed);
            if (evidenceScore >= Mathf.CeilToInt(evidenceTarget * 0.82f) ||
                completedCount >= Mathf.CeilToInt(tasks.Count * 0.72f))
            {
                result = "警方胜利：行动超时前已掌握关键证据。";
            }
            else
            {
                result = "黑帮胜利：20 分钟窗口结束，关键证据未能闭合。";
            }
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
            int aliveGang = 0, alivePolice = 0, aliveUndercover = 0, aliveMole = 0, totalAlive = 0;

            foreach (var kv in players)
            {
                if (!kv.Value.Alive) continue;
                totalAlive++;
                switch (getPrivateRole(kv.Key))
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
            int aliveNonGangSide = alivePolice + aliveUndercover + aliveMole;

            // 1) 证据链闭合
            if (evidenceScore >= evidenceTarget)
                return "警方胜利：证据链闭合。";

            // 2) 卧底特殊胜利：卧底是最后唯一存活者（优先于阵营全灭判定）
            if (aliveUndercover == 1 && totalAlive == 1)
                return "卧底胜利：港区暗线完美收网。";

            // 3) 黑帮全灭（Gang + Mole 均出局）
            if (gangSide == 0 && totalAlive >= 1)
                return "警方胜利：黑帮全部出局。";

            // 4) 警方全灭（Police + Undercover 均出局）
            if (policeSide == 0 && totalAlive >= 1)
                return "黑帮胜利：警方阵营全部出局。";

            // 5) 黑帮人数碾压。用 aliveGang（不含 Mole）做比较，
            //    平局时黑帮仍有优势（旧逻辑保持兼容）。
            if (aliveGang > 0 && (aliveNonGangSide == 0 || (totalAlive >= 4 && aliveGang >= aliveNonGangSide)))
                return "黑帮胜利：港区控制权失守。";

            // 6) 全部任务完成 + 证据过半
            int totalTasks = tasks.Count;
            int completedTasks = tasks.Count(t => t.Completed);
            int sabotagedTasks = tasks.Count(t => t.Sabotaged);

            if (totalTasks > 0 && completedTasks >= totalTasks && evidenceScore >= Mathf.CeilToInt(evidenceTarget * 0.5f))
                return "警方胜利：全部任务完成，证据链已足够收网。";

            // 7) 破坏过半 + 证据不足
            if (totalTasks > 0 && sabotagedTasks >= Mathf.CeilToInt(totalTasks * 0.5f) && evidenceScore < Mathf.CeilToInt(evidenceTarget * 0.3f))
                return "黑帮胜利：关键设施遭到严重破坏，警方无力回天。";

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
