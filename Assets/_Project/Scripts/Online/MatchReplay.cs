using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 5.3: 对局回放关键帧事件。
    /// 记录对局中重要时刻，用于赛后回放。
    /// </summary>
    [Serializable]
    public class MatchReplayEvent
    {
        public float Timestamp;
        public string EventType;   // Kill, Report, Meeting, Vote, Sabotage, TaskComplete, EmergencyTask
        public string Description;
        public Vector3 WorldPosition;
        public ulong ActorId;
        public ulong TargetId;

        public MatchReplayEvent() { }

        public MatchReplayEvent(float ts, string type, string desc, Vector3 pos, ulong actor = 0, ulong target = 0)
        {
            Timestamp = ts; EventType = type; Description = desc;
            WorldPosition = pos; ActorId = actor; TargetId = target;
        }
    }

    /// <summary>
    /// Phase 5.3: 对局回放数据。
    /// 完整记录一局的所有关键时刻。
    /// </summary>
    [Serializable]
    public class MatchReplay
    {
        public string MatchId;
        public string MapName;
        public int PlayerCount;
        public float MatchDuration;
        public string WinnerFaction;
        public string RecordedAt;
        public List<MatchReplayEvent> Events = new List<MatchReplayEvent>();

        /// <summary>记录事件。</summary>
        public void Record(MatchReplayEvent evt) => Events.Add(evt);

        /// <summary>获取关键时刻回放（最后N个事件）。</summary>
        public List<MatchReplayEvent> KeyMoments(int count = 5)
        {
            List<MatchReplayEvent> result = new List<MatchReplayEvent>();
            for (int i = Events.Count - 1; i >= 0 && result.Count < count; i--)
            {
                if (Events[i].EventType == "Kill" || Events[i].EventType == "EmergencyTask" || Events[i].EventType == "Meeting")
                    result.Add(Events[i]);
            }
            result.Reverse();
            return result;
        }

        /// <summary>序列化为 JSON。</summary>
        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>保存到磁盘。</summary>
        public void SaveToDisk()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Replays");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"replay_{MatchId}.json");
            File.WriteAllText(path, ToJson());
        }
    }

    /// <summary>
    /// Phase 5.2: 简化匹配队列。
    /// 按信誉分分组，降低恶意玩家相遇概率。
    /// </summary>
    public static class MatchmakingQueue
    {
        // 信誉分分组
        public enum ReputationTier { Low = 0, Mid = 1, High = 2 }

        public static ReputationTier GetTier(int reputation)
        {
            if (reputation <= 40) return ReputationTier.Low;
            if (reputation >= 100) return ReputationTier.High;
            return ReputationTier.Mid;
        }

        /// <summary>两个玩家是否可以匹配。</summary>
        public static bool CanMatch(PlayerProfile a, PlayerProfile b)
        {
            var tierA = GetTier(a.ReputationScore);
            var tierB = GetTier(b.ReputationScore);
            return Mathf.Abs((int)tierA - (int)tierB) <= 1; // 相邻等级可匹配
        }

        /// <summary>检查举报阈值——过多举报触发审查。</summary>
        public static bool NeedsReview(PlayerProfile profile)
        {
            return profile.ReportsReceived >= 5 && profile.ReportsReceived > profile.TotalMatches * 0.3f;
        }
    }

    /// <summary>
    /// Phase 5.2: 举报系统。
    /// </summary>
    public class ReportSystem
    {
        private readonly OnlineMatchController _controller;

        public ReportSystem(OnlineMatchController controller)
        {
            _controller = controller;
        }

        /// <summary>举报玩家。</summary>
        public void ReportPlayer(ulong reporterId, ulong reportedId, string reason)
        {
            var reporter = ProfileManager.GetOrCreate(reporterId.ToString());
            var reported = ProfileManager.GetOrCreate(reportedId.ToString());

            reporter.FileReport();
            ProfileManager.Save(reporter);

            reported.ReceiveReport();
            ProfileManager.Save(reported);

            if (MatchmakingQueue.NeedsReview(reported))
            {
                Debug.LogWarning($"[ReportSystem] 玩家 {reportedId} 举报过多，建议审查。");
            }
        }
    }
}
