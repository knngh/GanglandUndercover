using System;
using System.Linq;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class PrototypeSmokeTests
    {
        [MenuItem("Gangland/Run Smoke Tests")]
        public static void Run()
        {
            RunFactionPath(Faction.Gang);
            RunFactionPath(Faction.Police);
            RunFactionPath(Faction.Undercover);
            ValidatePrototypeScene();
            Debug.Log("Gangland smoke tests passed.");
        }

        private static void RunFactionPath(Faction faction)
        {
            GameController controller = new GameController();
            controller.SelectFaction(faction);

            for (int i = 0; i < 6 && controller.State.Phase != GamePhase.GameOver; i++)
            {
                DistrictType district = controller.State.Districts[i % controller.State.Districts.Count].Type;
                PlayerAction action = controller.Actions.GetActionsFor(faction).First();
                controller.RunPlayerAction(district, action);
            }

            if (controller.State.Phase == GamePhase.RoleSelect)
            {
                throw new InvalidOperationException("Smoke test failed: faction was not selected.");
            }

            if (controller.State.Day < 1)
            {
                throw new InvalidOperationException("Smoke test failed: invalid day count.");
            }

            if (controller.State.Log.Count == 0)
            {
                throw new InvalidOperationException("Smoke test failed: no log entries were produced.");
            }
        }

        private static void ValidatePrototypeScene()
        {
            const string scenePath = "Assets/_Project/Scenes/Prototype.unity";
            EditorSceneManager.OpenScene(scenePath);

            if (UnityEngine.Object.FindObjectOfType<PrototypeBootstrap>() == null)
            {
                throw new InvalidOperationException("Smoke test failed: Prototype scene has no PrototypeBootstrap.");
            }

            if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != scenePath)
            {
                throw new InvalidOperationException("Smoke test failed: Prototype scene is not in Build Settings.");
            }
        }
    }
}
