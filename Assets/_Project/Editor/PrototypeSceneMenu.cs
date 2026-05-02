using GanglandUndercover.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class PrototypeSceneMenu
    {
        [MenuItem("Gangland/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject bootstrap = new GameObject("PrototypeBootstrap");
            bootstrap.AddComponent<PrototypeBootstrap>();

            const string scenePath = "Assets/_Project/Scenes/Prototype.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            Selection.activeGameObject = bootstrap;
        }
    }
}
