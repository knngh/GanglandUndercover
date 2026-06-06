using System;
using System.Collections.Generic;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 5.4: 赛季定义。
    /// 每个赛季有不同的主题和新内容。
    /// </summary>
    [Serializable]
    public class SeasonDefinition
    {
        public string SeasonId;           // e.g. "S01"
        public string SeasonName;         // e.g. "九龙城寨之春"
        public string SeasonTheme;        // e.g. "noir_spring"
        public DateTime StartDate;
        public DateTime EndDate;
        public string Description;
        public List<string> NewFeatures;  // 新地图/新职业/新模式
        public List<SeasonReward> FreeRewards = new List<SeasonReward>();
        public List<SeasonReward> PremiumRewards = new List<SeasonReward>();

        public bool IsActive(DateTime now) => now >= StartDate && now <= EndDate;
    }

    /// <summary>
    /// Phase 5.4: 赛季奖励项。
    /// </summary>
    [Serializable]
    public class SeasonReward
    {
        public int RequiredLevel;         // 解锁所需通行证等级
        public string RewardType;         // "Title", "Skin", "Appearance", "Emote", "Currency"
        public string RewardId;           // 奖励的具体 ID
        public string RewardName;         // 显示名称
        public bool IsPremium;            // 是否付费通行证专属
    }

    /// <summary>
    /// Phase 5.4: 玩家赛季进度。
    /// 每个赛季独立追踪。
    /// </summary>
    [Serializable]
    public class PlayerSeasonProgress
    {
        public string SeasonId;
        public int XP;
        public int Level;
        public bool HasPremiumPass;
        public HashSet<string> ClaimedRewards = new HashSet<string>();

        public int XPToNextLevel()
        {
            // 每级需要 1000 + level*200 XP
            return 1000 + Level * 200;
        }

        /// <summary>添加 XP，返回是否升级。</summary>
        public bool AddXP(int amount)
        {
            XP += amount;
            int target = XPToNextLevel();
            if (XP >= target)
            {
                XP -= target;
                Level++;
                return true;
            }
            return false;
        }

        /// <summary>对局完成获得 XP。</summary>
        public static int MatchXP(bool won, int kills, int tasks, float durationMinutes)
        {
            int baseXp = Mathf.RoundToInt(durationMinutes * 10);
            if (won) baseXp += 50;
            baseXp += kills * 20;
            baseXp += tasks * 5;
            return baseXp;
        }

        private static class Mathf
        {
            public static int RoundToInt(float f) => (int)(f + 0.5f);
        }
    }

    /// <summary>
    /// Phase 5.4: 赛季管理器。
    /// </summary>
    public static class SeasonManager
    {
        private static readonly List<SeasonDefinition> _seasons = new List<SeasonDefinition>();
        private static readonly Dictionary<string, PlayerSeasonProgress> _progress = new Dictionary<string, PlayerSeasonProgress>();

        /// <summary>注册赛季。</summary>
        public static void RegisterSeason(SeasonDefinition season)
        {
            _seasons.RemoveAll(s => s.SeasonId == season.SeasonId);
            _seasons.Add(season);
        }

        /// <summary>获取当前活跃赛季。</summary>
        public static SeasonDefinition ActiveSeason()
        {
            var now = DateTime.UtcNow;
            foreach (var s in _seasons)
                if (s.IsActive(now)) return s;
            return null;
        }

        /// <summary>获取或创建玩家赛季进度。</summary>
        public static PlayerSeasonProgress GetProgress(string playerId, string seasonId)
        {
            string key = $"{playerId}_{seasonId}";
            if (!_progress.ContainsKey(key))
                _progress[key] = new PlayerSeasonProgress { SeasonId = seasonId };
            return _progress[key];
        }

        /// <summary>获取可领取的奖励。</summary>
        public static List<SeasonReward> ClaimableRewards(SeasonDefinition season, PlayerSeasonProgress progress)
        {
            List<SeasonReward> result = new List<SeasonReward>();
            foreach (var reward in season.FreeRewards)
            {
                if (progress.Level >= reward.RequiredLevel && !progress.ClaimedRewards.Contains(reward.RewardId))
                    result.Add(reward);
            }
            if (progress.HasPremiumPass)
            {
                foreach (var reward in season.PremiumRewards)
                {
                    if (progress.Level >= reward.RequiredLevel && !progress.ClaimedRewards.Contains(reward.RewardId))
                        result.Add(reward);
                }
            }
            return result;
        }

        /// <summary>创建默认 S01 赛季。</summary>
        public static SeasonDefinition CreateDefaultSeason()
        {
            return new SeasonDefinition
            {
                SeasonId = "S01",
                SeasonName = "九龙城寨之春",
                SeasonTheme = "noir_spring",
                StartDate = new DateTime(2026, 6, 1),
                EndDate = new DateTime(2026, 8, 31),
                Description = "首季：港区暗流涌动，六职业齐聚九龙城。",
                NewFeatures = new List<string> { "证据链系统", "职业能力全开", "卧底双身份" },
                FreeRewards = new List<SeasonReward>
                {
                    new SeasonReward { RequiredLevel = 1, RewardType = "Title", RewardId = "title_newcomer", RewardName = "新丁" },
                    new SeasonReward { RequiredLevel = 5, RewardType = "Title", RewardId = "title_detective", RewardName = "探员" },
                    new SeasonReward { RequiredLevel = 10, RewardType = "Appearance", RewardId = "skin_default_badge", RewardName = "警徽胸针" },
                    new SeasonReward { RequiredLevel = 20, RewardType = "Title", RewardId = "title_veteran", RewardName = "资深探员" },
                    new SeasonReward { RequiredLevel = 30, RewardType = "Appearance", RewardId = "skin_noir_hat", RewardName = "黑帽" },
                },
                PremiumRewards = new List<SeasonReward>
                {
                    new SeasonReward { RequiredLevel = 1, RewardType = "Appearance", RewardId = "prem_gold_badge", RewardName = "金警徽", IsPremium = true },
                    new SeasonReward { RequiredLevel = 15, RewardType = "Skin", RewardId = "prem_inspector_noir", RewardName = "暗夜警探皮肤", IsPremium = true },
                    new SeasonReward { RequiredLevel = 25, RewardType = "Title", RewardId = "prem_godfather", RewardName = "教父", IsPremium = true },
                    new SeasonReward { RequiredLevel = 40, RewardType = "Skin", RewardId = "prem_enforcer_dragon", RewardName = "龙纹打手皮肤", IsPremium = true },
                }
            };
        }
    }
}
