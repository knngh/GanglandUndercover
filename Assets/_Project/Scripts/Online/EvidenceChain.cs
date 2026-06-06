using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 3.1: 证据类型枚举。
    /// 不同类型可互相组合形成证据链。
    /// </summary>
    public enum EvidenceType : byte
    {
        None,
        Footprint,              // 足迹 — Inspector 能力产出
        Bloodstain,             // 血迹 — 击杀现场
        WeaponTrace,            // 武器痕迹 — 法医检验尸体
        AlibiBreak,             // 不在场破绽 — 任务站记录
        TransactionRecord,      // 交易记录 — 任务产出
        SurveillanceFootage     // 监控录像 — 监控摄像头
    }

    /// <summary>
    /// Phase 3.1: 单个证据节点。
    /// 由任务完成、能力使用、尸体检验等事件产出。
    /// </summary>
    [Serializable]
    public struct EvidenceNode
    {
        public int Id;
        public EvidenceType Type;
        public Vector2 WorldPosition;
        public float GameTime;          // 游戏内发现时间
        public ulong DiscovererId;      // 发现者 ClientId
        public int ChainId;             // 用于跨节点关联（同位置/同类型事件）
        public string Description;      // 人类可读描述

        public EvidenceNode(int id, EvidenceType type, Vector2 pos, float time, ulong discoverer, int chainId, string desc)
        {
            Id = id;
            Type = type;
            WorldPosition = pos;
            GameTime = time;
            DiscovererId = discoverer;
            ChainId = chainId;
            Description = desc;
        }
    }

    /// <summary>
    /// Phase 3.1: 证据关联规则和权重计算。
    /// 管理所有证据节点的关联矩阵，计算证据链强度。
    /// </summary>
    public class EvidenceChain
    {
        private readonly List<EvidenceNode> _nodes = new List<EvidenceNode>();
        private int _nextId;

        // ============================================================
        //  证据登记
        // ============================================================

        /// <summary>登记新证据节点，返回其 ID。</summary>
        public int Register(EvidenceType type, Vector2 worldPos, float gameTime, ulong discovererId,
            int chainId = 0, string customDesc = null)
        {
            string desc = customDesc ?? EvidenceDescription(type, discovererId);
            int id = _nextId++;
            _nodes.Add(new EvidenceNode(id, type, worldPos, gameTime, discovererId, chainId, desc));
            return id;
        }

        /// <summary>获取全部证据节点（只读）。</summary>
        public IReadOnlyList<EvidenceNode> AllNodes => _nodes;

        /// <summary>指定发现者的证据节点数。</summary>
        public int CountByDiscoverer(ulong discovererId)
        {
            int count = 0;
            foreach (var n in _nodes)
                if (n.DiscovererId == discovererId) count++;
            return count;
        }

        // ============================================================
        //  关联计算
        // ============================================================

        /// <summary>
        /// 计算两个证据节点之间的关联强度。
        /// 返回值含义: 0=无关, 1=弱(同类型), 2=中(跨类型), 3=强(时间线匹配)
        /// </summary>
        public int AssociationStrength(EvidenceNode a, EvidenceNode b)
        {
            int strength = 0;

            // 同类型证据 ×2 → +1
            if (a.Type == b.Type && a.Type != EvidenceType.None)
                strength += 1;

            // 跨类型证据 ×2 → +2
            if (a.Type != b.Type && a.Type != EvidenceType.None && b.Type != EvidenceType.None)
                strength += 2;

            // 同发现者链 → +1
            if (a.DiscovererId == b.DiscovererId && a.DiscovererId != 0)
                strength += 1;

            // 时间线匹配（15秒内发现的两个证据）→ +1
            float timeDelta = Mathf.Abs(a.GameTime - b.GameTime);
            if (timeDelta <= 15f && a.DiscovererId != b.DiscovererId)
                strength += 1;

            return strength;
        }

        /// <summary>
        /// 计算一组证据节点的总关联强度。
        /// 对每对节点调用 AssociationStrength 求和。
        /// </summary>
        public int TotalChainStrength(List<EvidenceNode> chain)
        {
            if (chain.Count < 2) return 0;
            int total = 0;
            for (int i = 0; i < chain.Count; i++)
                for (int j = i + 1; j < chain.Count; j++)
                    total += AssociationStrength(chain[i], chain[j]);
            return total;
        }

        /// <summary>
        /// 获取指定发现者的证据节点集合。
        /// </summary>
        public List<EvidenceNode> GetDiscovererNodes(ulong discovererId)
        {
            List<EvidenceNode> result = new List<EvidenceNode>();
            foreach (var n in _nodes)
                if (n.DiscovererId == discovererId)
                    result.Add(n);
            return result;
        }

        /// <summary>
        /// 获取最近 N 分钟内的证据节点。
        /// </summary>
        public List<EvidenceNode> GetRecentNodes(float currentTime, float windowSeconds = 120f)
        {
            List<EvidenceNode> result = new List<EvidenceNode>();
            foreach (var n in _nodes)
                if (currentTime - n.GameTime <= windowSeconds)
                    result.Add(n);
            return result;
        }

        /// <summary>清空所有证据。</summary>
        public void Clear()
        {
            _nodes.Clear();
            _nextId = 0;
        }

        // ============================================================
        //  描述生成
        // ============================================================

        public static string EvidenceDescription(EvidenceType type, ulong discovererId)
        {
            string discoverer = discovererId > 0 ? $"玩家{discovererId}" : "未知来源";
            switch (type)
            {
                case EvidenceType.Footprint: return $"{discoverer}发现了可疑足迹";
                case EvidenceType.Bloodstain: return $"{discoverer}在犯罪现场发现了血迹";
                case EvidenceType.WeaponTrace: return $"{discoverer}从尸体上提取了武器痕迹";
                case EvidenceType.AlibiBreak: return $"{discoverer}发现了不在场证明的破绽";
                case EvidenceType.TransactionRecord: return $"{discoverer}查获了可疑交易记录";
                case EvidenceType.SurveillanceFootage: return $"{discoverer}从监控录像中获取了线索";
                default: return $"{discoverer}发现了一条证据";
            }
        }

        /// <summary>
        /// 生成会议证据摘要（供 CaseLogUI 使用）。
        /// </summary>
        public string MeetingEvidenceSummary(ulong localPlayerId)
        {
            var myNodes = GetDiscovererNodes(localPlayerId);
            if (myNodes.Count == 0)
                return "你尚未收集任何证据。";

            int strength = TotalChainStrength(myNodes);
            string level = strength >= 6 ? "强" : strength >= 3 ? "中" : "弱";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"📋 你的证据链 ({level}, 强度{strength}):");
            foreach (var n in myNodes)
                sb.AppendLine($"  • {n.Description} [{EvidenceTypeName(n.Type)}]");
            sb.AppendLine($"共 {myNodes.Count} 条证据");
            return sb.ToString();
        }

        public static string EvidenceTypeName(EvidenceType type)
        {
            switch (type)
            {
                case EvidenceType.Footprint: return "足迹";
                case EvidenceType.Bloodstain: return "血迹";
                case EvidenceType.WeaponTrace: return "武器痕迹";
                case EvidenceType.AlibiBreak: return "不在场破绽";
                case EvidenceType.TransactionRecord: return "交易记录";
                case EvidenceType.SurveillanceFootage: return "监控录像";
                default: return "未知";
            }
        }
    }
}
