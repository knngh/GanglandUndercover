using System;
using System.Reflection;
using GanglandUndercover.Online;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GanglandUndercover.Gameplay
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private static readonly Vector3 DemoCameraPosition = new Vector3(0f, -13.5f, -13.5f);
        private static readonly Vector3 DemoCameraTarget = new Vector3(0f, 0f, -0.15f);

        private void Awake()
        {
            EnsureEventSystem();
            EnsureCamera();
            EnsureLight();

#if UNITY_EDITOR
            Type mirrorType = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                mirrorType = assembly.GetType("GanglandUndercover.Editor.QuaterniusRuntimeResourceMirror");

                if (mirrorType != null)
                {
                    break;
                }
            }

            mirrorType?.GetMethod("SyncRuntimeResources", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
#endif

            BuildOnlinePrototype();
        }

        private static void BuildOnlinePrototype()
        {
            if (FindExisting<OnlineMatchController>() != null)
            {
                return;
            }

            GameObject onlineObject = new GameObject("Port Undercover Online");
            onlineObject.AddComponent<UnityServiceBootstrap>();
            onlineObject.AddComponent<OnlineMatchController>();
        }

        private static void EnsureEventSystem()
        {
            if (FindExisting<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 9.25f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.105f, 0.11f, 1f);
            cameraObject.transform.position = DemoCameraPosition;
            cameraObject.transform.LookAt(DemoCameraTarget);
        }

        private static void EnsureLight()
        {
            if (FindExisting<Light>() != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.color = new Color(1f, 0.92f, 0.76f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 20f);
        }

        private static T FindExisting<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}
