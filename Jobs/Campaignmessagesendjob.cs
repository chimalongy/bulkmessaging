using System.Threading.Tasks;
using BulkMessaging.Services;
using Quartz;

namespace BulkMessaging.Jobs
{
    // Fired by Quartz — either right away ("send now") or at the scheduled
    // time. It only knows one thing: which message to send. All the actual
    // sending logic lives in CampaignMessageSender.
    [DisallowConcurrentExecution]
    public class CampaignMessageSendJob : IJob
    {
        private readonly CampaignMessageSender _sender;

        public CampaignMessageSendJob(CampaignMessageSender sender)
        {
            _sender = sender;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var messageId = context.JobDetail.JobDataMap.GetString("messageId");
            if (string.IsNullOrWhiteSpace(messageId))
                return;

            await _sender.SendMessagesAsync(messageId);
        }
    }
}