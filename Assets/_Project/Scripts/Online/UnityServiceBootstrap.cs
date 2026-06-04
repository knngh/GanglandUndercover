using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Relay;
using UnityEngine;

namespace GanglandUndercover.Online
{
    public sealed class UnityServiceBootstrap : MonoBehaviour
    {
        private bool initializationStarted;
        private Task initializationTask;
        private bool servicesReady;
        private bool authenticationReady;
        private bool lobbyReady;
        private bool relayReady;
        private string cloudProjectId = string.Empty;
        private string playerId = string.Empty;
        private string status = "Unity Services 待初始化。";

        public bool CloudProjectBound => !string.IsNullOrWhiteSpace(CloudProjectId);
        public string CloudProjectId => string.IsNullOrWhiteSpace(cloudProjectId) ? Application.cloudProjectId ?? string.Empty : cloudProjectId;
        public bool ServicesReady => servicesReady;
        public bool AuthenticationReady => authenticationReady;
        public bool LobbyReady => lobbyReady;
        public bool RelayReady => relayReady;

        // Vivox removed — stub properties
        public bool VivoxReady => false;
        public bool VivoxLoggedIn => false;
        public bool ActiveVoiceChannelIsPositional => false;
        public string ActiveVoiceChannel => string.Empty;
        public string VoiceStatus => "Vivox 未安装（已移除）。";
        public int ActiveVoiceParticipantCount => 0;
        public string PlayerId => playerId;
        public string ServiceReadinessSummary => BuildReadinessSummary();
        public string Status => status;

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                status = "编辑器未播放，Unity Services 未启动。";
                return;
            }

            _ = InitializeAsync();
        }

        public Task InitializeAsync()
        {
            if (initializationTask != null)
            {
                return initializationTask;
            }

            initializationTask = InitializeCoreAsync();
            return initializationTask;
        }

        private async Task InitializeCoreAsync()
        {
            if (initializationStarted)
            {
                return;
            }

            initializationStarted = true;
            RefreshCloudProjectId();
            InitializationOptions initializationOptions = new InitializationOptions();

            if (!CloudProjectBound)
            {
                status = "Unity Cloud Project 尚未绑定：Authentication/Lobby/Relay 等待项目 ID。";
                Debug.LogWarning(status);
                return;
            }

            try
            {
                status = "正在初始化 Unity Services。";
                await UnityServices.InitializeAsync(initializationOptions);
                servicesReady = UnityServices.State == ServicesInitializationState.Initialized;
                RefreshConnectivityReadiness();
            }
            catch (Exception exception)
            {
                servicesReady = UnityServices.State == ServicesInitializationState.Initialized;
                RefreshConnectivityReadiness();
                status = "Unity Services 初始化失败：" + exception.Message;
                Debug.LogWarning(status);
                return;
            }

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    status = "Unity Services 已初始化，正在匿名登录。";
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                authenticationReady = AuthenticationService.Instance.IsSignedIn;
                playerId = AuthenticationService.Instance.PlayerId ?? string.Empty;
            }
            catch (Exception exception)
            {
                authenticationReady = false;
                playerId = string.Empty;
                status = "Authentication 登录失败：" + exception.Message;
                Debug.LogWarning(status);
            }

            RefreshConnectivityReadiness();
            status = BuildReadinessSummary();
            Debug.Log(status + (string.IsNullOrEmpty(playerId) ? string.Empty : " PlayerId=" + playerId));
        }

        public Task<bool> EnsureVivoxLoggedInAsync(string displayName)
        {
            return Task.FromResult(false);
        }

        public Task<bool> JoinVoiceChannelAsync(string channelName, string displayName, bool positional)
        {
            return Task.FromResult(false);
        }

        public void UpdatePositionalVoice(Vector3 worldPosition)
        {
        }

        public Task LeaveVoiceChannelsAsync()
        {
            return Task.CompletedTask;
        }

        private void RefreshCloudProjectId()
        {
            cloudProjectId = Application.cloudProjectId ?? string.Empty;
        }

        private void RefreshConnectivityReadiness()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                lobbyReady = false;
                relayReady = false;
                return;
            }

            lobbyReady = HasLobbyInstance();
            relayReady = HasRelayInstance();
        }

        private static bool HasLobbyInstance()
        {
            try
            {
                return LobbyService.Instance != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool HasRelayInstance()
        {
            try
            {
                return RelayService.Instance != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private string BuildReadinessSummary()
        {
            return (CloudProjectBound ? "Cloud OK " + ShortCloudProjectId() : "Cloud 未绑定")
                + " | "
                + (servicesReady ? "Services OK" : "Services 待初始化")
                + " | "
                + (authenticationReady ? "Auth OK" : "Auth 待登录")
                + " | "
                + (lobbyReady ? "Lobby OK" : "Lobby 待初始化")
                + " | "
                + (relayReady ? "Relay OK" : "Relay 待初始化")
                + " | Vivox 已移除";
        }

        private string ShortCloudProjectId()
        {
            string id = CloudProjectId;
            return string.IsNullOrWhiteSpace(id) || id.Length <= 8 ? id : id.Substring(0, 8);
        }
    }
}
