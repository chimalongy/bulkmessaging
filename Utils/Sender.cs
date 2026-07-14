using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using BulkMessaging.Models;

namespace BulkMessaging.Utils
{
    public static class Sender
    {
        private static readonly IConfigurationRoot Config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // ---- SendGrid (Web API) config ----
        private static readonly string SendGridApiKey = Config["SendGrid:ApiKey"];
        private static readonly string FromEmail = Config["SendGrid:FromEmail"];
        private static readonly string FromName = Config["SendGrid:FromName"] ?? "Your Company";

        private static readonly SendGridClient SgClient;

        // ---- Twilio config ----
        private static readonly string TwilioAccountSid = Config["Twilio:AccountSid"];
        private static readonly string TwilioAuthToken = Config["Twilio:AuthToken"];
        private static readonly string TwilioFromNumber = Config["Twilio:FromNumber"];

        private static bool _twilioInitialized = false;

        static Sender()
        {
            if (string.IsNullOrWhiteSpace(SendGridApiKey))
                throw new InvalidOperationException("Missing config value: SendGrid:ApiKey");
            if (string.IsNullOrWhiteSpace(FromEmail))
                throw new InvalidOperationException("Missing config value: SendGrid:FromEmail");
            if (string.IsNullOrWhiteSpace(TwilioAccountSid))
                throw new InvalidOperationException("Missing config value: Twilio:AccountSid");
            if (string.IsNullOrWhiteSpace(TwilioAuthToken))
                throw new InvalidOperationException("Missing config value: Twilio:AuthToken");
            if (string.IsNullOrWhiteSpace(TwilioFromNumber))
                throw new InvalidOperationException("Missing config value: Twilio:FromNumber");

            SgClient = new SendGridClient(SendGridApiKey);
        }

        /// <summary>
        /// Sends a single email via the SendGrid Web API (POST /v3/mail/send).
        /// </summary>
        public static async Task<MessageSendResult> sendEmails(string toEmail, string subject, string body, bool isHtml = false)
        {
            try
            {
                var from = new EmailAddress(FromEmail, FromName);
                var to = new EmailAddress(toEmail);

                var msg = isHtml
                    ? MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: null, htmlContent: body)
                    : MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: body, htmlContent: null);

                var response = await SgClient.SendEmailAsync(msg);
                var success = response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK;

                string resultMessage;
                if (success)
                {
                    resultMessage = $"Email sent to {toEmail}";
                }
                else
                {
                    var errorBody = await response.Body.ReadAsStringAsync();
                    resultMessage = $"Failed to send email to {toEmail}: {response.StatusCode} - {errorBody}";
                }

                Console.WriteLine(resultMessage);

                return new MessageSendResult
                {
                    Success = success,
                    Message = resultMessage,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");

                return new MessageSendResult
                {
                    Success = false,
                    Message = $"Failed to send email to {toEmail}: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        //public static async Task<MessageSendResult> sendEmails(string toEmail, string subject, string body, bool isHtml = false)
        //{
        //    try
        //    {
        //        //var from = new EmailAddress(FromEmail, FromName);
        //        //var to = new EmailAddress(toEmail);

        //        //var msg = isHtml
        //        //    ? MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: null, htmlContent: body)
        //        //    : MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: body, htmlContent: null);

        //        //var response = await SgClient.SendEmailAsync(msg);
        //        //var success = response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK;

        //        //string resultMessage;
        //        //if (success)
        //        //{
        //        //    resultMessage = $"Email sent to {toEmail}";
        //        //}
        //        //else
        //        //{
        //        //    var errorBody = await response.Body.ReadAsStringAsync();
        //        //    resultMessage = $"Failed to send email to {toEmail}: {response.StatusCode} - {errorBody}";
        //        //}

        //        //Console.WriteLine(resultMessage);

        //        //return new MessageSendResult
        //        //{
        //        //    Success = success,
        //        //    Message = resultMessage,
        //        //    Timestamp = DateTime.UtcNow
        //        //};

        //        var apiKey = SendGridApiKey;
        //        var client = new SendGridClient(apiKey);
        //        var from_email = new EmailAddress("test@example.up-ng.com", "Example User");
        //        //var subject = "Sending with Twilio SendGrid is Fun";
        //        var to_email = new EmailAddress(toEmail, "Example User");
        //        var plainTextContent = "and easy to do anywhere, even with C#";
        //        var htmlContent = body;
        //        var msg = MailHelper.CreateSingleEmail(from_email, to_email, subject, plainTextContent, htmlContent);
        //        var response = await client.SendEmailAsync(msg).ConfigureAwait(false);

        //        return new MessageSendResult
        //        {
        //            Success = response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK,
        //            Message = $"Email send attempt to {toEmail} returned status code: {response.StatusCode}",
        //            Timestamp = DateTime.UtcNow
        //        };

        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");

        //        return new MessageSendResult
        //        {
        //            Success = false,
        //            Message = $"Failed to send email to {toEmail}: {ex.Message}",
        //            Timestamp = DateTime.UtcNow
        //        };
        //    }
        //}




        /// <summary>
        /// Sends an SMS via Twilio.
        /// </summary>
        public static async Task<MessageSendResult> sendSMS(string toPhoneNumber, string messageBody)
        {
            try
            {
                if (!_twilioInitialized)
                {
                    TwilioClient.Init(TwilioAccountSid, TwilioAuthToken);
                    _twilioInitialized = true;
                }

                var message = await MessageResource.CreateAsync(
                    body: messageBody,
                    from: new PhoneNumber(TwilioFromNumber),
                    to: new PhoneNumber(toPhoneNumber)
                );

                Console.WriteLine($"SMS sent to {toPhoneNumber}, SID: {message.Sid}");

                return new MessageSendResult
                {
                    Success = true,
                    Message = $"SMS sent to {toPhoneNumber}, SID: {message.Sid}",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send SMS to {toPhoneNumber}: {ex.Message}");

                return new MessageSendResult
                {
                    Success = false,
                    Message = $"Failed to send SMS to {toPhoneNumber}: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
    }
}