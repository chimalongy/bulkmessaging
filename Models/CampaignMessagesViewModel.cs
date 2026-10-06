using System.Collections.Generic;

namespace BulkMessaging.Models
{
    public class CampaignMessagesViewModel
    {
        public CampaignModel Campaign { get; set; } = null!;
        public List<MessageModel> Messages { get; set; } = new();
    }
}