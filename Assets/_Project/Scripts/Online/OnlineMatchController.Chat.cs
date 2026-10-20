using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using GanglandUndercover;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 聊天职责 partial（OMC 拆分第一刀）：频道判定、发送/广播收发、载荷编解码、
    /// 服务端冷却门。纯从 Network.cs / Controller.cs 搬移，零行为变更。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        private const int ChatMaxContentBytes = 4096;
        private const int ChatMaxNameBytes = 256;
        private const int ChatMaxIdBytes = 64;
        private const int ChatWriterCapacityBytes = ChatMaxContentBytes + 1024;
        private const float ServerChatSendCooldownSeconds = 5f;

        private ChatSystem chatSystem;
        private readonly Dictionary<ulong, float> serverChatLastSendTimes = new Dictionary<ulong, float>();

        private const string ChatSendMessage = "GanglandChatSend";
        private const string ChatBroadcastMessage = "GanglandChatBroadcast";

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
            bool isDead = !senderState.Alive;
            OnlineRole presentedRole = OnlineMatchUtils.ChatPresentationRole(
                role, senderState.PublicRole, senderState.Alive, phase);
            Faction faction = ChatSystem.RoleToFaction(presentedRole);
            string senderName = senderState.DisplayName;

            // 本地显示（服务器也显示）。
            // Host 即发送者时已在 SendChatMessage 本地显示过——跳过避免双显。
            if (senderClientId != LocalClientId())
            {
                chatSystem.ReceiveMessage(senderClientId.ToString(), senderName, content, isDead, faction, channel);
            }

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
                    // 会议频道：全员可见（死亡玩家旁观——鬼魂不能发言但能看讨论，
                    // 修复"死亡玩家会议零信息"；发言侧 CanSendNow/频道判定已拦截）
                    foreach (KeyValuePair<ulong, OnlinePlayerState> kv in players)
                    {
                        if (kv.Key == senderClientId) continue;
                        networkManager.CustomMessagingManager.SendNamedMessage(ChatBroadcastMessage, kv.Key, writer, Unity.Netcode.NetworkDelivery.ReliableSequenced);
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

    }
}
