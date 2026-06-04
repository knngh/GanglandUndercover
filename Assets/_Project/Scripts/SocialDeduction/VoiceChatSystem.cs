using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    #region Enums & Structs

    /// <summary>
    /// 语音输入模式
    /// </summary>
    public enum VoiceInputMode
    {
        /// <summary>按键说话（Push-to-Talk），按下指定键时采集语音</summary>
        PushToTalk,

        /// <summary>语音激活（Voice Activity Detection），自动检测说话状态</summary>
        VoiceActivity
    }

    /// <summary>
    /// 语音通道类型
    /// </summary>
    public enum VoiceChannelType
    {
        /// <summary>全局通道：所有存活玩家可互相通话</summary>
        Global,

        /// <summary>区域通道：仅同一区域（如牢房/隔离区）内玩家可通话</summary>
        Proximity,

        /// <summary>私聊通道：一对一私密语音</summary>
        Whisper
    }

    /// <summary>
    /// 玩家语音状态
    /// </summary>
    public enum VoicePlayerState
    {
        /// <summary>空闲：未说话也未收听</summary>
        Idle,

        /// <summary>正在说话</summary>
        Speaking,

        /// <summary>静音中（手动或系统强制）</summary>
        Muted,

        /// <summary>死亡静音：死亡玩家不可说也不可听</summary>
        DeadSilenced
    }

    /// <summary>
    /// 语音玩家信息，用于语音通道管理与 UI 指示器
    /// </summary>
    [Serializable]
    public struct VoicePlayerInfo
    {
        /// <summary>玩家唯一标识（与 SocialCharacter.CharacterName 对应）</summary>
        public string PlayerId;

        /// <summary>关联的 SocialCharacter 引用</summary>
        public SocialCharacter Character;

        /// <summary>当前语音状态</summary>
        public VoicePlayerState State;

        /// <summary>当前所在区域标识（用于 Proximity 通道匹配）</summary>
        public string AreaId;

        /// <summary>当前语音音量归一化值（0~1），驱动 UI 声波动画</summary>
        public float NormalizedVolume;

        /// <summary>是否为本地玩家</summary>
        public bool IsLocal;
    }

    #endregion

    #region AudioFilters Interface

    /// <summary>
    /// 音频滤波器接口，预留用于降噪、回声消除等 DSP 处理
    /// 实际音频处理由平台原生 SDK（如 WebRTC、FMOD）实现，
    /// 本接口提供 Unity 侧参数配置入口与开关控制
    /// </summary>
    public interface IAudioFilter
    {
        /// <summary>滤波器名称</summary>
        string FilterName { get; }

        /// <summary>是否启用</summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 处理音频采样缓冲区
        /// </summary>
        /// <param name="samples">输入/输出音频采样数据</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="channels">声道数</param>
        void Process(float[] samples, int sampleRate, int channels);

        /// <summary>重置滤波器状态</summary>
        void Reset();
    }

    /// <summary>
    /// 降噪滤波器配置
    /// </summary>
    [Serializable]
    public class NoiseReductionConfig
    {
        /// <summary>降噪强度（0~1，越高去噪越激进）</summary>
        [Range(0f, 1f)]
        public float suppressionLevel = 0.6f;

        /// <summary>噪声门限阈值（低于此值的信号视为噪声）</summary>
        [Range(0.001f, 0.1f)]
        public float noiseGateThreshold = 0.01f;

        /// <summary>噪声门限释放时间（秒）</summary>
        [Range(0.05f, 1f)]
        public float noiseGateRelease = 0.15f;
    }

    /// <summary>
    /// 回声消除滤波器配置
    /// </summary>
    [Serializable]
    public class EchoCancellationConfig
    {
        /// <summary>回声消除强度（0~1）</summary>
        [Range(0f, 1f)]
        public float cancellationLevel = 0.8f;

        /// <summary>回声尾长（毫秒），匹配房间混响时间</summary>
        [Range(50, 500)]
        public int echoTailLength = 200;

        /// <summary>双讲检测灵敏度</summary>
        [Range(0f, 1f)]
        public float doubleTalkSensitivity = 0.5f;
    }

    /// <summary>
    /// AudioFilters 聚合配置与模拟接口
    /// 在 WebRTC/FMOD 等真实音频后端集成前，
    /// 通过本类传递参数并对外暴露滤波器状态
    /// </summary>
    [Serializable]
    public class AudioFilters
    {
        [Header("Noise Reduction")]
        public bool enableNoiseReduction = true;
        public NoiseReductionConfig noiseReduction = new NoiseReductionConfig();

        [Header("Echo Cancellation")]
        public bool enableEchoCancellation = true;
        public EchoCancellationConfig echoCancellation = new EchoCancellationConfig();

        [Header("Auto Gain Control")]
        public bool enableAutoGainControl = true;

        [Range(0.1f, 5f)]
        public float targetGainLevel = 1f;

        /// <summary>
        /// 当前活跃的滤波器列表（由具体音频后端注入）
        /// </summary>
        [NonSerialized]
        public List<IAudioFilter> ActiveFilters = new List<IAudioFilter>();
    }

    #endregion

    #region Voice Indicator Data

    /// <summary>
    /// 语音指示器数据：驱动说话者头像旁声波动画
    /// </summary>
    [Serializable]
    public struct VoiceIndicatorData
    {
        /// <summary>说话者 ID</summary>
        public string PlayerId;

        /// <summary>归一化音量为 0~1</summary>
        public float Volume;

        /// <summary>是否正在说话</summary>
        public bool IsSpeaking;

        /// <summary>语音通道类型</summary>
        public VoiceChannelType ChannelType;
    }

    #endregion

    /// <summary>
    /// 语音聊天系统
    /// 管理房间内实时语音通话，支持按键说话/语音激活双模式、
    /// 声波指示器UI、降噪回声消除接口、私聊语音通道及死亡全局静音。
    /// 与 ChatSystem 通过事件集成：语音状态变化触发聊天 UI 更新。
    /// </summary>
    public class VoiceChatSystem : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Input Settings")]
        [SerializeField] private VoiceInputMode inputMode = VoiceInputMode.PushToTalk;
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;

        [Header("Voice Activity Detection")]
        [SerializeField] private float vadThreshold = 0.02f;
        [SerializeField] private float vadHoldTime = 0.3f;
        [SerializeField] private float vadSilenceTimeout = 0.8f;

        [Header("Audio Filters")]
        [SerializeField] private AudioFilters audioFilters = new AudioFilters();

        [Header("Proximity Settings")]
        [SerializeField] private float proximityMaxDistance = 25f;
        [SerializeField] private AnimationCurve proximityAttenuation = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Voice Indicator")]
        [SerializeField] private float indicatorSmoothTime = 0.1f;
        [SerializeField] private int indicatorSampleCount = 8;

        #endregion

        #region Private Fields

        private readonly Dictionary<string, VoicePlayerInfo> playerRegistry = new Dictionary<string, VoicePlayerInfo>();
        private readonly List<string> isolatedPlayerIds = new List<string>();
        private readonly Dictionary<string, string> forcedAreaMapping = new Dictionary<string, string>();

        // VAD 状态
        private float vadCurrentLevel;
        private float vadHoldTimer;
        private float vadSilenceTimer;
        private bool vadIsSpeaking;

        // 音量平滑
        private float smoothedVolume;
        private float volumeVelocity;

        // 指示器声波采样
        private readonly Queue<float> indicatorSamples = new Queue<float>();
        private float indicatorCurrentValue;

        // 本地玩家缓存
        private string localPlayerId;

        #endregion

        #region Public Properties

        /// <summary>当前语音输入模式</summary>
        public VoiceInputMode InputMode
        {
            get => inputMode;
            set => inputMode = value;
        }

        /// <summary>按键说话绑定的按键</summary>
        public KeyCode PushToTalkKey
        {
            get => pushToTalkKey;
            set => pushToTalkKey = value;
        }

        /// <summary>AudioFilters 配置（可运行时修改）</summary>
        public AudioFilters AudioFilterSettings => audioFilters;

        /// <summary>本地玩家是否正在说话</summary>
        public bool IsLocalSpeaking { get; private set; }

        /// <summary>本地玩家是否被静音（手动或系统强制）</summary>
        public bool IsLocalMuted { get; private set; }

        /// <summary>本地玩家是否死亡（死亡后全局静音）</summary>
        public bool IsLocalDead { get; private set; }

        /// <summary>当前活跃的说话者数量</summary>
        public int ActiveSpeakerCount { get; private set; }

        /// <summary>获取所有语音指示器数据（供 UI 层轮询）</summary>
        public List<VoiceIndicatorData> ActiveIndicators { get; } = new List<VoiceIndicatorData>();

        #endregion

        #region Events — ChatSystem Integration

        /// <summary>
        /// 本地玩家语音状态变化时触发。
        /// ChatSystem 可订阅此事件来更新聊天 UI（如显示/隐藏麦克风图标、声波等）。
        /// 参数：(playerId, isSpeaking, volume)
        /// </summary>
        public event Action<string, bool, float> OnLocalVoiceStateChanged;

        /// <summary>
        /// 远程玩家语音状态变化时触发。
        /// ChatSystem 可订阅此事件在说话者头像旁显示声波动画。
        /// 参数：(playerId, isSpeaking, volume, channelType)
        /// </summary>
        public event Action<string, bool, float, VoiceChannelType> OnRemoteVoiceStateChanged;

        /// <summary>
        /// 玩家语音通道切换时触发。
        /// ChatSystem 可订阅此事件更新语音通道 UI 标识。
        /// 参数：(playerId, newChannel)
        /// </summary>
        public event Action<string, VoiceChannelType> OnVoiceChannelChanged;

        /// <summary>
        /// 本地麦克风静音状态变化时触发。
        /// 参数：(isMuted)
        /// </summary>
        public event Action<bool> OnMuteStateChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            indicatorSamples.Clear();
            Debug.Log("[VoiceChatSystem] Initialized");
        }

        private void Update()
        {
            ProcessLocalVoiceInput();
            UpdateVoiceIndicators();
            CheckDeadPlayerSilence();
        }

        private void OnDestroy()
        {
            playerRegistry.Clear();
            isolatedPlayerIds.Clear();
            forcedAreaMapping.Clear();
            indicatorSamples.Clear();
            ActiveIndicators.Clear();
            Debug.Log("[VoiceChatSystem] Destroyed");
        }

        #endregion

        #region Player Registry

        /// <summary>
        /// 注册玩家到语音系统。在玩家加入房间时调用。
        /// </summary>
        public void RegisterPlayer(string playerId, SocialCharacter character, string areaId, bool isLocal = false)
        {
            if (string.IsNullOrEmpty(playerId) || playerRegistry.ContainsKey(playerId))
            {
                Debug.LogWarning($"[VoiceChatSystem] Player {playerId} already registered or invalid ID");
                return;
            }

            var info = new VoicePlayerInfo
            {
                PlayerId = playerId,
                Character = character,
                State = VoicePlayerState.Idle,
                AreaId = areaId,
                NormalizedVolume = 0f,
                IsLocal = isLocal
            };

            playerRegistry[playerId] = info;

            if (isLocal)
            {
                localPlayerId = playerId;
            }

            Debug.Log($"[VoiceChatSystem] Registered player: {playerId} (local={isLocal}, area={areaId})");
        }

        /// <summary>
        /// 注销玩家。在玩家离开房间或断开连接时调用。
        /// </summary>
        public void UnregisterPlayer(string playerId)
        {
            if (!playerRegistry.ContainsKey(playerId)) return;

            // 移除前清理语音状态
            if (playerRegistry[playerId].State == VoicePlayerState.Speaking)
            {
                SetPlayerSpeaking(playerId, false);
            }

            playerRegistry.Remove(playerId);

            if (playerId == localPlayerId)
            {
                localPlayerId = null;
            }

            Debug.Log($"[VoiceChatSystem] Unregistered player: {playerId}");
        }

        /// <summary>
        /// 获取指定玩家的语音信息
        /// </summary>
        public bool TryGetPlayerInfo(string playerId, out VoicePlayerInfo info)
        {
            return playerRegistry.TryGetValue(playerId, out info);
        }

        #endregion

        #region Voice Channel Management

        /// <summary>
        /// 将玩家移入隔离区域（如被关押）。
        /// 隔离玩家只能与同区域玩家语音通话。
        /// </summary>
        public void IsolatePlayer(string playerId, string isolationAreaId)
        {
            if (!playerRegistry.TryGetValue(playerId, out var info)) return;

            forcedAreaMapping[playerId] = isolationAreaId;
            info.AreaId = isolationAreaId;
            info.State = VoicePlayerState.Idle;
            playerRegistry[playerId] = info;

            if (!isolatedPlayerIds.Contains(playerId))
            {
                isolatedPlayerIds.Add(playerId);
            }

            OnVoiceChannelChanged?.Invoke(playerId, VoiceChannelType.Proximity);
            Debug.Log($"[VoiceChatSystem] Player {playerId} isolated to area: {isolationAreaId}");
        }

        /// <summary>
        /// 解除玩家隔离状态，恢复全局语音
        /// </summary>
        public void ReleasePlayer(string playerId)
        {
            forcedAreaMapping.Remove(playerId);
            isolatedPlayerIds.Remove(playerId);

            if (playerRegistry.TryGetValue(playerId, out var info))
            {
                info.AreaId = string.Empty;
                playerRegistry[playerId] = info;
            }

            OnVoiceChannelChanged?.Invoke(playerId, VoiceChannelType.Global);
            Debug.Log($"[VoiceChatSystem] Player {playerId} released from isolation");
        }

        /// <summary>
        /// 更新玩家所在区域（用于 Proximity 通道匹配）
        /// </summary>
        public void UpdatePlayerArea(string playerId, string areaId)
        {
            if (!playerRegistry.TryGetValue(playerId, out var info)) return;

            // 隔离玩家区域由系统强制管理
            if (forcedAreaMapping.ContainsKey(playerId)) return;

            info.AreaId = areaId;
            playerRegistry[playerId] = info;
        }

        /// <summary>
        /// 检查两个玩家是否在同一语音通道内可互相通话
        /// </summary>
        public bool CanPlayersCommunicate(string playerIdA, string playerIdB)
        {
            if (!playerRegistry.TryGetValue(playerIdA, out var infoA) ||
                !playerRegistry.TryGetValue(playerIdB, out var infoB))
            {
                return false;
            }

            // 死亡玩家不能说也不能听
            if (infoA.State == VoicePlayerState.DeadSilenced ||
                infoB.State == VoicePlayerState.DeadSilenced)
            {
                return false;
            }

            // 被手动静音的玩家
            if (infoA.State == VoicePlayerState.Muted ||
                infoB.State == VoicePlayerState.Muted)
            {
                return false;
            }

            // 隔离玩家只能与同区域玩家通话
            bool aIsolated = forcedAreaMapping.ContainsKey(playerIdA);
            bool bIsolated = forcedAreaMapping.ContainsKey(playerIdB);

            if (aIsolated && bIsolated)
            {
                return forcedAreaMapping[playerIdA] == forcedAreaMapping[playerIdB];
            }

            if (aIsolated || bIsolated)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取玩家当前的语音通道类型
        /// </summary>
        public VoiceChannelType GetPlayerChannelType(string playerId)
        {
            if (forcedAreaMapping.ContainsKey(playerId))
            {
                return VoiceChannelType.Proximity;
            }

            if (playerRegistry.TryGetValue(playerId, out var info) &&
                info.State == VoicePlayerState.DeadSilenced)
            {
                return VoiceChannelType.Global; // 死亡玩家技术上仍在全局通道但被静音
            }

            return VoiceChannelType.Global;
        }

        #endregion

        #region Voice Input Processing

        private void ProcessLocalVoiceInput()
        {
            if (string.IsNullOrEmpty(localPlayerId)) return;
            if (!playerRegistry.TryGetValue(localPlayerId, out var localInfo)) return;

            // 死亡玩家强制静音
            if (localInfo.State == VoicePlayerState.DeadSilenced)
            {
                if (IsLocalSpeaking)
                {
                    SetLocalSpeaking(false);
                }
                return;
            }

            bool wantsToSpeak = false;

            switch (inputMode)
            {
                case VoiceInputMode.PushToTalk:
                    wantsToSpeak = Input.GetKey(pushToTalkKey) && !IsLocalMuted;
                    break;

                case VoiceInputMode.VoiceActivity:
                    wantsToSpeak = EvaluateVAD() && !IsLocalMuted;
                    break;
            }

            if (wantsToSpeak && !IsLocalSpeaking)
            {
                SetLocalSpeaking(true);
            }
            else if (!wantsToSpeak && IsLocalSpeaking)
            {
                SetLocalSpeaking(false);
            }

            // 更新本地音量（说话时模拟，由真实音频后端替换）
            if (IsLocalSpeaking)
            {
                float rawVolume = Mathf.Clamp01(GetMicrophoneLevel());
                smoothedVolume = Mathf.SmoothDamp(smoothedVolume, rawVolume, ref volumeVelocity, indicatorSmoothTime);
                localInfo.NormalizedVolume = smoothedVolume;
                playerRegistry[localPlayerId] = localInfo;

                OnLocalVoiceStateChanged?.Invoke(localPlayerId, true, smoothedVolume);
            }
            else if (smoothedVolume > 0.01f)
            {
                smoothedVolume = Mathf.SmoothDamp(smoothedVolume, 0f, ref volumeVelocity, indicatorSmoothTime * 2f);
                localInfo.NormalizedVolume = smoothedVolume;
                playerRegistry[localPlayerId] = localInfo;

                OnLocalVoiceStateChanged?.Invoke(localPlayerId, false, smoothedVolume);
            }
        }

        /// <summary>
        /// 语音激活检测 (VAD)
        /// 基于阈值和静音超时判断玩家是否正在说话
        /// </summary>
        private bool EvaluateVAD()
        {
            float micLevel = GetMicrophoneLevel();

            if (micLevel > vadThreshold)
            {
                vadHoldTimer += Time.deltaTime;
                vadSilenceTimer = 0f;
            }
            else
            {
                vadSilenceTimer += Time.deltaTime;
            }

            if (vadHoldTimer >= vadHoldTime)
            {
                vadIsSpeaking = true;
            }

            if (vadSilenceTimer >= vadSilenceTimeout)
            {
                vadIsSpeaking = false;
                vadHoldTimer = 0f;
            }

            return vadIsSpeaking;
        }

        /// <summary>
        /// 获取麦克风输入电平（0~1）。
        /// 当前使用 Unity Microphone API 作为模拟输入源，
        /// 正式版应替换为平台原生音频 SDK（WebRTC/FMOD）。
        /// </summary>
        private float GetMicrophoneLevel()
        {
            // 模拟麦克风输入：采样 Unity Microphone 首设备
            if (Microphone.devices.Length == 0) return 0f;

            string device = Microphone.devices[0];
            if (!Microphone.IsRecording(device))
            {
                // 启动短录音以获取电平数据
                AudioClip clip = Microphone.Start(device, true, 1, 44100);
                if (clip == null) return 0f;
            }

            // 获取当前麦克风平均电平
            int sampleWindow = 128;
            float levelMax = 0f;
            float[] waveData = new float[sampleWindow];
            int micPosition = Microphone.GetPosition(device) - sampleWindow;

            if (micPosition < 0) return 0f;

            AudioClip recording = Microphone.Start(device, true, 1, 44100);
            if (recording != null)
            {
                recording.GetData(waveData, micPosition);

                for (int i = 0; i < sampleWindow; i++)
                {
                    float wavePeak = Mathf.Abs(waveData[i]);
                    if (wavePeak > levelMax) levelMax = wavePeak;
                }
            }

            return levelMax;
        }

        #endregion

        #region Voice State Management

        private void SetLocalSpeaking(bool speaking)
        {
            if (IsLocalSpeaking == speaking) return;
            IsLocalSpeaking = speaking;

            if (!playerRegistry.TryGetValue(localPlayerId, out var info)) return;

            info.State = speaking ? VoicePlayerState.Speaking : VoicePlayerState.Idle;
            info.NormalizedVolume = speaking ? smoothedVolume : 0f;
            playerRegistry[localPlayerId] = info;

            UpdateActiveSpeakerCount();

            Debug.Log($"[VoiceChatSystem] Local speaking: {speaking}");
        }

        /// <summary>
        /// 设置远程玩家的说话状态（由网络层调用）
        /// </summary>
        public void SetPlayerSpeaking(string playerId, bool speaking)
        {
            if (!playerRegistry.TryGetValue(playerId, out var info)) return;

            // 死亡玩家不能说话
            if (info.State == VoicePlayerState.DeadSilenced && speaking) return;

            info.State = speaking ? VoicePlayerState.Speaking : VoicePlayerState.Idle;
            playerRegistry[playerId] = info;

            VoiceChannelType channel = GetPlayerChannelType(playerId);
            OnRemoteVoiceStateChanged?.Invoke(playerId, speaking, info.NormalizedVolume, channel);

            UpdateActiveSpeakerCount();
        }

        /// <summary>
        /// 更新远程玩家的音量数据（由网络层每帧调用）
        /// </summary>
        public void UpdatePlayerVolume(string playerId, float volume)
        {
            if (!playerRegistry.TryGetValue(playerId, out var info)) return;

            volume = Mathf.Clamp01(volume);
            info.NormalizedVolume = volume;
            playerRegistry[playerId] = info;

            if (info.State == VoicePlayerState.Speaking)
            {
                VoiceChannelType channel = GetPlayerChannelType(playerId);
                OnRemoteVoiceStateChanged?.Invoke(playerId, true, volume, channel);
            }
        }

        /// <summary>
        /// 切换本地麦克风静音
        /// </summary>
        public void ToggleMute()
        {
            IsLocalMuted = !IsLocalMuted;

            if (IsLocalMuted && IsLocalSpeaking)
            {
                SetLocalSpeaking(false);
            }

            if (playerRegistry.TryGetValue(localPlayerId, out var info))
            {
                info.State = IsLocalMuted ? VoicePlayerState.Muted : VoicePlayerState.Idle;
                playerRegistry[localPlayerId] = info;
            }

            OnMuteStateChanged?.Invoke(IsLocalMuted);
            OnLocalVoiceStateChanged?.Invoke(localPlayerId, false, 0f);
            Debug.Log($"[VoiceChatSystem] Mute toggled: {IsLocalMuted}");
        }

        /// <summary>
        /// 设置本地麦克风静音
        /// </summary>
        public void SetMute(bool muted)
        {
            if (IsLocalMuted == muted) return;
            ToggleMute();
        }

        #endregion

        #region Dead Player Silence

        /// <summary>
        /// 将玩家标记为死亡，触发全局静音
        /// </summary>
        public void MarkPlayerDead(string playerId)
        {
            if (!playerRegistry.TryGetValue(playerId, out var info)) return;

            info.State = VoicePlayerState.DeadSilenced;
            info.NormalizedVolume = 0f;
            playerRegistry[playerId] = info;

            // 如果是本地玩家死亡
            if (info.IsLocal)
            {
                IsLocalDead = true;
                IsLocalMuted = true;
                SetLocalSpeaking(false);
                OnMuteStateChanged?.Invoke(true);
            }

            // 通知 ChatSystem 该玩家语音已停止
            VoiceChannelType channel = GetPlayerChannelType(playerId);
            OnRemoteVoiceStateChanged?.Invoke(playerId, false, 0f, channel);

            UpdateActiveSpeakerCount();
            Debug.Log($"[VoiceChatSystem] Player {playerId} marked dead — globally silenced");
        }

        /// <summary>
        /// 响应 SocialCharacter.Kill() 的场景回调，自动同步死亡状态
        /// </summary>
        private void CheckDeadPlayerSilence()
        {
            foreach (var kvp in playerRegistry)
            {
                var info = kvp.Value;
                if (info.Character == null) continue;

                // SocialCharacter.IsAlive 为 false 但语音系统尚未标记死亡
                if (!info.Character.IsAlive && info.State != VoicePlayerState.DeadSilenced)
                {
                    MarkPlayerDead(kvp.Key);
                }
            }
        }

        #endregion

        #region Voice Indicator

        private void UpdateVoiceIndicators()
        {
            ActiveIndicators.Clear();

            foreach (var kvp in playerRegistry)
            {
                var info = kvp.Value;

                if (info.State == VoicePlayerState.Speaking ||
                    (info.IsLocal && IsLocalSpeaking))
                {
                    ActiveIndicators.Add(new VoiceIndicatorData
                    {
                        PlayerId = info.PlayerId,
                        Volume = info.NormalizedVolume,
                        IsSpeaking = true,
                        ChannelType = GetPlayerChannelType(info.PlayerId)
                    });
                }
            }

            // 平滑指示器数值用于 UI 声波振幅
            if (ActiveIndicators.Count > 0)
            {
                float avgVolume = 0f;
                foreach (var ind in ActiveIndicators)
                {
                    avgVolume += ind.Volume;
                }
                avgVolume /= ActiveIndicators.Count;

                indicatorCurrentValue = Mathf.SmoothDamp(
                    indicatorCurrentValue, avgVolume, ref volumeVelocity, indicatorSmoothTime);

                indicatorSamples.Enqueue(indicatorCurrentValue);
                if (indicatorSamples.Count > indicatorSampleCount)
                {
                    indicatorSamples.Dequeue();
                }
            }
            else
            {
                indicatorCurrentValue = Mathf.SmoothDamp(
                    indicatorCurrentValue, 0f, ref volumeVelocity, indicatorSmoothTime * 2f);

                indicatorSamples.Enqueue(indicatorCurrentValue);
                if (indicatorSamples.Count > indicatorSampleCount)
                {
                    indicatorSamples.Dequeue();
                }
            }
        }

        /// <summary>
        /// 获取声波指示器采样数组（供 UI VoiceIndicator 组件读取，绘制声波动画）
        /// </summary>
        public float[] GetIndicatorSamples()
        {
            return indicatorSamples.ToArray();
        }

        /// <summary>
        /// 获取当前平滑指示器值
        /// </summary>
        public float GetIndicatorValue()
        {
            return indicatorCurrentValue;
        }

        #endregion

        #region Audio Filters Management

        /// <summary>
        /// 注册音频滤波器
        /// </summary>
        public void RegisterAudioFilter(IAudioFilter filter)
        {
            if (filter == null) return;

            if (!audioFilters.ActiveFilters.Contains(filter))
            {
                audioFilters.ActiveFilters.Add(filter);
                Debug.Log($"[VoiceChatSystem] Audio filter registered: {filter.FilterName}");
            }
        }

        /// <summary>
        /// 注销音频滤波器
        /// </summary>
        public void UnregisterAudioFilter(IAudioFilter filter)
        {
            if (filter == null) return;

            audioFilters.ActiveFilters.Remove(filter);
            Debug.Log($"[VoiceChatSystem] Audio filter unregistered: {filter.FilterName}");
        }

        /// <summary>
        /// 对音频数据应用所有已注册的滤波器
        /// </summary>
        public void ApplyAudioFilters(float[] samples, int sampleRate, int channels)
        {
            if (!audioFilters.enableNoiseReduction && !audioFilters.enableEchoCancellation) return;

            foreach (var filter in audioFilters.ActiveFilters)
            {
                if (!filter.IsEnabled) continue;

                bool shouldApply = false;

                if (filter is NoiseReductionFilter && audioFilters.enableNoiseReduction)
                    shouldApply = true;
                else if (filter is EchoCancellationFilter && audioFilters.enableEchoCancellation)
                    shouldApply = true;

                if (shouldApply)
                {
                    filter.Process(samples, sampleRate, channels);
                }
            }
        }

        #endregion

        #region Utility

        private void UpdateActiveSpeakerCount()
        {
            ActiveSpeakerCount = 0;
            foreach (var kvp in playerRegistry)
            {
                if (kvp.Value.State == VoicePlayerState.Speaking)
                {
                    ActiveSpeakerCount++;
                }
            }
        }

        /// <summary>
        /// 获取指定玩家是否可被本地玩家听到
        /// </summary>
        public bool CanHearPlayer(string remotePlayerId)
        {
            if (string.IsNullOrEmpty(localPlayerId)) return false;
            return CanPlayersCommunicate(localPlayerId, remotePlayerId);
        }

        /// <summary>
        /// 根据距离计算 Proximity 通道音量衰减（0~1）
        /// </summary>
        public float GetProximityAttenuation(Vector3 listenerPos, Vector3 speakerPos)
        {
            float distance = Vector3.Distance(listenerPos, speakerPos);
            float normalized = Mathf.Clamp01(distance / proximityMaxDistance);
            return proximityAttenuation.Evaluate(1f - normalized);
        }

        /// <summary>
        /// 判断玩家当前是否被隔离
        /// </summary>
        public bool IsPlayerIsolated(string playerId)
        {
            return isolatedPlayerIds.Contains(playerId);
        }

        #endregion

        #region NoiseReductionFilter (Built-in Mock)

        /// <summary>
        /// 内置降噪滤波器模拟实现。
        /// 提供简易噪声门（Noise Gate）作为降噪的占位实现，
        /// 正式版应由平台原生 DSP 替换。
        /// </summary>
        private sealed class NoiseReductionFilter : IAudioFilter
        {
            private readonly NoiseReductionConfig config;
            private float envelope;

            public string FilterName => "Noise Reduction (Built-in)";

            public bool IsEnabled { get; set; } = true;

            public NoiseReductionFilter(NoiseReductionConfig config)
            {
                this.config = config;
            }

            public void Process(float[] samples, int sampleRate, int channels)
            {
                if (samples == null || samples.Length == 0) return;

                float releaseCoeff = Mathf.Exp(-1f / (config.noiseGateRelease * sampleRate));

                for (int i = 0; i < samples.Length; i++)
                {
                    float abs = Mathf.Abs(samples[i]);
                    envelope = Mathf.Max(abs, envelope * releaseCoeff);

                    float gate = envelope > config.noiseGateThreshold ? 1f : 0f;
                    float suppression = 1f - config.suppressionLevel * (1f - gate);

                    samples[i] *= suppression;
                }
            }

            public void Reset()
            {
                envelope = 0f;
            }
        }

        #endregion

        #region EchoCancellationFilter (Built-in Mock)

        /// <summary>
        /// 内置回声消除滤波器模拟实现。
        /// 提供简易衰减作为回声消除的占位实现，
        /// 正式版应由平台原生 AEC（如 WebRTC AEC3）替换。
        /// </summary>
        private sealed class EchoCancellationFilter : IAudioFilter
        {
            private readonly EchoCancellationConfig config;
            private readonly Queue<float> delayLine;
            private int delaySamples;

            public string FilterName => "Echo Cancellation (Built-in)";

            public bool IsEnabled { get; set; } = true;

            public EchoCancellationFilter(EchoCancellationConfig config, int sampleRate)
            {
                this.config = config;
                delaySamples = Mathf.Max(1, sampleRate * config.echoTailLength / 1000);
                delayLine = new Queue<float>(delaySamples);

                for (int i = 0; i < delaySamples; i++)
                {
                    delayLine.Enqueue(0f);
                }
            }

            public void Process(float[] samples, int sampleRate, int channels)
            {
                if (samples == null || samples.Length == 0) return;

                for (int i = 0; i < samples.Length; i++)
                {
                    float delayedEcho = delayLine.Dequeue();
                    float cancelled = samples[i] - delayedEcho * config.cancellationLevel;
                    delayLine.Enqueue(samples[i]);
                    samples[i] = cancelled;
                }
            }

            public void Reset()
            {
                delayLine.Clear();
                for (int i = 0; i < delaySamples; i++)
                {
                    delayLine.Enqueue(0f);
                }
            }
        }

        #endregion
    }
}
