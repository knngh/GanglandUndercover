namespace GanglandUndercover.Core
{
    public enum GamePhase
    {
        RoleSelect,
        PlayerTurn,
        AiTurn,
        Meeting,
        /// <summary>AI 暂停（紧急任务期间）。</summary>
        Paused,
        GameOver
    }
}
