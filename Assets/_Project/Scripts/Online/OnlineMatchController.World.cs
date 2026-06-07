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
