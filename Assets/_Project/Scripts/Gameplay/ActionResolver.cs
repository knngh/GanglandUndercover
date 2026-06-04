using System.Collections.Generic;
using GanglandUndercover.Core;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.Gameplay
{
    public sealed class ActionResolver
    {
        private readonly List<PlayerAction> actions = new List<PlayerAction>
        {
            new PlayerAction("gang_expand", "Expand Turf", Faction.Gang, "Increase gang control in the selected district."),
            new PlayerAction("gang_ship", "Move Shipment", Faction.Gang, "Advance the major shipment, but increase police attention."),
            new PlayerAction("gang_silence", "Pressure Witness", Faction.Gang, "Remove a witness and damage public trust."),
            new PlayerAction("gang_bribe", "Bribe Checkpoint", Faction.Gang, "Clear a lockdown and weaken police presence."),
            new PlayerAction("police_investigate", "Investigate", Faction.Police, "Collect evidence from the selected district."),
            new PlayerAction("police_raid", "Raid", Faction.Police, "Reduce gang influence, but risk public trust."),
            new PlayerAction("police_protect", "Protect Witness", Faction.Police, "Secure a witness and improve public trust."),
            new PlayerAction("police_lockdown", "Set Checkpoint", Faction.Police, "Lock down a district to disrupt gang movement."),
            new PlayerAction("undercover_cover", "Maintain Cover", Faction.Undercover, "Act loyal to reduce suspicion and preserve cover."),
            new PlayerAction("undercover_intel", "Pass Intel", Faction.Undercover, "Gain evidence while risking suspicion."),
            new PlayerAction("undercover_sabotage", "Sabotage Shipment", Faction.Undercover, "Slow the gang and risk exposure."),
            new PlayerAction("undercover_dead_drop", "Dead Drop", Faction.Undercover, "Use a district handoff to build evidence without open police heat."),
            // ── Mole 行动（黑帮线人，伪装为警察） ──
            new PlayerAction("mole_surveil", "Surveil Target", Faction.Gang, "Track suspicious police members to gather intel."),
            new PlayerAction("mole_infiltrate", "Infiltrate Records", Faction.Gang, "Access police records to identify undercover agents."),
            new PlayerAction("mole_tipoff", "Tip Off Gang", Faction.Gang, "Pass gathered intel to gang leadership."),
            new PlayerAction("mole_frame", "Frame Suspect", Faction.Gang, "Plant false evidence to misdirect police investigation.")
        };

        // 信息区：PolicePrecinct（警方枢纽）、Clinic（情报流通）、WarehouseRow（证物仓库）
        private static readonly DistrictType[] IntelDistricts = { DistrictType.PolicePrecinct, DistrictType.Clinic, DistrictType.WarehouseRow };

        // 高风险区：Dockyard（货柜码头）、NightMarket（夜市巷）
        private static readonly DistrictType[] HighRiskDistricts = { DistrictType.Dockyard, DistrictType.NightMarket };

        public IEnumerable<PlayerAction> GetActionsFor(Faction faction)
        {
            foreach (PlayerAction action in actions)
            {
                if (action.Faction == faction)
                {
                    yield return action;
                }
            }
        }

        /// <summary>
        /// 获取角色可用的行动列表。Mole 使用 Faction.Gang 的行动池 + 专属行动。
        /// </summary>
        public IEnumerable<PlayerAction> GetActionsForRole(SocialRole role)
        {
            Faction faction = SocialKnowledge.GetRealFaction(role);
            return GetActionsFor(faction);
        }

        /// <summary>
        /// 区域事件结算。Undercover 行动 → UndercoverEvidence，Mole 行动 → MoleIntel。
        /// </summary>
        public void Resolve(GameState state, DistrictState district, PlayerAction action)
        {
            string districtName = DistrictDisplayName(district.Type);

            switch (action.Id)
            {
                case "gang_expand":
                    ResolveGangExpand(state, district, districtName);
                    break;
                case "gang_ship":
                    ResolveGangShip(state, district, districtName);
                    break;
                case "gang_silence":
                    ResolveGangSilence(state, district, districtName);
                    break;
                case "gang_bribe":
                    ResolveGangBribe(state, district, districtName);
                    break;
                case "police_investigate":
                    ResolvePoliceInvestigate(state, district, districtName);
                    break;
                case "police_raid":
                    ResolvePoliceRaid(state, district, districtName);
                    break;
                case "police_protect":
                    ResolvePoliceProtect(state, district, districtName);
                    break;
                case "police_lockdown":
                    ResolvePoliceLockdown(state, district, districtName);
                    break;
                case "undercover_cover":
                    ResolveUndercoverCover(state, district, districtName);
                    break;
                case "undercover_intel":
                    ResolveUndercoverIntel(state, district, districtName);
                    break;
                case "undercover_sabotage":
                    ResolveUndercoverSabotage(state, district, districtName);
                    break;
                case "undercover_dead_drop":
                    ResolveUndercoverDeadDrop(state, district, districtName);
                    break;
                case "mole_surveil":
                    ResolveMoleSurveil(state, district, districtName);
                    break;
                case "mole_infiltrate":
                    ResolveMoleInfiltrate(state, district, districtName);
                    break;
                case "mole_tipoff":
                    ResolveMoleTipoff(state, district, districtName);
                    break;
                case "mole_frame":
                    ResolveMoleFrame(state, district, districtName);
                    break;
            }
        }

        // ──────── Gang 行动 ────────

        private void ResolveGangExpand(GameState state, DistrictState district, string districtName)
        {
            int gain = IsHighRisk(district.Type) ? 3 : 2;
            district.AddGangInfluence(gain);
            district.AddCivilianTrust(-1);
            state.AddPoliceHeat(IsHighRisk(district.Type) ? 2 : 1);
            state.AddSuspicion(-2);
            string riskNote = IsHighRisk(district.Type) ? "（高风险区额外扩张）" : "";
            state.AddLog("黑帮在 " + districtName + " 扩张地盘 +" + gain + riskNote + "。");
        }

        private void ResolveGangShip(GameState state, DistrictState district, string districtName)
        {
            if (district.IsLockedDown)
            {
                state.AddPoliceHeat(1);
                state.AddEvidence(1);
                district.AddPolicePresence(1);
                state.AddLog("黑帮货物在 " + districtName + " 被关卡拦截，警方获得线索。");
                return;
            }

            state.AddShipmentProgress(1);
            state.AddPoliceHeat(IsHighRisk(district.Type) ? 3 : 2);
            district.AddGangInfluence(1);
            district.AddPolicePresence(1);
            string riskNote = IsHighRisk(district.Type) ? "（高风险路线，警方高度关注）" : "";
            state.AddLog("黑帮通过 " + districtName + " 运输货物" + riskNote + "。");
        }

        private void ResolveGangSilence(GameState state, DistrictState district, string districtName)
        {
            district.SetWitness(false);
            district.AddGangInfluence(1);
            district.AddCivilianTrust(-2);
            state.AddPublicTrust(-1);
            state.AddPoliceHeat(1);
            state.AddLog("黑帮在 " + districtName + " 对目击者施压，证人被迫沉默。");
        }

        private void ResolveGangBribe(GameState state, DistrictState district, string districtName)
        {
            if (district.IsLockedDown)
            {
                district.SetLockdown(false);
                state.AddLog("黑帮贿赂 " + districtName + " 关卡人员，解除封锁。");
            }
            else
            {
                state.AddLog("黑帮在 " + districtName + " 收买线人布控。");
            }

            district.AddGangInfluence(1);
            district.AddPolicePresence(-1);
            district.AddCivilianTrust(-1);
            state.AddPoliceHeat(1);
            state.AddPublicTrust(-1);
        }

        // ──────── Police 行动 ────────

        private void ResolvePoliceInvestigate(GameState state, DistrictState district, string districtName)
        {
            int evidenceGain = district.HasWitness ? 3 : 1;

            if (IsIntel(district.Type))
            {
                evidenceGain += 1;
            }

            state.AddEvidence(evidenceGain);
            state.AddUndercoverEvidence(evidenceGain);
            state.AddPoliceHeat(1);
            district.AddPolicePresence(1);

            string extra = "";
            if (district.HasWitness) extra += "（目击者提供关键证词）";
            if (IsIntel(district.Type)) extra += "（信息区效率加成）";
            state.AddLog("警察在 " + districtName + " 调查取证 +" + evidenceGain + extra + "。");
        }

        private void ResolvePoliceRaid(GameState state, DistrictState district, string districtName)
        {
            district.AddGangInfluence(-2);
            district.AddPolicePresence(2);
            state.AddPoliceHeat(2);
            state.AddPublicTrust(-1);
            state.AddLog("警察突袭 " + districtName + "，黑帮影响力受挫，但舆情下降。");
        }

        private void ResolvePoliceProtect(GameState state, DistrictState district, string districtName)
        {
            district.SetWitness(true);
            district.AddPolicePresence(1);
            district.AddCivilianTrust(IsIntel(district.Type) ? 2 : 1);
            state.AddPoliceHeat(1);
            state.AddPublicTrust(1);
            string note = IsIntel(district.Type) ? "（信息区居民更配合）" : "";
            state.AddLog("警察在 " + districtName + " 保护证人" + note + "。");
        }

        private void ResolvePoliceLockdown(GameState state, DistrictState district, string districtName)
        {
            district.SetLockdown(true);
            district.AddGangInfluence(-1);
            district.AddPolicePresence(1);
            state.AddPoliceHeat(1);
            state.AddPublicTrust(-1);
            state.AddLog("警察在 " + districtName + " 设置关卡封锁。");
        }

        // ──────── Undercover 行动 ────────

        private void ResolveUndercoverCover(GameState state, DistrictState district, string districtName)
        {
            int coverGain = IsHighRisk(district.Type) ? 12 : 10;
            state.AddCover(coverGain);
            state.AddSuspicion(-12);
            district.AddGangInfluence(1);
            string note = IsHighRisk(district.Type) ? "（高风险区表现忠诚效果更好）" : "";
            state.AddLog("卧底在 " + districtName + " 维持掩护 +" + coverGain + note + "。");
        }

        private void ResolveUndercoverIntel(GameState state, DistrictState district, string districtName)
        {
            int evidenceGain = district.HasWitness ? 3 : 1;

            if (IsIntel(district.Type))
            {
                evidenceGain += 2;
            }

            state.AddEvidence(evidenceGain);
            state.AddUndercoverEvidence(evidenceGain);
            state.AddPoliceHeat(1);
            state.AddCover(-8);
            state.AddSuspicion(15);
            district.AddPolicePresence(1);

            string extra = "";
            if (district.HasWitness) extra += "（目击者提供关键线索）";
            if (IsIntel(district.Type)) extra += "（信息区情报密度高 +2）";
            state.AddLog("卧底从 " + districtName + " 传递情报 +" + evidenceGain + extra + "。");
        }

        private void ResolveUndercoverSabotage(GameState state, DistrictState district, string districtName)
        {
            int sabotageEffect = IsHighRisk(district.Type) ? 2 : 1;
            state.AddShipmentProgress(-sabotageEffect);
            state.AddCover(-12);
            state.AddSuspicion(IsHighRisk(district.Type) ? 25 : 20);
            state.AddPoliceHeat(1);
            district.AddGangInfluence(-1);
            string riskNote = IsHighRisk(district.Type) ? "（高风险区破坏严重但极易暴露）" : "";
            state.AddLog("卧底破坏 " + districtName + " 的帮派物流 -" + sabotageEffect + riskNote + "。");
        }

        private void ResolveUndercoverDeadDrop(GameState state, DistrictState district, string districtName)
        {
            int evidenceGain = district.HasWitness ? 3 : 1;

            if (IsIntel(district.Type))
            {
                evidenceGain += 1;
            }

            state.AddEvidence(evidenceGain);
            state.AddUndercoverEvidence(evidenceGain);
            state.AddCover(-5);
            state.AddSuspicion(district.IsLockedDown ? 4 : IsHighRisk(district.Type) ? 12 : 8);
            district.AddCivilianTrust(1);

            string note = "";
            if (district.IsLockedDown) note += "（封锁区内暗桩传递，嫌疑变动小）";
            else if (IsIntel(district.Type)) note += "（信息区暗桩网络高效 +1）";
            else if (IsHighRisk(district.Type)) note += "（高风险区传递风险增大）";
            state.AddLog("卧底在 " + districtName + " 完成暗桩传递 +" + evidenceGain + note + "。");
        }

        // ──────── Mole 行动（黑帮线人） ────────

        private void ResolveMoleSurveil(GameState state, DistrictState district, string districtName)
        {
            int intelGain = IsIntel(district.Type) ? 2 : 1;
            state.AddMoleIntel(intelGain);
            state.AddCover(5);
            district.AddPolicePresence(1);

            string note = IsIntel(district.Type) ? "（信息区跟踪效率更高）" : "";
            state.AddLog("线人在 " + districtName + " 跟踪可疑警察目标，情报 +" + intelGain + note + "。");
        }

        private void ResolveMoleInfiltrate(GameState state, DistrictState district, string districtName)
        {
            int intelGain = IsIntel(district.Type) ? 3 : 1;
            state.AddMoleIntel(intelGain);
            state.AddCover(-5);
            state.AddSuspicion(5);
            district.AddPolicePresence(-1);

            string note = IsIntel(district.Type) ? "（警方档案室情报密度高）" : "";
            state.AddLog("线人潜入 " + districtName + " 警方档案室，情报 +" + intelGain + note + "。");
        }

        private void ResolveMoleTipoff(GameState state, DistrictState district, string districtName)
        {
            state.AddMoleIntel(2);
            state.AddCover(3);
            state.AddSuspicion(8);
            district.AddGangInfluence(1);

            bool risky = IsHighRisk(district.Type);
            string note = risky ? "（高风险区接头风险增大）" : "";
            state.AddLog("线人在 " + districtName + " 向黑帮传递卧底情报" + note + "。");
        }

        private void ResolveMoleFrame(GameState state, DistrictState district, string districtName)
        {
            int intelGain = IsHighRisk(district.Type) ? 2 : 1;
            state.AddMoleIntel(intelGain);
            state.AddCover(-10);
            state.AddSuspicion(10);
            district.AddCivilianTrust(-1);
            state.AddPublicTrust(-1);

            state.AddLog("线人在 " + districtName + " 伪造证据误导警方，情报 +" + intelGain + "。");
        }

        // ──────── 区域分类辅助 ────────

        private static bool IsIntel(DistrictType type)
        {
            foreach (DistrictType dt in IntelDistricts)
            {
                if (dt == type) return true;
            }

            return false;
        }

        private static bool IsHighRisk(DistrictType type)
        {
            foreach (DistrictType dt in HighRiskDistricts)
            {
                if (dt == type) return true;
            }

            return false;
        }

        private static string DistrictDisplayName(DistrictType type)
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