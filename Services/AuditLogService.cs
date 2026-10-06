using BulkMessaging.Models;

namespace BulkMessaging.Services
{
    public class AuditLogService
    {
        private readonly JsonDatabaseService<AuditLog> _auditLogDb;

        public AuditLogService(JsonDatabaseService<AuditLog> auditLogDb)
        {
            _auditLogDb = auditLogDb;
        }

        public async Task LogAsync(string username, string role, string action, string details, string? ipAddress)
        {
            var log = new AuditLog
            {
                Username = username,
                Role = role,
                Action = action,
                Details = details,
                IpAddress = ipAddress
            };
            await _auditLogDb.AddAsync(log);
        }

        public async Task<List<AuditLog>> GetAllAsync()
        {
            var logs = await _auditLogDb.GetAllAsync();
            return logs.OrderByDescending(l => l.Timestamp).ToList();
        }

        public async Task<List<AuditLog>> GetRecentAsync(int count = 50)
        {
            var logs = await GetAllAsync();
            return logs.Take(count).ToList();
        }
    }
}
