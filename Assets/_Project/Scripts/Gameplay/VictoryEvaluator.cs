using GanglandUndercover.Core;

namespace GanglandUndercover.Gameplay
{
    /// <summary>
    /// 双向渗透模型胜利条件判定。
    ///
    /// 警察阵营胜利条件（任一满足）：
    ///   1. 卧底证据充足（UndercoverEvidence >= 目标值）且卧底存活
    ///   2. 黑帮阵营全部被会议淘汰（Gang + Mole 均被投票出局）
    ///
    /// 黑帮阵营胜利条件（任一满足）：
    ///   1. 线人情报充足（MoleIntel >= 目标值）— 已识别出卧底
    ///   2. 卧底被消灭（Undercover 被淘汰）
    ///   3. 警察阵营全部被会议淘汰（Police + Undercover 均被投票出局）
    ///
    /// 僵局条件：
    ///   天数超过最大值（Day > MaxDays）且双方均未达成胜利条件
    /// </summary>
    public sealed class VictoryEvaluator
    {
        public bool TryEvaluate(GameState state, out string result)
        {
            result = string.Empty;

            // ── 全淘汰胜利 ──
            if (EvaluateTotalElimination(state, out result))
            {
                return true;
            }

            // ── 警察阵营胜利条件 ──
            if (EvaluatePoliceVictory(state, out result))
            {
                return true;
            }

            // ── 黑帮阵营胜利条件 ──
            if (EvaluateGangVictory(state, out result))
            {
                return true;
            }

            // ── 僵局 ──
            if (state.Day > GameState.MaxDays)
            {
                result = "僵局：港区陷入长期对峙，双方情报均不足以打破平衡，渗透任务双双失败。";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 全淘汰胜利：一方阵营所有角色被会议投票淘汰。
        /// </summary>
        private static bool EvaluateTotalElimination(GameState state, out string result)
        {
            result = string.Empty;

            bool gangSideWiped = state.GangEliminated && state.MoleEliminated;
            bool policeSideWiped = state.PoliceEliminated && state.UndercoverEliminated;

            if (gangSideWiped && policeSideWiped)
            {
                result = "双双覆灭：黑帮和警方都在会议中互相淘汰，港区陷入无主之地。";
                return true;
            }

            if (gangSideWiped)
            {
                result = "警察胜利：黑帮成员和线人均被会议清除，港区收网成功。";
                return true;
            }

            if (policeSideWiped)
            {
                result = "黑帮胜利：警察成员和卧底均被会议清除，港区彻底落入黑帮掌控。";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 警察阵营（Police + Undercover）胜利条件。
        /// </summary>
        private static bool EvaluatePoliceVictory(GameState state, out string result)
        {
            result = string.Empty;

            // 条件 1：卧底收集了足够的证据且存活
            if (state.UndercoverEvidence >= GameState.UndercoverEvidenceTarget
                && !state.UndercoverEliminated)
            {
                result = "警察胜利：卧底成功收集到足够证据（"
                    + state.UndercoverEvidence + "/" + GameState.UndercoverEvidenceTarget
                    + "），身份未暴露，收网行动即将展开。";
                return true;
            }

            // 条件 2：证据接近目标 + 警方热度充足（替代路径）
            if (state.UndercoverEvidence >= GameState.UndercoverEvidenceTarget - 2
                && state.PoliceHeat >= 8
                && !state.UndercoverEliminated)
            {
                result = "警察胜利：警方高度警觉配合卧底情报，提前发起收网。";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 黑帮阵营（Gang + Mole）胜利条件。
        /// </summary>
        private static bool EvaluateGangVictory(GameState state, out string result)
        {
            result = string.Empty;

            // 条件 1：线人已识别出卧底身份
            if (state.MoleIntel >= GameState.MoleIntelTarget)
            {
                result = "黑帮胜利：线人已掌握卧底的确凿证据（"
                    + state.MoleIntel + "/" + GameState.MoleIntelTarget
                    + "），卧底身份暴露，渗透任务失败。";
                return true;
            }

            // 条件 2：卧底已被消灭
            if (state.UndercoverEliminated)
            {
                result = "黑帮胜利：卧底已被清除，警方渗透计划彻底破产。";
                return true;
            }

            // 条件 3：卧底掩护完全崩溃
            if (state.Cover <= 0)
            {
                result = "黑帮胜利：卧底的掩护身份彻底崩溃，黑帮已锁定目标。";
                return true;
            }

            // 条件 4：嫌疑值爆表
            if (state.Suspicion >= 100)
            {
                result = "黑帮胜利：卧底嫌疑值爆表，黑帮已确定其身份。";
                return true;
            }

            return false;
        }
    }
}