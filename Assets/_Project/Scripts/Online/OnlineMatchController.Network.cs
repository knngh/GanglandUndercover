using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using GanglandUndercover;
using GanglandUndercover.Core;
using GanglandUndercover.Online;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {
        private const int ChatMaxContentBytes = 4096;
        private const int ChatMaxNameBytes = 256;
        private const int ChatMaxIdBytes = 64;
        private const int ChatWriterCapacityBytes = ChatMaxContentBytes + 1024;
        private const float ServerChatSendCooldownSeconds = 5f;
        private const int MaxSnapshotPlayers = 64;
        private const int MaxSnapshotTasks = 256;
        private const int MaxSnapshotBodies = 128;
        private const int MaxSnapshotVotes = 64;
        private const int MaxSnapshotCaseLogEntries = 512;

        // --- EnsureNetworkStack ---
        private void EnsureNetworkStack()
        {
            networkManager = GetComponent<NetworkManager>();

            if (networkManager == null)
            {
                networkManager = GetComponentInChildren<NetworkManager>(true);
            }

            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>();
            }

            if (networkManager == null)
            {
                GameObject networkObject = new GameObject("NetworkManager");
                networkManager = networkObject.AddComponent<NetworkManager>();
                transport = networkObject.AddComponent<UnityTransport>();
                networkManager.NetworkConfig = new NetworkConfig();
                networkManager.NetworkConfig.NetworkTransport = transport;

                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(networkObject);
                }
                else
                {
                    networkObject.transform.SetParent(transform, false);
                }
            }
            else
            {
                transport = networkManager.GetComponent<UnityTransport>();

                if (transport == null)
                {
                    transport = networkManager.gameObject.AddComponent<UnityTransport>();
                }

                if (networkManager.NetworkConfig == null)
                {
                    networkManager.NetworkConfig = new NetworkConfig();
                }

                networkManager.NetworkConfig.NetworkTransport = transport;
            }

            // 本作世界完全由 PrototypeBootstrap 程序化生成，对局不依赖 NGO 的场景同步。
            // 若保留默认开启的场景管理，远端 Client 接入时 NGO 会尝试按场景哈希同步当前
            // 活动场景（Prototype），而该哈希不在 Client 的 build scene 表里，导致抛出
            // "Scene Hash ... does not exist in the HashToBuildIndex table"。关闭它既消除该
            // 异常，也符合"程序化生成、无需场景同步"的实际架构。
            if (networkManager.NetworkConfig != null)
            {
                networkManager.NetworkConfig.EnableSceneManagement = false;
            }

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;

            // A1: 注册监控摄像头 NetworkPrefab，避免运行时 AddComponent<NetworkObject>.Spawn()
            // 导致 globalObjectIdHash=0，远端无法复制。
            RegisterSurveillanceCameraPrefab();
            RegisterMiniGameBridgePrefab();
        }

        // --- RegisterSurveillanceCameraPrefab ---
        private void RegisterSurveillanceCameraPrefab()
        {
            if (surveillanceCameraTemplate != null) return;
            if (TryReuseRegisteredSurveillanceCameraPrefab(out surveillanceCameraTemplate)) return;

            surveillanceCameraTemplate = Resources.Load<GameObject>(SurveillanceCameraPrefabResourcePath);
            if (surveillanceCameraTemplate == null)
            {
                Debug.LogError("[A1] Missing Resources prefab: " + SurveillanceCameraPrefabResourcePath);
                return;
            }

            networkManager.NetworkConfig.Prefabs.Add(
                new Unity.Netcode.NetworkPrefab
                {
                    Prefab = surveillanceCameraTemplate
                });
        }

        // --- TryReuseRegisteredSurveillanceCameraPrefab ---
        private bool TryReuseRegisteredSurveillanceCameraPrefab(out GameObject template)
        {
            template = null;

            if (networkManager?.NetworkConfig?.Prefabs == null)
            {
                return false;
            }

            foreach (NetworkPrefab prefab in networkManager.NetworkConfig.Prefabs.Prefabs)
            {
                GameObject candidate = prefab?.Prefab != null ? prefab.Prefab : prefab?.OverridingTargetPrefab;
                if (candidate != null && candidate.GetComponent<Online.Surveillance.OnlineSecurityCamera>() != null)
                {
                    template = candidate;
                    return true;
                }
            }

            return false;
        }

        // --- RegisterMiniGameBridgePrefab ---
        private void RegisterMiniGameBridgePrefab()
        {
            if (miniGameBridgeTemplate != null) return;
            if (TryReuseRegisteredMiniGameBridgePrefab(out miniGameBridgeTemplate)) return;

            miniGameBridgeTemplate = Resources.Load<GameObject>(MiniGameBridgePrefabResourcePath);
            if (miniGameBridgeTemplate == null)
            {
                Debug.LogError("[MiniGameBridge] Missing Resources prefab: " + MiniGameBridgePrefabResourcePath);
                return;
            }

            networkManager.NetworkConfig.Prefabs.Add(
                new Unity.Netcode.NetworkPrefab
                {
                    Prefab = miniGameBridgeTemplate
                });
        }

        // --- TryReuseRegisteredMiniGameBridgePrefab ---
        private bool TryReuseRegisteredMiniGameBridgePrefab(out GameObject template)
        {
            template = null;

            if (networkManager?.NetworkConfig?.Prefabs == null)
            {
                return false;
            }

            foreach (NetworkPrefab prefab in networkManager.NetworkConfig.Prefabs.Prefabs)
            {
                GameObject candidate = prefab?.Prefab != null ? prefab.Prefab : prefab?.OverridingTargetPrefab;
                if (candidate != null && candidate.GetComponent<Online.MiniGames.OnlineMiniGameBridge>() != null)
                {
                    template = candidate;
                    return true;
                }
            }

            return false;
        }

        // --- EnsureMiniGameBridgeNetworkObject ---
        private void EnsureMiniGameBridgeNetworkObject()
        {
            if (!Application.isPlaying || networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (miniGameBridge != null && miniGameBridge.IsSpawned)
            {
                miniGameBridge.BindController(this);
                return;
            }

            if (miniGameBridgeTemplate == null)
            {
                RegisterMiniGameBridgePrefab();
            }

            if (miniGameBridgeTemplate == null)
            {
                Debug.LogError("[MiniGameBridge] NetworkPrefab template not registered!");
                return;
            }

            NetworkObject networkObject = NetworkObject.InstantiateAndSpawn(
                miniGameBridgeTemplate,
                networkManager,
                ownerClientId: NetworkManager.ServerClientId,
                destroyWithScene: false,
                isPlayerObject: false,
                forceOverride: false);

            if (networkObject == null)
            {
                Debug.LogError("[MiniGameBridge] Failed to spawn NetworkPrefab.");
                return;
            }

            GameObject bridgeObject = networkObject.gameObject;
            bridgeObject.name = "OnlineMiniGameBridge";
            DontDestroyOnLoad(bridgeObject);

            miniGameBridge = bridgeObject.GetComponent<Online.MiniGames.OnlineMiniGameBridge>();
            miniGameBridge.BindController(this);
        }

        // --- EnsureServiceBootstrap ---
        private void EnsureServiceBootstrap()
        {
            if (serviceBootstrap != null)
            {
                return;
            }

            serviceBootstrap = GetComponent<UnityServiceBootstrap>();

            if (serviceBootstrap == null)
            {
                serviceBootstrap = gameObject.AddComponent<UnityServiceBootstrap>();
            }
        }

        // --- EnsureCanvasHud ---
        private void EnsureCanvasHud()
        {
            if (onlineHud != null)
            {
                return;
            }

            OnlineMatchHud existingHud = GetComponentInChildren<OnlineMatchHud>(true);

            if (existingHud != null)
            {
                onlineHud = existingHud;
                onlineHud.Bind(this);
                return;
            }

            GameObject hudObject = new GameObject("Online Match HUD");
            hudObject.transform.SetParent(transform, false);
            onlineHud = hudObject.AddComponent<OnlineMatchHud>();
            onlineHud.Bind(this);
        }

        // --- StartHost ---
        private void StartHost()
        {
            try
            {
                disconnectedNetworkSession = false;
                relayJoinCode = string.Empty;
                relayStatus = "使用直连 Host。";
                ConfigureTransport("0.0.0.0");
                RegisterMessages();

                if (networkManager.StartHost())
                {
                    localPreviewMode = false;
                    status = "Host 已创建。等待玩家 Ready。";
                    AddCaseLog(status);
                    UpsertLocalPlayer();
                    SendClientProfile();
                    EnsureMiniGameBridgeNetworkObject();
                    EnsureSurveillanceCameraNetworkObjects();
                    PlayCue("start");
                    BroadcastSnapshot();
                }
                else
                {
                    StartLocalPreviewRoom();
                    status = "Host 创建失败，已切换本地试玩模式。";
                    AddCaseLog(status);
                }
            }
            catch (Exception exception)
            {
                StartLocalPreviewRoom();
                status = "Host 启动异常，已切换本地试玩模式：" + exception.GetType().Name;
                AddCaseLog(status);
            }
        }

        // --- StartClient ---
        private void StartClient(string address)
        {
            string safeAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            disconnectedNetworkSession = false;
            relayJoinCode = string.Empty;
            relayStatus = "使用直连 Client。";
            ConfigureTransport(safeAddress);
            RegisterMessages();

            if (networkManager.StartClient())
            {
                status = "Client 正在连接 " + safeAddress + "。";
                AddCaseLog(status);
            }
            else
            {
                status = "Client 加入失败。";
            }
        }

        // --- StartRelayHost ---
        private async void StartRelayHost()
        {
            if (relayOperationInProgress)
            {
                return;
            }

            disconnectedNetworkSession = false;
            relayOperationInProgress = true;
            relayStatus = "Relay 正在创建房间码。";
            status = relayStatus;
            OnRelayStatusChanged?.Invoke(relayStatus);

            try
            {
                EnsureServiceBootstrap();
                EnsureNetworkStack();
                await serviceBootstrap.InitializeAsync();

                if (!CanUseRelay(out string reason))
                {
                    await CleanupJoinedLobbySessionAsync();
                    relayStatus = reason;
                    status = reason;
                    AddCaseLog(reason);
                    OnRelayStatusChanged?.Invoke(reason);
                    return;
                }

                int maxConnections = Mathf.Clamp(roomMaxPlayers - 1, 1, ruleSet.MaximumRoomPlayers - 1);
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                relayJoinCode = (await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId) ?? string.Empty).Trim().ToUpperInvariant();
                transport.UseWebSockets = false;
                transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
                RegisterMessages();

                if (networkManager.StartHost())
                {
                    localPreviewMode = false;
                    relayHostClientId = networkManager.LocalClientId;
                    relayStatus = "Relay 房间码: " + relayJoinCode;
                    status = "Relay Host 已创建。分享房间码 " + relayJoinCode + "。";
                    AddCaseLog(status);
                    UpsertLocalPlayer();
                    SendClientProfile();
                    EnsureMiniGameBridgeNetworkObject();
                    EnsureSurveillanceCameraNetworkObjects();
                    UpsertLocalRelayLobbyRoom();
                    RequestPublishRelayLobbySession();
                    PlayCue("start");
                    BroadcastSnapshot();

                    OnRelayStatusChanged?.Invoke(relayStatus);
                    OnRelayRoomCodeReady?.Invoke(relayJoinCode);
                    OnRelayConnectionChanged?.Invoke(true);
                }
                else
                {
                    relayStatus = "Relay Host 启动失败。";
                    status = relayStatus;
                    AddCaseLog(status);
                    OnRelayStatusChanged?.Invoke(relayStatus);
                }
            }
            catch (Exception exception)
            {
                relayJoinCode = string.Empty;
                relayStatus = "Relay 创建失败：" + exception.Message;
                status = relayStatus;
                AddCaseLog(status);
                OnRelayStatusChanged?.Invoke(relayStatus);
            }
            finally
            {
                relayOperationInProgress = false;
            }
        }

        // --- StartRelayClient ---
        private async void StartRelayClient()
        {
            if (relayOperationInProgress)
            {
                return;
            }

            string safeJoinCode = (relayJoinInput ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(safeJoinCode))
            {
                relayStatus = "请输入 Relay 房间码。";
                status = relayStatus;
                OnRelayStatusChanged?.Invoke(relayStatus);
                return;
            }

            disconnectedNetworkSession = false;
            relayOperationInProgress = true;
            relayStatus = "Relay 正在加入 " + safeJoinCode + "。";
            status = relayStatus;
            OnRelayStatusChanged?.Invoke(relayStatus);

            try
            {
                EnsureServiceBootstrap();
                EnsureNetworkStack();
                await serviceBootstrap.InitializeAsync();

                if (!CanUseRelay(out string reason))
                {
                    relayStatus = reason;
                    status = reason;
                    AddCaseLog(reason);
                    OnRelayStatusChanged?.Invoke(reason);
                    return;
                }

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(safeJoinCode);
                relayJoinCode = safeJoinCode;
                transport.UseWebSockets = false;
                transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
                RegisterMessages();

                if (networkManager.StartClient())
                {
                    relayStatus = "Relay 已发送加入请求: " + safeJoinCode;
                    status = relayStatus;
                    AddCaseLog(status);
                    OnRelayStatusChanged?.Invoke(relayStatus);
                    OnRelayConnectionChanged?.Invoke(true);
                }
                else
                {
                    await CleanupJoinedLobbySessionAsync();
                    relayStatus = "Relay Client 启动失败。";
                    status = relayStatus;
                    OnRelayStatusChanged?.Invoke(relayStatus);
                }
            }
            catch (Exception exception)
            {
                await CleanupJoinedLobbySessionAsync();
                relayStatus = "Relay 加入失败：" + exception.Message;
                status = relayStatus;
                AddCaseLog(status);
                OnRelayStatusChanged?.Invoke(relayStatus);
            }
            finally
            {
                relayOperationInProgress = false;
            }
        }

        // --- CanUseRelay ---
        private bool CanUseRelay(out string reason)
        {
            if (serviceBootstrap == null)
            {
                reason = "Unity Services 未挂载，Relay 暂不可用。";
                return false;
            }

            if (!serviceBootstrap.CloudProjectBound)
            {
                reason = "Unity Cloud Project 未绑定，Relay 暂不可用。";
                return false;
            }

            if (!serviceBootstrap.ServicesReady || !serviceBootstrap.AuthenticationReady || !serviceBootstrap.RelayReady)
            {
                reason = "Relay 未就绪：" + serviceBootstrap.ServiceReadinessSummary;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        // --- StartLocalPreviewRoom ---
        private void StartLocalPreviewRoom()
        {
            disconnectedNetworkSession = false;
            localPreviewMode = true;
            localReady = true;
            localPlayerName = OnlineMatchUtils.LimitText(localPlayerName, 16, "港区玩家");

            if (!players.ContainsKey(LocalPreviewClientId))
            {
                players[LocalPreviewClientId] = new OnlinePlayerState(LocalPreviewClientId, localPlayerName, FindNearestOpenPosition(localPosition, Vector3.zero), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }
            else
            {
                OnlinePlayerState state = players[LocalPreviewClientId];
                state.DisplayName = localPlayerName;
                state.Ready = true;
                state.IsBot = false;
                players[LocalPreviewClientId] = state;
            }

            killSystem.killCooldowns[LocalPreviewClientId] = 0f;
            abilityCooldowns[LocalPreviewClientId] = 0f;
            status = "本地试玩房间已创建。";
            AddCaseLog(status);
            PlayCue("start");
        }

        // --- Shutdown ---
        private void Shutdown()
        {
            CleanupJoinedLobbySession();
            CleanupPublishedLobbySession();
            UnregisterMessages();

            if (networkManager != null && networkManager.IsListening)
            {
                try
                {
                    networkManager.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Gangland network shutdown skipped: " + exception.GetType().Name);
                }
            }

            players.Clear();
            killSystem.bodies.Clear();
            caseLog.Clear();
            votes.Clear();
            privateRoles.Clear();
            killSystem.killCooldowns.Clear();
            abilityCooldowns.Clear();
            serverChatLastSendTimes.Clear();
            _botController?.Clear();
            localRole = OnlineRole.Unassigned;
            phase = OnlineMatchPhase.Lobby;
            localPreviewMode = false;
            disconnectedNetworkSession = false;
            localReady = false;
            matchStarted = false;
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;
            submittingActiveTask = false;
            taskService.EvidenceScore = 0;
            lastMeetingReason = "尚未召开会议。";
            lastVoteOutcome = "尚未投票。";
            lastEvidenceEvent = "尚未取得关键证据。";
            lastSabotageEvent = "尚未发生破坏。";
            evidenceMilestoneIndex = 0;
            phaseTimer = 0f;
            taskService.ResetAllSabotageTimers();
            emergencyCooldownTimer = 0f;
            aiActionGraceTimer = 0f;
            _cameraRig.SetSubject(LocalPreviewClientId);
            resultSummary = "尚未结算。";
            status = "已离开房间。";
            relayJoinCode = string.Empty;
            relayStatus = "Relay 房间码未创建。";
            chatSystem?.Clear();
        }

        // --- ConfigureTransport ---
        private void ConfigureTransport(string address)
        {
            transport.UseWebSockets = false;
            transport.UseEncryption = false;
            transport.SetConnectionData(address, DefaultPort);
        }

        // --- RegisterMessages ---
        private void RegisterMessages()
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            UnregisterMessages();
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ClientStateMessage, ReceiveClientState);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ClientActionMessage, ReceiveClientAction);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ClientProfileMessage, ReceiveClientProfile);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ServerSnapshotMessage, ReceiveServerSnapshot);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RoleAssignMessage, ReceiveRoleAssign);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ChatSendMessage, ReceiveChatSend);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ChatBroadcastMessage, ReceiveChatBroadcast);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MapSelectMessage, ReceiveMapSelect); // D5

            // 主机迁移消息
            if (migrationManager != null)
            {
                migrationManager.RegisterMessageHandlers(networkManager);
            }
            else
            {
                EnsureMigrationManager();
                migrationManager?.RegisterMessageHandlers(networkManager);
            }
        }

        // --- UnregisterMessages ---
        private void UnregisterMessages()
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ClientStateMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ClientActionMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ClientProfileMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ServerSnapshotMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RoleAssignMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ChatSendMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ChatBroadcastMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MapSelectMessage); // D5

            // 主机迁移消息
            migrationManager?.UnregisterMessageHandlers(networkManager);
        }

        // --- HandleClientConnected ---
        private void HandleClientConnected(ulong clientId)
        {
            disconnectedNetworkSession = false;

            if (networkManager.IsServer)
            {
                EnsureMiniGameBridgeNetworkObject();

                Vector3 spawn = mapService.SpawnPosition(players.Count);
                players[clientId] = new OnlinePlayerState(clientId, "玩家" + clientId, spawn, false, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
                killSystem.killCooldowns[clientId] = 0f;
                abilityCooldowns[clientId] = 0f;
                BroadcastSnapshot();
            }

            if (clientId == LocalClientId())
            {
                UpsertLocalPlayer();
                SendClientProfile();
                status = networkManager.IsHost ? "Host 在线。" : "Client 已连接。";
                AddCaseLog(status);
            }
        }

        // --- HandleClientDisconnected ---
        private void HandleClientDisconnected(ulong clientId)
        {
            // 主机迁移管理：通知迁移管理器检测主机断连
            migrationManager?.OnClientDisconnected(clientId);

            if (IsRemoteHostDisconnect(clientId))
            {
                MarkHostDisconnectedForRecovery();
                return;
            }

            ReleaseTasksHeldByClient(clientId);
            RemoveDisconnectedPlayerVotes(clientId);

            players.Remove(clientId);
            privateRoles.Remove(clientId);
            killSystem.killCooldowns.Remove(clientId);
            abilityCooldowns.Remove(clientId);
            serverChatLastSendTimes.Remove(clientId);
            _botController?.RemoveBot(clientId);

            if (networkManager != null && networkManager.IsServer)
            {
                AddCaseLog("玩家" + clientId + " 已离开房间。");

                int aliveCount = CountAlivePlayers();
                if ((phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting)
                    && aliveCount > 0
                    && votes.Count >= aliveCount)
                {
                    ResolveVotes();
                    return;
                }

                EvaluateWinConditions();
                BroadcastSnapshot();
            }
        }

        // --- IsRemoteHostDisconnect ---
        private bool IsRemoteHostDisconnect(ulong clientId)
        {
            if (clientId != NetworkManager.ServerClientId || localPreviewMode)
            {
                return false;
            }

            return networkManager == null || !networkManager.IsHost;
        }

        // --- MarkHostDisconnectedForRecovery ---
        private void MarkHostDisconnectedForRecovery()
        {
            disconnectedNetworkSession = true;
            localReady = false;
            matchStarted = false;
            phase = OnlineMatchPhase.Lobby;

            string safeJoinCode = OnlineMatchUtils.CleanRelayJoinInput(relayJoinCode);
            string codeStatus = string.IsNullOrEmpty(safeJoinCode)
                ? string.Empty
                : "，房间码 " + safeJoinCode + " 已失效";
            string message = "Host 已断开" + codeStatus + "。请返回主菜单或重新开房。";
            status = message;
            relayStatus = message;
            AddCaseLog(message);
            OnRelayConnectionChanged?.Invoke(false);
            OnRelayStatusChanged?.Invoke(relayStatus);
        }

        // --- ReleaseTasksHeldByClient ---
        private void ReleaseTasksHeldByClient(ulong clientId)
        {
            if (activeTaskUsers == null || activeTaskUsers.Count == 0)
            {
                return;
            }

            List<int> taskIdsToRelease = new List<int>();
            foreach (KeyValuePair<int, ulong> pair in activeTaskUsers)
            {
                if (pair.Value == clientId)
                {
                    taskIdsToRelease.Add(pair.Key);
                }
            }

            for (int i = 0; i < taskIdsToRelease.Count; i++)
            {
                activeTaskUsers.Remove(taskIdsToRelease[i]);
            }
        }

        // --- RemoveDisconnectedPlayerVotes ---
        private void RemoveDisconnectedPlayerVotes(ulong clientId)
        {
            votes.Remove(clientId);

            List<ulong> votersToClear = new List<ulong>();
            foreach (KeyValuePair<ulong, ulong> vote in votes)
            {
                if (vote.Value == clientId)
                {
                    votersToClear.Add(vote.Key);
                }
            }

            for (int i = 0; i < votersToClear.Count; i++)
            {
                votes.Remove(votersToClear[i]);
            }
        }

        // --- SendClientAction ---
        private void SendClientAction(OnlineActionType actionType, ulong targetClientId = SkipVoteTarget)
        {
            if (localPreviewMode)
            {
                ApplyClientAction(LocalPreviewClientId, actionType, targetClientId);
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsClient)
            {
                return;
            }

            if (networkManager.IsHost)
            {
                ApplyClientAction(networkManager.LocalClientId, actionType, targetClientId);
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(32, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe((int)actionType);
            writer.WriteValueSafe(targetClientId);
            networkManager.CustomMessagingManager.SendNamedMessage(ClientActionMessage, NetworkManager.ServerClientId, writer);
        }

        // --- SendClientState ---
        private void SendClientState(bool force = false)
        {
            if (localPreviewMode)
            {
                ApplyClientState(LocalPreviewClientId, localPosition, localInput, localReady);
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsClient)
            {
                return;
            }

            clientSnapshotTimer -= Time.deltaTime;

            if (!force && clientSnapshotTimer > 0f)
            {
                return;
            }

            clientSnapshotTimer = SnapshotIntervalSeconds;

            if (networkManager.IsHost)
            {
                ApplyClientState(networkManager.LocalClientId, localPosition, localInput, localReady);
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(localPosition);
            writer.WriteValueSafe(localInput);
            writer.WriteValueSafe(localReady);
            networkManager.CustomMessagingManager.SendNamedMessage(ClientStateMessage, NetworkManager.ServerClientId, writer);
        }

        // --- SendClientProfile ---
        private void SendClientProfile()
        {
            if (localPreviewMode)
            {
                ApplyClientProfile(LocalPreviewClientId, OnlineMatchUtils.LimitText(localPlayerName, 16, "港区玩家"));
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsClient)
            {
                return;
            }

            string safeName = OnlineMatchUtils.LimitText(localPlayerName, 16, "港区玩家");
            localPlayerName = safeName;

            if (networkManager.IsHost)
            {
                ApplyClientProfile(networkManager.LocalClientId, safeName);
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(safeName);
            networkManager.CustomMessagingManager.SendNamedMessage(ClientProfileMessage, NetworkManager.ServerClientId, writer);
        }

        // --- ReceiveClientState ---
        private void ReceiveClientState(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Vector2 input);
            reader.ReadValueSafe(out bool ready);
            ApplyClientState(senderClientId, position, input, ready);
        }

        // --- ReceiveClientProfile ---
        private void ReceiveClientProfile(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out string displayName);
            ApplyClientProfile(senderClientId, displayName);
        }

        // --- ReceiveClientAction ---
        private void ReceiveClientAction(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out int actionValue);
            reader.ReadValueSafe(out ulong targetClientId);
            if (!IsDefinedOnlineAction(actionValue))
            {
                return;
            }

            ApplyClientAction(senderClientId, (OnlineActionType)actionValue, targetClientId);
        }

        // --- ApplyClientAction ---
        private void ApplyClientAction(ulong senderClientId, OnlineActionType actionType, ulong targetClientId)
        {
            if (!IsDefinedOnlineAction((int)actionType))
            {
                return;
            }

            if ((!localPreviewMode && (networkManager == null || !networkManager.IsServer)) || !players.TryGetValue(senderClientId, out OnlinePlayerState player))
            {
                return;
            }

            if (actionType == OnlineActionType.Vote || actionType == OnlineActionType.SkipVote)
            {
                ApplyVote(senderClientId, actionType == OnlineActionType.SkipVote ? SkipVoteTarget : targetClientId);
                return;
            }

            if (phase == OnlineMatchPhase.Lobby || phase == OnlineMatchPhase.Opening || phase == OnlineMatchPhase.Result || !player.Alive)
            {
                return;
            }

            if (actionType == OnlineActionType.Report)
            {
                TryReportOrEmergency(senderClientId, player);
                return;
            }

            if (phase != OnlineMatchPhase.Action)
            {
                return;
            }

            if (actionType == OnlineActionType.Kill)
            {
                TryKill(senderClientId, player);
                return;
            }

            if (actionType == OnlineActionType.Interact)
            {
                TryInteractWithTask(senderClientId, player);
                return;
            }

            if (actionType == OnlineActionType.Ability)
            {
                TryUseProfessionAbility(senderClientId, player);
                return;
            }

            if (actionType == OnlineActionType.Vent)
            {
                TryUseUnderworldPassage(senderClientId, player);
            }
        }

        // --- ApplyClientState ---
        private void ApplyClientState(ulong senderClientId, Vector3 position, Vector2 input, bool ready)
        {
            if (!IsFinite(position) || !IsFinite(input))
            {
                return;
            }

            bool knownPlayer = players.TryGetValue(senderClientId, out OnlinePlayerState existing);
            if (!knownPlayer && matchStarted && phase != OnlineMatchPhase.Lobby)
            {
                return;
            }

            OnlinePlayerState state = knownPlayer
                ? existing
                : new OnlinePlayerState(senderClientId, "玩家" + senderClientId, position, ready, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);

            if (!matchStarted || phase == OnlineMatchPhase.Lobby)
            {
                state.Position = mapService.ClampToOnlineMap(position);
                state.Ready = ready;
            }

            state.Input = state.Alive && phase == OnlineMatchPhase.Action ? ClampClientInput(input) : Vector2.zero;
            players[senderClientId] = state;
        }

        // --- IsDefinedOnlineAction ---
        private static bool IsDefinedOnlineAction(int actionValue)
        {
            return Enum.IsDefined(typeof(OnlineActionType), actionValue);
        }

        // --- IsServerSender ---
        private static bool IsServerSender(ulong senderClientId)
        {
            return senderClientId == NetworkManager.ServerClientId;
        }

        // --- IsDefinedOnlineMatchPhase ---
        private static bool IsDefinedOnlineMatchPhase(int phaseValue)
        {
            return Enum.IsDefined(typeof(OnlineMatchPhase), phaseValue);
        }

        // --- IsDefinedOnlineRole ---
        private static bool IsDefinedOnlineRole(int roleValue)
        {
            return Enum.IsDefined(typeof(OnlineRole), roleValue);
        }

        // --- IsDefinedOnlineProfession ---
        private static bool IsDefinedOnlineProfession(int professionValue)
        {
            return Enum.IsDefined(typeof(OnlineProfession), professionValue);
        }

        // --- IsDefinedCriticalTaskType ---
        private static bool IsDefinedCriticalTaskType(byte criticalTaskType)
        {
            return Enum.IsDefined(typeof(SocialDeduction.CriticalTaskType), criticalTaskType);
        }

        // --- IsDefinedMapType ---
        private static bool IsDefinedMapType(int mapTypeValue)
        {
            return Enum.IsDefined(typeof(OnlineMapService.OnlineMapType), mapTypeValue);
        }

        // --- IsSnapshotCountInRange ---
        private static bool IsSnapshotCountInRange(int count, int maxCount)
        {
            return count >= 0 && count <= maxCount;
        }

        // --- ToDefinedOnlineRole ---
        private static OnlineRole ToDefinedOnlineRole(int roleValue)
        {
            return IsDefinedOnlineRole(roleValue) ? (OnlineRole)roleValue : OnlineRole.Unassigned;
        }

        // --- ToDefinedOnlineProfession ---
        private static OnlineProfession ToDefinedOnlineProfession(int professionValue)
        {
            return IsDefinedOnlineProfession(professionValue) ? (OnlineProfession)professionValue : OnlineProfession.Inspector;
        }

        // --- ToDefinedCriticalTaskType ---
        private static SocialDeduction.CriticalTaskType ToDefinedCriticalTaskType(byte criticalTaskType)
        {
            return IsDefinedCriticalTaskType(criticalTaskType)
                ? (SocialDeduction.CriticalTaskType)criticalTaskType
                : SocialDeduction.CriticalTaskType.None;
        }

        // --- ClampClientInput ---
        private static Vector2 ClampClientInput(Vector2 input)
        {
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        // --- IsFinite ---
        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        // --- IsFinite ---
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        // --- IsFinite ---
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // --- ApplyClientProfile ---
        private void ApplyClientProfile(ulong senderClientId, string displayName)
        {
            string safeName = OnlineMatchUtils.LimitText(displayName, 16, "港区玩家");

            if (players.TryGetValue(senderClientId, out OnlinePlayerState state))
            {
                state.DisplayName = safeName;
                state.IsBot = false;
                players[senderClientId] = state;
            }
            else
            {
                players[senderClientId] = new OnlinePlayerState(senderClientId, safeName, mapService.SpawnPosition(players.Count), false, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }

            status = safeName + " 已进入房间。";
            AddCaseLog(status);
            BroadcastSnapshot();
        }

        // --- BroadcastSnapshot ---
        internal void BroadcastSnapshot()
        {
            if (localPreviewMode)
            {
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (!networkManager.IsListening && !networkManager.IsClient && !networkManager.IsServer)
            {
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(8192, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(matchStarted);
            writer.WriteValueSafe((int)phase);
            writer.WriteValueSafe(taskService.EvidenceScore);
            writer.WriteValueSafe(taskService.EvidenceTarget);
            writer.WriteValueSafe(emergencyMeetingsLeft);
            writer.WriteValueSafe(roomMinPlayers);
            writer.WriteValueSafe(roomMaxPlayers);
            writer.WriteValueSafe(roomAutoFillAi);
            writer.WriteValueSafe(revealRoleOnEject);
            writer.WriteValueSafe(proximityVoiceEnabled);
            writer.WriteValueSafe(roomName);
            writer.WriteValueSafe(resultSummary);
            writer.WriteValueSafe(lastMeetingReason);
            writer.WriteValueSafe(lastVoteOutcome);
            writer.WriteValueSafe(lastEvidenceEvent);
            writer.WriteValueSafe(lastSabotageEvent);
            writer.WriteValueSafe(evidenceMilestoneIndex);
            writer.WriteValueSafe(phaseTimer);
            writer.WriteValueSafe(taskService.BlackoutTimer);
            writer.WriteValueSafe(taskService.LockdownTimer);
            writer.WriteValueSafe(taskService.CommunicationJamTimer);
            writer.WriteValueSafe(taskService.EvidenceLeakTimer);
            writer.WriteValueSafe(taskService.PatrolAlertTimer);
            writer.WriteValueSafe(taskService.EvidenceLeakAccumulator);
            writer.WriteValueSafe(emergencyCooldownTimer);
            writer.WriteValueSafe(killSystem.reportCooldownTimer);
            writer.WriteValueSafe(aiActionGraceTimer);
            writer.WriteValueSafe(matchElapsedSeconds);
            writer.WriteValueSafe(_criticalTaskActive);
            writer.WriteValueSafe((byte)_criticalTaskType);
            writer.WriteValueSafe(_criticalTaskTimeRemaining);
            writer.WriteValueSafe(players.Count);

            foreach (OnlinePlayerState state in players.Values)
            {
                float killCooldown = killSystem.killCooldowns.TryGetValue(state.ClientId, out float cooldown) ? cooldown : 0f;
                float abilityCooldown = abilityCooldowns.TryGetValue(state.ClientId, out float abilityCooldownValue) ? abilityCooldownValue : 0f;
                float ventCooldown = ventCooldowns.TryGetValue(state.ClientId, out float ventCooldownValue) ? ventCooldownValue : 0f;
                writer.WriteValueSafe(state.ClientId);
                writer.WriteValueSafe(state.DisplayName);
                writer.WriteValueSafe(state.Position);
                writer.WriteValueSafe(state.Ready);
                writer.WriteValueSafe(state.Alive);
                writer.WriteValueSafe(state.IsBot);
                writer.WriteValueSafe((int)state.PublicRole);
                writer.WriteValueSafe((int)state.Profession);
                writer.WriteValueSafe(state.Suspicion);
                writer.WriteValueSafe(killCooldown);
                writer.WriteValueSafe(abilityCooldown);
                writer.WriteValueSafe(ventCooldown);
            }

            writer.WriteValueSafe(tasks.Count);

            foreach (OnlineTaskState task in tasks)
            {
                writer.WriteValueSafe(task.Id);
                writer.WriteValueSafe(task.Position);
                writer.WriteValueSafe(task.Progress);
                writer.WriteValueSafe(task.RequiredProgress);
                writer.WriteValueSafe(task.Completed);
                writer.WriteValueSafe(task.Sabotaged);
            }

            writer.WriteValueSafe(killSystem.bodies.Count);

            foreach (OnlineBodyState body in killSystem.bodies)
            {
                writer.WriteValueSafe(body.Id);
                writer.WriteValueSafe(body.VictimClientId);
                writer.WriteValueSafe(body.Position);
                writer.WriteValueSafe(body.Reported);
            }

            writer.WriteValueSafe(votes.Count);

            foreach (KeyValuePair<ulong, ulong> vote in votes)
            {
                writer.WriteValueSafe(vote.Key);
                writer.WriteValueSafe(vote.Value);
            }

            writer.WriteValueSafe(caseLog.Count);

            foreach (string entry in caseLog)
            {
                writer.WriteValueSafe(entry);
            }

            networkManager.CustomMessagingManager.SendNamedMessageToAll(ServerSnapshotMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        // --- ReceiveServerSnapshot ---
        private void ReceiveServerSnapshot(ulong senderClientId, FastBufferReader reader)
        {
            if ((networkManager != null && networkManager.IsServer) || !IsServerSender(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out bool snapshotMatchStarted);
            reader.ReadValueSafe(out int phaseValue);
            if (!IsDefinedOnlineMatchPhase(phaseValue))
            {
                return;
            }

            reader.ReadValueSafe(out int snapshotEvidenceScore);
            reader.ReadValueSafe(out int snapshotEvidenceTarget);
            reader.ReadValueSafe(out int snapshotEmergencyMeetingsLeft);
            reader.ReadValueSafe(out int snapshotRoomMinPlayers);
            reader.ReadValueSafe(out int snapshotRoomMaxPlayers);
            reader.ReadValueSafe(out bool snapshotAutoFillAi);
            reader.ReadValueSafe(out bool snapshotRevealRoleOnEject);
            reader.ReadValueSafe(out bool snapshotProximityVoice);
            reader.ReadValueSafe(out string snapshotRoomName);
            reader.ReadValueSafe(out string snapshotResultSummary);
            reader.ReadValueSafe(out string snapshotLastMeetingReason);
            reader.ReadValueSafe(out string snapshotLastVoteOutcome);
            reader.ReadValueSafe(out string snapshotLastEvidenceEvent);
            reader.ReadValueSafe(out string snapshotLastSabotageEvent);
            reader.ReadValueSafe(out int snapshotEvidenceMilestoneIndex);
            reader.ReadValueSafe(out float snapshotPhaseTimer);
            reader.ReadValueSafe(out float snapshotBlackoutTimer);
            reader.ReadValueSafe(out float snapshotLockdownTimer);
            reader.ReadValueSafe(out float snapshotCommunicationJamTimer);
            reader.ReadValueSafe(out float snapshotEvidenceLeakTimer);
            reader.ReadValueSafe(out float snapshotPatrolAlertTimer);
            reader.ReadValueSafe(out float snapshotEvidenceLeakAccumulator);
            reader.ReadValueSafe(out float snapshotEmergencyCooldownTimer);
            reader.ReadValueSafe(out float snapshotReportCooldownTimer);
            reader.ReadValueSafe(out float snapshotAiActionGraceTimer);
            reader.ReadValueSafe(out float snapshotMatchElapsedSeconds);
            reader.ReadValueSafe(out bool snapshotCriticalTaskActive);
            reader.ReadValueSafe(out byte snapshotCriticalTaskType);
            reader.ReadValueSafe(out float snapshotCriticalTaskTimeRemaining);
            reader.ReadValueSafe(out int count);
            if (!IsDefinedCriticalTaskType(snapshotCriticalTaskType) || !IsSnapshotCountInRange(count, MaxSnapshotPlayers))
            {
                return;
            }

            HashSet<ulong> seenPlayers = new HashSet<ulong>();
            Dictionary<ulong, OnlinePlayerState> snapshotPlayers = new Dictionary<ulong, OnlinePlayerState>();
            bool hasSnapshotLocalPosition = false;
            Vector3 snapshotLocalPosition = localPosition;

            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out string displayName);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out bool ready);
                reader.ReadValueSafe(out bool alive);
                reader.ReadValueSafe(out bool isBot);
                reader.ReadValueSafe(out int roleValue);
                reader.ReadValueSafe(out int professionValue);
                reader.ReadValueSafe(out int suspicion);
                reader.ReadValueSafe(out float killCooldown);
                reader.ReadValueSafe(out float abilityCooldown);
                reader.ReadValueSafe(out float ventCooldown);
                OnlineRole publicRole = ToDefinedOnlineRole(roleValue);
                OnlineProfession profession = ToDefinedOnlineProfession(professionValue);

                OnlinePlayerState state = players.TryGetValue(clientId, out OnlinePlayerState existing)
                    ? existing
                    : new OnlinePlayerState(clientId, displayName, position, ready, alive, publicRole, profession, suspicion, isBot);

                state.DisplayName = displayName;
                state.Position = position;
                state.Ready = ready;
                state.Alive = alive;
                state.IsBot = isBot;
                state.PublicRole = publicRole;
                state.Profession = profession;
                state.Suspicion = suspicion;
                state.KillCooldown = killCooldown;
                state.AbilityCooldown = abilityCooldown;
                state.VentCooldown = ventCooldown;
                snapshotPlayers[clientId] = state;
                seenPlayers.Add(clientId);

                if (clientId == LocalClientId())
                {
                    hasSnapshotLocalPosition = true;
                    snapshotLocalPosition = position;
                }
            }

            reader.ReadValueSafe(out int taskCount);
            if (!IsSnapshotCountInRange(taskCount, MaxSnapshotTasks))
            {
                return;
            }

            List<OnlineTaskState> snapshotTasks = new List<OnlineTaskState>(taskCount);

            for (int i = 0; i < taskCount; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out int progress);
                reader.ReadValueSafe(out int requiredProgress);
                reader.ReadValueSafe(out bool completed);
                reader.ReadValueSafe(out bool sabotaged);
                snapshotTasks.Add(new OnlineTaskState(id, OnlineWorldBuilder.TaskNameFor(id), position, progress, requiredProgress, completed, sabotaged));
            }

            reader.ReadValueSafe(out int bodyCount);
            if (!IsSnapshotCountInRange(bodyCount, MaxSnapshotBodies))
            {
                return;
            }

            List<OnlineBodyState> snapshotBodies = new List<OnlineBodyState>(bodyCount);

            for (int i = 0; i < bodyCount; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out ulong victimClientId);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out bool reported);
                snapshotBodies.Add(new OnlineBodyState(id, victimClientId, position, reported));
            }

            reader.ReadValueSafe(out int voteCount);
            if (!IsSnapshotCountInRange(voteCount, MaxSnapshotVotes))
            {
                return;
            }

            Dictionary<ulong, ulong> snapshotVotes = new Dictionary<ulong, ulong>();

            for (int i = 0; i < voteCount; i++)
            {
                reader.ReadValueSafe(out ulong voterClientId);
                reader.ReadValueSafe(out ulong targetClientId);
                snapshotVotes[voterClientId] = targetClientId;
            }

            reader.ReadValueSafe(out int caseLogCount);
            if (!IsSnapshotCountInRange(caseLogCount, MaxSnapshotCaseLogEntries))
            {
                return;
            }

            List<string> snapshotCaseLog = new List<string>(caseLogCount);

            for (int i = 0; i < caseLogCount; i++)
            {
                reader.ReadValueSafe(out string entry);
                snapshotCaseLog.Add(entry);
            }

            matchStarted = snapshotMatchStarted;
            phase = (OnlineMatchPhase)phaseValue;
            taskService.EvidenceScore = snapshotEvidenceScore;
            taskService.EvidenceTarget = snapshotEvidenceTarget;
            emergencyMeetingsLeft = snapshotEmergencyMeetingsLeft;
            roomMinPlayers = snapshotRoomMinPlayers;
            roomMaxPlayers = snapshotRoomMaxPlayers;
            roomAutoFillAi = snapshotAutoFillAi;
            revealRoleOnEject = snapshotRevealRoleOnEject;
            proximityVoiceEnabled = snapshotProximityVoice;
            roomName = snapshotRoomName;
            resultSummary = snapshotResultSummary;
            lastMeetingReason = snapshotLastMeetingReason;
            lastVoteOutcome = snapshotLastVoteOutcome;
            lastEvidenceEvent = snapshotLastEvidenceEvent;
            lastSabotageEvent = snapshotLastSabotageEvent;
            evidenceMilestoneIndex = snapshotEvidenceMilestoneIndex;
            phaseTimer = snapshotPhaseTimer;
            taskService.LoadSabotageTimersFromSnapshot(
                snapshotBlackoutTimer, snapshotLockdownTimer, snapshotCommunicationJamTimer,
                snapshotEvidenceLeakTimer, snapshotEvidenceLeakAccumulator, snapshotPatrolAlertTimer);
            emergencyCooldownTimer = snapshotEmergencyCooldownTimer;
            killSystem.reportCooldownTimer = snapshotReportCooldownTimer;
            aiActionGraceTimer = snapshotAiActionGraceTimer;
            matchElapsedSeconds = snapshotMatchElapsedSeconds;

            // Phase 2.4: 紧急任务状态
            _criticalTaskActive = snapshotCriticalTaskActive;
            _criticalTaskType = ToDefinedCriticalTaskType(snapshotCriticalTaskType);
            _criticalTaskTimeRemaining = snapshotCriticalTaskTimeRemaining;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in snapshotPlayers)
            {
                players[pair.Key] = pair.Value;
            }

            RemoveMissingPlayers(seenPlayers);

            if (hasSnapshotLocalPosition)
            {
                localPosition = snapshotLocalPosition;
            }

            tasks.Clear();
            tasks.AddRange(snapshotTasks);

            killSystem.bodies.Clear();
            killSystem.bodies.AddRange(snapshotBodies);

            votes.Clear();
            foreach (KeyValuePair<ulong, ulong> pair in snapshotVotes)
            {
                votes[pair.Key] = pair.Value;
            }

            caseLog.Clear();
            caseLog.AddRange(snapshotCaseLog);
            status = "同步在线局：" + OnlineMatchUtils.PhaseName(phase) + "。";

            // ── 客户端初始快照完整性检查 ──
            ValidateClientSnapshotIntegrity();
        }

        // --- ValidateClientSnapshotIntegrity ---
        private void ValidateClientSnapshotIntegrity()
        {
            bool hasIssue = false;

            if (matchStarted && players.Count == 0)
            {
                Debug.LogError("[ClientSnapshot] 完整性异常：对局已开始但玩家列表为空");
                hasIssue = true;
            }

            if (tasks.Count == 0)
            {
                Debug.LogWarning("[ClientSnapshot] 完整性警告：任务列表为空，可能影响任务系统正常运行");
                hasIssue = true;
            }

            // 各玩家关键字段检查（检测反序列化字节错位）
            foreach (var kv in players)
            {
                var p = kv.Value;
                if (string.IsNullOrEmpty(p.DisplayName))
                {
                    Debug.LogWarning($"[ClientSnapshot] 完整性警告：玩家 {p.ClientId} DisplayName 为空");
                    hasIssue = true;
                }

                // 位置检查：NaN/Infinity 表示序列化异常
                if (float.IsNaN(p.Position.x) || float.IsNaN(p.Position.y) ||
                    float.IsInfinity(p.Position.x) || float.IsInfinity(p.Position.y))
                {
                    Debug.LogError($"[ClientSnapshot] 完整性异常：玩家 {p.ClientId} 位置为 NaN/Infinity");
                    hasIssue = true;
                }

                // 未分配角色但 Alive = true 视为异常（仅对局进行中）
                if (matchStarted && p.Alive && p.PublicRole == OnlineRole.Unassigned)
                {
                    Debug.LogWarning($"[ClientSnapshot] 完整性警告：玩家 {p.ClientId} 存活但角色未分配");
                    hasIssue = true;
                }
            }

            if (!hasIssue)
            {
                Debug.Log($"[ClientSnapshot] 快照完整性检查通过。玩家 {players.Count} / 任务 {tasks.Count} / 尸体 {killSystem.bodies.Count} / 投票 {votes.Count}");
            }
        }

        // --- SendRole ---
        private void SendRole(ulong clientId, OnlineRole role)
        {
            if (clientId == LocalClientId())
            {
                localRole = role;
                status = "收到身份：" + OnlineMatchUtils.RoleName(localRole);
            }

            if (localPreviewMode || OnlineBotController.IsBotClient(clientId) || networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(16, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe((int)role);
            networkManager.CustomMessagingManager.SendNamedMessage(RoleAssignMessage, clientId, writer);
        }

        // --- ReceiveRoleAssign ---
        private void ReceiveRoleAssign(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServerSender(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out int roleValue);
            if (!IsDefinedOnlineRole(roleValue))
            {
                return;
            }

            localRole = (OnlineRole)roleValue;
            status = "收到身份：" + OnlineMatchUtils.RoleName(localRole);
        }

        // --- EnsureChatSystem ---
        private void EnsureChatSystem()
        {
            if (chatSystem == null)
            {
                chatSystem = new ChatSystem(SendChatMessage);
            }
        }

        // --- EnsureMigrationManager ---
        private void EnsureMigrationManager()
        {
            if (migrationManager != null)
            {
                return;
            }

            migrationManager = GetComponent<HostMigrationManager>();
            if (migrationManager == null)
            {
                migrationManager = gameObject.AddComponent<HostMigrationManager>();
            }
        }

        // --- SendChatMessage ---
        private void SendChatMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || chatSystem == null)
            {
                return;
            }

            if (!CanSendChatMessageNow())
            {
                return;
            }

            // 限流检查
            if (!chatSystem.CanSendNow())
            {
                return;
            }

            OnlineRole role = LocalEffectiveRole();
            Faction faction = ChatSystem.RoleToFaction(role);
            bool isDead = !IsLocalAlive();
            string senderId = LocalClientId().ToString();
            string senderName = GetLocalDisplayName();

            // 确定通道
            ChatChannel channel = ChatSystem.DetermineChannel(phase, !isDead);

            // 本地立即显示
            chatSystem.ReceiveMessage(senderId, senderName, content, isDead, faction, channel);
            chatSystem.MarkSent();

            // 联机：发送到服务器
            if (localPreviewMode)
            {
                return; // 本地试玩模式，不发送网络消息
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsClient)
            {
                return;
            }

            try
            {
                FastBufferWriter writer = new FastBufferWriter(ChatWriterCapacityBytes, Unity.Collections.Allocator.Temp);
                try
                {
                    WriteChatSendPayload(ref writer, content);
                    networkManager.CustomMessagingManager.SendNamedMessage(ChatSendMessage, NetworkManager.ServerClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Gangland Chat: Send failed - " + ex.Message);
            }
        }

        // --- ReceiveChatSend ---
        private void ReceiveChatSend(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (!TryReadChatSendPayload(ref reader, out string content))
            {
                return;
            }

            if (!players.TryGetValue(senderClientId, out OnlinePlayerState senderState))
            {
                return;
            }

            if (!CanAcceptServerChatSend(senderClientId))
            {
                return;
            }

            OnlineRole role = GetPrivateRole(senderClientId);
            ChatChannel channel = ChatSystem.DetermineChannel(phase, senderState.Alive);
            Vector3 senderPos = senderState.Position;
            Faction faction = ChatSystem.RoleToFaction(role);
            bool isDead = !senderState.Alive;
            string senderName = senderState.DisplayName;

            // 本地显示（服务器也显示）
            chatSystem.ReceiveMessage(senderClientId.ToString(), senderName, content, isDead, faction, channel);

            // 按通道路由转发
            FastBufferWriter writer = new FastBufferWriter(ChatWriterCapacityBytes, Unity.Collections.Allocator.Temp);
            try
            {
                WriteChatBroadcastPayload(
                    ref writer,
                    senderClientId.ToString(),
                    senderName,
                    content,
                    isDead,
                    faction,
                    channel);

                if (channel == ChatChannel.Meeting)
                {
                    // 会议频道：仅发送给存活玩家
                    foreach (KeyValuePair<ulong, OnlinePlayerState> kv in players)
                    {
                        if (kv.Key == senderClientId) continue;
                        if (kv.Value.Alive)
                        {
                            networkManager.CustomMessagingManager.SendNamedMessage(ChatBroadcastMessage, kv.Key, writer, Unity.Netcode.NetworkDelivery.ReliableSequenced);
                        }
                    }
                }
                else if (channel == ChatChannel.Ghost)
                {
                    // 鬼魂频道：仅发送给死亡玩家
                    foreach (KeyValuePair<ulong, OnlinePlayerState> kv in players)
                    {
                        if (kv.Key == senderClientId) continue;
                        if (!kv.Value.Alive)
                        {
                            networkManager.CustomMessagingManager.SendNamedMessage(ChatBroadcastMessage, kv.Key, writer, Unity.Netcode.NetworkDelivery.ReliableSequenced);
                        }
                    }
                }
                else if (channel == ChatChannel.Proximity)
                {
                    // 近距离频道：发送给发送者附近范围的存活玩家（不分阵营）
                    const float proximityRange = 12f;
                    foreach (KeyValuePair<ulong, OnlinePlayerState> kv in players)
                    {
                        if (kv.Key == senderClientId) continue;
                        if (!kv.Value.Alive) continue;

                        Vector3 targetPos = GetPlayerPosition(kv.Key);
                        float dist = Vector3.Distance(senderPos, targetPos);
                        if (dist <= proximityRange)
                        {
                            networkManager.CustomMessagingManager.SendNamedMessage(ChatBroadcastMessage, kv.Key, writer, Unity.Netcode.NetworkDelivery.ReliableSequenced);
                        }
                    }
                }
                else // Global
                {
                    // 全局频道：发送给所有存活玩家
                    foreach (KeyValuePair<ulong, OnlinePlayerState> kv in players)
                    {
                        if (kv.Key == senderClientId) continue;
                        if (kv.Value.Alive)
                        {
                            networkManager.CustomMessagingManager.SendNamedMessage(ChatBroadcastMessage, kv.Key, writer, Unity.Netcode.NetworkDelivery.ReliableSequenced);
                        }
                    }
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        // --- CanAcceptServerChatSend ---
        private bool CanAcceptServerChatSend(ulong senderClientId)
        {
            if (phase != OnlineMatchPhase.Action
                && phase != OnlineMatchPhase.Meeting
                && phase != OnlineMatchPhase.Voting)
            {
                return false;
            }

            if (serverChatLastSendTimes.TryGetValue(senderClientId, out float lastSentAt)
                && Time.time - lastSentAt < ServerChatSendCooldownSeconds)
            {
                return false;
            }

            serverChatLastSendTimes[senderClientId] = Time.time;
            return true;
        }

        // --- ReceiveChatBroadcast ---
        private void ReceiveChatBroadcast(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsClient)
            {
                return;
            }

            if (!IsServerSender(senderClientId))
            {
                return;
            }

            if (TryReadChatBroadcastPayload(
                    ref reader,
                    out string senderId,
                    out string senderName,
                    out string content,
                    out bool isDead,
                    out Faction faction,
                    out ChatChannel channel))
            {
                EnsureChatSystem();
                chatSystem.ReceiveMessage(senderId, senderName, content, isDead, faction, channel);
            }
        }

        // --- WriteChatSendPayload ---
        private static void WriteChatSendPayload(ref FastBufferWriter writer, string content)
        {
            WriteBoundedUtf8String(ref writer, ChatSystem.Sanitize(content) ?? string.Empty);
        }

        // --- TryReadChatSendPayload ---
        private static bool TryReadChatSendPayload(ref FastBufferReader reader, out string content)
        {
            content = string.Empty;

            if (!TryReadBoundedUtf8String(ref reader, ChatMaxContentBytes, out string rawContent))
            {
                return false;
            }

            content = ChatSystem.Sanitize(rawContent) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(content);
        }

        // --- WriteChatBroadcastPayload ---
        private static void WriteChatBroadcastPayload(
            ref FastBufferWriter writer,
            string senderId,
            string senderName,
            string content,
            bool isDead,
            Faction faction,
            ChatChannel channel)
        {
            WriteBoundedUtf8String(ref writer, senderId ?? string.Empty);
            WriteBoundedUtf8String(ref writer, senderName ?? string.Empty);
            WriteBoundedUtf8String(ref writer, ChatSystem.Sanitize(content) ?? string.Empty);
            writer.WriteValueSafe(isDead);
            writer.WriteValueSafe((int)faction);
            writer.WriteValueSafe((int)channel);
        }

        // --- TryReadChatBroadcastPayload ---
        private static bool TryReadChatBroadcastPayload(
            ref FastBufferReader reader,
            out string senderId,
            out string senderName,
            out string content,
            out bool isDead,
            out Faction faction,
            out ChatChannel channel)
        {
            senderId = string.Empty;
            senderName = string.Empty;
            content = string.Empty;
            faction = Faction.None;
            isDead = false;
            channel = ChatChannel.Global;

            if (!TryReadBoundedUtf8String(ref reader, ChatMaxIdBytes, out senderId)
                || !TryReadBoundedUtf8String(ref reader, ChatMaxNameBytes, out senderName)
                || !TryReadBoundedUtf8String(ref reader, ChatMaxContentBytes, out content))
            {
                return false;
            }

            try
            {
                reader.ReadValueSafe(out isDead);
                reader.ReadValueSafe(out int factionValue);
                reader.ReadValueSafe(out int channelValue);

                faction = Enum.IsDefined(typeof(Faction), factionValue) ? (Faction)factionValue : Faction.None;
                channel = Enum.IsDefined(typeof(ChatChannel), channelValue) ? (ChatChannel)channelValue : ChatChannel.Global;
                content = ChatSystem.Sanitize(content) ?? string.Empty;
                return !string.IsNullOrWhiteSpace(senderId)
                    && !string.IsNullOrWhiteSpace(senderName)
                    && !string.IsNullOrWhiteSpace(content);
            }
            catch
            {
                return false;
            }
        }

        // --- WriteBoundedUtf8String ---
        private static void WriteBoundedUtf8String(ref FastBufferWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.WriteValueSafe(bytes.Length);

            if (bytes.Length > 0)
            {
                writer.WriteBytesSafe(bytes, bytes.Length);
            }
        }

        // --- TryReadBoundedUtf8String ---
        private static bool TryReadBoundedUtf8String(ref FastBufferReader reader, int maxBytes, out string value)
        {
            value = string.Empty;

            try
            {
                reader.ReadValueSafe(out int length);

                if (length < 0 || length > maxBytes)
                {
                    return false;
                }

                if (length == 0)
                {
                    return true;
                }

                byte[] bytes = new byte[length];
                reader.ReadBytesSafe(ref bytes, length);
                value = Encoding.UTF8.GetString(bytes);
                return true;
            }
            catch
            {
                value = string.Empty;
                return false;
            }
        }

        // --- ReceiveMapSelect ---
        private void ReceiveMapSelect(ulong senderClientId, FastBufferReader reader)
        {
            // 仅服务器可发送地图选择
            if (!IsServerSender(senderClientId)) return;

            reader.ReadValueSafe(out int mapTypeInt);
            if (!IsDefinedMapType(mapTypeInt)) return;

            var type = (OnlineMapService.OnlineMapType)mapTypeInt;
            mapService.ActiveMapType = type;
            Debug.Log($"[D5] Client received map select: {type}");
        }

        // --- GetLocalDisplayName ---
        private string GetLocalDisplayName()
        {
            ulong clientId = LocalClientId();

            if (players.TryGetValue(clientId, out OnlinePlayerState state))
            {
                return state.DisplayName;
            }

            return localPlayerName;
        }

        // --- UpsertLocalPlayer ---
        private void UpsertLocalPlayer()
        {
            if (localPreviewMode)
            {
                StartLocalPreviewRoom();
                return;
            }

            if (networkManager == null || !networkManager.IsClient)
            {
                return;
            }

            ulong clientId = LocalClientId();

            if (players.TryGetValue(clientId, out OnlinePlayerState existing))
            {
                existing.Position = localPosition;
                existing.Ready = localReady;
                existing.IsBot = false;
                existing.DisplayName = OnlineMatchUtils.LimitText(localPlayerName, 16, "港区玩家");
                players[clientId] = existing;
            }
            else
            {
                players[clientId] = new OnlinePlayerState(clientId, OnlineMatchUtils.LimitText(localPlayerName, 16, "港区玩家"), localPosition, localReady, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }
        }
    }
}
