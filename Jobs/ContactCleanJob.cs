
    using BulkMessaging.Services;
    using global::BulkMessaging.Services;
    using Quartz;
    using System.Threading.Tasks;

    namespace BulkMessaging.Jobs
    {
        // Fired once, immediately, right after a campaign is saved. Keyed per
        // campaign so re-saving/re-triggering for the same campaign while a
        // clean is already in flight doesn't run two cleans concurrently.
        [DisallowConcurrentExecution]
        public class ContactCleanJob : IJob
        {
            private readonly ContactCleanerService _cleaner;

            public ContactCleanJob(ContactCleanerService cleaner)
            {
                _cleaner = cleaner;
            }

            public async Task Execute(IJobExecutionContext context)
            {
                var campaignId = context.JobDetail.JobDataMap.GetString("campaignId");
                if (string.IsNullOrWhiteSpace(campaignId))
                    return;

                await _cleaner.CleanCampaignContactsAsync(campaignId);
            }
        }
    }

