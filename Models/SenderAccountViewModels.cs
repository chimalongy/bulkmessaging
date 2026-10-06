using System.ComponentModel.DataAnnotations;

namespace BulkMessaging.Models
{
    public class CreateSenderAccountViewModel
    {
        [Required, Display(Name = "API Key")]
        public string ApiKey { get; set; } = string.Empty;

        [Required, EmailAddress, Display(Name = "From Email")]
        public string FromEmail { get; set; } = string.Empty;

        [Required, Display(Name = "From Name")]
        public string FromName { get; set; } = string.Empty;
    }

    public class EditSenderAccountViewModel : CreateSenderAccountViewModel
    {
        public Guid Id { get; set; }
        public bool IsActive { get; set; }
    }
}