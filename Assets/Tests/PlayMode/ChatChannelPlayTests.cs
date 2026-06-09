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
    public class ChatChannelPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private const string BridgeTypeName = "GanglandUndercover.Online.MiniGames.OnlineMiniGameBridge";
        private const string ChatSendMessage = "GanglandChatSend";
        private const string ChatBroadcastMessage = "GanglandChatBroadcast";

        private readonly List<GameObject> _ownedObjects = new List<GameObject>();
        private readonly List<NetworkManager> _clientNetworkManagers = new List<NetworkManager>();
        private readonly Dictionary<NetworkManager, List<CapturedChatMessage>> _captures =
            new Dictionary<NetworkManager, List<CapturedChatMessage>>();

        private Type _controllerType;
        private Type _bridgeType;
        private Type _chatChannelType;
        private MonoBehaviour _serverController;
        private NetworkManager _serverNetworkManager;
        private ushort _port;

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            _bridgeType = Type.GetType($"{BridgeTypeName}, {RuntimeAssemblyName}");
            _chatChannelType = RuntimeType("GanglandUndercover.Online.ChatChannel");
            Assert.IsNotNull(_controllerType, $"找不到运行时类型 {ControllerTypeName}。");
            Assert.IsNotNull(_bridgeType, $"找不到运行时类型 {BridgeTypeName}。");

            DestroyObjectsOfType(typeof(NetworkManager));
            DestroyObjectsOfType(_bridgeType);
            DestroyObjectsOfType(_controllerType);

            _port = AllocateUdpPort();
            _serverNetworkManager = CreateNetworkManager("ChatChannel_ServerNetworkManager", _port, true);

            GameObject controllerObject = new GameObject("ChatChannel_ServerController");
            _ownedObjects.Add(controllerObject);
            _serverController = (MonoBehaviour)controllerObject.AddComponent(_controllerType);
            Assert.IsNotNull(_serverController, "无法挂载 OnlineMatchController。");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _clientNetworkManagers.Count - 1; i >= 0; i--)
            {
                NetworkManager client = _clientNetworkManagers[i];
                if (client != null && client.IsListening)
                {
                    client.Shutdown();
                }
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

            _clientNetworkManagers.Clear();
            _captures.Clear();
        }

        [UnityTest]
        public IEnumerator ChatChannels_RouteMeetingProximityAndGhostOverNetcode()
        {
            yield return null;

            InvokePrivate("RegisterMessages");
            Assert.IsTrue(_serverNetworkManager.StartHost(), "Host NetworkManager 应能启动。");
            InvokePrivate("RegisterMessages");

            NetworkManager sender = StartClient("ChatChannel_SenderClientNetworkManager");
            yield return WaitForClient(sender, "发送者 Client 应连接到 Host。");

            NetworkManager nearRecipient = StartClient("ChatChannel_NearRecipientNetworkManager");
            yield return WaitForClient(nearRecipient, "近距离接收者 Client 应连接到 Host。");

            NetworkManager farOrDeadRecipient = StartClient("ChatChannel_FarOrDeadRecipientNetworkManager");
            yield return WaitForClient(farOrDeadRecipient, "远端/死亡接收者 Client 应连接到 Host。");

            ulong senderId = sender.LocalClientId;
            ulong nearRecipientId = nearRecipient.LocalClientId;
            ulong farOrDeadRecipientId = farOrDeadRecipient.LocalClientId;

            SetPhase("Meeting");
            SetPlayer(senderId, "Sender", Vector3.zero, alive: true);
            SetPlayer(nearRecipientId, "AliveRecipient", new Vector3(4f, 0f, 0f), alive: true);
            SetPlayer(farOrDeadRecipientId, "DeadRecipient", new Vector3(6f, 0f, 0f), alive: false);
            ClearServerChatCooldown();
            ClearCaptures();

            const string meetingContent = "meeting-route";
            SendChatFromClient(sender, meetingContent);
            yield return WaitUntilOrFail(
                () => CapturedContains(nearRecipient, meetingContent, "Meeting"),
                "会议频道应通过真实 ChatBroadcast 到达存活玩家。");
            yield return RunFrames(10);
            Assert.IsFalse(CapturedContains(farOrDeadRecipient, meetingContent),
                "会议频道不应发给死亡玩家。");

            SetPhase("Action");
            SetPlayer(senderId, "Sender", Vector3.zero, alive: true);
            SetPlayer(nearRecipientId, "NearRecipient", new Vector3(2f, 0f, 0f), alive: true);
            SetPlayer(farOrDeadRecipientId, "FarRecipient", new Vector3(50f, 0f, 0f), alive: true);
            ClearServerChatCooldown();
            ClearCaptures();

            const string proximityContent = "proximity-route";
            SendChatFromClient(sender, proximityContent);
            yield return WaitUntilOrFail(
                () => CapturedContains(nearRecipient, proximityContent, "Proximity"),
                "近距离频道应只发给范围内存活玩家。");
            yield return RunFrames(10);
            Assert.IsFalse(CapturedContains(farOrDeadRecipient, proximityContent),
                "近距离频道不应发给范围外玩家。");

            SetPhase("Action");
            SetPlayer(senderId, "GhostSender", Vector3.zero, alive: false);
            SetPlayer(nearRecipientId, "AliveRecipient", new Vector3(2f, 0f, 0f), alive: true);
            SetPlayer(farOrDeadRecipientId, "GhostRecipient", new Vector3(50f, 0f, 0f), alive: false);
            ClearServerChatCooldown();
            ClearCaptures();

            const string ghostContent = "ghost-route";
            SendChatFromClient(sender, ghostContent);
            yield return WaitUntilOrFail(
                () => CapturedContains(farOrDeadRecipient, ghostContent, "Ghost"),
                "鬼魂频道应通过真实 ChatBroadcast 到达死亡玩家。");
            yield return RunFrames(10);
            Assert.IsFalse(CapturedContains(nearRecipient, ghostContent),
                "鬼魂频道不应发给存活玩家。");
        }

        private NetworkManager StartClient(string name)
        {
            NetworkManager client = CreateNetworkManager(name, _port, false);
            _clientNetworkManagers.Add(client);
            CopyServerNetworkPrefabs(client);
            Assert.IsTrue(client.StartClient(), name + " 应能启动。");
            RegisterBroadcastCapture(client);
            return client;
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

        private void RegisterBroadcastCapture(NetworkManager client)
        {
            _captures[client] = new List<CapturedChatMessage>();
            Assert.IsNotNull(client.CustomMessagingManager, "Client CustomMessagingManager 应已初始化。");
            client.CustomMessagingManager.RegisterNamedMessageHandler(
                ChatBroadcastMessage,
                (senderClientId, reader) =>
                {
                    if (TryReadBroadcast(ref reader, out CapturedChatMessage message))
                    {
                        message.NetworkSenderClientId = senderClientId;
                        message.ReceiverClientId = client.LocalClientId;
                        _captures[client].Add(message);
                    }
                });
        }

        private IEnumerator WaitForClient(NetworkManager client, string message)
        {
            yield return WaitUntilOrFail(
                () => client.IsConnectedClient
                    && ServerHasClient(client.LocalClientId)
                    && DictionaryContainsKey(GetField("players"), client.LocalClientId),
                message);
        }

        private void SendChatFromClient(NetworkManager client, string content)
        {
            FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp);
            try
            {
                object[] args = { writer, content };
                StaticPrivate("WriteChatSendPayload").Invoke(null, args);
                writer = (FastBufferWriter)args[0];
                client.CustomMessagingManager.SendNamedMessage(ChatSendMessage, NetworkManager.ServerClientId, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private bool CapturedContains(NetworkManager receiver, string expectedContent, string expectedChannelName)
        {
            int expectedChannel = ChannelValue(expectedChannelName);
            return CapturedContains(receiver, expectedContent, expectedChannel);
        }

        private bool CapturedContains(NetworkManager receiver, string expectedContent)
        {
            if (!_captures.TryGetValue(receiver, out List<CapturedChatMessage> messages))
            {
                return false;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Content == expectedContent)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CapturedContains(NetworkManager receiver, string expectedContent, int expectedChannel)
        {
            if (!_captures.TryGetValue(receiver, out List<CapturedChatMessage> messages))
            {
                return false;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                CapturedChatMessage message = messages[i];
                if (message.NetworkSenderClientId == NetworkManager.ServerClientId
                    && message.Content == expectedContent
                    && message.ChannelValue == expectedChannel)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearCaptures()
        {
            foreach (List<CapturedChatMessage> messages in _captures.Values)
            {
                messages.Clear();
            }
        }

        private void SetPlayer(ulong clientId, string displayName, Vector3 position, bool alive)
        {
            Type playerType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            object player = Activator.CreateInstance(
                playerType,
                clientId,
                displayName,
                position,
                true,
                alive,
                Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Police"),
                Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineProfession"), "Inspector"),
                0,
                false);

            IDictionary players = (IDictionary)GetField("players");
            players[clientId] = player;

            IDictionary privateRoles = (IDictionary)GetField("privateRoles");
            privateRoles[clientId] = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Police");
        }

        private void ClearServerChatCooldown()
        {
            object cooldowns = GetField("serverChatLastSendTimes");
            cooldowns.GetType().GetMethod("Clear").Invoke(cooldowns, null);
        }

        private void SetPhase(string phaseName)
        {
            Type phaseType = RuntimeType("GanglandUndercover.Online.OnlineMatchPhase");
            SetField("phase", Enum.Parse(phaseType, phaseName));
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

        private static bool TryReadBroadcast(ref FastBufferReader reader, out CapturedChatMessage message)
        {
            message = null;

            try
            {
                string senderId = ReadUtf8String(ref reader);
                string senderName = ReadUtf8String(ref reader);
                string content = ReadUtf8String(ref reader);
                reader.ReadValueSafe(out bool isDead);
                reader.ReadValueSafe(out int factionValue);
                reader.ReadValueSafe(out int channelValue);

                message = new CapturedChatMessage
                {
                    SenderId = senderId,
                    SenderName = senderName,
                    Content = content,
                    IsDead = isDead,
                    FactionValue = factionValue,
                    ChannelValue = channelValue
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadUtf8String(ref FastBufferReader reader)
        {
            reader.ReadValueSafe(out int length);
            if (length <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = new byte[length];
            reader.ReadBytesSafe(ref bytes, length);
            return Encoding.UTF8.GetString(bytes);
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

        private object GetField(string name)
        {
            FieldInfo fi = _controllerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            return fi.GetValue(_serverController);
        }

        private void SetField(string name, object value)
        {
            FieldInfo fi = _controllerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            fi.SetValue(_serverController, value);
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

        private int ChannelValue(string channelName)
        {
            return Convert.ToInt32(Enum.Parse(_chatChannelType, channelName));
        }

        private static bool DictionaryContainsKey(object dictionary, ulong key)
        {
            MethodInfo containsKey = dictionary.GetType().GetMethod("ContainsKey");
            return (bool)containsKey.Invoke(dictionary, new object[] { key });
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

        private static Type RuntimeType(string fullName)
            => Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);

        private sealed class CapturedChatMessage
        {
            public ulong NetworkSenderClientId;
            public ulong ReceiverClientId;
            public string SenderId;
            public string SenderName;
            public string Content;
            public bool IsDead;
            public int FactionValue;
            public int ChannelValue;
        }
    }
}
