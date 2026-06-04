using GanglandUndercover.Core;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 双向渗透信息可见性系统。
    ///
    /// 警察阵营（Undercover + Police）：
    ///   可见所有黑帮成员（Gang + 伪装成 Gang 的 Undercover）
    ///   + 所有警察成员（Police + 伪装成 Police 的 Mole）
    ///   但无法区分哪些"警察"是线人，哪些"黑帮"是卧底。
    ///
    /// 黑帮阵营（Gang + Mole）：
    ///   可见所有警察成员（Police + 伪装成 Police 的 Mole）
    ///   + 所有黑帮成员（Gang + 伪装成 Gang 的 Undercover）
    ///   但无法区分哪些"黑帮"是卧底，哪些"警察"是线人。
    /// </summary>
    public static class SocialKnowledge
    {
        /// <summary>
        /// 角色的真实阵营归属。
        /// </summary>
        public static Faction GetRealFaction(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Police:
                case SocialRole.Undercover:
                    return Faction.Police;
                case SocialRole.Gang:
                case SocialRole.Mole:
                    return Faction.Gang;
                default:
                    return Faction.Police;
            }
        }

        /// <summary>
        /// 角色对外公开的可见身份。伪装角色返回其伪装身份。
        /// Undercover（警察卧底）→ 对外表现为 Gang
        /// Mole（黑帮线人）→ 对外表现为 Police
        /// </summary>
        public static SocialRole GetVisibleRole(SocialRole realRole)
        {
            switch (realRole)
            {
                case SocialRole.Undercover:
                    return SocialRole.Gang;
                case SocialRole.Mole:
                    return SocialRole.Police;
                default:
                    return realRole;
            }
        }

        /// <summary>
        /// 从观察者视角，目标角色表现为哪个阵营。
        /// 观察者只能看到目标的表面身份，无法穿透伪装。
        /// </summary>
        public static Faction GetPerceivedFaction(SocialRole observerRealRole, SocialRole targetRealRole)
        {
            Faction observerFaction = GetRealFaction(observerRealRole);
            SocialRole visible = GetVisibleRole(targetRealRole);
            Faction visibleFaction = GetRealFaction(visible);

            // 无论观察者阵营如何，都只能看到目标的表面阵营
            return visibleFaction;
        }

        /// <summary>
        /// 角色名称（含伪装标记）。
        /// </summary>
        public static string DescribeRole(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:
                    return "黑帮成员";
                case SocialRole.Police:
                    return "警察成员";
                case SocialRole.Undercover:
                    return "警察卧底（黑帮伪装）";
                case SocialRole.Mole:
                    return "黑帮线人（警察伪装）";
                default:
                    return "未知";
            }
        }
    }
}