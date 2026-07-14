using System.Collections.Generic;

namespace BulkMessaging.Models
{
    public class NewMessageViewModel
    {
        public CampaignModel Campaign { get; set; } = null!;
        public List<TemplateModel> Templates { get; set; } = new();
    }
}