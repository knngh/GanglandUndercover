using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace GanglandUndercover.PlayTests
{
    /// <summary>
    /// Task#6 真·云 Relay 双进程（端到端）联调。
    ///
    /// Unity Services / Relay 只能在 Play Mode 初始化，故本验证以 PlayMode 测试形式运行，
    /// 由两个独立的 Unity 批处理进程分别扮演 Host 与 Client，通过共享文件交换 Relay 房间码：
    ///
    ///   进程A (GANGLAND_RELAY_ROLE=host)   : RequestRelayHost() → 拿到 RelayJoinCode → 写入码文件
    ///                                        → 等待 ConnectedClientCount >= 2 → 断言对端真的连进来
    ///   进程B (GANGLAND_RELAY_ROLE=client) : 轮询读取码文件 → RequestRelayClient(code)
    ///                                        → 等待 IsClientConnected → 断言连上 Host
    ///
    /// 未设置 GANGLAND_RELAY_ROLE 时该用例直接 Ignore，避免影响常规 PlayMode 套件（Task#4）。
    ///
    /// 运行时类型仍在预定义的 Assembly-CSharp，通过反射访问，与 MatchLoopPlayTests 风格一致。
    /// </summary>
    public class RelayTwoProcessPlayTests
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";
        private const string ControllerTypeName = "GanglandUndercover.Online.OnlineMatchController";
        private const string MiniGameBridgeTypeName = "GanglandUndercover.Online.MiniGames.OnlineMiniGameBridge";
        private const string CameraTypeName = "GanglandUndercover.Online.Surveillance.OnlineSecurityCamera";
        private const string CharacterCustomizerTypeName = "GanglandUndercover.SocialDeduction.CharacterCustomizer";

        private const string RoleEnv = "GANGLAND_RELAY_ROLE";
        private const string CodeFileEnv = "GANGLAND_RELAY_CODEFILE";
        private const string ServerSnapshotMessage = "GanglandServerSnapshot";
        private const string RoleAssignMessage = "GanglandRoleAssign";
        private const string ChatSendMessage = "GanglandChatSend";
        private const string ChatBroadcastMessage = "GanglandChatBroadcast";
        private const string MapSelectMessage = "GanglandMapSelect";
        private const string CharacterCustomMessage = "GanglandCharacterCustom";

        // 真实网络 + 云服务初始化耗时，给宽裕但有界的上限。
        // PeerConnectTimeout 要覆盖对端进程从冷启动到加入的时间，故给到 4 分钟。
        private const float ServiceReadyTimeout = 60f;
        private const float CodeAvailableTimeout = 90f;
        private const float PeerConnectTimeout = 240f;
        private const int TestTimeoutMilliseconds = 360000;

        private GameObject _host;
        private MonoBehaviour _controller;
        private Type _controllerType;
        private int _cameraDataUpdateCount;
        private int _cameraNonEmptyDataCount;

        private static string Role => System.Environment.GetEnvironmentVariable(RoleEnv);
        private static string CodeFilePath =>
            System.Environment.GetEnvironmentVariable(CodeFileEnv)
            ?? Path.Combine(Path.GetTempPath(), "gangland-relay-code.txt");
        private static string MaliciousMarkerPath => CodeFilePath + ".malicious";
        private static string MigrationCodeFilePath => CodeFilePath + ".migration";
        private static string MigrationCandidateOldReadyPath => CodeFilePath + ".candidate-old";
        private static string MigrationObserverOldReadyPath => CodeFilePath + ".observer-old";
        private static string MigrationObserverNewReadyPath => CodeFilePath + ".observer-new";
        private static string MigrationOldHostReconnectedPath => CodeFilePath + ".oldhost-reconnected";
        private static string MigrationRemoteTaskRequestPath => CodeFilePath + ".remote-task";
        private static string MigrationRemoteTaskSubmittedPath => CodeFilePath + ".remote-task-submitted";
        private static string MigrationRemoteVoteRequestPath => CodeFilePath + ".remote-vote";
        private static string MigrationOldHostVoteSubmittedPath => CodeFilePath + ".oldhost-vote-submitted";
        private static string MigrationObserverVoteSubmittedPath => CodeFilePath + ".observer-vote-submitted";
        private static string CameraLegalReadyPath => CodeFilePath + ".camera-legal-ready";
        private static string CameraDataReceivedPath => CodeFilePath + ".camera-data-received";

        [SetUp]
        public void SetUp()
        {
            _controllerType = Type.GetType($"{ControllerTypeName}, {RuntimeAssemblyName}");
            Assert.IsNotNull(_controllerType,
                $"找不到运行时类型 {ControllerTypeName}（Assembly-CSharp 未编译？）");

            _host = new GameObject("RelayTest_OnlineMatchHost");
            _controller = (MonoBehaviour)_host.AddComponent(_controllerType);
            Assert.IsNotNull(_controller, "无法在 PlayMode 下挂载 OnlineMatchController。");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator RelayHost_PublishesCodeAndAcceptsPeer()
        {
            if (!string.Equals(Role, "host", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"未设置 {RoleEnv}=host，跳过 Relay Host 端到端用例。");
            }

            yield return null; // 让 Awake 执行一帧，核心服务/网络栈就绪。

            string codeFile = CodeFilePath;
            if (File.Exists(codeFile))
            {
                File.Delete(codeFile); // 清理上一轮残留，避免 Client 读到旧码。
            }
            if (File.Exists(MaliciousMarkerPath))
            {
                File.Delete(MaliciousMarkerPath);
            }
            DeleteIfExists(CameraLegalReadyPath);
            DeleteIfExists(CameraDataReceivedPath);

            Debug.Log("[RelayTest][host] 请求创建 Relay 房间…");
            Invoke("RequestRelayHost");

            // 等待真实房间码生成（含 UnityServices 初始化 + 匿名登录 + CreateAllocation/GetJoinCode）。
            yield return WaitUntil(() => !string.IsNullOrWhiteSpace(GetString("RelayJoinCode")),
                ServiceReadyTimeout, () => "Host 在超时内未拿到 Relay 房间码。状态: " + GetString("RelayStatus"));

            string joinCode = GetString("RelayJoinCode");
            Debug.Log("[RelayTest][host] 房间码=" + joinCode + " 状态=" + GetString("RelayStatus"));
            Assert.IsTrue(GetBool("IsHost"), "拿到房间码后本端应已成为 Host。");
            Assert.IsTrue(GetBool("IsListeningOrConnected"), "Host 的 NetworkManager 应处于监听状态。");

            // 把房间码原子地交给 Client（先写临时文件再 move，避免 Client 读到半写内容）。
            string tmp = codeFile + ".tmp";
            File.WriteAllText(tmp, joinCode);
            if (File.Exists(codeFile)) { File.Delete(codeFile); }
            File.Move(tmp, codeFile);
            Debug.Log("[RelayTest][host] 已写出房间码到 " + codeFile);

            // 等待对端真的通过 Relay 连进来。
            // 注意：Host 的 NetworkManager.ConnectedClientsList 含本机 host 客户端（id 0），
            // 因此远端 Client 真正接入后计数应 >= 2。必须等到这个条件，Host 才能退出，
            // 否则 Host 一退出就会回收 Relay 分配，对端会拿到 "join code not found"。
            yield return WaitUntil(() => GetInt("ConnectedClientCount") >= 2,
                PeerConnectTimeout, () => "Host 在超时内未观察到远端客户端连入（当前计数="
                    + GetInt("ConnectedClientCount") + "，含本机）。状态: " + GetString("RelayStatus"));

            int connected = GetInt("ConnectedClientCount");
            Debug.Log("[RelayTest][host] 已连接客户端数（含本机）=" + connected);
            Assert.GreaterOrEqual(connected, 2,
                "Relay 双进程：Host 应在本机之外再接纳到至少 1 个远端客户端。");

            bool baselineMatchStarted = GetBool("MatchStarted");
            string baselinePhase = GetProp("Phase").ToString();
            string baselineLocalRole = GetProp("LocalRole").ToString();
            string baselineMapType = GetActiveMapTypeName();
            int baselineChatCount = GetServerChatMessageCount();
            yield return WaitUntil(() => GetServerCameraCount() > 0,
                ServiceReadyTimeout, () => "Host 未生成监控摄像头 NetworkObject。");
            int baselineCameraWatcherCount = GetServerCameraWatcherCount();
            Assert.AreEqual(0, baselineCameraWatcherCount,
                "Relay 注入前不应已有摄像头 watcher。");
            yield return WaitUntil(() => GetServerCharacterCustomizerJson(NetworkManager.ServerClientId) != null,
                ServiceReadyTimeout, () => "Host 未生成 server-owned CharacterCustomizer NetworkObject。");
            string baselineCharacterCustomJson = GetServerCharacterCustomizerJson(NetworkManager.ServerClientId);

            string maliciousMarkerDetail = null;
            yield return WaitUntil(
                () => TryReadMaliciousMarker(out maliciousMarkerDetail)
                    && maliciousMarkerDetail.Contains("cameraWatchRequest=sent"),
                CodeAvailableTimeout,
                () => "Host 未等到 Client 真实摄像头越权观看请求标记。");

            // 给 Relay/NGO 几帧处理 Client 注入的 named messages 和 camera ServerRpc。
            yield return RunFrames(120);

            Assert.AreEqual(baselineMatchStarted, GetBool("MatchStarted"),
                "Relay 恶意 ServerSnapshot 不应改变 Host 对局启动状态。");
            Assert.AreEqual(baselinePhase, GetProp("Phase").ToString(),
                "Relay 恶意 ServerSnapshot 不应改变 Host 阶段。");
            Assert.AreEqual(baselineLocalRole, GetProp("LocalRole").ToString(),
                "Relay 恶意 RoleAssign 不应改变 Host 本地身份。");
            Assert.AreEqual(baselineMapType, GetActiveMapTypeName(),
                "Relay 恶意 MapSelect 不应改变 Host 地图。");
            Assert.AreEqual(baselineChatCount, GetServerChatMessageCount(),
                "Relay 恶意 ChatBroadcast/Lobby ChatSend 不应污染 Host 聊天。");
            Assert.AreEqual(baselineCameraWatcherCount, GetServerCameraWatcherCount(),
                "Relay 越权摄像头 StartWatchingServerRpc 不应把 Client 加入 watcher 集合。");
            Assert.IsTrue(maliciousMarkerDetail.Contains("characterCustom=sent"),
                "Client 应通过真实 CharacterCustomizer clone 发送越权外观消息。");
            Assert.AreEqual(baselineCharacterCustomJson, GetServerCharacterCustomizerJson(NetworkManager.ServerClientId),
                "Relay 越权 CharacterCustom 消息不应改变 server-owned 外观选择。");

            // 恶意注入断言完成后，进入真实 Action 阶段，准备一个合法的近距离观看者。
            Invoke("RequestFillBotsAndStart");
            yield return WaitUntil(() => GetProp("Phase").ToString() == "Action",
                ServiceReadyTimeout, () => "Host 未在摄像头合法观看门禁内进入 Action 阶段。");
            ulong remoteClientId = PlaceRemotePlayerInsideFirstCameraZone(out ulong cameraNetworkObjectId);
            WriteAtomicFile(CameraLegalReadyPath,
                "phase=Action remoteId=" + remoteClientId
                + " cameraNetworkObjectId=" + cameraNetworkObjectId
                + " cameraReady=true");

            string cameraDataMarker = null;
            yield return WaitUntil(() => TryReadAtomicFile(CameraDataReceivedPath, out cameraDataMarker),
                ServiceReadyTimeout, () => "Host 未收到 Client 的非空摄像头数据回调 marker。");
            Assert.IsTrue(cameraDataMarker.Contains("nonEmpty=true"),
                "Host 只应接受 Client 实际收到非空 VisiblePlayerData 的证据。");
            Assert.AreEqual(1, GetServerCameraWatcherCount(),
                "合法近距离 Client 应加入且仅加入一个摄像头 watcher。");

            // 远端已连上并完成注入，再多挺几帧让连接稳定（证明不是瞬时抖动）。
            yield return RunFrames(30);
            Assert.GreaterOrEqual(GetInt("ConnectedClientCount"), 2, "远端连接应保持稳定。");

            WriteResult("host", "PASS",
                $"joinCode={joinCode} connectedClients(incl self)={GetInt("ConnectedClientCount")} maliciousMessagesRejected=true cameraWatchRejected=true characterCustomRejected=true");
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator RelayClient_JoinsHostByCode()
        {
            if (!string.Equals(Role, "client", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"未设置 {RoleEnv}=client，跳过 Relay Client 端到端用例。");
            }

            yield return null;

            string codeFile = CodeFilePath;

            // 轮询等待 Host 写出房间码。
            Debug.Log("[RelayTest][client] 等待房间码文件 " + codeFile);
            string joinCode = null;
            yield return WaitUntil(() =>
            {
                if (!File.Exists(codeFile)) { return false; }
                try { joinCode = File.ReadAllText(codeFile)?.Trim(); }
                catch (IOException) { return false; } // Host 可能正在写。
                return !string.IsNullOrWhiteSpace(joinCode);
            }, CodeAvailableTimeout, () => "Client 在超时内未读到 Host 写出的房间码文件。");

            Debug.Log("[RelayTest][client] 读到房间码=" + joinCode + "，发起加入…");
            InvokeWithString("RequestRelayClient", joinCode);

            // 等待真实连接建立（JoinAllocation + StartClient + Relay 中转握手）。
            yield return WaitUntil(() => GetBool("IsClientConnected"),
                PeerConnectTimeout, () => "Client 在超时内未连上 Host。状态: " + GetString("RelayStatus"));

            Debug.Log("[RelayTest][client] 已连上 Host，状态=" + GetString("RelayStatus"));
            Assert.IsTrue(GetBool("IsClientConnected"), "Relay 双进程：Client 应成功连上 Host。");

            yield return WaitUntil(() => FindClientSecurityCamera() != null,
                ServiceReadyTimeout, () => "Client 未收到 Host 生成的监控摄像头 NetworkObject clone。");
            yield return WaitUntil(() => FindClientNonOwnerCharacterCustomizer() != null,
                ServiceReadyTimeout, () => "Client 未收到 server-owned CharacterCustomizer NetworkObject clone。");
            SendCharacterCustomFromClient();
            SendCameraWatchRequestFromClient();
            yield return RunFrames(30);
            SendMaliciousRelayPayloads();
            WriteMaliciousMarker(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " characterCustom=sent cameraWatchRequest=sent maliciousPayloads=sent");
            Debug.Log("[RelayTest][client] 已发送恶意 CharacterCustom/camera ServerRpc/named messages 并写出标记 " + MaliciousMarkerPath);

            string cameraReadyMarker = null;
            yield return WaitUntil(() => TryReadAtomicFile(CameraLegalReadyPath, out cameraReadyMarker),
                PeerConnectTimeout, () => "Client 未等到 Host 发布合法摄像头观看准备 marker。");
            ulong cameraNetworkObjectId = MarkerUlong(cameraReadyMarker, "cameraNetworkObjectId");
            object legalCamera = FindClientSecurityCamera(cameraNetworkObjectId);
            Assert.IsNotNull(legalCamera, "合法观看前 Client 摄像头 clone 应仍存在。");
            _cameraDataUpdateCount = 0;
            _cameraNonEmptyDataCount = 0;
            SubscribeToCameraData(legalCamera);
            SendCameraWatchRequestFromClient(legalCamera);
            yield return WaitUntil(() => _cameraNonEmptyDataCount > 0,
                ServiceReadyTimeout, () => "Client 合法观看后未收到非空 VisiblePlayerData 回调。");
            WriteAtomicFile(CameraDataReceivedPath,
                "updates=" + _cameraDataUpdateCount
                + " nonEmpty=" + (_cameraNonEmptyDataCount > 0 ? "true" : "false")
                + " ready=" + cameraReadyMarker);

            // 连上后多挺一会儿，让 Host 端确实观察到本客户端（计数稳定 >= 2）后再退出。
            yield return RunFrames(120);
            Assert.IsTrue(GetBool("IsClientConnected"), "Client 连接应保持稳定。");

            WriteResult("client", "PASS", "joinCode=" + joinCode + " connected=true characterCustom=sent cameraWatchRequest=sent maliciousPayloads=sent");
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator RelayMigration_OldHostReconnectsToReplacementRelay()
        {
            if (!string.Equals(Role, "migration-host", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"未设置 {RoleEnv}=migration-host，跳过 Relay Host migration 重连用例。");
            }

            yield return null;

            string codeFile = CodeFilePath;
            DeleteIfExists(codeFile);
            DeleteIfExists(MigrationCodeFilePath);
            DeleteIfExists(MaliciousMarkerPath);

            Debug.Log("[RelayMigrationTest][old-host] 创建旧 Relay 房间。");
            Invoke("RequestRelayHost");
            yield return WaitUntil(() => !string.IsNullOrWhiteSpace(GetString("RelayJoinCode")),
                ServiceReadyTimeout, () => "旧 Host 在超时内未拿到 Relay 房间码。状态: " + GetString("RelayStatus"));

            string oldJoinCode = GetString("RelayJoinCode");
            WriteAtomicFile(codeFile, oldJoinCode);
            Debug.Log("[RelayMigrationTest][old-host] 已写出旧房间码=" + oldJoinCode);

            yield return WaitUntil(() => GetInt("ConnectedClientCount") >= 2,
                PeerConnectTimeout, () => "旧 Host 未等到候选 Client 加入。当前连接数="
                    + GetInt("ConnectedClientCount") + " 状态: " + GetString("RelayStatus"));

            string migrationJoinCode = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationCodeFilePath, out migrationJoinCode),
                CodeAvailableTimeout, () => "旧 Host 未等到候选 Client 写出 migration 新 Relay 房间码。");
            Assert.AreNotEqual(oldJoinCode, migrationJoinCode,
                "Host migration 必须使用新的 Relay allocation，不能复用旧房间码。");

            Invoke("RequestShutdown");
            yield return WaitUntil(() => !GetBool("IsListeningOrConnected"),
                ServiceReadyTimeout, () => "旧 Host 关闭旧 Relay 连接超时。");

            Debug.Log("[RelayMigrationTest][old-host] 关闭旧 Host 后加入新 Relay=" + migrationJoinCode);
            InvokeWithString("RequestRelayClient", migrationJoinCode);
            yield return WaitUntil(() => GetBool("IsClientConnected"),
                PeerConnectTimeout, () => "旧 Host 作为 Client 未能连入 migration 新 Relay。状态: " + GetString("RelayStatus"));

            yield return RunFrames(60);
            Assert.IsTrue(GetBool("IsClientConnected"), "重连到 migration 新 Relay 后连接应保持稳定。");
            WriteResult("migration-host", "PASS",
                "oldCode=" + oldJoinCode + " migrationCode=" + migrationJoinCode + " reconnected=true");
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator RelayMigration_ClientPromotesToReplacementRelayHost()
        {
            if (!string.Equals(Role, "migration-client", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"未设置 {RoleEnv}=migration-client，跳过 Relay Client migration 接管用例。");
            }

            yield return null;

            string oldJoinCode = null;
            yield return WaitUntil(() => TryReadAtomicFile(CodeFilePath, out oldJoinCode),
                CodeAvailableTimeout, () => "候选 Client 未读到旧 Host Relay 房间码。");

            Debug.Log("[RelayMigrationTest][candidate] 加入旧 Relay=" + oldJoinCode);
            InvokeWithString("RequestRelayClient", oldJoinCode);
            yield return WaitUntil(() => GetBool("IsClientConnected"),
                PeerConnectTimeout, () => "候选 Client 未能连入旧 Host。状态: " + GetString("RelayStatus"));

            Task<string> migrationTask = InvokeReplacementRelayHostForMigration();
            yield return WaitUntil(() => migrationTask.IsCompleted,
                ServiceReadyTimeout, () => "候选 Client 创建 replacement Relay Host 超时。状态: " + GetString("RelayStatus"));

            Assert.IsFalse(migrationTask.IsFaulted, "replacement Relay Host 创建不应抛异常。");
            Assert.IsTrue(string.IsNullOrWhiteSpace(migrationTask.Result),
                "replacement Relay Host 创建应成功，失败原因: " + migrationTask.Result);
            Assert.IsTrue(GetBool("IsHost"), "候选 Client 应已成为 replacement Relay Host。");
            Assert.IsTrue(GetBool("IsListeningOrConnected"), "replacement Relay Host 应处于监听状态。");

            string migrationJoinCode = GetString("RelayJoinCode");
            Assert.IsFalse(string.IsNullOrWhiteSpace(migrationJoinCode), "replacement Relay Host 应生成新房间码。");
            Assert.AreNotEqual(oldJoinCode, migrationJoinCode,
                "replacement Relay Host 必须生成不同于旧 Relay 的新房间码。");
            WriteAtomicFile(MigrationCodeFilePath, migrationJoinCode);
            Debug.Log("[RelayMigrationTest][candidate] 已写出 migration 新房间码=" + migrationJoinCode);

            yield return WaitUntil(() => GetInt("ConnectedClientCount") >= 2,
                PeerConnectTimeout, () => "replacement Relay Host 未等到旧端重连。当前连接数="
                    + GetInt("ConnectedClientCount") + " 状态: " + GetString("RelayStatus"));

            yield return RunFrames(60);
            Assert.GreaterOrEqual(GetInt("ConnectedClientCount"), 2, "migration 新 Relay 应保持至少 2 个连接（含 Host）。");
            WriteResult("migration-client", "PASS",
                "oldCode=" + oldJoinCode + " migrationCode=" + migrationJoinCode
                + " replacementHost=true connectedClients(incl self)=" + GetInt("ConnectedClientCount"));
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator RelayMigration_ThreeClientOldHostReconnectsToReplacementRelay()
        {
            if (!string.Equals(Role, "migration-host-threeclient", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"未设置 {RoleEnv}=migration-host-threeclient，跳过 Relay 三端 migration 旧 Host 用例。");
            }

            yield return null;

            DeleteIfExists(CodeFilePath);
            DeleteIfExists(MigrationCodeFilePath);
            DeleteIfExists(MaliciousMarkerPath);
            DeleteIfExists(MigrationCandidateOldReadyPath);
            DeleteIfExists(MigrationObserverOldReadyPath);
            DeleteIfExists(MigrationObserverNewReadyPath);
            DeleteIfExists(MigrationOldHostReconnectedPath);
            DeleteIfExists(MigrationRemoteTaskRequestPath);
            DeleteIfExists(MigrationRemoteTaskSubmittedPath);
            DeleteIfExists(MigrationRemoteVoteRequestPath);
            DeleteIfExists(MigrationOldHostVoteSubmittedPath);
            DeleteIfExists(MigrationObserverVoteSubmittedPath);

            Debug.Log("[RelayMigration3Test][old-host] 创建旧 Relay 房间。");
            Invoke("RequestRelayHost");
            yield return WaitUntil(() => !string.IsNullOrWhiteSpace(GetString("RelayJoinCode")),
                ServiceReadyTimeout, () => "三端旧 Host 在超时内未拿到 Relay 房间码。状态: " + GetString("RelayStatus"));

            string oldJoinCode = GetString("RelayJoinCode");
            WriteAtomicFile(CodeFilePath, oldJoinCode);
            Debug.Log("[RelayMigration3Test][old-host] 已写出旧房间码=" + oldJoinCode);

            yield return WaitUntil(() => GetInt("ConnectedClientCount") >= 3,
                PeerConnectTimeout, () => "旧 Host 未等到候选和观察客户端同时加入。当前连接数="
                    + GetInt("ConnectedClientCount") + " 状态: " + GetString("RelayStatus"));

            string migrationJoinCode = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationCodeFilePath, out migrationJoinCode),
                CodeAvailableTimeout, () => "三端旧 Host 未等到 replacement Host 写出 migration 新 Relay 房间码。");
            Assert.AreNotEqual(oldJoinCode, migrationJoinCode,
                "三端 Host migration 必须使用新的 Relay allocation，不能复用旧房间码。");

            Invoke("RequestShutdown");
            yield return WaitUntil(() => !GetBool("IsListeningOrConnected"),
                ServiceReadyTimeout, () => "三端旧 Host 关闭旧 Relay 连接超时。");

            Debug.Log("[RelayMigration3Test][old-host] 关闭旧 Host 后加入新 Relay=" + migrationJoinCode);
            InvokeWithString("RequestRelayClient", migrationJoinCode);
            yield return WaitUntil(() => GetBool("IsClientConnected"),
                PeerConnectTimeout, () => "三端旧 Host 作为 Client 未能连入 migration 新 Relay。状态: " + GetString("RelayStatus"));

            yield return RunFrames(60);
            Assert.IsTrue(GetBool("IsClientConnected"), "三端重连到 migration 新 Relay 后旧 Host 连接应保持稳定。");
            NetworkManager reconnectedManager = GetNetworkManager();
            Assert.IsNotNull(reconnectedManager, "三端旧 Host 重连后应存在 NetworkManager。");
            WriteAtomicFile(MigrationOldHostReconnectedPath,
                "clientId=" + reconnectedManager.LocalClientId);
            yield return WaitForRemoteTaskRequestAndSubmit();
            yield return WaitForRemoteVoteRequestAndSubmit(
                "old-host",
                MigrationOldHostVoteSubmittedPath);
            yield return RunFrames(60);
            Assert.IsTrue(GetBool("IsClientConnected"), "旧 Host 提交迁移后任务/投票后连接应保持稳定。");
            WriteResult("migration-host-threeclient", "PASS",
                "oldCode=" + oldJoinCode + " migrationCode=" + migrationJoinCode
                + " reconnected=true oldRelayClients(incl self)>=3 remoteTaskSubmitted=true remoteVoteSubmitted=true");
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator RelayMigration_ThreeClientCandidatePromotesAndRunsPostRestoreFlow()
        {
            if (!string.Equals(Role, "migration-candidate-threeclient", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"未设置 {RoleEnv}=migration-candidate-threeclient，跳过 Relay 三端 migration candidate 用例。");
            }

            yield return null;

            string oldJoinCode = null;
            yield return WaitUntil(() => TryReadAtomicFile(CodeFilePath, out oldJoinCode),
                CodeAvailableTimeout, () => "三端候选 Client 未读到旧 Host Relay 房间码。");

            Debug.Log("[RelayMigration3Test][candidate] 加入旧 Relay=" + oldJoinCode);
            InvokeWithString("RequestRelayClient", oldJoinCode);
            yield return WaitUntil(() => GetBool("IsClientConnected"),
                PeerConnectTimeout, () => "三端候选 Client 未能连入旧 Host。状态: " + GetString("RelayStatus"));
            WriteAtomicFile(MigrationCandidateOldReadyPath, "candidate-old-connected");

            string observerReady = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationObserverOldReadyPath, out observerReady),
                PeerConnectTimeout, () => "三端候选 Client 未等到观察客户端加入旧 Relay。");

            Task<string> migrationTask = InvokeReplacementRelayHostForMigration();
            yield return WaitUntil(() => migrationTask.IsCompleted,
                ServiceReadyTimeout, () => "三端候选 Client 创建 replacement Relay Host 超时。状态: " + GetString("RelayStatus"));

            Assert.IsFalse(migrationTask.IsFaulted, "三端 replacement Relay Host 创建不应抛异常。");
            Assert.IsTrue(string.IsNullOrWhiteSpace(migrationTask.Result),
                "三端 replacement Relay Host 创建应成功，失败原因: " + migrationTask.Result);
            Assert.IsTrue(GetBool("IsHost"), "三端候选 Client 应已成为 replacement Relay Host。");
            Assert.IsTrue(GetBool("IsListeningOrConnected"), "三端 replacement Relay Host 应处于监听状态。");

            string migrationJoinCode = GetString("RelayJoinCode");
            Assert.IsFalse(string.IsNullOrWhiteSpace(migrationJoinCode), "三端 replacement Relay Host 应生成新房间码。");
            Assert.AreNotEqual(oldJoinCode, migrationJoinCode,
                "三端 replacement Relay Host 必须生成不同于旧 Relay 的新房间码。");
            WriteAtomicFile(MigrationCodeFilePath, migrationJoinCode);
            Debug.Log("[RelayMigration3Test][candidate] 已写出 migration 新房间码=" + migrationJoinCode);

            yield return WaitUntil(() => GetInt("ConnectedClientCount") >= 3,
                PeerConnectTimeout, () => "三端 replacement Relay Host 未等到旧 Host 和观察客户端重连。当前连接数="
                    + GetInt("ConnectedClientCount") + " 状态: " + GetString("RelayStatus"));

            string oldHostMarker = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationOldHostReconnectedPath, out oldHostMarker),
                PeerConnectTimeout, () => "三端 replacement Host 未等到旧 Host 写出重连 clientId。");

            string observerNewReady = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationObserverNewReadyPath, out observerNewReady),
                PeerConnectTimeout, () => "三端 replacement Host 未等到观察客户端完成新 Relay 稳定性断言。");

            yield return VerifyThreeClientPostRestoreRemoteTaskMeetingVotingFlow(
                MarkerUlong(oldHostMarker, "clientId"));

            yield return RunFrames(60);
            // old Host/observer 在写出各自 PASS 后会正常退出；此时 replacement
            // Host 可能只剩自身连接。前面的 >=3 和远端任务/投票门禁已证明迁移链路。
            Assert.GreaterOrEqual(GetInt("ConnectedClientCount"), 1,
                "三端迁移连续性完成后 replacement Host 至少应保持自身连接。");
            WriteResult("migration-candidate-threeclient", "PASS",
                "oldCode=" + oldJoinCode + " migrationCode=" + migrationJoinCode
                + " replacementHost=true connectedClients(incl self)=" + GetInt("ConnectedClientCount")
                + " postRestoreRemoteTaskRpc=true postRestoreRemoteVoting=true observerStable=true");
        }

        [UnityTest]
        [Timeout(TestTimeoutMilliseconds)]
        public IEnumerator RelayMigration_ThreeClientObserverFollowsReplacementRelay()
        {
            if (!string.Equals(Role, "migration-observer-threeclient", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"未设置 {RoleEnv}=migration-observer-threeclient，跳过 Relay 三端 migration observer 用例。");
            }

            yield return null;

            string oldJoinCode = null;
            yield return WaitUntil(() => TryReadAtomicFile(CodeFilePath, out oldJoinCode),
                CodeAvailableTimeout, () => "三端观察客户端未读到旧 Host Relay 房间码。");

            Debug.Log("[RelayMigration3Test][observer] 加入旧 Relay=" + oldJoinCode);
            yield return ConnectRelayClientWithRetries(
                oldJoinCode,
                "三端观察客户端旧 Relay",
                attempts: 3,
                perAttemptTimeout: 75f);
            WriteAtomicFile(MigrationObserverOldReadyPath, "observer-old-connected");

            string migrationJoinCode = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationCodeFilePath, out migrationJoinCode),
                PeerConnectTimeout, () => "三端观察客户端未等到 migration 新 Relay 房间码。");
            Assert.AreNotEqual(oldJoinCode, migrationJoinCode,
                "观察客户端必须从旧 Relay 切换到新的 migration Relay。");

            Invoke("RequestShutdown");
            yield return WaitUntil(() => !GetBool("IsListeningOrConnected"),
                ServiceReadyTimeout, () => "三端观察客户端关闭旧 Relay 连接超时。");

            Debug.Log("[RelayMigration3Test][observer] 加入新 Relay=" + migrationJoinCode);
            yield return ConnectRelayClientWithRetries(
                migrationJoinCode,
                "三端观察客户端新 Relay",
                attempts: 3,
                perAttemptTimeout: 75f);

            yield return RunFrames(60);
            Assert.IsTrue(GetBool("IsClientConnected"), "三端观察客户端迁移到新 Relay 后连接应保持稳定。");
            WriteAtomicFile(MigrationObserverNewReadyPath, "observer-new-connected");
            yield return WaitForRemoteVoteRequestAndSubmit(
                "observer",
                MigrationObserverVoteSubmittedPath);
            yield return RunFrames(60);
            Assert.IsTrue(GetBool("IsClientConnected"), "观察客户端提交迁移后投票后连接应保持稳定。");
            WriteResult("migration-observer-threeclient", "PASS",
                "oldCode=" + oldJoinCode + " migrationCode=" + migrationJoinCode
                + " reconnected=true remoteVoteSubmitted=true");
        }

        // ──────────────────────────────────────────────────────────
        //  等待辅助：每帧轮询条件，超时则带诊断信息 Fail。
        // ──────────────────────────────────────────────────────────

        private IEnumerator RunFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, Func<string> onTimeout)
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

            if (!condition())
            {
                WriteResult(Role ?? "?", "FAIL", onTimeout());
                Assert.Fail(onTimeout());
            }
        }

        private IEnumerator ConnectRelayClientWithRetries(
            string joinCode,
            string label,
            int attempts,
            float perAttemptTimeout)
        {
            string lastStatus = string.Empty;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                Debug.Log($"[RelayMigration3Test][observer] {label} Join attempt {attempt}/{attempts}: {joinCode}");
                InvokeWithString("RequestRelayClient", joinCode);

                float deadline = Time.realtimeSinceStartup + perAttemptTimeout;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (GetBool("IsClientConnected"))
                    {
                        Debug.Log($"[RelayMigration3Test][observer] {label} connected on attempt {attempt}. 状态=" + GetString("RelayStatus"));
                        yield break;
                    }

                    lastStatus = GetString("RelayStatus");
                    if (lastStatus.StartsWith("Relay 加入失败", StringComparison.Ordinal)
                        || lastStatus.Contains("Client 启动失败"))
                    {
                        break;
                    }

                    yield return null;
                }

                lastStatus = GetString("RelayStatus");
                if (attempt < attempts)
                {
                    Debug.LogWarning($"[RelayMigration3Test][observer] {label} attempt {attempt} 未连上，重置连接后重试。状态={lastStatus}");
                    Invoke("RequestShutdown");

                    float shutdownDeadline = Time.realtimeSinceStartup + 20f;
                    while (Time.realtimeSinceStartup < shutdownDeadline && GetBool("IsListeningOrConnected"))
                    {
                        yield return null;
                    }

                    yield return RunFrames(90);
                }
            }

            string failure = $"{label} 多次加入 Relay 失败。最后状态: {lastStatus}";
            WriteResult(Role ?? "?", "FAIL", failure);
            Assert.Fail(failure);
        }

        private static void WriteResult(string role, string status, string detail)
        {
            try
            {
                string logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(logsDirectory);
                File.WriteAllText(
                    Path.Combine(logsDirectory, $"relay-{role}-result.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + status
                        + System.Environment.NewLine + detail);
            }
            catch (Exception)
            {
                // 结果文件只是辅助，写失败不影响断言结论。
            }
        }

        private static void WriteMaliciousMarker(string detail)
        {
            string tmp = MaliciousMarkerPath + ".tmp";
            File.WriteAllText(tmp, detail);
            if (File.Exists(MaliciousMarkerPath))
            {
                File.Delete(MaliciousMarkerPath);
            }
            File.Move(tmp, MaliciousMarkerPath);
        }

        private static bool TryReadMaliciousMarker(out string detail)
        {
            return TryReadAtomicFile(MaliciousMarkerPath, out detail);
        }

        private static void WriteAtomicFile(string path, string detail)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, detail);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tmp, path);
        }

        private static bool TryReadAtomicFile(string path, out string detail)
        {
            detail = null;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                detail = File.ReadAllText(path)?.Trim();
                return !string.IsNullOrWhiteSpace(detail);
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static int MarkerInt(string detail, string key)
        {
            Assert.IsTrue(TryReadMarkerValue(detail, key, out string raw),
                "marker 缺少字段 " + key + ": " + detail);
            Assert.IsTrue(int.TryParse(raw, out int value),
                "marker 字段 " + key + " 不是 int: " + raw);
            return value;
        }

        private static ulong MarkerUlong(string detail, string key)
        {
            Assert.IsTrue(TryReadMarkerValue(detail, key, out string raw),
                "marker 缺少字段 " + key + ": " + detail);
            Assert.IsTrue(ulong.TryParse(raw, out ulong value),
                "marker 字段 " + key + " 不是 ulong: " + raw);
            return value;
        }

        private static bool TryReadMarkerValue(string detail, string key, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(detail))
            {
                return false;
            }

            string[] parts = detail.Split(new[] { ' ', '\n', '\r', '\t', ';' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                int separator = part.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                if (string.Equals(part.Substring(0, separator), key, StringComparison.Ordinal))
                {
                    value = part.Substring(separator + 1);
                    return !string.IsNullOrWhiteSpace(value);
                }
            }

            return false;
        }

        private IEnumerator VerifyThreeClientPostRestoreRemoteTaskMeetingVotingFlow(ulong oldHostClientId)
        {
            List<ulong> ids = GetConnectedClientIds();
            Assert.GreaterOrEqual(ids.Count, 3, "post-restore 连续性采样需要 replacement Host + 两名远端客户端。");
            ids.Sort();

            NetworkManager manager = GetNetworkManager();
            Assert.IsNotNull(manager, "replacement Host 应存在 NetworkManager。");
            Assert.Contains(oldHostClientId, ids,
                "旧 Host 写出的 clientId 必须存在于 replacement Relay 的连接列表。");
            ulong policeA = manager.LocalClientId;
            ulong policeB = oldHostClientId;
            ulong gang = ids.Find(id => id != policeA && id != policeB);
            Assert.AreNotEqual(policeA, policeB, "replacement Host 与旧 Host 必须是不同 clientId。");
            Assert.AreNotEqual(ulong.MaxValue, gang, "应能从三端连接中选出 observer 作为黑帮玩家。");

            SeedThreeClientMigrationSnapshot(policeA, policeB, gang);
            object snapshot = _controllerType.GetMethod("CaptureSnapshot").Invoke(_controller, null);
            Assert.IsNotNull(snapshot, "三端 migration candidate 应能捕获对局快照。");

            ClearMigratedStateBeforeRestore();
            _controllerType.GetMethod("RestoreFromSnapshot").Invoke(_controller, new[] { snapshot });

            Assert.AreEqual("Action", GetProp("Phase").ToString());
            Assert.AreEqual(3, GetInt("PlayerCount"));
            Assert.AreEqual(2, TaskProgress(9));
            Assert.IsFalse(TaskCompleted(9));

            InvokeNoArgs("BroadcastSnapshot");
            yield return WaitUntil(() => FindServerMiniGameBridge() != null,
                ServiceReadyTimeout, () => "replacement Host 未找到已 Spawn 的服务器 MiniGameBridge。");

            OpenMinigameOnServer(policeB, 9);
            WriteAtomicFile(MigrationRemoteTaskRequestPath,
                "taskId=9 clientId=" + policeB + " gangId=" + gang);

            yield return WaitUntil(() => TaskCompleted(9),
                PeerConnectTimeout, () => "旧 Host 未通过真实 MiniGameBridge ServerRpc 完成迁移后任务。状态: "
                    + GetString("Status"));
            Assert.IsTrue(TaskCompleted(9), "三端 migration 后应允许继续完成恢复前推进中的任务。");
            Assert.Greater(GetInt("EvidenceScore"), 10, "三端 migration 后任务完成仍应推进证据链。");

            InvokeBeginMeeting("三端迁移后任务会议");
            InvokeNoArgs("BroadcastSnapshot");
            InvokeWithUlong("RequestVote", gang);
            WriteAtomicFile(MigrationRemoteVoteRequestPath,
                "targetId=" + gang + " oldHostVoterId=" + policeB + " observerVoterId=" + gang);

            yield return WaitUntil(() => GetProp("Phase").ToString() == "Result",
                PeerConnectTimeout, () => "旧 Host/observer 未通过真实 ClientAction named message 完成迁移后投票。当前阶段="
                    + GetProp("Phase") + " 状态: " + GetString("Status"));

            Assert.AreEqual("Result", GetProp("Phase").ToString(),
                "三端 migration 后任务、会议、投票应能连续推进到结算。");
            Assert.IsFalse(PlayerAlive(gang));
            StringAssert.Contains("警方胜利", GetString("Status"));
        }

        private IEnumerator WaitForRemoteTaskRequestAndSubmit()
        {
            string detail = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationRemoteTaskRequestPath, out detail),
                PeerConnectTimeout, () => "旧 Host 未等到 replacement Host 发起迁移后远端任务请求。");

            int taskId = MarkerInt(detail, "taskId");
            ulong expectedClientId = MarkerUlong(detail, "clientId");
            NetworkManager manager = GetNetworkManager();
            Assert.IsNotNull(manager, "远端任务提交需要 NetworkManager。");
            Assert.AreEqual(expectedClientId, manager.LocalClientId,
                "远端任务请求应定向到当前旧 Host 客户端。");

            yield return WaitUntil(() => FindClientMiniGameBridge() != null,
                ServiceReadyTimeout, () => "旧 Host 未收到 MiniGameBridge client clone，无法提交任务结果。");

            InvokeBridgeRpc(FindClientMiniGameBridge(), "SubmitTaskResultServerRpc", taskId, true);
            WriteAtomicFile(MigrationRemoteTaskSubmittedPath,
                "taskId=" + taskId + " clientId=" + manager.LocalClientId);
        }

        private IEnumerator WaitForRemoteVoteRequestAndSubmit(string label, string submittedPath)
        {
            string detail = null;
            yield return WaitUntil(() => TryReadAtomicFile(MigrationRemoteVoteRequestPath, out detail),
                PeerConnectTimeout, () => label + " 未等到 replacement Host 发起迁移后远端投票请求。");

            ulong targetId = MarkerUlong(detail, "targetId");
            InvokeWithUlong("RequestVote", targetId);

            NetworkManager manager = GetNetworkManager();
            ulong localClientId = manager != null ? manager.LocalClientId : ulong.MaxValue;
            WriteAtomicFile(submittedPath,
                "targetId=" + targetId + " voterId=" + localClientId + " role=" + label);
        }

        private NetworkBehaviour FindServerMiniGameBridge()
        {
            NetworkManager manager = GetNetworkManager();
            return FindMiniGameBridge(bridge =>
                bridge.NetworkManager == manager
                && bridge.IsSpawned
                && bridge.IsServer);
        }

        private NetworkBehaviour FindClientMiniGameBridge()
        {
            NetworkManager manager = GetNetworkManager();
            return FindMiniGameBridge(bridge =>
                bridge.NetworkManager == manager
                && bridge.IsSpawned
                && bridge.IsClient
                && !bridge.IsServer);
        }

        private NetworkBehaviour FindMiniGameBridge(Func<NetworkBehaviour, bool> predicate)
        {
            UnityEngine.Object[] bridges = UnityEngine.Object.FindObjectsByType(
                RuntimeType(MiniGameBridgeTypeName),
                FindObjectsSortMode.None);

            foreach (UnityEngine.Object bridge in bridges)
            {
                if (bridge is NetworkBehaviour networkBehaviour && predicate(networkBehaviour))
                {
                    return networkBehaviour;
                }
            }

            return null;
        }

        private void OpenMinigameOnServer(ulong clientId, int taskId)
        {
            NetworkBehaviour bridge = FindServerMiniGameBridge();
            Assert.IsNotNull(bridge, "replacement Host 应拥有服务器 MiniGameBridge。");

            MethodInfo mi = bridge.GetType().GetMethod("OpenMinigameOnClient",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(ulong), typeof(int) }, null);
            Assert.IsNotNull(mi, "找不到 OnlineMiniGameBridge.OpenMinigameOnClient(ulong, int)。");
            mi.Invoke(bridge, new object[] { clientId, taskId });
        }

        private void InvokeBridgeRpc(NetworkBehaviour bridge, string methodName, int taskId, bool success)
        {
            Assert.IsNotNull(bridge, "找不到可调用 " + methodName + " 的 MiniGameBridge。");
            MethodInfo mi = bridge.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, "找不到 OnlineMiniGameBridge." + methodName + "。");
            mi.Invoke(bridge, new object[] { taskId, success, default(RpcParams) });
        }

        private void SeedThreeClientMigrationSnapshot(ulong policeA, ulong policeB, ulong gang)
        {
            SetFieldValue("matchStarted", true);
            SetFieldValue("phase", Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), "Action"));
            SetFieldValue("lastMeetingReason", "三端迁移前会议");
            SetFieldValue("lastVoteOutcome", "三端迁移前未结算");

            IDictionary players = GetField("players") as IDictionary;
            Assert.IsNotNull(players, "controller.players 应可作为 IDictionary 访问。");
            players.Clear();
            players[policeA] = CreatePlayer(policeA, "接管警探", Vector3.zero, "Police", "Inspector");
            players[policeB] = CreatePlayer(policeB, "旧主机警探", Vector3.right, "Police", "Tech");
            players[gang] = CreatePlayer(gang, "跟随黑帮", Vector3.left, "Gang", "Enforcer");

            IDictionary privateRoles = GetField("privateRoles") as IDictionary;
            Assert.IsNotNull(privateRoles, "controller.privateRoles 应可作为 IDictionary 访问。");
            privateRoles.Clear();
            privateRoles[policeA] = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Police");
            privateRoles[policeB] = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Police");
            privateRoles[gang] = Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineRole"), "Gang");

            IList tasks = GetField("tasks") as IList;
            Assert.IsNotNull(tasks, "controller.tasks 应可作为 IList 访问。");
            tasks.Clear();
            tasks.Add(CreateTask(9, Vector3.zero, progress: 2, requiredProgress: 3, completed: false, sabotaged: false));

            IDictionary votes = GetField("votes") as IDictionary;
            Assert.IsNotNull(votes, "controller.votes 应可作为 IDictionary 访问。");
            votes.Clear();

            SetTaskServiceEvidence(10, 99);
            SetFieldValue("emergencyMeetingsLeft", 1);
            SetFieldValue("emergencyCooldownTimer", 0f);
            SetFieldValue("phaseTimer", 15f);
        }

        private void ClearMigratedStateBeforeRestore()
        {
            (GetField("players") as IDictionary)?.Clear();
            (GetField("privateRoles") as IDictionary)?.Clear();
            (GetField("votes") as IDictionary)?.Clear();
            (GetField("tasks") as IList)?.Clear();
            SetTaskServiceEvidence(0, 1);
            SetFieldValue("matchStarted", false);
            SetFieldValue("phase", Enum.Parse(RuntimeType("GanglandUndercover.Online.OnlineMatchPhase"), "Lobby"));
        }

        private object CreatePlayer(
            ulong clientId,
            string displayName,
            Vector3 position,
            string roleName,
            string professionName)
        {
            Type playerType = RuntimeType("GanglandUndercover.Online.OnlinePlayerState");
            Type roleType = RuntimeType("GanglandUndercover.Online.OnlineRole");
            Type professionType = RuntimeType("GanglandUndercover.Online.OnlineProfession");
            return Activator.CreateInstance(
                playerType,
                clientId,
                displayName,
                position,
                true,
                true,
                Enum.Parse(roleType, roleName),
                Enum.Parse(professionType, professionName),
                0,
                false);
        }

        private object CreateTask(
            int taskId,
            Vector3 position,
            int progress,
            int requiredProgress,
            bool completed,
            bool sabotaged)
        {
            return Activator.CreateInstance(
                RuntimeType("GanglandUndercover.Online.OnlineTaskState"),
                taskId,
                "Task" + taskId,
                position,
                progress,
                requiredProgress,
                completed,
                sabotaged);
        }

        private List<ulong> GetConnectedClientIds()
        {
            NetworkManager manager = GetNetworkManager();
            Assert.IsNotNull(manager, "NetworkManager 应存在。");

            List<ulong> ids = new List<ulong>();
            foreach (ulong id in manager.ConnectedClientsIds)
            {
                ids.Add(id);
            }

            return ids;
        }

        private void SetTaskServiceEvidence(int score, int target)
        {
            object taskService = GetField("taskService");
            Assert.IsNotNull(taskService, "taskService 应已初始化。");
            taskService.GetType().GetProperty("EvidenceScore").SetValue(taskService, score);
            taskService.GetType().GetProperty("EvidenceTarget").SetValue(taskService, target);
            MethodInfo syncEvidence = _controllerType.GetMethod("SyncEvidenceServiceFromController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            syncEvidence?.Invoke(_controller, null);
        }

        private int TaskProgress(int taskId)
        {
            object task = FindTask(taskId);
            return Convert.ToInt32(task.GetType().GetField("Progress").GetValue(task));
        }

        private bool TaskCompleted(int taskId)
        {
            object task = FindTask(taskId);
            return Convert.ToBoolean(task.GetType().GetField("Completed").GetValue(task));
        }

        private object FindTask(int taskId)
        {
            IEnumerable tasks = GetField("tasks") as IEnumerable;
            Assert.IsNotNull(tasks, "controller.tasks 应可枚举。");

            foreach (object task in tasks)
            {
                if (Convert.ToInt32(task.GetType().GetField("Id").GetValue(task)) == taskId)
                {
                    return task;
                }
            }

            Assert.Fail("找不到任务 " + taskId);
            return null;
        }

        private bool PlayerAlive(ulong clientId)
        {
            IDictionary players = GetField("players") as IDictionary;
            Assert.IsNotNull(players, "controller.players 应可作为 IDictionary 访问。");
            // 远端 observer 在写出 PASS 后会退出进程，replacement Host 可能先收到
            // disconnect 并移除该玩家；对结算断言而言，缺席玩家与 Alive=false 等价。
            if (!players.Contains(clientId))
            {
                return false;
            }
            object player = players[clientId];
            return Convert.ToBoolean(player.GetType().GetField("Alive").GetValue(player));
        }

        // ──────────────────────────────────────────────────────────
        //  反射访问辅助（与 MatchLoopPlayTests 一致）
        // ──────────────────────────────────────────────────────────

        private void Invoke(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}()");
            mi.Invoke(_controller, null);
        }

        private void InvokeNoArgs(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.IsNotNull(mi, $"找不到方法 {method}()");
            mi.Invoke(_controller, null);
        }

        private void InvokeWithString(string method, string arg)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}(string)");
            mi.Invoke(_controller, new object[] { arg });
        }

        private void InvokeWithUlong(string method, ulong arg)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(ulong) }, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}(ulong)");
            mi.Invoke(_controller, new object[] { arg });
        }

        private void InvokeWithUlongInt(string method, ulong clientId, int taskId)
        {
            MethodInfo mi = _controllerType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(ulong), typeof(int) }, null);
            Assert.IsNotNull(mi, $"找不到方法 {method}(ulong, int)");
            mi.Invoke(_controller, new object[] { clientId, taskId });
        }

        private bool InvokeBoolOutString(string methodName, ulong clientId, int taskId, out string message)
        {
            MethodInfo mi = _controllerType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(ulong), typeof(int), typeof(string).MakeByRefType() }, null);
            Assert.IsNotNull(mi, $"找不到方法 {methodName}(ulong, int, out string)");
            object[] args = { clientId, taskId, string.Empty };
            bool accepted = (bool)mi.Invoke(_controller, args);
            message = (string)args[2];
            return accepted;
        }

        private void InvokeBeginMeeting(string reason)
        {
            MethodInfo mi = _controllerType.GetMethod("BeginMeeting",
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(string), typeof(ulong), typeof(bool) }, null);
            Assert.IsNotNull(mi, "找不到 BeginMeeting(string, ulong, bool)");
            mi.Invoke(_controller, new object[] { reason, 0UL, false });
        }

        private void InvokeApplyVote(ulong voterClientId, ulong targetClientId)
        {
            MethodInfo mi = _controllerType.GetMethod("ApplyVote",
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(ulong), typeof(ulong) }, null);
            Assert.IsNotNull(mi, "找不到 ApplyVote(ulong, ulong)");
            mi.Invoke(_controller, new object[] { voterClientId, targetClientId });
        }

        private Task<string> InvokeReplacementRelayHostForMigration()
        {
            MethodInfo mi = _controllerType.GetMethod("TryStartReplacementRelayHostForMigrationAsync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(mi, "找不到 TryStartReplacementRelayHostForMigrationAsync()。");
            object result = mi.Invoke(_controller, null);
            Assert.IsInstanceOf<Task<string>>(result, "replacement Relay Host 入口应返回 Task<string>。");
            return (Task<string>)result;
        }

        private void SendMaliciousRelayPayloads()
        {
            SendRoleAssignFromClient(3);
            SendMapSelectFromClient(1);
            SendServerSnapshotFromClient();
            SendChatBroadcastFromClient("relay-forged-broadcast");
            SendChatSendFromClient("relay-lobby-chat-should-not-appear");
        }

        private void SendRoleAssignFromClient(int roleValue)
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(roleValue);
            SendNamedToServer(RoleAssignMessage, writer);
        }

        private void SendMapSelectFromClient(int mapTypeValue)
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(mapTypeValue);
            SendNamedToServer(MapSelectMessage, writer);
        }

        private void SendServerSnapshotFromClient()
        {
            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(true);
            SendNamedToServer(ServerSnapshotMessage, writer);
        }

        private void SendChatSendFromClient(string content)
        {
            FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp);
            try
            {
                object[] args = { writer, content };
                StaticPrivate("WriteChatSendPayload").Invoke(null, args);
                writer = (FastBufferWriter)args[0];
                SendNamedToServer(ChatSendMessage, writer);
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
                object[] args = { writer, "999", "Relay伪造者", content, true, faction, channel };
                StaticPrivate("WriteChatBroadcastPayload").Invoke(null, args);
                writer = (FastBufferWriter)args[0];
                SendNamedToServer(ChatBroadcastMessage, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void SendNamedToServer(string messageName, FastBufferWriter writer)
        {
            NetworkManager manager = GetNetworkManager();
            Assert.IsNotNull(manager?.CustomMessagingManager, "Client NetworkManager CustomMessagingManager 应可用。");
            manager.CustomMessagingManager.SendNamedMessage(messageName, NetworkManager.ServerClientId, writer);
        }

        private void SendCameraWatchRequestFromClient()
        {
            object camera = FindClientSecurityCamera();
            Assert.IsNotNull(camera, "Client 应能找到已 spawn 的监控摄像头 clone。");

            SendCameraWatchRequestFromClient(camera);
        }

        private void SendCameraWatchRequestFromClient(object camera)
        {
            Assert.IsNotNull(camera, "Client 摄像头对象不能为空。");

            MethodInfo mi = camera.GetType().GetMethod("StartWatchingServerRpc",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(mi, "找不到 OnlineSecurityCamera.StartWatchingServerRpc。");
            mi.Invoke(camera, new object[] { default(RpcParams) });
        }

        private void SubscribeToCameraData(object camera)
        {
            FieldInfo callbackField = camera.GetType().GetField("OnCameraDataReceived",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(callbackField, "OnlineSecurityCamera 应暴露 OnCameraDataReceived 回调。");

            Type callbackType = callbackField.FieldType;
            Type payloadArrayType = callbackType.GetGenericArguments()[0];
            Type payloadType = payloadArrayType.GetElementType();
            MethodInfo bridge = GetType().GetMethod(nameof(OnCameraDataBridge),
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(bridge, "找不到摄像头回调测试桥接方法。");
            Delegate handler = Delegate.CreateDelegate(
                callbackType,
                this,
                bridge.MakeGenericMethod(payloadType));
            callbackField.SetValue(camera, handler);
        }

        private void OnCameraDataBridge<T>(T[] data)
        {
            _cameraDataUpdateCount++;
            if (data != null && data.Length > 0)
            {
                _cameraNonEmptyDataCount++;
            }
        }

        private ulong PlaceRemotePlayerInsideFirstCameraZone(out ulong cameraNetworkObjectId)
        {
            List<ulong> clientIds = GetConnectedClientIds();
            ulong remoteClientId = ulong.MaxValue;
            foreach (ulong clientId in clientIds)
            {
                if (clientId != NetworkManager.ServerClientId)
                {
                    remoteClientId = clientId;
                    break;
                }
            }

            Assert.AreNotEqual(ulong.MaxValue, remoteClientId,
                "摄像头合法观看门禁需要一个真实远端 Client。");

            IEnumerable cameras = GetField("surveillanceCameras") as IEnumerable;
            Assert.IsNotNull(cameras, "Host surveillanceCameras 应可枚举。");
            object firstCamera = null;
            foreach (object camera in cameras)
            {
                if (camera != null)
                {
                    firstCamera = camera;
                    break;
                }
            }
            Assert.IsNotNull(firstCamera, "Host 应至少有一个摄像头用于合法观看门禁。");
            NetworkBehaviour cameraNetworkBehaviour = firstCamera as NetworkBehaviour;
            Assert.IsNotNull(cameraNetworkBehaviour, "Host 摄像头应为已生成的 NetworkBehaviour。");
            Assert.IsTrue(cameraNetworkBehaviour.IsSpawned, "Host 摄像头应已通过 NGO 生成。");
            cameraNetworkObjectId = cameraNetworkBehaviour.NetworkObjectId;

            PropertyInfo centerProperty = firstCamera.GetType().GetProperty("ZoneCenter",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(centerProperty, "OnlineSecurityCamera.ZoneCenter 应可读。");
            Vector2 center = (Vector2)centerProperty.GetValue(firstCamera);

            IDictionary players = GetField("players") as IDictionary;
            Assert.IsNotNull(players, "Host players 应可枚举。");
            Assert.IsTrue(players.Contains(remoteClientId), "Host 应已建立远端 Client 玩家状态。");
            object state = players[remoteClientId];
            FieldInfo positionField = state.GetType().GetField("Position",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(positionField, "OnlinePlayerState.Position 应可写入测试位置。");
            positionField.SetValue(state, new Vector3(center.x, center.y, 0f));
            players[remoteClientId] = state;
            return remoteClientId;
        }

        private void SendCharacterCustomFromClient()
        {
            NetworkBehaviour customizer = FindClientNonOwnerCharacterCustomizer();
            Assert.IsNotNull(customizer, "Client 应能找到 server-owned CharacterCustomizer clone。");

            const string json = "{\"hat\":\"hat_none\",\"top\":\"top_jacket\",\"bottom\":\"bottom_pants\",\"accessory\":\"acc_none\",\"skinTone\":\"skin_light\",\"height\":\"height_m\"}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            using FastBufferWriter writer = new FastBufferWriter(jsonBytes.Length + 16, Allocator.Temp);
            writer.WriteValueSafe(customizer.NetworkObjectId);
            writer.WriteValueSafe(jsonBytes.Length);
            writer.WriteBytesSafe(jsonBytes, jsonBytes.Length);

            NetworkManager manager = GetNetworkManager();
            Assert.IsNotNull(manager?.CustomMessagingManager, "Client NetworkManager CustomMessagingManager 应可用。");
            manager.CustomMessagingManager.SendNamedMessage(
                CharacterCustomMessage,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private object FindClientSecurityCamera()
        {
            return FindClientSecurityCamera(null);
        }

        private object FindClientSecurityCamera(ulong networkObjectId)
        {
            return FindClientSecurityCamera((ulong?)networkObjectId);
        }

        private object FindClientSecurityCamera(ulong? networkObjectId)
        {
            UnityEngine.Object[] cameras = UnityEngine.Object.FindObjectsByType(
                RuntimeType(CameraTypeName),
                FindObjectsSortMode.None);

            foreach (UnityEngine.Object camera in cameras)
            {
                if (camera is NetworkBehaviour networkBehaviour
                    && networkBehaviour.IsSpawned
                    && networkBehaviour.IsClient
                    && !networkBehaviour.IsServer
                    && (!networkObjectId.HasValue
                        || networkBehaviour.NetworkObjectId == networkObjectId.Value))
                {
                    return camera;
                }
            }

            return null;
        }

        private NetworkBehaviour FindClientNonOwnerCharacterCustomizer()
        {
            UnityEngine.Object[] customizers = UnityEngine.Object.FindObjectsByType(
                RuntimeType(CharacterCustomizerTypeName),
                FindObjectsSortMode.None);

            foreach (UnityEngine.Object customizer in customizers)
            {
                if (customizer is NetworkBehaviour networkBehaviour
                    && networkBehaviour.IsSpawned
                    && networkBehaviour.IsClient
                    && !networkBehaviour.IsServer
                    && !networkBehaviour.IsOwner
                    && networkBehaviour.OwnerClientId == NetworkManager.ServerClientId)
                {
                    return networkBehaviour;
                }
            }

            return null;
        }

        private object GetProp(string name)
        {
            PropertyInfo pi = _controllerType.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(pi, $"找不到属性 {name}");
            return pi.GetValue(_controller);
        }

        private object GetField(string name)
        {
            FieldInfo fi = _controllerType.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            return fi.GetValue(_controller);
        }

        private void SetFieldValue(string name, object value)
        {
            FieldInfo fi = _controllerType.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"找不到字段 {name}");
            fi.SetValue(_controller, value);
        }

        private NetworkManager GetNetworkManager()
        {
            return GetField("networkManager") as NetworkManager;
        }

        private string GetActiveMapTypeName()
        {
            object mapService = GetField("mapService");
            PropertyInfo activeMap = mapService.GetType().GetProperty("ActiveMapType");
            Assert.IsNotNull(activeMap, "找不到 mapService.ActiveMapType。");
            return activeMap.GetValue(mapService).ToString();
        }

        private int GetServerChatMessageCount()
        {
            object chatSystem = GetField("chatSystem");
            Assert.IsNotNull(chatSystem, "Host ChatSystem 应已初始化。");
            return Convert.ToInt32(chatSystem.GetType().GetProperty("MessageCount").GetValue(chatSystem));
        }

        private int GetServerCameraCount()
        {
            IEnumerable cameras = GetField("surveillanceCameras") as IEnumerable;
            Assert.IsNotNull(cameras, "Host surveillanceCameras 应可枚举。");

            int count = 0;
            foreach (object camera in cameras)
            {
                if (camera != null)
                {
                    count++;
                }
            }
            return count;
        }

        private int GetServerCameraWatcherCount()
        {
            IEnumerable cameras = GetField("surveillanceCameras") as IEnumerable;
            Assert.IsNotNull(cameras, "Host surveillanceCameras 应可枚举。");

            int total = 0;
            foreach (object camera in cameras)
            {
                if (camera == null)
                {
                    continue;
                }

                FieldInfo watchersField = camera.GetType().GetField("_watchingPlayers",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(watchersField, "找不到 OnlineSecurityCamera._watchingPlayers。");

                object watchers = watchersField.GetValue(camera);
                Assert.IsNotNull(watchers, "OnlineSecurityCamera._watchingPlayers 不应为空。");

                PropertyInfo countProperty = watchers.GetType().GetProperty("Count");
                Assert.IsNotNull(countProperty, "OnlineSecurityCamera._watchingPlayers 应暴露 Count。");
                total += Convert.ToInt32(countProperty.GetValue(watchers));
            }
            return total;
        }

        private string GetServerCharacterCustomizerJson(ulong ownerClientId)
        {
            IDictionary customizers = GetField("characterCustomizers") as IDictionary;
            Assert.IsNotNull(customizers, "Host characterCustomizers 应可枚举。");

            foreach (DictionaryEntry entry in customizers)
            {
                if (!Equals(entry.Key, ownerClientId) || entry.Value == null)
                {
                    continue;
                }

                MethodInfo mi = entry.Value.GetType().GetMethod("GetCustomDataJson",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(mi, "找不到 CharacterCustomizer.GetCustomDataJson。");
                return (string)mi.Invoke(entry.Value, null);
            }

            return null;
        }

        private MethodInfo StaticPrivate(string method)
        {
            MethodInfo mi = _controllerType.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, $"找不到静态私有方法 {method}");
            return mi;
        }

        private static Type RuntimeType(string fullName)
            => Type.GetType(fullName + ", " + RuntimeAssemblyName, throwOnError: true);

        private int GetInt(string name) => Convert.ToInt32(GetProp(name));
        private bool GetBool(string name) => (bool)GetProp(name);
        private string GetString(string name) => (string)GetProp(name);
    }
}
