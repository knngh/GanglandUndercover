using System;
using Unity.Netcode;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 能力反馈联网 partial（OMC 拆分第二刀）：服务端把一次能力反馈广播给所有
    /// 非 Host 客户端（GanglandAbilityFeedback），客户端接收后走本地视觉管线。
    /// 纯从 Network.cs / Controller.cs 搬移，零行为变更。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        private const string AbilityFeedbackMessage = "GanglandAbilityFeedback"; // P0-3

        // --- P0-3: 能力反馈联网（GanglandAbilityFeedback） ---

        private const int AbilityFeedbackMaxKeyBytes = 64;

        /// <summary>服务端：把一次能力反馈广播给所有非 Host 客户端（Host 本地视觉已在 Emit 内生成）。</summary>
        private void BroadcastAbilityFeedback(ulong clientId, string abilityKey, Color color, Vector3 position)
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsServer)
            {
                return;
            }

            FastBufferWriter writer = new FastBufferWriter(96, Unity.Collections.Allocator.Temp);
            try
            {
                writer.WriteValueSafe(clientId);
                WriteBoundedUtf8String(ref writer, abilityKey ?? string.Empty);
                writer.WriteValueSafe(position);
                writer.WriteValueSafe(color);

                foreach (var kv in networkManager.ConnectedClients)
                {
                    if (kv.Key == NetworkManager.ServerClientId)
                    {
                        continue;
                    }

                    networkManager.CustomMessagingManager.SendNamedMessage(
                        AbilityFeedbackMessage, kv.Key, writer, NetworkDelivery.ReliableSequenced);
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void ReceiveAbilityFeedback(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsClient || !IsServerSender(senderClientId))
            {
                return;
            }

            ulong abilityUserId;
            string abilityKey;
            Vector3 position;
            Color color;
            reader.ReadValueSafe(out abilityUserId);
            if (!TryReadBoundedUtf8String(ref reader, AbilityFeedbackMaxKeyBytes, out abilityKey))
            {
                return;
            }

            reader.ReadValueSafe(out position);
            reader.ReadValueSafe(out color);

            ApplyRemoteAbilityFeedback(abilityKey, position, color);
        }

        /// <summary>客户端：远端能力反馈落地为本地 VFX + 音效（Host 已在 Emit 内生成，服务端短路避免双份）。</summary>
        private void ApplyRemoteAbilityFeedback(string abilityKey, Vector3 position, Color color)
        {
            if (networkManager != null && networkManager.IsServer)
            {
                return;
            }

            if (worldRoot == null)
                EnsureWorld();

            if (worldRoot != null && worldBuilder != null)
                worldBuilder.CreateAbilityFeedbackVisual(abilityKey, position, color);

            PlayCue("ability");
        }

    }
}
