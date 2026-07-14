using System;

namespace BulkMessaging.Models
{
    public class TemplateModel
    {
        // Empty string on a brand-new template — the service assigns a real
        // id the first time it's saved. Kept as a plain string (not Guid) so
        // it binds cleanly from a hidden form field.
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Subject { get; set; }

        public string? Cc { get; set; }

        public string? Bcc { get; set; }

        // Full HTML markup produced by the WYSIWYG editor. This is what
        // gets used as the email body when the template is attached to a
        // campaign.
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
