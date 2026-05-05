using System;
using UnityEngine;

namespace GanglandUndercover.Online
{
    [Serializable]
    public readonly struct VerticalSliceStageOneAnchorSpec
    {
        public VerticalSliceStageOneAnchorSpec(string id, string category, string description, Vector3 designPosition, Vector2 footprint, Color debugColor)
        {
            Id = id;
            Category = category;
            Description = description;
            DesignPosition = designPosition;
            Footprint = footprint;
            DebugColor = debugColor;
        }

        public readonly string Id;
        public readonly string Category;
        public readonly string Description;
        public readonly Vector3 DesignPosition;
        public readonly Vector2 Footprint;
        public readonly Color DebugColor;
    }

    public sealed class VerticalSliceStageOneAnchor : MonoBehaviour
    {
        [SerializeField] private string anchorId = string.Empty;
        [SerializeField] private string category = string.Empty;
        [SerializeField] private string description = string.Empty;
        [SerializeField] private Vector3 designPosition;
        [SerializeField] private Vector2 footprint = Vector2.one;
        [SerializeField] private Color debugColor = Color.white;

        public string AnchorId => anchorId;
        public string Category => category;
        public string Description => description;
        public Vector3 DesignPosition => designPosition;
        public Vector2 Footprint => footprint;
        public Color DebugColor => debugColor;

        public void Configure(VerticalSliceStageOneAnchorSpec spec)
        {
            anchorId = spec.Id;
            category = spec.Category;
            description = spec.Description;
            designPosition = spec.DesignPosition;
            footprint = spec.Footprint;
            debugColor = spec.DebugColor;
        }
    }

