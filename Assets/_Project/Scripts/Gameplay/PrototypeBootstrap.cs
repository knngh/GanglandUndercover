using System;
using System.Reflection;
using GanglandUndercover.Online;
using GanglandUndercover.SocialDeduction;
using GanglandUndercover.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GanglandUndercover.Gameplay
{
    public enum GameMode
    {
        Offline,
        Online
    }

    /// <summary>
    /// 场景启动引导器 — 第 8 阶段改造后，改为菜单驱动架构。
    /// Awake 中创建 MainMenuController 作为场景入口，不再直接启动游戏。
    /// 提供公共方法供 MainMenuController / LobbyController / GameOverController 调用。
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private static readonly Vector3 DemoCameraPosition = new Vector3(0f, -13.5f, -13.5f);
        private static readonly Vector3 DemoCameraTarget = new Vector3(0f, 0f, -0.15f);

        [SerializeField] private GameMode _mode = GameMode.Online;
        [SerializeField] private SocialRole _offlinePlayerRole = SocialRole.Undercover;
        [SerializeField] private MapType _offlineMapType = MapType.GanglandDistrict;

        private MainMenuController _mainMenuController;
        private GameOverController _gameOverController;
        private LobbyController _lobbyController;

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

            // 第 8 阶段改造：不再从 _mode 直接启动游戏，改为创建主菜单
            CreateMainMenu();
        }

        private void CreateMainMenu()
        {
            GameObject menuObject = new GameObject("Main Menu");
            _mainMenuController = menuObject.AddComponent<MainMenuController>();
            _mainMenuController.Initialize(this);
            DontDestroyOnLoad(menuObject);
        }

        /// <summary>
        /// 由 MainMenuController 调用：启动离线模式游戏。
        /// </summary>
        public void StartOfflineGame(SocialRole role, MapType mapType = MapType.GanglandDistrict)
        {
            DestroyActiveGame();
            _offlinePlayerRole = role;
            _offlineMapType = mapType;
            BuildOfflinePrototype();
            CreateGameOverController();
        }

        /// <summary>
        /// 由 MainMenuController 调用：启动联机模式游戏。
        /// </summary>
        public void StartOnlineGame()
        {
            DestroyActiveGame();
            BuildOnlinePrototype();
            CreateGameOverController();
            CreateLobbyController();
        }

        /// <summary>
        /// 由 GameOverController / LobbyController 调用：销毁当前游戏对象，返回主菜单。
        /// </summary>
        public void ReturnToMainMenu()
        {
            DestroyActiveGame();

            if (_gameOverController != null)
            {
                DestroyController(_gameOverController);
                _gameOverController = null;
            }

            if (_lobbyController != null)
            {
                DestroyController(_lobbyController);
                _lobbyController = null;
            }

            if (_mainMenuController != null)
            {
                _mainMenuController.Show();
            }
        }

        private void DestroyActiveGame()
        {
            var offline = FindExisting<SocialPrototypeController>();
            if (offline != null)
            {
                DestroyControllerObject(offline);
            }

            var online = FindExisting<OnlineMatchController>();
            if (online != null)
            {
                DestroyControllerObject(online);
            }

            var service = FindExisting<UnityServiceBootstrap>();
            if (service != null)
            {
                DestroyControllerObject(service);
            }

            var sync = FindExisting<OnlineSyncManager>();
            if (sync != null)
            {
                DestroyControllerObject(sync);
            }

            if (_gameOverController != null)
            {
                DestroyController(_gameOverController);
                _gameOverController = null;
            }

            if (_lobbyController != null)
            {
                DestroyController(_lobbyController);
                _lobbyController = null;
            }
        }

        private void BuildOfflinePrototype()
        {
            var existingOnline = FindExisting<OnlineMatchController>();
            if (existingOnline != null)
            {
                DestroyControllerObject(existingOnline);
            }

            var existingService = FindExisting<UnityServiceBootstrap>();
            if (existingService != null)
            {
                DestroyControllerObject(existingService);
            }

            if (FindExisting<SocialPrototypeController>() != null)
            {
                return;
            }

            GameObject offlineObject = new GameObject("Port Undercover Offline");
            var controller = offlineObject.AddComponent<SocialPrototypeController>();
            controller.AutoStartOnAwake = false;
            controller.SetMapType(_offlineMapType);

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(offlineObject);
                controller.StartOfflineMode(_offlinePlayerRole);
            }
        }

        private void BuildOnlinePrototype()
        {
            if (FindExisting<OnlineMatchController>() != null)
            {
                return;
            }

            var existingOffline = FindExisting<SocialPrototypeController>();
            if (existingOffline != null)
            {
                DestroyControllerObject(existingOffline);
            }

            GameObject onlineObject = new GameObject("Port Undercover Online");
            onlineObject.AddComponent<UnityServiceBootstrap>();
            onlineObject.AddComponent<OnlineMatchController>();
            onlineObject.AddComponent<OnlineSyncManager>();
        }

        private void CreateGameOverController()
        {
            if (_gameOverController != null)
            {
                DestroyController(_gameOverController);
            }

            GameObject go = new GameObject("Game Over Controller");
            _gameOverController = go.AddComponent<GameOverController>();
            _gameOverController.Initialize(this);
            DontDestroyOnLoad(go);
        }

        private void CreateLobbyController()
        {
            if (_lobbyController != null)
            {
                DestroyController(_lobbyController);
            }

            GameObject go = new GameObject("Lobby Controller");
            _lobbyController = go.AddComponent<LobbyController>();
            _lobbyController.Initialize(this, FindExisting<OnlineSyncManager>());
            DontDestroyOnLoad(go);
        }

        private static void DestroyController(MonoBehaviour controller)
        {
            if (controller == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(controller.gameObject);
            }
            else
            {
                DestroyImmediate(controller.gameObject);
            }
        }

        private static void DestroyControllerObject(MonoBehaviour controller)
        {
            if (controller == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(controller.gameObject);
            }
            else
            {
                DestroyImmediate(controller.gameObject);
            }
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
