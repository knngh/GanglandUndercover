using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 主机迁移管理器：检测主机断开连接，从剩余客户端中选举新主机，
    /// 重建游戏状态并恢复对局。作为 OnlineMatchController 的辅助组件运行。
    ///
    /// 协议：
    ///   - HOST_HEARTBEAT：主机每 2 秒向所有客户端发送心跳。
    ///   - HOST_MIGRATION：新主机选举完成后，广播快照通知所有客户端恢复。
    ///   - 心跳超时 5 秒未收到 → 判定主机离线 → 启动迁移。
    ///   - 只剩 1 名玩家时迁移失败 → 游戏结束。
    /// </summary>
    [RequireComponent(typeof(OnlineMatchController))]
    public sealed class HostMigrationManager : MonoBehaviour
    {
        private const string HostHeartbeatMessage = "GanglandHostHeartbeat";
        private const string HostMigrationMessage = "GanglandHostMigration";
        private const float HeartbeatInterval = 2.0f;
        private const float HeartbeatTimeout = 5.0f;

        [Header("M7.2 迁移配置")]
        [SerializeField] private bool useFallbackOnMigrationFail = true;  // 默认降级：Host掉线→友好结算
        [SerializeField] private float migrationTimeout = 30f;            // 迁移总超时

        private OnlineMatchController matchController;
        private NetworkManager networkManager;

        // 心跳计时
        private float heartbeatSendTimer;
        private float lastHeartbeatReceiveTime;
        private bool migrationInProgress;
        private bool hostDisconnectedDetected;
        private float migrationElapsed;       // M7.2: 迁移已耗时

        // 迁移 UI 提示
        private string migrationStatus = string.Empty;
        private float migrationMessageAlpha;

        // 旧主机断连前最后的快照（客户端缓存，用于提交给新主机）
        private GameStateSnapshot? cachedSnapshot;

        // M7.2: 追踪旧主机 ClientId
        private ulong oldHostClientId;

        public bool MigrationInProgress => migrationInProgress;
        public string MigrationStatus => migrationStatus;

        // ── Unity Lifecycle ──

        private void Awake()
        {
            matchController = GetComponent<OnlineMatchController>();
            if (matchController == null)
            {
                Debug.LogError("[HostMigrationManager] 未找到 OnlineMatchController，主机迁移不可用。");
                enabled = false;
            }
        }

        private void Start()
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogWarning("[HostMigrationManager] 未找到 NetworkManager。");
            }

            // 通过反射或公开 API 获取 NetworkManager — 使用 NetworkManager.Singleton
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }
        }

        private void Update()
        {
            if (matchController == null || !matchController.enabled || !matchController.MatchStarted)
            {
                return;
            }

            if (!matchController.IsOnline)
            {
                return;
            }

            // 迁移进行中不执行常规心跳
            if (migrationInProgress)
            {
                TickMigrationMessage();
                return;
            }

            if (matchController.IsHost)
            {
                TickHostHeartbeat();
            }
            else
            {
                TickClientHeartbeatWatchdog();
            }
        }

        // ── 主机心跳发送 ──

        private void TickHostHeartbeat()
        {
            heartbeatSendTimer += Time.deltaTime;

            if (heartbeatSendTimer >= HeartbeatInterval)
            {
                heartbeatSendTimer = 0f;
                SendHeartbeat();
            }
        }

        private void SendHeartbeat()
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            if (!networkManager.IsHost && !networkManager.IsServer)
            {
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(Time.realtimeSinceStartup); // 时间戳仅用于日志调试

            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                HostHeartbeatMessage, writer, NetworkDelivery.Unreliable);
        }

        // ── 客户端心跳看门狗 ──

        private void TickClientHeartbeatWatchdog()
        {
            if (hostDisconnectedDetected)
            {
                return;
            }

            float timeSinceHeartbeat = Time.realtimeSinceStartup - lastHeartbeatReceiveTime;

            // 初始化：若从未收到心跳则宽容初始等待
            if (lastHeartbeatReceiveTime <= 0f)
            {
                return;
            }

            if (timeSinceHeartbeat > HeartbeatTimeout)
            {
                hostDisconnectedDetected = true;
                Debug.LogWarning("[HostMigrationManager] 主机心跳超时 " + timeSinceHeartbeat.ToString("F1") + "s，启动迁移流程。");
                BeginMigration();
            }
        }

        /// <summary>
        /// 收到主机心跳时调用（由 OnlineMatchController 消息处理器转发）。
        /// </summary>
        public void OnHeartbeatReceived()
        {
            lastHeartbeatReceiveTime = Time.realtimeSinceStartup;

            // 如果之前检测到断连但迁移尚未完成，重置检测标志
            if (hostDisconnectedDetected && !migrationInProgress)
            {
                hostDisconnectedDetected = false;
                Debug.Log("[HostMigrationManager] 主机心跳恢复，取消迁移。");
            }
        }

        // ── 迁移流程 ──

        private void BeginMigration()
        {
            if (migrationInProgress)
            {
                return;
            }

            migrationInProgress = true;
            migrationElapsed = 0f;
            migrationStatus = "主机迁移中...";
            migrationMessageAlpha = 1.0f;

            // M7.2: 记录旧主机 ID（用于选举时排除）
            oldHostClientId = matchController.OldHostClientId();

            // 阶段一：缓存当前快照
            cachedSnapshot = matchController.CaptureSnapshot();

            // M7.2 降级策略：少于 2 人直接结算
            if (GetRemainingPlayerCount() <= 1)
            {
                Debug.LogWarning("[HostMigrationManager] 只剩 1 名玩家，迁移失败。");
                FallbackToGameOver("主机已离线，剩余玩家不足。");
                return;
            }

            // 阶段二：从剩余客户端选举新主机
            bool hasNewHost = TryElectNewHost(out ulong newHostId);
            ulong localClientId = matchController.LocalClientIdValue;

            if (hasNewHost && newHostId == localClientId)
            {
                // 本机成为新主机
                BecomeNewHost();
            }
            else if (hasNewHost)
            {
                // 等待新主机广播快照
                Debug.Log("[HostMigrationManager] 新主机选举为 " + newHostId + "，等待快照同步。");
            }
            else
            {
                FallbackToGameOver("无法选举新主机。");
            }
        }

        private bool TryElectNewHost(out ulong newHostId)
        {
            newHostId = 0UL;
            if (networkManager == null)
            {
                return false;
            }

            List<ulong> connectedClientIds = new List<ulong>();
            foreach (NetworkClient client in networkManager.ConnectedClientsList)
            {
                connectedClientIds.Add(client.ClientId);
            }

            return TryElectNewHostId(connectedClientIds, oldHostClientId, out newHostId);
        }

        internal static ulong ElectNewHostId(IEnumerable<ulong> connectedClientIds, ulong oldHostClientId)
        {
            return TryElectNewHostId(connectedClientIds, oldHostClientId, out ulong newHostId)
                ? newHostId
                : 0UL;
        }

        internal static bool TryElectNewHostId(IEnumerable<ulong> connectedClientIds, ulong oldHostClientId, out ulong newHostId)
        {
            newHostId = 0UL;
            if (connectedClientIds == null)
            {
                return false;
            }

            bool found = false;
            ulong bestId = 0UL;
            foreach (ulong clientId in connectedClientIds)
            {
                if (clientId == oldHostClientId)
                {
                    continue;
                }

                if (!found || clientId < bestId)
                {
                    bestId = clientId;
                    found = true;
                }
            }

            newHostId = bestId;
            return found;
        }

        private async void BecomeNewHost()
        {
            Debug.Log("[HostMigrationManager] 本机成为新主机，从快照重建游戏。");

            string reason = string.Empty;
            if (matchController.ShouldUseRelayReplacementHostForMigration())
            {
                reason = await matchController.TryStartReplacementRelayHostForMigrationAsync();
            }
            else if (!matchController.TryStartReplacementHostForMigration(out reason))
            {
                // reason 已由 TryStartReplacementHostForMigration 写入。
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                FallbackToGameOver("主机迁移失败：" + reason);
                return;
            }

            // 重建游戏状态
            matchController.RestoreFromSnapshot(cachedSnapshot ?? GameStateSnapshot.FromDefault());

            // 广播迁移通知给所有剩余客户端
            BroadcastMigrationSnapshot();

            migrationStatus = string.Empty;
            migrationInProgress = false;
            hostDisconnectedDetected = false;
            heartbeatSendTimer = 0f;

            Debug.Log("[HostMigrationManager] 主机迁移完成，游戏已恢复。");
        }

        private void BroadcastMigrationSnapshot()
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            var snapshot = matchController.CaptureSnapshot();
            using FastBufferWriter writer = new FastBufferWriter(16384, Allocator.Temp);
            writer.WriteValueSafe(networkManager.LocalClientId); // 新主机 ID
            snapshot.ToBytes(writer);

            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                HostMigrationMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// 收到迁移快照时调用（由 OnlineMatchController 消息处理器转发）。
        /// </summary>
        public void OnMigrationSnapshotReceived(FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong newHostId);
            GameStateSnapshot snapshot = GameStateSnapshot.FromBytes(reader);

            Debug.Log("[HostMigrationManager] 收到迁移快照，新主机=" + newHostId + "，正在恢复状态。");

            matchController.RestoreFromSnapshot(snapshot);

            migrationStatus = string.Empty;
            migrationInProgress = false;
            hostDisconnectedDetected = false;
            lastHeartbeatReceiveTime = Time.realtimeSinceStartup;

            Debug.Log("[HostMigrationManager] 迁移完成，游戏已恢复。");
        }

        // ── 辅助 ──

        private int GetRemainingPlayerCount()
        {
            if (networkManager == null)
            {
                return 1;
            }

            return networkManager.ConnectedClientsList.Count;
        }

        private void TickMigrationMessage()
        {
            if (string.IsNullOrEmpty(migrationStatus))
            {
                return;
            }

            migrationMessageAlpha = Mathf.Lerp(migrationMessageAlpha, 1.0f, Time.deltaTime * 2.0f);

            // M7.2: 迁移超时降级
            migrationElapsed += Time.deltaTime;
            if (migrationElapsed >= migrationTimeout)
            {
                FallbackToGameOver("主机迁移超时（" + migrationTimeout.ToString("F0") + "s），自动结算。");
            }
        }

        /// <summary>M7.2 降级策略：Host 掉线 → 友好结算</summary>
        private void FallbackToGameOver(string reason)
        {
            migrationStatus = reason;
            migrationMessageAlpha = 1.0f;
            migrationInProgress = false;
            hostDisconnectedDetected = false;

            if (!useFallbackOnMigrationFail)
            {
                Debug.LogWarning("[HostMigrationManager] 迁移失败但已关闭降级结算: " + reason);
                return;
            }

            if (matchController != null)
            {
                matchController.ForceGameOver(reason);
            }

            Debug.Log("[HostMigrationManager] 降级结算: " + reason);
        }

        private void OnGUI()
        {
            if (!migrationInProgress || string.IsNullOrEmpty(migrationStatus))
            {
                return;
            }

            // 半透明全屏遮罩
            Color overlayColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.color = overlayColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 居中提示文字
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
            };
            labelStyle.normal.textColor = new Color(1f, 1f, 1f, migrationMessageAlpha);

            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60f), migrationStatus, labelStyle);

            // 旋转加载指示器
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
            };
            subStyle.normal.textColor = new Color(1f, 1f, 1f, migrationMessageAlpha * 0.7f);
            GUI.Label(new Rect(0, Screen.height * 0.4f + 70f, Screen.width, 30f), "正在选举新主机并同步对局状态，请稍候...", subStyle);
        }

        // ── 公开 API：注册消息处理器 ──

        /// <summary>
        /// 注册迁移相关命名消息处理器。由 OnlineMatchController 在
        /// StartOnlineMatchCore 中调用。
        /// </summary>
        public void RegisterMessageHandlers(NetworkManager netManager)
        {
            if (netManager == null || netManager.CustomMessagingManager == null)
            {
                return;
            }

            networkManager = netManager;
            netManager.CustomMessagingManager.RegisterNamedMessageHandler(
                HostHeartbeatMessage, HandleHeartbeat);
            netManager.CustomMessagingManager.RegisterNamedMessageHandler(
                HostMigrationMessage, HandleMigrationSnapshot);
        }

        /// <summary>
        /// 反注册迁移相关消息处理器。由 OnlineMatchController 在
        /// Shutdown / ReturnToLobby 中调用。
        /// </summary>
        public void UnregisterMessageHandlers(NetworkManager netManager)
        {
            if (netManager == null || netManager.CustomMessagingManager == null)
            {
                return;
            }

            netManager.CustomMessagingManager.UnregisterNamedMessageHandler(HostHeartbeatMessage);
            netManager.CustomMessagingManager.UnregisterNamedMessageHandler(HostMigrationMessage);
        }

        // ── 消息处理器 ──

        private void HandleHeartbeat(ulong senderClientId, FastBufferReader reader)
        {
            // 心跳只能由当前 NGO Server 发出；主机本机忽略自己的广播。
            if (!IsTrustedHostMessageSender(
                    senderClientId,
                    NetworkManager.ServerClientId,
                    matchController != null && matchController.IsHost))
            {
                return;
            }

            OnHeartbeatReceived();
        }

        private void HandleMigrationSnapshot(ulong senderClientId, FastBufferReader reader)
        {
            // 迁移快照也必须来自当前 Server，避免普通客户端伪造状态恢复。
            if (!IsTrustedHostMessageSender(
                    senderClientId,
                    NetworkManager.ServerClientId,
                    matchController != null && matchController.IsHost))
            {
                return;
            }

            OnMigrationSnapshotReceived(reader);
        }

        internal static bool IsTrustedHostMessageSender(
            ulong senderClientId,
            ulong serverClientId,
            bool receiverIsHost)
        {
            return !receiverIsHost && senderClientId == serverClientId;
        }

        /// <summary>
        /// 网络回调：客户端断连时触发（Unity Netcode OnClientDisconnectCallback）。
        /// 由 OnlineMatchController 转发，用于区分主机断连场景。
        /// </summary>
        public void OnClientDisconnected(ulong clientId)
        {
            if (migrationInProgress)
            {
                return;
            }

            if (networkManager == null)
            {
                return;
            }

            // 检查断开的是否为主机
            // 主机 clientId 始终为 NetworkManager.ServerClientId (0)
            if (clientId == NetworkManager.ServerClientId && !matchController.IsHost)
            {
                Debug.Log("[HostMigrationManager] 检测到主机 " + clientId + " 断开连接，启动迁移。");
                hostDisconnectedDetected = true;
                BeginMigration();
            }
        }

        /// <summary>
        /// 重置迁移管理器状态（新对局开始时调用）。
        /// </summary>
        public void ResetState()
        {
            heartbeatSendTimer = 0f;
            lastHeartbeatReceiveTime = 0f;
            migrationInProgress = false;
            hostDisconnectedDetected = false;
            cachedSnapshot = null;
            migrationStatus = string.Empty;
            migrationMessageAlpha = 0f;
        }
    }
}
