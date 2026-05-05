using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Relay;
using Unity.Services.Vivox;
using UnityEngine;

namespace GanglandUndercover.Online
{
    public sealed class UnityServiceBootstrap : MonoBehaviour
    {
        private const string PlaceholderVivoxServer = "https://placeholder.vivox.invalid/api2";
        private const string PlaceholderVivoxDomain = "gangland-placeholder";
        private const string PlaceholderVivoxIssuer = "gangland-placeholder";
        private const string PlaceholderVivoxTokenKey = "gangland-placeholder";

        private bool initializationStarted;
        private Task initializationTask;
        private bool servicesReady;
        private bool authenticationReady;
        private bool lobbyReady;
        private bool relayReady;
        private bool vivoxReady;
        private bool vivoxLoggedIn;
        private bool vivoxEventsRegistered;
        private bool vivoxProjectCredentialsAvailable = true;
        private bool usingPlaceholderVivoxCredentials;
        private bool activeVoiceChannelIsPositional;
        private string cloudProjectId = string.Empty;
        private string playerId = string.Empty;
        private string activeVoiceChannel = string.Empty;
        private string vivoxStatus = "Vivox 待初始化。";
        private string voiceStatus = "Vivox 语音待连接。";
        private string status = "Unity Services 待初始化。";

        public bool CloudProjectBound => !string.IsNullOrWhiteSpace(CloudProjectId);
        public string CloudProjectId => string.IsNullOrWhiteSpace(cloudProjectId) ? Application.cloudProjectId ?? string.Empty : cloudProjectId;
        public bool ServicesReady => servicesReady;
        public bool AuthenticationReady => authenticationReady;
        public bool LobbyReady => lobbyReady;
        public bool RelayReady => relayReady;
        public bool VivoxReady => vivoxReady;
        public bool VivoxLoggedIn => vivoxLoggedIn || SafeVivoxLoggedIn();
        public string ActiveVoiceChannel => activeVoiceChannel;
        public bool ActiveVoiceChannelIsPositional => activeVoiceChannelIsPositional;
        public string VoiceStatus => voiceStatus;
        public int ActiveVoiceParticipantCount => CountParticipants(activeVoiceChannel);
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
            InitializationOptions initializationOptions = BuildInitializationOptions();

            if (!CloudProjectBound)
            {
                status = "Unity Cloud Project 尚未绑定：Authentication/Lobby/Relay/Vivox 等待项目 ID。";
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

            if (Application.isEditor && !vivoxProjectCredentialsAvailable)
            {
                vivoxReady = false;
                vivoxLoggedIn = false;
                vivoxStatus = "Vivox 未配置，已降级为无语音模式。";
                voiceStatus = vivoxStatus;
                Debug.Log(vivoxStatus);
            }
            else if (usingPlaceholderVivoxCredentials)
            {
                vivoxReady = false;
                vivoxLoggedIn = false;
                vivoxStatus = "Vivox 使用占位配置，已降级为无语音模式。";
                voiceStatus = vivoxStatus;
                Debug.Log(vivoxStatus);
            }
            else
            {
                try
                {
                    status = "Authentication 已处理，正在初始化 Vivox。";
                    await VivoxService.Instance.InitializeAsync();
                    RegisterVivoxEvents();
                    vivoxReady = true;
                    vivoxStatus = "Vivox OK";
                    voiceStatus = "Vivox 已初始化，等待加入语音频道。";
                }
                catch (Exception exception)
                {
                    vivoxReady = false;
                    vivoxLoggedIn = false;
                    vivoxStatus = "Vivox 待 Dashboard/语音频道配置：" + exception.Message;
                    voiceStatus = vivoxStatus;
                    Debug.LogWarning("Vivox 初始化未完成：" + exception.Message);
                }
            }

            RefreshConnectivityReadiness();
            status = BuildReadinessSummary();
            Debug.Log(status + (string.IsNullOrEmpty(playerId) ? string.Empty : " PlayerId=" + playerId));
        }

