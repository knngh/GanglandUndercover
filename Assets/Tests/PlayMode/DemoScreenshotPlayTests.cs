using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace GanglandUndercover.PlayTests
{
    /// <summary>
    /// Demo 计划 D-1：关键截图基线自动化（2026-08-21）。
    ///
    /// 在真实 PlayMode 生命周期下驱动 OnlineMatchController 跑完整一局，
    /// 并在 5 个关键节点截屏，产出 Demo 用的 4K 基线截图：
    ///   Lobby（大厅）→ Opening（身份简报）→ Action（行动 HUD）
    ///   → Meeting（会议）→ Voting（投票）→ Result（结算）。
    ///
    /// 运行方式（需要图形，不能 -nographics）：
    ///   Unity -runTests -testPlatform PlayMode \
    ///     -testFilter "GanglandUndercover.PlayTests.DemoScreenshotPlayTests" \
    ///     -testResults demo_shots.xml -logFile demo_shots.log
    ///
    /// 截图输出：&lt;projectPath&gt;/Screenshots/DemoBaseline/（ScreenCapture
    /// 在 Editor 下写到工程根相对路径）。
    ///
    /// 相变驱动复用 MatchLoopPlayTests 的 Editor*ForSmokeTest 确定性入口。
    /// </summary>
    public class DemoScreenshotPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private static readonly MethodInfo EncodeToPngMethod = Type.GetType(
            "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
            ?.GetMethod("EncodeToPNG", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Texture2D) }, null);

        private GameObject _host;
        private GameObject _cameraHost;
        private MonoBehaviour _controller;
        private Type _controllerType;

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(_controllerType,
                $"找不到运行时类型 {ControllerTypeName}（Assembly-CSharp 未编译？）");

            _cameraHost = new GameObject("PlayTest_DemoShots_MainCamera", typeof(Camera), typeof(AudioListener));
            _cameraHost.tag = "MainCamera";
            Camera camera = _cameraHost.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 13.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 1f);
            _cameraHost.transform.position = new Vector3(0f, 0f, -16.2f);

            _host = new GameObject("PlayTest_DemoShots");
            _controller = (MonoBehaviour)_host.AddComponent(_controllerType);
            Assert.IsNotNull(_controller, "无法在 PlayMode 下挂载 OnlineMatchController。");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }

            if (_cameraHost != null)
            {
                UnityEngine.Object.Destroy(_cameraHost);
            }
        }

        [UnityTest]
        public IEnumerator DemoBaseline_CapturesAllKeyPhases()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("Demo screenshot baseline requires a graphics device; run this test without -nographics.");
            }

            // Awake 排队执行；跑一帧确保核心服务与 Canvas HUD 就绪。
            yield return RunFrames(3);

            // ── 1. Lobby（大厅）──
            AssertPhase("Lobby", "截图流程应从 Lobby 开始");
            yield return Capture("01_lobby");

            // ── 2. 启动本地对局（自动补 AI）→ Opening（身份简报）──
            Invoke("EditorSimulateLocalMatch");
            yield return RunFrames(3);
            Assert.IsTrue(GetBool("MatchStarted"), "本地对局应已开始");
            Assert.IsTrue(GetBool("IsOnline"), "本地截图对局必须进入在线预览 HUD 状态");
            AssertPhase("Opening", "启动后应进入 Opening 开局简报");
            yield return Capture("02_opening_briefing");

            // ── 3. Opening → Action（行动 HUD）──
            Invoke("EditorSkipOpeningForSmokeTest");
            // The normal frame loop eases the camera from the wide briefing view.
            // Snap once here so the baseline captures the actual Action framing,
            // while AssertActionCameraFocused guards this contract on future edits.
            Assert.IsTrue(InvokeBool("EditorConfigureActionCameraForSmokeTest"),
                "行动阶段应能配置正交跟随相机。");
            yield return RunFrames(5);
            AssertPhase("Action", "跳过简报后应进入 Action 行动阶段");
            AssertActionCameraFocused();
            AssertActionWorldVisible();
            LogActionCanvasStack();
            yield return Capture("03_action_hud");

            // ── 4. 制造倒地 + 召开会议 → Meeting（会议界面）──
            bool downed = InvokeBool("EditorForceDownedStateForSmokeTest");
            Assert.IsTrue(downed, "应成功制造倒地现场供会议展示");
            Invoke("EditorForceMeetingForSmokeTest");
            yield return RunFrames(3);
            AssertPhase("Meeting", "报案后应进入 Meeting 会议阶段");
            yield return Capture("04_meeting");

            // ── 5. 投票 → Voting（投票面板）──
            bool voteVisible = InvokeBool("EditorForceVoteStateForSmokeTest");
            Assert.IsTrue(voteVisible, "投票应产生可见反馈");
            yield return RunFrames(3);
            yield return Capture("05_voting");

            // ── 6. 结算 → Result（胜负揭晓）──
            bool reachedResult = InvokeBool("EditorForceResultForSmokeTest");
            Assert.IsTrue(reachedResult, "应判定出胜负并进入 Result");
            AssertPhase("Result", "胜负判定后应进入 Result 结算阶段");
            yield return RunFrames(3);
            yield return Capture("06_result");

            Assert.IsFalse(string.IsNullOrWhiteSpace(GetString("ResultSummary")),
                "结算文案应非空");
        }

        // ── 截图助手 ──

        private IEnumerator Capture(string name)
        {
            // 确保输出目录存在（Editor 下相对路径位于工程根）。
            string directory = Path.Combine(Application.dataPath, "..", "Screenshots", "DemoBaseline");
            Directory.CreateDirectory(directory);

            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "截图基线要求存在 Main Camera。");

            // RenderTexture 路径是同步的，适用于 Unity batchmode；固定 1080p
            // 也避免测试分辨率 640x480 污染 Demo 基线。
            string absolutePath = Path.Combine(directory, $"{name}_{DateTime.Now:HHmmssfff}.png");
            RenderTexture target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                image.Apply(false, false);
                Assert.IsNotNull(EncodeToPngMethod,
                    "截图基线要求 UnityEngine.ImageConversionModule.EncodeToPNG。");
                byte[] bytes = (byte[])EncodeToPngMethod.Invoke(null, new object[] { image });
                File.WriteAllBytes(absolutePath, bytes);

                FileInfo info = new FileInfo(absolutePath);
                Assert.Greater(info.Length, 0, $"截图文件为空: {absolutePath}");
                Assert.IsTrue(IsPng(bytes), $"截图不是有效 PNG: {absolutePath}");
                Assert.AreEqual(1920, ReadPngDimension(bytes, 16), $"截图宽度错误: {absolutePath}");
                Assert.AreEqual(1080, ReadPngDimension(bytes, 20), $"截图高度错误: {absolutePath}");
                AssertHasVisualContent(image, absolutePath);
                Debug.Log($"[DemoShots] 已写入截图: {name} -> {absolutePath}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(image);
            }

            yield return null;
        }

        private static void AssertHasVisualContent(Texture2D image, string path)
        {
            float minLuminance = 1f;
            float maxLuminance = 0f;
            const int sampleColumns = 64;
            const int sampleRows = 36;

            for (int y = 0; y < sampleRows; y++)
            {
                for (int x = 0; x < sampleColumns; x++)
                {
                    Color pixel = image.GetPixelBilinear(
                        (x + 0.5f) / sampleColumns,
                        (y + 0.5f) / sampleRows);
                    float luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                    minLuminance = Mathf.Min(minLuminance, luminance);
                    maxLuminance = Mathf.Max(maxLuminance, luminance);
                }
            }

            Assert.Greater(maxLuminance, 0.03f, $"截图接近纯黑: {path}");
            Assert.Greater(maxLuminance - minLuminance, 0.02f, $"截图缺少可辨识内容: {path}");
        }

        private void AssertActionWorldVisible()
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "Action 阶段要求存在 Main Camera。");

            FieldInfo worldRootField = _controllerType.GetField("worldRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(worldRootField, "Action 阶段诊断要求可访问 worldRoot。");
            GameObject worldRoot = worldRootField.GetValue(_controller) as GameObject;
            Assert.IsNotNull(worldRoot, "Action 阶段要求世界根节点已创建。");

            Renderer[] renderers = worldRoot.GetComponentsInChildren<Renderer>(true);
            int visibleCount = 0;
            int spriteCount = 0;
            int spriteWithContentCount = 0;
            List<SpriteRenderer> viewSprites = new List<SpriteRenderer>();
            string firstSprite = "<none>";
            Bounds visibleBounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                if (spriteRenderer != null)
                {
                    spriteCount++;
                    if (spriteRenderer.sprite != null && spriteRenderer.color.a > 0.001f)
                    {
                        spriteWithContentCount++;
                        if (firstSprite == "<none>")
                        {
                            Sprite sprite = spriteRenderer.sprite;
                            firstSprite = $"{spriteRenderer.name} pos={spriteRenderer.transform.position} sprite={sprite.name} rect={sprite.rect} ppu={sprite.pixelsPerUnit:F1} texture={sprite.texture?.name} color={spriteRenderer.color} layer={spriteRenderer.gameObject.layer} sorting={spriteRenderer.sortingLayerName}/{spriteRenderer.sortingOrder} shader={spriteRenderer.sharedMaterial?.shader?.name}";
                        }
                    }
                }

                Vector3 viewport = camera.WorldToViewportPoint(renderer.bounds.center);
                bool inFront = viewport.z > camera.nearClipPlane && viewport.z < camera.farClipPlane;
                bool inViewport = viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
                if (inFront && inViewport)
                {
                    visibleCount++;
                    if (visibleCount == 1) visibleBounds = renderer.bounds;
                    if (spriteRenderer != null) viewSprites.Add(spriteRenderer);
                }
            }

            Debug.Log($"[DemoShots] Action camera pos={camera.transform.position} rot={camera.transform.rotation.eulerAngles} ortho={camera.orthographic} size={camera.orthographicSize:F2} cullingMask={camera.cullingMask} worldRenderers={renderers.Length} visible={visibleCount} sprites={spriteCount} spriteContent={spriteWithContentCount} firstSprite={firstSprite} firstBounds={visibleBounds}");
            Assert.Greater(visibleCount, 0,
                "Action 阶段相机视锥内没有世界渲染器，截图会只剩 HUD。");

            int boundedSpriteCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sprite = renderers[i] as SpriteRenderer;
                if (sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null)
                    continue;

                Vector3 viewport = camera.WorldToViewportPoint(sprite.bounds.center);
                if (viewport.z > camera.nearClipPlane && viewport.z < camera.farClipPlane
                    && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f
                    && sprite.bounds.size.x < camera.orthographicSize * 8f
                    && sprite.bounds.size.y < camera.orthographicSize * 8f)
                {
                    boundedSpriteCount++;
                }
            }

            Assert.Greater(boundedSpriteCount, 0,
                "Action 阶段视锥内没有尺寸正常的世界 Sprite；资源图集不能以整张图参与世界渲染。");

            viewSprites.Sort((a, b) => b.sortingOrder.CompareTo(a.sortingOrder));
            int sampleCount = Mathf.Min(8, viewSprites.Count);
            for (int i = 0; i < sampleCount; i++)
            {
                SpriteRenderer sprite = viewSprites[i];
                Debug.Log($"[DemoShots] Action topSprite[{i}] name={sprite.name} sorting={sprite.sortingLayerName}/{sprite.sortingOrder} pos={sprite.transform.position} bounds={sprite.bounds} color={sprite.color} texture={sprite.sprite.texture?.name} rect={sprite.sprite.rect}");
            }
        }

        private static void AssertActionCameraFocused()
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "Action 阶段要求存在 Main Camera。");
            Assert.LessOrEqual(camera.orthographicSize, 3.1f,
                "Action 基线必须等待相机从全图简报视角收束到玩家跟随视角。");
        }

        private void LogActionCanvasStack()
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas current = canvases[i];
                if (current == null) continue;

                Image[] images = current.GetComponentsInChildren<Image>(true);
                int fullScreenImages = 0;
                string firstFullScreen = "<none>";
                for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
                {
                    Image image = images[imageIndex];
                    if (image == null || !image.enabled || !image.gameObject.activeInHierarchy) continue;
                    RectTransform rect = image.rectTransform;
                    bool stretchesScreen = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one
                        && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
                    if (stretchesScreen)
                    {
                        fullScreenImages++;
                        if (firstFullScreen == "<none>")
                        {
                            firstFullScreen = $"{image.name} color={image.color} raycast={image.raycastTarget}";
                        }
                    }
                }

                Debug.Log($"[DemoShots] Action canvas name={current.name} active={current.gameObject.activeInHierarchy} enabled={current.enabled} mode={current.renderMode} sorting={current.sortingOrder} override={current.overrideSorting} plane={current.planeDistance:F2} images={images.Length} fullScreenImages={fullScreenImages} firstFullScreen={firstFullScreen}");
            }
        }

        private static bool IsPng(byte[] bytes)
        {
            return bytes != null && bytes.Length >= 24
                && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        }

        private static int ReadPngDimension(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16)
                | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        // ── 反射助手（与 MatchLoopPlayTests 同风格）──

        private IEnumerator RunFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private void Invoke(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {method}");
            mi.Invoke(_controller, null);
        }

        private bool InvokeBool(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到方法 {method}");
            object result = mi.Invoke(_controller, null);
            return result is bool b && b;
        }

        private object GetPropertyOrField(string name)
        {
            PropertyInfo pi = _controllerType.GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pi != null) return pi.GetValue(_controller);

            FieldInfo fi = _controllerType.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return fi != null ? fi.GetValue(_controller) : null;
        }

        private void AssertPhase(string expected, string message)
        {
            // 与 MatchLoopPlayTests 一致：相位经公共属性 Phase 读取。
            PropertyInfo pi = _controllerType.GetProperty("Phase",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(pi, "控制器应暴露 Phase 属性");
            object phase = pi.GetValue(_controller);
            Assert.IsNotNull(phase, "Phase 属性值不应为空");
            Assert.AreEqual(expected, phase.ToString(), message);
        }

        private int GetInt(string name)
        {
            object value = GetPropertyOrField(name);
            Assert.IsNotNull(value, $"控制器应暴露 {name}");
            return Convert.ToInt32(value);
        }

        private bool GetBool(string name)
        {
            object value = GetPropertyOrField(name);
            Assert.IsNotNull(value, $"控制器应暴露 {name}");
            return Convert.ToBoolean(value);
        }

        private string GetString(string name)
        {
            object value = GetPropertyOrField(name);
            return value?.ToString();
        }
    }
}
