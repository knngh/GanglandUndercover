using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机/离线通用聊天消息数据结构（方案B：支持三通道 + 鬼魂频道）。
    /// </summary>
    public struct ChatMessage
    {
        public string SenderId;
        public string SenderName;
        public string Content;
        public float Timestamp;
        public bool IsDead;
        public Faction Faction;

        /// <summary>消息所属聊天通道。</summary>
        public ChatChannel Channel;

        public ChatMessage(string senderId, string senderName, string content, float timestamp, bool isDead, Faction faction, ChatChannel channel = ChatChannel.Meeting)
        {
            SenderId = senderId;
            SenderName = senderName;
            Content = content;
            Timestamp = timestamp;
            IsDead = isDead;
            Faction = faction;
            Channel = channel;
        }
    }

    /// <summary>
    /// 本地聊天举报快照。后续接入后台时以该结构作为最小证据输入。
    /// </summary>
    public struct ChatReport
    {
        public string SenderId;
        public string SenderName;
        public string Content;
        public string Reason;
        public float Timestamp;
        public Faction Faction;
        public ChatChannel Channel;

        public ChatReport(
            string senderId,
            string senderName,
            string content,
            string reason,
            float timestamp,
            Faction faction,
            ChatChannel channel)
        {
            SenderId = senderId;
            SenderName = senderName;
            Content = content;
            Reason = reason;
            Timestamp = timestamp;
            Faction = faction;
            Channel = channel;
        }
    }
}
