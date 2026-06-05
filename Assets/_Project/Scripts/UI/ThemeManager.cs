using UnityEngine;

using GanglandUndercover.Core;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 主题管理器 — Among Us 太空主题完整视觉体系。
    /// 所有 UI 控制器引用此类的静态属性，保证全局视觉一致性。
    /// </summary>
    public static class ThemeManager
    {
        // ══════════════════════════════════════════════════════
        // 太空主题配色
        // ══════════════════════════════════════════════════════

        /// <summary>主背景 — 深空蓝黑 #0a0a1a</summary>
        public static Color BackgroundDark  => Hex("#0a0a1a");

        /// <summary>面板背景 — 半透明深蓝 #1a1a3e</summary>
        public static Color PanelBackground  => Hex("#1a1a3e");

        /// <summary>按钮主色 — 霓虹蓝 #3a7bd5</summary>
        public static Color ButtonPrimary    => Hex("#3a7bd5");

        /// <summary>按钮悬停 — 亮霓虹蓝</summary>
        public static Color ButtonHover      => Hex("#5a9bf5");

        /// <summary>按钮按下 — 暗霓虹蓝</summary>
        public static Color ButtonPressed    => Hex("#1a4b8a");

        /// <summary>危险色 — 猩红 #ff4444</summary>
        public static Color DangerRed        => Hex("#ff4444");

        /// <summary>安全色 — 翠绿 #44ff44</summary>
        public static Color SafeGreen        => Hex("#44ff44");

        /// <summary>警告色 — 琥珀 #ffaa00</summary>
        public static Color WarningAmber     => Hex("#ffaa00");

        /// <summary>Mole 线人色 — 青色 #00cccc</summary>
        public static Color MoleTeal         => Hex("#00cccc");

        /// <summary>霓虹青 — 边框/描边 #1aeeff</summary>
        public static Color NeonCyan         => Hex("#1aeeff");

        /// <summary>标题金 — 尊贵金 #f0e68c</summary>
        public static Color TitleGold        => Hex("#f0e68c");

        /// <summary>文字主色 — 暖白</summary>
        public static Color TextPrimary      => Hex("#f5f2eb");

        /// <summary>文字次要色</summary>
        public static Color TextMuted        => Hex("#7a7a8a");

        /// <summary>文字禁用色</summary>
        public static Color TextDisabled     => Hex("#4a4a5a");

        /// <summary>叠加层 — 半透明遮罩</summary>
        public static Color OverlayDark      => new Color(0.02f, 0.02f, 0.06f, 0.85f);

        /// <summary>卡片背景</summary>
        public static Color CardBackground   => Hex("#12122e");

        /// <summary>输入框背景</summary>
        public static Color InputBackground  => Hex("#0d0d24");

        /// <summary>分隔线</summary>
        public static Color Divider          => Hex("#2a2a4e");

        // ══════════════════════════════════════════════════════
        // 阵营色
        // ══════════════════════════════════════════════════════

        public static Color GangRed          => Hex("#ff4444");
        public static Color UndercoverBlue   => Hex("#3a7bd5");
        public static Color PoliceGray       => Hex("#8a8a9a");
        public static Color GhostTint        => new Color(0.6f, 0.65f, 0.7f, 0.35f);

        public static Color GetFactionColor(Faction faction) => faction switch
        {
            Faction.Gang       => GangRed,
            Faction.Undercover => UndercoverBlue,
            Faction.Police     => PoliceGray,
            Faction.Mole       => MoleTeal,
            _                  => TextMuted
        };

        public static Color GetRoleColor(SocialRole role) => role switch
        {
            SocialRole.Gang       => GangRed,
            SocialRole.Undercover => UndercoverBlue,
            SocialRole.Police     => PoliceGray,
            SocialRole.Mole       => MoleTeal,
            _                     => TextMuted
        };

        // ══════════════════════════════════════════════════════
        // 字体大小体系（标题28/正文18/小字14）
        // ══════════════════════════════════════════════════════

        public static int FontSizeTitle       => 28;
        public static int FontSizeSubtitle    => 22;
        public static int FontSizeHeader      => 20;
        public static int FontSizeBody        => 18;
        public static int FontSizeButton      => 18;
        public static int FontSizeSmall       => 14;
        public static int FontSizeFooter      => 13; // F4: 最小可读字号 13px (WCAG AA 720p)

        // ══════════════════════════════════════════════════════
        // 圆角（面板12px / 按钮8px）
        // ══════════════════════════════════════════════════════

        /// <summary>面板圆角半径</summary>
        public const float CornerRadiusPanel  = 12f;

        /// <summary>按钮圆角半径</summary>
        public const float CornerRadiusButton = 8f;

        /// <summary>卡片圆角半径</summary>
        public const float CornerRadiusCard   = 6f;

        // ══════════════════════════════════════════════════════
        // 间距
        // ══════════════════════════════════════════════════════

        public const float ButtonHeight       = 52f;
        public const float ButtonWidthSmall   = 180f;
        public const float ButtonWidthMedium  = 260f;
        public const float ButtonWidthLarge   = 320f;
        public const float PanelPadding       = 24f;
        public const float ElementGap         = 16f;
        public const float SectionGap         = 32f;

        // ══════════════════════════════════════════════════════
        // 动画
        // ══════════════════════════════════════════════════════

        public const float SlideInDuration    = 0.5f;
        public const float FadeInDuration     = 0.45f;
        public const float FlipDuration       = 0.6f;
        public const float BarFillDuration    = 1.2f;
        public const float ParticleSpeed      = 0.3f;

        // ══════════════════════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════════════════════

        public static Color ScaleColor(Color c, float factor) =>
            new Color(
                Mathf.Clamp01(c.r * factor),
                Mathf.Clamp01(c.g * factor),
                Mathf.Clamp01(c.b * factor),
                c.a);

        public static Color WithAlpha(Color c, float a) =>
            new Color(c.r, c.g, c.b, Mathf.Clamp01(a));

        // ══════════════════════════════════════════════════════
        // 色盲模式（M9.5 可访问性）
        // ══════════════════════════════════════════════════════

        /// <summary>当前色盲模式（0=关,1=红绿色盲,2=蓝黄色盲,3=全色盲）</summary>
        public static int ColorBlindMode { get; set; } = 0;

        /// <summary>色盲安全阵营色 — 不用颜色区分，加图标后缀</summary>
        public static Color GetFactionColorSafe(Faction faction) => ColorBlindMode switch
        {
            0 => GetFactionColor(faction),
            1 => faction switch
            {
                Faction.Gang       => Hex("#a6a6a6"),   // 红绿色盲：Gang → 灰红
                Faction.Undercover => Hex("#4a4adf"),   // 蓝紫
                Faction.Police     => Hex("#bfbf40"),   // 黄绿
                Faction.Mole       => Hex("#40bfbf"),   // 青
                _                  => TextMuted
            },
            2 => faction switch
            {
                Faction.Gang       => Hex("#c7c744"),   // 蓝黄色盲：Gang → 黄绿
                Faction.Undercover => Hex("#7644c7"),   // 紫
                Faction.Police     => Hex("#44c776"),   // 绿青
                Faction.Mole       => Hex("#c7444a"),   // 红
                _                  => TextMuted
            },
            _ => new Color(0.65f, 0.65f, 0.65f, 1f)  // 全色盲：全部灰度
        };

        /// <summary>获取阵营图标后缀（色盲模式下追加到显示名后面）</summary>
        public static string GetFactionIconSuffix(Faction faction) => ColorBlindMode switch
        {
            0 => string.Empty,
            1 or 2 => faction switch
            {
                Faction.Gang       => " ●",   // 圆点
                Faction.Undercover => " ▲",   // 方块
                Faction.Police     => " ▲",   // 三角
                Faction.Mole       => " ◆",   // 菱形
                _                    => " ○"
            },
            _ => faction switch
            {
                Faction.Gang       => " G",
                Faction.Undercover => " U",
                Faction.Police     => " P",
                Faction.Mole       => " M",
                _                    => " ?"
            }
        };

        /// <summary>色盲安全投票条颜色（嫌疑值条不用纯红/纯绿）</summary>
        public static Color GetSusicionBarColor(float suspicionNormalized) => ColorBlindMode switch
        {
            0 => Color.Lerp(SafeGreen, DangerRed, suspicionNormalized),
            1 or 2 => Color.Lerp(Hex("#4a9a40"), Hex("#a69a40"), suspicionNormalized),  // 绿→黄
            _ => new Color(0.3f + suspicionNormalized * 0.5f, 0.3f, 0.3f, 1f)  // 灰度
        };

        private static Color Hex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            return new Color(r, g, b, 1f);
        }
    }
}
