using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

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

        [MenuItem(MenuRoot + "Open Screenshots Folder", false, 2002)]
        public static void OpenScreenshotsFolder()
        {
            string dir = Path.Combine(Application.dataPath, "..", ScreenshotDir);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
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
