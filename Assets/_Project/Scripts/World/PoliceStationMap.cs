using UnityEngine;

namespace GanglandUndercover.World
{
    /// <summary>
    /// 警察局地图定义 — 第二张地图。
    /// 6 个区域：Lobby / Interrogation / Evidence / Armory / Cells / Briefing
    /// </summary>
    public static class PoliceStationMap
    {
        // ─── 区域枚举 ──────────────────────────────
        public enum Area
        {
            Lobby,        // 大厅
            Interrogation, // 审讯室
            Evidence,      // 证物室
            Armory,        // 武器库
            Cells,         // 拘留室
            Briefing       // 简报室
        }

        // ─── 区域中文名 ──────────────────────────
        private static readonly string[] AreaNames =
        {
            "大厅",
            "审讯室",
            "证物室",
            "武器库",
            "拘留室",
            "简报室"
        };

        public static string GetAreaName(Area area) => AreaNames[(int)area];

        // ─── 区域中心坐标（世界空间）────────────────
        private static readonly Vector3[] AreaCenters =
        {
            new Vector3(0f,       0f,   0f), // Lobby
            new Vector3(-3.2f,    1.6f, 0f), // Interrogation
            new Vector3(-3.0f,   -1.8f, 0f), // Evidence
            new Vector3(3.1f,    -1.6f, 0f), // Armory
            new Vector3(-0.8f,    2.4f, 0f), // Cells
            new Vector3(2.8f,     1.5f, 0f), // Briefing
        };

        public static Vector3 GetAreaCenter(Area area) => AreaCenters[(int)area];

        // ─── 区域大小 ──────────────────────────────
        private static readonly Vector2[] AreaSizes =
        {
            new Vector2(3.2f, 2.4f), // Lobby
            new Vector2(2.4f, 1.8f), // Interrogation
            new Vector2(2.6f, 1.8f), // Evidence
            new Vector2(2.2f, 1.6f), // Armory
            new Vector2(2.0f, 1.8f), // Cells
            new Vector2(2.4f, 1.8f), // Briefing
        };

        public static Vector2 GetAreaSize(Area area) => AreaSizes[(int)area];

        // ─── 区域颜色 ──────────────────────────────
        private static readonly Color[] AreaColors =
        {
            new Color(0.18f, 0.22f, 0.32f, 1f), // Lobby — 深蓝灰
            new Color(0.28f, 0.18f, 0.18f, 1f), // Interrogation — 暗红棕
            new Color(0.14f, 0.22f, 0.16f, 1f), // Evidence — 暗绿
            new Color(0.22f, 0.20f, 0.14f, 1f), // Armory — 军绿棕
            new Color(0.16f, 0.16f, 0.22f, 1f), // Cells — 深紫灰
            new Color(0.20f, 0.24f, 0.30f, 1f), // Briefing — 钢蓝
        };

        public static Color GetAreaColor(Area area) => AreaColors[(int)area];

        // ─── 邻接表（无向图）─────────────────────
        // Lobby(0)      ↔ Interrogation(1), Evidence(2), Briefing(5)
        // Interrogation(1) ↔ Lobby(0), Cells(4)
        // Evidence(2)    ↔ Lobby(0), Armory(3)
        // Armory(3)      ↔ Evidence(2), Briefing(5)
        // Cells(4)        ↔ Interrogation(1)
        // Briefing(5)    ↔ Lobby(0), Armory(3)
        private static readonly int[][] Adjacency =
        {
            new[] { 1, 2, 5 }, // Lobby
            new[] { 0, 4 },     // Interrogation
            new[] { 0, 3 },     // Evidence
            new[] { 2, 5 },     // Armory
            new[] { 1 },         // Cells
            new[] { 0, 3 },     // Briefing
        };

        public static int[] GetNeighbors(Area area) => Adjacency[(int)area];

        // ─── 任务站位置 ────────────────────────────
        // 每个区域一个任务站
        private static readonly Vector3[] TaskPositions =
        {
            new Vector3(0f,      0.3f, 0f),  // Lobby — 整理档案
            new Vector3(-3.5f,   2.0f, 0f),  // Interrogation — 审讯记录
            new Vector3(-3.3f,  -2.2f, 0f),  // Evidence — 证据归档
            new Vector3(3.5f,   -1.9f, 0f),  // Armory — 武器清点
            new Vector3(-1.1f,   2.8f, 0f),  // Cells — （备用）
            new Vector3(3.1f,    1.9f, 0f),  // Briefing — 调取监控
        };

        public static Vector3 GetTaskPosition(Area area) => TaskPositions[(int)area];

        // ─── 角色初始位置 ─────────────────────────
        private static readonly Vector3[] SpawnPositions =
        {
            new Vector3(0f,      -0.5f, 0f), // 大厅中央
            new Vector3(-2.2f,   1.2f, 0f), // 审讯室附近
            new Vector3(-2.0f,  -1.2f, 0f), // 证物室附近
            new Vector3(2.0f,   -1.0f, 0f), // 武器库附近
            new Vector3(-0.3f,   1.8f, 0f), // 拘留室附近
        };

        public static Vector3 GetSpawnPosition(int index) => SpawnPositions[index % SpawnPositions.Length];

        // ─── 通风管节点（警察局版）────────────────
        // 4 个通风管节点，拓扑：Lobby ↔ Interrogation ↔ Cells, Lobby ↔ Evidence ↔ Armory, Lobby ↔ Briefing
        public static readonly (string name, Vector3 position, int[] connections)[] VentConfigs =
        {
            ("大厅通风管",     new Vector3(0f,     0f,   0f),   new[] { 1, 2, 3 }),
            ("审讯室通风管",   new Vector3(-3.0f,  1.3f, 0f),  new[] { 0, 4 }),
            ("证物室通风管",   new Vector3(-2.7f, -1.3f, 0f),  new[] { 0, 5 }),
            ("武器库通风管",   new Vector3(2.8f,  -1.2f, 0f),  new[] { 2, 3 }),
            // 注：Cells 和 Briefing 的通风管复用节点 1 和 3 的邻接
        };

        // ─── 监控节点 ──────────────────────────────
        private static readonly (string name, Vector3 position, float radius)[] SurveillanceConfigs =
        {
            ("大厅监控",   new Vector3(0f,     0f,   0f),   2.0f),
            ("审讯室监控", new Vector3(-3.2f,  1.6f, 0f),   1.6f),
            ("证物室监控", new Vector3(-3.0f, -1.8f, 0f),   1.8f),
            ("武器库监控", new Vector3(3.1f,   -1.6f, 0f),  1.5f),
        };

        public static (string name, Vector3 position, float radius)[] GetSurveillanceConfigs() => SurveillanceConfigs;

        // ─── 紧急按钮位置 ─────────────────────────
        public static Vector3 EmergencyButtonPosition => new Vector3(0f, -0.8f, 0f);
    }
}
