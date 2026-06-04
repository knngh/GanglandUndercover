using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GanglandUndercover.Online;

namespace GanglandUndercover.Editor.Tools
{
    /// <summary>
    /// M8.4.3 — Unity Editor 对局日志查看器。
    /// 菜单位置：Tools → Gangland → Match Stats Viewer
    ///
    /// 功能：
    ///   1. 读取 Application.persistentDataPath/match_logs/ 下所有 JSON 日志
    ///   2. 汇总统计（总场次/平均时长/各阵营胜率/地图分布）
    ///   3. 表格化展示每场对局关键数据
    ///   4. 一键导出汇总 CSV 到桌面
    ///   5. 一键清空日志（带确认弹窗）
    /// </summary>
    public class MatchStatsWindow : EditorWindow
    {
        private List<MatchLogEntry> _logs = new List<MatchLogEntry>();
        private Vector2 _scrollPos;
        private bool _showRawJson;
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "汇总", "对局列表", "平衡建议" };

        // 汇总缓存
        private int _totalGames;
        private float _avgDurationSec;
        private Dictionary<string, int> _winCountByFaction = new Dictionary<string, int>();
        private Dictionary<string, int> _mapCount = new Dictionary<string, int>();
        private Dictionary<string, float> _avgDurationByMap = new Dictionary<string, float>();

