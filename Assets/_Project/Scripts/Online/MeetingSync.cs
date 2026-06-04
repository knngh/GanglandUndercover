using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机会议同步：报告尸体触发 → 投票 → 淘汰同步。
    /// 包裹 OnlineMatchController 中的会议流程，提供生命周期事件与状态验证。
    ///
    /// 联机会话周期：
    ///   ReportBody / EmergencyBell → BeginMeeting → [Intro/讨论] → Vote → Resolve → [淘汰] → EndMeeting
    /// 所有阶段变更由 BroadcastSnapshot 同步，本类提供侦听回调。
    /// </summary>
    public sealed class MeetingSync
    {
        public const float MinVoteIntervalSeconds = 1.5f;
        public const float MaxVoteDurationSeconds = 55f;

        private readonly Action<string> addCaseLog;
        private readonly Dictionary<ulong, ulong> pendingVotes = new Dictionary<ulong, ulong>();
        private string lastReason = string.Empty;
        private string lastOutcome = string.Empty;
        private int meetingCount;

        public event Action<string, string> MeetingStarted;    // (reason, phase)
        public event Action<ulong, ulong> VoteCast;             // (voterId, targetId)
        public event Action<string, ulong> VoteResolved;         // (outcome, ejectedClientId)
        public event Action MeetingEnded;

        public string LastReason => lastReason;
        public string LastOutcome => lastOutcome;
        public int MeetingCount => meetingCount;
        public bool IsActive { get; private set; }

        public MeetingSync(Action<string> addCaseLog)
        {
            this.addCaseLog = addCaseLog ?? (_ => { });
        }

        // ------ 生命周期 (Host 调用) ------

        public void Begin(string reason, OnlineMatchPhase currentPhase)
        {
            IsActive = true;
            lastReason = reason;
            lastOutcome = string.Empty;
            pendingVotes.Clear();
            meetingCount++;
            addCaseLog($"MeetingSync #{meetingCount}: 会议开始 — {reason}。");
            MeetingStarted?.Invoke(reason, currentPhase.ToString());
        }

        public void RegisterVote(ulong voterClientId, ulong targetClientId)
        {
            if (!IsActive) return;
            pendingVotes[voterClientId] = targetClientId;
            VoteCast?.Invoke(voterClientId, targetClientId);
            addCaseLog($"MeetingSync: {voterClientId} 投票 {targetClientId}。");
        }

        /// <summary>
        /// 会议结束：回包最多票的玩家，或 SkipVoteTarget 表示平票/跳过。
        /// </summary>
        public void Resolve(ulong ejectedClientId, bool tied, Dictionary<ulong, int> tally)
        {
            if (!IsActive) return;

            if (ejectedClientId == SkipVoteTarget || tied)
            {
                lastOutcome = "投票无结果，无人出局。";
            }
            else
            {
                lastOutcome = $"玩家 {ejectedClientId} 被投出局。";
            }

            IsActive = false;
            VoteResolved?.Invoke(lastOutcome, ejectedClientId);
            addCaseLog($"MeetingSync #{meetingCount}: {lastOutcome}");
        }

        public void End()
        {
            IsActive = false;
            MeetingEnded?.Invoke();
            addCaseLog($"MeetingSync #{meetingCount}: 会议结束。");
        }

        // ------ 状态查询 ------

        public bool HasAllVoted(int alivePlayerCount)
        {
            return pendingVotes.Count >= alivePlayerCount;
        }

        public static ulong SkipVoteTarget => ulong.MaxValue;

        // ------ 统计 ------

        public int VoteCount() => pendingVotes.Count;
    }
}
