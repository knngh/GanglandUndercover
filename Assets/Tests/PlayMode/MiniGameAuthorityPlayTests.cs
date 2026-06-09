using System;
using System.Collections;
using System.Collections.Generic;
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
    public class MiniGameAuthorityPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private const string BridgeTypeName = "GanglandUndercover.Online.MiniGames.OnlineMiniGameBridge";

        private readonly List<GameObject> _ownedObjects = new List<GameObject>();
        private Type _controllerType;
        private Type _bridgeType;
        private MonoBehaviour _serverController;
        private NetworkManager _serverNetworkManager;
        private NetworkManager _clientNetworkManager;
        private ushort _port;

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            _bridgeType = Type.GetType($"{BridgeTypeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(_controllerType, $"找不到运行时类型 {ControllerTypeName}。");
            Assert.IsNotNull(_bridgeType, $"找不到运行时类型 {BridgeTypeName}。");

            DestroyObjectsOfType(typeof(NetworkManager));
            DestroyObjectsOfType(_bridgeType);
            DestroyObjectsOfType(_controllerType);

            _port = AllocateUdpPort();
            _serverNetworkManager = CreateNetworkManager("MiniGameAuthority_ServerNetworkManager", _port, true);

            GameObject controllerObject = new GameObject("MiniGameAuthority_ServerController");
            _ownedObjects.Add(controllerObject);
            _serverController = (MonoBehaviour)controllerObject.AddComponent(_controllerType);
            Assert.IsNotNull(_serverController, "无法挂载 OnlineMatchController。");
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

            DestroyObjectsOfType(_bridgeType);
            DestroyTemplateField("miniGameBridgeTemplate");
            DestroyTemplateField("surveillanceCameraTemplate");

            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
                }
            }
        }

        [UnityTest]
        public IEnumerator MiniGameBridge_RejectsUnopenedTask_AndCompletesServerOpenedTaskOverRpc()
        {
            yield return null;

            Assert.IsTrue(_serverNetworkManager.StartHost(), "Host NetworkManager 应能启动。");

            yield return WaitUntilOrFail(
                () => FindBridge(b => b.IsSpawned && b.IsServer) != null,
                "Host 启动后应生成已 Spawn 的 MiniGameBridge NetworkObject。");

            _clientNetworkManager = CreateNetworkManager("MiniGameAuthority_ClientNetworkManager", _port, false);
            CopyServerNetworkPrefabs(_clientNetworkManager);
            Assert.IsTrue(_clientNetworkManager.StartClient(), "Client NetworkManager 应能启动。");

            yield return WaitUntilOrFail(
                () => _clientNetworkManager.IsConnectedClient
                    && FindBridge(b => b.IsSpawned && b.IsClient && !b.IsServer) != null,
                "Client 应连接并收到已 Spawn 的 MiniGameBridge NetworkObject。");

            ulong clientId = _clientNetworkManager.LocalClientId;
            const int taskId = 0;
            PlaceClientAtSingleTask(clientId, taskId, sabotaged: false);
            SetPhase("Action");

            NetworkBehaviour clientBridge = FindBridge(b => b.IsSpawned && b.IsClient && !b.IsServer);
            InvokeBridgeRpc(clientBridge, "SubmitTaskResultServerRpc", taskId, true);
            yield return RunFrames(12);

            Assert.IsFalse(GetTaskCompleted(taskId), "未由服务器打开/锁定的任务结果必须被拒绝。");

            ApplyClientInteract(clientId);

            yield return WaitUntilOrFail(
                () => TaskLockOwnedBy(taskId, clientId),
                "服务器收到 Interact 后应通过 MiniGameBridge 打开小游戏并写入任务锁。");

            Assert.IsFalse(GetTaskCompleted(taskId), "服务器打开小游戏不能直接完成任务，必须等待 Client 结果 RPC。");

            InvokeBridgeRpc(clientBridge, "SubmitTaskResultServerRpc", taskId, true);

            yield return WaitUntilOrFail(
                () => GetTaskCompleted(taskId),
                "Client 通过真实 ServerRpc 提交后，服务器应完成被打开的任务。");
        }

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

            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                TickRate = 30,
                PlayerPrefab = null
            };

            return manager;
        }

        private void CopyServerNetworkPrefabs(NetworkManager client)
        {
            foreach (NetworkPrefab prefab in _serverNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                client.NetworkConfig.Prefabs.Add(prefab);
            }
        }

        private NetworkBehaviour FindBridge(Func<NetworkBehaviour, bool> predicate)
        {
            UnityEngine.Object[] bridges = UnityEngine.Object.FindObjectsByType(_bridgeType);

            foreach (UnityEngine.Object bridge in bridges)
            {
                if (bridge is NetworkBehaviour networkBehaviour && predicate(networkBehaviour))
                {
                    return networkBehaviour;
                }
            }

            return null;
        }

        private void InvokeBridgeRpc(NetworkBehaviour bridge, string methodName, int taskId, bool success)
        {
            Assert.IsNotNull(bridge, $"找不到可调用 {methodName} 的 MiniGameBridge。");
            _bridgeType.GetMethod(methodName).Invoke(bridge, new object[] { taskId, success, default(RpcParams) });
        }

        private void ApplyClientInteract(ulong clientId)
        {
            object action = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineActionType"), "Interact");
            _controllerType.GetMethod("ApplyClientAction", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_serverController, new[] { clientId, action, 0UL });
        }

        private void PlaceClientAtSingleTask(ulong clientId, int taskId, bool sabotaged)
        {
            object mapService = GetField("mapService");
            Vector3 taskPosition = (Vector3)mapService.GetType().GetMethod("TaskPositionFor").Invoke(mapService, new object[] { taskId });

            Type taskType = RuntimeType("GanglandUndercover.Online.OnlineTaskState");
            object task = Activator.CreateInstance(taskType, taskId, "Task" + taskId, taskPosition, 0, 1, false, sabotaged);
            IList tasks = (IList)GetField("tasks");
            tasks.Clear();
            tasks.Add(task);

            Type playerType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            object player = Activator.CreateInstance(
                playerType,
                clientId,
                "Client" + clientId,
                taskPosition,
                true,
                true,
                Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Unassigned"),
                Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineProfession"), "Inspector"),
                0,
                false);

            IDictionary players = (IDictionary)GetField("players");
            players[clientId] = player;

            IDictionary privateRoles = (IDictionary)GetField("privateRoles");
            privateRoles[clientId] = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Police");
        }

        private void SetPhase(string phaseName)
        {
            FieldInfo field = _controllerType.GetField("phase", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field.SetValue(_serverController, Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), phaseName));
        }

        private bool GetTaskCompleted(int taskId)
        {
            IList tasks = (IList)GetField("tasks");

            foreach (object task in tasks)
            {
                if ((int)task.GetType().GetField("Id").GetValue(task) == taskId)
                {
                    return (bool)task.GetType().GetField("Completed").GetValue(task);
                }
            }

            return false;
        }

        private bool TaskLockOwnedBy(int taskId, ulong clientId)
        {
            IDictionary locks = GetField("activeTaskUsers") as IDictionary;
            return locks != null && locks.Contains(taskId) && (ulong)locks[taskId] == clientId;
        }

        private object GetField(string fieldName)
        {
            return _controllerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(_serverController);
        }

        private Type RuntimeType(string typeName)
        {
            Type type = Type.GetType($"{typeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(type, $"找不到运行时类型 {typeName}。");
            return type;
        }

        private void DestroyTemplateField(string fieldName)
        {
            if (_serverController == null)
            {
                return;
            }

            FieldInfo field = _controllerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            GameObject template = field?.GetValue(_serverController) as GameObject;

            if (template != null && template.scene.IsValid())
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        private static void DestroyObjectsOfType(Type type)
        {
            if (type == null)
            {
                return;
            }

            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsByType(type);
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                if (objects[i] is Component component && component.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
                }
            }
        }

        private static ushort AllocateUdpPort()
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        }

        private static IEnumerator RunFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntilOrFail(Func<bool> condition, string message, float timeoutSeconds = 5f)
        {
            float startedAt = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(message);
        }
    }
}
