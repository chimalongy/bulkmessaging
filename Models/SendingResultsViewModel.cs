namespace BulkMessaging.Models
{
    public class SendingResultsViewModel
    {
        public CampaignModel Campaign { get; set; } = null!;
        public MessageModel Message { get; set; } = null!;
        public List<ContactSendStatus> ContactStatuses { get; set; } = new();
    }

    public class ContactSendStatus
    {
        public string Contact { get; set; } = string.Empty;

        // null = not yet attempted (still pending, or sending stopped before reaching this contact)
        public bool? Success { get; set; }

        public string? Message { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}
