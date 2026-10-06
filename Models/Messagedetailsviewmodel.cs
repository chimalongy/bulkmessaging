namespace BulkMessaging.Models
{
    // Powers the "view a single message" page — shows exactly what was
    // (or will be) sent for one message on a campaign.
    public class MessageDetailsViewModel
    {
        public CampaignModel Campaign { get; set; } = null!;
        public MessageModel Message { get; set; } = null!;
    }
}