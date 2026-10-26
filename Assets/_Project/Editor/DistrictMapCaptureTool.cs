using System.Collections.Generic;
using System.IO;
using GanglandUndercover.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// 批处理地图视觉验收截图：EditMode 下构建完整街区地图，正交俯视渲染整图输出 PNG。
    /// 菜单: Gangland > Art > Capture District Map。
    /// 命令行（勿加 -nographics，渲染需要图形设备；必须带 -quit）:
    ///   Unity -batchmode -quit -projectPath &lt;proj&gt; -executeMethod GanglandUndercover.Editor.DistrictMapCaptureTool.CaptureHarbourDistrict
    /// 输出: Screenshots/district-map-&lt;map&gt;.png（美术批次 gate 证据，存档到 output/ 时一并归档）。
    /// </summary>
    public static class DistrictMapCaptureTool
    {
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        [MenuItem("Gangland/Art/Capture District Map (Harbour)")]
        public static void CaptureHarbourDistrict()
        {
            CaptureTo(OnlineMapService.OnlineMapType.HarbourDistrict, "district-map-harbour.png");
        }

        [MenuItem("Gangland/Art/Capture District Map (PoliceStation)")]
        public static void CapturePoliceStation()
        {
            CaptureTo(OnlineMapService.OnlineMapType.PoliceStation, "district-map-police.png");
        }

        [MenuItem("Gangland/Art/Capture District Map (KowloonWalledCity)")]
        public static void CaptureKowloonWalledCity()
        {
            CaptureTo(OnlineMapService.OnlineMapType.KowloonWalledCity, "district-map-kowloon.png");
        }

        /// <summary>-executeMethod 批处理入口：三张地图一次出齐。</summary>
        public static void CaptureAllDistrictMaps()
        {
            CaptureHarbourDistrict();
            CapturePoliceStation();
            CaptureKowloonWalledCity();
        }

        /// <summary>
        /// -executeMethod 入口：玩家视角截图（批次 5e 角色醒目度审查）。
        /// 建图后在主走廊放 4 个假玩家（各职业），相机按游戏内观感（≈8 单位宽）取景输出 PNG。
        /// 命令行同整图截图：勿加 -nographics，必须带 -quit。
        /// 输出: Screenshots/action-view-players.png
        /// </summary>
        public static void CaptureActionViewWithPlayers()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Action View Capture Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 4f; // 游戏内行动视角观感（8 单位宽）
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.102f, 0.11f, 0.173f, 1f);

            GameObject mapHost = new GameObject("Action View Capture MapService");
            OnlineMapService mapService = mapHost.AddComponent<OnlineMapService>();
            mapService.ActiveMapType = OnlineMapService.OnlineMapType.HarbourDistrict;

            GameObject worldRoot = new GameObject("Action View Capture World");
            var builder = new OnlineWorldBuilder();
            builder.Initialize(
                worldRoot,
                mapService,
                new List<Rect>(),
                new List<Rect>(),
                new List<TextMesh>(),
                8);
            builder.EnsureRuntimeSprites();
            builder.BuildDistrictMap();

            OnlineProfession[] professions =
            {
                OnlineProfession.Inspector,
                OnlineProfession.Enforcer,
                OnlineProfession.UndercoverAgent,
                OnlineProfession.Fixer,
            };
            Vector3[] spawns =
            {
                new Vector3(-0.9f, 0.35f, 0f),
                new Vector3(-0.3f, -0.25f, 0f),
                new Vector3(0.45f, 0.4f, 0f),
                new Vector3(1.0f, -0.2f, 0f),
            };
            for (int i = 0; i < professions.Length; i++)
            {
                var state = new OnlinePlayerState((ulong)(i + 1), "测试" + i, spawns[i], false, true,
                    OnlineRole.Unassigned, professions[i], 0, i == 3);
                GameObject visual = builder.CreatePlayerVisual(state, i == 0);
                // 对齐 UpdatePlayerVisuals 的运行时摆放：玩家活着渲染 z=0.32（高于低矮道具、
                // 低于立面遮挡），否则截图会失真成"玩家被会议桌/道具盖住"。
                if (visual != null)
                {
                    visual.transform.position = state.Position + new Vector3(0f, 0f, 0.32f);
                    OnlineWorldBuilder.SetSortingFromZ(visual);
                }
            }

            string absolutePath = Path.GetFullPath(Path.Combine("Screenshots", "action-view-players.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            RenderToPng(camera, absolutePath, CaptureWidth, CaptureHeight);
            Debug.Log($"[DistrictMapCaptureTool] action view -> {absolutePath}");
        }

        private static void CaptureTo(OnlineMapService.OnlineMapType mapType, string fileName)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("District Map Capture Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.102f, 0.11f, 0.173f, 1f); // 港夜深蓝 #1a1c2c

            GameObject mapHost = new GameObject("District Map Capture MapService");
            OnlineMapService mapService = mapHost.AddComponent<OnlineMapService>();
            mapService.ActiveMapType = mapType;

            GameObject worldRoot = new GameObject("District Map Capture World");
            var builder = new OnlineWorldBuilder();
            builder.Initialize(
                worldRoot,
                mapService,
                new List<Rect>(),
                new List<Rect>(),
                new List<TextMesh>(),
                8);
            builder.EnsureRuntimeSprites();
            builder.BuildDistrictMap();

            FrameCameraOnPlan(camera, worldRoot);

            string absolutePath = Path.GetFullPath(Path.Combine("Screenshots", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            RenderToPng(camera, absolutePath, CaptureWidth, CaptureHeight);
            Debug.Log($"[DistrictMapCaptureTool] {mapType} -> {absolutePath}");
        }

        /// <summary>按平面 sprite 包围盒取景（2.5D 立面保持游戏内实际遮挡关系叠在前）。</summary>
        private static void FrameCameraOnPlan(Camera camera, GameObject worldRoot)
        {
            bool hasBounds = false;
            var bounds = new Bounds();
            foreach (SpriteRenderer renderer in worldRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer == null || renderer.sprite == null) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning("[DistrictMapCaptureTool] 未找到任何 sprite，使用默认取景。");
                camera.orthographicSize = 10f;
                return;
            }

            const float margin = 1.04f;
            float halfHeight = bounds.extents.y * margin;
            float halfWidth = bounds.extents.x * margin;
            float aspect = (float)CaptureWidth / CaptureHeight;
            camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
        }

        private static void RenderToPng(Camera camera, string absolutePath, int width, int height)
        {
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(texture);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
