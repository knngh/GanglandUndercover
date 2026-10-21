using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using GanglandUndercover;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 连接生命周期 partial（OMC 拆分第三刀）：客户端连入/断开、断连宽限与重连认领、
    /// 行动代号档案收发、Host 丢失标记。纯从 Network.cs / Controller.cs 搬移，零行为变更。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        private const int ClientProfileMaxNameBytes = 128;

        private const string ClientProfileMessage = "GanglandClientProfile";

        // --- HandleClientConnected ---
        private void HandleClientConnected(ulong clientId)
        {
            disconnectedNetworkSession = false;

            if (networkManager.IsServer)
            {
                EnsureMiniGameBridgeNetworkObject();
                EnsureCharacterCustomizerNetworkObject(clientId);

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

            // 对局中人掉线进入重连宽限期：保留身份/角色/冷却，到期未回归才正式拆除。
            // （断连回调只在 host/单机侧触发——client 侧 host 丢失已被 IsRemoteHostDisconnect 拦截）
            bool midMatch = matchStarted && phase != OnlineMatchPhase.Lobby;
            bool isBot = players.TryGetValue(clientId, out OnlinePlayerState disconnectedState) && disconnectedState.IsBot;
            if (midMatch && !isBot)
            {
                DeferDisconnectedClientTeardown(clientId);
                return;
            }

            TeardownDisconnectedClient(clientId);
        }

        // --- DeferDisconnectedClientTeardown ---
        // 宽限期内保留 players 条目（输入停更看门狗会冻结其位置），仅剥离其进行中贡献：
        // 释放手持任务/维修、剔除已投票——防会议卡死与挂机推进。身份映射到正式拆除时再清。
        private void DeferDisconnectedClientTeardown(ulong clientId)
        {
            pendingDisconnectTeardowns[clientId] = ClientReconnectGraceSeconds;
            ReleaseTasksHeldByClient(clientId);
            RemoveDisconnectedPlayerVotes(clientId);

            string displayName = players.TryGetValue(clientId, out OnlinePlayerState state)
                ? state.DisplayName
                : "玩家" + clientId;
            AddCaseLog(displayName + " 已断线，" + Mathf.RoundToInt(ClientReconnectGraceSeconds) + " 秒内用相同行动代号可重连回归。");
            BroadcastSnapshot();
        }

        // --- TeardownDisconnectedClient ---
        private void TeardownDisconnectedClient(ulong clientId)
        {
            pendingDisconnectTeardowns.Remove(clientId);
            ReleaseTasksHeldByClient(clientId);
            RemoveDisconnectedPlayerVotes(clientId);

            players.Remove(clientId);
            RemoveCharacterCustomizerNetworkObject(clientId);
            privateRoles.Remove(clientId);
            killSystem.killCooldowns.Remove(clientId);
            abilityCooldowns.Remove(clientId);
            serverChatLastSendTimes.Remove(clientId);
            clientInputStaleTimers.Remove(clientId);
            _botController?.RemoveBot(clientId);

            if (networkManager != null && networkManager.IsServer)
            {
                AddCaseLog("玩家" + clientId + " 已离开房间。");

                int aliveCount = CountAlivePlayers();
                if ((phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting)
                    && aliveCount > 0
                    && votingService != null && votingService.AllVoted)
                {
                    ResolveVotes();
                    return;
                }

                EvaluateWinConditions();
                BroadcastSnapshot();
            }
        }

        // --- TickReconnectGrace ---
        // 宽限期倒计时；到期未回归者正式拆除。dt 累加制（同输入看门狗），EditMode 可直调测试。
        private void TickReconnectGrace(float deltaTime)
        {
            if (pendingDisconnectTeardowns.Count == 0)
            {
                return;
            }

            List<ulong> pendingIds = new List<ulong>(pendingDisconnectTeardowns.Keys);
            foreach (ulong clientId in pendingIds)
            {
                float remaining = pendingDisconnectTeardowns[clientId] - deltaTime;
                if (remaining > 0f)
                {
                    pendingDisconnectTeardowns[clientId] = remaining;
                    continue;
                }

                Debug.Log("[Reconnect] 宽限期到期，正式移除客户端 " + clientId);
                TeardownDisconnectedClient(clientId);
            }
        }

        // --- TryReclaimReconnectingClient ---
        // 断连宽限期内，同行动代号的新连接可认领旧身份槽位（角色/存活/冷却/任务分配整体迁移）。
        // 局限（v1）：以 DisplayName 精确匹配为凭证——代号需唯一；正式身份令牌见 Alpha 队列。
        private bool TryReclaimReconnectingClient(ulong newClientId, string displayName)
        {
            if (!matchStarted || pendingDisconnectTeardowns.Count == 0)
            {
                return false;
            }

            ulong oldClientId = ulong.MaxValue;
            foreach (KeyValuePair<ulong, float> pair in pendingDisconnectTeardowns)
            {
                if (players.TryGetValue(pair.Key, out OnlinePlayerState pendingState)
                    && string.Equals(pendingState.DisplayName, displayName, System.StringComparison.Ordinal))
                {
                    oldClientId = pair.Key;
                    break;
                }
            }

            if (oldClientId == ulong.MaxValue || !players.TryGetValue(oldClientId, out OnlinePlayerState state))
            {
                return false;
            }

            pendingDisconnectTeardowns.Remove(oldClientId);
            state.ClientId = newClientId;
            players.Remove(oldClientId);
            players[newClientId] = state;

            if (privateRoles.TryGetValue(oldClientId, out OnlineRole role))
            {
                privateRoles.Remove(oldClientId);
                privateRoles[newClientId] = role;
            }

            RemapCooldownKey(killSystem?.killCooldowns, oldClientId, newClientId);
            RemapCooldownKey(abilityCooldowns, oldClientId, newClientId);
            RemapCooldownKey(serverChatLastSendTimes, oldClientId, newClientId);
            RemapCooldownKey(clientInputStaleTimers, oldClientId, newClientId);
            RemapTaskAssignments(oldClientId, newClientId);

            // 关键：认领后必须给新 clientId 补发身份指派——否则回归端本地
            // localRole 停留在 Unassigned，HUD/身份简报没有自己的身份。
            if (privateRoles.TryGetValue(newClientId, out OnlineRole reclaimedRole))
            {
                SendRole(newClientId, reclaimedRole);
            }

            AddCaseLog(state.DisplayName + " 已重连回归。");
            status = state.DisplayName + " 已重连回归。";
            return true;
        }

        // --- RemapCooldownKey ---
        private static void RemapCooldownKey(Dictionary<ulong, float> table, ulong oldClientId, ulong newClientId)
        {
            if (table == null || !table.TryGetValue(oldClientId, out float value))
            {
                return;
            }

            table.Remove(oldClientId);
            table[newClientId] = value;
        }

        // --- RemapTaskAssignments ---
        // 任务分配/进度整表导出→换 clientId→重载（走快照同款通道，保进度与完成态）。
        private void RemapTaskAssignments(ulong oldClientId, ulong newClientId)
        {
            List<GameStateSnapshot.SnapshotTaskAssignmentEntry> entries = TaskSyncAssignmentsSnapshot();
            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ClientId == oldClientId)
                {
                    GameStateSnapshot.SnapshotTaskAssignmentEntry entry = entries[i];
                    entry.ClientId = newClientId;
                    entries[i] = entry;
                    changed = true;
                }
            }

            if (changed)
            {
                LoadTaskSyncAssignments(entries);
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

            FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
            try
            {
                WriteClientProfilePayload(ref writer, safeName);
                networkManager.CustomMessagingManager.SendNamedMessage(ClientProfileMessage, NetworkManager.ServerClientId, writer);
            }
            finally
            {
                writer.Dispose();
            }
        }


        // --- ReceiveClientProfile ---
        private void ReceiveClientProfile(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (!TryReadClientProfilePayload(ref reader, out string displayName))
            {
                return;
            }

            ApplyClientProfile(senderClientId, displayName);
        }


        // --- ApplyClientProfile ---
        private void ApplyClientProfile(ulong senderClientId, string displayName)
        {
            string safeName = OnlineMatchUtils.LimitText(displayName, 16, "港区玩家");

            // 对局中掉线玩家的同代号重连：优先认领旧身份槽位。
            if (TryReclaimReconnectingClient(senderClientId, safeName))
            {
                BroadcastSnapshot();
                return;
            }

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


        // --- WriteClientProfilePayload ---
        private static void WriteClientProfilePayload(ref FastBufferWriter writer, string displayName)
        {
            WriteBoundedUtf8String(ref writer, OnlineMatchUtils.LimitText(displayName, 16, "港区玩家"));
        }

        // --- TryReadClientProfilePayload ---
        private static bool TryReadClientProfilePayload(ref FastBufferReader reader, out string displayName)
        {
            displayName = string.Empty;
            if (!TryReadBoundedUtf8String(ref reader, ClientProfileMaxNameBytes, out string rawName))
            {
                return false;
            }

            displayName = OnlineMatchUtils.LimitText(rawName, 16, "港区玩家");
            return !string.IsNullOrWhiteSpace(displayName);
        }


    }
}
