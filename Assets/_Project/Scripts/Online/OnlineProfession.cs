using System;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// M8.2 职业定义：具体职业类型，比 OnlineRole 更细粒度。
    /// 每个职业有专属能力（能力由 OnlineRuleSet 的职业表配置）。
    ///
    /// 整数枚举值保持兼容（与 M4 旧 OnlineProfession 一致，已从 OnlineMatchController 迁移至此）。
    /// </summary>
    public enum OnlineProfession
    {
        Inspector       = 0,  // 警探：报告冷却缩短，可查看足迹
        Forensics       = 1,  // 法医：检验尸体获得额外线索
        Tech            = 2,  // 技术员：远程监控，证据链加速
        UndercoverAgent = 3,  // 卧底：公开为黑帮，可潜伏破坏并在达标后背叛
        Enforcer        = 4,  // 打手：击杀冷却缩短
        Fixer           = 5,  // 清道夫：可拖动尸体，破坏冷却缩短
        Driver          = 6,  // 车手：通风管加速，移动速度微增
        Mole            = 7,  // 内鬼（M8.2 新增）：公开为警察，可内部破坏
    }

    /// <summary>
    /// M8.2：职业能力定义。
    /// 每个职业可以拥有 0-N 个能力，能力效果由 OnlineRuleSet 中的数值驱动。
    /// </summary>
    [Serializable]
    public struct ProfessionAbility
    {
        [Tooltip("能力类型")]
        public AbilityType Type;

        [Tooltip("数值倍率（相对于基础值），1.0 = 不变")]
        public float Multiplier;

        [Tooltip("附加值（秒/%/次数等，由类型决定）")]
        public float BonusValue;

        [Tooltip("是否启用此能力")]
        public bool Enabled;
    }

    /// <summary>
    /// M8.2：职业能力类型枚举。
    /// </summary>
    public enum AbilityType
    {
        // ── 击杀相关 ──
        KillCooldownReduce,    // 击杀冷却缩减（倍率）
        KillRangeBonus,        // 击杀范围加成（世界单位）

        // ── 报告相关 ──
        ReportCooldownReduce,  // 报告冷却缩减（倍率）
        ReportRangeBonus,      // 报告范围加成
        CorpseExamine,         // 检验尸体获得额外线索（Boolean，BonusValue=线索数）

        // ── 任务相关 ──
        TaskSpeedBonus,        // 任务完成速度加成（倍率）
        EvidenceChainBonus,    // 证据链加速（倍率）

        // ── 监控相关 ──
        RemoteSurveillance,    // 可远程查看监控（Boolean）

        // ── 破坏相关 ──
        SabotageCooldownReduce,// 破坏冷却缩减（倍率）
        SabotageRangeBonus,    // 破坏范围加成

        // ── 移动相关 ──
        MoveSpeedBonus,        // 移动速度加成（倍率）
        VentSpeedBonus,        // 通风管速度加成（倍率）
        VentCooldownReduce,    // 通风管冷却缩减（倍率）

        // ── 特殊 ──
        DarkVision,            // 黑灯时视野不衰减（Boolean）
        BodyDrag,              // 可拖动尸体（Boolean）
        SecretVote,            // 保留的旧存档枚举值；当前规则对所有玩家统一采用匿名投票
        FootprintTrack,        // 查看附近玩家足迹（Boolean）
    }

    /// <summary>
    /// 职业辅助方法。
    /// </summary>
    public static class ProfessionExtensions
    {
        /// <summary>获取职业的中文名</summary>
        public static string DisplayName(this OnlineProfession profession)
        {
            return profession switch
            {
                OnlineProfession.Inspector       => "警探",
                OnlineProfession.Forensics       => "法医",
                OnlineProfession.Tech            => "技术员",
                OnlineProfession.Enforcer        => "打手",
                OnlineProfession.Fixer           => "清道夫",
                OnlineProfession.UndercoverAgent => "卧底",
                OnlineProfession.Mole            => "内鬼",
                OnlineProfession.Driver          => "车手",
                _                                => "未知"
            };
        }

        /// <summary>获取职业的英文名</summary>
        public static string EnglishName(this OnlineProfession profession)
        {
            return profession switch
            {
                OnlineProfession.Inspector       => "Inspector",
                OnlineProfession.Forensics       => "Forensics",
                OnlineProfession.Tech            => "Tech",
                OnlineProfession.Enforcer        => "Enforcer",
                OnlineProfession.Fixer           => "Fixer",
                OnlineProfession.UndercoverAgent => "Undercover Agent",
                OnlineProfession.Mole            => "Mole",
                OnlineProfession.Driver          => "Driver",
                _                                => "Unknown"
            };
        }

        /// <summary>职业归属的阵营角色</summary>
        public static OnlineRole FactionRole(this OnlineProfession profession)
        {
            return profession switch
            {
                OnlineProfession.Inspector       => OnlineRole.Police,
                OnlineProfession.Forensics       => OnlineRole.Police,
                OnlineProfession.Tech            => OnlineRole.Police,
                OnlineProfession.Enforcer        => OnlineRole.Gang,
                OnlineProfession.Fixer           => OnlineRole.Gang,
                OnlineProfession.UndercoverAgent => OnlineRole.Undercover,
                OnlineProfession.Mole            => OnlineRole.Mole,
                OnlineProfession.Driver          => OnlineRole.Undercover,
                _                                => OnlineRole.Unassigned,
            };
        }

        /// <summary>职业的公开身份（对他人可见的阵营）</summary>
        public static OnlineRole PublicRole(this OnlineProfession profession)
        {
            return profession switch
            {
                OnlineProfession.Mole            => OnlineRole.Police,    // 内鬼公开为警察
                OnlineProfession.UndercoverAgent => OnlineRole.Gang,      // 卧底公开为黑帮
                OnlineProfession.Driver          => OnlineRole.Gang,      // 车手公开为黑帮
                _ => profession.FactionRole(),
            };
        }
    }
}
