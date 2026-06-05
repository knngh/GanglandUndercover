using GanglandUndercover.Core;
using GanglandUndercover.Online;
using GanglandUndercover.UI;

namespace GanglandUndercover.Accessibility
{
    /// <summary>
    /// F4 可访问性辅助类 — 提供阵营文字标签、破坏状态文字标签、
    /// 小地图形状标记、字号/对比度工具、灰度模式检查。
    ///
    /// 设计原则：关键信息不依赖单一颜色通道，始终提供「形状+文字+颜色」三重编码。
    /// </summary>
    public static class AccessibilityHelper
    {
        // ══════════════════════════════════════════════════════
        // 最小可读字号
        // ══════════════════════════════════════════════════════

        /// <summary>720p 分辨率下的最小可读字号（像素）</summary>
        public const int MinFontSize = 13;

        /// <summary>
        /// 强制字号不小于 MinFontSize。在创建 UI Text 时调用。
        /// </summary>
        public static int ClampFontSize(int requested) => requested < MinFontSize ? MinFontSize : requested;

        // ══════════════════════════════════════════════════════
        // 对比度检查
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 正文对比度最低要求 4.5:1（WCAG AA 标准）。
        /// 返回 true 表示满足可访问性要求。
        /// </summary>
        public static bool MeetsContrastMinimum(UnityEngine.Color text, UnityEngine.Color background)
        {
            float contrast = CalculateContrastRatio(text, background);
            return contrast >= 4.5f;
        }

