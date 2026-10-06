using BulkMessaging.Models;

namespace BulkMessaging.Services
{
    public class SenderAccountService
    {
        private readonly JsonDatabaseService<SenderAccount> _senderAccountDb;
        private readonly AuditLogService _auditLogService;

        public SenderAccountService(JsonDatabaseService<SenderAccount> senderAccountDb, AuditLogService auditLogService)
        {
            _senderAccountDb = senderAccountDb;
            _auditLogService = auditLogService;
        }

        public async Task<List<SenderAccount>> GetAllSenderAccountsAsync()
        {
            var accounts = await _senderAccountDb.GetAllAsync();
            return accounts.OrderByDescending(a => a.CreatedAt).ToList();
        }

        public async Task<SenderAccount?> GetByIdAsync(Guid id)
        {
            var accounts = await _senderAccountDb.GetAllAsync();
            return accounts.FirstOrDefault(a => a.Id == id);
        }

        public async Task<(bool success, string message)> CreateSenderAccountAsync(
            CreateSenderAccountViewModel model, string username, string role, string? ipAddress)
        {
            var accounts = await _senderAccountDb.GetAllAsync();
            if (accounts.Any(a => a.FromEmail.Equals(model.FromEmail, StringComparison.OrdinalIgnoreCase)))
                return (false, "A sender account with this From Email already exists.");

            var account = new SenderAccount
            {
                Id = Guid.NewGuid(),
                ApiKey = model.ApiKey,
                FromEmail = model.FromEmail,
                FromName = model.FromName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };

            await _senderAccountDb.AddAsync(account);
            await _auditLogService.LogAsync(username, role, "Create Sender Account",
                $"Created sender account '{account.FromName}' <{account.FromEmail}>", ipAddress);

            return (true, "Sender account created successfully.");
        }

        public async Task<(bool success, string message)> UpdateSenderAccountAsync(
            EditSenderAccountViewModel model, string username, string role, string? ipAddress)
        {
            var account = await GetByIdAsync(model.Id);
            if (account == null)
                return (false, "Sender account not found.");

            account.FromEmail = model.FromEmail;
            account.FromName = model.FromName;
            account.IsActive = model.IsActive;

            if (!string.IsNullOrWhiteSpace(model.ApiKey))
                account.ApiKey = model.ApiKey;

            await _senderAccountDb.UpdateAsync(a => a.Id == account.Id, account);
            await _auditLogService.LogAsync(username, role, "Update Sender Account",
                $"Updated sender account '{account.FromName}' <{account.FromEmail}>", ipAddress);

            return (true, "Sender account updated successfully.");
        }

        public async Task<(bool success, string message)> DeleteSenderAccountAsync(
            Guid id, string username, string role, string? ipAddress)
        {
            var account = await GetByIdAsync(id);
            if (account == null)
                return (false, "Sender account not found.");

            await _senderAccountDb.DeleteAsync(a => a.Id == account.Id);
            await _auditLogService.LogAsync(username, role, "Delete Sender Account",
                $"Deleted sender account '{account.FromName}' <{account.FromEmail}>", ipAddress);

            return (true, "Sender account deleted successfully.");
        }
    }
}