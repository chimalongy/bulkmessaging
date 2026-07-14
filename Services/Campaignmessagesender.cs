using BulkMessaging.Models;
using BulkMessaging.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BulkMessaging.Services
{
    public class CampaignMessageSender
    {
        private readonly MessageService _messageService;
        private readonly CampaignService _campaignService;

        // SendGrid's Web API allows up to 10,000 requests/second, and Twilio's
        // REST API has generous per-account limits too — so this is no longer
        // bounded by SMTP connection limits. 50 is a reasonable, safe default;
        // raise it further if your provider plan supports it.
        private const int MaxConcurrency = 50;

        public CampaignMessageSender(MessageService messageService, CampaignService campaignService)
        {
            _messageService = messageService;
            _campaignService = campaignService;
        }

        public async Task SendMessagesAsync(string messageId)
        {
            var message = await _messageService.GetByIdAsync(messageId);
            if (message == null)
                return;

            if (message.Status == MessageStatus.Sent || message.Status == MessageStatus.Canceled)
                return;

            var campaign = await _campaignService.GetByIdAsync(message.CampaignId);
            if (campaign == null)
                return;

            message.Status = MessageStatus.Sending;
            await _messageService.SaveAsync(message);

            var wasCanceled = campaign.Type == CampaignType.Sms
                ? await SendSmsAsync(campaign, message, messageId)
                : await SendEmailAsync(campaign, message, messageId);

            if (wasCanceled)
            {
                await _messageService.SaveAsync(message);
                return;
            }

            message.Status = MessageStatus.Sent;
            message.SentAt = DateTime.UtcNow;

            await _messageService.SaveAsync(message);
        }

        private async Task<bool> SendEmailAsync(CampaignModel campaign, MessageModel message, string messageId)
        {
            var resultsLock = new object();

            foreach (var batch in campaign.Contacts.Chunk(MaxConcurrency))
            {
                var current = await _messageService.GetByIdAsync(messageId);
                if (current == null || current.Status == MessageStatus.Canceled)
                {
                    lock (resultsLock)
                    {
                        message.SendResults.Add(new MessageSendResult
                        {
                            Success = false,
                            Message = "Sending canceled",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    return true;
                }

                var tasks = batch.Select(async contact =>
                {
                    var result = await Sender.sendEmails(
                        toEmail: contact,
                        subject: message.Subject,
                        body: message.HtmlBody,
                        isHtml: true
                    );

                    lock (resultsLock)
                    {
                        message.SendResults.Add(result);
                    }
                });

                await Task.WhenAll(tasks);
            }

            return false;
        }

        private async Task<bool> SendSmsAsync(CampaignModel campaign, MessageModel message, string messageId)
        {
            var resultsLock = new object();

            foreach (var batch in campaign.Contacts.Chunk(MaxConcurrency))
            {
                var current = await _messageService.GetByIdAsync(messageId);
                if (current == null || current.Status == MessageStatus.Canceled)
                {
                    lock (resultsLock)
                    {
                        message.SendResults.Add(new MessageSendResult
                        {
                            Success = false,
                            Message = "Sending canceled",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    return true;
                }

                var tasks = batch.Select(async contact =>
                {
                    var result = await Sender.sendSMS(
                        toPhoneNumber: contact,
                        messageBody: message.SmsBody
                    );

                    lock (resultsLock)
                    {
                        message.SendResults.Add(result);
                    }
                });

                await Task.WhenAll(tasks);
            }

            return false;
        }
    }
}