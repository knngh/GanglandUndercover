using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Chat service abstraction for the online match text chat system.
    /// Extracted from <see cref="ChatSystem"/> to enable testing and alternative chat implementations.
    /// Covers message sending/receiving, channel management, blocking/reporting, and chat UI rendering.
    /// </summary>
    public interface IChatService
    {
        // ================================================================
        //  State Properties
        // ================================================================

        /// <summary>Whether the local player is currently allowed to send messages.</summary>
        bool CanSend { get; set; }

        /// <summary>Local player's faction (used for name coloring).</summary>
        Faction LocalFaction { get; set; }

        /// <summary>Current game phase (determines which channel is active).</summary>
        OnlineMatchPhase CurrentPhase { get; set; }

        /// <summary>Whether the local player is alive (affects channel routing).</summary>
        bool IsAlive { get; set; }

        /// <summary>The chat channel currently active (computed from phase and alive state).</summary>
        ChatChannel CurrentChannel { get; }

        // ================================================================
        //  Message & Report Collections
        // ================================================================

        /// <summary>Number of messages in the buffer.</summary>
        int MessageCount { get; }

        /// <summary>Number of local reports filed.</summary>
        int ReportCount { get; }

        /// <summary>Number of blocked senders.</summary>
        int BlockedSenderCount { get; }

        /// <summary>Read-only access to the message list.</summary>
        IReadOnlyList<ChatMessage> Messages { get; }

        /// <summary>Read-only access to the local report list.</summary>
        IReadOnlyList<ChatReport> Reports { get; }

        // ================================================================
        //  Rate Limiting
        // ================================================================

        /// <summary>Check whether the send cooldown has elapsed.</summary>
        bool CanSendNow();

        /// <summary>Mark a message as sent, resetting the cooldown timer.</summary>
        void MarkSent();

        // ================================================================
        //  Message Management
        // ================================================================

        /// <summary>Receive a chat message from a remote or local sender.</summary>
        void ReceiveMessage(string senderId, string senderName, string content, bool isDead, Faction faction,
            ChatChannel channel = ChatChannel.Meeting);

        /// <summary>Clear all messages, reports, blocked senders, and input state.</summary>
        void Clear();

        /// <summary>Build a formatted text representation of recent messages for display.</summary>
        string BuildMessageFeedText(int maxLines);

        // ================================================================
        //  Block / Report
        // ================================================================

        /// <summary>Block all messages from a given sender ID.</summary>
        void BlockSender(string senderId);

        /// <summary>Remove a sender from the block list.</summary>
        void UnblockSender(string senderId);

        /// <summary>Check whether a sender is currently blocked.</summary>
        bool IsSenderBlocked(string senderId);

        /// <summary>File a report against a specific message.</summary>
        bool ReportMessage(ChatMessage message, string reason);

        /// <summary>Report the most recent message in the buffer.</summary>
        bool ReportLatestMessage(string reason);

        /// <summary>Block the sender of the most recent message.</summary>
        bool BlockLatestSender();

        // ================================================================
        //  Input & GUI
        // ================================================================

        /// <summary>Process keyboard input for chat (Enter to send, Escape to cancel).</summary>
        void ProcessInputKeys();

        /// <summary>Draw the full chat panel (message list + input bar) in the given area.</summary>
        void DrawChatPanel(Rect area, GUISkin skin);
    }
}
