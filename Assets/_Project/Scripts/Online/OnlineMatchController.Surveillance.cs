using System.Collections.Generic;
using GanglandUndercover.Online.Surveillance;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// P0-3: RemoteSurveillance 客户端入口（Tech 远程监控 / 普通玩家贴机观看）。
    /// 服务端权威链路见 OnlineSecurityCamera（StartWatchingServerRpc 校验 + ServerTick 推送），
    /// 这里只负责本机 UI 状态机：C 键/HUD 按钮开关、摄像头切换、数据缓存与生命周期清理。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        private bool surveillanceViewOpen;
        private OnlineSecurityCamera watchedCamera;
        private VisiblePlayerData[] lastSurveillanceData = System.Array.Empty<VisiblePlayerData>();
        private float surveillanceDataAgeSeconds = 999f;
        private readonly List<OnlineSecurityCamera> clientCameras = new List<OnlineSecurityCamera>();

        public bool SurveillanceViewOpen => surveillanceViewOpen;
        public OnlineSecurityCamera WatchedCamera => watchedCamera;

        /// <summary>本机最近一帧摄像头数据（仅观看中有效；HUD 渲染用）。</summary>
        public IReadOnlyList<VisiblePlayerData> LastSurveillanceData => lastSurveillanceData;

        /// <summary>距上次收到摄像头数据的秒数（超过 ~1s 视为信号中断，如被服务端复审剔除）。</summary>
        public float SurveillanceDataAge => surveillanceDataAgeSeconds;

        /// <summary>客户端摄像头清单（打开面板时刷新一次并缓存）。</summary>
        public IReadOnlyList<OnlineSecurityCamera> ClientCameras => clientCameras;

        /// <summary>
        /// 本机当前是否允许打开监控视图：Tech（RemoteSurveillance 能力）任意时刻；
        /// 其他职业需在任一摄像头交互范围内（客户端预测，服务端 CanClientWatchCamera 仍终审）。
        /// </summary>
        public bool CanLocalOpenSurveillance()
        {
            if (!IsOnline || phase != OnlineMatchPhase.Action || !LocalAlive)
            {
                return false;
            }

            if (CanRemoteSurveil(LocalClientId()))
            {
                return true;
            }

            RefreshClientCameras();
            float range = ruleSet != null ? Mathf.Max(2.5f, ruleSet.InteractionRange) : 2.5f;
            Vector2 local = new Vector2(localPosition.x, localPosition.y);
            for (int i = 0; i < clientCameras.Count; i++)
            {
                OnlineSecurityCamera camera = clientCameras[i];
                if (camera != null && Vector2.Distance(local, camera.ZoneCenter) <= range)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>HUD/C 键入口：切换监控视图开关。</summary>
        public void ToggleSurveillanceView()
        {
            if (surveillanceViewOpen)
            {
                CloseSurveillanceView();
                return;
            }

            if (!CanLocalOpenSurveillance())
            {
                status = CanRemoteSurveil(LocalClientId())
                    ? "当前没有可用的监控摄像头。"
                    : "需要 Tech 权限或靠近监控摄像头才能查看。";
                return;
            }

            // CanLocalOpenSurveillance 内部已刷新 clientCameras 缓存，这里不再重复扫描。
            OnlineSecurityCamera preferred = PickDefaultSurveillanceCamera();
            if (preferred == null)
            {
                status = "当前没有可用的监控摄像头。";
                return;
            }

            surveillanceViewOpen = true;
            WatchSurveillanceCamera(preferred);
        }

        /// <summary>切换到指定摄像头观看（HUD 摄像头列表调用）。</summary>
        public void WatchSurveillanceCamera(OnlineSecurityCamera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (watchedCamera != null && watchedCamera != camera)
            {
                StopWatchingCamera(watchedCamera);
            }

            watchedCamera = camera;
            camera.OnCameraDataReceived += HandleSurveillanceData;
            camera.StartWatchingServerRpc();
            surveillanceDataAgeSeconds = 999f;
        }

        /// <summary>关闭监控视图并通知服务端停止推送。</summary>
        public void CloseSurveillanceView()
        {
            if (watchedCamera != null)
            {
                StopWatchingCamera(watchedCamera);
                watchedCamera = null;
            }

            surveillanceViewOpen = false;
            lastSurveillanceData = System.Array.Empty<VisiblePlayerData>();
            surveillanceDataAgeSeconds = 999f;
        }

        private void StopWatchingCamera(OnlineSecurityCamera camera)
        {
            camera.OnCameraDataReceived -= HandleSurveillanceData;
            camera.StopWatchingServerRpc();
        }

        private void HandleSurveillanceData(VisiblePlayerData[] data)
        {
            lastSurveillanceData = data ?? System.Array.Empty<VisiblePlayerData>();
            surveillanceDataAgeSeconds = 0f;
        }

        /// <summary>默认摄像头：Tech 取第一台；普通玩家取交互范围内最近一台。</summary>
        private OnlineSecurityCamera PickDefaultSurveillanceCamera()
        {
            if (clientCameras.Count == 0)
            {
                return null;
            }

            if (CanRemoteSurveil(LocalClientId()))
            {
                for (int i = 0; i < clientCameras.Count; i++)
                {
                    if (clientCameras[i] != null)
                    {
                        return clientCameras[i];
                    }
                }

                return null;
            }

            float range = ruleSet != null ? Mathf.Max(2.5f, ruleSet.InteractionRange) : 2.5f;
            Vector2 local = new Vector2(localPosition.x, localPosition.y);
            OnlineSecurityCamera nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < clientCameras.Count; i++)
            {
                OnlineSecurityCamera camera = clientCameras[i];
                if (camera == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(local, camera.ZoneCenter);
                if (distance <= range && distance < nearestDistance)
                {
                    nearest = camera;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void RefreshClientCameras()
        {
            clientCameras.Clear();
            // 摄像头是服务端 spawn 的 NetworkPrefab，各端场景里都有 clone；
            // 面板打开时扫描一次即可，避免每帧 FindObjects。
            OnlineSecurityCamera[] found = Object.FindObjectsByType<OnlineSecurityCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].IsSpawned)
                {
                    clientCameras.Add(found[i]);
                }
            }
        }

        /// <summary>监控数据年龄计时（Update 中推进；服务端 0.25s 一推，>1s 视为中断）。</summary>
        private void TickSurveillanceView(float deltaTime)
        {
            if (!surveillanceViewOpen)
            {
                return;
            }

            surveillanceDataAgeSeconds += deltaTime;

            // 客户端兜底生命周期：死亡或离开 Action 阶段（如会议）立即关闭。
            if (phase != OnlineMatchPhase.Action || !LocalAlive)
            {
                CloseSurveillanceView();
            }
        }
    }
}
