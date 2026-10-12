using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// World lifecycle management methods for OnlineMatchController.
    /// Extracted as partial class to keep main controller lean.
    /// </summary>
    public partial class OnlineMatchController : MonoBehaviour
    {
        // ============================================================
        //  WORLD LIFECYCLE
        // ============================================================

        private void EnsureWorld()
        {
            if (worldRoot != null)
            {
                return;
            }

            DestroyStaleWorldRoots();
            solidObstacleRects.Clear();
            walkableRects.Clear();
            worldLabels.Clear();

            worldRoot = new GameObject(WorldRootName);
            worldRoot.transform.SetParent(transform, false);

            if (WorldBuilder == null)
            {
                WorldBuilder = new OnlineWorldBuilder();
            }
            WorldBuilder.Initialize(worldRoot, mapService, solidObstacleRects, walkableRects, worldLabels,
                ruleSet?.UnderworldPassageCount ?? 8);
            WorldBuilder.SetTasks(tasks);
            WorldBuilder.EnsureRuntimeSprites();

            // Delegate world construction to WorldBuilder
            if (mapService != null)
            {
                WorldBuilder.BuildDistrictMap();
            }

            // P0-2: 任务点坐标与碰撞对齐。TaskPositionFor 的 28 个硬编码设计坐标
            // 从未与 WorldBuilder 注册的障碍矩形校验，诊断采样发现过半任务点落在
            // 墙/障碍里（IsWalkable=False），bot 与真人都无法贴近交互。
            // 世界（含碰撞矩形）建好后，把不可走任务点 snap 到最近开阔位置。
            // FindNearestOpenPosition 为确定性 ring 搜索，各端本地结果一致。
            SnapTasksToWalkable();
        }

        private void SnapTasksToWalkable()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                OnlineTaskState task = tasks[i];
                if (IsWalkable(task.Position))
                {
                    continue;
                }

                Vector3 snapped = FindNearestOpenPosition(task.Position, task.Position);
                if (snapped != task.Position)
                {
                    tasks[i] = new OnlineTaskState(task.Id, task.Name, snapped,
                        task.Progress, task.RequiredProgress, task.Completed, task.Sabotaged);
                }
            }
        }

        private void DestroyRuntimeWorld()
        {
            if (worldRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(worldRoot);
                }
                else
                {
                    DestroyImmediate(worldRoot);
                }
            }

            worldRoot = null;
            solidObstacleRects.Clear();
            walkableRects.Clear();
            worldLabels.Clear();
            taskVisuals.Clear();
            playerVisuals.Clear();
            playerVisualBaseScales.Clear();
            killSystem?.bodyVisuals.Clear();
            surveillanceCameras?.Clear();
            DestroyStaleWorldRoots();
        }

        private void DestroyStaleWorldRoots()
        {
            var staleRoots = new List<GameObject>();

            foreach (Transform child in transform)
            {
                if (child == null) continue;
                if (child.name == WorldRootName
                    || child.name.StartsWith("Online Hong Kong Port Map", StringComparison.Ordinal)
                    || child.name.StartsWith("Online Gangland Runtime Map", StringComparison.Ordinal))
                {
                    staleRoots.Add(child.gameObject);
                }
            }

            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (candidate == null || candidate == transform || candidate.IsChildOf(transform)) continue;
                if (candidate.name == WorldRootName
                    || candidate.name.StartsWith("Online Hong Kong Port Map", StringComparison.Ordinal)
                    || candidate.name.StartsWith("Online Gangland Runtime Map", StringComparison.Ordinal))
                {
                    staleRoots.Add(candidate.gameObject);
                }
            }

            for (int i = 0; i < staleRoots.Count; i++)
            {
                if (staleRoots[i] == null) continue;
                if (Application.isPlaying)
                    Destroy(staleRoots[i]);
                else
                    DestroyImmediate(staleRoots[i]);
            }
        }
    }
}
