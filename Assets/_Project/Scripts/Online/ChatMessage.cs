using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 联机/离线通用聊天消息数据结构。
    /// </summary>
    public struct ChatMessage
    {
        public string SenderId;
        public string SenderName;
        public string Content;
        public float Timestamp;
        public bool IsDead;
        public Faction Faction;

        public ChatMessage(string senderId, string senderName, string content, float timestamp, bool isDead, Faction faction)
        {
            SenderId = senderId;
            SenderName = senderName;
            Content = content;
            Timestamp = timestamp;
            IsDead = isDead;
            Faction = faction;
        }
    }
}