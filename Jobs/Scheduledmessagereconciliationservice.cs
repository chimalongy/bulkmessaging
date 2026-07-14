using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BulkMessaging.Jobs;
using BulkMessaging.Models;
using BulkMessaging.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace BulkMessaging.Jobs
{
    // Quartz's default job store keeps everything in memory — a restart
    // wipes every scheduled trigger, whether it was due in the future or
    // already overdue. This runs once at startup and re-queues every
    // message still sitting in Scheduled status:
    //   - ScheduledAt in the future  -> re-scheduled for that same time.
    //   - ScheduledAt already passed -> queued to fire immediately, so a
    //     message that was due while the app was down still goes out.
    public class ScheduledMessageReconciliationService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScheduledMessageReconciliationService> _logger;

        public ScheduledMessageReconciliationService(
            IServiceScopeFactory scopeFactory,
            ILogger<ScheduledMessageReconciliationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var campaignService = scope.ServiceProvider.GetRequiredService<CampaignService>();
            var messageService = scope.ServiceProvider.GetRequiredService<MessageService>();
            var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
            var scheduler = await schedulerFactory.GetScheduler(cancellationToken);

            var campaigns = await campaignService.GetAllAsync();
            var now = DateTime.Now;
            var requeued = 0;

            foreach (var campaign in campaigns)
            {
                var messages = await messageService.GetByCampaignIdAsync(campaign.Id);

                foreach (var message in messages.Where(m => m.Status == MessageStatus.Scheduled))
                {
                    // Only Scheduled messages are re-queued. Draft was never
                    // sent, Sending/Sent are already handled or in flight,
                    // and Canceled was deliberately stopped — none of those
                    // should be picked back up here.
                    var jobKey = new JobKey($"send-message-{message.Id}", "campaign-messages");

                    // Shouldn't exist yet right after startup with an in-memory
                    // store, but guard against double-scheduling regardless.
                    if (await scheduler.CheckExists(jobKey, cancellationToken))
                        continue;

                    var fireAt = message.ScheduledAt ?? now;
                    var triggerTime = fireAt <= now ? now : fireAt;

                    var job = JobBuilder.Create<CampaignMessageSendJob>()
                        .WithIdentity(jobKey)
                        .UsingJobData("messageId", message.Id)
                        .Build();

                    var trigger = TriggerBuilder.Create()
                        .WithIdentity($"send-message-trigger-{message.Id}", "campaign-messages")
                        .StartAt(new DateTimeOffset(triggerTime))
                        .Build();

                    await scheduler.ScheduleJob(job, trigger, cancellationToken);
                    requeued++;
                }
            }

            _logger.LogInformation("Re-queued {Count} scheduled message(s) after startup.", requeued);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}