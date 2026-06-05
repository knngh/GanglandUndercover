using System.Collections.Generic;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机文本聊天通道类型（方案B：三通道 + 鬼魂频道）。
    /// </summary>
    public enum ChatChannel
    {
        /// <summary>会议/投票阶段 — 所有存活玩家可见。</summary>
        Meeting,

        /// <summary>行动阶段全局喊话 — 所有存活玩家可见。</summary>
        Global,

        /// <summary>行动阶段近距离 — 附近存活玩家可见（不分阵营）。</summary>
        Proximity,

        /// <summary>鬼魂频道 — 仅死亡玩家之间可见，活人不可见。</summary>
        Ghost
    }

    /// <summary>
    /// 跨模式聊天系统：管理消息列表、输入、渲染，支持联机四通道。
    ///
    /// M1 收尾状态：联机通信已通过 CustomMessagingManager（OnlineMatchController.SendChatMessage
    /// → ReceiveChatSend → 按通道路由 → ReceiveChatBroadcast）实现，无需改为 NetworkBehaviour。
    ///
    /// 联机模式（OnlineMatchController）：
    ///   - 会议/投票阶段（Meeting）：所有存活玩家可发言，广播全体存活客户端
    ///   - 行动阶段近距离（Proximity）：附近 12f 内存活玩家可见，服务器端距离判定
    ///   - 行动阶段全局（Global）：所有存活玩家可见
    ///   - 鬼魂频道（Ghost）：仅死亡玩家之间可见
    ///
    /// 离线模式（SocialPrototypeController）：
    ///   - 会议阶段：玩家可输入，AI 自动发送预设消息
    ///   - 非会议阶段：无聊天功能
    /// </summary>
    public class ChatSystem
    {
        private const int MaxMessages = 50;
        private const float SendCooldown = 5.0f;   // F3: 发言冷却 5 秒
        private const int MaxMessageLength = 256;   // F3: 消息长度限制 256 字符

        /// <summary>近距离聊天半径（世界单位，x/y 平面距离）。</summary>
        public const float ProximityRadius = 15f;

        private readonly List<ChatMessage> messages = new List<ChatMessage>();
        private readonly System.Action<string> sendCallback;
        private string inputBuffer = string.Empty;
        private bool isInputActive;
        private Vector2 scrollPosition;
        private float lastSendTime;

        /// <summary>当前是否允许发送消息。</summary>
        public bool CanSend { get; set; }

        /// <summary>本地玩家阵营（用于着色）。</summary>
        public Faction LocalFaction { get; set; } = Faction.None;

        /// <summary>当前游戏阶段。</summary>
        public OnlineMatchPhase CurrentPhase { get; set; } = OnlineMatchPhase.Lobby;

        /// <summary>玩家是否存活。</summary>
        public bool IsAlive { get; set; } = true;

        /// <summary>当前聊天通道（根据阶段和存活状态自动判定）。</summary>
        public ChatChannel CurrentChannel => DetermineChannel(CurrentPhase, IsAlive);

        /// <summary>根据阶段和存活状态判定聊天通道。</summary>
        public static ChatChannel DetermineChannel(OnlineMatchPhase phase, bool isAlive)
        {
            if (!isAlive)
                return ChatChannel.Ghost;

            if (phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting)
                return ChatChannel.Meeting;

            // 行动阶段默认近距离
            return ChatChannel.Proximity;
        }

        /// <summary>获取当前通道的简体中文名称。</summary>
        public static string ChannelDisplayName(ChatChannel channel)
        {
            switch (channel)
            {
                case ChatChannel.Meeting:
                    return "会议聊天";
                case ChatChannel.Global:
                    return "全局喊话";
                case ChatChannel.Proximity:
                    return "近距离聊天";
                case ChatChannel.Ghost:
                    return "鬼魂频道";
                default:
                    return "聊天";
            }
        }

        public ChatSystem(System.Action<string> sendCallback)
        {
            this.sendCallback = sendCallback;
        }

        // ─── 限流与安全 ─────────────────────────────

        /// <summary>检查是否已达发送冷却。每秒最多 1 条。</summary>
        public bool CanSendNow()
        {
            return Time.time - lastSendTime >= SendCooldown;
        }

        /// <summary>标记已发送，更新冷却时间。</summary>
        public void MarkSent()
        {
            lastSendTime = Time.time;
        }

        /// <summary>清理输入内容：去除 HTML 标签、截断超长。</summary>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // 先整段移除危险元素及其内部内容（script/style 等），再去除其余 < > 标签（防 XSS）
            string sanitized = System.Text.RegularExpressions.Regex.Replace(
                input,
                @"<(script|style|iframe|object|embed)\b[^>]*>.*?</\1\s*>",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            // 去除剩余的成对/单个标签，保留其包裹的可见文本
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized, @"<[^>]*>", string.Empty);

            if (sanitized.Length > MaxMessageLength)
                sanitized = sanitized.Substring(0, MaxMessageLength);

            return sanitized;
        }

        // ─── 消息管理 ─────────────────────────────

        public void ReceiveMessage(string senderId, string senderName, string content, bool isDead, Faction faction, ChatChannel channel = ChatChannel.Meeting)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            messages.Add(new ChatMessage(senderId, senderName, Sanitize(content), Time.time, isDead, faction, channel));

            while (messages.Count > MaxMessages)
            {
                messages.RemoveAt(0);
            }
        }

        public void Clear()
        {
            messages.Clear();
            inputBuffer = string.Empty;
            isInputActive = false;
        }

        public int MessageCount => messages.Count;

        /// <summary>获取消息列表（供 Canvas UI 读取）。</summary>
        public IReadOnlyList<ChatMessage> Messages => messages;

        // ─── 输入处理 ─────────────────────────────

        /// <summary>处理键盘输入（每帧 OnGUI 调用，未来迁移到 Canvas 后由 Update 接管）。</summary>
        public void ProcessInputKeys()
        {
            if (Event.current == null)
            {
                return;
            }

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    if (!isInputActive)
                    {
                        if (CanSend && CanSendNow())
                        {
                            isInputActive = true;
                            inputBuffer = string.Empty;
                            GUI.FocusControl("ChatInputField");
                        }

                        Event.current.Use();
                    }
                    else
                    {
                        // Input is active, try to send
                        string trimmed = inputBuffer.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            if (CanSendNow())
                            {
                                sendCallback?.Invoke(Sanitize(trimmed));
                                MarkSent();
                            }
                        }

                        inputBuffer = string.Empty;
                        isInputActive = false;
                        Event.current.Use();
                    }
                }
                else if (Event.current.keyCode == KeyCode.Escape && isInputActive)
                {
                    isInputActive = false;
                    inputBuffer = string.Empty;
                    Event.current.Use();
                }
            }
        }

        // ─── GUI 渲染（OnGUI，计划 M7 全 Canvas 化后移除） ────

        /// <summary>在指定区域绘制完整聊天面板（消息列表 + 输入栏）。</summary>
        public void DrawChatPanel(Rect area, GUISkin skin)
        {
            GUIStyle boxStyle = skin?.box ?? GUI.skin.box;
            GUIStyle labelStyle = skin?.label ?? GUI.skin.label;

            GUILayout.BeginArea(area, boxStyle);

            // 标题栏（含通道名）
            string title = "  " + ChannelDisplayName(CurrentChannel);
            DrawHeader(title, skin);

            // 消息列表（可滚动）
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.ExpandHeight(true));

            foreach (ChatMessage msg in messages)
            {
                DrawMessageLine(msg, skin);
            }

            GUILayout.EndScrollView();

            // 输入区域
            DrawInputArea(skin);

            GUILayout.EndArea();
        }

        private void DrawHeader(string title, GUISkin skin)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.18f, 0.2f, 0.24f, 1f);
            GUILayout.BeginHorizontal(skin?.box);
            GUILayout.Label(title);
            GUILayout.EndHorizontal();
            GUI.color = oldColor;
        }

        private void DrawMessageLine(ChatMessage msg, GUISkin skin)
        {
            GUILayout.BeginHorizontal();

            // 通道标签
            string channelTag = msg.Channel == ChatChannel.Ghost ? "[鬼]"
                : msg.Channel == ChatChannel.Proximity ? "[近]"
                : msg.Channel == ChatChannel.Global ? "[全]"
                : "";

            if (!string.IsNullOrEmpty(channelTag))
            {
                Color oldTagColor = GUI.contentColor;
                GUI.contentColor = MutedColor;
                GUILayout.Label(channelTag, skin?.label, GUILayout.ExpandWidth(false));
                GUI.contentColor = oldTagColor;
            }

            // 玩家名（阵营色）
            Color oldColor = GUI.contentColor;
            GUI.contentColor = GetFactionColor(msg.Faction);

            string namePart = msg.IsDead ? msg.SenderName + " [亡]" : msg.SenderName;
            GUILayout.Label(namePart, skin?.label, GUILayout.ExpandWidth(false));

            GUI.contentColor = oldColor;

            // 消息内容
            GUILayout.Label("：" + msg.Content, skin?.label, GUILayout.ExpandWidth(true));

            GUILayout.EndHorizontal();
        }

        private void DrawInputArea(GUISkin skin)
        {
            GUILayout.BeginHorizontal();

            if (CanSend)
            {
                if (isInputActive)
                {
                    GUI.SetNextControlName("ChatInputField");

                    // 捕获回车发送
                    if (Event.current.type == EventType.KeyDown &&
                        (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                        GUI.GetNameOfFocusedControl() == "ChatInputField")
                    {
                        string trimmed = inputBuffer.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            if (CanSendNow())
                            {
                                sendCallback?.Invoke(Sanitize(trimmed));
                                MarkSent();
                            }
                        }

                        inputBuffer = string.Empty;
                        isInputActive = false;
                        Event.current.Use();
                    }

                    inputBuffer = GUILayout.TextField(inputBuffer, GUILayout.ExpandWidth(true));

                    if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "ChatInputField")
                    {
                        GUI.FocusControl("ChatInputField");
                    }
                }
                else
                {
                    string hint = CanSendNow()
                        ? (CurrentChannel == ChatChannel.Ghost ? "按 Enter 对鬼魂发言..." : "按 Enter 发言...")
                        : "发言冷却中...";

                    GUIStyle hintStyle = new GUIStyle(skin?.label ?? GUI.skin.label);
                    hintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    GUILayout.Label(hint, hintStyle, GUILayout.ExpandWidth(true));
                }
            }
            else
            {
                string reason = !IsAlive ? "[你已死亡，无法发言]" : "[此阶段无法发言]";
                GUIStyle disabledStyle = new GUIStyle(skin?.label ?? GUI.skin.label);
                disabledStyle.normal.textColor = new Color(0.45f, 0.35f, 0.35f, 1f);
                GUILayout.Label(reason, disabledStyle, GUILayout.ExpandWidth(true));
            }

            GUILayout.EndHorizontal();
        }

        // ─── 工具方法 ─────────────────────────────

        private static readonly Color MutedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        public static Color GetFactionColor(Faction faction)
        {
            switch (faction)
            {
                case Faction.Police:
                    return new Color(0.35f, 0.68f, 1f);
                case Faction.Undercover:
                    return new Color(0.28f, 0.88f, 0.52f);
                case Faction.Gang:
                    return new Color(0.92f, 0.28f, 0.22f);
                case Faction.Mole:
                    return new Color(0.95f, 0.55f, 0.12f);
                default:
                    return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        public static Faction RoleToFaction(OnlineRole role)
        {
            switch (role)
            {
                case OnlineRole.Police:
                    return Faction.Police;
                case OnlineRole.Undercover:
                    return Faction.Undercover;
                case OnlineRole.Gang:
                    return Faction.Gang;
                case OnlineRole.Mole:
                    return Faction.Mole;
                default:
                    return Faction.None;
            }
        }

        public static bool IsSameFaction(Faction a, Faction b)
        {
            if (a == Faction.None || b == Faction.None)
            {
                return false;
            }

            bool aIsGood = a == Faction.Police || a == Faction.Undercover;
            bool bIsGood = b == Faction.Police || b == Faction.Undercover;
            return aIsGood == bIsGood;
        }
    }
}
