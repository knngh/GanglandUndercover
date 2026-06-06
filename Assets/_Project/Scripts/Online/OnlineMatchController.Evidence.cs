using System.Collections.Generic;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 3.1: 证据链系统在线集成。
    /// 管理证据登记、关联计算、会议指证、投票权重。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        private EvidenceChain _evidenceChain;
        private readonly Dictionary<ulong, ulong> _accusationTargets = new Dictionary<ulong, ulong>();

        public EvidenceChain EvidenceManager => _evidenceChain;
        public IReadOnlyDictionary<ulong, ulong> AccusationTargets => _accusationTargets;

        private void EnsureEvidenceChain()
        {
            if (_evidenceChain != null) return;
            _evidenceChain = new EvidenceChain();
        }

        // ============================================================
        //  证据登记钩子（在任务完成/击杀/能力使用时调用）
        // ============================================================

        /// <summary>任务完成时登记证据。</summary>
        public void RegisterTaskEvidence(int taskId, Vector2 position, ulong discovererId)
        {
            EnsureEvidenceChain();
            EvidenceType type = (EvidenceType)((taskId % 5) + 1); // 轮转类型
            _evidenceChain.Register(type, position, matchElapsedSeconds, discovererId,
                chainId: taskId, customDesc: $"任务 #{taskId} 完成时发现{EvidenceChain.EvidenceTypeName(type)}");
        }

        /// <summary>击杀发生时登记血迹证据。</summary>
        public void RegisterKillEvidence(Vector2 position)
        {
            EnsureEvidenceChain();
            _evidenceChain.Register(EvidenceType.Bloodstain, position, matchElapsedSeconds, 0,
                chainId: -1);
        }

        /// <summary>尸体检验时登记武器痕迹。</summary>
        public void RegisterCorpseExamineEvidence(Vector2 position, ulong examinerId)
        {
            EnsureEvidenceChain();
            _evidenceChain.Register(EvidenceType.WeaponTrace, position, matchElapsedSeconds, examinerId,
                chainId: -2);
        }

        /// <summary>监控摄像头捕获时登记。</summary>
        public void RegisterSurveillanceEvidence(Vector2 position, ulong watcherId)
        {
            EnsureEvidenceChain();
            _evidenceChain.Register(EvidenceType.SurveillanceFootage, position, matchElapsedSeconds, watcherId,
                chainId: -3);
        }

        // ============================================================
        //  会议指证系统
        // ============================================================

        /// <summary>玩家指证目标（会议中使用）。</summary>
        public void AccusePlayer(ulong accuserId, ulong targetId)
        {
            _accusationTargets[accuserId] = targetId;
        }

        /// <summary>清除所有指证（新会议开始时）。</summary>
        public void ClearAccusations()
        {
            _accusationTargets.Clear();
        }

        /// <summary>
        /// 获取指证的投票权重加成。
        /// 有证据链指证 → 被指证者投票权重 +2
        /// 无证据链指证 → 无加成
        /// </summary>
        public int GetAccusationWeightBonus(ulong targetId)
        {
            if (_evidenceChain == null) return 0;

            int bonus = 0;
            foreach (var kv in _accusationTargets)
            {
                if (kv.Value == targetId)
                {
                    var nodes = _evidenceChain.GetDiscovererNodes(kv.Key);
                    int strength = _evidenceChain.TotalChainStrength(nodes);
                    if (strength >= 3) // 至少中等强度链才有效
                        bonus += 2;
                }
            }
            return bonus;
        }

        /// <summary>获取会议证据摘要（供 UI 展示）。</summary>
        public string MeetingEvidenceDigest(ulong localPlayerId)
        {
            EnsureEvidenceChain();
            return _evidenceChain.MeetingEvidenceSummary(localPlayerId);
        }

        /// <summary>清空证据链（新局开始时）。</summary>
        public void ClearEvidenceChain()
        {
            _evidenceChain?.Clear();
            _accusationTargets.Clear();
        }
    }
}
