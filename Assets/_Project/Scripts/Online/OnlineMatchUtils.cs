using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using GanglandUndercover.Audio;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Pure-static helpers extracted from OnlineMatchController partial files.
    /// </summary>
    public static class OnlineMatchUtils
    {
        // ── From OnlineMatchController.Gameplay.cs ──────────────────────

        internal static int CorrectTaskStepInput(int taskId, int step)
        {
            switch (TaskTemplateMode(taskId))
            {
                case 0:
                    return new[] { 1, 3, 2 }[Mathf.Clamp(step, 0, 2)];
                case 1:
                    return new[] { 2, 1, 3 }[Mathf.Clamp(step, 0, 2)];
                case 2:
                    return new[] { 3, 2, 1 }[Mathf.Clamp(step, 0, 2)];
                case 3:
                    return new[] { 1, 2, 3 }[Mathf.Clamp(step, 0, 2)];
                case 4:
                    return new[] { 2, 3, 1 }[Mathf.Clamp(step, 0, 2)];
                default:
                    return new[] { 3, 1, 2 }[Mathf.Clamp(step, 0, 2)];
            }
        }

        internal static float TaskChargeRate(int taskId)
        {
            switch (TaskTemplateMode(taskId))
            {
                case 0:
                    return 0.58f;
                case 1:
                    return 0.72f;
                case 2:
                    return 0.68f;
                case 3:
                    return 0.56f;
                case 4:
                    return 0.76f;
                default:
                    return 0.62f;
            }
        }

        internal static int EvidenceMilestoneFor(int score, int target)
        {
            if (target <= 0)
            {
                return 0;
            }

            float ratio = score / (float)target;

            if (ratio >= 1f)
            {
                return 4;
            }

            if (ratio >= 0.75f)
            {
                return 3;
            }

            if (ratio >= 0.5f)
            {
                return 2;
            }

            return ratio >= 0.25f ? 1 : 0;
        }

        internal static SabotageType SabotageForTask(int taskId)
        {
            switch (taskId)
            {
                case 2:
                case 14:
                    return SabotageType.Blackout;
                case 7:
                case 12:
                    return SabotageType.Lockdown;
                case 6:
                case 13:
                    return SabotageType.Communications;
                case 3:
                case 11:
                case 16:
                    return SabotageType.EvidenceLeak;
                case 4:
                case 10:
                case 17:
                case 24:
                case 26:
                    return SabotageType.PatrolAlert;
                case 20:
                case 21:
                case 27:
                    return SabotageType.Communications;
                case 22:
                case 23:
                case 25:
                    return SabotageType.EvidenceLeak;
                default:
                    return SabotageType.None;
            }
        }

        internal static int SabotageEvidencePenalty(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.EvidenceLeak:
                    return 2;
                case SabotageType.Blackout:
                case SabotageType.Lockdown:
                case SabotageType.Communications:
                    return 1;
                default:
                    return 0;
            }
        }

        internal static string EvidenceMilestoneName(int milestone)
        {
            switch (milestone)
            {
                case 1:
                    return "初步锁线";
                case 2:
                    return "重点盘问";
                case 3:
                    return "接近结案";
                case 4:
                    return "证据闭合";
                default:
                    return "摸排中";
            }
        }

        internal static string SabotageName(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.Blackout:
                    return "黑灯";
                case SabotageType.Lockdown:
                    return "封锁";
                case SabotageType.Communications:
                    return "断讯";
                case SabotageType.EvidenceLeak:
                    return "泄证";
                case SabotageType.PatrolAlert:
                    return "巡逻";
                default:
                    return "普通";
            }
        }

        internal static string TaskPanelTemplateTitle(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "监控追踪";
                case 1:
                case 10:
                case 23:
                    return "货柜查验";
                case 2:
                case 14:
                case 24:
                    return "电力修复";
                case 3:
                case 15:
                    return "证物鉴证";
                case 4:
                case 11:
                case 16:
                case 22:
                    return "档案账本";
                case 5:
                case 27:
                    return "接头安全";
                case 6:
                case 13:
                case 21:
                    return "通讯监听";
                case 7:
                case 12:
                    return "门禁封控";
                case 8:
                case 18:
                case 26:
                    return "巡线取证";
                case 9:
                case 19:
                    return "诊所搜查";
                case 17:
                    return "街口执勤";
                case 20:
                    return "鱼档暗号";
                case 25:
                    return "后巷排查";
                default:
                    return "现场任务";
            }
        }

        internal static string TaskPanelTemplateSubtitle(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "多屏比对 / 导出线索";
                case 1:
                case 10:
                case 23:
                    return "封条核验 / 货单比对";
                case 2:
                case 14:
                case 24:
                    return "断路恢复 / 电网重启";
                case 3:
                case 15:
                    return "样本扫描 / 证据归档";
                case 4:
                case 11:
                case 16:
                case 22:
                    return "账目追踪 / 异常冻结";
                case 5:
                case 27:
                    return "短接传递 / 风险控制";
                case 6:
                case 13:
                case 21:
                    return "锁频过滤 / 信号回收";
                case 7:
                case 12:
                    return "刷卡开闸 / 通道清理";
                case 8:
                case 18:
                case 26:
                    return "路线校验 / 目击补强";
                case 9:
                case 19:
                    return "现场搜查 / 痕迹比对";
                case 17:
                    return "巡逻打卡 / 风险压制";
                case 20:
                    return "暗号识别 / 交易追踪";
                case 25:
                    return "摩托排查 / 后路封锁";
                default:
                    return "证据推进 / 风险判断";
            }
        }

        internal static string TaskPanelFooter(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "监控面板优先看路线";
                case 1:
                case 23:
                    return "货柜越多，假线索越容易藏";
                case 2:
                case 14:
                    return "电力恢复会重开部分视野";
                case 4:
                case 16:
                case 22:
                    return "账本任务更容易拉高证据链";
                case 6:
                case 13:
                case 21:
                    return "通讯越乱，黑帮越容易行动";
                case 7:
                case 12:
                    return "门禁任务适合配合追捕";
                case 8:
                case 18:
                case 26:
                    return "巡线任务会给路线压力";
                default:
                    return "完成后会推进整局节奏";
            }
        }

        internal static void DrawTaskScreenGrid(Rect rect)
        {
            Color oldColor = GUI.color;

            for (int i = 0; i < 6; i++)
            {
                float column = i % 3;
                float row = i / 3;
                Rect screen = new Rect(rect.x + 18f + column * (rect.width - 56f) / 3f, rect.y + 14f + row * 42f, (rect.width - 78f) / 3f, 30f);
                GUI.color = i % 2 == 0 ? new Color(0.06f, 0.42f, 0.52f, 1f) : new Color(0.08f, 0.22f, 0.28f, 1f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = new Color(0.1f, 0.9f, 0.95f, 1f);
                GUI.DrawTexture(new Rect(screen.x + 8f, screen.y + 8f, screen.width * 0.62f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(screen.x + 8f, screen.y + 17f, screen.width * 0.42f, 3f), Texture2D.whiteTexture);
            }

            GUI.color = oldColor;
        }

        internal static void DrawTaskSealScanner(Rect rect)
        {
            Color oldColor = GUI.color;
            Rect belt = new Rect(rect.x + 20f, rect.y + rect.height * 0.48f, rect.width - 40f, 18f);
            GUI.color = new Color(0.12f, 0.14f, 0.15f, 1f);
            GUI.DrawTexture(belt, Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.72f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 44f, rect.y + 22f, rect.width - 88f, 18f), Texture2D.whiteTexture);

            for (int i = 0; i < 5; i++)
            {
                GUI.color = i <= 2 ? new Color(0.1f, 0.72f, 0.84f, 1f) : new Color(0.34f, 0.36f, 0.34f, 1f);
                GUI.DrawTexture(new Rect(rect.x + 46f + i * 54f, rect.y + 66f, 34f, 14f), Texture2D.whiteTexture);
            }

            GUI.color = oldColor;
        }

        internal static void DrawTaskBreakerWidget(Rect rect)
        {
            Color oldColor = GUI.color;
            float startX = rect.x + rect.width * 0.28f;

            for (int i = 0; i < 4; i++)
            {
                Rect slot = new Rect(startX + i * 58f, rect.y + 20f, 18f, rect.height - 42f);
                GUI.color = new Color(0.12f, 0.16f, 0.18f, 1f);
                GUI.DrawTexture(slot, Texture2D.whiteTexture);
                GUI.color = i == 2 ? new Color(0.9f, 0.1f, 0.06f, 1f) : new Color(0.16f, 0.72f, 0.32f, 1f);
                GUI.DrawTexture(new Rect(slot.x - 10f, slot.y + 18f + i * 7f, 38f, 10f), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.92f, 0.74f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 28f, rect.y + rect.height - 34f, rect.width - 56f, 4f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        internal static void DrawTaskEvidenceTray(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.74f, 0.78f, 0.72f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 32f, rect.y + 26f, rect.width - 64f, rect.height - 52f), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 48f, rect.y + 42f, rect.width - 96f, rect.height - 84f), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.68f, 0.82f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 58f, rect.y + 54f, rect.width - 116f, 5f), Texture2D.whiteTexture);
            GUI.color = new Color(0.82f, 0.14f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.38f, rect.y + 68f, 46f, 14f), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.76f, 0.16f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.56f, rect.y + 72f, 34f, 10f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        internal static void DrawTaskLedgerWidget(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.16f, 0.12f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 24f, rect.y + 18f, rect.width - 48f, rect.height - 36f), Texture2D.whiteTexture);
            GUI.color = new Color(0.86f, 0.76f, 0.54f, 1f);

            for (int i = 0; i < 5; i++)
            {
                float y = rect.y + 28f + i * 15f;
                GUI.DrawTexture(new Rect(rect.x + 42f, y, rect.width * 0.42f, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x + rect.width * 0.62f, y, rect.width * 0.22f, 4f), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.12f, 0.62f, 0.28f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.54f, rect.y + 72f, 72f, 12f), Texture2D.whiteTexture);
            GUI.color = new Color(0.92f, 0.12f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.26f, rect.y + 54f, 52f, 10f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        internal static void DrawTaskRouteWidget(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.11f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 18f, rect.y + 18f, rect.width - 36f, rect.height - 36f), Texture2D.whiteTexture);
            GUI.color = new Color(0.42f, 0.62f, 0.66f, 1f);

            for (int i = 0; i < 4; i++)
            {
                float x = rect.x + 54f + i * (rect.width - 120f) / 3f;
                GUI.DrawTexture(new Rect(x, rect.y + 28f, 7f, rect.height - 54f), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.9f, 0.7f, 0.1f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 58f, rect.y + 76f, rect.width - 116f, 5f), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.72f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.34f, rect.y + 46f, 44f, 12f), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.08f, 0.06f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.68f, rect.y + 72f, 38f, 12f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        internal static void DrawProgressBar(Rect rect, float progress, Color fillColor)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.06f, 0.07f, 0.08f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, Mathf.Max(0f, rect.width - 4f) * Mathf.Clamp01(progress), Mathf.Max(0f, rect.height - 4f)), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        internal static string TaskMapCode(int taskId)
        {
            string title = TaskPanelTemplateTitle(taskId);

            if (string.IsNullOrEmpty(title))
            {
                return "T" + taskId;
            }

            return "T" + taskId + " " + ShortDisplayName(title, 2);
        }

        internal static string ShortDisplayName(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string safeValue = value.Trim();
            return safeValue.Length <= maxLength ? safeValue : safeValue.Substring(0, maxLength);
        }

        internal static string OpeningRouteStatus(int index)
        {
            switch (index)
            {
                case 0:
                    return "货柜/巡线";
                case 1:
                    return "录像/通话";
                case 2:
                    return "线人/暗号";
                case 3:
                    return "账本/赃款";
                default:
                    return "鉴证/结案";
            }
        }

        internal static void ApplyHudSkin()
        {
            int baseSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 72f), 12, 15);
            GUI.skin.label.fontSize = baseSize;
            GUI.skin.button.fontSize = baseSize;
            GUI.skin.textField.fontSize = baseSize;
            GUI.skin.toggle.fontSize = baseSize;
            GUI.skin.box.fontSize = baseSize;
            GUI.skin.label.wordWrap = true;
        }

        internal static void DrawResultBar(Rect rect, float ratio, Color color, string label)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.12f, 0.14f, 0.15f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y - 1f, rect.width - 16f, rect.height + 4f), label);
            GUI.color = oldColor;
        }

        internal static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }
        }

        internal static Vector3 RotateOffset(Vector3 offset, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector3(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos, offset.z);
        }

        internal static string LimitText(string value, int maxLength, string fallback)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

            if (safeValue.Length > maxLength)
            {
                safeValue = safeValue.Substring(0, maxLength);
            }

            return safeValue;
        }

        internal static string CleanRelayJoinInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string safeValue = value.Trim().ToUpperInvariant();

            if (safeValue.Length > 12)
            {
                safeValue = safeValue.Substring(0, 12);
            }

            return safeValue;
        }

        internal static string BuildRelayLobbySummary(
            string relayStatusValue,
            string relayJoinCodeValue,
            string relayJoinInputValue,
            bool operationInProgress,
            bool isOnline,
            bool isHost,
            bool isClientConnected,
            int connectedClientCount)
        {
            string safeStatus = string.IsNullOrWhiteSpace(relayStatusValue)
                ? "Relay 房间码未创建。"
                : relayStatusValue.Trim();
            string safeJoinCode = CleanRelayJoinInput(relayJoinCodeValue);
            string safeJoinInput = CleanRelayJoinInput(relayJoinInputValue);
            StringBuilder builder = new StringBuilder();
            builder.Append(safeStatus);

            if (operationInProgress)
            {
                if (string.IsNullOrEmpty(safeJoinInput))
                {
                    builder.Append("\n房间码流程: 正在创建房间码，请稍候。");
                }
                else
                {
                    builder.Append("\n房间码流程: 正在加入 ").Append(safeJoinInput).Append("，请稍候。");
                }

                builder.Append("\n晚测记录: 超过 20 秒无变化就截图本行、房间码和 Console。");
                return builder.ToString();
            }

            if (!string.IsNullOrEmpty(safeJoinCode) && safeStatus.Contains("Host 已断开"))
            {
                builder.Append("\n房间码: ").Append(safeJoinCode).Append("。");
                builder.Append("\n断线处理: 旧房间码已失效；返回主菜单后由 Host 重新开房，再发新码。");
                builder.Append("\n晚测记录: 截图本状态栏、系统时间和双方最后一步。");
                return builder.ToString();
            }

            if (isOnline && isHost && !string.IsNullOrEmpty(safeJoinCode))
            {
                int visibleClientCount = Mathf.Max(1, connectedClientCount);
                builder.Append("\n房主: 分享房间码 ").Append(safeJoinCode)
                    .Append(" | 已连接 ").Append(visibleClientCount).Append(" 人。");
                builder.Append("\n晚测记录: 截图房间码和人数，朋友加入后再截图玩家列表。");
                return builder.ToString();
            }

            if (isOnline && isClientConnected && !string.IsNullOrEmpty(safeJoinCode))
            {
                builder.Append("\nClient: 已加入房间码 ").Append(safeJoinCode)
                    .Append("，等待 Host 开局。");
                builder.Append("\n晚测记录: 截图玩家列表和 Ready 状态，若 Host 看不到你就记录时间。");
                return builder.ToString();
            }

            if (!string.IsNullOrEmpty(safeJoinInput))
            {
                builder.Append("\nClient: 已输入房间码 ").Append(safeJoinInput)
                    .Append("，点击 Relay 加入。");
                builder.Append("\n提示: 确认房间码为 6 位大写字母数字；加入失败就记录错误提示。");
                return builder.ToString();
            }

            if (!string.IsNullOrEmpty(safeJoinCode))
            {
                builder.Append("\n房间码: ").Append(safeJoinCode).Append("。");
                builder.Append("\n晚测记录: 把房间码和当前状态一起截图。");
                return builder.ToString();
            }

            builder.Append("\n下一步: Host 点击 Relay 开房生成房间码；Client 输入房间码加入。");
            builder.Append("\n晚测记录: 每次失败请截图本状态栏和当前步骤编号。");
            return builder.ToString();
        }

        internal static OnlineProfession ProfessionFor(OnlineRole role, int index)
        {
            // 内鬼：公开为警察，分配警察职业维持掩护
            if (role == OnlineRole.Mole)
            {
                OnlineProfession[] moleCoverProfessions =
                {
                    OnlineProfession.Tech,       // 技术员可访问监控最不易暴露
                    OnlineProfession.Forensics,
                    OnlineProfession.Inspector,
                };
                return moleCoverProfessions[index % moleCoverProfessions.Length];
            }

            // 黑帮：打手/清道夫
            if (role == OnlineRole.Gang)
            {
                OnlineProfession[] gangProfessions =
                {
                    OnlineProfession.Enforcer,
                    OnlineProfession.Fixer,
                };
                return gangProfessions[index % gangProfessions.Length];
            }

            // 卧底：卧底特工/车手
            if (role == OnlineRole.Undercover)
            {
                OnlineProfession[] undercoverProfessions =
                {
                    OnlineProfession.UndercoverAgent,
                    OnlineProfession.Driver,
                };
                return undercoverProfessions[index % undercoverProfessions.Length];
            }

            // 警察：督察/法医/技术员
            OnlineProfession[] policeProfessions =
            {
                OnlineProfession.Inspector,
                OnlineProfession.Forensics,
                OnlineProfession.Tech
            };
            return policeProfessions[index % policeProfessions.Length];
        }

        internal static string RoleName(OnlineRole role)
        {
            switch (role)
            {
                case OnlineRole.Police:
                    return "警方";
                case OnlineRole.Undercover:
                    return "卧底";
                case OnlineRole.Gang:
                    return "黑帮";
                case OnlineRole.Mole:
                    return "线人";
                default:
                    return "未分配";
            }
        }

        internal static string ProfessionName(OnlineProfession profession)
        {
            switch (profession)
            {
                case OnlineProfession.Inspector:
                    return "督察";
                case OnlineProfession.Forensics:
                    return "法证";
                case OnlineProfession.Tech:
                    return "技术";
                case OnlineProfession.UndercoverAgent:
                    return "卧底";
                case OnlineProfession.Enforcer:
                    return "打手";
                case OnlineProfession.Fixer:
                    return "善后";
                case OnlineProfession.Driver:
                    return "车手";
                case OnlineProfession.Mole:
                    return "内鬼";
                default:
                    return "未知";
            }
        }

        internal static string PhaseName(OnlineMatchPhase matchPhase)
        {
            switch (matchPhase)
            {
                case OnlineMatchPhase.Lobby:
                    return "房间";
                case OnlineMatchPhase.Opening:
                    return "简报";
                case OnlineMatchPhase.Action:
                    return "行动";
                case OnlineMatchPhase.Meeting:
                    return "会议";
                case OnlineMatchPhase.Voting:
                    return "投票";
                case OnlineMatchPhase.Result:
                    return "结算";
                default:
                    return "未知";
            }
        }

        internal static int TaskRequiredProgress(int taskId)
        {
            return OnlineTaskService.TaskRequiredProgress(taskId);
        }

        internal static bool CircleIntersectsRect(Vector3 center, float radius, Rect rect)
        {
            float nearestX = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
            float nearestY = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
            float dx = center.x - nearestX;
            float dy = center.y - nearestY;
            return dx * dx + dy * dy < radius * radius;
        }

        // ── From OnlineMatchController.cs (main file) ───────────────────

        internal static List<GameStateSnapshot.SnapshotCooldownEntry> CooldownsToList(IReadOnlyDictionary<ulong, float> dict)
        {
            var list = new List<GameStateSnapshot.SnapshotCooldownEntry>(dict.Count);
            foreach (var kv in dict)
            {
                list.Add(new GameStateSnapshot.SnapshotCooldownEntry { ClientId = kv.Key, Value = kv.Value });
            }
            return list;
        }

        internal static void ListToCooldowns(Dictionary<ulong, float> dict, List<GameStateSnapshot.SnapshotCooldownEntry> list)
        {
            dict.Clear();
            foreach (var entry in list)
            {
                dict[entry.ClientId] = entry.Value;
            }
        }

        internal static int TaskEvidenceValue(int taskId)
        {
            switch (taskId)
            {
                case 0:
                case 3:
                case 11:
                case 15:
                case 16:
                case 21:
                case 22:
                case 26:
                    return 2;
                case 4:
                case 8:
                case 18:
                case 24:
                case 27:
                    return 3;
                default:
                    return 1;
            }
        }

        internal static string FormatMatchTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }

        internal static int TaskTemplateMode(int taskId)
        {
            switch (taskId)
            {
                case 0:
                case 6:
                case 13:
                case 21:
                    return 0;
                case 1:
                case 10:
                case 20:
                case 23:
                    return 1;
                case 2:
                case 7:
                case 12:
                case 14:
                case 24:
                    return 2;
                case 3:
                case 9:
                case 15:
                case 19:
                    return 3;
                case 4:
                case 11:
                case 16:
                case 22:
                    return 4;
                default:
                    return 5;
            }
        }

        internal static Color TaskPanelAccent(int taskId)
        {
            return OnlineWorldBuilder.TaskPanelAccent(taskId);
        }

        internal static string TaskPanelInstruction(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "切换摄像头、锁定可疑动线、导出录像。";
                case 1:
                case 23:
                    return "核对封条号、扫描货柜、同步查验记录。";
                case 2:
                case 14:
                case 24:
                    return "对齐断路器、按住充电、恢复港区供电。";
                case 3:
                case 15:
                    return "放置样本、校准光谱、生成鉴证报告。";
                case 4:
                case 16:
                case 22:
                    return "翻账本、标记异常、冻结可疑现金流。";
                case 5:
                case 27:
                    return "递送情报、控制暴露、稳住接头安全。";
                case 6:
                case 13:
                case 21:
                    return "调频、过滤噪声、恢复无线电通道。";
                case 7:
                case 12:
                    return "刷卡、解除门禁、记录出入日志。";
                case 8:
                case 18:
                case 26:
                    return "巡线、补充目击、锁定撤离路线。";
                case 9:
                case 19:
                    return "搜查诊所、对照病历、追痕提证。";
                case 10:
                    return "顺线走访货场，补强路线证据。";
                case 11:
                    return "核对财务流向，锁定异常资金。";
                case 17:
                    return "执行巡逻打卡，压制高风险街口。";
                case 20:
                    return "读懂鱼档暗号，辨识黑市交易。";
                case 25:
                    return "排查后巷摩托，封死逃逸支线。";
                default:
                    return "完成现场校验并提交证据链。";
            }
        }

        internal static void RemoveStaleVisuals<T>(Dictionary<T, GameObject> visuals, HashSet<T> seen)
        {
            OnlineWorldBuilder.RemoveStaleVisuals(visuals, seen);
        }

        internal static int SortingOrderForZ(float z)
        {
            return OnlineWorldBuilder.SortingOrderForZ(z);
        }

        internal static int SortingOrderForLocalZ(float z)
        {
            return OnlineWorldBuilder.SortingOrderForLocalZ(z);
        }

        internal static bool TryResolveSoundEffectCue(string cueName, out SoundEffect effect)
        {
            switch (cueName)
            {
                case "task":
                    effect = SoundEffect.TaskComplete;
                    return true;
                case "kill":
                    effect = SoundEffect.Kill;
                    return true;
                case "meeting":
                    effect = SoundEffect.MeetingStart;
                    return true;
                case "vote":
                    effect = SoundEffect.VoteCast;
                    return true;
                case "eliminated":
                    effect = SoundEffect.PlayerEliminated;
                    return true;
                case "blackout":
                    effect = SoundEffect.Emergency;
                    return true;
                case "vent":
                    effect = SoundEffect.VentOpen;
                    return true;
                case "result":
                    effect = SoundEffect.Victory;
                    return true;
                default:
                    effect = default;
                    return false;
            }
        }

        internal static AudioClip CreateToneClip(string clipName, float frequency, float duration)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = 1f - i / (float)sampleCount;
                samples[i] = Mathf.Sin(time * frequency * Mathf.PI * 2f) * 0.28f * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        internal static void Shuffle<T>(IList<T> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, items.Count);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