        /// <summary>
        /// 计算两个颜色的 WCAG 对比度。
        /// </summary>
        public static float CalculateContrastRatio(UnityEngine.Color c1, UnityEngine.Color c2)
        {
            float l1 = RelativeLuminance(c1);
            float l2 = RelativeLuminance(c2);

            float lighter = l1 > l2 ? l1 : l2;
            float darker  = l1 > l2 ? l2 : l1;

            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static float RelativeLuminance(UnityEngine.Color c)
        {
            float Linearize(float channel)
            {
                return channel <= 0.04045f
                    ? channel / 12.92f
                    : UnityEngine.Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
            }

            return 0.2126f * Linearize(c.r) + 0.7152f * Linearize(c.g) + 0.0722f * Linearize(c.b);
        }

        // ══════════════════════════════════════════════════════
        // 阵营文字标签（色盲/灰度模式下替代颜色区分）
        // ══════════════════════════════════════════════════════

        /// <summary>获取阵营文字标签（中文/英文自适应）</summary>
        public static string GetFactionTextLabel(Faction faction) => faction switch
        {
            Faction.Police     => Localization.CurrentLanguage == GameLanguage.Chinese ? "警蓝"   : "Police",
            Faction.Gang       => Localization.CurrentLanguage == GameLanguage.Chinese ? "匪红"   : "Gang",
            Faction.Undercover => Localization.CurrentLanguage == GameLanguage.Chinese ? "卧绿"   : "UC",
            Faction.Mole       => Localization.CurrentLanguage == GameLanguage.Chinese ? "内黄"   : "Mole",
            _                  => "?"
        };

        /// <summary>获取阵营标签的背景色（对比度安全）</summary>
        public static UnityEngine.Color GetFactionLabelBg(Faction faction) => faction switch
        {
            Faction.Police     => ThemeManager.UndercoverBlue,
            Faction.Gang       => ThemeManager.GangRed,
            Faction.Undercover => ThemeManager.SafeGreen,
            Faction.Mole       => ThemeManager.MoleTeal,
            _                  => ThemeManager.TextMuted
        };

        // ══════════════════════════════════════════════════════
        // 小地图玩家形状标记（□△○◇☆）
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 获取阵营对应的 Unicode 形状字符（用于小地图标记）。
        /// 色盲模式开启时自动附加形状，正常模式返回空字符串（纯颜色即可）。
        /// </summary>
        public static string GetMinimapShape(Faction faction) => ThemeManager.ColorBlindMode switch
        {
            0 => string.Empty,
            _ => faction switch
            {
                Faction.Police     => "□",
                Faction.Gang       => "△",
                Faction.Undercover => "○",
                Faction.Mole       => "◇",
                _                  => "☆"
            }
        };

        /// <summary>
        /// 判断是否应该使用文字标签辅助（色盲或灰度模式下为 true）。
        /// </summary>
        public static bool ShouldUseTextLabels => ThemeManager.ColorBlindMode > 0;

        // ══════════════════════════════════════════════════════
        // 破坏状态文字标签
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 获取破坏类型的文字标签。
        /// 色盲模式下始终返回文字标签，正常模式下返回空（依赖 UI 动画颜色）。
        /// </summary>
        public static string GetSabotageTextLabel(SabotageType type) => type switch
        {
            SabotageType.Blackout        => Localization.CurrentLanguage == GameLanguage.Chinese ? "⚡停电"  : "⚡ Blackout",
            SabotageType.Lockdown        => Localization.CurrentLanguage == GameLanguage.Chinese ? "🔒封锁"  : "🔒 Lockdown",
            SabotageType.Communications  => Localization.CurrentLanguage == GameLanguage.Chinese ? "📡断讯"  : "📡 Comms Down",
            SabotageType.EvidenceLeak    => Localization.CurrentLanguage == GameLanguage.Chinese ? "📋泄密"  : "📋 Evidence Leak",
            SabotageType.PatrolAlert     => Localization.CurrentLanguage == GameLanguage.Chinese ? "🚨警戒"  : "🚨 Patrol Alert",
            SabotageType.CriticalO2      => Localization.CurrentLanguage == GameLanguage.Chinese ? "🫁缺氧"  : "🫁 O2 Critical",
            SabotageType.CriticalReactor => Localization.CurrentLanguage == GameLanguage.Chinese ? "☢️反应堆" : "☢️ Reactor",
            _                            => "?"
        };

        /// <summary>
        /// 获取破坏标签的背景色（色盲安全配色）。
        /// </summary>
        public static UnityEngine.Color GetSabotageLabelColor(SabotageType type) => type switch
        {
            SabotageType.Blackout        => new UnityEngine.Color(0.2f, 0.2f, 0.2f, 1f),
            SabotageType.Lockdown        => new UnityEngine.Color(0.6f, 0.1f, 0.05f, 1f),
            SabotageType.Communications  => new UnityEngine.Color(0.8f, 0.55f, 0.1f, 1f),
            SabotageType.EvidenceLeak    => new UnityEngine.Color(0.7f, 0.2f, 0.5f, 1f),
            SabotageType.PatrolAlert     => new UnityEngine.Color(0.8f, 0.35f, 0.1f, 1f),
            SabotageType.CriticalO2      => new UnityEngine.Color(0.1f, 0.5f, 0.7f, 1f),
            SabotageType.CriticalReactor => new UnityEngine.Color(0.15f, 0.7f, 0.15f, 1f),
            _                            => new UnityEngine.Color(0.4f, 0.4f, 0.4f, 1f)
        };

        // ══════════════════════════════════════════════════════
        // 灰度测试辅助
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 灰度测试检查单（由编辑器工具调用）。
        /// 返回当前各项可访问功能的通过/未通过状态。
        /// </summary>
        public static string[] GrayscaleChecklist()
        {
            bool colorBlindOn = ShouldUseTextLabels;
            return new string[]
            {
                $"会议 UI 区分存活/出局：{(colorBlindOn ? "✅ 文字标签" : "⚠️ 灰度下纯色难分")}",
                $"任务状态 完成/未完成/破坏：{(colorBlindOn ? "✅ 文字标签" : "⚠️ 需依赖颜色区分")}",
                $"小地图标记区分阵营：{(colorBlindOn ? "✅ 形状+文字" : "⚠️ 仅形状符号")}",
                $"阵营标签：{GetFactionTextLabel(Faction.Police)}/{GetFactionTextLabel(Faction.Gang)}/{GetFactionTextLabel(Faction.Undercover)}/{GetFactionTextLabel(Faction.Mole)}",
                $"破坏标签：{GetSabotageTextLabel(SabotageType.Blackout)}/{GetSabotageTextLabel(SabotageType.Lockdown)}/{GetSabotageTextLabel(SabotageType.Communications)}",
                $"全局色盲模式：{(colorBlindOn ? "已开启" : "关闭 → 建议开启以测试")}",
            };
        }
    }
}
