using BulkMessaging.Models;

namespace BulkMessaging.Services
{
    public class UserService
    {
        private readonly JsonDatabaseService<User> _userDb;
        private readonly AuditLogService _auditLogService;

        public UserService(JsonDatabaseService<User> userDb, AuditLogService auditLogService)
        {
            _userDb = userDb;
            _auditLogService = auditLogService;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var users = await _userDb.GetAllAsync();
            return users.OrderBy(u => u.CreatedAt).ToList();
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _userDb.GetByPredicateAsync(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _userDb.GetByPredicateAsync(u => u.Id == id);
        }

        public async Task<(bool Success, string Message)> CreateUserAsync(string username, string password, string role, string createdBy)
        {
            var existing = await GetByUsernameAsync(username);
            if (existing != null)
                return (false, "Username already exists");

            var user = new User
            {
                Username = username,
                PasswordHash = AuthService.HashPassword(password),
                Role = role,
                CreatedBy = createdBy,
                IsActive = true
            };

            await _userDb.AddAsync(user);
            await _auditLogService.LogAsync(createdBy, "Admin", "User Created", $"Created user '{username}' with role '{role}'", null);

            return (true, "User created successfully");
        }

        public async Task<(bool Success, string Message)> ToggleUserStatusAsync(Guid userId, string actionBy)
        {
            var user = await GetByIdAsync(userId);
            if (user == null)
                return (false, "User not found");

            if (user.Role == "SuperAdmin")
                return (false, "Cannot modify super admin account");

            user.IsActive = !user.IsActive;
            await _userDb.UpdateAsync(u => u.Id == userId, user);

            var status = user.IsActive ? "activated" : "deactivated";
            await _auditLogService.LogAsync(actionBy, "Admin", $"User {status}", $"{status} user '{user.Username}'", null);

            return (true, $"User {status} successfully");
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(Guid userId, string actionBy)
        {
            var user = await GetByIdAsync(userId);
            if (user == null)
                return (false, "User not found");

            if (user.Role == "SuperAdmin")
                return (false, "Cannot delete super admin account");

            await _userDb.DeleteAsync(u => u.Id == userId);
            await _auditLogService.LogAsync(actionBy, "Admin", "User Deleted", $"Deleted user '{user.Username}'", null);

            return (true, "User deleted successfully");
        }

        public async Task RecordLoginAsync(string username)
        {
            var user = await GetByUsernameAsync(username);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userDb.UpdateAsync(u => u.Id == user.Id, user);
            }
        }
    }
}
