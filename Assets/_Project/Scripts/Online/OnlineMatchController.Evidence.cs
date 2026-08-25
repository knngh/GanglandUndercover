using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 3.1: 证据链系统在线集成 — 薄委托层。
    /// 实际逻辑由 EvidenceService 承担，此处仅保留公开 API 以便向后兼容。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        public EvidenceChain EvidenceManager => evidenceService != null ? evidenceService.EvidenceManager : null;
        public IReadOnlyDictionary<ulong, ulong> AccusationTargets => evidenceService != null ? evidenceService.AccusationTargets : new Dictionary<ulong, ulong>();

        public string AccusationSummary
        {
            get
            {
                if (AccusationTargets.Count == 0)
                {
                    return "尚无公开指证。";
                }

                StringBuilder summary = new StringBuilder();
                foreach (KeyValuePair<ulong, ulong> accusation in AccusationTargets)
                {
                    string accuserName = players.TryGetValue(accusation.Key, out OnlinePlayerState accuser)
                        ? accuser.DisplayName
                        : "玩家" + accusation.Key;
                    string targetName = players.TryGetValue(accusation.Value, out OnlinePlayerState target)
                        ? target.DisplayName
                        : "玩家" + accusation.Value;
                    summary.Append(accuserName).Append(" 指证 ").Append(targetName).Append("；");
                }

                return summary.ToString().TrimEnd('；');
            }
        }

        private void EnsureEvidenceChain()
        {
            // EvidenceService 在 Awake 中自动初始化；此处保留兼容性。
        }

        // ============================================================
        //  证据登记钩子（在任务完成/击杀/能力使用时调用）
        // ============================================================

        /// <summary>任务完成时登记证据。</summary>
        public void RegisterTaskEvidence(int taskId, Vector2 position, ulong discovererId)
        {
            evidenceService?.RegisterTaskEvidence(taskId, position, discovererId);
        }

        /// <summary>击杀发生时登记血迹证据。</summary>
        public void RegisterKillEvidence(Vector2 position)
        {
            evidenceService?.RegisterKillEvidence(position);
        }

        /// <summary>尸体检验时登记武器痕迹。</summary>
        public void RegisterCorpseExamineEvidence(Vector2 position, ulong examinerId)
        {
            evidenceService?.RegisterCorpseExamineEvidence(position, examinerId);
        }

        /// <summary>监控摄像头捕获时登记。</summary>
        public void RegisterSurveillanceEvidence(Vector2 position, ulong watcherId)
        {
            evidenceService?.RegisterSurveillanceEvidence(position, watcherId);
        }

        // ============================================================
        //  会议指证系统
        // ============================================================

        /// <summary>玩家指证目标（会议中使用）。</summary>
        public void AccusePlayer(ulong accuserId, ulong targetId)
        {
            evidenceService?.AccusePlayer(accuserId, targetId);
        }

        /// <summary>检查玩家本轮是否已经提交指证。</summary>
        public bool HasAccused(ulong accuserId)
        {
            return evidenceService != null && evidenceService.HasAccused(accuserId);
        }

        internal bool TryAccusePlayer(ulong accuserId, ulong targetId)
        {
            return evidenceService != null && evidenceService.TryAccusePlayer(accuserId, targetId);
        }

        internal void LoadAccusations(IEnumerable<KeyValuePair<ulong, ulong>> snapshotAccusations)
        {
            evidenceService?.LoadAccusations(snapshotAccusations);
        }

        internal void LoadAccusations(IReadOnlyList<GameStateSnapshot.SnapshotAccusationEntry> snapshotAccusations)
        {
            if (evidenceService == null)
            {
                return;
            }

            List<KeyValuePair<ulong, ulong>> pairs = new List<KeyValuePair<ulong, ulong>>();
            if (snapshotAccusations != null)
            {
                for (int i = 0; i < snapshotAccusations.Count; i++)
                {
                    GameStateSnapshot.SnapshotAccusationEntry accusation = snapshotAccusations[i];
                    pairs.Add(new KeyValuePair<ulong, ulong>(accusation.AccuserClientId, accusation.TargetClientId));
                }
            }

            evidenceService.LoadAccusations(pairs);
        }

        internal List<GameStateSnapshot.SnapshotAccusationEntry> AccusationsSnapshot()
        {
            List<GameStateSnapshot.SnapshotAccusationEntry> snapshot = new List<GameStateSnapshot.SnapshotAccusationEntry>();
            foreach (KeyValuePair<ulong, ulong> accusation in AccusationTargets)
            {
                snapshot.Add(new GameStateSnapshot.SnapshotAccusationEntry
                {
                    AccuserClientId = accusation.Key,
                    TargetClientId = accusation.Value,
                });
            }

            return snapshot;
        }

        /// <summary>清除所有指证（新会议开始时）。</summary>
        public void ClearAccusations()
        {
            evidenceService?.ClearAccusations();
        }

        /// <summary>
        /// 获取指证的投票权重加成。
        /// 有证据链指证 → 被指证者投票权重 +2
        /// 无证据链指证 → 无加成
        /// </summary>
        public int GetAccusationWeightBonus(ulong targetId)
        {
            return evidenceService != null ? evidenceService.GetAccusationWeightBonus(targetId) : 0;
        }

        /// <summary>获取会议证据摘要（供 UI 展示）。</summary>
        public string MeetingEvidenceDigest(ulong localPlayerId)
        {
            return evidenceService != null ? evidenceService.MeetingEvidenceDigest(localPlayerId) : "证据系统未就绪。";
        }

        /// <summary>清空证据链（新局开始时）。</summary>
        public void ClearEvidenceChain()
        {
            evidenceService?.ClearAll();
        }
    }
}
