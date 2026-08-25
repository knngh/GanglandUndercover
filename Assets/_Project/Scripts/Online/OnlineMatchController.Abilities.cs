using System.Collections.Generic;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Phase 3.2: 职业能力运行时集成。
    /// 把 OnlineRuleSet 中定义的能力接入实际游戏逻辑。
    /// </summary>
    public sealed partial class OnlineMatchController
    {
        // ============================================================
        //  能力状态
        // ============================================================

        /// <summary>暗视激活的玩家（Enforcer DarkVision）</summary>
        private readonly HashSet<ulong> _darkVisionActive = new HashSet<ulong>();
        private readonly Dictionary<ulong, float> _darkVisionRemaining = new Dictionary<ulong, float>();
        private const float DarkVisionDurationSeconds = 6f;

        /// <summary>尸体检验累积（Forensics CorpseExamine）</summary>
        private readonly Dictionary<ulong, int> _corpseExamineBonus = new Dictionary<ulong, int>();

        /// <summary>足迹数据（Inspector FootprintTrack）</summary>
        private readonly List<FootprintMark> _footprints = new List<FootprintMark>();
        private string _lastProfessionAbilityFeedback = string.Empty;

        public string LastProfessionAbilityFeedback => _lastProfessionAbilityFeedback;

        public struct FootprintMark
        {
            public Vector2 Position;
            public float ExpireTime;
            public ulong SourceId;
        }

        // ============================================================
        //  能力检查辅助
        // ============================================================

        private bool HasAbility(ulong clientId, AbilityType type)
        {
            if (ruleSet == null) return false;
            if (!players.TryGetValue(clientId, out var state)) return false;
            return ruleSet.HasAbility(state.Profession, type);
        }

        private float GetAbilityMult(ulong clientId, AbilityType type, float defaultVal = 1f)
        {
            if (ruleSet == null) return defaultVal;
            if (!players.TryGetValue(clientId, out var state)) return defaultVal;
            return ruleSet.GetAbilityMultiplier(state.Profession, type);
        }

        // ============================================================
        //  BodyDrag — Fixer: 拖动尸体
        // ============================================================

        /// <summary>检查玩家是否可以拖动尸体。Fixer 专属。</summary>
        public bool CanDragBody(ulong clientId)
        {
            return HasAbility(clientId, AbilityType.BodyDrag);
        }

        /// <summary>尝试拖动最近尸体到新位置。</summary>
        public bool TryDragBody(ulong clientId, Vector3 targetPosition)
        {
            if (!CanDragBody(clientId)) return false;
            if (!players.TryGetValue(clientId, out var player)) return false;
            if (killSystem == null) return false;

            var bodies = killSystem.bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body.Reported) continue;
                float dist = Vector3.Distance(player.Position,
                    new Vector3(body.Position.x, body.Position.y, 0));
                if (dist <= 1.5f)
                {
                    body.Position = new Vector2(targetPosition.x, targetPosition.y);
                    bodies[i] = body;
                    killSystem.UpdateBodyVisuals();
                    return true;
                }
            }
            return false;
        }

        // ============================================================
        //  DarkVision — Enforcer: 短暂穿墙轮廓
        // ============================================================

        public bool IsDarkVisionActive(ulong clientId) => _darkVisionActive.Contains(clientId);

        public void ActivateDarkVision(ulong clientId)
        {
            if (!HasAbility(clientId, AbilityType.DarkVision)) return;
            _darkVisionActive.Add(clientId);
            _darkVisionRemaining[clientId] = DarkVisionDurationSeconds;
        }

        public void DeactivateDarkVision(ulong clientId)
        {
            _darkVisionActive.Remove(clientId);
            _darkVisionRemaining.Remove(clientId);
        }

        // ============================================================
        //  FootprintTrack — Inspector: 地面足迹
        // ============================================================

        public void LeaveFootprint(ulong clientId)
        {
            if (!players.TryGetValue(clientId, out var state)) return;
            _footprints.Add(new FootprintMark
            {
                Position = new Vector2(state.Position.x, state.Position.y),
                ExpireTime = matchElapsedSeconds + 5f,
                SourceId = clientId
            });
        }

        public IReadOnlyList<FootprintMark> VisibleFootprints(ulong viewerId)
        {
            if (!HasAbility(viewerId, AbilityType.FootprintTrack))
                return System.Array.Empty<FootprintMark>();

            // 清理过期足迹
            _footprints.RemoveAll(f => matchElapsedSeconds > f.ExpireTime);
            return _footprints;
        }

        // ============================================================
        //  CorpseExamine — Forensics: 尸体检验额外线索
        // ============================================================

        public int CorpseExamineBonus(ulong examinerId)
        {
            if (!HasAbility(examinerId, AbilityType.CorpseExamine)) return 0;
            float bonus = ruleSet.GetAbilityBonus(OnlineProfession.Forensics, AbilityType.CorpseExamine);
            return Mathf.RoundToInt(bonus);
        }

        // ============================================================
        //  TaskSpeedBonus — Tech: 任务速度加成
        // ============================================================

        public float TaskSpeedMultiplier(ulong clientId)
        {
            if (!HasAbility(clientId, AbilityType.TaskSpeedBonus)) return 1f;
            return ruleSet.GetAbilityMultiplier(OnlineProfession.Tech, AbilityType.TaskSpeedBonus);
        }

        // ============================================================
        //  VentSpeedBonus — Driver: 暗线/通风管速度
        // ============================================================

        public float VentCooldownMultiplier(ulong clientId)
        {
            if (!HasAbility(clientId, AbilityType.VentSpeedBonus)) return 1f;
            return ruleSet.GetAbilityMultiplier(OnlineProfession.Driver, AbilityType.VentSpeedBonus);
        }

        // ============================================================
        //  SabotageCooldownReduce — Fixer/Mole: 破坏冷却
        // ============================================================

        public float SabotageCooldownMultiplier(ulong clientId)
        {
            if (!HasAbility(clientId, AbilityType.SabotageCooldownReduce)) return 1f;
            if (!players.TryGetValue(clientId, out var state)) return 1f;
            return ruleSet.GetAbilityMultiplier(state.Profession, AbilityType.SabotageCooldownReduce);
        }

        // ============================================================
        //  MoleIntel — Mole: 窃取 Police 任务进度
        // ============================================================

        private readonly Dictionary<ulong, int> _moleIntel = new Dictionary<ulong, int>();
        private const int MoleIntelWinThreshold = 5;

        public void AccumulateMoleIntel(ulong moleId, int amount)
        {
            // C1-1: Fixed — removed wrong SabotageCooldownReduce gate, role check is sufficient
            if (!players.TryGetValue(moleId, out var state) || GetPrivateRole(moleId) != OnlineRole.Mole) return;

            if (!_moleIntel.ContainsKey(moleId))
                _moleIntel[moleId] = 0;
            _moleIntel[moleId] += amount;

            if (_moleIntel[moleId] >= MoleIntelWinThreshold)
            {
                AssignMoleHit(moleId);
            }

            SendIdentityProgress(moleId);
        }

        public int GetMoleIntel(ulong moleId)
        {
            return _moleIntel.TryGetValue(moleId, out int val) ? val : 0;
        }

        public bool CheckMoleWinCondition(ulong moleId)
        {
            return CheckMoleSoloWin(moleId);
        }

        // ============================================================
        //  RemoteSurveillance — Tech: 远程查看监控
        // ============================================================

        public bool CanRemoteSurveil(ulong clientId)
        {
            return HasAbility(clientId, AbilityType.RemoteSurveillance);
        }

        private int TrackNearbyFootprints(ulong viewerId)
        {
            if (!players.TryGetValue(viewerId, out OnlinePlayerState viewer)) return 0;

            int tracked = 0;
            foreach (OnlinePlayerState state in players.Values)
            {
                if (!state.Alive || state.ClientId == viewerId) continue;
                if (Vector3.Distance(viewer.Position, state.Position) > 7f) continue;
                LeaveFootprint(state.ClientId);
                tracked++;
            }

            return tracked;
        }

        internal void EmitProfessionAbilityFeedback(
            ulong clientId,
            string abilityKey,
            Color color,
            Vector3 position)
        {
            if (worldRoot == null)
                EnsureWorld();

            if (worldRoot != null && worldBuilder != null)
                worldBuilder.CreateAbilityFeedbackVisual(abilityKey, position, color);

            _lastProfessionAbilityFeedback = abilityKey;
        }

        public bool CanClientWatchCamera(ulong clientId, Vector2 cameraCenter)
        {
            if (phase != OnlineMatchPhase.Action)
                return false;

            if (!players.TryGetValue(clientId, out OnlinePlayerState state) || !state.Alive)
                return false;

            if (CanRemoteSurveil(clientId))
                return true;

            float range = ruleSet != null ? Mathf.Max(2.5f, ruleSet.InteractionRange) : 2.5f;
            Vector2 playerPosition = new Vector2(state.Position.x, state.Position.y);
            return Vector2.Distance(playerPosition, cameraCenter) <= range;
        }

        // ============================================================
        //  清理
        // ============================================================

        public void ClearProfessionAbilities()
        {
            _darkVisionActive.Clear();
            _darkVisionRemaining.Clear();
            _corpseExamineBonus.Clear();
            _footprints.Clear();
            _moleIntel.Clear();
            _lastProfessionAbilityFeedback = string.Empty;
        }

        public void TickProfessionAbilities(float deltaTime)
        {
            if (_darkVisionRemaining.Count > 0)
            {
                List<ulong> expired = new List<ulong>();
                List<ulong> active = new List<ulong>(_darkVisionRemaining.Keys);
                for (int i = 0; i < active.Count; i++)
                {
                    ulong clientId = active[i];
                    float remaining = _darkVisionRemaining[clientId] - Mathf.Max(0f, deltaTime);
                    if (remaining <= 0f)
                        expired.Add(clientId);
                    else
                        _darkVisionRemaining[clientId] = remaining;
                }

                for (int i = 0; i < expired.Count; i++)
                    DeactivateDarkVision(expired[i]);
            }

            _footprints.RemoveAll(f => matchElapsedSeconds > f.ExpireTime);
        }
    }
}
