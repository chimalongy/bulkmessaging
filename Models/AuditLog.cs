namespace BulkMessaging.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
    }
}
