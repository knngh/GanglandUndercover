using GanglandUndercover.SocialDeduction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GanglandUndercover.Gameplay
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureEventSystem();
            EnsureCamera();
            EnsureLight();

            GameObject controllerObject = new GameObject("Social Deduction Prototype");
            controllerObject.AddComponent<SocialPrototypeController>();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
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
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 4.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.065f, 0.06f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureLight()
        {
            if (FindObjectOfType<Light>() != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }
    }
}
