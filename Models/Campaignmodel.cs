using System;
using System.Collections.Generic;

namespace BulkMessaging.Models
{
    public enum CampaignType
    {
        Sms,
        Email
    }

    public class CampaignModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public CampaignType Type { get; set; } = CampaignType.Email;

        // Final, de-duplicated list of emails or phone numbers, merged from
        // whatever combination of pasted text / uploaded Excel column the
        // user provided when the campaign was created. Once cleaning has run,
        // this holds only the contacts that passed validation — see BadContacts
        // for the ones that didn't.
        public List<string> Contacts { get; set; } = new List<string>();

        // Contacts that failed format validation during cleaning (bad email
        // format for Email campaigns, bad phone format for Sms campaigns).
        // Empty until ContactCleanerService has run at least once.
        public List<string> BadContacts { get; set; } = new List<string>();

        // NotClean until ContactCleanerService picks the campaign up, Cleaning
        // while it's actively validating, Clean once Contacts/BadContacts
        // reflect the validated split.
        public ContactCleanStatus ContactCleanStatus { get; set; } = ContactCleanStatus.NotClean;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}