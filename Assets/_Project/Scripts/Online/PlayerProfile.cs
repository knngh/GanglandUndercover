using System;
using System.Collections.Generic;
using System.IO;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 5.1: 玩家档案数据。
    /// 本地 JSON 持久化，跨对局累计。
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string PlayerId;
        public string DisplayName;
        public string CreatedAt;

        // ── 统计 ──
        public int TotalMatches;
        public int Wins;
        public int Losses;
        public int MeetingsCalled;
        public int KillsPerformed;
        public int TasksCompleted;
        public int TimesEjected;
        public float TotalPlayTimeSeconds;
        public int MaxKillsInMatch;

        // ── 职业统计 ──
        public Dictionary<string, int> ProfessionPlayCount = new Dictionary<string, int>();
        public Dictionary<string, int> ProfessionWins = new Dictionary<string, int>();

        // ── 信誉 ──
        public int ReputationScore = 100;       // 0-200, 100起点
        public int ReportsReceived;
        public int ReportsFiled;

        // ── 称号 ──
        public List<string> UnlockedTitles = new List<string>();
        public string ActiveTitle;

        // ── 计算属性 ──
        public float WinRate => TotalMatches > 0 ? (float)Wins / TotalMatches : 0f;
        public string MostPlayedProfession
        {
            get
            {
                string best = "Inspector";
                int bestCount = 0;
                foreach (var kv in ProfessionPlayCount)
                {
                    if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
                }
                return best;
            }
        }

        // ── 称号判定 ──
        public string CalculateTitle()
        {
            if (TotalMatches < 5) return "新丁";
            if (WinRate >= 0.7f && TotalMatches >= 20) return "神探";
            if (KillsPerformed >= 50) return "冷血杀手";
            if (TimesEjected == 0 && TotalMatches >= 15) return "千面卧底";
            if (TasksCompleted >= 100) return "勤劳警员";
            if (ReputationScore >= 180) return "模范市民";
            if (ReputationScore <= 30) return "街头混混";
            if (WinRate >= 0.5f) return "资深探员";
            return "普通市民";
        }

        /// <summary>对局结束时更新统计。</summary>
        public void RecordMatchEnd(bool won, OnlineRole role, OnlineProfession profession,
            int kills, int tasks, float duration, bool wasEjected)
        {
            TotalMatches++;
            TotalPlayTimeSeconds += duration;
            if (won) Wins++; else Losses++;
            KillsPerformed += kills;
            TasksCompleted += tasks;
            MaxKillsInMatch = Mathf.Max(MaxKillsInMatch, kills);
            if (wasEjected) TimesEjected++;

            string profKey = profession.ToString();
            if (!ProfessionPlayCount.ContainsKey(profKey)) ProfessionPlayCount[profKey] = 0;
            ProfessionPlayCount[profKey]++;
            if (won)
            {
                if (!ProfessionWins.ContainsKey(profKey)) ProfessionWins[profKey] = 0;
                ProfessionWins[profKey]++;
            }

            // 信誉微调
            if (wasEjected) ReputationScore = Mathf.Max(0, ReputationScore - 3);
            else ReputationScore = Mathf.Min(200, ReputationScore + 1);

            ActiveTitle = CalculateTitle();
        }

        /// <summary>举报他人。</summary>
        public void FileReport()
        {
            ReportsFiled++;
            ReputationScore = Mathf.Min(200, ReputationScore + 1);
        }

        /// <summary>被举报。</summary>
        public void ReceiveReport()
        {
            ReportsReceived++;
            ReputationScore = Mathf.Max(0, ReputationScore - 5);
        }
    }

    /// <summary>
    /// Phase 5.1: 玩家档案管理器。
    /// 本地 JSON 持久化，支持多 Profile（按 PlayerId 索引）。
    /// </summary>
    public static class ProfileManager
    {
        private static readonly string ProfileDir = Path.Combine(Application.persistentDataPath, "Profiles");
        private static readonly Dictionary<string, PlayerProfile> _cache = new Dictionary<string, PlayerProfile>();

        /// <summary>获取或创建档案。</summary>
        public static PlayerProfile GetOrCreate(string playerId, string displayName = null)
        {
            if (_cache.TryGetValue(playerId, out var cached)) return cached;

            string path = ProfilePath(playerId);
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var profile = JsonUtility.FromJson<PlayerProfile>(json);
                    _cache[playerId] = profile;
                    return profile;
                }
                catch { /* corrupted, create new */ }
            }

            var newProfile = new PlayerProfile
            {
                PlayerId = playerId,
                DisplayName = displayName ?? "港区玩家",
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            _cache[playerId] = newProfile;
            Save(newProfile);
            return newProfile;
        }

        /// <summary>保存档案到磁盘。</summary>
        public static void Save(PlayerProfile profile)
        {
            if (!Directory.Exists(ProfileDir)) Directory.CreateDirectory(ProfileDir);
            string path = ProfilePath(profile.PlayerId);
            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(path, json);
        }

        /// <summary>清除缓存（重新加载）。</summary>
        public static void ClearCache() => _cache.Clear();

        private static string ProfilePath(string playerId)
        {
            string safe = playerId.Replace("/", "_").Replace("\\", "_");
            return Path.Combine(ProfileDir, $"{safe}.json");
        }
    }
}
