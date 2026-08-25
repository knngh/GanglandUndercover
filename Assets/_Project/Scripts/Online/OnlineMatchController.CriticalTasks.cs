using System.Collections.Generic;
using GanglandUndercover.Core;
using GanglandUndercover.SocialDeduction;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 2.4: 紧急任务系统在线集成。
    /// 复用离线 CriticalTaskSystem，服务端权威触发和判定，
    /// 状态通过现有快照系统同步到所有客户端。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        private CriticalTaskSystem _criticalTaskSystem;
        private bool _criticalTaskActive;
        private CriticalTaskType _criticalTaskType;
        private float _criticalTaskTimeRemaining;
        private float _criticalTaskTotalTime;
        private CriticalTaskState _criticalTaskState;
        private float _gangPositionRevealTimer;

        private readonly HashSet<int> _criticalEvidenceRepairStations = new HashSet<int>();

        public bool CriticalTaskActive => _criticalTaskActive;
        public CriticalTaskType ActiveCriticalTaskType => _criticalTaskType;
        public float CriticalTaskTimeRemaining => _criticalTaskTimeRemaining;
        public float CriticalTaskTotalTime => _criticalTaskTotalTime;
        public CriticalTaskState CriticalTaskStateValue => _criticalTaskState;
        public int CriticalEvidenceRepairStationCount => _criticalEvidenceRepairStations.Count;
        public float GangPositionRevealTimeRemaining => _gangPositionRevealTimer;
        public bool GangPositionsRevealed => _gangPositionRevealTimer > 0f;

        private void EnsureCriticalTaskSystem()
        {
            if (_criticalTaskSystem != null) return;
            _criticalTaskSystem = gameObject.AddComponent<CriticalTaskSystem>();
            _criticalTaskSystem.OnCriticalTaskCompleted += OnCriticalTaskCompleted;
            _criticalTaskSystem.OnCriticalTaskFailed += OnCriticalTaskFailed;
        }

        /// <summary>
        /// 服务端每帧检查触发条件。
        /// </summary>
        private void TickCriticalTaskTriggers()
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (!matchStarted || phase != OnlineMatchPhase.Action) return;
            if (_criticalTaskSystem == null) return;
            if (_criticalTaskSystem.State != CriticalTaskState.Inactive) return;

            // 条件 1: 证据销毁 — Police 证据分 ≥ 75%
            if (taskService != null && taskService.EvidenceTarget > 0)
            {
                float evidencePct = (float)taskService.EvidenceScore / taskService.EvidenceTarget;
                if (evidencePct >= 0.75f)
                {
                    TriggerCriticalTask(CriticalTaskType.EvidenceDestruction);
                    return;
                }
            }

            // 条件 2: 警方增援 — 黑帮侧人数 ≤ 警察侧人数 × 50%。
            // 必须先发生黑帮侧减员，避免按角色分配完成后在开局立即触发。
            int aliveGangSide = CountAliveRole(OnlineRole.Gang) + CountAliveRole(OnlineRole.Mole);
            int alivePoliceSide = CountAliveRole(OnlineRole.Police) + CountAliveRole(OnlineRole.Undercover);
            int totalGangSide = 0;
            foreach (OnlineRole role in privateRoles.Values)
            {
                if (OnlineMatchUtils.IsGangSide(role)) totalGangSide++;
            }

            if (totalGangSide > aliveGangSide
                && aliveGangSide > 0
                && aliveGangSide * 2 <= alivePoliceSide)
            {
                TriggerCriticalTask(CriticalTaskType.PoliceReinforcement);
            }
        }

        private void TriggerCriticalTask(CriticalTaskType type)
        {
            EnsureCriticalTaskSystem();
            _criticalTaskSystem.Trigger(type);
            TickCriticalTaskSync(0f);
            if (!_criticalTaskActive || _criticalTaskType != type)
            {
                return;
            }

            status = $"⚠ 紧急任务: {type}";
        }

        private void TickCriticalTaskSync(float deltaTime)
        {
            if (_criticalTaskSystem == null) return;

            _criticalTaskActive = _criticalTaskSystem.State == CriticalTaskState.Active;
            _criticalTaskType = _criticalTaskSystem.ActiveType;
            _criticalTaskTimeRemaining = _criticalTaskSystem.TimeRemaining;
            _criticalTaskTotalTime = _criticalTaskSystem.TotalTime;
            _criticalTaskState = _criticalTaskSystem.State;

            _gangPositionRevealTimer = Mathf.Max(0f, _gangPositionRevealTimer - Mathf.Max(0f, deltaTime));
        }

        private void OnCriticalTaskCompleted(CriticalTaskType type)
        {
            _criticalTaskActive = false;
            status = $"✓ 紧急任务完成: {type}";
            BroadcastSnapshot();
        }

        private void OnCriticalTaskFailed(CriticalTaskType type)
        {
            _criticalTaskActive = false;

            switch (type)
            {
                case CriticalTaskType.EvidenceDestruction:
                    // 证据分 -40%
                    if (evidenceService != null)
                    {
                        int penalty = Mathf.RoundToInt(evidenceService.EvidenceScore * 0.4f);
                        evidenceService.SubtractEvidence(penalty);
                    }
                    status = "✗ 证据销毁失败 — 证据分 -40%";
                    break;

                case CriticalTaskType.PoliceReinforcement:
                    _gangPositionRevealTimer = 30f;
                    status = "✗ 警方增援失败 — 黑帮位置暴露 30s";
                    break;
            }

            BroadcastSnapshot();
        }

        private void RecordCriticalTaskRepair(ulong clientId, int taskId)
        {
            if (!_criticalTaskActive
                || _criticalTaskType != CriticalTaskType.EvidenceDestruction
                || _criticalTaskSystem == null
                || !players.TryGetValue(clientId, out OnlinePlayerState player)
                || !player.Alive
                || !OnlineMatchUtils.IsPoliceSide(GetPrivateRole(clientId)))
            {
                return;
            }

            if (!_criticalEvidenceRepairStations.Contains(taskId)
                && _criticalTaskSystem.SubmitEvidenceRepair(taskId))
            {
                _criticalEvidenceRepairStations.Add(taskId);
                _criticalTaskTimeRemaining = _criticalTaskSystem.TimeRemaining;
            }
        }

        private void RecordCriticalTaskSabotage(ulong clientId, SabotageType type)
        {
            if (!_criticalTaskActive
                || _criticalTaskType != CriticalTaskType.PoliceReinforcement
                || type != SabotageType.Communications
                || _criticalTaskSystem == null
                || !players.TryGetValue(clientId, out OnlinePlayerState player)
                || !player.Alive
                || !OnlineMatchUtils.CanSabotage(GetPrivateRole(clientId)))
            {
                return;
            }

            _criticalTaskSystem.SubmitPoliceReinforcementSabotage();
            _criticalTaskTimeRemaining = _criticalTaskSystem.TimeRemaining;
        }

        /// <summary>快照序列化: 紧急任务状态（供 MatchSnapshotService 调用）</summary>
        internal void WriteCriticalTaskSnapshot(ref GameStateSnapshot snap)
        {
            snap.CriticalTaskActive = _criticalTaskActive;
            snap.CriticalTaskType = (byte)_criticalTaskType;
            snap.CriticalTaskTimeRemaining = _criticalTaskTimeRemaining;
            snap.CriticalEvidenceRepairStations = new List<int>(_criticalEvidenceRepairStations);
            snap.GangPositionRevealTimeRemaining = _gangPositionRevealTimer;
        }

        /// <summary>快照反序列化: 紧急任务状态（供 MatchSnapshotService 调用）</summary>
        internal void ReadCriticalTaskSnapshot(GameStateSnapshot snap)
        {
            EnsureCriticalTaskSystem();
            _criticalTaskActive = snap.CriticalTaskActive;
            _criticalTaskType = (CriticalTaskType)snap.CriticalTaskType;
            _criticalTaskTimeRemaining = snap.CriticalTaskTimeRemaining;
            _gangPositionRevealTimer = Mathf.Max(0f, snap.GangPositionRevealTimeRemaining);
            if (snap.CriticalTaskActive)
            {
                _criticalTaskSystem.RestoreActive(_criticalTaskType, snap.CriticalTaskTimeRemaining);
                _criticalEvidenceRepairStations.Clear();
                if (snap.CriticalEvidenceRepairStations != null)
                {
                    foreach (int stationId in snap.CriticalEvidenceRepairStations)
                    {
                        if (stationId >= 0)
                        {
                            _criticalEvidenceRepairStations.Add(stationId);
                            _criticalTaskSystem.SubmitEvidenceRepair(stationId);
                        }
                    }
                }
                _criticalTaskTotalTime = _criticalTaskSystem.TotalTime;
                _criticalTaskState = _criticalTaskSystem.State;
            }
            else
            {
                _criticalTaskSystem.Cancel();
                _criticalEvidenceRepairStations.Clear();
                _criticalTaskTotalTime = 0f;
                _criticalTaskState = CriticalTaskState.Inactive;
            }
        }

        private void ClearCriticalTasks()
        {
            if (_criticalTaskSystem != null)
            {
                _criticalTaskSystem.Cancel();
            }
            _criticalTaskActive = false;
            _criticalTaskType = CriticalTaskType.None;
            _criticalTaskTimeRemaining = 0f;
            _criticalTaskTotalTime = 0f;
            _criticalTaskState = CriticalTaskState.Inactive;
            _criticalEvidenceRepairStations.Clear();
            _gangPositionRevealTimer = 0f;
        }

        internal void ReadCriticalTaskStationCount(int count)
        {
            _criticalEvidenceRepairStations.Clear();
            if (_criticalTaskType != CriticalTaskType.EvidenceDestruction)
            {
                return;
            }

            for (int i = 0; i < Mathf.Clamp(count, 0, 2); i++)
            {
                _criticalEvidenceRepairStations.Add(i);
            }
        }

        public bool ShouldRevealPlayerPosition(ulong viewerClientId, ulong targetClientId)
        {
            if (viewerClientId == targetClientId || phase == OnlineMatchPhase.Result)
            {
                return true;
            }

            if (!players.TryGetValue(viewerClientId, out OnlinePlayerState viewer) || !viewer.Alive)
            {
                return true;
            }

            OnlineRole viewerRole = viewerClientId == LocalClientId()
                ? LocalEffectiveRole()
                : GetPrivateRole(viewerClientId);
            return GangPositionsRevealed
                && OnlineMatchUtils.IsPoliceSide(viewerRole)
                && players.TryGetValue(targetClientId, out OnlinePlayerState target)
                && target.PublicRole == OnlineRole.Gang;
        }
    }
}
