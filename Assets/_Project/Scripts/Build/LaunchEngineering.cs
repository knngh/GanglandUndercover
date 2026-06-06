using System;
using UnityEngine;

namespace GanglandUndercover.Build
{
    /// <summary>
    /// Phase 6: Steamworks SDK 集成入口。
    /// 对接 Steam 成就、排行榜、好友邀请。
    /// 使用条件编译 #if STEAMWORKS_ENABLED 隔离依赖。
    /// </summary>
    public static class SteamIntegration
    {
        public static bool IsAvailable => false; // TODO: 接入 Steamworks.NET 后改为条件编译

        /// <summary>解锁 Steam 成就。</summary>
        public static void UnlockAchievement(string achievementId)
        {
#if STEAMWORKS_ENABLED
            // SteamUserStats.SetAchievement(achievementId);
            // SteamUserStats.StoreStats();
#endif
            Debug.Log($"[Steam] Achievement unlocked: {achievementId}");
        }

        /// <summary>更新 Steam 排行榜分数。</summary>
        public static void UpdateLeaderboard(string boardId, int score)
        {
#if STEAMWORKS_ENABLED
            // SteamUserStats.UploadLeaderboardScore(boardId, Steamworks.ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, score, null, 0);
#endif
            Debug.Log($"[Steam] Leaderboard '{boardId}' score: {score}");
        }

        /// <summary>邀请好友加入游戏。</summary>
        public static void InviteFriend(string friendId)
        {
#if STEAMWORKS_ENABLED
            // SteamFriends.InviteUserToGame(new Steamworks.CSteamID(ulong.Parse(friendId)), "");
#endif
            Debug.Log($"[Steam] Friend invite: {friendId}");
        }

        /// <summary>Steam 成就列表。</summary>
        public static class Achievements
        {
            public const string FirstWin = "FIRST_WIN";
            public const string TenWins = "TEN_WINS";
            public const string PerfectDetective = "PERFECT_DETECTIVE";     // 10局0误指
            public const string ColdBlooded = "COLD_BLOODED";              // 单局3杀
            public const string MasterOfDisguise = "MASTER_OF_DISGUISE";   // 卧底5连胜
            public const string SpeedRunner = "SPEED_RUNNER";              // 5分钟内获胜
            public const string Pacifist = "PACIFIST";                     // 10局0杀获胜
            public const string AllProfessions = "ALL_PROFESSIONS";        // 所有职业各玩1次
        }
    }

    /// <summary>
    /// Phase 6: 性能基线配置。
    /// 运行时采集和日志输出。
    /// </summary>
    public class PerformanceBaseline : MonoBehaviour
    {
        [Header("Targets")]
        public int TargetFPS = 60;
        public int MaxMemoryMB = 2048;
        public int MaxNetworkKbps = 50;

        private float _fpsTimer;
        private int _fpsFrameCount;
        private float _currentFPS;

        private void Update()
        {
            _fpsTimer += Time.unscaledDeltaTime;
            _fpsFrameCount++;
            if (_fpsTimer >= 1f)
            {
                _currentFPS = _fpsFrameCount / _fpsTimer;
                _fpsTimer = 0f;
                _fpsFrameCount = 0;
            }
        }

        /// <summary>当前帧率。</summary>
        public float CurrentFPS => _currentFPS;

        /// <summary>当前内存使用 (MB)。</summary>
        public float CurrentMemoryMB => System.GC.GetTotalMemory(false) / (1024f * 1024f);

        /// <summary>检查是否达标。</summary>
        public bool IsPerformanceOK()
        {
            bool fpsOk = _currentFPS >= TargetFPS * 0.9f || _currentFPS == 0; // 0=刚启动
            bool memOk = CurrentMemoryMB <= MaxMemoryMB;
            return fpsOk && memOk;
        }

        /// <summary>输出性能报告。</summary>
        public string PerformanceReport()
        {
            return $"FPS: {_currentFPS:F1}/{TargetFPS} | Mem: {CurrentMemoryMB:F0}/{MaxMemoryMB}MB | " +
                   (IsPerformanceOK() ? "✓ OK" : "⚠ DEGRADED");
        }
    }
}
