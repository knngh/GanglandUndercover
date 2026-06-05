using UnityEngine;
using GanglandUndercover.Online;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// E2 职业视觉调色板。
    /// 7 职业各一套主色+辅助色，用于角色 sprite 着色和 UI 展示。
    /// 警匪冷暖对比：警方冷色系 / 黑帮暖色系 / 卧底紫色系。
    /// </summary>
    public static class ProfessionPalette
    {
        public static Color MainColor(OnlineProfession prof) => prof switch
        {
            OnlineProfession.Inspector      => Hex("#2d6fba"),   // 警蓝
            OnlineProfession.Tech           => Hex("#1a9eaa"),   // 青
            OnlineProfession.Forensics      => Hex("#2e8b57"),   // 法医绿
            OnlineProfession.Enforcer       => Hex("#c0392b"),   // 赤红
            OnlineProfession.Fixer          => Hex("#7f8c8d"),   // 灰黑
            OnlineProfession.UndercoverAgent=> Hex("#8e44ad"),   // 卧底紫
            OnlineProfession.Driver         => Hex("#d4a017"),   // 车手黄
            OnlineProfession.Mole           => Hex("#c0392b"),   // 内鬼伪装红
            _                               => Hex("#95a5a6"),
        };

        public static Color AccentColor(OnlineProfession prof) => prof switch
        {
            OnlineProfession.Inspector      => Hex("#5dade2"),
            OnlineProfession.Tech           => Hex("#48c9b0"),
            OnlineProfession.Forensics      => Hex("#58d68d"),
            OnlineProfession.Enforcer       => Hex("#e74c3c"),
            OnlineProfession.Fixer          => Hex("#bdc3c7"),
            OnlineProfession.UndercoverAgent=> Hex("#af7ac5"),
            OnlineProfession.Driver         => Hex("#f1c40f"),
            OnlineProfession.Mole           => Hex("#e74c3c"),
            _                               => Hex("#d5dbdb"),
        };

        /// <summary>尸体颜色（所有职业统一为暗红灰色）</summary>
        public static Color CorpseColor => new Color(0.35f, 0.18f, 0.18f, 0.7f);

        /// <summary>鬼魂颜色</summary>
        public static Color GhostColor => new Color(0.5f, 0.5f, 0.55f, 0.35f);

        /// <summary>职业图标字符（单字标识）</summary>
        public static string IconChar(OnlineProfession prof) => prof switch
        {
            OnlineProfession.Inspector       => "探",
            OnlineProfession.Tech            => "技",
            OnlineProfession.Forensics       => "鉴",
            OnlineProfession.Enforcer        => "打",
            OnlineProfession.Fixer           => "清",
            OnlineProfession.UndercoverAgent => "卧",
            OnlineProfession.Driver          => "车",
            OnlineProfession.Mole            => "鬼",
            _                                => "?",
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
