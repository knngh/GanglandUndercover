using System.Collections.Generic;

namespace GanglandUndercover.Core
{
    public static class Localization
    {
        private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>
        {
            { "game.title", "黑街卧底" },
            { "role.choose", "选择你在港口区的身份" },
            { "role.Gang", "黑帮" },
            { "role.Police", "警察" },
            { "role.Undercover", "卧底" },
            { "phase.RoleSelect", "选择身份" },
            { "phase.PlayerTurn", "玩家回合" },
            { "phase.AiTurn", "对手回合" },
            { "phase.GameOver", "游戏结束" },
            { "button.restart", "重新开始" },
            { "button.language", "语言: 中文" },
            { "label.role", "身份" },
            { "label.day", "第几天" },
            { "label.evidence", "证据" },
            { "label.policeHeat", "警力热度" },
            { "label.shipment", "货运进度" },
            { "label.cover", "掩护" },
            { "label.suspicion", "怀疑值" },
            { "label.publicTrust", "民众信任" },
            { "label.gangDistricts", "黑帮区域" },
            { "label.policeDistricts", "警方区域" },
            { "label.contested", "争夺区域" },
            { "label.control", "控制方" },
            { "label.witness", "证人" },
            { "label.lockdown", "封锁" },
            { "short.gang", "黑" },
            { "short.police", "警" },
            { "district.Dockyard", "码头" },
            { "district.WarehouseRow", "仓库街" },
            { "district.NightMarket", "夜市" },
            { "district.PolicePrecinct", "警局" },
            { "district.Clinic", "诊所" },
            { "district.TenementBlock", "公寓楼" },
            { "action.gang_expand.label", "扩张地盘" },
            { "action.gang_expand.desc", "提升所选区域的黑帮控制。" },
            { "action.gang_ship.label", "转运货物" },
            { "action.gang_ship.desc", "推进大货运，但会引来警方注意。" },
            { "action.gang_silence.label", "威压证人" },
            { "action.gang_silence.desc", "移除证人并损害民众信任。" },
            { "action.gang_bribe.label", "买通卡点" },
            { "action.gang_bribe.desc", "解除封锁并削弱警力。" },
            { "action.police_investigate.label", "调查取证" },
            { "action.police_investigate.desc", "从所选区域收集证据。" },
            { "action.police_raid.label", "突击搜查" },
            { "action.police_raid.desc", "削弱黑帮影响，但会损害民众信任。" },
            { "action.police_protect.label", "保护证人" },
            { "action.police_protect.desc", "保护证人并提高民众信任。" },
            { "action.police_lockdown.label", "设置卡点" },
            { "action.police_lockdown.desc", "封锁区域，干扰黑帮转运。" },
            { "action.undercover_cover.label", "维持掩护" },
            { "action.undercover_cover.desc", "表现忠诚，降低怀疑并维持身份。" },
            { "action.undercover_intel.label", "传递情报" },
            { "action.undercover_intel.desc", "获得证据，但提高暴露风险。" },
            { "action.undercover_sabotage.label", "破坏货运" },
            { "action.undercover_sabotage.desc", "拖慢黑帮货运，但风险很高。" },
            { "action.undercover_dead_drop.label", "秘密投递" },
            { "action.undercover_dead_drop.desc", "暗中传递证据，不直接升高警力热度。" }
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "game.title", "Gangland Undercover" },
            { "role.choose", "Choose your role in the Port District" },
            { "role.Gang", "Gang" },
            { "role.Police", "Police" },
            { "role.Undercover", "Undercover" },
            { "phase.RoleSelect", "Role Select" },
            { "phase.PlayerTurn", "Player Turn" },
            { "phase.AiTurn", "AI Turn" },
            { "phase.GameOver", "Game Over" },
            { "button.restart", "Restart" },
            { "button.language", "Language: English" },
            { "label.role", "Role" },
            { "label.day", "Day" },
            { "label.evidence", "Evidence" },
            { "label.policeHeat", "Police Heat" },
            { "label.shipment", "Shipment" },
            { "label.cover", "Cover" },
            { "label.suspicion", "Suspicion" },
            { "label.publicTrust", "Public Trust" },
            { "label.gangDistricts", "Gang Districts" },
            { "label.policeDistricts", "Police Districts" },
            { "label.contested", "Contested" },
            { "label.control", "Control" },
            { "label.witness", "Witness" },
            { "label.lockdown", "Lockdown" },
            { "short.gang", "G" },
            { "short.police", "P" },
            { "district.Dockyard", "Dockyard" },
            { "district.WarehouseRow", "Warehouse Row" },
            { "district.NightMarket", "Night Market" },
            { "district.PolicePrecinct", "Police Precinct" },
            { "district.Clinic", "Clinic" },
            { "district.TenementBlock", "Tenement Block" },
            { "action.gang_expand.label", "Expand Turf" },
            { "action.gang_expand.desc", "Increase gang control in the selected district." },
            { "action.gang_ship.label", "Move Shipment" },
            { "action.gang_ship.desc", "Advance the major shipment, but increase police attention." },
            { "action.gang_silence.label", "Pressure Witness" },
            { "action.gang_silence.desc", "Remove a witness and damage public trust." },
            { "action.gang_bribe.label", "Bribe Checkpoint" },
            { "action.gang_bribe.desc", "Clear lockdown and weaken police presence." },
            { "action.police_investigate.label", "Investigate" },
            { "action.police_investigate.desc", "Collect evidence from the selected district." },
            { "action.police_raid.label", "Raid" },
            { "action.police_raid.desc", "Reduce gang influence, but risk public trust." },
            { "action.police_protect.label", "Protect Witness" },
            { "action.police_protect.desc", "Secure a witness and improve public trust." },
            { "action.police_lockdown.label", "Set Checkpoint" },
            { "action.police_lockdown.desc", "Lock down a district to disrupt gang movement." },
            { "action.undercover_cover.label", "Maintain Cover" },
            { "action.undercover_cover.desc", "Act loyal to reduce suspicion and preserve cover." },
            { "action.undercover_intel.label", "Pass Intel" },
            { "action.undercover_intel.desc", "Gain evidence while risking suspicion." },
            { "action.undercover_sabotage.label", "Sabotage Shipment" },
            { "action.undercover_sabotage.desc", "Slow the gang and risk exposure." },
            { "action.undercover_dead_drop.label", "Dead Drop" },
            { "action.undercover_dead_drop.desc", "Build evidence without open police heat." }
        };

        public static string Text(GameLanguage language, string key)
        {
            Dictionary<string, string> table = language == GameLanguage.Chinese ? Chinese : English;

            if (table.TryGetValue(key, out string value))
            {
                return value;
            }

            return key;
        }
    }
}
