using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using GanglandUndercover.SocialDeduction.MiniGames;

namespace GanglandUndercover.Online.MiniGames
{
    /// <summary>
    /// M5.1 小游戏联机协议桥。
    ///
    /// 协议流程（服务器驱动）：
    ///   1. Controller → OpenMinigameOnClient(clientId, taskId)  服务器请求客户端打开小游戏
    ///   2. Bridge → StartTaskClientRpc(taskId) → 客户端打开小游戏
    ///   3. 客户端完成小游戏 → SubmitTaskResultServerRpc(taskId, success)
    ///   4. 服务器校验 → 调用 controller.ValidateAndCompleteTask / ReleaseTask
    ///   5. 广播 TaskDoneClientRpc
    /// </summary>
    public class OnlineMiniGameBridge : NetworkBehaviour
    {
        private OnlineMatchController _controller;
        private GameObject _currentMinigame;
        private int _currentTaskId = -1;

        public void BindController(OnlineMatchController controller)
        {
            _controller = controller;
        }

        /// <summary>
        /// 服务器端：请求指定客户端打开小游戏。
        /// </summary>
        public void OpenMinigameOnClient(ulong clientId, int taskId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MiniGameBridge] OpenMinigameOnClient must be called on server");
                return;
            }

            if (_controller == null) return;

            if (!_controller.ValidateTaskStart(clientId, taskId, out string rejectReason))
            {
                RejectStartClientRpc(taskId, rejectReason, SingleTarget(clientId));
                return;
            }

