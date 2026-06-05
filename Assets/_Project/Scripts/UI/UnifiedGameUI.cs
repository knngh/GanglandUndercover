using UnityEngine;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// E6: Unified styling constants and helpers for all Canvas/IMGUI HUD, meeting, vote,
    /// and result screens across the project.
    /// </summary>
    public static class UnifiedGameUI
    {
        // ── Faction / role colors ───────────────────────────────────────
        public static readonly Color PoliceBlue        = HexColor(0x1a5fb4);
        public static readonly Color GangRed           = HexColor(0xc01c28);
        public static readonly Color UndercoverPurple  = HexColor(0x9141ac);
        public static readonly Color MoleGrey          = HexColor(0x5e5c64);

        // ── Neutral / UI chrome ─────────────────────────────────────────
        public static readonly Color PanelBackground = new Color(0f, 0f, 0f, 0.85f);
        public static readonly Color PanelBorder     = new Color(0.25f, 0.25f, 0.28f, 0.92f);
        public static readonly Color TextPrimary     = new Color(0.92f, 0.9f,  0.82f, 1f);
        public static readonly Color TextMuted       = new Color(0.63f, 0.68f, 0.66f, 1f);
        public static readonly Color AccentInfo      = PoliceBlue;
        public static readonly Color AccentDanger    = GangRed;
        public static readonly Color AccentSpecial   = UndercoverPurple;

        // ── Status indicator colors (color‑blind safe labels in addition) ──
        public static readonly Color StatusGreen  = new Color(0.18f, 0.72f, 0.38f, 1f);
        public static readonly Color StatusYellow = new Color(0.86f, 0.65f, 0.13f, 1f);
        public static readonly Color StatusRed    = GangRed;

        // ── Font‑size constants ─────────────────────────────────────────
        public const int FontTitle = 28;
        public const int FontBody  = 18;
        public const int FontSmall = 13;
        public const int FontTiny  = 10;

        // ── Corner radius (mimics rounded‑corner feel via border images) ──
        public const float PanelCornerRadius = 8f;

        // ── Helpers ─────────────────────────────────────────────────────
        public static Color HexColor(uint hex)
        {
            return new Color(
                ((hex >> 16) & 0xFF) / 255f,
                ((hex >>  8) & 0xFF) / 255f,
                ( hex        & 0xFF) / 255f,
                1f);
        }

        /// <summary>
        /// Returns a color‑blind safe status label for a given role or state.
        /// These text labels accompany or replace purely‑colored indicators.
        /// </summary>
        public static string StatusLabel(string context)
        {
            switch (context)
            {
                case "police":       return "[警]";
                case "gang":         return "[匪]";
                case "undercover":   return "[卧]";
                case "mole":         return "[鼠]";
                case "dead":         return "[死]";
                case "silenced":     return "[禁]";
                case "power_out":    return "停电";
                case "lockdown":     return "封锁";
                case "disconnected": return "[断]";
                case "ready":        return "[就绪]";
                default:             return "";
            }
        }

        /// <summary>
        /// Returns the faction color for the given role name.
        /// </summary>
        public static Color RoleColor(string role)
        {
            switch (role?.ToLowerInvariant())
            {
                case "police":      return PoliceBlue;
                case "gang":        return GangRed;
                case "undercover":  return UndercoverPurple;
                case "mole":        return MoleGrey;
                default:            return TextMuted;
            }
        }

        /// <summary>
        /// Apply panel‑background style to an existing UnityEngine.UI.Image.
        /// </summary>
        public static void StylePanel(UnityEngine.UI.Image image)
        {
            if (image == null) return;
            image.color = PanelBackground;
        }

        /// <summary>
        /// Applies consistent body‑text style to a UnityEngine.UI.Text element.
        /// </summary>
        public static void StyleBodyText(UnityEngine.UI.Text text)
        {
            if (text == null) return;
            text.fontSize = FontBody;
            text.color = TextPrimary;
        }

        /// <summary>
        /// Common resolution list used by SimpleResolutionGuard.
        /// </summary>
        public static readonly Vector2Int[] CommonResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
        };

        /// <summary>
        /// Returns a user‑friendly resolution string (e.g. "1920x1080").
        /// </summary>
        public static string ResolutionLabel(int width, int height)
        {
            int w = Mathf.Max(1, width);
            int h = Mathf.Max(1, height);
            return $"{w}x{h}";
        }
    }
}
