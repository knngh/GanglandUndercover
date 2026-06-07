using System;
using UnityEngine;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 设置数据结构 — 所有可持久化游戏设置的可序列化容器。
    /// 作为 SettingsManager 的数据载体，提供 Apply() 批量生效机制。
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        // ══════════════════════════════════════════════════════
        // 音频设置
        // ══════════════════════════════════════════════════════

        [SerializeField, Range(0f, 1f)]
        private float _masterVolume = 0.8f;
        public float MasterVolume
        {
            get => _masterVolume;
            set => _masterVolume = Mathf.Clamp01(value);
        }

        [SerializeField, Range(0f, 1f)]
        private float _sfxVolume = 0.8f;
        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        /// <summary>F2: BGM 独立音量轨</summary>
        [SerializeField, Range(0f, 1f)]
        private float _bgmVolume = 0.7f;
        public float BgmVolume
        {
            get => _bgmVolume;
            set => _bgmVolume = Mathf.Clamp01(value);
        }

        /// <summary>F4: 窗口模式 0=全屏, 1=窗口, 2=无边框</summary>
        [SerializeField, Range(0, 2)]
        private int _windowMode;
        public int WindowMode
        {
            get => _windowMode;
            set => _windowMode = Mathf.Clamp(value, 0, 2);
        }

        /// <summary>F2: 分辨率预设 0=自动,1=720p,2=1080p,3=1440p</summary>
        [SerializeField, Range(0, 3)]
        private int _resolutionPreset;

        [SerializeField, Range(0f, 1f)]
        private float _voiceChatVolume = 0.7f;
        public float VoiceChatVolume
        {
            get => _voiceChatVolume;
            set => _voiceChatVolume = Mathf.Clamp01(value);
        }

        [SerializeField, Range(0f, 1f)]
        private float _micSensitivity = 0.5f;
        public float MicSensitivity
        {
            get => _micSensitivity;
            set => _micSensitivity = Mathf.Clamp01(value);
        }

        // ══════════════════════════════════════════════════════
        // 画面设置
        // ══════════════════════════════════════════════════════

        /// <summary>分辨率索引，0=使用当前原生分辨率，后续按键入分辨率数组</summary>
        [SerializeField]
        private int _resolutionIndex;
        public int ResolutionIndex
        {
            get => _resolutionIndex;
            set => _resolutionIndex = Mathf.Max(0, value);
        }

        public int ResolutionPreset
        {
            get => _resolutionPreset;
            set => _resolutionPreset = Mathf.Clamp(value, 0, 3);
        }

        /// <summary>分辨率预设显示名称</summary>
        public static readonly string[] ResolutionPresetNames = { "自动", "1280×720", "1920×1080", "2560×1440" };

        /// <summary>窗口模式显示名称</summary>
        public static readonly string[] WindowModeNames = { "全屏", "窗口", "无边框" };

        [SerializeField]
        private bool _isFullscreen = true;
        public bool IsFullscreen
        {
            get => _isFullscreen;
            set => _isFullscreen = value;
        }

        /// <summary>画质预设：0=低，1=中，2=高，3=极致</summary>
        [SerializeField, Range(0, 3)]
        private int _qualityPreset = 2;
        public int QualityPreset
        {
            get => _qualityPreset;
            set => _qualityPreset = Mathf.Clamp(value, 0, 3);
        }

        /// <summary>帧率上限：30/60/120/144/240，0 为无限制</summary>
        [SerializeField]
        private int _frameRateCap = 60;
        public int FrameRateCap
        {
            get => _frameRateCap;
            set => _frameRateCap = value < 0 ? 0 : value;
        }

        /// <summary>垂直同步</summary>
        [SerializeField]
        private bool _vSync = true;
        public bool VSync
        {
            get => _vSync;
            set => _vSync = value;
        }

        // ══════════════════════════════════════════════════════
        // 游戏设置
        // ══════════════════════════════════════════════════════

        /// <summary>语言代码：zh-CN / en-US / ja-JP / ko-KR</summary>
        [SerializeField]
        private string _language = "zh-CN";
        public string Language
        {
            get => _language;
            set => _language = !string.IsNullOrEmpty(value) ? value : _language;
        }

        /// <summary>语音模式：0=按键说话，1=自由发言，2=禁用</summary>
        [SerializeField, Range(0, 2)]
        private int _voiceMode;
        public int VoiceMode
        {
            get => _voiceMode;
            set => _voiceMode = Mathf.Clamp(value, 0, 2);
        }

        [SerializeField, Range(0.1f, 10f)]
        private float _mouseSensitivity = 1f;
        public float MouseSensitivity
        {
            get => _mouseSensitivity;
            set => _mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f);
        }

        /// <summary>色盲模式：0=关闭，1=红绿色盲，2=蓝黄色盲，3=全色盲</summary>
        [SerializeField, Range(0, 3)]
        private int _colorBlindMode;
        public int ColorBlindMode
        {
            get => _colorBlindMode;
            set => _colorBlindMode = Mathf.Clamp(value, 0, 3);
        }

        /// <summary>按键绑定字典（KeyCode 序列化时以字符串存储）</summary>
        [SerializeField]
        private KeyBindingData _keyBindings = new KeyBindingData();
        public KeyBindingData KeyBindings => _keyBindings;

        // ══════════════════════════════════════════════════════
        // 批量生效
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 将所有设置批量应用到引擎运行时。
        /// 由 SettingsManager.ApplySettings() 调用。
        /// </summary>
        public void Apply()
        {
            ApplyAudio();
            ApplyGraphics();
            ApplyGame();
            ApplyAccessibility();
        }

        private void ApplyAccessibility()
        {
            SettingsManager.InvokeColorBlindModeChanged(_colorBlindMode);
        }

        // ─── 音频应用 ───────────────────────────────────────────
        private void ApplyAudio()
        {
            // AudioMixer 参数通过 SettingsManager 设置，此处分发事件
            SettingsManager.InvokeVolumeChanged(
                _masterVolume,
                _sfxVolume,
                _bgmVolume,
                _voiceChatVolume,
                _micSensitivity
            );
        }

        // ─── 画面应用 ───────────────────────────────────────────
        private void ApplyGraphics()
        {
            // 全屏模式
            Screen.fullScreen = _windowMode == 0 || _windowMode == 2;
            Screen.fullScreenMode = _windowMode switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.Windowed,
                2 => FullScreenMode.FullScreenWindow, // 无边框
                _ => FullScreenMode.ExclusiveFullScreen
            };

            // 画质预设
            QualitySettings.SetQualityLevel(_qualityPreset, applyExpensiveChanges: true);

            // 帧率上限
            Application.targetFrameRate = _frameRateCap > 0 ? _frameRateCap : -1;

            // 垂直同步
            QualitySettings.vSyncCount = _vSync ? 1 : 0;

            // 分辨率（预设优先）
            ApplyResolutionPreset();
        }

        /// <summary>F2: 按预设值设定分辨率</summary>
        private void ApplyResolutionPreset()
        {
            switch (_resolutionPreset)
            {
                case 1: // 720p
                    Screen.SetResolution(1280, 720, Screen.fullScreen);
                    break;
                case 2: // 1080p
                    Screen.SetResolution(1920, 1080, Screen.fullScreen);
                    break;
                case 3: // 1440p
                    Screen.SetResolution(2560, 1440, Screen.fullScreen);
                    break;
                case 0: // 自动 → 回退到旧 ResolutionIndex 行为
                default:
                    ApplyResolution();
                    break;
            }
        }

        private void ApplyResolution()
        {
            if (_resolutionIndex <= 0) return;

            Resolution[] resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0) return;

            int idx = Mathf.Clamp(_resolutionIndex - 1, 0, resolutions.Length - 1);
            Resolution target = resolutions[idx];
            Screen.SetResolution(target.width, target.height, _isFullscreen);
        }

        // ─── 游戏应用 ───────────────────────────────────────────
        private void ApplyGame()
        {
            // 鼠标灵敏度
            // 实际由 SettingsManager 事件体系传递至玩家控制器
        }

        // ══════════════════════════════════════════════════════
        // 默认值工厂
        // ══════════════════════════════════════════════════════

        public static SettingsData CreateDefault() => new SettingsData();
    }

    /// <summary>
    /// 按键绑定数据结构 — 用字符串键值对序列化自定义按键映射。
    /// </summary>
    [Serializable]
    public sealed class KeyBindingData
    {
        /// <summary>前进</summary>
        public KeyCode MoveForward = KeyCode.W;
        /// <summary>后退</summary>
        public KeyCode MoveBackward = KeyCode.S;
        /// <summary>左移</summary>
        public KeyCode MoveLeft = KeyCode.A;
        /// <summary>右移</summary>
        public KeyCode MoveRight = KeyCode.D;

        /// <summary>交互/使用</summary>
        public KeyCode Interact = KeyCode.E;
        /// <summary>蹲下</summary>
        public KeyCode Crouch = KeyCode.LeftControl;
        /// <summary>冲刺</summary>
        public KeyCode Sprint = KeyCode.LeftShift;
        /// <summary>跳跃</summary>
        public KeyCode Jump = KeyCode.Space;

        /// <summary>Tab 菜单</summary>
        public KeyCode TabMenu = KeyCode.Tab;
        /// <summary>地图</summary>
        public KeyCode Map = KeyCode.M;
        /// <summary>背包</summary>
        public KeyCode Inventory = KeyCode.I;
        /// <summary>报告尸体</summary>
        public KeyCode Report = KeyCode.R;
        /// <summary>踢人投票</summary>
        public KeyCode KickVote = KeyCode.K;

        /// <summary>语音聊天按键</summary>
        public KeyCode PushToTalk = KeyCode.V;

        /// <summary>
        /// 按 action 名称获取绑定按键。
        /// </summary>
        public KeyCode GetBinding(string action)
        {
            switch (action)
            {
                case "MoveForward":    return MoveForward;
                case "MoveBackward":   return MoveBackward;
                case "MoveLeft":       return MoveLeft;
                case "MoveRight":      return MoveRight;
                case "Interact":       return Interact;
                case "Crouch":         return Crouch;
                case "Sprint":         return Sprint;
                case "Jump":           return Jump;
                case "TabMenu":        return TabMenu;
                case "Map":            return Map;
                case "Inventory":      return Inventory;
                case "Report":         return Report;
                case "KickVote":       return KickVote;
                case "PushToTalk":     return PushToTalk;
                default:               return KeyCode.None;
            }
        }

        /// <summary>
        /// 设置指定 action 的按键绑定。
        /// </summary>
        public void SetBinding(string action, KeyCode key)
        {
            if (key == KeyCode.None) return;

            switch (action)
            {
                case "MoveForward":  MoveForward  = key; break;
                case "MoveBackward": MoveBackward = key; break;
                case "MoveLeft":     MoveLeft     = key; break;
                case "MoveRight":    MoveRight    = key; break;
                case "Interact":     Interact     = key; break;
                case "Crouch":       Crouch       = key; break;
                case "Sprint":       Sprint       = key; break;
                case "Jump":         Jump         = key; break;
                case "TabMenu":      TabMenu      = key; break;
                case "Map":          Map          = key; break;
                case "Inventory":    Inventory    = key; break;
                case "Report":       Report       = key; break;
                case "KickVote":     KickVote     = key; break;
                case "PushToTalk":   PushToTalk   = key; break;
            }
        }
    }
}
