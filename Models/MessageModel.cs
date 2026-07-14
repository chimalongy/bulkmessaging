using System;
using System.Collections.Generic;

namespace BulkMessaging.Models
{
    public class MessageModel
    {
        public string Id { get; set; } = string.Empty;

        public string CampaignId { get; set; } = string.Empty;

        public CampaignType Type { get; set; }

        // --- Email fields ---
        // Reference only — Subject/Cc/Bcc/HtmlBody below are this message's
        // own copy, so editing a message never touches the saved template.
        public string? TemplateId { get; set; }

        public string? Subject { get; set; }

        public string? Cc { get; set; }

        public string? Bcc { get; set; }

        public string? HtmlBody { get; set; }

        // --- SMS fields ---
        public string? SmsBody { get; set; }

        // --- Send / schedule ---
        public MessageStatus Status { get; set; } = MessageStatus.Draft;


        public DateTime? ScheduledAt { get; set; }


        public DateTime? SentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // --- Send results ---
        // Starts empty; one entry gets appended per contact as the message
        // is sent (email or SMS), so you can see exactly what happened.
        public List<MessageSendResult> SendResults { get; set; } = new();
    }
}