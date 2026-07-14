using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BulkMessaging.Models;

namespace BulkMessaging.Services
{
    // Validates a campaign's contact list against format rules appropriate
    // to its type (email format for Email campaigns, phone format for Sms
    // campaigns), splitting Contacts into valid/invalid and recording the
    // invalid ones in BadContacts. Triggered by ContactCleanJob right after
    // a campaign is saved.
    public class ContactCleanerService
    {
        private readonly CampaignService _campaignService;

        // Standard-ish email pattern: local@domain.tld — deliberately not
        // fully RFC 5322 compliant (that regex is enormous and mostly
        // academic); this catches the format mistakes that actually happen
        // in pasted/uploaded lists (missing @, missing domain, spaces, etc).
        private static readonly Regex EmailPattern = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled);

        // Accepts optional leading +, then 7–15 digits total (E.164-ish),
        // ignoring spaces/dashes/parentheses commonly present in pasted
        // numbers before we strip them for the actual digit count check.
        private static readonly Regex PhonePattern = new Regex(
            @"^\+?[0-9\s\-\(\)]{7,20}$",
            RegexOptions.Compiled);

        public ContactCleanerService(CampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        public async Task CleanCampaignContactsAsync(string campaignId)
        {
            var campaign = await _campaignService.GetByIdAsync(campaignId);
            if (campaign == null)
                return; // campaign was deleted before the job fired

            campaign.ContactCleanStatus = ContactCleanStatus.Cleaning;
            await _campaignService.SaveAsync(campaign);

            var validContacts = new List<string>();
            var badContacts = new List<string>();

            foreach (var contact in campaign.Contacts)
            {
                var isValid = campaign.Type == CampaignType.Email
                    ? IsValidEmail(contact)
                    : IsValidPhone(contact);

                if (isValid)
                    validContacts.Add(contact);
                else
                    badContacts.Add(contact);
            }

            campaign.Contacts = validContacts;
            campaign.BadContacts = badContacts;
            campaign.ContactCleanStatus = ContactCleanStatus.Clean;
            campaign.UpdatedAt = DateTime.UtcNow;

            await _campaignService.SaveAsync(campaign);
        }

        private static bool IsValidEmail(string contact)
        {
            if (string.IsNullOrWhiteSpace(contact))
                return false;

            return EmailPattern.IsMatch(contact.Trim());
        }

        private static bool IsValidPhone(string contact)
        {
            if (string.IsNullOrWhiteSpace(contact))
                return false;

            var trimmed = contact.Trim();
            if (!PhonePattern.IsMatch(trimmed))
                return false;

            // Count actual digits after stripping formatting chars — the
            // pattern above allows spaces/dashes/parens in the raw string,
            // but the *digit* count is what actually matters for validity.
            var digitCount = 0;
            foreach (var c in trimmed)
            {
                if (char.IsDigit(c))
                    digitCount++;
            }

            return digitCount >= 7 && digitCount <= 15;
        }
    }
}