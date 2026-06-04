using System.IO;
using GanglandUndercover.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GanglandUndercover.Editor
{
    public static class VerticalSliceStageOneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Stage1VerticalSlice.unity";
        public const string PrefabPath = "Assets/_Project/Prefabs/Stage1VerticalSliceWorld.prefab";
        public const string BakeStampPath = "Assets/_Project/Docs/Stage1VerticalSliceBakeStamp.txt";
        public const string ScreenshotPath = "Screenshots/stage1-vertical-slice.png";

        [MenuItem("Gangland/Build Stage1 Vertical Slice Scene")]
        public static void BuildStageOneVerticalSliceScene()
        {
            QuaterniusRuntimeResourceMirror.SyncRuntimeResources();
            EnsureDirectory("Assets/_Project/Scenes");
            EnsureDirectory("Assets/_Project/Prefabs");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = CreateCamera();
            CreateLight();
            CreateEventSystem();

            GameObject controllerObject = new GameObject("Stage1 Vertical Slice Authoring Controller");
            controllerObject.AddComponent<UnityServiceBootstrap>();
            OnlineMatchController controller = controllerObject.AddComponent<OnlineMatchController>();
            controller.EditorBuildStageOneAuthoringWorldForBake();

            GameObject worldRoot = FindStageOneWorldRoot(controllerObject.transform);

            if (worldRoot == null)
            {
                throw new InvalidDataException("Stage1 build failed: runtime world root was not generated.");
            }

            worldRoot.name = "Stage1 Vertical Slice Editable World";
            int removedMissingScripts = StripMissingScriptsRecursive(worldRoot);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(worldRoot, PrefabPath);

            if (savedPrefab == null)
            {
                throw new InvalidDataException("Stage1 build failed: prefab was not saved. Removed missing scripts: " + removedMissingScripts + ".");
            }

            Selection.activeGameObject = worldRoot;

            EditorSceneManager.SaveScene(scene, ScenePath);
            WriteBakeStamp(worldRoot, removedMissingScripts);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("Stage1 vertical slice scene built: " + ScenePath + " and " + PrefabPath);
        }

        [MenuItem("Gangland/Capture Stage1 Vertical Slice Screenshot")]
        public static void CaptureStageOneVerticalSliceScreenshot()
        {
            if (!StageOneAuthoringAssetsExist())
            {
                BuildStageOneVerticalSliceScene();
            }

            EditorSceneManager.OpenScene(ScenePath);
            Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();

            if (camera == null)
            {
                throw new InvalidDataException("Stage1 screenshot failed: no camera found in " + ScenePath + ".");
            }

            string absoluteScreenshotPath = Path.GetFullPath(ScreenshotPath);
            string directory = Path.GetDirectoryName(absoluteScreenshotPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            RenderTexture renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
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

            Debug.Log("Stage1 vertical slice screenshot saved: " + absoluteScreenshotPath);
        }

        [MenuItem("Gangland/Build Stage1 Vertical Slice Scene", true)]
        private static bool CanBuildStageOneVerticalSliceScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        public static bool StageOneAuthoringAssetsExist()
        {
            return File.Exists(ScenePath) && File.Exists(PrefabPath) && File.Exists(BakeStampPath);
        }

        private static GameObject FindStageOneWorldRoot(Transform controllerRoot)
        {
            foreach (Transform child in controllerRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Runtime Map") || child.name.Contains("Editable World"))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static int StripMissingScriptsRecursive(GameObject root)
        {
            int removed = 0;

            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            }

            return removed;
        }

        private static GameObject CreateCamera()
        {
            GameObject cameraObject = new GameObject("Stage1 Review Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 12.8f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 1f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, -13.6f, -16.2f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0.3f));
            return cameraObject;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Stage1 Review Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(0.88f, 0.94f, 1f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -24f, 18f);
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static void EnsureDirectory(string assetPath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            Directory.CreateDirectory(fullPath);
        }

        private static void WriteBakeStamp(GameObject worldRoot, int removedMissingScripts)
        {
            EnsureDirectory("Assets/_Project/Docs");
            int objectCount = worldRoot.GetComponentsInChildren<Transform>(true).Length - 1;
            int anchorCount = worldRoot.GetComponentsInChildren<VerticalSliceStageOneAnchor>(true).Length;
            string stamp = "Stage1 vertical slice bake\n"
                + "Time: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n"
                + "Scene: " + ScenePath + "\n"
                + "Prefab: " + PrefabPath + "\n"
                + "World objects: " + objectCount + "\n"
                + "Editable anchors: " + anchorCount + "\n";
            stamp += "Removed missing scripts: " + removedMissingScripts + "\n";
            File.WriteAllText(BakeStampPath, stamp);
        }
    }
}
