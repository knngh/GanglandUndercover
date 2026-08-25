using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// M8.4：对局数据采集器。
    ///
    /// 在对局结算时自动记录关键数据（时长/阵营/职业/任务/证据/地图等），
    /// 支持 JSON 导出和编辑器内查看，用于平衡调参的数据驱动决策。
    ///
    /// 使用方式：
    ///   1. OnlineMatchController.SetResult() 中自动调用 LogMatch()
    ///   2. 对局日志写入 Application.persistentDataPath/match_logs/
    ///   3. Editor 通过 Tools → Match Stats Viewer 查看汇总
    /// </summary>
    public sealed class MatchStatsCollector
    {
        private const string LogDirName = "match_logs";
        private const string LogFilePrefix = "match_";
        private const int MaxMemoryEntries = 50;  // 内存中最多保留最近 N 场

        private readonly List<MatchLogEntry> _recentEntries = new List<MatchLogEntry>();
        private int _totalMatchesLogged;

        public IReadOnlyList<MatchLogEntry> RecentEntries => _recentEntries;
        public int TotalMatchesLogged => _totalMatchesLogged;

        /// <summary>对局日志保存目录（跨平台）</summary>
        public static string LogDirectory =>
            Path.Combine(Application.persistentDataPath, LogDirName);

        /// <summary>确保日志目录存在</summary>
        private static void EnsureLogDirectory()
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);
        }

        /// <summary>
        /// 从 OnlineMatchController 采集当前对局的完整数据并持久化。
        /// 应在结算时调用（SetResult 内）。
        /// </summary>
        public void LogMatch(OnlineMatchController ctrl)
        {
            if (ctrl == null) return;

            try
            {
                var entry = BuildEntry(ctrl);
                _recentEntries.Add(entry);
                _totalMatchesLogged++;

                // 内存裁剪
                while (_recentEntries.Count > MaxMemoryEntries)
                    _recentEntries.RemoveAt(0);

                // 持久化到磁盘
                PersistEntry(entry);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MatchStats] 采集失败: {ex.Message}");
            }
        }

        /// <summary>构建单场对局日志条目</summary>
        private static MatchLogEntry BuildEntry(OnlineMatchController ctrl)
        {
            var entry = new MatchLogEntry
            {
                // ── 基础信息 ──
                TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                MapType = ctrl.MapService?.ActiveMapType == OnlineMapService.OnlineMapType.PoliceStation
                    ? "PoliceStation" : "HarbourDistrict",
                DurationSeconds = ctrl.MatchElapsedSeconds,
                DurationFormatted = FormatMatchTime(ctrl.MatchElapsedSeconds),

                // ── 结果 ──
                ResultText = ctrl.Status ?? "",
                WinningFaction = ParseWinningFaction(ctrl.Status ?? ""),
            };

            // ── 玩家统计 ──
            var allPlayers = ctrl.Players;
            entry.TotalPlayers = allPlayers.Count;
            entry.HumanPlayers = allPlayers.Count(kv => !kv.Value.IsBot);
            entry.BotPlayers = allPlayers.Count(kv => kv.Value.IsBot);
            entry.AliveAtEnd = ctrl.AlivePlayerCount;

            // ── 阵营/角色/职业分布 ──
            int gangCount = 0, policeCount = 0, undercoverCount = 0, moleCount = 0;
            var professionCounts = new Dictionary<string, int>();

            foreach (var kv in allPlayers)
            {
                OnlineRole role = ctrl.GetPrivateRole(kv.Key);
                OnlineProfession prof = kv.Value.Profession;

                switch (role)
                {
                    case OnlineRole.Gang: gangCount++; break;
                    case OnlineRole.Police: policeCount++; break;
                    case OnlineRole.Undercover: undercoverCount++; break;
                    case OnlineRole.Mole: moleCount++; break;
                }

                string profKey = prof.ToString();
                professionCounts[profKey] = GetValueOrDefault(professionCounts, profKey, 0) + 1;
            }

            entry.GangCount = gangCount;
            entry.PoliceCount = policeCount;
            entry.UndercoverCount = undercoverCount;
            entry.MoleCount = moleCount;
            entry.ProfessionDistribution = professionCounts
                .Select(kv => $"{kv.Key}:{kv.Value}")
                .ToArray();

            // ── 存活阵营统计（结束时刻） ──
            int aliveGang = 0, alivePolice = 0, aliveUndercover = 0, aliveMole = 0;
            foreach (var kv in allPlayers)
            {
                if (!kv.Value.Alive) continue;
                switch (ctrl.GetPrivateRole(kv.Key))
                {
                    case OnlineRole.Gang: aliveGang++; break;
                    case OnlineRole.Police: alivePolice++; break;
                    case OnlineRole.Undercover: aliveUndercover++; break;
                    case OnlineRole.Mole: aliveMole++; break;
                }
            }
            entry.AliveGang = aliveGang;
            entry.AlivePolice = alivePolice;
            entry.AliveUndercover = aliveUndercover;
            entry.AliveMole = aliveMole;

            // ── 任务统计 ──
            var tasks = ctrl.Tasks;
            if (tasks != null)
            {
                entry.TotalTasks = tasks.Count;
                entry.CompletedTasks = tasks.Count(t => t.Completed);
                entry.SabotagedTasks = tasks.Count(t => t.Sabotaged);
            }

            // ── 证据统计 ──
            entry.EvidenceScore = ctrl.EvidenceScore;
            entry.EvidenceTarget = ctrl.TaskService?.EvidenceTarget ?? 0;

            // ── 尸体 ──
            entry.BodyCount = ctrl.BodyCount;

            // ── Bot 贡献 ──
            if (ctrl.BotController != null)
            {
                entry.BotCompletedTasks = ctrl.BotController.CompletedTaskCount;
            }

            // ── 会议 & 击杀统计 ──
            entry.MeetingCount = ctrl.MeetingCount;
            entry.KillCount = ctrl.KillCount;

            return entry;
        }

        /// <summary>将单条日志写入磁盘 JSON 文件</summary>
        private static void PersistEntry(MatchLogEntry entry)
        {
            try
            {
                EnsureLogDirectory();
                string shortId = Guid.NewGuid().ToString("N").Substring(0, 6);
                string fileName = $"{LogFilePrefix}{DateTime.UtcNow:yyyyMMdd_HHmmss}_{shortId}.json";
                string filePath = Path.Combine(LogDirectory, fileName);
                string json = JsonUtility.ToJson(entry, prettyPrint: true);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MatchStats] 持久化失败: {ex.Message}");
            }
        }

        /// <summary>从磁盘读取所有历史对局日志</summary>
        public static List<MatchLogEntry> LoadAllLogs()
        {
            var logs = new List<MatchLogEntry>();
            try
            {
                if (!Directory.Exists(LogDirectory)) return logs;

                foreach (string file in Directory.GetFiles(LogDirectory, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var entry = JsonUtility.FromJson<MatchLogEntry>(json);
                        if (entry != null) logs.Add(entry);
                    }
                    catch { /* 跳过损坏文件 */ }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MatchStats] 读取历史日志失败: {ex.Message}");
            }
            return logs;
        }

        /// <summary>清除内存中的近期记录（不影响磁盘文件）</summary>
        public void ClearMemory()
        {
            _recentEntries.Clear();
        }

        // ── 工具方法 ──

        /// <summary>从结果文本解析获胜阵营</summary>
        private static string ParseWinningFaction(string resultText)
        {
            if (string.IsNullOrEmpty(resultText)) return "Unknown";
            if (resultText.Contains("警方")) return "Police";
            if (resultText.Contains("黑帮")) return "Gang";
            if (resultText.Contains("卧底")) return "Undercover";
            return "Unknown";
        }

        private static string FormatMatchTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{(totalSeconds / 60):00}:{(totalSeconds % 60):00}";
        }

        /// <summary>Dictionary 安全取值</summary>
        private static TValue GetValueOrDefault<TKey, TValue>(
            Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            return dict.TryGetValue(key, out TValue value) ? value : defaultValue;
        }
    }

    /// <summary>
    /// M8.4：单场对局日志条目。
    /// 包含完整的对局统计数据，可序列化为 JSON 供外部分析工具使用。
    /// </summary>
    [Serializable]
    public class MatchLogEntry
    {
        // ── 基础 ──
        public string TimestampUtc;
        public string MapType;              // "HarbourDistrict" / "PoliceStation"
        public float DurationSeconds;
        public string DurationFormatted;    // "MM:SS"

        // ── 结果 ──
        public string ResultText;
        public string WinningFaction;       // "Police" / "Gang" / "Undercover" / "Unknown"

        // ── 玩家 ──
        public int TotalPlayers;
        public int HumanPlayers;
        public int BotPlayers;
        public int AliveAtEnd;

        // ── 阵营分布（开局） ──
        public int GangCount;
        public int PoliceCount;
        public int UndercoverCount;
        public int MoleCount;

        // ── 存活阵营（结束） ──
        public int AliveGang;
        public int AlivePolice;
        public int AliveUndercover;
        public int AliveMole;

        // ── 职业分布 ──
        public string[] ProfessionDistribution; // e.g. ["Inspector:2", "Enforcer:1", ...]

        // ── 任务 ──
        public int TotalTasks;
        public int CompletedTasks;
        public int SabotagedTasks;

        // ── 证据 ──
        public int EvidenceScore;
        public int EvidenceTarget;

        // ── 尸体 ──
        public int BodyCount;

        // ── Bot ──
        public int BotCompletedTasks;

        // ── 会议 & 击杀 ──
        // Populated from OnlineMatchController at SetResult time.
        public int MeetingCount;
        public int KillCount;

        /// <summary>任务完成率</summary>
        public float TaskCompletionRate =>
            TotalTasks > 0 ? (float)CompletedTasks / TotalTasks : 0f;

        /// <summary>破坏率</summary>
        public float SabotageRate =>
            TotalTasks > 0 ? (float)SabotagedTasks / TotalTasks : 0f;

        /// <summary>证据进度</summary>
        public float EvidenceProgress =>
            EvidenceTarget > 0 ? (float)EvidenceScore / EvidenceTarget : 0f;

        /// <summary>存活率</summary>
        public float SurvivalRate =>
            TotalPlayers > 0 ? (float)AliveAtEnd / TotalPlayers : 0f;

        /// <summary>生成人类可读的一行摘要</summary>
        public string SummaryLine =>
            $"[{TimestampUtc}] {MapType} | {WinningFaction}胜 | {DurationFormatted} | " +
            $"{HumanPlayers}人+{BotPlayers}Bot | 任务{CompletedTasks}/{TotalTasks} | " +
            $"证据{EvidenceScore}/{EvidenceTarget} | 存活{AliveAtEnd}/{TotalPlayers}";
    }
}
