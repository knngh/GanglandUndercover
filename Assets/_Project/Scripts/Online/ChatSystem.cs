using System.Collections.Generic;
using GanglandUndercover.Core;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 跨模式聊天系统：管理消息列表、输入、渲染，支持联机/离线两种场景。
    ///
    /// 联机模式（OnlineMatchController）：
    ///   - 会议/投票阶段：所有存活玩家可发言，消息广播给所有客户端
    ///   - 自由行动阶段：仅同阵营玩家可私聊
    ///   - 死亡玩家：仅可观看会议聊天，不能发言
    ///
    /// 离线模式（SocialPrototypeController）：
    ///   - 会议阶段：玩家可输入，AI 自动发送预设消息
    ///   - 非会议阶段：无聊天功能
    /// </summary>
    public class ChatSystem
    {
        private const int MaxMessages = 50;

        private readonly List<ChatMessage> messages = new List<ChatMessage>();
        private readonly System.Action<string> sendCallback;
        private string inputBuffer = string.Empty;
        private bool isInputActive;
        private Vector2 scrollPosition;

        /// <summary>当前是否允许发送消息。</summary>
        public bool CanSend { get; set; }

        /// <summary>本地玩家阵营（用于着色）。</summary>
        public Faction LocalFaction { get; set; } = Faction.None;

        /// <summary>当前游戏阶段。</summary>
        public OnlineMatchPhase CurrentPhase { get; set; } = OnlineMatchPhase.Lobby;

        /// <summary>玩家是否存活。</summary>
        public bool IsAlive { get; set; } = true;

        public ChatSystem(System.Action<string> sendCallback)
        {
            this.sendCallback = sendCallback;
        }

        // ─── 消息管理 ─────────────────────────────

        public void ReceiveMessage(string senderId, string senderName, string content, bool isDead, Faction faction)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            messages.Add(new ChatMessage(senderId, senderName, content, Time.time, isDead, faction));

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

        // ─── 输入处理（每帧由控制器 OnGUI / Update 调用）─────────────

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
                        if (CanSend)
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
                            sendCallback?.Invoke(trimmed);
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

        // ─── GUI 渲染 ─────────────────────────────

        /// <summary>在指定区域绘制完整聊天面板（消息列表 + 输入栏）。</summary>
        public void DrawChatPanel(Rect area, GUISkin skin)
        {
            GUIStyle boxStyle = skin?.box ?? GUI.skin.box;
            GUIStyle labelStyle = skin?.label ?? GUI.skin.label;

            GUILayout.BeginArea(area, boxStyle);

            // 标题栏
            string title = (CurrentPhase == OnlineMatchPhase.Meeting || CurrentPhase == OnlineMatchPhase.Voting)
                ? "  会议聊天"
                : "  阵营私聊";
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

            // 玩家名（阵营色）
            Color oldColor = GUI.contentColor;
            GUI.contentColor = GetFactionColor(msg.Faction);

            string namePart = msg.IsDead ? msg.SenderName + " [亡]" : msg.SenderName;
            GUILayout.Label(namePart, skin?.label, GUILayout.ExpandWidth(false));

            GUI.contentColor = oldColor;
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
                            sendCallback?.Invoke(trimmed);
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
                    string hint = (CurrentPhase == OnlineMatchPhase.Meeting || CurrentPhase == OnlineMatchPhase.Voting)
                        ? "按 Enter 发言..."
                        : "按 Enter 私聊阵营...";
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

        public static Color GetFactionColor(Faction faction)
        {
            switch (faction)
            {
                case Faction.Police:
                    return new Color(0.35f, 0.68f, 1f);     // 蓝
                case Faction.Undercover:
                    return new Color(0.28f, 0.88f, 0.52f);   // 绿
                case Faction.Gang:
                    return new Color(0.92f, 0.28f, 0.22f);   // 红
                case Faction.Mole:
                    return new Color(0.95f, 0.55f, 0.12f);   // 橙
                default:
                    return new Color(0.6f, 0.6f, 0.6f);      // 灰
            }
        }

        /// <summary>将 OnlineRole 转换为 Faction（用于联机模式阵营判定）。</summary>
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

        /// <summary>判断两个 Faction 是否属于同一阵营（Police+Undercover / Gang+Mole）。</summary>
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