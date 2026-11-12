using System.Collections.Generic;
using GanglandUndercover.Online;
using NUnit.Framework;
using UnityEngine;

namespace GanglandUndercover.Tests
{
    /// <summary>
    /// 批次 6（画质攻坚）回归：
    /// - 6a 相机整数 texel 吸附数学
    /// - 6b 角色接触阴影（builder 生产路径，加强后应超出基座、alpha 可见）
    /// - 6c 全屏调色 shader 可找到、controller 幂等
    /// </summary>
    [TestFixture]
    public class ScreenGradeAndPixelCrispTests
    {
        // ─── 6a: SnapOrthoSizeToTexelGrid ──────────────────────

        [Test]
        public void SnapOrthoSize_1440p_Target3_IsExact()
        {
            // 1440 / (2*48*5) = 3.0 —— 恰好整数档
            Assert.AreEqual(3.0f, OnlineCameraRig.SnapOrthoSizeToTexelGrid(3.0f, 1440f, 48f), 1e-4f);
        }

        [Test]
        public void SnapOrthoSize_1080p_Target3_SnapsToNearestIntegerTexelStep()
        {
            // 1080p 无整数解档（3.75 vs 2.8125），取最近：2.8125
            Assert.AreEqual(2.8125f, OnlineCameraRig.SnapOrthoSizeToTexelGrid(3.0f, 1080f, 48f), 1e-4f);
        }

        [Test]
        public void SnapOrthoSize_900p_Target3_SnapsToNearestIntegerTexelStep()
        {
            // 900 / (2*48*3) = 3.125
            Assert.AreEqual(3.125f, OnlineCameraRig.SnapOrthoSizeToTexelGrid(3.0f, 900f, 48f), 1e-4f);
        }

        [Test]
        public void SnapOrthoSize_ResultIsAlwaysIntegerTexelStep()
        {
            // 吸附结果必须满足：screenH / (2*size) 是 PPU 的整数倍
            float[] targets = { 2.4f, 2.8f, 3.0f, 3.2f, 4.0f, 5.7f };
            int[] heights = { 720, 800, 900, 1050, 1080, 1200, 1440, 1600, 2160 };
            foreach (int h in heights)
            {
                foreach (float t in targets)
                {
                    float snapped = OnlineCameraRig.SnapOrthoSizeToTexelGrid(t, h, 48f);
                    float texelsPerPixel = h / (2f * snapped);
                    Assert.AreEqual(
                        Mathf.Round(texelsPerPixel),
                        texelsPerPixel,
                        1e-2f,
                        $"screenH={h} target={t} → size={snapped} 应为整数 texel/像素");
                }
            }
        }

        [Test]
        public void SnapOrthoSize_DegenerateInputs_ReturnUnchanged()
        {
            Assert.AreEqual(3.0f, OnlineCameraRig.SnapOrthoSizeToTexelGrid(3.0f, 0f, 48f));
            Assert.AreEqual(3.0f, OnlineCameraRig.SnapOrthoSizeToTexelGrid(3.0f, 1080f, 0f));
            Assert.AreEqual(0f, OnlineCameraRig.SnapOrthoSizeToTexelGrid(0f, 1080f, 48f));
            Assert.AreEqual(-1f, OnlineCameraRig.SnapOrthoSizeToTexelGrid(-1f, 1080f, 48f));
        }

        [Test]
        public void SnapOrthoSize_1080p_DeviationWithinHalfStep()
        {
            // 吸附偏移不应超过一档步长的一半（1080p 一档 = 0.9375，半档 0.47）
            float snapped = OnlineCameraRig.SnapOrthoSizeToTexelGrid(3.0f, 1080f, 48f);
            Assert.LessOrEqual(Mathf.Abs(snapped - 3.0f), 0.47f, "吸附偏移超过半档，视野变化过大");
        }

        // ─── 6b: 接触阴影（builder 生产路径） ──────────────────

        [Test]
        public void PlayerVisual_ContactShadow_PresentStrippedAndVisible()
        {
            GameObject mapHost = new GameObject("shadowTest_mapSvc");
            GameObject worldRoot = new GameObject("shadowTest_world");
            try
            {
                Online.OnlineMapService mapService = mapHost.AddComponent<Online.OnlineMapService>();
                mapService.ActiveMapType = Online.OnlineMapService.OnlineMapType.HarbourDistrict;

                var builder = new Online.OnlineWorldBuilder();
                builder.Initialize(
                    worldRoot,
                    mapService,
                    new List<Rect>(),
                    new List<Rect>(),
                    new List<TextMesh>(),
                    8);
                builder.EnsureRuntimeSprites();

                var state = new Online.OnlinePlayerState(1, "阴影测试", Vector3.zero, false, true,
                    Online.OnlineRole.Police, Online.OnlineProfession.Inspector, 0);
                GameObject visual = builder.CreatePlayerVisual(state, false);

                Assert.IsNotNull(visual, "CreatePlayerVisual 应返回角色根");
                Transform shadow = visual.transform.Find("阴影");
                Assert.IsNotNull(shadow, "角色根应有接触阴影子物体（B1）");

                SpriteRenderer shadowRenderer = shadow.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(shadowRenderer, "阴影子物体应有 SpriteRenderer");
                Assert.GreaterOrEqual(shadowRenderer.color.a, 0.38f,
                    "批次 6b：阴影 alpha 应加强到可见（≥0.38）");
                Assert.GreaterOrEqual(shadow.transform.localScale.x, 0.42f,
                    "批次 6b：阴影宽度应超出 0.28 宽的基座（≥0.42）");
            }
            finally
            {
                Object.DestroyImmediate(worldRoot);
                Object.DestroyImmediate(mapHost);
            }
        }

        // ─── 6c: 全屏调色资源 ─────────────────────────────────

        [Test]
        public void ScreenGradeShader_IsFindable()
        {
            Shader shader = Shader.Find(Online.ScreenGradeController.ShaderName);
            Assert.IsNotNull(shader, "Gangland/ScreenGrade shader 应可被 Shader.Find 找到");
        }

        [Test]
        public void ScreenGradeController_Ensure_IsIdempotent()
        {
            GameObject host = new GameObject("gradeHost");
            try
            {
                Online.ScreenGradeController first = Online.ScreenGradeController.Ensure(host);
                Online.ScreenGradeController second = Online.ScreenGradeController.Ensure(host);
                Assert.IsNotNull(first);
                Assert.AreSame(first, second, "Ensure 应幂等返回同一实例");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ScreenGradeController_BindNow_BindsCameraAndMaterial()
        {
            GameObject host = new GameObject("gradeBindHost");
            GameObject cameraObject = new GameObject("gradeBindCamera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                Online.ScreenGradeController grade = Online.ScreenGradeController.Ensure(host);
                grade.BindNow(camera);
                Assert.AreSame(camera, grade.BoundCamera, "BindNow 应绑定指定相机");
                Assert.IsTrue(grade.MaterialReady, "shader 存在时材质应就绪");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
