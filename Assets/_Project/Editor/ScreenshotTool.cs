using System;
using System.Collections;
using System.IO;
using GanglandUndercover.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// Batch screenshot capture tool.
    /// Menu: Gangland > Screenshots > Capture Demo Shots
    ///
    /// Captures: MainMenu, Lobby (after Enter), Chat in Action, Settings panel.
    /// Output: Screenshots/YYYY-MM-DD_HHmm_*.png
    /// </summary>
    public static class ScreenshotTool
    {
        private const string MenuRoot = "Gangland/Screenshots/";
        private const string ScreenshotDir = "Screenshots";

        [MenuItem(MenuRoot + "Capture Demo Shots (PlayMode required)", false, 2000)]
        public static void CaptureDemoShots()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Screenshot Tool",
                    "Please enter PlayMode first (click Play in the Editor), " +
                    "navigate to the screen you want to capture, " +
                    "then run this menu item again.",
                    "OK");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            string dir = Path.Combine(Application.dataPath, "..", ScreenshotDir);
            Directory.CreateDirectory(dir);

            EditorCoroutineRunner.StartCoroutine(CaptureSequence(dir, timestamp));
        }

        private static IEnumerator CaptureSequence(string dir, string timestamp)
        {
            // Wait for UI to stabilize
            yield return new WaitForSecondsRealtime(0.5f);

            string path = Path.Combine(dir, $"{timestamp}_demo.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[ScreenshotTool] Saved: {path}");
            EditorUtility.DisplayDialog("Screenshot Tool",
                $"Screenshot saved to: {path}\n\n" +
                "To capture different screens:\n" +
                "1. Navigate to the desired screen in PlayMode\n" +
                "2. Run Gangland > Screenshots > Capture Demo Shots again",
                "OK");
        }

        [MenuItem(MenuRoot + "Capture Current Screen (4K)", false, 2001)]
        public static void CaptureCurrent4K()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Screenshot Tool",
                    "Please enter PlayMode first.", "OK");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string dir = Path.Combine(Application.dataPath, "..", ScreenshotDir);
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{timestamp}_4k.png");
            ScreenCapture.CaptureScreenshot(path, 2); // 2x supersampling ≈ 4K
            Debug.Log($"[ScreenshotTool] Saved 4K: {path}");
        }

        [MenuItem(MenuRoot + "Capture Main Menu Art Review Set", false, 2002)]
        public static void CaptureMainMenuArtReviewSet()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Camera camera = CreateMainMenuReviewBaseline();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScreenshotDir));
                Directory.CreateDirectory(directory);

                CaptureCameraToPng(camera, Path.Combine(directory, "main-menu-ai-1280x720.png"), 1280, 720);
                CaptureCameraToPng(camera, Path.Combine(directory, "main-menu-ai-1920x1080.png"), 1920, 1080);
                CaptureCameraToPng(camera, Path.Combine(directory, "main-menu-ai-2560x1440.png"), 2560, 1440);
                Debug.Log("[ScreenshotTool] Main menu art review set saved: " + directory);
            }
            finally
            {
                if (previousSceneSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
                }
            }
        }

        [MenuItem(MenuRoot + "Capture Named (mainmenu_setting_entry)", false, 2100)]
        public static void CaptureMainMenuSetting() => CaptureNamed("mainmenu_setting_entry");

        [MenuItem(MenuRoot + "Capture Named (setting_overlay)", false, 2101)]
        public static void CaptureSettingOverlay() => CaptureNamed("setting_overlay");

        [MenuItem(MenuRoot + "Capture Named (login_anonymous)", false, 2102)]
        public static void CaptureLoginAnonymous() => CaptureNamed("login_anonymous");

        [MenuItem(MenuRoot + "Capture Named (login_ready)", false, 2103)]
        public static void CaptureLoginReady() => CaptureNamed("login_ready");

        [MenuItem(MenuRoot + "Capture Named (hud_report_disabled)", false, 2104)]
        public static void CaptureHudReportDisabled() => CaptureNamed("hud_report_disabled");

        [MenuItem(MenuRoot + "Capture Named (hud_report_enabled)", false, 2105)]
        public static void CaptureHudReportEnabled() => CaptureNamed("hud_report_enabled");

        [MenuItem(MenuRoot + "Capture Named (hud_report_done)", false, 2106)]
        public static void CaptureHudReportDone() => CaptureNamed("hud_report_done");

        [MenuItem(MenuRoot + "Capture Named (hud_block_done)", false, 2107)]
        public static void CaptureHudBlockDone() => CaptureNamed("hud_block_done");

        private static void CaptureNamed(string name)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Screenshot Tool",
                    "Please enter PlayMode first.", "OK");
                return;
            }

            string dir = Path.Combine(Application.dataPath, "..", ScreenshotDir);
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"qa_{name}_20260609.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[ScreenshotTool] Saved: {path}");
            EditorUtility.DisplayDialog("Screenshot Tool", $"Saved: qa_{name}_20260609.png", "OK");
        }

        [MenuItem(MenuRoot + "Open Screenshots Folder", false, 2200)]
        public static void OpenScreenshotsFolder()
        {
            string dir = Path.Combine(Application.dataPath, "..", ScreenshotDir);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private static Camera CreateMainMenuReviewBaseline()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Menu Review Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            GameObject canvasObject = new GameObject(
                "Main Menu Review Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            canvasRect.localScale = Vector3.one * 0.01f;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            GameObject menuObject = new GameObject("Main Menu Art Review");
            MainMenuController menu = menuObject.AddComponent<MainMenuController>();
            menu.Initialize(null);
            return camera;
        }

        private static void CaptureCameraToPng(Camera camera, string path, int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }

    /// <summary>
    /// Simple coroutine runner for Editor scripts.
    /// </summary>
    internal class EditorCoroutineRunner : MonoBehaviour
    {
        private static EditorCoroutineRunner _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance == null)
            {
                var go = new GameObject("__EditorCoroutineRunner__");
                go.hideFlags = HideFlags.HideAndDontSave;
                _instance = go.AddComponent<EditorCoroutineRunner>();
                DontDestroyOnLoad(go);
            }
        }

        public static void StartCoroutine(IEnumerator routine)
        {
            if (_instance == null) Initialize();
            _instance.DoStartCoroutine(routine);
        }

        private void DoStartCoroutine(IEnumerator routine)
        {
            StartCoroutine(routine);
        }
    }
}
