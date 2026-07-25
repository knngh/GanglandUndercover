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

        public bool CriticalTaskActive => _criticalTaskActive;
        public CriticalTaskType ActiveCriticalTaskType => _criticalTaskType;
        public float CriticalTaskTimeRemaining => _criticalTaskTimeRemaining;
        public float CriticalTaskTotalTime => _criticalTaskTotalTime;
        public CriticalTaskState CriticalTaskStateValue => _criticalTaskState;

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
            if (!matchStarted) return;
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

            // 条件 2: 警方增援 — 黑帮人数 ≤ 警察人数 × 50%
            if (CountAliveRole(OnlineRole.Gang) <= CountAliveRole(OnlineRole.Police) * 0.5f)
            {
                if (CountAliveRole(OnlineRole.Gang) > 0) // 至少还有活着的黑帮
                {
                    TriggerCriticalTask(CriticalTaskType.PoliceReinforcement);
                    return;
                }
            }
        }

        private void TriggerCriticalTask(CriticalTaskType type)
        {
            EnsureCriticalTaskSystem();
            _criticalTaskSystem.Trigger(type);
            _criticalTaskActive = true;
            _criticalTaskType = type;
            status = $"⚠ 紧急任务: {type}";
        }

        private void TickCriticalTaskSync()
        {
            if (_criticalTaskSystem == null) return;

            _criticalTaskActive = _criticalTaskSystem.State == CriticalTaskState.Active;
            _criticalTaskType = _criticalTaskSystem.ActiveType;
            _criticalTaskTimeRemaining = _criticalTaskSystem.TimeRemaining;
            _criticalTaskTotalTime = _criticalTaskSystem.TotalTime;
            _criticalTaskState = _criticalTaskSystem.State;
        }

        private void OnCriticalTaskCompleted(CriticalTaskType type)
        {
            _criticalTaskActive = false;
            status = $"✓ 紧急任务完成: {type}";
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
                    // 黑帮位置暴露 30 秒
                    foreach (var kv in players)
                    {
                        if (GetPrivateRole(kv.Key) == OnlineRole.Gang || GetPrivateRole(kv.Key) == OnlineRole.Mole)
                        {
                            OnlinePlayerState state = kv.Value;
                            state.Suspicion = Mathf.Max(state.Suspicion, 30);
                            players[kv.Key] = state;
                        }
                    }
                    status = "✗ 警方增援失败 — 黑帮位置暴露 30s";
                    break;
            }
        }

        /// <summary>快照序列化: 紧急任务状态（供 MatchSnapshotService 调用）</summary>
        internal void WriteCriticalTaskSnapshot(GameStateSnapshot snap)
        {
            snap.CriticalTaskActive = _criticalTaskActive;
            snap.CriticalTaskType = (byte)_criticalTaskType;
            snap.CriticalTaskTimeRemaining = _criticalTaskTimeRemaining;
        }

        /// <summary>快照反序列化: 紧急任务状态（供 MatchSnapshotService 调用）</summary>
        internal void ReadCriticalTaskSnapshot(GameStateSnapshot snap)
        {
            _criticalTaskActive = snap.CriticalTaskActive;
            _criticalTaskType = (CriticalTaskType)snap.CriticalTaskType;
            _criticalTaskTimeRemaining = snap.CriticalTaskTimeRemaining;
            _criticalTaskTotalTime = snap.CriticalTaskTimeRemaining;
        }

        private void ClearCriticalTasks()
        {
            if (_criticalTaskSystem != null)
            {
                _criticalTaskSystem.Cancel();
            }
            _criticalTaskActive = false;
            _criticalTaskType = CriticalTaskType.None;
        }
    }
}
