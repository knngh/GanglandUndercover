using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 设置 UI 绑定辅助 — 提供 Slider / Toggle / Dropdown / Button 与 SettingsManager 的
    /// 标准双向绑定方法，支持实时预览。挂载到设置菜单面板上使用。
    /// </summary>
    public sealed class SettingsUIHelper : MonoBehaviour
    {
        // ─── 引用 ───────────────────────────────────────────────
        [Header("音频")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private TMP_Text _masterVolumeLabel;

        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private TMP_Text _sfxVolumeLabel;

        [SerializeField] private Slider _voiceChatVolumeSlider;
        [SerializeField] private TMP_Text _voiceChatVolumeLabel;

        [SerializeField] private Slider _micSensitivitySlider;
        [SerializeField] private TMP_Text _micSensitivityLabel;

        [Header("画面")]
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private TMP_Dropdown _qualityDropdown;
        [SerializeField] private TMP_Dropdown _frameRateDropdown;
        [SerializeField] private Toggle _vSyncToggle;

        [Header("游戏")]
        [SerializeField] private TMP_Dropdown _languageDropdown;
        [SerializeField] private TMP_Dropdown _voiceModeDropdown;
        [SerializeField] private Slider _mouseSensitivitySlider;
        [SerializeField] private TMP_Text _mouseSensitivityLabel;

        [Header("按键绑定")]
        [SerializeField] private List<KeyBindingEntry> _keyBindingEntries;

        // ─── 状态 ───────────────────────────────────────────────
        private SettingsManager _settings;
        private bool _initialized;

        // ─── 生命周期 ───────────────────────────────────────────
        private void OnEnable()
        {
            Initialize();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Initialize()
        {
            if (_initialized) return;

            _settings = SettingsManager.Instance;
            if (_settings == null)
            {
                Debug.LogWarning("[SettingsUIHelper] SettingsManager.Instance 为空，延迟至 OnEnable 重试。");
                return;
            }

            BuildDropdownOptions();
            Subscribe();
            _initialized = true;
        }

        // ══════════════════════════════════════════════════════
        // 下拉选项构建
        // ══════════════════════════════════════════════════════

        private void BuildDropdownOptions()
        {
            BuildResolutionDropdown();
            BuildQualityDropdown();
            BuildFrameRateDropdown();
            BuildLanguageDropdown();
            BuildVoiceModeDropdown();
        }

        private void BuildResolutionDropdown()
        {
            PopulateDropdown(_resolutionDropdown, SettingsManager.GetResolutionNames());
        }

        private void BuildQualityDropdown()
        {
            PopulateDropdown(_qualityDropdown, SettingsManager.QualityPresetNames);
        }

        private void BuildFrameRateDropdown()
        {
            string[] names = new string[SettingsManager.FrameRateOptions.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = SettingsManager.GetFrameRateName(SettingsManager.FrameRateOptions[i]);
            }
            PopulateDropdown(_frameRateDropdown, names);
        }

        private void BuildLanguageDropdown()
        {
            PopulateDropdown(_languageDropdown, new[] { "简体中文", "English", "日本語", "한국어" });
        }

        private void BuildVoiceModeDropdown()
        {
            PopulateDropdown(_voiceModeDropdown, SettingsManager.VoiceModeNames);
        }

        private static void PopulateDropdown(TMP_Dropdown dropdown, string[] options)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
        }

        // ══════════════════════════════════════════════════════
        // 订阅与取消
        // ══════════════════════════════════════════════════════

        private void Subscribe()
        {
            if (_settings == null) return;

            // ─── Audio Sliders ──────────────────────────────────
            BindSlider(_masterVolumeSlider,    v => _settings.SetMasterVolume(v),    label: _masterVolumeLabel,    format: "F0", unit: "%");
            BindSlider(_sfxVolumeSlider,       v => _settings.SetSfxVolume(v),       label: _sfxVolumeLabel,       format: "F0", unit: "%");
            BindSlider(_voiceChatVolumeSlider, v => _settings.SetVoiceChatVolume(v), label: _voiceChatVolumeLabel, format: "F0", unit: "%");
            BindSlider(_micSensitivitySlider,  v => _settings.SetMicSensitivity(v),  label: _micSensitivityLabel,  format: "F0", unit: "%");

            // ─── Graphics ───────────────────────────────────────
            BindDropdown(_resolutionDropdown, idx => _settings.SetResolutionIndex(idx));
            BindToggle(_fullscreenToggle,     v => _settings.SetFullscreen(v));
            BindDropdown(_qualityDropdown,    idx => _settings.SetQualityPreset(idx));
            BindFrameRateDropdown();
            BindToggle(_vSyncToggle,          v => _settings.SetVSync(v));

            // ─── Game ───────────────────────────────────────────
            BindLanguageDropdown();
            BindDropdown(_voiceModeDropdown,  idx => _settings.SetVoiceMode(idx));
            BindSlider(_mouseSensitivitySlider, v => _settings.SetMouseSensitivity(v), label: _mouseSensitivityLabel, format: "F1");
        }

        private void Unsubscribe()
        {
            RemoveSliderListener(_masterVolumeSlider);
            RemoveSliderListener(_sfxVolumeSlider);
            RemoveSliderListener(_voiceChatVolumeSlider);
            RemoveSliderListener(_micSensitivitySlider);
            RemoveSliderListener(_mouseSensitivitySlider);

            RemoveDropdownListener(_resolutionDropdown);
            RemoveDropdownListener(_qualityDropdown);
            RemoveDropdownListener(_frameRateDropdown);
            RemoveDropdownListener(_languageDropdown);
            RemoveDropdownListener(_voiceModeDropdown);

            RemoveToggleListener(_fullscreenToggle);
            RemoveToggleListener(_vSyncToggle);
        }

        // ══════════════════════════════════════════════════════
        // Slider 标准绑定
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 绑定 Slider 到设置回调，并驱动关联的文本标签实时显示值。
        /// </summary>
        /// <param name="slider">目标 Slider</param>
        /// <param name="onValueChanged">值变更回调</param>
        /// <param name="label">可选的 TMP_Text 标签</param>
        /// <param name="format">数值格式化串（如 "F0"）</param>
        /// <param name="unit">显示单位（如 "%"）</param>
        private void BindSlider(Slider slider, Action<float> onValueChanged,
            TMP_Text label = null, string format = "F0", string unit = "")
        {
            if (slider == null) return;

            slider.onValueChanged.AddListener(value =>
            {
                onValueChanged?.Invoke(value);
                UpdateSliderLabel(label, value, format, unit);
            });

            // 初始化标签
            UpdateSliderLabel(label, slider.value, format, unit);
        }

        private static void UpdateSliderLabel(TMP_Text label, float value,
            string format, string unit)
        {
            if (label == null) return;

            if (unit == "%")
            {
                label.text = $"{value * 100f:0}%";
            }
            else
            {
                label.text = value.ToString(format) + unit;
            }
        }

        // ══════════════════════════════════════════════════════
        // Toggle 标准绑定
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 绑定 Toggle 到设置回调（实时生效）。
        /// </summary>
        private void BindToggle(Toggle toggle, Action<bool> onValueChanged)
        {
            if (toggle == null) return;
            toggle.onValueChanged.AddListener(value => onValueChanged?.Invoke(value));
        }

        // ══════════════════════════════════════════════════════
        // Dropdown 标准绑定
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 绑定 Dropdown 到设置回调（实时生效）。
        /// </summary>
        private void BindDropdown(TMP_Dropdown dropdown, Action<int> onValueChanged)
        {
            if (dropdown == null) return;
            dropdown.onValueChanged.AddListener(idx => onValueChanged?.Invoke(idx));
        }

        private void BindFrameRateDropdown()
        {
            if (_frameRateDropdown == null) return;
            _frameRateDropdown.onValueChanged.AddListener(idx =>
            {
                if (idx >= 0 && idx < SettingsManager.FrameRateOptions.Length)
                {
                    _settings.SetFrameRateCap(SettingsManager.FrameRateOptions[idx]);
                }
            });
        }

        private void BindLanguageDropdown()
        {
            if (_languageDropdown == null) return;
            _languageDropdown.onValueChanged.AddListener(idx =>
            {
                string lang = idx switch
                {
                    0 => "zh-CN",
                    1 => "en-US",
                    2 => "ja-JP",
                    3 => "ko-KR",
                    _ => "zh-CN"
                };
                _settings.SetLanguage(lang);
            });
        }

        // ══════════════════════════════════════════════════════
        // 取消监听工具
        // ══════════════════════════════════════════════════════

        private static void RemoveSliderListener(Slider slider)
        {
            if (slider != null) slider.onValueChanged.RemoveAllListeners();
        }

        private static void RemoveToggleListener(Toggle toggle)
        {
            if (toggle != null) toggle.onValueChanged.RemoveAllListeners();
        }

        private static void RemoveDropdownListener(TMP_Dropdown dropdown)
        {
            if (dropdown != null) dropdown.onValueChanged.RemoveAllListeners();
        }

        // ══════════════════════════════════════════════════════
        // 刷新 — 从 SettingsManager 同步到 UI
        // ══════════════════════════════════════════════════════

        /// <summary>从 SettingsManager 重新读取所有值并刷新 UI。</summary>
        public void RefreshAll()
        {
            if (_settings == null) return;

            SettingsData d = _settings.Current;

            SetSliderSafe(_masterVolumeSlider,    d.MasterVolume);
            SetSliderSafe(_sfxVolumeSlider,       d.SfxVolume);
            SetSliderSafe(_voiceChatVolumeSlider, d.VoiceChatVolume);
            SetSliderSafe(_micSensitivitySlider,  d.MicSensitivity);
            SetSliderSafe(_mouseSensitivitySlider, d.MouseSensitivity);

            SetDropdownSafe(_resolutionDropdown, d.ResolutionIndex);
            SetToggleSafe(_fullscreenToggle,     d.IsFullscreen);
            SetDropdownSafe(_qualityDropdown,    d.QualityPreset);
            SetFrameRateDropdown(d.FrameRateCap);
            SetToggleSafe(_vSyncToggle,          d.VSync);

            SetLanguageDropdown(d.Language);
            SetDropdownSafe(_voiceModeDropdown,  d.VoiceMode);

            // 刷新按键标签
            RefreshKeyBindingLabels();
        }

        private void SetFrameRateDropdown(int cap)
        {
            if (_frameRateDropdown == null) return;
            int idx = Array.IndexOf(SettingsManager.FrameRateOptions, cap);
            if (idx < 0) idx = Array.IndexOf(SettingsManager.FrameRateOptions, 60);
            SetDropdownSafe(_frameRateDropdown, idx);
        }

        private void SetLanguageDropdown(string lang)
        {
            if (_languageDropdown == null) return;
            int idx = lang switch
            {
                "en-US" => 1,
                "ja-JP" => 2,
                "ko-KR" => 3,
                _       => 0
            };
            SetDropdownSafe(_languageDropdown, idx);
        }

        // ─── 安全赋值 ───────────────────────────────────────────
        private static void SetSliderSafe(Slider s, float v)
        {
            if (s != null) s.SetValueWithoutNotify(v);
        }

        private static void SetToggleSafe(Toggle t, bool v)
        {
            if (t != null) t.SetIsOnWithoutNotify(v);
        }

        private static void SetDropdownSafe(TMP_Dropdown d, int idx)
        {
            if (d != null && idx >= 0 && idx < d.options.Count)
                d.SetValueWithoutNotify(idx);
        }

        // ══════════════════════════════════════════════════════
        // 按键绑定 UI
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 刷新所有按键绑定标签显示。
        /// </summary>
        public void RefreshKeyBindingLabels()
        {
            if (_keyBindingEntries == null) return;

            foreach (KeyBindingEntry entry in _keyBindingEntries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ActionName)) continue;

                KeyCode key = _settings.Current.KeyBindings.GetBinding(entry.ActionName);
                if (entry.Label != null)
                {
                    entry.Label.text = KeyCodeToDisplayName(key);
                }
            }
        }

        /// <summary>
        /// 开始监听某个按键绑定项的重新绑定（由 UI Button 调用）。
        /// </summary>
        public void StartRebind(KeyBindingEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ActionName)) return;
            StartCoroutine(WaitForKeyPress(entry));
        }

        private System.Collections.IEnumerator WaitForKeyPress(KeyBindingEntry entry)
        {
            if (entry.Label != null) entry.Label.text = "...";

            // 等待一帧以跳过当前按键
            yield return null;

            while (!Input.anyKeyDown)
            {
                yield return null;
            }

            KeyCode pressed = DetectPressedKey();
            if (pressed != KeyCode.None)
            {
                _settings.RebindKey(entry.ActionName, pressed);
                if (entry.Label != null) entry.Label.text = KeyCodeToDisplayName(pressed);
            }
            else
            {
                // 恢复原标签
                RefreshKeyBindingLabels();
            }
        }

        private static KeyCode DetectPressedKey()
        {
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(k))
                {
                    // 过滤 Escape 取消
                    if (k == KeyCode.Escape) return KeyCode.None;
                    return k;
                }
            }
            return KeyCode.None;
        }

        private static string KeyCodeToDisplayName(KeyCode key)
        {
            return key switch
            {
                KeyCode.LeftControl  => "LCtrl",
                KeyCode.RightControl => "RCtrl",
                KeyCode.LeftShift    => "LShift",
                KeyCode.RightShift   => "RShift",
                KeyCode.LeftAlt      => "LAlt",
                KeyCode.RightAlt     => "RAlt",
                KeyCode.Return       => "Enter",
                KeyCode.KeypadEnter  => "NumpadEnter",
                KeyCode.Alpha0       => "0",
                KeyCode.Alpha1       => "1",
                KeyCode.Alpha2       => "2",
                KeyCode.Alpha3       => "3",
                KeyCode.Alpha4       => "4",
                KeyCode.Alpha5       => "5",
                KeyCode.Alpha6       => "6",
                KeyCode.Alpha7       => "7",
                KeyCode.Alpha8       => "8",
                KeyCode.Alpha9       => "9",
                _                    => key.ToString()
            };
        }

        // ══════════════════════════════════════════════════════
        // 按键绑定入口数据结构
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 按键绑定条目 — 在 Inspector 中配置 action 名称与显示标签。
        /// </summary>
        [Serializable]
        public sealed class KeyBindingEntry
        {
            /// <summary>action 名称（如 "MoveForward"）</summary>
            public string ActionName;

            /// <summary>显示当前按键的 TMP_Text</summary>
            public TMP_Text Label;

            /// <summary>触发重新绑定的 Button</summary>
            public Button RebindButton;
        }
    }
}
