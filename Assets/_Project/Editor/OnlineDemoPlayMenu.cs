using System.IO;
using GanglandUndercover.Gameplay;
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
        private const string OnlineRequestedKey = "Gangland.PlayDemo.OnlineRequested";
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
            try
            {
                QuaterniusRuntimeResourceMirror.SyncRuntimeResources();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Gangland Play Online Demo: resource mirror skipped so the demo can start. " + exception.Message);
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(StartedKey, false);
            SessionState.SetBool(OnlineRequestedKey, false);
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
            string absoluteScreenshotPath = Path.GetFullPath(ScreenshotPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteScreenshotPath));

            Camera camera = Camera.main;

            if (camera != null)
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Debug.LogWarning("Gangland online demo screenshot skipped: Unity is running without a graphics device. Run this method without -nographics or use the editor menu.");
                    return;
                }

                CaptureCameraToPng(camera, absoluteScreenshotPath, 1600, 900);
                Debug.Log("Gangland online demo screenshot saved with gameplay HUD: " + absoluteScreenshotPath);
                return;
            }

            ScreenCapture.CaptureScreenshot(ScreenshotPath);
            Debug.Log("Gangland online demo screenshot queued with gameplay HUD: " + absoluteScreenshotPath);
        }

        public static void CaptureOnlineDemoScreenshotBaseline()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Online Demo Screenshot Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";

            GameObject onlineObject = new GameObject("Online Demo Screenshot Match");
            OnlineMatchController controller = onlineObject.AddComponent<OnlineMatchController>();

            controller.EditorSimulateLocalMatch();
            controller.EditorSkipOpeningForSmokeTest();
            controller.EditorConfigureActionCameraForSmokeTest();
            controller.EditorForceActionPreviewForSmokeTest();
            controller.EditorRefreshWorldVisualsForSmokeTest();
            CaptureOnlineDemoScreenshot();
        }

        private static void CaptureCameraToPng(Camera camera, string absoluteScreenshotPath, int width, int height)
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
                File.WriteAllBytes(absoluteScreenshotPath, texture.EncodeToPNG());
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
                PrototypeBootstrap bootstrap = Object.FindAnyObjectByType<PrototypeBootstrap>();
                if (bootstrap != null && !SessionState.GetBool(OnlineRequestedKey, false))
                {
                    bootstrap.StartOnlineGame();
                    SessionState.SetBool(OnlineRequestedKey, true);
                    SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                    return;
                }

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
            SessionState.SetBool(OnlineRequestedKey, false);
            Debug.Log("Gangland Play Online Demo: " + message);
        }
    }
}
