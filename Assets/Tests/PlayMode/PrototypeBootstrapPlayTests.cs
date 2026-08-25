using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GanglandUndercover.PlayTests
{
    public sealed class PrototypeBootstrapPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string BootstrapTypeName = "GanglandUndercover.Gameplay.PrototypeBootstrap";
        private const string EditorAssemblyName = "Assembly-CSharp-Editor";
        private const string ResourceMirrorTypeName = "GanglandUndercover.Editor.QuaterniusRuntimeResourceMirror";

        [UnityTest]
        public IEnumerator StartingPrototype_DoesNotSynchronizeEditorResources()
        {
            var messages = new List<string>();
            HashSet<int> existingObjectIds = CaptureGameObjectIds();
            Application.LogCallback capture = (message, _, _) => messages.Add(message);
            Application.logMessageReceived += capture;

            try
            {
                Type bootstrapType = Type.GetType($"{BootstrapTypeName}, {RuntimeAssemblyName}");
                Assert.IsNotNull(bootstrapType, $"Could not find runtime type {BootstrapTypeName}.");

                GameObject bootstrapObject = new GameObject("PrototypeBootstrapPlayTests Bootstrap");
                bootstrapObject.AddComponent(bootstrapType);
                yield return null;

                Assert.IsFalse(
                    messages.Exists(message => message.StartsWith("Quaternius runtime resource", StringComparison.Ordinal)),
                    "PrototypeBootstrap.Awake() must not call the editor resource mirror.");
            }
            finally
            {
                Application.logMessageReceived -= capture;
                DestroyGameObjectsCreatedAfter(existingObjectIds);
            }
        }

        [UnityTest]
        public IEnumerator ResourceMirror_WhenPlaying_DoesNotRefreshAssetDatabase()
        {
            var messages = new List<string>();
            Application.LogCallback capture = (message, _, _) => messages.Add(message);
            Application.logMessageReceived += capture;

            try
            {
                Type mirrorType = Type.GetType($"{ResourceMirrorTypeName}, {EditorAssemblyName}");
                Assert.IsNotNull(mirrorType, $"Could not find editor type {ResourceMirrorTypeName}.");

                var syncMethod = mirrorType.GetMethod("SyncRuntimeResources");
                Assert.IsNotNull(syncMethod, "Could not find SyncRuntimeResources on the editor resource mirror.");
                syncMethod.Invoke(null, null);
                yield return null;

                Assert.IsFalse(
                    messages.Exists(message => message.StartsWith("Quaternius runtime resources synced:", StringComparison.Ordinal)),
                    "The resource mirror must not refresh the AssetDatabase while Unity is in Play Mode.");
                Assert.IsTrue(
                    messages.Exists(message => message.StartsWith("Quaternius runtime resource mirror skipped:", StringComparison.Ordinal)),
                    "The resource mirror should report that synchronization was skipped in Play Mode.");
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }
        }

        private static HashSet<int> CaptureGameObjectIds()
        {
            var ids = new HashSet<int>();
            foreach (GameObject gameObject in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                ids.Add(gameObject.GetInstanceID());
            }

            return ids;
        }

        private static void DestroyGameObjectsCreatedAfter(HashSet<int> existingObjectIds)
        {
            GameObject[] gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int index = gameObjects.Length - 1; index >= 0; index--)
            {
                GameObject gameObject = gameObjects[index];
                if (gameObject != null && !existingObjectIds.Contains(gameObject.GetInstanceID()))
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
