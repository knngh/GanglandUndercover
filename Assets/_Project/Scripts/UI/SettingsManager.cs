using System;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 设置管理器 — 单例，管理所有游戏设置的加载、保存、应用。
    /// 使用 PlayerPrefs 持久化，通过事件体系通知各子系统变更。
    /// 由 PrototypeBootstrap 或 SettingMenu 初始化。
    /// </summary>
    public sealed class SettingsManager : MonoBehaviour
    {
        // ─── 单例 ───────────────────────────────────────
        public static SettingsManager Instance { get; private set; }

        // ─── 配置键常量 ─────────────────────────────────────────
        private const string PrefKeyPrefix = "GL_Settings_";

        private const string KeyMasterVolume     = PrefKeyPrefix + "MasterVolume";
        private const string KeySfxVolume        = PrefKeyPrefix + "SfxVolume";
        private const string KeyBgmVolume        = PrefKeyPrefix + "BgmVolume";      // F2
        private const string KeyVoiceChatVolume  = PrefKeyPrefix + "VoiceChatVolume";
        private const string KeyMicSensitivity   = PrefKeyPrefix + "MicSensitivity";
        private const string KeyResolutionIndex  = PrefKeyPrefix + "ResolutionIndex";
        private const string KeyResolutionPreset = PrefKeyPrefix + "ResolutionPreset"; // F2
        private const string KeyIsFullscreen     = PrefKeyPrefix + "IsFullscreen";
        private const string KeyWindowMode       = PrefKeyPrefix + "WindowMode";        // F2
        private const string KeyQualityPreset    = PrefKeyPrefix + "QualityPreset";
        private const string KeyFrameRateCap     = PrefKeyPrefix + "FrameRateCap";
        private const string KeyVSync            = PrefKeyPrefix + "VSync";
        private const string KeyLanguage         = PrefKeyPrefix + "Language";
        private const string KeyVoiceMode        = PrefKeyPrefix + "VoiceMode";
        private const string KeyMouseSensitivity = PrefKeyPrefix + "MouseSensitivity";
        private const string KeyColorBlindMode  = PrefKeyPrefix + "ColorBlindMode";
        private const string KeyBindPrefix       = PrefKeyPrefix + "Key_";

        // ─── 运行时数据 ─────────────────────────────────────────
        private SettingsData _current;
        public SettingsData Current => _current;

        // ─── 事件 ───────────────────────────────────────
        /// <summary>音量变更事件（主音量, 音效, BGM, 语音聊天, 麦克风灵敏度）</summary>
        public static event Action<float, float, float, float, float> OnVolumeChanged;

        /// <summary>画面设置变更事件</summary>
        public event Action<SettingsData> OnGraphicsChanged;

        /// <summary>语言变更事件</summary>
        public event Action<string> OnLanguageChanged;

        /// <summary>按键绑定变更事件（action名称, 新按键）</summary>
        public event Action<string, KeyCode> OnKeyRebound;

        /// <summary>任意设置变更事件</summary>
        public event Action OnAnySettingChanged;

        /// <summary>色盲模式变更事件（新模式）</summary>
        public event Action<int> OnColorBlindModeChanged;

        // ─── 生命周期 ─────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Load();
        }

        // ══════════════════════════════════════════════════════
        // 持久化 — 加载 / 保存 / 重置
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 从 PlayerPrefs 加载所有设置。首次运行时使用默认值。
        /// </summary>
        public void Load()
        {
            _current = SettingsData.CreateDefault();

            _current.MasterVolume    = PlayerPrefs.GetFloat(KeyMasterVolume,    _current.MasterVolume);
            _current.SfxVolume       = PlayerPrefs.GetFloat(KeySfxVolume,       _current.SfxVolume);
            _current.BgmVolume       = PlayerPrefs.GetFloat(KeyBgmVolume,       _current.BgmVolume);        // F2
            _current.VoiceChatVolume = PlayerPrefs.GetFloat(KeyVoiceChatVolume, _current.VoiceChatVolume);
            _current.MicSensitivity  = PlayerPrefs.GetFloat(KeyMicSensitivity,  _current.MicSensitivity);

            _current.ResolutionIndex  = PlayerPrefs.GetInt(KeyResolutionIndex,  _current.ResolutionIndex);
            _current.ResolutionPreset = PlayerPrefs.GetInt(KeyResolutionPreset, _current.ResolutionPreset); // F2
            _current.IsFullscreen     = PlayerPrefs.GetInt(KeyIsFullscreen,  _current.IsFullscreen ? 1 : 0) == 1;
            _current.WindowMode       = PlayerPrefs.GetInt(KeyWindowMode,    _current.WindowMode);           // F2
            _current.QualityPreset   = PlayerPrefs.GetInt(KeyQualityPreset, _current.QualityPreset);
            _current.FrameRateCap    = PlayerPrefs.GetInt(KeyFrameRateCap,  _current.FrameRateCap);
            _current.VSync           = PlayerPrefs.GetInt(KeyVSync,         _current.VSync ? 1 : 0) == 1;

            _current.Language         = PlayerPrefs.GetString(KeyLanguage,         _current.Language);
            _current.VoiceMode        = PlayerPrefs.GetInt(KeyVoiceMode,           _current.VoiceMode);
            _current.MouseSensitivity = PlayerPrefs.GetFloat(KeyMouseSensitivity,  _current.MouseSensitivity);
            _current.ColorBlindMode  = PlayerPrefs.GetInt(KeyColorBlindMode,  _current.ColorBlindMode);

            LoadKeyBindings();

            // 同步到 Localization 快捷访问
            SyncLocalizationLanguage();
            ThemeManager.ColorBlindMode = _current.ColorBlindMode;
        }

        /// <summary>
        /// 将所有设置写入 PlayerPrefs 并立即生效。
        /// </summary>
        public void Save()
        {
            SaveAllPrefs();
            PlayerPrefs.Save();
            _current.Apply();
            OnAnySettingChanged?.Invoke();
        }

        /// <summary>
        /// 重置所有设置为默认值。
        /// </summary>
        public void ResetToDefault()
        {
            _current = SettingsData.CreateDefault();
            Save();
        }

        // ─── 按键绑定序列化 ─────────────────────────────────────
        private void LoadKeyBindings()
        {
            string[] actions =
            {
                "MoveForward", "MoveBackward", "MoveLeft", "MoveRight",
                "Interact", "Crouch", "Sprint", "Jump",
                "TabMenu", "Map", "Inventory", "Report", "KickVote", "PushToTalk"
            };

            foreach (string action in actions)
            {
                string stored = PlayerPrefs.GetString(KeyBindPrefix + action, string.Empty);
                if (!string.IsNullOrEmpty(stored) && Enum.TryParse(stored, out KeyCode key))
                {
                    _current.KeyBindings.SetBinding(action, key);
                }
            }
        }

        private void SaveKeyBindings()
        {
            string[] actions =
            {
                "MoveForward", "MoveBackward", "MoveLeft", "MoveRight",
                "Interact", "Crouch", "Sprint", "Jump",
                "TabMenu", "Map", "Inventory", "Report", "KickVote", "PushToTalk"
            };

            foreach (string action in actions)
            {
                KeyCode key = _current.KeyBindings.GetBinding(action);
                PlayerPrefs.SetString(KeyBindPrefix + action, key.ToString());
            }
        }

        private void SaveAllPrefs()
        {
            PlayerPrefs.SetFloat(KeyMasterVolume,    _current.MasterVolume);
            PlayerPrefs.SetFloat(KeySfxVolume,       _current.SfxVolume);
            PlayerPrefs.SetFloat(KeyBgmVolume,       _current.BgmVolume);        // F2
            PlayerPrefs.SetFloat(KeyVoiceChatVolume, _current.VoiceChatVolume);
            PlayerPrefs.SetFloat(KeyMicSensitivity,  _current.MicSensitivity);

            PlayerPrefs.SetInt(KeyResolutionIndex,  _current.ResolutionIndex);
            PlayerPrefs.SetInt(KeyResolutionPreset, _current.ResolutionPreset);  // F2
            PlayerPrefs.SetInt(KeyWindowMode,       _current.WindowMode);        // F2
            PlayerPrefs.SetInt(KeyIsFullscreen,     _current.IsFullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyQualityPreset,   _current.QualityPreset);
            PlayerPrefs.SetInt(KeyFrameRateCap,    _current.FrameRateCap);
            PlayerPrefs.SetInt(KeyVSync,           _current.VSync ? 1 : 0);

            PlayerPrefs.SetString(KeyLanguage,         _current.Language);
            PlayerPrefs.SetInt(KeyVoiceMode,        _current.VoiceMode);
            PlayerPrefs.SetFloat(KeyMouseSensitivity,  _current.MouseSensitivity);
            PlayerPrefs.SetInt(KeyColorBlindMode,  _current.ColorBlindMode);

            SaveKeyBindings();
        }

        // ══════════════════════════════════════════════════════
        // 单个设置读写 API
        // ══════════════════════════════════════════════════════

        // ─── 音频 ───────────────────────────────────────────────
        public void SetMasterVolume(float value)
        {
            _current.MasterVolume = value;
            PlayerPrefs.SetFloat(KeyMasterVolume, value);
            InvokeVolumeChanged();
            OnAnySettingChanged?.Invoke();
        }

        public void SetSfxVolume(float value)
        {
            _current.SfxVolume = value;
            PlayerPrefs.SetFloat(KeySfxVolume, value);
            InvokeVolumeChanged();
            OnAnySettingChanged?.Invoke();
        }

        /// <summary>F2: 设置 BGM 音量</summary>
        public void SetBgmVolume(float value)
        {
            _current.BgmVolume = value;
            PlayerPrefs.SetFloat(KeyBgmVolume, value);
            InvokeVolumeChanged();
            OnAnySettingChanged?.Invoke();
        }

        public void SetVoiceChatVolume(float value)
        {
            _current.VoiceChatVolume = value;
            PlayerPrefs.SetFloat(KeyVoiceChatVolume, value);
            InvokeVolumeChanged();
            OnAnySettingChanged?.Invoke();
        }

        public void SetMicSensitivity(float value)
        {
            _current.MicSensitivity = value;
            PlayerPrefs.SetFloat(KeyMicSensitivity, value);
            InvokeVolumeChanged();
            OnAnySettingChanged?.Invoke();
        }

        // ─── 画面 ───────────────────────────────────────────────
        public void SetResolutionIndex(int index)
        {
            _current.ResolutionIndex = index;
            PlayerPrefs.SetInt(KeyResolutionIndex, index);
            _current.Apply();
            OnGraphicsChanged?.Invoke(_current);
            OnAnySettingChanged?.Invoke();
        }

        public void SetFullscreen(bool value)
        {
            _current.IsFullscreen = value;
            PlayerPrefs.SetInt(KeyIsFullscreen, value ? 1 : 0);
            _current.Apply();
            OnGraphicsChanged?.Invoke(_current);
            OnAnySettingChanged?.Invoke();
        }

        /// <summary>F2: 设置窗口模式 0=全屏,1=窗口,2=无边框</summary>
        public void SetWindowMode(int mode)
        {
            _current.WindowMode = mode;
            PlayerPrefs.SetInt(KeyWindowMode, mode);
            _current.Apply();
            OnGraphicsChanged?.Invoke(_current);
            OnAnySettingChanged?.Invoke();
        }

        /// <summary>F2: 设置分辨率预设 0=自动,1=720p,2=1080p,3=1440p</summary>
        public void SetResolutionPreset(int preset)
        {
            _current.ResolutionPreset = preset;
            PlayerPrefs.SetInt(KeyResolutionPreset, preset);
            _current.Apply();
            OnGraphicsChanged?.Invoke(_current);
            OnAnySettingChanged?.Invoke();
        }

        public void SetQualityPreset(int preset)
        {
            _current.QualityPreset = preset;
            PlayerPrefs.SetInt(KeyQualityPreset, preset);
            _current.Apply();
            OnGraphicsChanged?.Invoke(_current);
            OnAnySettingChanged?.Invoke();
        }

        public void SetFrameRateCap(int cap)
        {
            _current.FrameRateCap = cap;
            PlayerPrefs.SetInt(KeyFrameRateCap, cap);
            _current.Apply();
            OnGraphicsChanged?.Invoke(_current);
            OnAnySettingChanged?.Invoke();
        }

        public void SetVSync(bool value)
        {
            _current.VSync = value;
            PlayerPrefs.SetInt(KeyVSync, value ? 1 : 0);
            _current.Apply();
            OnGraphicsChanged?.Invoke(_current);
            OnAnySettingChanged?.Invoke();
        }

        // ─── 游戏 ───────────────────────────────────────────────
        public void SetLanguage(string lang)
        {
            _current.Language = lang;
            PlayerPrefs.SetString(KeyLanguage, lang);
            SyncLocalizationLanguage();
            OnLanguageChanged?.Invoke(lang);
            OnAnySettingChanged?.Invoke();
        }

        public void SetVoiceMode(int mode)
        {
            _current.VoiceMode = mode;
            PlayerPrefs.SetInt(KeyVoiceMode, mode);
            OnAnySettingChanged?.Invoke();
        }

        public void SetMouseSensitivity(float value)
        {
            _current.MouseSensitivity = value;
            PlayerPrefs.SetFloat(KeyMouseSensitivity, value);
            OnAnySettingChanged?.Invoke();
        }

        public void RebindKey(string action, KeyCode newKey)
        {
            if (newKey == KeyCode.None) return;

            _current.KeyBindings.SetBinding(action, newKey);
            PlayerPrefs.SetString(KeyBindPrefix + action, newKey.ToString());
            PlayerPrefs.Save();
            OnKeyRebound?.Invoke(action, newKey);
            OnAnySettingChanged?.Invoke();
        }

        // ─── 可访问性 ───────────────────────────────────────────────
        public void SetColorBlindMode(int mode)
        {
            _current.ColorBlindMode = mode;
            PlayerPrefs.SetInt(KeyColorBlindMode, mode);
            ThemeManager.ColorBlindMode = mode;
            InvokeColorBlindModeChanged(mode);
            OnAnySettingChanged?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 静态事件触发入口
        // ══════════════════════════════════════════════════════

        /// <summary>内部调用：音量变更事件触发器</summary>
        internal static void InvokeVolumeChanged(
            float master, float sfx, float bgm, float voice, float mic)
        {
            OnVolumeChanged?.Invoke(master, sfx, bgm, voice, mic);
        }

        private void InvokeVolumeChanged()
        {
            OnVolumeChanged?.Invoke(
                _current.MasterVolume,
                _current.SfxVolume,
                _current.BgmVolume,
                _current.VoiceChatVolume,
                _current.MicSensitivity
            );
        }

        /// <summary>内部调用：色盲模式变更事件触发器</summary>
        internal static void InvokeColorBlindModeChanged(int mode)
        {
            // 更新 ThemeManager 静态属性（已由 SetColorBlindMode 设置）
            ThemeManager.ColorBlindMode = mode;
            Instance?.OnColorBlindModeChanged?.Invoke(mode);
        }

        /// <summary>同步 Language 到 Localization 快捷属性</summary>
        private void SyncLocalizationLanguage()
        {
            Localization.CurrentLanguage = _current.Language == "zh-CN"
                ? GameLanguage.Chinese
                : GameLanguage.English;
        }

        // ══════════════════════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════════════════════

        /// <summary>获取可用分辨率列表</summary>
        public static Resolution[] GetAvailableResolutions() => Screen.resolutions;

        /// <summary>获取分辨率显示名称列表</summary>
        public static string[] GetResolutionNames()
        {
            Resolution[] res = Screen.resolutions;
            if (res == null || res.Length == 0)
                return new[] { "当前分辨率" };

            string[] names = new string[res.Length];
            for (int i = 0; i < res.Length; i++)
            {
                names[i] = $"{res[i].width} × {res[i].height} @ {res[i].refreshRateRatio.value:F0}Hz";
            }
            return names;
        }

        /// <summary>获取帧率上限选项列表</summary>
        public static readonly int[] FrameRateOptions = { 0, 30, 60, 120, 144, 240 };

        /// <summary>获取帧率上限显示名称</summary>
        public static string GetFrameRateName(int cap)
        {
            return cap <= 0 ? "无限制" : $"{cap} FPS";
        }

        /// <summary>获取画质预设名称列表</summary>
        public static readonly string[] QualityPresetNames = { "低", "中", "高", "极致" };

        /// <summary>获取聊天发送模式名称列表。字段名沿用 VoiceMode 以兼容既有存档。</summary>
        public static readonly string[] VoiceModeNames = { "按键发送", "自由发送", "禁用" };
    }
}
