using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 房间规则配置 ScriptableObject。
    /// 从 OnlineMatchController 抽出的可配置规则层，默认值与 M0 基线行为一致。
    /// </summary>
    [CreateAssetMenu(menuName = "Gangland/Online Rule Set", fileName = "OnlineRuleSet")]
    public class OnlineRuleSet : ScriptableObject
    {
        [Header("击杀与报案")]
        [Tooltip("击杀互动范围（世界单位）。")]
        public float KillRange = 0.9f;
        [Tooltip("报案互动范围（世界单位）。")]
        public float ReportRange = 1.25f;
        [Tooltip("击杀冷却时间（秒）。")]
        public float KillCooldownSeconds = 34f;

        [Header("会议与投票")]
        [Tooltip("会议讨论阶段时长（秒）。")]
        public float MeetingIntroSeconds = 35f;
        [Tooltip("投票阶段时长（秒）。")]
        public float VotingSeconds = 55f;
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
        public int MinimumPlayablePlayers = 5;

        [Header("证据")]
        [Tooltip("默认证据胜利目标。")]
        public int DefaultEvidenceTarget = 44;
        [Tooltip("证据目标滑条最小值。")]
        public int MinEvidenceTarget = 34;
        [Tooltip("证据目标滑条最大值。")]
        public int MaxEvidenceTarget = 56;

        [Header("房间规则开关")]
        [Tooltip("人数不足时是否自动 AI 补位。")]
        public bool RoomAutoFillAi = true;
        [Tooltip("出局时是否公开角色身份。")]
        public bool RevealRoleOnEject = true;
        [Tooltip("行动阶段是否启用近距离语音。")]
        [Tooltip("M1 收尾：Vivox 已移除，方案 B（文本聊天）替代。此处保留序列化占位，对游戏逻辑无影响。")]
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
        [Tooltip("比赛最短目标时间（秒）。")]
        public float MatchTargetMinSeconds = 600f;
        [Tooltip("比赛硬性上限时间（秒）。")]
        public float MatchHardLimitSeconds = 1200f;

        [Header("AI")]
        [Tooltip("AI 行动延迟（秒），联机模式。")]
        public float AiActionGraceSeconds = 22f;
        [Tooltip("AI 行动延迟（秒），本地预览模式。")]
        public float PreviewAiActionGraceSeconds = 55f;

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

        /// <summary>
        /// 根据当前玩家数计算可用紧急会议次数。
        /// </summary>
        public int EmergencyMeetingLimitFor(int playerCount)
        {
            return Mathf.Clamp(playerCount / 3, 1, MaxEmergencyMeetings);
        }
    }
}