            _controller.MarkTaskActive(clientId, taskId);
            StartTaskClientRpc(taskId, SingleTarget(clientId));
        }

        /// <summary>
        /// 客户端调用：提交任务完成结果。
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitTaskResultServerRpc(int taskId, bool success, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (_controller == null) return;

            if (!success)
            {
                _controller.ReleaseTask(senderId, taskId);
                CancelTaskClientRpc(taskId, SingleTarget(senderId));
                return;
            }

            if (!_controller.ValidateAndCompleteTask(senderId, taskId, out string error))
            {
                RejectResultClientRpc(taskId, error, SingleTarget(senderId));
                return;
            }

            TaskDoneClientRpc(taskId, (int)senderId);
        }

        // ── ClientRpc ──

        [ClientRpc]
        private void StartTaskClientRpc(int taskId, ClientRpcParams _ = default)
        {
            OpenMinigame(taskId);
        }

        [ClientRpc]
        private void RejectStartClientRpc(int taskId, string reason, ClientRpcParams _ = default)
        {
            Debug.Log($"[MiniGameBridge] 任务 {taskId} 被拒绝: {reason}");
        }

        [ClientRpc]
        private void RejectResultClientRpc(int taskId, string error, ClientRpcParams _ = default)
        {
            Debug.Log($"[MiniGameBridge] 任务 {taskId} 结果被拒绝: {error}");
        }

        [ClientRpc]
        private void CancelTaskClientRpc(int taskId, ClientRpcParams _ = default)
        {
            Debug.Log($"[MiniGameBridge] 任务 {taskId} 已取消");
            if (_currentTaskId == taskId) CloseMinigame();
        }

        [ClientRpc]
        private void TaskDoneClientRpc(int taskId, int clientId, ClientRpcParams _ = default)
        {
            Debug.Log($"[MiniGameBridge] 任务 {taskId} 由玩家 {clientId} 完成");
            if (_currentTaskId == taskId) CloseMinigame();
        }

        /// <summary>
        /// M5.3 服务器端：请求指定客户端打开修复小游戏。
        /// </summary>
        public void OpenRepairMinigameOnClient(ulong clientId, int taskId)
        {
            if (!IsServer) return;
            if (_controller == null) return;

            if (!_controller.ValidateRepairStart(clientId, taskId, out string rejectReason))
            {
                RejectStartClientRpc(taskId, rejectReason, SingleTarget(clientId));
                return;
            }

            _controller.MarkTaskActive(clientId, taskId);
            _pendingRepair[clientId] = taskId;
            StartRepairClientRpc(taskId, SingleTarget(clientId));
        }

        /// <summary>
        /// M5.3 客户端调用：提交修复结果。
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitRepairResultServerRpc(int taskId, bool success, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (_controller == null) return;
            _pendingRepair.Remove(senderId);

            if (!success)
            {
                _controller.ReleaseTask(senderId, taskId);
                CancelTaskClientRpc(taskId, SingleTarget(senderId));
                return;
            }

            if (!_controller.ValidateAndRepairTask(senderId, taskId, out string error))
            {
                RejectResultClientRpc(taskId, error, SingleTarget(senderId));
                return;
            }

            RepairDoneClientRpc(taskId, (int)senderId);
        }

        [ClientRpc]
        private void StartRepairClientRpc(int taskId, ClientRpcParams _ = default)
        {
            _currentIsRepair = true;
            OpenMinigame(taskId);
        }

        [ClientRpc]
        private void RepairDoneClientRpc(int taskId, int clientId, ClientRpcParams _ = default)
        {
            Debug.Log($"[MiniGameBridge] 破坏 {taskId} 由玩家 {clientId} 修复");
            if (_currentTaskId == taskId) CloseMinigame();
        }

        // ── Minigame Lifecycle (client-side only) ──

        private Dictionary<ulong, int> _pendingRepair = new Dictionary<ulong, int>();
        private bool _currentIsRepair;

        /// <summary>
        /// 清理指定客户端的修复挂起状态（断线/离开时调用）。
        /// </summary>
        public void CleanupPendingRepair(ulong clientId)
        {
            if (_pendingRepair.Remove(clientId))
            {
                Debug.Log($"[MiniGameBridge] 清理客户端 {clientId} 的挂起修复");
            }
        }

        public override void OnDestroy()
        {
            CloseMinigame();
            _pendingRepair.Clear();
            base.OnDestroy();
        }

        private void OpenMinigame(int taskId)
        {
            CloseMinigame();
            _currentTaskId = taskId;

            MiniGameBase minigame = CreateDefaultMinigame(taskId, transform);
            if (minigame == null) return;

            minigame.OnComplete += OnMinigameComplete;
            minigame.OnCancel += OnMinigameCancel;
            _currentMinigame = minigame.gameObject;
            minigame.Show();
        }

        private void CloseMinigame()
        {
            if (_currentMinigame != null)
            {
                MiniGameBase mb = _currentMinigame.GetComponent<MiniGameBase>();
                if (mb != null)
                {
                    mb.OnComplete -= OnMinigameComplete;
                    mb.OnCancel -= OnMinigameCancel;
                    mb.Hide();
                }
                Destroy(_currentMinigame);
                _currentMinigame = null;
            }
            _currentTaskId = -1;
            _currentIsRepair = false;
        }

        private void OnMinigameComplete(MiniGameBase _)
        {
            int taskId = _currentTaskId;
            if (_currentIsRepair)
            {
                SubmitRepairResultServerRpc(taskId, true);
            }
            else
            {
                SubmitTaskResultServerRpc(taskId, true);
            }
        }

        private void OnMinigameCancel(MiniGameBase _)
        {
            int taskId = _currentTaskId;
            bool wasRepair = _currentIsRepair;
            CloseMinigame();
            if (wasRepair)
            {
                SubmitRepairResultServerRpc(taskId, false);
            }
            else
            {
                SubmitTaskResultServerRpc(taskId, false);
            }
        }

        // ── Minigame Factory ──

        public static MiniGameBase CreateDefaultMinigame(int taskId, Transform parent)
        {
            int bucket = Mathf.Abs(taskId) % 13;
            GameObject go = new GameObject($"MiniGame_{taskId}");
            go.transform.SetParent(parent ?? null);

            MiniGameBase result = bucket switch
            {
                0  => go.AddComponent<WireTask>(),
                1  => go.AddComponent<KeypadTask>(),
                2  => go.AddComponent<SwipeCardTask>(),
                3  => go.AddComponent<ScanTask>(),
                4  => go.AddComponent<DownloadTask>(),
                5  => go.AddComponent<MemoryTask>(),
                6  => go.AddComponent<SortTask>(),
                7  => go.AddComponent<TapTask>(),
                8  => go.AddComponent<AsteroidTask>(),
                9  => go.AddComponent<CalibrateTask>(),
                10 => go.AddComponent<EvidenceArchiveTask>(),
                11 => go.AddComponent<WireTask>(),   // 轮转复用
                12 => go.AddComponent<KeypadTask>(),  // 轮转复用
                _  => go.AddComponent<WireTask>()
            };
            return result;
        }

        private static ClientRpcParams SingleTarget(ulong clientId)
        {
            if (clientId == 0)
            {
                Debug.LogWarning("[MiniGameBridge] SingleTarget called with clientId=0, this sends RPC to host — verify intent.");
            }
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
        }
    }
}
