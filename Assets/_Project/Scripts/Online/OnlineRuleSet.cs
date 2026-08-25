using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// M4 增强：角色分配表 + 节奏参数。单个 ScriptableObject 控制对局全部可调配置。
    /// </summary>
    [CreateAssetMenu(menuName = "Gangland/Online Rule Set", fileName = "OnlineRuleSet")]
    public class OnlineRuleSet : ScriptableObject
    {
        private const int MidSizeEvidenceTargetFloor = 34;
        private const int SmallLobbyPacingPlayers = 4;
        private const int MidLobbyPacingPlayers = 6;
        private const int LargeLobbyPacingPlayers = 8;

        // ═══════════════════════════════════════════════════════════════
        // M4.1 角色分配
        // ═══════════════════════════════════════════════════════════════

        [Header("── M4.1 角色分配 ──")]
        [Tooltip("按人数配置阵营比例。列表按 playerCount 升序排列，获取时取 <= 当前人数的最大预设。")]
        public RoleDistribution[] RoleDistributionTable = new RoleDistribution[]
        {
            // 4 人仅作为本地预览兼容档，也必须保留核心卧底博弈。
            new RoleDistribution { playerCount = 4,  gang = 1, undercover = 1, mole = 0 },
            new RoleDistribution { playerCount = 5,  gang = 1, undercover = 1, mole = 0 },
            new RoleDistribution { playerCount = 6,  gang = 1, undercover = 1, mole = 0 },
            new RoleDistribution { playerCount = 7,  gang = 2, undercover = 1, mole = 0 },
            new RoleDistribution { playerCount = 8,  gang = 2, undercover = 1, mole = 1 },
            new RoleDistribution { playerCount = 9,  gang = 2, undercover = 2, mole = 1 },
            new RoleDistribution { playerCount = 10, gang = 3, undercover = 2, mole = 1 },
        };

        /// <summary>
        /// 根据玩家数获取阵营分配。取 <= playerCount 的最大预设，兜底为第一项。
        /// </summary>
        public RoleDistribution GetRoleDistribution(int playerCount)
        {
            if (RoleDistributionTable == null || RoleDistributionTable.Length == 0)
                return new RoleDistribution { playerCount = playerCount, gang = 1, undercover = 1, mole = 0 };

            RoleDistribution best = RoleDistributionTable[0];
            for (int i = 0; i < RoleDistributionTable.Length; i++)
            {
                if (RoleDistributionTable[i].playerCount <= playerCount &&
                    RoleDistributionTable[i].playerCount > best.playerCount)
                    best = RoleDistributionTable[i];
            }
            return best;
        }

        // ═══════════════════════════════════════════════════════════════
        // M4.2 节奏与时长
        // ═══════════════════════════════════════════════════════════════

        [Header("── M4.2 节奏 ──")]
        [Tooltip("任务总量（每个玩家承担的短任务数），默认 4。总任务 = 非Gang人数 × tasksPerPlayer。")]
        [Range(2, 8)]
        public int TasksPerNonGangPlayer = 4;

        [Tooltip("任务完成给予的证据分（每完成一个任务）。")]
        [Range(1, 6)]
        public int EvidencePerTask = 3;

        [Tooltip("会议后击杀冷却宽容期（秒）。会议结束后的短暂免杀窗口。")]
        [Range(0f, 10f)]
        public float PostMeetingKillGraceSeconds = 3f;

        [Tooltip("首次击杀最小延迟（开局后秒数），防止开局秒杀。")]
        [Range(0f, 30f)]
        public float FirstKillMinDelaySeconds = 8f;

        [Tooltip("报告尸体冷却时间（秒），防止连续报案刷会议。")]
        [Range(0f, 15f)]
        public float ReportCooldownSeconds = 5f;

        // ═══════════════════════════════════════════════════════════════
        // 原有参数（保留不变，部分调整默认值以匹配 M4 闭合目标）
        // ═══════════════════════════════════════════════════════════════

        [Header("击杀与报案")]
        [Tooltip("击杀互动范围（世界单位）。")]
        public float KillRange = 1.1f;
        [Tooltip("报案互动范围（世界单位）。")]
        public float ReportRange = 1.25f;
        [Tooltip("击杀冷却时间（秒）。M4 收紧至 25s，确保 10-15 分钟内击杀密度合理。")]
        public float KillCooldownSeconds = 25f;

        [Header("会议与投票")]
        [Tooltip("会议讨论阶段时长（秒）。")]
        public float MeetingIntroSeconds = 35f;
        [Tooltip("投票阶段时长（秒）。M4 收紧至 40s。")]
        public float VotingSeconds = 40f;
        [Tooltip("紧急会议冷却时间（秒）。")]
        public float EmergencyCooldownSeconds = 75f;
        [Tooltip("紧急会议次数上限。")]
        public int MaxEmergencyMeetings = 3;

        [Header("人数")]
        [Tooltip("房间允许的最少玩家数（绝对下限）。")]
        public int MinimumRoomPlayers = 4;
        [Tooltip("房间允许的最多玩家数（绝对上限）。")]
        public int MaximumRoomPlayers = 10;
        [Tooltip("默认房间最小人数。")]
        public int DefaultRoomMinPlayers = 8;
        [Tooltip("默认房间最大人数。")]
        public int DefaultRoomMaxPlayers = 10;
        [Tooltip("开局所需的最少可玩人数。")]
        public int MinimumPlayablePlayers = 4;

        [Header("证据")]
        [Tooltip("默认证据胜利目标。")]
        public int DefaultEvidenceTarget = 44;
        [Tooltip("证据目标滑条最小值。")]
        public int MinEvidenceTarget = 28;
        [Tooltip("证据目标滑条最大值。")]
        public int MaxEvidenceTarget = 56;

        [Header("房间规则开关")]
        [Tooltip("人数不足时是否自动 AI 补位。")]
        public bool RoomAutoFillAi = true;
        [Tooltip("出局时是否公开角色身份。正式规则默认隐藏，仅保留为房主自定义规则。")]
        public bool RevealRoleOnEject = false;
        [Tooltip("行动阶段是否启用近距离语音。M1 收尾：Vivox 已移除，方案 B（文本聊天）替代。")]
        public bool ProximityVoiceEnabled = true;

        [Header("破坏技能时长")]
        [Tooltip("停电持续时间（秒）。")]
        public float BlackoutSeconds = 28f;
        [Tooltip("封锁持续时间（秒）。")]
        public float LockdownSeconds = 32f;
        [Tooltip("通讯干扰持续时间（秒）。")]
        public float CommunicationJamSeconds = 30f;
        [Tooltip("证据泄露持续时间（秒）。")]
        public float EvidenceLeakSeconds = 36f;
        [Tooltip("巡逻警报持续时间（秒）。")]
        public float PatrolAlertSeconds = 30f;

        [Header("技能")]
        [Tooltip("技能冷却时间（秒）。")]
        public float AbilityCooldownSeconds = 13f;

        [Header("时间限制")]
        [Tooltip("比赛最短目标时间（秒）。M4 收紧至 600s（10min）。")]
        public float MatchTargetMinSeconds = 600f;
        [Tooltip("比赛硬性上限时间（秒）。10-20 分钟局时设计的上限，1200s（20min）。")]
        public float MatchHardLimitSeconds = 1200f;

        [Header("AI")]
        [Tooltip("AI 行动延迟（秒），联机模式。")]
        public float AiActionGraceSeconds = 22f;
        [Tooltip("AI 行动延迟（秒），本地预览模式。")]
        public float PreviewAiActionGraceSeconds = 55f;
        [Tooltip("Bot 发现尸体时主动报案的概率。")]
        [Range(0f, 1f)]
        public float BotBodyReportProbability = 0.42f;
        [Tooltip("Bot 在行动阶段主动发起紧急会议的概率。")]
        [Range(0f, 1f)]
        public float BotEmergencyMeetingProbability = 0.12f;
        [Tooltip("Bot 移动速度倍率。")]
        [Range(0.5f, 4f)]
        public float BotMoveSpeedMultiplier = 1f;

        [Header("地图交互")]
        [Tooltip("通用互动范围（世界单位）。")]
        public float InteractionRange = 1.08f;
        [Tooltip("暗线/通风管入口交互范围（世界单位）。")]
        public float UnderworldTransitRange = 1.15f;
        [Tooltip("通风管冷却时间（秒）。")]
        public float VentCooldownSeconds = 10f;
        [Tooltip("暗线通道节点数量。")]
        public int UnderworldPassageCount = 4;

        [Header("案卷")]
        [Tooltip("案卷最大条目数。")]
        public int MaxCaseLogEntries = 8;

        // ═══════════════════════════════════════════════════════════════
        // M8.2 职业能力表
        // ═══════════════════════════════════════════════════════════════

        [Header("── M8.2 职业能力 ──")]
        [Tooltip("按职业配置的专属能力列表。每个职业可以有 0-N 个能力。")]
        public ProfessionAbilitySet[] ProfessionAbilities = new ProfessionAbilitySet[]
        {
            // ── 警察方 ──
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.Inspector,
                Abilities = new[]
                {
                    new ProfessionAbility { Type = AbilityType.ReportCooldownReduce, Multiplier = 0.8f, BonusValue = 0f, Enabled = true },
                    new ProfessionAbility { Type = AbilityType.FootprintTrack, Multiplier = 1f, BonusValue = 0f, Enabled = true },
                }
            },
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.Forensics,
                Abilities = new[]
                {
                    new ProfessionAbility { Type = AbilityType.CorpseExamine, Multiplier = 1f, BonusValue = 2f, Enabled = true },
                    new ProfessionAbility { Type = AbilityType.TaskSpeedBonus, Multiplier = 1.1f, BonusValue = 0f, Enabled = true },
                }
            },
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.Tech,
                Abilities = new[]
                {
                    new ProfessionAbility { Type = AbilityType.RemoteSurveillance, Multiplier = 1f, BonusValue = 0f, Enabled = true },
                    new ProfessionAbility { Type = AbilityType.EvidenceChainBonus, Multiplier = 1.3f, BonusValue = 0f, Enabled = true },
                }
            },

            // ── 黑帮方 ──
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.Enforcer,
                Abilities = new[]
                {
                    new ProfessionAbility { Type = AbilityType.KillCooldownReduce, Multiplier = 0.75f, BonusValue = 0f, Enabled = true },
                    new ProfessionAbility { Type = AbilityType.DarkVision, Multiplier = 1f, BonusValue = 0f, Enabled = true },
                }
            },
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.Fixer,
                Abilities = new[]
                {
                    new ProfessionAbility { Type = AbilityType.BodyDrag, Multiplier = 1f, BonusValue = 0f, Enabled = true },
                    new ProfessionAbility { Type = AbilityType.SabotageCooldownReduce, Multiplier = 0.8f, BonusValue = 0f, Enabled = true },
                }
            },

            // ── 卧底方 ──
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.UndercoverAgent,
                Abilities = Array.Empty<ProfessionAbility>()
            },
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.Driver,
                Abilities = new[]
                {
                    new ProfessionAbility { Type = AbilityType.VentSpeedBonus, Multiplier = 1.5f, BonusValue = 0f, Enabled = true },
                    new ProfessionAbility { Type = AbilityType.MoveSpeedBonus, Multiplier = 1.08f, BonusValue = 0f, Enabled = true },
                }
            },
            new ProfessionAbilitySet
            {
                Profession = OnlineProfession.Mole,
                Abilities = new[]
                {
                    new ProfessionAbility { Type = AbilityType.SabotageCooldownReduce, Multiplier = 0.9f, BonusValue = 0f, Enabled = true },
                }
            },
        };

        // ═══════════════════════════════════════════════════════════════
        // 便捷查询方法
        // ═══════════════════════════════════════════════════════════════

        /// <summary>根据当前玩家数计算可用紧急会议次数。</summary>
        public int EmergencyMeetingLimitFor(int playerCount)
        {
            return Mathf.Clamp(playerCount / 3, 1, MaxEmergencyMeetings);
        }

        public float KillCooldownFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, 30f, KillCooldownSeconds, 22f);
        }

        public float MeetingIntroSecondsFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, 30f, MeetingIntroSeconds, 45f);
        }

        public float VotingSecondsFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, 30f, VotingSeconds, 50f);
        }

        public float EmergencyCooldownSecondsFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, 60f, EmergencyCooldownSeconds, 90f);
        }

        public float ReportRangeFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, ReportRange, 1.35f, 1.5f);
        }

        public float ReportCooldownSecondsFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, ReportCooldownSeconds, ReportCooldownSeconds, 6f);
        }

        public float FirstKillMinDelaySecondsFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, 12f, 10f, FirstKillMinDelaySeconds);
        }

        public float PostMeetingKillGraceSecondsFor(int playerCount)
        {
            return ScalePacingByPlayerCount(playerCount, PostMeetingKillGraceSeconds, PostMeetingKillGraceSeconds, 4f);
        }

        /// <summary>计算本局任务总数 = 非Gang人数 × tasksPerNonGangPlayer。</summary>
        public int TotalTaskCount(int playerCount, int gangCount)
        {
            int nonGang = Mathf.Max(1, playerCount - gangCount);
            return nonGang * TasksPerNonGangPlayer;
        }

        /// <summary>根据人数缩放证据目标（默认值 × 人数系数）。</summary>
        public int ScaledEvidenceTarget(int playerCount)
        {
            float ratio = Mathf.Clamp(playerCount / 8f, 0.6f, 1.3f);
            int minTarget = playerCount >= 6
                ? Mathf.Max(MinEvidenceTarget, MidSizeEvidenceTargetFloor)
                : MinEvidenceTarget;

            return Mathf.Clamp(
                Mathf.RoundToInt(DefaultEvidenceTarget * ratio),
                minTarget,
                MaxEvidenceTarget);
        }

        private static float ScalePacingByPlayerCount(int playerCount, float smallLobbyValue, float midLobbyValue, float largeLobbyValue)
        {
            if (playerCount <= SmallLobbyPacingPlayers)
            {
                return smallLobbyValue;
            }

            if (playerCount >= LargeLobbyPacingPlayers)
            {
                return largeLobbyValue;
            }

            if (playerCount <= MidLobbyPacingPlayers)
            {
                float t = Mathf.InverseLerp(SmallLobbyPacingPlayers, MidLobbyPacingPlayers, playerCount);
                return Mathf.Lerp(smallLobbyValue, midLobbyValue, t);
            }

            float upperT = Mathf.InverseLerp(MidLobbyPacingPlayers, LargeLobbyPacingPlayers, playerCount);
            return Mathf.Lerp(midLobbyValue, largeLobbyValue, upperT);
        }

        // ═══════════════════════════════════════════════════════════════
        // M8.2 职业能力查询
        // ═══════════════════════════════════════════════════════════════

        /// <summary>获取指定职业的能力集</summary>
        public ProfessionAbilitySet? GetProfessionAbilities(OnlineProfession profession)
        {
            if (ProfessionAbilities == null) return null;
            foreach (var set in ProfessionAbilities)
            {
                if (set.Profession == profession)
                    return set;
            }
            return null;
        }

        /// <summary>检查职业是否有某能力</summary>
        public bool HasAbility(OnlineProfession profession, AbilityType type)
        {
            var set = GetProfessionAbilities(profession);
            return set?.HasAbility(type) ?? false;
        }

        /// <summary>获取职业的某能力倍率（默认 1.0）</summary>
        public float GetAbilityMultiplier(OnlineProfession profession, AbilityType type)
        {
            var set = GetProfessionAbilities(profession);
            return set?.GetMultiplier(type) ?? 1f;
        }

        /// <summary>获取职业的某能力附加值（默认 0）</summary>
        public float GetAbilityBonus(OnlineProfession profession, AbilityType type)
        {
            var set = GetProfessionAbilities(profession);
            return set?.GetBonus(type) ?? 0f;
        }
    }

    /// <summary>
    /// M4.1：按人数配置的阵营分配预设。
    /// </summary>
    [Serializable]
    public struct RoleDistribution
    {
        [Tooltip("对应玩家人数（含 Bot）。")]
        public int playerCount;

        [Tooltip("黑帮阵营人数（Enforcer / Fixer）。")]
        public int gang;

        [Tooltip("卧底阵营人数（Undercover 公开为黑帮）。")]
        public int undercover;

        [Tooltip("内鬼人数（Mole 公开为警察）。")]
        public int mole;

        /// <summary>警察/市民阵营人数 = 总人数 - gang - undercover - mole。</summary>
        public int PoliceCount => Mathf.Max(1, playerCount - gang - undercover - mole);

        /// <summary>黑帮方总人数（含表面卧底）。</summary>
        public int GangSideTotal => gang + undercover;
    }

    /// <summary>
    /// M8.2：职业能力集合。每个职业包含一组能力。
    /// </summary>
    [Serializable]
    public struct ProfessionAbilitySet
    {
        [Tooltip("对应职业")]
        public OnlineProfession Profession;

        [Tooltip("该职业拥有的能力列表")]
        public ProfessionAbility[] Abilities;

        /// <summary>检查是否拥有指定能力类型</summary>
        public bool HasAbility(AbilityType type)
        {
            if (Abilities == null) return false;
            foreach (var a in Abilities)
            {
                if (a.Enabled && a.Type == type)
                    return true;
            }
            return false;
        }

        /// <summary>获取指定能力的倍率（默认 1.0）</summary>
        public float GetMultiplier(AbilityType type)
        {
            if (Abilities == null) return 1f;
            foreach (var a in Abilities)
            {
                if (a.Enabled && a.Type == type)
                    return a.Multiplier;
            }
            return 1f;
        }

        /// <summary>获取指定能力的附加值（默认 0）</summary>
        public float GetBonus(AbilityType type)
        {
            if (Abilities == null) return 0f;
            foreach (var a in Abilities)
            {
                if (a.Enabled && a.Type == type)
                    return a.BonusValue;
            }
            return 0f;
        }
    }
}
