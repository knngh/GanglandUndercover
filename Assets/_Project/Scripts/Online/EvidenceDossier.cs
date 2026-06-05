using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// C2 证据链指证系统。
    ///
    /// 追踪每个玩家与证据事件的关联——当证据事件（任务完成/破坏/击杀尸体发现）
    /// 发生时，自动标记事件附近的存活玩家为「嫌疑人」。
    ///
    /// 会议中通过 MeetingEvidenceDossier 展示证据指证面板，
    /// 帮助玩家推理出高嫌疑目标。
    /// </summary>
    public sealed class EvidenceDossier
    {
        /// <summary>嫌疑记录</summary>
        public readonly Dictionary<ulong, int> SuspectEvidenceCount = new Dictionary<ulong, int>();
        public readonly Dictionary<ulong, List<string>> SuspectEvidenceLog = new Dictionary<ulong, List<string>>();

        /// <summary>最新证据事件描述</summary>
        public readonly List<string> RecentEvents = new List<string>();
        private const int MaxRecentEvents = 10;

        private readonly OnlineMatchController _ctrl;

        public EvidenceDossier(OnlineMatchController ctrl)
        {
            _ctrl = ctrl;
        }

        /// <summary>
        /// 注册证据事件。自动将事件发生点附近的存活玩家标记为嫌疑人。
        /// </summary>
        public void RegisterEvidence(string eventDescription, Vector2 eventPosition, float suspectRadius = 4.0f)
        {
            RecentEvents.Add(eventDescription);
            if (RecentEvents.Count > MaxRecentEvents)
                RecentEvents.RemoveAt(0);

            // 标记附近存活玩家
            foreach (var kv in _ctrl.Players)
            {
                if (!kv.Value.Alive) continue;
                float dist = Vector2.Distance(
                    new Vector2(kv.Value.Position.x, kv.Value.Position.y),
                    eventPosition);
                if (dist <= suspectRadius)
                {
                    AddSuspect(kv.Key, eventDescription);
                }
            }
        }

        private void AddSuspect(ulong playerId, string reason)
        {
            if (!SuspectEvidenceCount.ContainsKey(playerId))
            {
                SuspectEvidenceCount[playerId] = 0;
                SuspectEvidenceLog[playerId] = new List<string>();
            }
            SuspectEvidenceCount[playerId]++;
            SuspectEvidenceLog[playerId].Add(reason);
            if (SuspectEvidenceLog[playerId].Count > 5)
                SuspectEvidenceLog[playerId].RemoveAt(0);
        }

        /// <summary>
        /// 获取会议证据指证摘要（供 HUD 展示）。
        /// 格式：玩家名：证据数 + 最近一件证据
        /// </summary>
        public string MeetingEvidenceDossier()
        {
            if (SuspectEvidenceCount.Count == 0)
                return "暂无证据指向特定目标。";

            var lines = new List<string>();
            foreach (var kv in SuspectEvidenceCount)
            {
                if (kv.Value == 0) continue;
                string name = _ctrl.GetPlayerDisplayName(kv.Key);
                string lastEvidence = SuspectEvidenceLog.TryGetValue(kv.Key, out var log) && log.Count > 0
                    ? log[log.Count - 1]
                    : "未知";
                lines.Add($"{name}：{kv.Value}条证据 — {lastEvidence}");
            }
            return string.Join("\n", lines);
        }

        /// <summary>清除所有证据追踪（新一局开始时）</summary>
        public void Clear()
        {
            SuspectEvidenceCount.Clear();
            SuspectEvidenceLog.Clear();
            RecentEvents.Clear();
        }
    }
}
