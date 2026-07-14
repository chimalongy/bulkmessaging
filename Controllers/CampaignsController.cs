using BulkMessaging.Jobs;

using BulkMessaging.Models;
using BulkMessaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkMessaging.Controllers
{
    [Authorize]
    public class CampaignsController : Controller
    {
        private readonly CampaignService _campaignService;
        private readonly MessageService _messageService;
        private readonly TemplateService _templateService;
        private readonly ISchedulerFactory _schedulerFactory;

        public CampaignsController(
            CampaignService campaignService,
            MessageService messageService,
            TemplateService templateService,
            ISchedulerFactory schedulerFactory)
        {
            _campaignService = campaignService;
            _messageService = messageService;
            _templateService = templateService;
            _schedulerFactory = schedulerFactory;
        }

        private async Task<CampaignModel?> FindCampaignByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var campaigns = await _campaignService.GetAllAsync();
            return campaigns.FirstOrDefault(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        // Schedules a CampaignMessageSendJob to fire at the given time.
        // "Send now" and "Schedule for later" both go through here — the
        // only difference is when the trigger fires. That single unified
        // path is what actually sends the message (see CampaignMessageSender).
        private async Task ScheduleSendAsync(string messageId, DateTimeOffset fireAt)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            var job = JobBuilder.Create<CampaignMessageSendJob>()
                .WithIdentity($"send-message-{messageId}", "campaign-messages")
                .UsingJobData("messageId", messageId)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"send-message-trigger-{messageId}", "campaign-messages")
                .StartAt(fireAt)
                .Build();

            await scheduler.ScheduleJob(job, trigger);
        }

        // GET /Campaigns/Index — lists every saved campaign.
        public async Task<IActionResult> Index()
        {
            var campaigns = await _campaignService.GetAllAsync();
            return View(campaigns);
        }

        // GET /Campaigns/NewCampaign — blank creation form.
        [HttpGet]
        public IActionResult NewCampaign()
        {
            return View("NewCampaign/Index", new CampaignModel());
        }

        // GET /Campaigns/{name}/Index — the campaign's messages page.
        [HttpGet("Campaigns/{name}/Index")]
        public async Task<IActionResult> Details(string name)
        {
            var campaign = await FindCampaignByNameAsync(name);
            if (campaign == null)
                return NotFound();

            var messages = await _messageService.GetByCampaignIdAsync(campaign.Id);

            var vm = new CampaignMessagesViewModel
            {
                Campaign = campaign,
                Messages = messages
            };

            return View("Details/Index", vm);
        }

        // GET /Campaigns/{name}/Contacts — the contact list page.
        [HttpGet("Campaigns/{name}/Contacts")]
        public async Task<IActionResult> Contacts(string name)
        {
            var campaign = await FindCampaignByNameAsync(name);
            if (campaign == null)
                return NotFound();

            return View("Contacts/Index", campaign);
        }

        // GET /Campaigns/{name}/Index/new-message — message creation form.
        [HttpGet("Campaigns/{name}/Index/new-message")]
        public async Task<IActionResult> NewMessage(string name)
        {
            var campaign = await FindCampaignByNameAsync(name);
            if (campaign == null)
                return NotFound();

            var vm = new NewMessageViewModel
            {
                Campaign = campaign,
                Templates = campaign.Type == CampaignType.Email
                    ? await _templateService.GetAllAsync()
                    : new List<TemplateModel>()
            };

            return View("NewMessage/Index", vm);
        }

        // POST /Campaigns/{name}/Index/new-message — saves the message, then
        // hands it off to Quartz. "Send now" schedules the job to fire
        // immediately; "Schedule for later" schedules it for ScheduledAt.
        // Either way, the message starts out as Scheduled and CampaignMessageSender
        // flips it to Sent once the job actually runs.
        [HttpPost("Campaigns/{name}/Index/new-message")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMessage(
            string name,
            string? TemplateId,
            string? Subject,
            string? Cc,
            string? Bcc,
            string? HtmlBody,
            string? SmsBody,
            string SendOption,
            DateTime? ScheduledAt)
        {
            var campaign = await FindCampaignByNameAsync(name);
            if (campaign == null)
                return NotFound();

            async Task<IActionResult> ReturnWithError(string error)
            {
                ModelState.AddModelError("Message", error);
                var errVm = new NewMessageViewModel
                {
                    Campaign = campaign,
                    Templates = campaign.Type == CampaignType.Email
                        ? await _templateService.GetAllAsync()
                        : new List<TemplateModel>()
                };
                return View("NewMessage/Index", errVm);
            }

            if (campaign.Type == CampaignType.Email)
            {
                if (string.IsNullOrWhiteSpace(TemplateId) || string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(HtmlBody))
                    return await ReturnWithError("Choose a template and make sure it has a subject and body.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(SmsBody))
                    return await ReturnWithError("Type a message before saving.");
            }

            var isSchedule = string.Equals(SendOption, "schedule", StringComparison.OrdinalIgnoreCase);
            if (isSchedule && (!ScheduledAt.HasValue || ScheduledAt.Value <= DateTime.Now))
                return await ReturnWithError("Pick a date and time in the future to schedule this message.");

            var fireAt = isSchedule ? ScheduledAt!.Value : DateTime.Now;

            var message = new MessageModel
            {
                CampaignId = campaign.Id,
                Type = campaign.Type,
                TemplateId = TemplateId,
                Subject = Subject,
                Cc = Cc,
                Bcc = Bcc,
                HtmlBody = HtmlBody,
                SmsBody = SmsBody,
                Status = MessageStatus.Scheduled,
                ScheduledAt = fireAt
            };

            await _messageService.SaveAsync(message);
            await ScheduleSendAsync(message.Id, new DateTimeOffset(fireAt));

            return RedirectToAction("Details", new { name });
        }

        // POST /Campaigns/{name}/Index/delete-message
        [HttpPost("Campaigns/{name}/Index/delete-message")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(string name, string messageId)
        {
            // Also cancel the Quartz job/trigger if the message hasn't sent yet,
            // so a deleted-but-still-scheduled message doesn't get sent anyway.
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.DeleteJob(new JobKey($"send-message-{messageId}", "campaign-messages"));

            await _messageService.DeleteAsync(messageId);
            return RedirectToAction("Details", new { name });
        }

        // POST /Campaigns/SaveCampaign — merges pasted contacts and/or an
        // uploaded Excel column into one de-duplicated contact list, then
        // saves the campaign.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCampaign(
            string Name,
            CampaignType Type,
            string? ContactsText,
            IFormFile? ExcelFile,
            string? ExcelColumn)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ModelState.AddModelError("Name", "Campaign name is required.");
                return View("NewCampaign/Index", new CampaignModel { Name = Name, Type = Type });
            }

            var contacts = new List<string>();

            if (!string.IsNullOrWhiteSpace(ContactsText))
            {
                var pasted = ContactsText
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line));
                contacts.AddRange(pasted);
            }

            if (ExcelFile != null && ExcelFile.Length > 0)
            {
                if (string.IsNullOrWhiteSpace(ExcelColumn))
                {
                    ModelState.AddModelError("ExcelColumn",
                        "Specify which column contains the contacts (e.g. \"B\" or a header name) before uploading a file.");
                    return View("NewCampaign/Index", new CampaignModel { Name = Name, Type = Type });
                }

                try
                {
                    using var stream = ExcelFile.OpenReadStream();
                    var fromExcel = ExcelContactExtractor.ExtractColumnValues(stream, ExcelColumn);
                    contacts.AddRange(fromExcel);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("ExcelFile", $"Couldn't read that file: {ex.Message}");
                    return View("NewCampaign/Index", new CampaignModel { Name = Name, Type = Type });
                }
            }

            // De-dupe (case-insensitive) while preserving the order contacts were added in.
            contacts = contacts
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (contacts.Count == 0)
            {
                ModelState.AddModelError("Contacts",
                    "Add at least one contact — paste some in, upload a file, or both.");
                return View("NewCampaign/Index", new CampaignModel { Name = Name, Type = Type });
            }

            var campaign = new CampaignModel
            {
                Name = Name,
                Type = Type,
                Contacts = contacts
            };

            await _campaignService.SaveAsync(campaign);
            await ScheduleContactCleanAsync(campaign.Id);

            return RedirectToAction("Index");
        }


        // Schedules a ContactCleanJob to fire immediately after a campaign is saved.
        // Keyed per-campaign so it can't double-run for the same campaign.
        private async Task ScheduleContactCleanAsync(string campaignId)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            var job = JobBuilder.Create<ContactCleanJob>()
                .WithIdentity($"clean-contacts-{campaignId}", "contact-cleaning")
                .UsingJobData("campaignId", campaignId)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"clean-contacts-trigger-{campaignId}", "contact-cleaning")
                .StartNow()
                .Build();

            await scheduler.ScheduleJob(job, trigger);
        }





        // POST /Campaigns/DeleteCampaign — removes the JSON file.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCampaign(string id)
        {
            await _campaignService.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        // so you can see exactly what was (or will be) sent.
        [HttpGet("Campaigns/{name}/Index/message/{messageId}")]
        public async Task<IActionResult> MessageDetails(string name, string messageId)
        {
            var campaign = await FindCampaignByNameAsync(name);
            if (campaign == null)
                return NotFound();

            var messages = await _messageService.GetByCampaignIdAsync(campaign.Id);
            var message = messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
                return NotFound();

            var vm = new MessageDetailsViewModel
            {
                Campaign = campaign,
                Message = message
            };

            return View("MessageDetails/Index", vm);
        }


        // POST /Campaigns/{name}/Index/cancel-message — stops a pending or in-flight
        // send without deleting the message record. Unlike DeleteMessage, this keeps
        // history around so you can see it was intentionally canceled.
        [HttpPost("Campaigns/{name}/Index/cancel-message")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMessage(string name, string messageId)
        {
            var campaign = await FindCampaignByNameAsync(name);
            if (campaign == null)
                return NotFound();

            var messages = await _messageService.GetByCampaignIdAsync(campaign.Id);
            var message = messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
                return NotFound();

            // Only Scheduled/Sending are cancelable — Draft was never queued,
            // Sent already happened, and Canceled is already canceled.
            if (message.Status == MessageStatus.Scheduled || message.Status == MessageStatus.Sending)
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                await scheduler.DeleteJob(new JobKey($"send-message-{messageId}", "campaign-messages"));

                message.Status = MessageStatus.Canceled;
                message.UpdatedAt = DateTime.UtcNow;
                await _messageService.SaveAsync(message);
            }

            return RedirectToAction("Details", new { name });
        }


        // GET /Campaigns/{name}/Index/message/{messageId}/sending-results — per-contact
        // breakdown of a message's send. Zips campaign.Contacts with message.SendResults
        // by index, since SendResults are appended in the same order contacts were
        // iterated (see CampaignMessageSender). Contacts beyond SendResults.Count
        // haven't been attempted yet (still sending, or sending was canceled early).
        [HttpGet("Campaigns/{name}/Index/message/{messageId}/sending-results")]
        public async Task<IActionResult> SendingResults(string name, string messageId)
        {
            var campaign = await FindCampaignByNameAsync(name);
            if (campaign == null)
                return NotFound();

            var messages = await _messageService.GetByCampaignIdAsync(campaign.Id);
            var message = messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
                return NotFound();

            var statuses = new List<ContactSendStatus>();
            for (int i = 0; i < campaign.Contacts.Count; i++)
            {
                var contact = campaign.Contacts[i];

                if (i < message.SendResults.Count)
                {
                    var result = message.SendResults[i];
                    statuses.Add(new ContactSendStatus
                    {
                        Contact = contact,
                        Success = result.Success,
                        Message = result.Message,
                        Timestamp = result.Timestamp
                    });
                }
                else
                {
                    statuses.Add(new ContactSendStatus { Contact = contact, Success = null });
                }
            }

            var vm = new SendingResultsViewModel
            {
                Campaign = campaign,
                Message = message,
                ContactStatuses = statuses
            };

            return View("SendingResults/Index", vm);
        }






    }
}