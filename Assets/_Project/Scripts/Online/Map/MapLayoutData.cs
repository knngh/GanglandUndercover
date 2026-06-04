using System;
using UnityEngine;

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// M6.1 灰盒地图布局数据 — 全图几何与玩法点的单一数据源。
    ///
    /// ScriptableObject，可在 Editor 中直接编辑，Inspector 一目了然。
    /// 替换 OnlineMatchController 中散落各处的硬编码建造参数。
    ///
    /// 坐标系：全部使用「设计坐标」(Design Space)，运行时经 OnlineMapService.ScaleMapPosition 转换为世界坐标。
    /// </summary>
    [CreateAssetMenu(menuName = "Gangland Undercover/Map Layout Data", fileName = "MapLayout_HarbourDistrict")]
    public sealed class MapLayoutData : ScriptableObject
    {
        [Header("地图边界（设计坐标）")]
        [Tooltip("设计坐标系半宽")]
        public float DesignHalfWidth = 12.0f;

        [Tooltip("设计坐标系半高")]
        public float DesignHalfHeight = 7.57f;

        [Header("会议点")]
        public Vector2 MeetingCenter = new Vector2(0f, -0.35f);

        [Range(6, 10)]
        public int MaxMeetingSeats = 10;

        // ── 房间 ──

        [Header("房间定义（12 个港区房间）")]
        public RoomDefinition[] Rooms;

        // ── 走廊 / 通道 ──

        [Header("走廊网络")]
        public CorridorDefinition[] Corridors;

        // ── 任务分配 ──

        [Header("任务点 → 房间分配")]
        public TaskAssignment[] TaskAssignments;

        // ── 暗线 / 通风管 ──

        [Header("暗线（通风管）网络")]
        public VentNodeDefinition[] VentNodes;

        // ── 监控 ──

        [Header("监控摄像头布点")]
        public SurveillanceZoneDefinition[] SurveillanceZones;

        // ── 视线遮挡 ──

        [Header("视线遮挡体")]
        public BlockerVolume[] SightBlockers;

        // ── 出生点 ──

        [Header("玩家出生点")]
        public Vector2[] SpawnPoints;

        // ── 辅助方法 ──

        /// <summary>获取所有任务点位置（设计坐标）</summary>
        public Vector2[] GetAllTaskPositions()
        {
            if (TaskAssignments == null) return Array.Empty<Vector2>();
            Vector2[] positions = new Vector2[TaskAssignments.Length];
            for (int i = 0; i < TaskAssignments.Length; i++)
                positions[i] = TaskAssignments[i].Position;
            return positions;
        }

        /// <summary>获取指定房间包含的任务 ID 列表</summary>
        public int[] GetTaskIdsForRoom(int roomIndex)
        {
            if (TaskAssignments == null) return Array.Empty<int>();
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < TaskAssignments.Length; i++)
            {
                if (TaskAssignments[i].RoomIndex == roomIndex)
                    list.Add(i);
            }
            return list.ToArray();
        }

        /// <summary>获取暗线节点邻接表</summary>
        public int[][] GetVentAdjacencyList()
        {
            if (VentNodes == null) return Array.Empty<int[]>();
            int[][] adjacency = new int[VentNodes.Length][];
            for (int i = 0; i < VentNodes.Length; i++)
                adjacency[i] = VentNodes[i].ConnectedIndices ?? Array.Empty<int>();
            return adjacency;
        }

        /// <summary>检查两个位置间是否有视线遮挡</summary>
        public bool IsLineOfSightBlocked(Vector2 from, Vector2 to)
        {
            if (SightBlockers == null) return false;
            foreach (var blocker in SightBlockers)
            {
                // AABB 线段相交检测
                if (LineIntersectsAABB(from, to, blocker.Center, blocker.Size))
                    return true;
            }
            return false;
        }

        private static bool LineIntersectsAABB(Vector2 a, Vector2 b, Vector2 boxCenter, Vector2 boxSize)
        {
            Vector2 half = boxSize * 0.5f;
            Vector2 min = boxCenter - half;
            Vector2 max = boxCenter + half;

            // Cohen-Sutherland 线段裁剪
            float dx = b.x - a.x;
            float dy = b.y - a.y;

            float[] p = { -dx, dx, -dy, dy };
            float[] q = { a.x - min.x, max.x - a.x, a.y - min.y, max.y - a.y };

            float u1 = 0f, u2 = 1f;
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-6f)
                {
                    if (q[i] < 0f) return false;
                }
                else
                {
                    float t = q[i] / p[i];
                    if (p[i] < 0f)
                    {
                        if (t > u2) return false;
                        if (t > u1) u1 = t;
                    }
                    else
                    {
                        if (t < u1) return false;
                        if (t < u2) u2 = t;
                    }
                }
            }
            return u1 <= u2;
        }
    }

    // ══════════════════════════════════════════════════════
    // 可序列化子结构
    // ══════════════════════════════════════════════════════

    /// <summary>房间定义（设计坐标）</summary>
    [Serializable]
    public struct RoomDefinition
    {
        [Tooltip("中文名，如「西码头货柜场」")]
        public string Name;

        [Tooltip("简称，如「货柜舱」")]
        public string Label;

        [Tooltip("房间中心（设计坐标）")]
        public Vector2 Center;

        [Tooltip("房间尺寸（设计坐标），z=深度")]
        public Vector3 Size;

        [Tooltip("地板颜色")]
        public Color FloorColor;

        [Tooltip("入口方向")]
        public OnlineMapService.MapEntrance Entrance;
    }

    /// <summary>走廊定义（设计坐标）</summary>
    [Serializable]
    public struct CorridorDefinition
    {
        [Tooltip("走廊名称")]
        public string Name;

        [Tooltip("走廊中心（设计坐标）")]
        public Vector2 Center;

        [Tooltip("走廊尺寸（设计坐标）")]
        public Vector2 Size;

        [Tooltip("是否为可步行区域")]
        public bool Walkable;

        [Tooltip("是否为圆形节点")]
        public bool IsRoundNode;

        [Tooltip("节点半径（仅 IsRoundNode=true 时有效）")]
        public float NodeRadius;
    }

    /// <summary>任务分配：任务 ID → 房间索引 + 位置</summary>
    [Serializable]
    public struct TaskAssignment
    {
        [Tooltip("任务 ID (0-27)")]
        public int TaskId;

        [Tooltip("所属房间索引")]
        public int RoomIndex;

        [Tooltip("任务交互点（设计坐标）")]
        public Vector2 Position;
    }

    /// <summary>暗线节点定义</summary>
    [Serializable]
    public struct VentNodeDefinition
    {
        [Tooltip("节点名称")]
        public string Name;

        [Tooltip("节点位置（设计坐标）")]
        public Vector2 Position;

        [Tooltip("邻接节点索引数组")]
        public int[] ConnectedIndices;
    }

    /// <summary>监控摄像头布点</summary>
    [Serializable]
    public struct SurveillanceZoneDefinition
    {
        [Tooltip("摄像头标签")]
        public string Label;

        [Tooltip("监控区中心（设计坐标）")]
        public Vector2 Center;

        [Tooltip("监控区尺寸（设计坐标）")]
        public Vector2 Size;

        [Tooltip("所属房间索引（-1=走廊/公共区域）")]
        public int RoomIndex;
    }

    /// <summary>视线遮挡体（设计坐标）</summary>
    [Serializable]
    public struct BlockerVolume
    {
        [Tooltip("遮挡体名称")]
        public string Name;

        [Tooltip("遮挡体中心（设计坐标）")]
        public Vector2 Center;

        [Tooltip("遮挡体尺寸（设计坐标）")]
        public Vector2 Size;
    }
}
