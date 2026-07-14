using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BulkMessaging.Models;
using Microsoft.Extensions.Hosting;

namespace BulkMessaging.Services
{
    /// <summary>
    /// Polls once a minute for messages with Status == Scheduled whose
    /// ScheduledAt has passed, "sends" them via the same mock delivery used
    /// for immediate sends (see CampaignsController.SendMessageAsync), and
    /// marks them Sent. Swap the mock loop below for a real SMTP/SMS
    /// provider call once one exists.
    /// </summary>
    public class ScheduledMessageDispatcher : BackgroundService
    {
        private readonly MessageService _messageService;
        private readonly CampaignService _campaignService;
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

        public ScheduledMessageDispatcher(MessageService messageService, CampaignService campaignService)
        {
            _messageService = messageService;
            _campaignService = campaignService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DispatchDueMessagesAsync();
                }
                catch
                {
                    // Don't let a single bad message stop future polling.
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // App is shutting down.
                }
            }
        }

        private async Task DispatchDueMessagesAsync()
        {
            var messages = await _messageService.GetAllAsync();
            var due = messages.Where(m =>
                m.Status == MessageStatus.Scheduled &&
                m.ScheduledAt.HasValue &&
                m.ScheduledAt.Value <= DateTime.Now);

            foreach (var message in due)
            {
                var campaign = await _campaignService.GetByIdAsync(message.CampaignId);
                if (campaign == null)
                    continue;

                // MOCK send — mirrors CampaignsController.SendMessageAsync.
                foreach (var contact in campaign.Contacts)
                {
                    await Task.Delay(25);
                }

                message.Status = MessageStatus.Sent;
                message.SentAt = DateTime.UtcNow;
                await _messageService.SaveAsync(message);
            }
        }
    }
}