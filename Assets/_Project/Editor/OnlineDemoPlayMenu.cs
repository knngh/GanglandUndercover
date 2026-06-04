using System.IO;
using GanglandUndercover.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    [InitializeOnLoad]
    public static class OnlineDemoPlayMenu
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";
        private const string ScreenshotPath = "Screenshots/gangland-online-demo.png";
        private const string ActiveKey = "Gangland.PlayDemo.Active";
        private const string StartedKey = "Gangland.PlayDemo.Started";
        private const string ActionViewKey = "Gangland.PlayDemo.ActionView";
        private const string ScreenshotKey = "Gangland.PlayDemo.Screenshot";
        private const string RequestedAtKey = "Gangland.PlayDemo.RequestedAt";

        static OnlineDemoPlayMenu()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Gangland/Play Online Demo")]
        public static void PlayOnlineDemo()
        {
            QuaterniusRuntimeResourceMirror.SyncRuntimeResources();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(StartedKey, false);
            SessionState.SetBool(ActionViewKey, false);
            SessionState.SetBool(ScreenshotKey, false);
            SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.OpenScene(ScenePath);
                EditorApplication.isPlaying = true;
            }
        }

        [MenuItem("Gangland/Capture Online Demo Screenshot")]
        public static void CaptureOnlineDemoScreenshot()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
            Debug.Log("Gangland online demo screenshot queued with gameplay HUD: " + Path.GetFullPath(ScreenshotPath));
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                StopWaiting("Inactive.");
                return;
            }

            double requestedAt = SessionState.GetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);

            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - requestedAt > 20.0)
                {
                    StopWaiting("Timed out waiting for Play mode.");
                }

                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
            {
                return;
            }

            OnlineMatchController controller = Object.FindAnyObjectByType<OnlineMatchController>();

            if (controller == null)
            {
                if (EditorApplication.timeSinceStartup - requestedAt > 20.0)
                {
                    StopWaiting("Timed out waiting for OnlineMatchController.");
                }

                return;
            }

            if (!SessionState.GetBool(StartedKey, false))
            {
                controller.EditorStartLocalPlayablePreview();
                SessionState.SetBool(StartedKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                return;
            }

            if (!SessionState.GetBool(ActionViewKey, false) && EditorApplication.timeSinceStartup - requestedAt > 2.0)
            {
                controller.EditorSkipOpeningForSmokeTest();
                controller.EditorConfigureActionCameraForSmokeTest();
                controller.EditorForceActionPreviewForSmokeTest();
                SessionState.SetBool(ActionViewKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                return;
            }

            if (SessionState.GetBool(ActionViewKey, false) && !SessionState.GetBool(ScreenshotKey, false) && EditorApplication.timeSinceStartup - requestedAt > 1.6)
            {
                controller.EditorForceActionPreviewForSmokeTest();
                SessionState.SetBool(ScreenshotKey, true);
                CaptureOnlineDemoScreenshot();
                StopWaiting("Online playable demo is running.");
            }
        }

        private static void StopWaiting(string message)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ActionViewKey, false);
            Debug.Log("Gangland Play Online Demo: " + message);
        }
    }
}
