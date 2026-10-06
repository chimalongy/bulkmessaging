using System;

namespace BulkMessaging.Models
{
    // One entry per contact per send attempt — lets you see exactly which
    // contacts succeeded or failed for a given message, and when.
    public class MessageSendResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