        [MenuItem("Tools/Gangland/Match Stats Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<MatchStatsWindow>("对局数据统计");
            window.minSize = new Vector2(720, 500);
        }

        private void OnEnable() => RefreshLogs();

        private void OnGUI()
        {
            DrawToolbar();
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(28));

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawSummaryTab(); break;
                case 1: DrawLogListTab(); break;
                case 2: DrawTuningTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton, GUILayout.Width(80)))
                RefreshLogs();

            GUILayout.Space(10);

            if (GUILayout.Button("📂 打开日志目录", EditorStyles.toolbarButton, GUILayout.Width(120)))
                OpenLogDirectory();

            GUILayout.Space(10);

            if (GUILayout.Button("🗑 清空日志", EditorStyles.toolbarButton, GUILayout.Width(80)))
                ClearLogs();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("📊 导出 CSV", EditorStyles.toolbarButton, GUILayout.Width(100)))
                ExportCsv();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void RefreshLogs()
        {
            _logs = MatchStatsCollector.LoadAllLogs();
            RecalcSummary();
        }

        private void RecalcSummary()
        {
            _totalGames = _logs.Count;
            _avgDurationSec = _totalGames > 0 ? _logs.Average(e => e.DurationSeconds) : 0f;

            _winCountByFaction = new Dictionary<string, int>();
            _mapCount = new Dictionary<string, int>();
            _avgDurationByMap = new Dictionary<string, float>();

            foreach (var e in _logs)
            {
                // 胜率
                string winKey = string.IsNullOrEmpty(e.WinningFaction) ? "Unknown" : e.WinningFaction;
                _winCountByFaction[winKey] = _winCountByFaction.GetValueOrDefault(winKey, 0) + 1;

                // 地图分布
                string mapKey = string.IsNullOrEmpty(e.MapType) ? "Unknown" : e.MapType;
                _mapCount[mapKey] = _mapCount.GetValueOrDefault(mapKey, 0) + 1;
            }

            // 按地图平均时长
            var mapGroups = _logs.GroupBy(e => string.IsNullOrEmpty(e.MapType) ? "Unknown" : e.MapType);
            foreach (var g in mapGroups)
                _avgDurationByMap[g.Key] = g.Average(e => e.DurationSeconds);
        }

        // ─── Tab 1：汇总 ────────────────────────────────────────────────

        private void DrawSummaryTab()
        {
            EditorGUILayout.LabelField("📈 对局汇总", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatBox("总场次", _totalGames.ToString(), new Color(0.2f, 0.6f, 0.9f));
                DrawStatBox("平均时长", FormatTime(_avgDurationSec), new Color(0.9f, 0.6f, 0.2f));
                DrawStatBox("人类玩家局均", _logs.Count > 0 ? _logs.Average(e => e.HumanPlayers).ToString("F1") : "-", new Color(0.3f, 0.8f, 0.4f));
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🏆 阵营胜率", EditorStyles.boldLabel);

            foreach (var kv in _winCountByFaction)
            {
                float pct = _totalGames > 0 ? (float)kv.Value / _totalGames * 100f : 0f;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  {FactionLabel(kv.Key)}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{kv.Value} 胜 ({pct:F1}%)", GUILayout.Width(120));
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(18)), pct / 100f, "");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🗺 地图分布 & 平均时长", EditorStyles.boldLabel);

            foreach (var kv in _mapCount)
            {
                float avg = _avgDurationByMap.GetValueOrDefault(kv.Key, 0f);
                EditorGUILayout.LabelField($"  {kv.Key}：{kv.Value} 场，平均 {FormatTime(avg)}", EditorStyles.label);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("📋 原始数据（最近 5 场）", EditorStyles.boldLabel);
            int start = Mathf.Max(0, _logs.Count - 5);
            for (int i = start; i < _logs.Count; i++)
            {
                EditorGUILayout.LabelField($"  {_logs[i].SummaryLine}");
            }
        }

        private void DrawStatBox(string label, string value, Color color)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.color = color;
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(value, EditorStyles.largeLabel);
                GUI.color = Color.white;
            }
            EditorGUILayout.Space(6);
        }

        // ─── Tab 2：对局列表 ────────────────────────────────────────────

        private void DrawLogListTab()
        {
            EditorGUILayout.LabelField("📋 所有对局记录", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // 表头
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("时间", EditorStyles.miniBoldLabel, GUILayout.Width(140));
                EditorGUILayout.LabelField("地图", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField("胜方", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField("时长", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("人数", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField("任务率", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("证据", EditorStyles.miniBoldLabel, GUILayout.Width(70));
            }

            EditorGUILayout.Space(2);

            foreach (var entry in _logs.OrderByDescending(e => e.TimestampUtc))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(entry.TimestampUtc, GUILayout.Width(140));
                    EditorGUILayout.LabelField(entry.MapType, GUILayout.Width(100));
                    EditorGUILayout.LabelField(FactionLabel(entry.WinningFaction), GUILayout.Width(70));
                    EditorGUILayout.LabelField(entry.DurationFormatted, GUILayout.Width(60));
                    EditorGUILayout.LabelField($"{entry.HumanPlayers}+{entry.BotPlayers}", GUILayout.Width(50));
                    EditorGUILayout.LabelField($"{entry.TaskCompletionRate * 100:F0}%", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"{entry.EvidenceScore}/{entry.EvidenceTarget}", GUILayout.Width(70));
                }
            }
        }

        // ─── Tab 3：平衡建议 ────────────────────────────────────────────

        private void DrawTuningTab()
        {
            EditorGUILayout.LabelField("⚖️ 平衡调参建议（基于现有数据）", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_totalGames < 3)
            {
                EditorGUILayout.HelpBox("至少需要 3 场对局数据才能生成可靠的调参建议。\n请多进行几场测试对局后再查看此页。", MessageType.Info);
                return;
            }

            // 时长分析
            float avgMin = _avgDurationSec / 60f;
            EditorGUILayout.LabelField("⏱ 时长分析", EditorStyles.boldLabel);
            if (avgMin < 8f)
                EditorGUILayout.HelpBox($"⚠️ 平均时长偏短（{avgMin:F1} 分钟）。建议：\n  • 调高 KillCooldownSeconds\n  • 调高 MeetingCooldownSeconds\n  • 增加任务总数或难度", MessageType.Warning);
            else if (avgMin > 18f)
                EditorGUILayout.HelpBox($"⚠️ 平均时长偏长（{avgMin:F1} 分钟）。建议：\n  • 调低 KillCooldownSeconds\n  • 降低 EvidenceTarget\n  • 增加 Bot 任务完成速度", MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"✅ 平均时长 {avgMin:F1} 分钟，在目标区间（8-15 分钟）内。", MessageType.Info);

            EditorGUILayout.Space(6);

            // 胜率分析
            EditorGUILayout.LabelField("⚖️ 胜率分析", EditorStyles.boldLabel);
            foreach (var kv in _winCountByFaction)
            {
                float pct = _totalGames > 0 ? (float)kv.Value / _totalGames * 100f : 0f;
                string faction = kv.Key;

                if (pct < 35f)
                    EditorGUILayout.HelpBox($"⚠️ {FactionLabel(faction)} 胜率偏低（{pct:F1}%）。建议增强该阵营：\n  • 检查职业技能平衡\n  • 降低关键冷却时间", MessageType.Warning);
                else if (pct > 65f)
                    EditorGUILayout.HelpBox($"⚠️ {FactionLabel(faction)} 胜率偏高（{pct:F1}%）。建议削弱该阵营：\n  • 增加任务难度\n  • 提高击杀冷却", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox($"✅ {FactionLabel(faction)} 胜率 {pct:F1}%，平衡良好。", MessageType.Info);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("📝 调参参考（OnlineRuleSet 字段）", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(
                "KillCooldownSeconds      → 黑帮击杀冷却（默认 30）\n" +
                "MeetingCooldownSeconds   → 紧急会议冷却（默认 0）\n" +
                "EvidenceTarget           → 证据目标值（默认 10）\n" +
                "MatchTargetMinSeconds    → 目标最短时长（默认 600）\n" +
                "MatchHardLimitSeconds    → 硬限时（默认 1200）\n" +
                "BotThinkMinSeconds       → Bot 决策频率（默认 1.2）\n" +
                "BotTaskSpeedMultiplier   → Bot 任务速度倍率（默认 1.0）\n",
                EditorStyles.helpBox, GUILayout.Height(130));
        }

        // ─── 工具方法 ──────────────────────────────────────────────────

        private void OpenLogDirectory()
        {
            string dir = MatchStatsCollector.LogDirectory;
            if (Directory.Exists(dir))
                EditorUtility.RevealInFinder(dir);
            else
                EditorUtility.DisplayDialog("提示", "日志目录尚不存在，先进行至少一场对局。", "确定");
        }

        private void ClearLogs()
        {
            if (!EditorUtility.DisplayDialog("确认", "确定要删除所有对局日志吗？此操作不可撤销。", "确定", "取消"))
                return;

            string dir = MatchStatsCollector.LogDirectory;
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                    File.Delete(file);
            }
            RefreshLogs();
        }

        private void ExportCsv()
        {
            if (_logs.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有可导出的数据。", "确定");
                return;
            }

            string path = EditorUtility.SaveFilePanel("导出 CSV", "~/Desktop", "gangland_match_stats.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("时间,地图,胜方,时长(秒),人数(人+Bot),任务完成率,证据进度,会议次数,击杀次数");
                foreach (var e in _logs.OrderBy(e => e.TimestampUtc))
                {
                    sw.WriteLine($"{e.TimestampUtc},{e.MapType},{e.WinningFaction},{e.DurationSeconds}," +
                                 $"{e.HumanPlayers}+{e.BotPlayers},{e.TaskCompletionRate},{e.EvidenceProgress}," +
                                 $"{e.MeetingCount},{e.KillCount}");
                }
            }

            EditorUtility.DisplayDialog("完成", $"已导出 {_logs.Count} 条记录到：\n{path}", "确定");
            EditorUtility.RevealInFinder(path);
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static string FactionLabel(string faction)
        {
            return faction switch
            {
                "Police" => "警方",
                "Gang" => "黑帮",
                "Undercover" => "卧底",
                _ => faction
            };
        }
    }
}