        private InitializationOptions BuildInitializationOptions()
        {
            InitializationOptions options = new InitializationOptions();
            usingPlaceholderVivoxCredentials = false;

#if UNITY_EDITOR
            if (TryLoadEditorVivoxCredentials(out string server, out string domain, out string issuer, out string tokenKey))
            {
                vivoxProjectCredentialsAvailable = true;
                options.SetVivoxCredentials(server, domain, issuer, tokenKey);
            }
            else
            {
                vivoxProjectCredentialsAvailable = false;
                usingPlaceholderVivoxCredentials = true;
                options.SetVivoxCredentials(PlaceholderVivoxServer, PlaceholderVivoxDomain, PlaceholderVivoxIssuer, PlaceholderVivoxTokenKey);
            }
#endif

            return options;
        }

#if UNITY_EDITOR
        private static bool TryLoadEditorVivoxCredentials(out string server, out string domain, out string issuer, out string tokenKey)
        {
            server = string.Empty;
            domain = string.Empty;
            issuer = string.Empty;
            tokenKey = string.Empty;

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    return false;
                }

                string settingsPath = Path.Combine(projectRoot, "ProjectSettings/Packages/com.unity.services.vivox/Settings.json");
                if (!File.Exists(settingsPath))
                {
                    return false;
                }

                string json = File.ReadAllText(settingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                VivoxSettingsFile settings = JsonUtility.FromJson<VivoxSettingsFile>(json);
                if (settings?.m_Dictionary?.m_DictionaryValues == null)
                {
                    return false;
                }

                foreach (VivoxSettingsEntry entry in settings.m_Dictionary.m_DictionaryValues)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    {
                        continue;
                    }

                    string value = UnwrapVivoxSettingValue(entry.value);

                    switch (entry.key)
                    {
                        case "server":
                            server = value;
                            break;
                        case "domain":
                            domain = value;
                            break;
                        case "tokenIssuer":
                            issuer = value;
                            break;
                        case "tokenKey":
                            tokenKey = value;
                            break;
                    }
                }

