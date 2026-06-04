using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GanglandUndercover.Online.Map
{
    /// <summary>
    /// M6.1 地图验证器 — 编辑器工具，验证灰盒地图的玩法可达性和平衡性。
    ///
    /// 使用方式：在 Editor 中选中 MapLayoutData 资产，调用静态方法。
    /// </summary>
    public static class MapValidator
    {
        /// <summary>
        /// 验证结果结构。
        /// </summary>
        public struct ValidationResult
        {
            public bool AllPassed;
            public int Warnings;
            public int Errors;
            public List<string> Messages;
        }

        // ── 网格搜索步长（设计坐标单位）──
        private const float GridStep = 0.3f;
        private const float MaxDistBetweenSpawnAndTask = 30f; // 最大步行距离

        /// <summary>
        /// 完整验证：可达性 + 覆盖 + 平衡。
        /// </summary>
        public static ValidationResult ValidateFull(MapLayoutData layout)
        {
            var result = new ValidationResult
            {
                AllPassed = true,
                Messages = new List<string>()
            };

            ValidateReachability(layout, ref result);
            ValidateTaskDistribution(layout, ref result);
            ValidateVentConnectivity(layout, ref result);
            ValidateSurveillanceCoverage(layout, ref result);
            ValidateSightBlockers(layout, ref result);
            ValidateRoomConnectivity(layout, ref result);

            result.AllPassed = result.Errors == 0;
            return result;
        }

        /// <summary>
        /// 验证所有任务点对于所有出生点的可达性（BFS 网格搜索）。
        /// </summary>
        public static void ValidateReachability(MapLayoutData layout, ref ValidationResult result)
        {
            if (layout.SpawnPoints == null || layout.SpawnPoints.Length == 0)
            {
                result.Errors++;
                result.Messages.Add("[ERROR] 没有定义出生点。");
                return;
            }

            if (layout.TaskAssignments == null || layout.TaskAssignments.Length == 0)
            {
                result.Warnings++;
                result.Messages.Add("[WARN] 没有定义任务点。");
                return;
            }

            // 构建障碍物格网
            HashSet<Vector2Int> blocked = BuildBlockedGrid(layout);

            int unreachableCount = 0;
            foreach (var task in layout.TaskAssignments)
            {
                bool anyReachable = false;
                Vector2Int taskCell = ToGrid(task.Position);

                foreach (var spawn in layout.SpawnPoints)
                {
                    Vector2Int startCell = ToGrid(spawn);
                    if (BfsReachable(startCell, taskCell, blocked, Mathf.CeilToInt(MaxDistBetweenSpawnAndTask / GridStep)))
                    {
                        anyReachable = true;
                        break;
                    }
                }

                if (!anyReachable)
                {
                    unreachableCount++;
                    result.Errors++;
                    result.Messages.Add($"[ERROR] 任务 {task.TaskId} ({task.Position}) 从任何出生点都不可达。");
                }
            }

            if (unreachableCount == 0)
            {
                result.Messages.Add($"[OK] 全部 {layout.TaskAssignments.Length} 个任务点可达。");
            }
        }

        /// <summary>
        /// 验证任务在各房间的分布是否均匀。
        /// </summary>
        public static void ValidateTaskDistribution(MapLayoutData layout, ref ValidationResult result)
        {
            if (layout.Rooms == null || layout.TaskAssignments == null) return;

            Dictionary<int, int> roomTaskCount = new Dictionary<int, int>();
            for (int i = 0; i < layout.Rooms.Length; i++)
                roomTaskCount[i] = 0;

            foreach (var task in layout.TaskAssignments)
            {
                if (task.RoomIndex >= 0 && task.RoomIndex < layout.Rooms.Length)
                    roomTaskCount[task.RoomIndex]++;
                else
                {
                    result.Warnings++;
                    result.Messages.Add($"[WARN] 任务 {task.TaskId} 的 RoomIndex={task.RoomIndex} 越界。");
                }
            }

            int min = roomTaskCount.Values.Min();
            int max = roomTaskCount.Values.Max();

            if (max - min > 3)
            {
                result.Warnings++;
                result.Messages.Add($"[WARN] 任务分布不均：最少{min}个/房间，最多{max}个/房间。");
            }
            else
            {
                result.Messages.Add($"[OK] 任务分布均匀：{min}~{max}个/房间。");
            }
        }

        /// <summary>
        /// 验证暗线节点邻接表的连通性。
        /// </summary>
        public static void ValidateVentConnectivity(MapLayoutData layout, ref ValidationResult result)
        {
            if (layout.VentNodes == null || layout.VentNodes.Length < 2)
            {
                result.Warnings++;
                result.Messages.Add("[WARN] 暗线节点少于2个，暗线系统无效。");
                return;
            }

            // 检查邻接表指向是否有效
            for (int i = 0; i < layout.VentNodes.Length; i++)
            {
                if (layout.VentNodes[i].ConnectedIndices == null ||
                    layout.VentNodes[i].ConnectedIndices.Length == 0)
                {
                    result.Warnings++;
                    result.Messages.Add($"[WARN] 暗线节点 {i} ({layout.VentNodes[i].Name}) 无邻接，为孤立节点。");
                }

                foreach (int connIdx in layout.VentNodes[i].ConnectedIndices ?? System.Array.Empty<int>())
                {
                    if (connIdx < 0 || connIdx >= layout.VentNodes.Length)
                    {
                        result.Errors++;
                        result.Messages.Add($"[ERROR] 暗线节点 {i} 的邻接索引 {connIdx} 越界。");
                    }
                }
            }

            // BFS 检查连通分量数
            bool[] visited = new bool[layout.VentNodes.Length];
            int components = 0;
            var queue = new Queue<int>();

            for (int i = 0; i < layout.VentNodes.Length; i++)
            {
                if (visited[i]) continue;
                components++;
                queue.Clear();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    foreach (int next in layout.VentNodes[current].ConnectedIndices ?? System.Array.Empty<int>())
                    {
                        if (!visited[next])
                        {
                            visited[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            if (components > 1)
            {
                result.Warnings++;
                result.Messages.Add($"[WARN] 暗线网络有 {components} 个连通分量，部分节点无法互达。");
            }
            else
            {
                result.Messages.Add($"[OK] 暗线网络全连通（{layout.VentNodes.Length} 个节点）。");
            }
        }

        /// <summary>
        /// 验证监控摄像头覆盖是否有盲区过大问题。
        /// </summary>
        public static void ValidateSurveillanceCoverage(MapLayoutData layout, ref ValidationResult result)
        {
            if (layout.SurveillanceZones == null || layout.SurveillanceZones.Length == 0)
            {
                result.Warnings++;
                result.Messages.Add("[WARN] 没有定义监控摄像头。");
                return;
            }

            // 检查每个房间是否至少有一个摄像头覆盖
            if (layout.Rooms != null)
            {
                for (int i = 0; i < layout.Rooms.Length; i++)
                {
                    bool covered = false;
                    foreach (var zone in layout.SurveillanceZones)
                    {
                        if (zone.RoomIndex == i)
                        {
                            covered = true;
                            break;
                        }
                    }

                    if (!covered)
                    {
                        result.Warnings++;
                        result.Messages.Add($"[WARN] 房间 {i} ({layout.Rooms[i].Name}) 无监控覆盖。");
                    }
                }
            }

            // 检查走廊区域是否有监控
            int corridorCameras = 0;
            foreach (var zone in layout.SurveillanceZones)
            {
                if (zone.RoomIndex < 0) corridorCameras++;
            }

            if (corridorCameras < 2)
            {
                result.Warnings++;
                result.Messages.Add($"[WARN] 走廊/公共区域仅 {corridorCameras} 个摄像头，建议至少 2 个。");
            }

            if (result.Messages.All(m => !m.Contains("[WARN] 房间") && !m.Contains("[WARN] 走廊")))
            {
                result.Messages.Add($"[OK] 监控覆盖完整：{layout.SurveillanceZones.Length} 个摄像头覆盖全部关键区域。");
            }
        }

        /// <summary>
        /// 验证视线遮挡体是否合理（不遮挡所有路线）。
        /// </summary>
        public static void ValidateSightBlockers(MapLayoutData layout, ref ValidationResult result)
        {
            if (layout.SightBlockers == null || layout.SightBlockers.Length == 0)
            {
                result.Warnings++;
                result.Messages.Add("[WARN] 没有定义视线遮挡体，暗杀难度过低。");
                return;
            }

            if (layout.SightBlockers.Length < 4)
            {
                result.Warnings++;
                result.Messages.Add($"[WARN] 仅有 {layout.SightBlockers.Length} 个视线遮挡体，建议至少 4 个以创造暗杀机会。");
            }
            else
            {
                result.Messages.Add($"[OK] {layout.SightBlockers.Length} 个视线遮挡体。");
            }
        }

        /// <summary>
        /// 验证房间网络连通性（走廊是否连接所有房间）。
        /// </summary>
        public static void ValidateRoomConnectivity(MapLayoutData layout, ref ValidationResult result)
        {
            if (layout.Rooms == null || layout.Corridors == null) return;

            int connectedRooms = 0;
            foreach (var room in layout.Rooms)
            {
                Vector2 roomCenter = room.Center;
                bool connected = false;

                foreach (var corridor in layout.Corridors)
                {
                    if (corridor.IsRoundNode) continue;

                    // 检查走廊与房间是否有重叠
                    Vector2 corMin = corridor.Center - corridor.Size * 0.5f;
                    Vector2 corMax = corridor.Center + corridor.Size * 0.5f;
                    Vector2 roomMin = roomCenter - new Vector2(room.Size.x, room.Size.y) * 0.5f;
                    Vector2 roomMax = roomCenter + new Vector2(room.Size.x, room.Size.y) * 0.5f;

                    if (corMin.x < roomMax.x && corMax.x > roomMin.x &&
                        corMin.y < roomMax.y && corMax.y > roomMin.y)
                    {
                        connected = true;
                        break;
                    }

                    // 也检查扩展边界（入口侧允许更近的连接）
                    Vector2 roomMinExt = roomMin - new Vector2(0.5f, 0.5f);
                    Vector2 roomMaxExt = roomMax + new Vector2(0.5f, 0.5f);
                    if (corMin.x < roomMaxExt.x && corMax.x > roomMinExt.x &&
                        corMin.y < roomMaxExt.y && corMax.y > roomMinExt.y)
                    {
                        connected = true;
                        break;
                    }
                }

                if (connected)
                    connectedRooms++;
                else
                {
                    result.Warnings++;
                    result.Messages.Add($"[WARN] 房间 {room.Name} 与走廊网络无连接。");
                }
            }

            if (connectedRooms == layout.Rooms.Length)
            {
                result.Messages.Add($"[OK] 全部 {layout.Rooms.Length} 个房间与走廊网络连通。");
            }
        }

        // ══════════════════════════════════════════════════════
        // BFS / 网格辅助
        // ══════════════════════════════════════════════════════

        private static HashSet<Vector2Int> BuildBlockedGrid(MapLayoutData layout)
        {
            var blocked = new HashSet<Vector2Int>();

            // 房间墙壁 -> 不可行走
            if (layout.Rooms != null)
            {
                foreach (var room in layout.Rooms)
                {
                    float hw = room.Size.x * 0.5f + 0.1f;
                    float hh = room.Size.y * 0.5f + 0.1f;
                    // 标记房间边界为阻挡（入口侧除外，这里简化处理）
                    MarkRect(blocked,
                        room.Center.x - hw, room.Center.x + hw,
                        room.Center.y - hh, room.Center.y + hh, false);
                }
            }

            // 视线遮挡体 -> 不可行走
            if (layout.SightBlockers != null)
            {
                foreach (var blocker in layout.SightBlockers)
                {
                    MarkRect(blocked,
                        blocker.Center.x - blocker.Size.x * 0.5f,
                        blocker.Center.x + blocker.Size.x * 0.5f,
                        blocker.Center.y - blocker.Size.y * 0.5f,
                        blocker.Center.y + blocker.Size.y * 0.5f, true);
                }
            }

            // 地图外 -> 不可行走
            float hwMap = layout.DesignHalfWidth;
            float hhMap = layout.DesignHalfHeight;
            MarkRect(blocked, -hwMap - 1f, hwMap + 1f, hhMap, hhMap + 1f, true); // 北
            MarkRect(blocked, -hwMap - 1f, hwMap + 1f, -hhMap - 1f, -hhMap, true); // 南
            MarkRect(blocked, -hwMap - 1f, -hwMap, -hhMap - 1f, hhMap + 1f, true); // 西
            MarkRect(blocked, hwMap, hwMap + 1f, -hhMap - 1f, hhMap + 1f, true);   // 东

            return blocked;
        }

        private static void MarkRect(HashSet<Vector2Int> blocked,
            float xMin, float xMax, float yMin, float yMax, bool markBlocked)
        {
            int ixMin = Mathf.FloorToInt(xMin / GridStep);
            int ixMax = Mathf.CeilToInt(xMax / GridStep);
            int iyMin = Mathf.FloorToInt(yMin / GridStep);
            int iyMax = Mathf.CeilToInt(yMax / GridStep);

            for (int x = ixMin; x <= ixMax; x++)
            {
                for (int y = iyMin; y <= iyMax; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (markBlocked)
                        blocked.Add(cell);
                    else
                        blocked.Remove(cell);
                }
            }
        }

        private static Vector2Int ToGrid(Vector2 pos) =>
            new Vector2Int(Mathf.RoundToInt(pos.x / GridStep), Mathf.RoundToInt(pos.y / GridStep));

        private static bool BfsReachable(Vector2Int start, Vector2Int target,
            HashSet<Vector2Int> blocked, int maxSteps)
        {
            if (start == target) return true;

            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<(Vector2Int, int)>();
            queue.Enqueue((start, 0));

            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            while (queue.Count > 0)
            {
                var (current, steps) = queue.Dequeue();
                if (steps >= maxSteps) continue;

                foreach (var dir in dirs)
                {
                    Vector2Int next = current + dir;
                    if (next == target) return true;
                    if (visited.Contains(next)) continue;
                    if (blocked.Contains(next)) continue;
                    visited.Add(next);
                    queue.Enqueue((next, steps + 1));
                }
            }

            return false;
        }
    }
}
