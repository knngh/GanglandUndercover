using System;
using System.Collections.Generic;
using System.Linq;
using GanglandUndercover.Core;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.Gameplay
{
    /// <summary>
    /// 双向渗透模型 AI 决策引擎。
    ///
    /// 四角色策略：
    ///   Gang  — 去卧底高概率区域，会议投票可疑目标
    ///   Undercover — 去信息区收集证据，避免暴露
    ///   Police — 保护卧底，巡逻收集辅助证据
    ///   Mole  — 混在警察中，去卧底常出现的区域调查
    /// </summary>
    public sealed class OpponentAi
    {
        private readonly System.Random rng = new System.Random();

        private static readonly DistrictType[] HighRiskDistricts = { DistrictType.Dockyard, DistrictType.NightMarket };
        private static readonly DistrictType[] IntelDistricts = { DistrictType.PolicePrecinct, DistrictType.Clinic, DistrictType.WarehouseRow };

        /// <summary>
        /// 执行所有非玩家角色的 AI 回合行动。
        /// </summary>
        public void Run(GameState state)
        {
            SocialRole playerRole = state.PlayerRole;

            // 获取需要执行 AI 的角色列表（非玩家且未被淘汰）
            foreach (SocialRole role in Enum.GetValues(typeof(SocialRole)))
            {
                if (role == playerRole) continue;
                if (IsEliminated(state, role)) continue;

                RunRoleTurn(state, role);
            }
        }

        /// <summary>
        /// 会议投票：AI 角色根据策略和目标投票。
        /// 返回被投票淘汰的角色（SocialRole），null 表示弃权。
        /// </summary>
        public SocialRole? CastMeetingVote(GameState state)
        {
            List<(SocialRole voter, SocialRole target)> votes = new List<(SocialRole, SocialRole)>();

            SocialRole playerRole = state.PlayerRole;

            foreach (SocialRole role in Enum.GetValues(typeof(SocialRole)))
            {
                if (role == playerRole) continue;
                if (IsEliminated(state, role)) continue;

                SocialRole? vote = GetRoleVote(state, role);
                if (vote.HasValue)
                {
                    votes.Add((role, vote.Value));
                }
            }

            if (votes.Count == 0)
            {
                state.AddLog("AI 全部弃权，本轮无人被淘汰。");
                return null;
            }

            // 按得票数排序
            var grouped = votes.GroupBy(v => v.target)
                .OrderByDescending(g => g.Count())
                .ToList();

            int maxCount = grouped[0].Count();
            var topCandidates = grouped.Where(g => g.Count() == maxCount).Select(g => g.Key).ToList();

            SocialRole result = topCandidates[rng.Next(topCandidates.Count)];

            foreach (var (voter, target) in votes)
            {
                state.AddLog($"AI {RoleLabel(voter)} 投票：{RoleLabel(target)}");
            }

            state.AddLog($"AI 投票结果：{RoleLabel(result)} 被投出局（{maxCount}/{votes.Count} 票）。");
            return result;
        }

        // ──────────────── 各角色回合策略 ────────────────

        private void RunRoleTurn(GameState state, SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:
                    RunGangTurn(state);
                    break;
                case SocialRole.Undercover:
                    RunUndercoverTurn(state);
                    break;
                case SocialRole.Police:
                    RunPoliceTurn(state);
                    break;
                case SocialRole.Mole:
                    RunMoleTurn(state);
                    break;
            }
        }

        // ── Gang：去卧底高概率区域搜查，会议时投票可疑目标 ──

        private void RunGangTurn(GameState state)
        {
            DistrictState target;

            // 25% 概率去高风险区（卧底出没地）
            if (rng.NextDouble() < 0.35)
            {
                target = state.Districts
                    .Where(d => HighRiskDistricts.Contains(d.Type))
                    .OrderByDescending(d => d.PolicePresence - d.GangInfluence)
                    .First();
            }
            else
            {
                // 去警察存在感最低的区域
                target = state.Districts
                    .OrderBy(d => d.PolicePresence)
                    .ThenByDescending(_ => rng.NextDouble())
                    .First();
            }

            if (target.IsLockedDown)
            {
                target.SetLockdown(false);
                target.AddGangInfluence(1);
                target.AddPolicePresence(-1);
                state.AddPoliceHeat(1);
                state.AddMoleIntel(1);
                state.AddLog($"黑帮在 {DistrictDisplay(target.Type)} 贿赂突破封锁，线人获得新情报。");
                return;
            }

            // 在高风险区或目击者区域搜查可疑对象
            if (HighRiskDistricts.Contains(target.Type) && state.Suspicion >= 30)
            {
                state.AddMoleIntel(2);
                state.AddSuspicion(5);
                target.AddGangInfluence(1);
                state.AddLog($"黑帮在 {DistrictDisplay(target.Type)} 锁定可疑目标，线人情报 +2。");
                return;
            }

            if (target.HasWitness && target.CivilianTrust >= 5)
            {
                target.SetWitness(false);
                target.AddGangInfluence(1);
                target.AddCivilianTrust(-2);
                state.AddPublicTrust(-1);
                state.AddPoliceHeat(1);
                state.AddLog($"黑帮在 {DistrictDisplay(target.Type)} 施压目击者使其沉默。");
                return;
            }

            int influenceGain = rng.NextDouble() < 0.3 ? 2 : 1;
            target.AddGangInfluence(influenceGain);
            target.AddCivilianTrust(-1);
            state.AddLog($"黑帮在 {DistrictDisplay(target.Type)} 扩张地盘影响力 +{influenceGain}。");
        }

        // ── Undercover：去信息区收集证据，避免暴露 ──

        private void RunUndercoverTurn(GameState state)
        {
            DistrictState target;

            // 70% 概率去信息区收集证据
            if (rng.NextDouble() < 0.7)
            {
                target = state.Districts
                    .Where(d => IntelDistricts.Contains(d.Type))
                    .OrderByDescending(d => d.CivilianTrust)
                    .ThenBy(_ => rng.NextDouble())
                    .First();
            }
            else
            {
                // 偶尔去其他区域降低嫌疑
                target = state.Districts
                    .Where(d => !HighRiskDistricts.Contains(d.Type))
                    .OrderBy(_ => rng.NextDouble())
                    .FirstOrDefault()
                    ?? state.Districts.OrderBy(_ => rng.NextDouble()).First();
            }

            // 嫌疑高时优先维持掩护
            if (state.Suspicion >= 70)
            {
                state.AddCover(10);
                state.AddSuspicion(-12);
                target.AddGangInfluence(1);
                state.AddLog($"卧底在 {DistrictDisplay(target.Type)} 低调维持掩护，降低嫌疑。");
                return;
            }

            // 收集证据
            int evidenceGain = target.HasWitness ? 3 : 1;
            if (IntelDistricts.Contains(target.Type))
            {
                evidenceGain += 2;
            }

            state.AddUndercoverEvidence(evidenceGain);
            state.AddPoliceHeat(1);
            state.AddCover(-8);
            state.AddSuspicion(15);
            target.AddPolicePresence(1);

            string extra = "";
            if (target.HasWitness) extra += "（目击者线索）";
            if (IntelDistricts.Contains(target.Type)) extra += "（信息区 +2）";
            state.AddLog($"卧底从 {DistrictDisplay(target.Type)} 收集证据 +{evidenceGain}{extra}。");
        }

        // ── Police：保护卧底，巡逻收集辅助证据 ──

        private void RunPoliceTurn(GameState state)
        {
            DistrictState target = state.Districts
                .OrderByDescending(d => d.GangInfluence)
                .ThenBy(_ => rng.NextDouble())
                .First();

            // 黑帮热度高时封锁区域
            if (state.PoliceHeat >= 7 && !target.IsLockedDown)
            {
                target.SetLockdown(true);
                target.AddGangInfluence(-1);
                target.AddPolicePresence(1);
                state.AddPoliceHeat(1);
                state.AddPublicTrust(-1);
                state.AddLog($"警察封锁了 {DistrictDisplay(target.Type)}，保护卧底行动。");
                return;
            }

            // 在信息区巡逻，辅助收集证据
            if (IntelDistricts.Contains(target.Type))
            {
                int evidenceGain = target.HasWitness ? 2 : 1;
                state.AddUndercoverEvidence(evidenceGain);
                state.AddEvidence(evidenceGain);
                state.AddPoliceHeat(1);
                target.AddPolicePresence(1);
                state.AddLog($"警察在 {DistrictDisplay(target.Type)} 巡逻取证 +{evidenceGain}，配合卧底行动。");
                return;
            }

            // 突袭黑帮控制区
            if (target.GangInfluence >= 5)
            {
                target.AddGangInfluence(-2);
                target.AddPolicePresence(2);
                state.AddPoliceHeat(2);
                state.AddPublicTrust(-1);
                state.AddLog($"警察突袭 {DistrictDisplay(target.Type)}，削弱黑帮控制。");
                return;
            }

            target.AddPolicePresence(1);
            state.AddUndercoverEvidence(1);
            state.AddLog($"警察在 {DistrictDisplay(target.Type)} 巡逻，为卧底提供掩护。");
        }

        // ── Mole：混在警察中，去卧底常出现的区域调查 ──

        private void RunMoleTurn(GameState state)
        {
            DistrictState target;

            // 40% 概率去卧底可能出现的高风险区或信息区
            double roll = rng.NextDouble();
            if (roll < 0.25)
            {
                // 去高风险区排查卧底
                target = state.Districts
                    .Where(d => HighRiskDistricts.Contains(d.Type))
                    .OrderByDescending(d => d.PolicePresence)
                    .First();
            }
            else if (roll < 0.55)
            {
                // 去信息区（卧底常去收集证据的地方）排查
                target = state.Districts
                    .Where(d => IntelDistricts.Contains(d.Type))
                    .OrderByDescending(d => d.PolicePresence)
                    .First();
            }
            else
            {
                // 混在警察中随机巡逻
                target = state.Districts
                    .OrderBy(d => d.PolicePresence)
                    .ThenByDescending(_ => rng.NextDouble())
                    .First();
            }

            // 调查卧底出没区域，收集线人情报
            int intelGain = 1;

            if (HighRiskDistricts.Contains(target.Type) && target.PolicePresence >= 4)
            {
                // 高风险区 + 警方活跃 → 发现卧底痕迹
                intelGain = 2;
                state.AddMoleIntel(2);
                target.AddGangInfluence(1);
                state.AddLog($"线人在 {DistrictDisplay(target.Type)} 发现卧底活动迹象，情报 +2。");
            }
            else if (IntelDistricts.Contains(target.Type) && target.HasWitness)
            {
                intelGain = 2;
                state.AddMoleIntel(2);
                target.AddGangInfluence(1);
                target.AddCivilianTrust(-1);
                state.AddLog($"线人在 {DistrictDisplay(target.Type)} 利用目击者线索排查卧底，情报 +2。");
            }
            else if (target.IsLockedDown)
            {
                intelGain = 2;
                state.AddMoleIntel(2);
                target.AddPolicePresence(-1);
                state.AddLog($"线人在封锁的 {DistrictDisplay(target.Type)} 秘密调查，情报 +2。");
            }
            else
            {
                state.AddMoleIntel(1);
                target.AddPolicePresence(1);
                state.AddLog($"线人在 {DistrictDisplay(target.Type)} 混入警察巡逻，暗中收集情报 +1。");
            }
        }

        // ──────────────── 会议投票策略 ────────────────

        private SocialRole? GetRoleVote(GameState state, SocialRole voterRole)
        {
            switch (voterRole)
            {
                case SocialRole.Gang:
                    return GetGangVote(state);
                case SocialRole.Undercover:
                    return GetUndercoverVote(state);
                case SocialRole.Police:
                    return GetPoliceVote(state);
                case SocialRole.Mole:
                    return GetMoleVote(state);
                default:
                    return null;
            }
        }

        private SocialRole? GetGangVote(GameState state)
        {
            // 嫌疑高时优先投 Undercover
            if (!state.UndercoverEliminated && state.Suspicion >= 60)
            {
                state.AddLog("黑帮投票理由：嫌疑人嫌疑过高，必须清除。");
                return SocialRole.Undercover;
            }

            // 证据充足时投 Police
            if (!state.PoliceEliminated && state.UndercoverEvidence >= 7)
            {
                state.AddLog("黑帮投票理由：卧底证据过多，警察威胁最大。");
                return SocialRole.Police;
            }

            // 线人已收集足够情报 → 投卧底
            if (!state.UndercoverEliminated && state.MoleIntel >= 7)
            {
                state.AddLog("黑帮投票理由：线人情报充足，锁定卧底。");
                return SocialRole.Undercover;
            }

            // 随机投 Police 或 Undercover
            List<SocialRole> targets = new List<SocialRole>();
            if (!state.PoliceEliminated) targets.Add(SocialRole.Police);
            if (!state.UndercoverEliminated) targets.Add(SocialRole.Undercover);
            if (targets.Count == 0) return null;

            SocialRole vote = targets[rng.Next(targets.Count)];
            state.AddLog($"黑帮投票：{RoleLabel(vote)}（随机选择）。");
            return vote;
        }

        private SocialRole? GetUndercoverVote(GameState state)
        {
            // 嫌疑高时自保弃权
            if (state.Suspicion >= 70)
            {
                state.AddLog("卧底投票：暴露风险高，弃权自保。");
                return null;
            }

            // 安全范围内指认 Gang
            if (state.Suspicion < 40 && !state.GangEliminated)
            {
                state.AddLog("卧底投票理由：安全范围内，指认黑帮。");
                return SocialRole.Gang;
            }

            // 线人有情报时尝试反击
            if (state.MoleIntel >= 6 && !state.MoleEliminated)
            {
                state.AddLog("卧底投票理由：线人情报威胁大，尝试反制。");
                return SocialRole.Mole;
            }

            // 废除队友也投 Gang
            if (!state.GangEliminated && rng.NextDouble() < 0.7)
            {
                state.AddLog("卧底投票理由：指认黑帮。");
                return SocialRole.Gang;
            }

            state.AddLog("卧底投票：安全考量，弃权。");
            return null;
        }

        private SocialRole? GetPoliceVote(GameState state)
        {
            // 线人暴露时优先投 Mole
            if (state.MoleIntel >= 7 && !state.MoleEliminated)
            {
                state.AddLog("警察投票理由：发现线人嫌疑。");
                return SocialRole.Mole;
            }

            // 证据充足锁定 Gang
            if (!state.GangEliminated && state.UndercoverEvidence >= 6)
            {
                state.AddLog("警察投票理由：卧底证据充足，锁定黑帮。");
                return SocialRole.Gang;
            }

            // 标准证据判定
            if (!state.GangEliminated && state.Evidence >= 4 && rng.NextDouble() < 0.7)
            {
                state.AddLog("警察投票理由：证据指向黑帮。");
                return SocialRole.Gang;
            }

            if (rng.NextDouble() < 0.25 && !state.UndercoverEliminated)
            {
                state.AddLog("警察投票理由：证据不足，误判卧底身份。");
                return SocialRole.Undercover;
            }

            state.AddLog("警察弃权：证据不足无法判断。");
            return null;
        }

        private SocialRole? GetMoleVote(GameState state)
        {
            // 情报充足时锁定卧底
            if (!state.UndercoverEliminated && state.MoleIntel >= 7)
            {
                state.AddLog("线人投票理由：情报充足，指认卧底。");
                return SocialRole.Undercover;
            }

            // 嫌疑值高时配合 Gang 投 Undercover
            if (!state.UndercoverEliminated && state.Suspicion >= 50)
            {
                state.AddLog("线人投票理由：嫌疑值高，配合黑帮投卧底。");
                return SocialRole.Undercover;
            }

            // 混在警察中投 Gang 掩盖身份
            if (!state.GangEliminated && state.UndercoverEvidence < 4 && rng.NextDouble() < 0.6)
            {
                state.AddLog("线人投票理由：投黑帮掩盖身份。");
                return SocialRole.Gang;
            }

            // 投警察（伪装为警察的正常投票行为）
            if (!state.PoliceEliminated && rng.NextDouble() < 0.4)
            {
                state.AddLog("线人投票理由：伪装投警察。");
                return SocialRole.Police;
            }

            state.AddLog("线人投票：弃权保持低调。");
            return null;
        }

        // ──────────────── 辅助方法 ────────────────

        private static bool IsEliminated(GameState state, SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang: return state.GangEliminated;
                case SocialRole.Police: return state.PoliceEliminated;
                case SocialRole.Undercover: return state.UndercoverEliminated;
                case SocialRole.Mole: return state.MoleEliminated;
                default: return true;
            }
        }

        private static string RoleLabel(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang: return "黑帮";
                case SocialRole.Police: return "警察";
                case SocialRole.Undercover: return "卧底";
                case SocialRole.Mole: return "线人";
                default: return role.ToString();
            }
        }

        private static string DistrictDisplay(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.Dockyard: return "货柜码头";
                case DistrictType.WarehouseRow: return "证物库";
                case DistrictType.NightMarket: return "夜市巷";
                case DistrictType.PolicePrecinct: return "专案办公室";
                case DistrictType.Clinic: return "地下诊所";
                case DistrictType.TenementBlock: return "主街";
                default: return type.ToString();
            }
        }
    }
}