    public static class VerticalSliceStageOneAnchorCatalog
    {
        public static readonly VerticalSliceStageOneAnchorSpec[] Specs =
        {
            new VerticalSliceStageOneAnchorSpec("OpeningFirstScreen", "Camera", "开局第一屏：集合点、警车、茶餐厅和夜市入口同时入镜。", new Vector3(-1.18f, -0.72f, 0.18f), new Vector2(5.8f, 3.2f), new Color(0.08f, 0.72f, 0.92f, 1f)),
            new VerticalSliceStageOneAnchorSpec("ActionFollow", "Camera", "行动阶段近景跟随，验证人物比例、门框高度和任务可读性。", new Vector3(-0.62f, -1.02f, 0.18f), new Vector2(4.2f, 2.6f), new Color(0.92f, 0.72f, 0.12f, 1f)),
            new VerticalSliceStageOneAnchorSpec("BlackoutRoute", "Camera", "断电阶段镜头，压低视野并突出应急灯路线。", new Vector3(8.72f, 5.18f, 0.22f), new Vector2(3.4f, 2.2f), new Color(0.86f, 0.12f, 0.08f, 1f)),
            new VerticalSliceStageOneAnchorSpec("MeetingTable", "Camera", "会议阶段镜头，圆桌、语音席位、证据墙和投票席都能被看见。", new Vector3(-1.18f, -0.72f, 0.2f), new Vector2(3.0f, 2.0f), new Color(0.48f, 0.9f, 0.82f, 1f)),
            new VerticalSliceStageOneAnchorSpec("ResultStage", "Camera", "结算阶段镜头，保留警方/黑帮投影、证据时间线和重开入口。", new Vector3(0f, -0.35f, 0.22f), new Vector2(3.4f, 2.4f), new Color(0.72f, 0.42f, 0.95f, 1f)),

            new VerticalSliceStageOneAnchorSpec("EntranceCCTV", "Room", "监控室入口：CCTV 任务和报案线索入口。", new Vector3(-8.82f, 0.96f, 0.12f), new Vector2(1.4f, 0.5f), new Color(0.08f, 0.72f, 0.92f, 1f)),
            new VerticalSliceStageOneAnchorSpec("EntranceCafe", "Room", "茶餐厅入口：线人录音与玩家遭遇点。", new Vector3(-4.54f, 0.88f, 0.12f), new Vector2(1.4f, 0.5f), new Color(0.95f, 0.46f, 0.12f, 1f)),
            new VerticalSliceStageOneAnchorSpec("EntranceMarket", "Room", "夜市入口：暗号任务和人群遮挡测试。", new Vector3(-0.72f, 2.02f, 0.12f), new Vector2(1.7f, 0.5f), new Color(0.92f, 0.2f, 0.42f, 1f)),
            new VerticalSliceStageOneAnchorSpec("EntranceAlley", "Room", "后巷入口：黑帮短线、击倒和尸体报案路径。", new Vector3(4.92f, -0.82f, 0.12f), new Vector2(1.3f, 0.5f), new Color(0.9f, 0.68f, 0.14f, 1f)),
            new VerticalSliceStageOneAnchorSpec("EntrancePower", "Room", "电房入口：断电破坏、修复任务和应急灯路线。", new Vector3(7.86f, 4.02f, 0.12f), new Vector2(1.3f, 0.5f), new Color(0.86f, 0.18f, 0.12f, 1f)),
            new VerticalSliceStageOneAnchorSpec("EntranceMeeting", "Room", "集合点入口：紧急会议和投票返回行动的衔接。", new Vector3(-1.18f, -1.58f, 0.12f), new Vector2(1.5f, 0.5f), new Color(0.12f, 0.78f, 0.76f, 1f)),

            new VerticalSliceStageOneAnchorSpec("TaskCCTV", "Task", "监控调取任务：多屏、时间线、键盘三步交互。", new Vector3(-9.45f, 2.12f, 0.18f), new Vector2(1.25f, 0.82f), new Color(0.08f, 0.72f, 0.92f, 1f)),
            new VerticalSliceStageOneAnchorSpec("TaskRecorder", "Task", "线人录音任务：录音机、音轨条、桌椅场景。", new Vector3(-4.8f, 1.32f, 0.18f), new Vector2(1.2f, 0.8f), new Color(0.92f, 0.46f, 0.12f, 1f)),
            new VerticalSliceStageOneAnchorSpec("TaskBreaker", "Task", "电闸修复任务：断电破坏后的主要修复点。", new Vector3(8.72f, 5.18f, 0.18f), new Vector2(1.25f, 0.85f), new Color(0.96f, 0.76f, 0.1f, 1f)),
            new VerticalSliceStageOneAnchorSpec("TaskPlate", "Task", "车牌追踪任务：警车、出租车、车牌卡片三步验证。", new Vector3(1.74f, -3.62f, 0.18f), new Vector2(1.25f, 0.85f), new Color(0.28f, 0.66f, 0.92f, 1f)),

            new VerticalSliceStageOneAnchorSpec("KillSightlineA", "Gameplay", "击倒视线 A：短视野遮挡后的报案距离验证。", new Vector3(3.62f, 0.36f, 0.22f), new Vector2(1.3f, 0.9f), new Color(0.86f, 0.08f, 0.06f, 1f)),
            new VerticalSliceStageOneAnchorSpec("KillSightlineB", "Gameplay", "击倒视线 B：后巷与主路的转角遮挡。", new Vector3(5.95f, -1.82f, 0.22f), new Vector2(1.2f, 0.8f), new Color(0.86f, 0.08f, 0.06f, 1f)),
            new VerticalSliceStageOneAnchorSpec("ReportPath", "Gameplay", "尸体报案路径：从后巷返回集合点的可读路线。", new Vector3(2.18f, -3.18f, 0.14f), new Vector2(2.2f, 0.8f), new Color(0.92f, 0.72f, 0.12f, 1f)),
            new VerticalSliceStageOneAnchorSpec("VoiceBubble", "Gameplay", "行动语音范围：近距离语音、会议全员语音的切换参考。", new Vector3(-0.78f, 0.86f, 0.16f), new Vector2(2.4f, 1.5f), new Color(0.12f, 0.78f, 0.66f, 1f)),
            new VerticalSliceStageOneAnchorSpec("UnderworldGate", "Gameplay", "黑帮暗线口：黑帮快速转移与警方推理反制。", new Vector3(4.9f, -0.32f, 0.08f), new Vector2(1.1f, 0.8f), new Color(0.72f, 0.22f, 0.86f, 1f)),
            new VerticalSliceStageOneAnchorSpec("PatrolLoop", "Gameplay", "AI 巡逻回路：10-20 分钟局时内的移动密度参考。", new Vector3(-5.35f, 1.95f, 0.1f), new Vector2(3.0f, 1.4f), new Color(0.08f, 0.32f, 0.95f, 1f)),
            new VerticalSliceStageOneAnchorSpec("EmergencyButton", "Gameplay", "紧急铃：会议触发、冷却和视觉焦点参考。", new Vector3(0f, 0f, 0.12f), new Vector2(1.3f, 1.0f), new Color(0.9f, 0.08f, 0.06f, 1f))
        };
    }
}
