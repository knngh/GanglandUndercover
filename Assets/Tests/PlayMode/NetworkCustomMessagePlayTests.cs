using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace GanglandUndercover.PlayTests
{
    public class NetworkCustomMessagePlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private const string CharacterCustomizerTypeName = "GanglandUndercover.SocialDeduction.CharacterCustomizer";

        private const string ClientStateMessage = "GanglandClientState";
        private const string ClientActionMessage = "GanglandClientAction";
        private const string ClientProfileMessage = "GanglandClientProfile";
        private const string CharacterCustomMessage = "GanglandCharacterCustom";
        private const string ServerSnapshotMessage = "GanglandServerSnapshot";
        private const string RoleAssignMessage = "GanglandRoleAssign";
        private const string ChatSendMessage = "GanglandChatSend";
        private const string ChatBroadcastMessage = "GanglandChatBroadcast";
        private const string MapSelectMessage = "GanglandMapSelect";

        private readonly List<GameObject> _ownedObjects = new List<GameObject>();
        private Type _controllerType;
        private Type _customizerType;
        private MonoBehaviour _serverController;
        private NetworkManager _serverNetworkManager;
        private NetworkManager _clientNetworkManager;
        private ushort _port;

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            _customizerType = Type.GetType($"{CharacterCustomizerTypeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(_controllerType, $"找不到运行时类型 {ControllerTypeName}。");
            Assert.IsNotNull(_customizerType, $"找不到运行时类型 {CharacterCustomizerTypeName}。");

            DestroyObjectsOfType(typeof(NetworkManager));
            DestroyObjectsOfType(_customizerType);
            DestroyObjectsOfType(_controllerType);

            _port = AllocateUdpPort();
            _serverNetworkManager = CreateNetworkManager("CustomMessageTest_ServerNetworkManager", _port, true);

            GameObject controllerObject = new GameObject("CustomMessageTest_ServerController");
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

            DestroyTemplateField("surveillanceCameraTemplate");
            DestroyTemplateField("miniGameBridgeTemplate");
            DestroyTemplateField("characterCustomizerTemplate");
            DestroyObjectsOfType(_customizerType);

            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
                }
            }
        }

        [UnityTest]
        public IEnumerator CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode()
        {
            yield return null;

            InvokePrivate("RegisterMessages");
            Assert.IsTrue(_serverNetworkManager.StartHost(), "Host NetworkManager 应能启动。");
            InvokePrivate("RegisterMessages");

            _clientNetworkManager = CreateNetworkManager("CustomMessageTest_ClientNetworkManager", _port, false);
            CopyServerNetworkPrefabs(_clientNetworkManager);
            Assert.IsTrue(_clientNetworkManager.StartClient(), "Client NetworkManager 应能启动。");

            yield return WaitUntilOrFail(
                () => _clientNetworkManager.IsConnectedClient
                    && ServerHasClient(_clientNetworkManager.LocalClientId)
                    && DictionaryContainsKey(GetField("players"), _clientNetworkManager.LocalClientId),
                "Client 应通过本地 UnityTransport 连接到 Host。");

            ulong clientId = _clientNetworkManager.LocalClientId;

            Vector3 acceptedPosition = new Vector3(1.25f, -0.75f, 0f);
            Vector2 acceptedInput = new Vector2(0.25f, 0.5f);
            SendClientState(acceptedPosition, acceptedInput, true);

            yield return WaitUntilOrFail(
                () => GetPlayerReady(clientId) && Approximately(GetPlayerPosition(clientId), acceptedPosition),
                "合法 ClientState 应通过真实 named-message 路径更新服务器玩家状态。");

            SendClientProfileFromClient("超长玩家代号ABCDEFGHIJKLMNOPQRSTUV");
            yield return WaitUntilOrFail(
                () => GetPlayerDisplayName(clientId).Length <= 16 && GetPlayerDisplayName(clientId).StartsWith("超长玩家代号"),
                "合法但过长的 ClientProfile 应被服务器截断为安全显示名。");

            string stableDisplayName = GetPlayerDisplayName(clientId);
            SendMalformedClientProfileFromClient();
            yield return RunFrames(8);
            Assert.AreEqual(stableDisplayName, GetPlayerDisplayName(clientId),
                "畸形 ClientProfile payload 不应覆盖服务器玩家显示名。");

            Vector3 stablePosition = GetPlayerPosition(clientId);
            Vector2 stableInput = GetPlayerInput(clientId);

            SendClientState(
                new Vector3(float.NaN, 12f, 0f),
                new Vector2(float.PositiveInfinity, -3f),
                false);
            yield return RunFrames(8);

            Assert.IsTrue(GetPlayerReady(clientId), "畸形 ClientState 不应覆盖 Ready 状态。");
            AssertVectorClose(stablePosition, GetPlayerPosition(clientId), "畸形 ClientState 不应覆盖位置。");
            AssertVectorClose(stableInput, GetPlayerInput(clientId), "畸形 ClientState 不应覆盖输入。");

            SetPhase("Meeting");
            ClearVotes();
            SendClientAction(4, ulong.MaxValue);
            yield return WaitUntilOrFail(
                () => GetCollectionCount(GetField("votes")) == 1,
                "合法 ClientAction 应能通过真实 named-message 路径进入投票表。");

            SetPhase("Meeting");
            ClearVotes();
            SendClientAction(999, 0UL);
            yield return RunFrames(8);
            Assert.AreEqual(0, GetCollectionCount(GetField("votes")), "未定义 ClientAction enum 不应产生投票或行动副作用。");

            string roleBefore = GetProp("LocalRole").ToString();
            SendRoleAssignFromClient(3);
            yield return RunFrames(8);
            Assert.AreEqual(roleBefore, GetProp("LocalRole").ToString(), "Client 伪造 RoleAssign 不应改变服务器本机身份。");

            object mapService = GetField("mapService");
            PropertyInfo activeMapType = mapService.GetType().GetProperty("ActiveMapType");
            object mapBefore = activeMapType.GetValue(mapService);
            SendMapSelectFromClient(1);
            yield return RunFrames(8);
            Assert.AreEqual(mapBefore, activeMapType.GetValue(mapService), "Client 伪造 MapSelect 不应改变服务器地图。");

            SetField("matchStarted", false);
            SetPhase("Lobby");
            SendServerSnapshotFromClient();
            yield return RunFrames(8);
            Assert.IsFalse((bool)GetProp("MatchStarted"), "Client 伪造 ServerSnapshot 不应改变服务器对局启动状态。");
            Assert.AreEqual("Lobby", GetProp("Phase").ToString(), "Client 伪造 ServerSnapshot 不应改变服务器阶段。");

            ClearServerChat();
            SetPhase("Action");
            SendChatBroadcastFromClient("伪造广播|不应显示");
            yield return RunFrames(8);
            Assert.AreEqual(0, GetServerChatMessageCount(), "Client 伪造 ChatBroadcast 不应污染 Host/Server 本地聊天。");

            SetPhase("Lobby");
            SendChatSendFromClient("大厅阶段|不应显示");
            yield return RunFrames(8);
            Assert.AreEqual(0, GetServerChatMessageCount(), "Lobby 阶段的 ChatSend 不应进入服务器聊天。");

            SetPhase("Action");
            SendChatSendFromClient("<b>码头|安全</b>");
            yield return WaitUntilOrFail(
                () => ServerChatContainsContent("码头|安全"),
                "合法 ChatSend 应通过真实 named-message 路径到达服务器并完成清洗。");

            int chatCountAfterFirstAccepted = GetServerChatMessageCount();
            SendChatSendFromClient("刷屏|不应显示");
            yield return RunFrames(8);
            Assert.AreEqual(chatCountAfterFirstAccepted, GetServerChatMessageCount(), "连续 ChatSend 应被服务器限流拒绝。");
            Assert.IsFalse(ServerChatContainsContent("刷屏|不应显示"), "被限流的 ChatSend 不应进入服务器聊天。");
        }

        [UnityTest]
        public IEnumerator CharacterCustomizer_SpawnsOwnerObjectsAndRejectsNonOwnerPayload()
        {
            yield return null;

            Assert.IsTrue(_serverNetworkManager.StartHost(), "Host NetworkManager 应能启动。");

            yield return WaitUntilOrFail(
                () => FindCustomizer(c => c.IsSpawned && c.IsServer && c.OwnerClientId == NetworkManager.ServerClientId) != null,
                "Host 启动后应生成 server-owned CharacterCustomizer NetworkObject。");

            _clientNetworkManager = CreateNetworkManager("CharacterCustom_ClientNetworkManager", _port, false);
            CopyServerNetworkPrefabs(_clientNetworkManager);
            Assert.IsTrue(_clientNetworkManager.StartClient(), "Client NetworkManager 应能启动。");

            yield return WaitUntilOrFail(
                () => _clientNetworkManager.IsConnectedClient
                    && FindCustomizer(c => c.IsSpawned && c.IsServer && c.OwnerClientId == _clientNetworkManager.LocalClientId) != null
                    && FindCustomizer(c => c.IsSpawned && c.IsClient && !c.IsServer && !c.IsOwner
                        && c.OwnerClientId == NetworkManager.ServerClientId) != null,
                "Client 连接后应收到 Host 和远端 owner 对应的 CharacterCustomizer clone。");

            NetworkBehaviour serverOwnedCustomizer = FindCustomizer(c =>
                c.IsSpawned && c.IsServer && c.OwnerClientId == NetworkManager.ServerClientId);
            NetworkBehaviour clientServerOwnedClone = FindCustomizer(c =>
                c.IsSpawned && c.IsClient && !c.IsServer && !c.IsOwner && c.OwnerClientId == NetworkManager.ServerClientId);
            string baselineJson = GetCustomizerJson(serverOwnedCustomizer);

            SendCharacterCustomFromClient(
                clientServerOwnedClone.NetworkObjectId,
                "{\"hat\":\"hat_none\",\"top\":\"top_jacket\",\"bottom\":\"bottom_pants\",\"accessory\":\"acc_none\",\"skinTone\":\"skin_light\",\"height\":\"height_m\"}");

            yield return RunFrames(16);

            Assert.AreEqual(baselineJson, GetCustomizerJson(serverOwnedCustomizer),
                "Client 伪造 server-owned CharacterCustomizer 的外观消息必须被 Host 拒绝。");
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

        private void DestroyTemplateField(string fieldName)
        {
            if (_serverController == null || _controllerType == null)
            {
                return;
            }

            FieldInfo templateField = _controllerType.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            GameObject template = templateField?.GetValue(_serverController) as GameObject;

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

        private void CopyServerNetworkPrefabs(NetworkManager client)
        {
            foreach (NetworkPrefab prefab in _serverNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                client.NetworkConfig.Prefabs.Add(prefab);
            }
        }

        private static ushort AllocateUdpPort()
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        }

        private bool ServerHasClient(ulong clientId)
        {
            foreach (ulong connectedId in _serverNetworkManager.ConnectedClientsIds)
            {
                if (connectedId == clientId)
                {
                    return true;
                }
            }

            return false;
        }

        private void SendClientState(Vector3 position, Vector2 input, bool ready)
        {
            using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(position);
            writer.WriteValueSafe(input);
            writer.WriteValueSafe(ready);
            _clientNetworkManager.CustomMessagingManager.SendNamedMessage(ClientStateMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendClientAction(int actionValue, ulong targetClientId)
        {
            using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(actionValue);
            writer.WriteValueSafe(targetClientId);
            _clientNetworkManager.CustomMessagingManager.SendNamedMessage(ClientActionMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendClientProfileFromClient(string displayName)
        {
            FastBufferWriter writer = new FastBufferWriter(512, Allocator.Temp);
            try
            {
                WriteBoundedUtf8String(ref writer, displayName);
                _clientNetworkManager.CustomMessagingManager.SendNamedMessage(ClientProfileMessage, NetworkManager.ServerClientId, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void SendMalformedClientProfileFromClient()
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(4096);
            _clientNetworkManager.CustomMessagingManager.SendNamedMessage(ClientProfileMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendRoleAssignFromClient(int roleValue)
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(roleValue);
            _clientNetworkManager.CustomMessagingManager.SendNamedMessage(RoleAssignMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendMapSelectFromClient(int mapTypeValue)
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(mapTypeValue);
            _clientNetworkManager.CustomMessagingManager.SendNamedMessage(MapSelectMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendServerSnapshotFromClient()
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(true);
            _clientNetworkManager.CustomMessagingManager.SendNamedMessage(ServerSnapshotMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendChatSendFromClient(string content)
        {
            FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp);
            try
            {
                object[] args = { writer, content };
                StaticPrivate("WriteChatSendPayload").Invoke(null, args);
                writer = (FastBufferWriter)args[0];
                _clientNetworkManager.CustomMessagingManager.SendNamedMessage(ChatSendMessage, NetworkManager.ServerClientId, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void SendChatBroadcastFromClient(string content)
        {
            FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp);
            try
            {
                object faction = Enum.Parse(RuntimeType("GanglandUndercover.Core.Faction"), "Gang");
                object channel = Enum.Parse(RuntimeType("GanglandUndercover.Online.ChatChannel"), "Ghost");
                object[] args = { writer, "999", "伪造者", content, true, faction, channel };
                StaticPrivate("WriteChatBroadcastPayload").Invoke(null, args);
                writer = (FastBufferWriter)args[0];
                _clientNetworkManager.CustomMessagingManager.SendNamedMessage(ChatBroadcastMessage, NetworkManager.ServerClientId, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void SendCharacterCustomFromClient(ulong objectId, string json)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
            using FastBufferWriter writer = new FastBufferWriter(jsonBytes.Length + 16, Allocator.Temp);
            writer.WriteValueSafe(objectId);
            writer.WriteValueSafe(jsonBytes.Length);
            writer.WriteBytesSafe(jsonBytes, jsonBytes.Length);
            _clientNetworkManager.CustomMessagingManager.SendNamedMessage(
                CharacterCustomMessage,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private IEnumerator WaitUntilOrFail(Func<bool> condition, string message, float timeoutSeconds = 4f)
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

        private static IEnumerator RunFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private object GetProp(string name)
        {
            PropertyInfo pi = _controllerType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(pi, $"找不到属性 {name}");
            return pi.GetValue(_serverController);
        }

        private object GetField(string name)
        {
            FieldInfo fi = _controllerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            return fi.GetValue(_serverController);
        }

        private NetworkBehaviour FindCustomizer(Func<NetworkBehaviour, bool> predicate)
        {
            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsByType(_customizerType, FindObjectsSortMode.None);
            foreach (UnityEngine.Object obj in objects)
            {
                if (obj is NetworkBehaviour networkBehaviour && predicate(networkBehaviour))
                {
                    return networkBehaviour;
                }
            }

            return null;
        }

        private string GetCustomizerJson(NetworkBehaviour customizer)
        {
            Assert.IsNotNull(customizer, "CharacterCustomizer 不应为空。");
            MethodInfo mi = _customizerType.GetMethod("GetCustomDataJson", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, "找不到 CharacterCustomizer.GetCustomDataJson。");
            return (string)mi.Invoke(customizer, null);
        }

        private void SetField(string name, object value)
        {
            FieldInfo fi = _controllerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            fi.SetValue(_serverController, value);
        }

        private void SetPhase(string phaseName)
        {
            Type phaseType = RuntimeType("GanglandUndercover.Online.OnlineMatchPhase");
            SetField("phase", Enum.Parse(phaseType, phaseName));
        }

        private void InvokePrivate(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"找不到私有方法 {method}");
            mi.Invoke(_serverController, null);
        }

        private MethodInfo StaticPrivate(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, $"找不到静态私有方法 {method}");
            return mi;
        }

        private object GetPlayerState(ulong clientId)
        {
            object players = GetField("players");
            MethodInfo tryGetValue = players.GetType().GetMethod("TryGetValue");
            object[] args = { clientId, null };
            bool found = (bool)tryGetValue.Invoke(players, args);
            Assert.IsTrue(found, $"找不到玩家 {clientId}");
            return args[1];
        }

        private Vector3 GetPlayerPosition(ulong clientId)
        {
            object state = GetPlayerState(clientId);
            return (Vector3)state.GetType().GetField("Position").GetValue(state);
        }

        private Vector2 GetPlayerInput(ulong clientId)
        {
            object state = GetPlayerState(clientId);
            return (Vector2)state.GetType().GetField("Input").GetValue(state);
        }

        private bool GetPlayerReady(ulong clientId)
        {
            object state = GetPlayerState(clientId);
            return (bool)state.GetType().GetField("Ready").GetValue(state);
        }

        private string GetPlayerDisplayName(ulong clientId)
        {
            object state = GetPlayerState(clientId);
            return (string)state.GetType().GetField("DisplayName").GetValue(state);
        }

        private void ClearVotes()
        {
            object votes = GetField("votes");
            votes.GetType().GetMethod("Clear").Invoke(votes, null);
        }

        private void ClearServerChat()
        {
            object chatSystem = GetServerChatSystem();
            chatSystem.GetType().GetMethod("Clear").Invoke(chatSystem, null);
        }

        private int GetServerChatMessageCount()
        {
            object chatSystem = GetServerChatSystem();
            return Convert.ToInt32(chatSystem.GetType().GetProperty("MessageCount").GetValue(chatSystem));
        }

        private bool ServerChatContainsContent(string expectedContent)
        {
            object chatSystem = GetServerChatSystem();
            object messages = chatSystem.GetType().GetProperty("Messages").GetValue(chatSystem);

            foreach (object message in (IEnumerable)messages)
            {
                string content = (string)message.GetType().GetField("Content").GetValue(message);
                if (content == expectedContent)
                {
                    return true;
                }
            }

            return false;
        }

        private object GetServerChatSystem()
        {
            object chatSystem = GetField("chatSystem");
            Assert.IsNotNull(chatSystem, "服务器 ChatSystem 应已初始化。");
            return chatSystem;
        }

        private static bool DictionaryContainsKey(object dictionary, ulong key)
        {
            MethodInfo containsKey = dictionary.GetType().GetMethod("ContainsKey");
            return (bool)containsKey.Invoke(dictionary, new object[] { key });
        }

        private static int GetCollectionCount(object collection)
        {
            PropertyInfo count = collection.GetType().GetProperty("Count");
            return Convert.ToInt32(count.GetValue(collection));
        }

        private static Type RuntimeType(string fullName)
            => Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);

        private static void WriteBoundedUtf8String(ref FastBufferWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.WriteValueSafe(bytes.Length);
            if (bytes.Length > 0)
            {
                writer.WriteBytesSafe(bytes);
            }
        }

        private static bool Approximately(Vector3 actual, Vector3 expected)
            => Vector3.SqrMagnitude(actual - expected) < 0.0001f;

        private static void AssertVectorClose(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less(Vector3.SqrMagnitude(actual - expected), 0.0001f, message);
        }

        private static void AssertVectorClose(Vector2 expected, Vector2 actual, string message)
        {
            Assert.Less(Vector2.SqrMagnitude(actual - expected), 0.0001f, message);
        }
    }
}
