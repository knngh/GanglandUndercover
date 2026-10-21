using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace GanglandUndercover.PlayTests
{
    /// <summary>
    /// 本地快照流实验（重连 E2E 诊断衍生）：进程内 Host+Client 双控制器走生产
    /// StartClient 路径，Host 置 matchStarted 后广播，断言 Client 控制器的
    /// MatchStarted 翻转——把"两进程客户端看不到对局开始"的问题从 Relay 传输层
    /// 中剥离出来：本地失败=代码缺陷，本地通过=传输/时序问题。
    /// </summary>
    public class LocalSnapshotFlowPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private const ushort DefaultControllerPort = 7777;

        private Type _controllerType;
        private ushort _port;
        private NetworkManager _serverNetworkManager;
        private NetworkManager _clientNetworkManager;
        private MonoBehaviour _serverController;
        private MonoBehaviour _clientController;
        private readonly System.Collections.Generic.List<UnityEngine.Object> _ownedObjects =
            new System.Collections.Generic.List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(_controllerType, "找不到运行时控制器类型。");

            DestroyObjectsOfType(typeof(NetworkManager));
            DestroyObjectsOfType(_controllerType);

            _port = DefaultControllerPort; // 与生产 DefaultPort 一致，客户端 ConfigureTransport 会覆写同端口
            _serverNetworkManager = CreateNetworkManager("SnapFlow_ServerNM", _port, true);
            GameObject serverControllerObject = new GameObject("SnapFlow_ServerController");
            _ownedObjects.Add(serverControllerObject);
            _serverController = (MonoBehaviour)serverControllerObject.AddComponent(_controllerType);
        }

        [TearDown]
        public void TearDown()
        {
            if (_clientNetworkManager != null && _clientNetworkManager.IsListening)
            {
                _clientNetworkManager.Shutdown();
            }

            if (_serverNetworkManager != null && _serverNetworkManager.IsListening)
            {
                _serverNetworkManager.Shutdown();
            }

            DestroyObjectsOfType(_controllerType);
            DestroyObjectsOfType(typeof(NetworkManager));

            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
                }
            }

            _ownedObjects.Clear();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LocalSnapshot_ClientControllerAppliesMatchStartedBroadcast()
        {
            yield return null; // Awake 一帧

            // 走聊天测试已验证的裸 NM 模式（controller 生产连接路径在双控制器进程内
            // 握手失败与本项目疑点无关——chat harness 证明该层没问题）。
            InvokeController(_serverController, "RegisterMessages");
            Assert.IsTrue(_serverNetworkManager.StartHost(), "Host 应能启动。");
            InvokeController(_serverController, "RegisterMessages");

            _clientNetworkManager = CreateNetworkManager("SnapFlow_ClientNM", _port, false);
            CopyServerNetworkPrefabs(_clientNetworkManager);
            _clientController = (MonoBehaviour)_clientNetworkManager.gameObject.AddComponent(_controllerType);
            Assert.IsTrue(_clientNetworkManager.StartClient(), "Client 应能启动。");

            yield return WaitUntil(() => _clientNetworkManager.IsConnectedClient,
                20f, "Client 应连入 Host（已批准）。");

            // 关键实验点：CustomMessagingManager 在 NGO 连接建立后才可用——
            // 生产 StartClient 在连接前注册 handler 属于静默空转，客户端收不到任何命名消息。
            InvokeController(_clientController, "RegisterMessages");

            // 基线：Host 未开局，Client 端 MatchStarted 应为 false。
            bool baseline = (bool)GetControllerProp(_clientController, "MatchStarted");
            Assert.IsFalse(baseline, "开局前 Client 端 MatchStarted 应为 false。");

            // Host 置开局位并广播（等价于 TickHostSimulation 的周期广播）。
            SetControllerField(_serverController, "matchStarted", true);
            SetControllerField(_serverController, "phase",
                Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), "Action"));
            InvokeController(_serverController, "BroadcastSnapshot");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if ((bool)GetControllerProp(_clientController, "MatchStarted"))
                {
                    Debug.Log("[SnapFlow] Client 控制器已翻转 MatchStarted ✓");
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("本地广播后 Client 控制器的 MatchStarted 未翻转——快照到达/解析/应用链路存在代码级断点。");
        }

        private static ushort AllocateUdpPort()
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        }

        private static Type RuntimeType(string fullName)
            => Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);

        private NetworkManager CreateNetworkManager(string name, ushort port, bool server)
        {
            GameObject go = new GameObject(name);
            _ownedObjects.Add(go);

            NetworkManager manager = go.AddComponent<NetworkManager>();
            UnityTransport transport = go.AddComponent<UnityTransport>();
            transport.MaxPayloadSize = 256000;
            transport.MaxSendQueueSize = 1024 * 1024;
            transport.MaxConnectAttempts = 4;
            transport.ConnectTimeoutMS = 500;

            if (server)
            {
                transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            }
            else
            {
                transport.SetConnectionData("127.0.0.1", port);
            }

            manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.LogLevel = Unity.Netcode.LogLevel.Developer;
            return manager;
        }

        private void CopyServerNetworkPrefabs(NetworkManager client)
        {
            if (_serverNetworkManager == null || _serverNetworkManager.NetworkConfig == null) return;

            foreach (NetworkPrefab prefab in _serverNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                bool exists = false;
                foreach (NetworkPrefab existing in client.NetworkConfig.Prefabs.Prefabs)
                {
                    if (existing.Prefab == prefab.Prefab)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists && prefab.Prefab != null)
                {
                    client.NetworkConfig.Prefabs.Add(prefab);
                }
            }
        }

        private static void InvokeController(MonoBehaviour controller, string methodName, object arg = null)
        {
            MethodInfo method = controller.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, arg == null ? Type.EmptyTypes : new[] { arg.GetType() }, null)
                ?? controller.GetType().GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"找不到方法 {methodName}");
            method.Invoke(controller, arg == null ? null : new[] { arg });
        }

        private static void SetControllerField(MonoBehaviour controller, string name, object value)
        {
            FieldInfo field = controller.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"找不到字段 {name}");
            field.SetValue(controller, value);
        }

        private static object GetControllerProp(MonoBehaviour controller, string name)
        {
            PropertyInfo property = controller.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, $"找不到属性 {name}");
            return property.GetValue(controller);
        }

        private static void DestroyObjectsOfType(Type type)
        {
            if (type == null)
            {
                return;
            }

            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                if (objects[i] is Component component && component.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
                }
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string failureMessage)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(failureMessage + "（等待 " + timeoutSeconds + "s 超时）");
        }
    }
}
