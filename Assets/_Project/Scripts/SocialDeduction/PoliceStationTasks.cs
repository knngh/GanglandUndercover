using UnityEngine;
using GanglandUndercover.SocialDeduction.MiniGames;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 警察局专属任务定义。
    /// 整理档案 / 调取监控 / 武器清点 / 审讯记录 复用已有 MiniGame；
    /// 证据归档为新增拖拽任务。
    /// </summary>
    public static class PoliceStationTasks
    {
        // ─── 任务名 → 区域 映射 ──────────────────────────
        public enum TaskArea
        {
            Lobby,        // 整理档案（SortTask）
            Briefing,     // 调取监控（ScanTask）
            Armory,       // 武器清点（TapTask）
            Interrogation,// 审讯记录（KeypadTask）
            Evidence,     // 证据归档（新任务）
        }

        public static string GetTaskName(TaskArea area) => area switch
        {
            TaskArea.Lobby        => "整理档案",
            TaskArea.Briefing     => "调取监控",
            TaskArea.Armory       => "武器清点",
            TaskArea.Interrogation=> "审讯记录",
            TaskArea.Evidence     => "证据归档",
            _                     => "未知任务",
        };

        /// <summary>
        /// 根据任务名返回对应的 MiniGameType（供 SocialPrototypeController.PickMiniGameType 使用）。
        /// </summary>
        public static MiniGameType? GetMiniGameType(string taskName)
        {
            return taskName switch
            {
                "整理档案" => MiniGameType.SortTask,
                "调取监控" => MiniGameType.ScanTask,
                "武器清点" => MiniGameType.TapTask,
                "审讯记录" => MiniGameType.KeypadTask,
                "证据归档" => MiniGameType.EvidenceArchiveTask,
                _          => null,
            };
        }

        // ─── 证据归档任务：拖拽证据到对应案件槽 ─────
        // 这是一个新的 MiniGame，需要在 MiniGames 目录下创建 EvidenceArchiveTask.cs
        // 此处仅定义数据结构，具体实现见 EvidenceArchiveTask.cs

        /// <summary>
        /// 证据条目数据。
        /// </summary>
        public sealed class EvidenceItem
        {
            public string ItemName;   // 证据名称，如"血迹报告"
            public string CaseTag;    // 所属案件标签，如"案件A"
            public string DisplayText; // UI 显示文本
        }

        /// <summary>
        /// 案件槽数据。
        /// </summary>
        public sealed class CaseSlot
        {
            public string CaseName;   // 案件名称，如"案件A：码头凶杀"
            public string CaseTag;    // 匹配标签
            public int Capacity;      // 可容纳证据数
            public int CurrentCount;  // 已归档数
        }

        /// <summary>
        /// 生成一组随机证据和案件槽（供 EvidenceArchiveTask 使用）。
        /// </summary>
        public static (EvidenceItem[] items, CaseSlot[] slots) GenerateEvidencePuzzle()
        {
            string[] caseNames = { "案件A：码头凶杀", "案件B：夜市毒品", "案件C：诊所贪污" };
            string[] caseTags  = { "案件A", "案件B", "案件C" };

            EvidenceItem[] items =
            {
                new EvidenceItem { ItemName = "血迹报告",   CaseTag = "案件A", DisplayText = "血迹DNA报告" },
                new EvidenceItem { ItemName = "监控截图",   CaseTag = "案件A", DisplayText = "码头监控截图" },
                new EvidenceItem { ItemName = "口供笔录",   CaseTag = "案件A", DisplayText = "嫌疑人A口供" },
                new EvidenceItem { ItemName = "毒品样本",   CaseTag = "案件B", DisplayText = "缴获毒品样本" },
                new EvidenceItem { ItemName = "交易记录",   CaseTag = "案件B", DisplayText = "暗网交易记录" },
                new EvidenceItem { ItemName = "线人情报",   CaseTag = "案件B", DisplayText = "线人B情报单" },
                new EvidenceItem { ItemName = "账本复印件", CaseTag = "案件C", DisplayText = "诊所账本复印件" },
                new EvidenceItem { ItemName = "汇款记录",   CaseTag = "案件C", DisplayText = "可疑汇款记录" },
            };

            CaseSlot[] slots =
            {
                new CaseSlot { CaseName = caseNames[0], CaseTag = caseTags[0], Capacity = 3, CurrentCount = 0 },
                new CaseSlot { CaseName = caseNames[1], CaseTag = caseTags[1], Capacity = 3, CurrentCount = 0 },
                new CaseSlot { CaseName = caseNames[2], CaseTag = caseTags[2], Capacity = 2, CurrentCount = 0 },
            };

            return (items, slots);
        }
    }
}
