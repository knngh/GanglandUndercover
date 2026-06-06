using UnityEngine;
using GanglandUndercover;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {

        // --- TryUseUnderworldPassage ---
        private bool TryUseUnderworldPassage(ulong senderClientId, OnlinePlayerState player)
        {
            // 1. 权限检查：仅 Gang/Mole 可用
            OnlineRole role = GetPrivateRole(senderClientId);
            if (role != OnlineRole.Gang && role != OnlineRole.Mole)
            {
                status = player.DisplayName + " 试图使用暗线通道但权限不足（非黑帮）。";
                BroadcastSnapshot();
                return false;
            }

            // 2. 存活检查
            if (!player.Alive)
            {
                return false;
            }

            // 3. 冷却检查
            if (ventCooldowns.TryGetValue(senderClientId, out float cooldown) && cooldown > 0f)
            {
                status = "暗线通道冷却中：" + Mathf.CeilToInt(cooldown) + "s";
                BroadcastSnapshot();
                return false;
            }

            // 4. 找到最近的暗线节点
            Vector3 current = player.Position;
            int nearestIdx = -1;
            float nearestDist = ruleSet.UnderworldTransitRange;

            for (int i = 0; i < ruleSet.UnderworldPassageCount; i++)
            {
                Vector3 node = mapService.UnderworldPassagePosition(i, ruleSet.UnderworldPassageCount);
                float dist = Vector3.Distance(current, node);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIdx = i;
                }
            }

            if (nearestIdx < 0)
            {
                status = player.DisplayName + " 附近没有暗线通道入口。";
                BroadcastSnapshot();
                return false;
            }

            // 5. 选择目标节点（对侧节点）
            int targetIdx = (nearestIdx + 2) % ruleSet.UnderworldPassageCount;
            Vector3 exit = mapService.UnderworldPassagePosition(targetIdx, ruleSet.UnderworldPassageCount);
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.32f, 0.32f), UnityEngine.Random.Range(-0.24f, 0.24f), 0f);
            Vector3 destination = FindNearestOpenPosition(exit + offset, exit);

            // 6. 执行瞬移
            player.Position = destination;
            ventCooldowns[senderClientId] = ruleSet.VentCooldownSeconds;
            player.VentCooldown = ruleSet.VentCooldownSeconds;
            players[senderClientId] = player;

            string msg = player.DisplayName + " 通过暗线通道从节点 " + nearestIdx + " 瞬移到节点 " + targetIdx + "。";
            status = msg;
            AddCaseLog(msg);
            PlayCue("vent");
            BroadcastSnapshot();
            return true;
        }

        // --- TryUseUnderworldPassage ---
        private bool TryUseUnderworldPassage(ref OnlinePlayerState player)
        {
            // 查找 player 对应的 clientId
            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (ReferenceEquals(pair.Value, player) || pair.Value.ClientId == player.ClientId)
                {
                    return TryUseUnderworldPassage(pair.Key, player);
                }
            }

            // fallback：找不到对应 clientId，直接返回 false
            return false;
        }

        // --- IsNearUnderworldPassage ---
        private bool IsNearUnderworldPassage(Vector3 position)
        {
            for (int i = 0; i < ruleSet.UnderworldPassageCount; i++)
            {
                if (Vector3.Distance(position, mapService.UnderworldPassagePosition(i, ruleSet.UnderworldPassageCount)) <= ruleSet.UnderworldTransitRange)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
