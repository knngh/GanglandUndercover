using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Netcode;

namespace GanglandUndercover.Online.Surveillance
{
    /// <summary>
    /// M5.4 联机监控系统。
    /// 服务器权威：只下发摄像头可视区域内的玩家位置。
    /// 客户端渲染 2D 标记图（不暴露全图信息）。
    /// </summary>
    public class OnlineSecurityCamera : NetworkBehaviour
    {
        [Header("Camera Zone (world-space)")]
        public NetworkVariable<Vector2> ZoneCenterNet = new NetworkVariable<Vector2>();
        public NetworkVariable<Vector2> ZoneSizeNet = new NetworkVariable<Vector2>(new Vector2(6f, 4f));
        public NetworkVariable<FixedString32Bytes> CameraLabelNet = new NetworkVariable<FixedString32Bytes>("监控摄像头");

        /// <summary>世界坐标快捷访问（NetworkVariable 读写代理）</summary>
        public Vector2 ZoneCenter
        {
            get => ZoneCenterNet.Value;
            set { if (IsServer) ZoneCenterNet.Value = value; }
        }
        public Vector2 ZoneSize
        {
            get => ZoneSizeNet.Value;
            set { if (IsServer) ZoneSizeNet.Value = value; }
        }
        public string CameraLabel
        {
            get => CameraLabelNet.Value.ToString();
            set { if (IsServer) CameraLabelNet.Value = new FixedString32Bytes(value ?? "监控摄像头"); }
        }

        /// <summary>
        /// 哪些玩家当前在此摄像头的可视区域内（仅服务器维护）。
        /// </summary>
        private HashSet<ulong> _visiblePlayers = new HashSet<ulong>();

        /// <summary>
        /// 哪些玩家正在观看此摄像头（服务器维护）。
        /// </summary>
        private HashSet<ulong> _watchingPlayers = new HashSet<ulong>();

        private OnlineMatchController _controller;

        public void BindController(OnlineMatchController controller)
        {
            _controller = controller;
        }

        public void ServerTick(IReadOnlyDictionary<ulong, OnlinePlayerState> players)
        {
            if (!IsServer) return;

            _visiblePlayers.Clear();

            foreach (var kv in players)
            {
                if (!kv.Value.Alive) continue;
                Vector2 pos = new Vector2(kv.Value.Position.x, kv.Value.Position.y);
                if (IsInZone(pos))
                {
                    _visiblePlayers.Add(kv.Key);
                }
            }

            // 推送给正在观看的客户端
            foreach (ulong watcherId in _watchingPlayers)
            {
                UpdateWatcherClientRpc(
                    SerializeVisiblePlayers(players),
                    SingleTarget(watcherId));
            }
        }

        /// <summary>
        /// 客户端请求开始观看。
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void StartWatchingServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong watcherId = rpcParams.Receive.SenderClientId;
            _watchingPlayers.Add(watcherId);
        }

        /// <summary>
        /// 客户端请求停止观看。
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void StopWatchingServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong watcherId = rpcParams.Receive.SenderClientId;
            _watchingPlayers.Remove(watcherId);
        }

        [ClientRpc]
        private void UpdateWatcherClientRpc(VisiblePlayerData[] data, ClientRpcParams _ = default)
        {
            OnCameraDataReceived?.Invoke(data);
        }

        /// <summary>
        /// 客户端事件：收到摄像头数据。
        /// </summary>
        public System.Action<VisiblePlayerData[]> OnCameraDataReceived;

        // ── Helpers ──

        private bool IsInZone(Vector2 worldPos)
        {
            Vector2 min = ZoneCenter - ZoneSize * 0.5f;
            Vector2 max = ZoneCenter + ZoneSize * 0.5f;
            return worldPos.x >= min.x && worldPos.x <= max.x &&
                   worldPos.y >= min.y && worldPos.y <= max.y;
        }

        private VisiblePlayerData[] SerializeVisiblePlayers(IReadOnlyDictionary<ulong, OnlinePlayerState> players)
        {
            List<VisiblePlayerData> list = new List<VisiblePlayerData>();
            foreach (ulong id in _visiblePlayers)
            {
                if (players.TryGetValue(id, out OnlinePlayerState state))
                {
                    // 只下发位置和公共身份，不下发私密身份
                    bool isGang = _controller != null && _controller.IsGangFaction(id);
                    list.Add(new VisiblePlayerData
                    {
                        ClientId = (int)id,
                        PositionX = state.Position.x,
                        PositionY = state.Position.y,
                        IsSuspicious = isGang // 黑帮亮红灯
                    });
                }
            }
            return list.ToArray();
        }

        private static ClientRpcParams SingleTarget(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
        }
    }

    /// <summary>
    /// 摄像头下发数据结构（轻量，不含身份）。
    /// </summary>
    [System.Serializable]
    public struct VisiblePlayerData : INetworkSerializable
    {
        public int ClientId;
        public float PositionX;
        public float PositionY;
        public bool IsSuspicious; // 黑帮亮红灯

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PositionX);
            serializer.SerializeValue(ref PositionY);
            serializer.SerializeValue(ref IsSuspicious);
        }
    }
}