                return !string.IsNullOrWhiteSpace(server)
                    && !string.IsNullOrWhiteSpace(domain)
                    && !string.IsNullOrWhiteSpace(issuer);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Vivox 编辑器设置读取失败：" + exception.Message);
                return false;
            }
        }

        private static string UnwrapVivoxSettingValue(string wrappedValueJson)
        {
            if (string.IsNullOrWhiteSpace(wrappedValueJson))
            {
                return string.Empty;
            }

            try
            {
                VivoxSettingValueWrapper wrapper = JsonUtility.FromJson<VivoxSettingValueWrapper>(wrappedValueJson);
                return wrapper?.m_Value ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        [Serializable]
        private sealed class VivoxSettingsFile
        {
            public VivoxSettingsDictionary m_Dictionary;
        }

        [Serializable]
        private sealed class VivoxSettingsDictionary
        {
            public VivoxSettingsEntry[] m_DictionaryValues;
        }

        [Serializable]
        private sealed class VivoxSettingsEntry
        {
            public string key;
            public string value;
        }

        [Serializable]
        private sealed class VivoxSettingValueWrapper
        {
            public string m_Value;
        }
#endif

        public async Task<bool> EnsureVivoxLoggedInAsync(string displayName)
        {
            if (!Application.isPlaying)
            {
                voiceStatus = "编辑器未播放，Vivox 登录跳过。";
                return false;
            }

            if (!initializationStarted)
            {
                await InitializeAsync();
            }

            if (!vivoxReady)
            {
                voiceStatus = string.IsNullOrWhiteSpace(vivoxStatus) ? "Vivox 尚未初始化。" : vivoxStatus;
                return false;
            }

            try
            {
                if (VivoxService.Instance.IsLoggedIn)
                {
                    RegisterVivoxEvents();
                    vivoxLoggedIn = true;
                    voiceStatus = string.IsNullOrWhiteSpace(activeVoiceChannel)
                        ? "Vivox 已登录，等待语音频道。"
                        : "Vivox 已登录：" + activeVoiceChannel;
                    return true;
                }

                LoginOptions loginOptions = new LoginOptions
                {
                    DisplayName = LimitDisplayName(displayName),
                    ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.TenPerSecond
                };
                loginOptions.SpeechToTextLanguages.Add("zh-CN");
                loginOptions.SpeechToTextLanguages.Add("en-US");

                voiceStatus = "Vivox 正在登录。";
                await VivoxService.Instance.LoginAsync(loginOptions);
                RegisterVivoxEvents();
                vivoxLoggedIn = VivoxService.Instance.IsLoggedIn;
                voiceStatus = vivoxLoggedIn ? "Vivox 已登录，整局语音可用。" : "Vivox 登录未完成。";
                return vivoxLoggedIn;
            }
            catch (Exception exception)
            {
                vivoxLoggedIn = SafeVivoxLoggedIn();
                voiceStatus = "Vivox 登录失败：" + exception.Message;
                Debug.LogWarning(voiceStatus);
                return false;
            }
        }

        public async Task<bool> JoinVoiceChannelAsync(string channelName, string displayName, bool positional)
        {
            string safeChannelName = NormalizeChannelName(channelName);

            if (string.IsNullOrWhiteSpace(safeChannelName))
            {
                voiceStatus = "Vivox 频道名为空。";
                return false;
            }

            if (ActiveVoiceChannel == safeChannelName && VivoxLoggedIn)
            {
                return true;
            }

            if (!await EnsureVivoxLoggedInAsync(displayName))
            {
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(activeVoiceChannel) && activeVoiceChannel != safeChannelName)
                {
                    await VivoxService.Instance.LeaveAllChannelsAsync();
                    activeVoiceChannel = string.Empty;
                    activeVoiceChannelIsPositional = false;
                }

                ChannelOptions channelOptions = new ChannelOptions
                {
                    MakeActiveChannelUponJoining = true
                };

                if (positional)
                {
                    Channel3DProperties voiceSpace = new Channel3DProperties(9, 2, 1.25f, AudioFadeModel.InverseByDistance);
                    await VivoxService.Instance.JoinPositionalChannelAsync(safeChannelName, ChatCapability.AudioOnly, voiceSpace, channelOptions);
                    activeVoiceChannelIsPositional = true;
                }
                else
                {
                    await VivoxService.Instance.JoinGroupChannelAsync(safeChannelName, ChatCapability.AudioOnly, channelOptions);
                    activeVoiceChannelIsPositional = false;
                }

                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, safeChannelName);
                activeVoiceChannel = safeChannelName;
                voiceStatus = (positional ? "Vivox 行动近距离语音：" : "Vivox 全员语音：") + safeChannelName;
                return true;
            }
            catch (Exception exception)
            {
                voiceStatus = "Vivox 加入频道失败：" + exception.Message;
                Debug.LogWarning(voiceStatus);
                return false;
            }
        }

        public void UpdatePositionalVoice(Vector3 worldPosition)
        {
            if (!VivoxLoggedIn || string.IsNullOrWhiteSpace(activeVoiceChannel) || !activeVoiceChannelIsPositional)
            {
                return;
            }

            try
            {
                Vector3 vivoxPosition = new Vector3(worldPosition.x, 0f, worldPosition.y);
                VivoxService.Instance.Set3DPosition(vivoxPosition, vivoxPosition, Vector3.forward, Vector3.up, activeVoiceChannel, true);
            }
            catch (Exception exception)
            {
                voiceStatus = "Vivox 位置语音更新失败：" + exception.Message;
            }
        }

        public async Task LeaveVoiceChannelsAsync()
        {
            if (!vivoxReady || !VivoxLoggedIn)
            {
                activeVoiceChannel = string.Empty;
                activeVoiceChannelIsPositional = false;
                return;
            }

            try
            {
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
                await VivoxService.Instance.LeaveAllChannelsAsync();
                activeVoiceChannel = string.Empty;
                activeVoiceChannelIsPositional = false;
                voiceStatus = "Vivox 已离开语音频道。";
            }
            catch (Exception exception)
            {
                voiceStatus = "Vivox 离开频道失败：" + exception.Message;
                Debug.LogWarning(voiceStatus);
            }
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

        private void RegisterVivoxEvents()
        {
            if (vivoxEventsRegistered)
            {
                return;
            }

            VivoxService.Instance.LoggedIn += OnVivoxLoggedIn;
            VivoxService.Instance.LoggedOut += OnVivoxLoggedOut;
            VivoxService.Instance.ChannelJoined += OnVivoxChannelJoined;
            VivoxService.Instance.ChannelLeft += OnVivoxChannelLeft;
            VivoxService.Instance.ConnectionRecovering += OnVivoxConnectionRecovering;
            VivoxService.Instance.ConnectionRecovered += OnVivoxConnectionRecovered;
            VivoxService.Instance.ConnectionFailedToRecover += OnVivoxConnectionFailed;
            vivoxEventsRegistered = true;
        }

        private void UnregisterVivoxEvents()
        {
            if (!vivoxEventsRegistered)
            {
                return;
            }

            VivoxService.Instance.LoggedIn -= OnVivoxLoggedIn;
            VivoxService.Instance.LoggedOut -= OnVivoxLoggedOut;
            VivoxService.Instance.ChannelJoined -= OnVivoxChannelJoined;
            VivoxService.Instance.ChannelLeft -= OnVivoxChannelLeft;
            VivoxService.Instance.ConnectionRecovering -= OnVivoxConnectionRecovering;
            VivoxService.Instance.ConnectionRecovered -= OnVivoxConnectionRecovered;
            VivoxService.Instance.ConnectionFailedToRecover -= OnVivoxConnectionFailed;
            vivoxEventsRegistered = false;
        }

        private void OnDestroy()
        {
            UnregisterVivoxEvents();
        }

        private void OnVivoxLoggedIn()
        {
            vivoxLoggedIn = true;
            voiceStatus = "Vivox 已登录。";
        }

        private void OnVivoxLoggedOut()
        {
            vivoxLoggedIn = false;
            activeVoiceChannel = string.Empty;
            activeVoiceChannelIsPositional = false;
            voiceStatus = "Vivox 已登出。";
        }

        private void OnVivoxChannelJoined(string channelName)
        {
            activeVoiceChannel = NormalizeChannelName(channelName);
            voiceStatus = "Vivox 已加入频道：" + activeVoiceChannel;
        }

        private void OnVivoxChannelLeft(string channelName)
        {
            string safeName = NormalizeChannelName(channelName);

            if (safeName == activeVoiceChannel)
            {
                activeVoiceChannel = string.Empty;
                activeVoiceChannelIsPositional = false;
            }

            voiceStatus = "Vivox 已离开频道：" + safeName;
        }

        private void OnVivoxConnectionRecovering()
        {
            voiceStatus = "Vivox 网络恢复中。";
        }

        private void OnVivoxConnectionRecovered()
        {
            voiceStatus = string.IsNullOrWhiteSpace(activeVoiceChannel) ? "Vivox 网络已恢复。" : "Vivox 网络已恢复：" + activeVoiceChannel;
        }

        private void OnVivoxConnectionFailed()
        {
            voiceStatus = "Vivox 网络恢复失败。";
        }

        private static string NormalizeChannelName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);

            foreach (char character in value.ToLowerInvariant())
            {
                if (character >= 'a' && character <= 'z'
                    || character >= '0' && character <= '9'
                    || character == '-'
                    || character == '_')
                {
                    builder.Append(character);
                }
                else if (char.IsWhiteSpace(character) || character == ':' || character == '/')
                {
                    builder.Append('-');
                }
            }

            string normalized = builder.ToString().Trim('-');
            return normalized.Length <= 58 ? normalized : normalized.Substring(0, 58).Trim('-');
        }

        private static string LimitDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Gangland Player";
            }

            string trimmed = displayName.Trim();
            return trimmed.Length <= 24 ? trimmed : trimmed.Substring(0, 24);
        }

        private static bool SafeVivoxLoggedIn()
        {
            try
            {
                return VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int CountParticipants(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return 0;
            }

            try
            {
                string safeName = NormalizeChannelName(channelName);
                return VivoxService.Instance.ActiveChannels.TryGetValue(safeName, out var participants) ? participants.Count : 0;
            }
            catch (Exception)
            {
                return 0;
            }
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
                + " | "
                + (vivoxReady ? "Vivox OK" : vivoxStatus)
                + " | "
                + (VivoxLoggedIn ? "Voice " + (string.IsNullOrWhiteSpace(activeVoiceChannel) ? "已登录" : activeVoiceChannel) : voiceStatus);
        }

        private string ShortCloudProjectId()
        {
            string id = CloudProjectId;
            return string.IsNullOrWhiteSpace(id) || id.Length <= 8 ? id : id.Substring(0, 8);
        }
    }
}
