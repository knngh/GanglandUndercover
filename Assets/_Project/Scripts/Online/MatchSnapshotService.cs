using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 游戏快照服务：负责捕获和恢复完整对局状态（供主机迁移使用）。
    /// 从 OnlineMatchController 中提取，以降低控制器复杂度。
    /// </summary>
    internal sealed class MatchSnapshotService
    {
        private readonly OnlineMatchController _ctrl;

        public MatchSnapshotService(OnlineMatchController controller)
        {
            _ctrl = controller;
        }

        /// <summary>
        /// 捕获当前游戏状态的完整快照（供主机迁移使用）。
        /// </summary>
        public GameStateSnapshot Capture()
        {
            var snap = new GameStateSnapshot();

            // ── 版本标记 ──
            snap.Version = GameStateSnapshot.SNAPSHOT_VERSION;

            // ── 全局状态 ──
            snap.MatchStarted = _ctrl.matchStarted;
            snap.Phase = _ctrl.phase;
            snap.EvidenceScore = _ctrl.taskService.EvidenceScore;
            snap.EvidenceTarget = _ctrl.taskService.EvidenceTarget;
            snap.EmergencyMeetingsLeft = _ctrl.emergencyMeetingsLeft;
            snap.MeetingCount = _ctrl.MeetingCount;
            snap.EvidenceMilestoneIndex = _ctrl.evidenceMilestoneIndex;
            snap.NextBodyId = _ctrl.killSystem.nextBodyId;
            snap.RoomMinPlayers = _ctrl.roomMinPlayers;
            snap.RoomMaxPlayers = _ctrl.roomMaxPlayers;
            snap.RoomAutoFillAi = _ctrl.roomAutoFillAi;
            snap.RevealRoleOnEject = _ctrl.revealRoleOnEject;
            snap.ProximityVoiceEnabled = _ctrl.proximityVoiceEnabled;
            snap.RoomName = _ctrl.roomName;
            snap.ResultSummary = _ctrl.resultSummary;
            snap.LastMeetingReason = _ctrl.lastMeetingReason;
            snap.LastVoteOutcome = _ctrl.lastVoteOutcome;
            snap.LastEvidenceEvent = _ctrl.lastEvidenceEvent;
            snap.LastSabotageEvent = _ctrl.lastSabotageEvent;
            snap.PhaseTimer = _ctrl.phaseTimer;
            snap.BlackoutTimer = _ctrl.taskService.BlackoutTimer;
            snap.LockdownTimer = _ctrl.taskService.LockdownTimer;
            snap.CommunicationJamTimer = _ctrl.taskService.CommunicationJamTimer;
            snap.EvidenceLeakTimer = _ctrl.taskService.EvidenceLeakTimer;
            snap.EvidenceLeakAccumulator = _ctrl.taskService.EvidenceLeakAccumulator;
            snap.PatrolAlertTimer = _ctrl.taskService.PatrolAlertTimer;
            snap.EmergencyCooldownTimer = _ctrl.emergencyCooldownTimer;
            snap.ReportCooldownTimer = _ctrl.killSystem.reportCooldownTimer;
            snap.AiActionGraceTimer = _ctrl.aiActionGraceTimer;
            snap.MatchElapsedSeconds = _ctrl.matchElapsedSeconds;

            // ── 紧急任务 (Phase 2.4) ──
            _ctrl.WriteCriticalTaskSnapshot(ref snap);

            // ── 玩家状态 ──
            snap.Players = new List<GameStateSnapshot.SnapshotPlayerEntry>(_ctrl.players.Count);
            foreach (var p in _ctrl.players.Values)
            {
                snap.Players.Add(new GameStateSnapshot.SnapshotPlayerEntry
                {
                    ClientId = p.ClientId,
                    DisplayName = p.DisplayName,
                    Position = p.Position,
                    Input = p.Input,
                    Ready = p.Ready,
                    Alive = p.Alive,
                    IsBot = p.IsBot,
                    PublicRole = p.PublicRole,
                    Profession = p.Profession,
                    KillCooldown = _ctrl.killSystem.killCooldowns.TryGetValue(p.ClientId, out float kd) ? kd : 0f,
                    AbilityCooldown = _ctrl.abilityCooldowns.TryGetValue(p.ClientId, out float ac) ? ac : 0f,
                    Suspicion = p.Suspicion,
                });
            }

            // ── 私密角色 ──
            snap.PrivateRoles = new List<GameStateSnapshot.SnapshotRoleEntry>(_ctrl.privateRoles.Count);
            foreach (var kv in _ctrl.privateRoles)
            {
                snap.PrivateRoles.Add(new GameStateSnapshot.SnapshotRoleEntry { ClientId = kv.Key, Role = kv.Value });
            }
            snap.UndercoverStates = _ctrl.UndercoverStatesSnapshot();
            snap.MoleStates = _ctrl.MoleStatesSnapshot();

            // ── 任务 ──
            snap.Tasks = new List<GameStateSnapshot.SnapshotTaskEntry>(_ctrl.tasks.Count);
            foreach (var t in _ctrl.tasks)
            {
                snap.Tasks.Add(new GameStateSnapshot.SnapshotTaskEntry
                {
                    Id = t.Id, Name = t.Name, Position = t.Position,
                    Progress = t.Progress, RequiredProgress = t.RequiredProgress,
                    Completed = t.Completed, Sabotaged = t.Sabotaged,
                });
            }
            snap.TaskAssignments = _ctrl.TaskSyncAssignmentsSnapshot();

            // ── 尸体 ──
            snap.Bodies = new List<GameStateSnapshot.SnapshotBodyEntry>(_ctrl.killSystem.bodies.Count);
            foreach (var b in _ctrl.killSystem.bodies)
            {
                snap.Bodies.Add(new GameStateSnapshot.SnapshotBodyEntry
                {
                    Id = b.Id, VictimClientId = b.VictimClientId,
                    Position = b.Position, Reported = b.Reported,
                });
            }

            // ── 投票 ──
            snap.Votes = new List<GameStateSnapshot.SnapshotVoteEntry>(_ctrl.votes.Count);
            foreach (var v in _ctrl.votes)
            {
                snap.Votes.Add(new GameStateSnapshot.SnapshotVoteEntry { VoterClientId = v.Key, TargetClientId = v.Value });
            }

            snap.Accusations = _ctrl.AccusationsSnapshot();

            // ── 案卷 ──
            snap.CaseLog = new List<string>(_ctrl.caseLog);

            // ── 冷却 ──
            snap.KillCooldowns = OnlineMatchUtils.CooldownsToList(_ctrl.killSystem.killCooldowns);
            snap.AbilityCooldowns = OnlineMatchUtils.CooldownsToList(_ctrl.abilityCooldowns);
            snap.VentCooldowns = OnlineMatchUtils.CooldownsToList(_ctrl.ventCooldowns);
            snap.BotThinkTimers = OnlineMatchUtils.CooldownsToList(_ctrl.botController.ThinkTimers);
            snap.BotVoteTimers = OnlineMatchUtils.CooldownsToList(_ctrl.botController.VoteTimers);

            // ── Bot 目标 ──
            snap.BotTargets = new List<GameStateSnapshot.SnapshotTargetEntry>(_ctrl.botController.Targets.Count);
            foreach (var bt in _ctrl.botController.Targets)
            {
                snap.BotTargets.Add(new GameStateSnapshot.SnapshotTargetEntry { ClientId = bt.Key, Target = bt.Value });
            }

            return snap;
        }

        /// <summary>
        /// 从快照恢复游戏状态（主机迁移时由新主机或客户端调用）。
        /// </summary>
        public void Restore(GameStateSnapshot snap)
        {
            ulong localClientId = _ctrl.LocalClientIdValue;
            OnlineRole localRole = _ctrl.LocalRole;
            List<GameStateSnapshot.SnapshotUndercoverStateEntry> localUndercoverStates = _ctrl.UndercoverStatesSnapshot();
            List<GameStateSnapshot.SnapshotMoleStateEntry> localMoleStates = _ctrl.MoleStatesSnapshot();
            bool snapshotContainsLocalRole = false;
            if (snap.PrivateRoles != null)
            {
                foreach (GameStateSnapshot.SnapshotRoleEntry role in snap.PrivateRoles)
                {
                    if (role.ClientId == localClientId)
                    {
                        snapshotContainsLocalRole = true;
                        break;
                    }
                }
            }

            // ── 版本兼容性检查 ──
            if (snap.Version != GameStateSnapshot.SNAPSHOT_VERSION)
            {
                Debug.LogWarning(
                    $"[RestoreFromSnapshot] 快照版本不匹配: 快照 v{snap.Version}, 当前 v{GameStateSnapshot.SNAPSHOT_VERSION}。" +
                    "将尽力恢复，但部分状态可能存在差异。");
            }

            if (!snap.IsValid())
            {
                Debug.LogError("[RestoreFromSnapshot] 快照完整性检查失败，恢复后的状态可能不完整。");
            }

            _ctrl.ClearActiveTaskLocks();

            // ── 全局状态 ──
            _ctrl.matchStarted = snap.MatchStarted;
            _ctrl.phase = snap.Phase;
            _ctrl.taskService.EvidenceScore = snap.EvidenceScore;
            _ctrl.taskService.EvidenceTarget = snap.EvidenceTarget;
            _ctrl.emergencyMeetingsLeft = snap.EmergencyMeetingsLeft;
            _ctrl.RestoreMeetingCountFromSnapshot(snap.MeetingCount);
            _ctrl.evidenceMilestoneIndex = snap.EvidenceMilestoneIndex;
            _ctrl.killSystem.nextBodyId = snap.NextBodyId;
            _ctrl.roomMinPlayers = snap.RoomMinPlayers;
            _ctrl.roomMaxPlayers = snap.RoomMaxPlayers;
            _ctrl.roomAutoFillAi = snap.RoomAutoFillAi;
            _ctrl.revealRoleOnEject = snap.RevealRoleOnEject;
            _ctrl.proximityVoiceEnabled = snap.ProximityVoiceEnabled;
            _ctrl.roomName = snap.RoomName;
            _ctrl.resultSummary = snap.ResultSummary;
            _ctrl.lastMeetingReason = snap.LastMeetingReason;
            _ctrl.lastVoteOutcome = snap.LastVoteOutcome;
            _ctrl.lastEvidenceEvent = snap.LastEvidenceEvent;
            _ctrl.lastSabotageEvent = snap.LastSabotageEvent;
            _ctrl.phaseTimer = snap.PhaseTimer;
            _ctrl.taskService.LoadSabotageTimersFromSnapshot(
                snap.BlackoutTimer, snap.LockdownTimer, snap.CommunicationJamTimer,
                snap.EvidenceLeakTimer, snap.EvidenceLeakAccumulator, snap.PatrolAlertTimer);
            _ctrl.emergencyCooldownTimer = snap.EmergencyCooldownTimer;
            _ctrl.killSystem.reportCooldownTimer = snap.ReportCooldownTimer;
            _ctrl.SyncMeetingSnapshotToService();
            _ctrl.SyncEvidenceServiceFromController();
            _ctrl.aiActionGraceTimer = snap.AiActionGraceTimer;
            _ctrl.matchElapsedSeconds = snap.MatchElapsedSeconds;

            // ── 玩家状态 ──
            _ctrl.players.Clear();
            if (snap.Players != null)
            {
                foreach (var p in snap.Players)
                {
                    var state = new OnlinePlayerState(p.ClientId, p.DisplayName, p.Position, p.Ready, p.Alive, p.PublicRole, p.Profession, p.Suspicion, p.IsBot)
                    {
                        Input = p.Input,
                        KillCooldown = p.KillCooldown,
                        AbilityCooldown = p.AbilityCooldown,
                    };
                    _ctrl.players[p.ClientId] = state;
                }
            }

            // ── 私密角色 ──
            _ctrl.privateRoles.Clear();
            if (snap.PrivateRoles != null)
            {
                foreach (var r in snap.PrivateRoles)
                {
                    _ctrl.privateRoles[r.ClientId] = r.Role;
                }
            }
            _ctrl.LoadIdentityStates(snap.UndercoverStates, snap.MoleStates);
            if (!snapshotContainsLocalRole)
            {
                _ctrl.RestoreLocalIdentityIfMissing(localClientId, localRole, localUndercoverStates, localMoleStates);
            }

            // ── 任务 ──
            _ctrl.tasks.Clear();
            if (snap.Tasks != null)
            {
                foreach (var t in snap.Tasks)
                {
                    _ctrl.tasks.Add(new OnlineTaskState(t.Id, t.Name, t.Position, t.Progress, t.RequiredProgress, t.Completed, t.Sabotaged));
                }
            }
            _ctrl.LoadTaskSyncAssignments(snap.TaskAssignments ?? new List<GameStateSnapshot.SnapshotTaskAssignmentEntry>());

            // ── 尸体 ──
            _ctrl.killSystem.bodies.Clear();
            if (snap.Bodies != null)
            {
                foreach (var b in snap.Bodies)
                {
                    _ctrl.killSystem.bodies.Add(new OnlineBodyState(b.Id, b.VictimClientId, b.Position, b.Reported));
                }
            }

            // ── 投票 ──
            _ctrl.votes.Clear();
            if (snap.Votes != null)
            {
                foreach (var v in snap.Votes)
                {
                    _ctrl.votes[v.VoterClientId] = v.TargetClientId;
                }
            }

            // ── 会议指证 ──
            _ctrl.LoadAccusations(snap.Accusations);

            // ── 案卷 ──
            _ctrl.caseLog.Clear();
            if (snap.CaseLog != null)
            {
                _ctrl.caseLog.AddRange(snap.CaseLog);
            }

            // ── 冷却 ──
            OnlineMatchUtils.ListToCooldowns(_ctrl.killSystem.killCooldowns, snap.KillCooldowns);
            OnlineMatchUtils.ListToCooldowns(_ctrl.abilityCooldowns, snap.AbilityCooldowns);
            OnlineMatchUtils.ListToCooldowns(_ctrl.ventCooldowns, snap.VentCooldowns);
            // Bot 计时器通过 bot 控制器恢复
            if (snap.BotThinkTimers != null)
                foreach (var entry in snap.BotThinkTimers)
                    _ctrl.botController.SetThinkTimer(entry.ClientId, entry.Value);
            if (snap.BotVoteTimers != null)
                foreach (var entry in snap.BotVoteTimers)
                    _ctrl.botController.SetVoteTimer(entry.ClientId, entry.Value);

            // ── Bot 目标 ──
            _ctrl.botController.ClearTargets();
            if (snap.BotTargets != null)
            {
                foreach (var bt in snap.BotTargets)
                {
                    _ctrl.botController.SetTarget(bt.ClientId, bt.Target);
                }
            }

            // ── 紧急任务 (Phase 2.4) ──
            _ctrl.ReadCriticalTaskSnapshot(snap);

            // ── 收尾：更新本地位置、状态、UI ──
            _ctrl.OnSnapshotRestored();
        }
    }
}
