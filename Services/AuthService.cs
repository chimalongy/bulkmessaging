using BulkMessaging.Models;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BulkMessaging.Services
{
    public class AuthService
    {
        private readonly JsonDatabaseService<User> _userDb;
        private readonly AuditLogService _auditLogService;

        public AuthService(JsonDatabaseService<User> userDb, AuditLogService auditLogService)
        {
            _userDb = userDb;
            _auditLogService = auditLogService;
        }

        public async Task InitializeDefaultAdminAsync()
        {
            var users = await _userDb.GetAllAsync();
            if (!users.Any())
            {
                var superAdmin = new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    PasswordHash = HashPassword("Admin@123"),
                    Role = "SuperAdmin",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system",
                    IsActive = true
                };
                await _userDb.AddAsync(superAdmin);
                await _auditLogService.LogAsync("admin", "SuperAdmin", "System Initialization", "Default super admin account created", null);
            }
        }

        public async Task<User?> ValidateCredentialsAsync(string username, string password)
        {
            var user = await _userDb.GetByPredicateAsync(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.IsActive);

            if (user == null) return null;

            return VerifyPassword(password, user.PasswordHash) ? user : null;
        }

        public async Task<bool> UpdatePasswordAsync(string username, string currentPassword, string newPassword)
        {
            var user = await ValidateCredentialsAsync(username, currentPassword);
            if (user == null) return false;

            user.PasswordHash = HashPassword(newPassword);
            await _userDb.UpdateAsync(u => u.Id == user.Id, user);
            await _auditLogService.LogAsync(username, user.Role, "Password Changed", $"User '{username}' changed their password", null);

            return true;
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 32);

            byte[] hashBytes = new byte[48];
            Buffer.BlockCopy(salt, 0, hashBytes, 0, 16);
            Buffer.BlockCopy(hash, 0, hashBytes, 16, 32);

            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            byte[] hashBytes = Convert.FromBase64String(storedHash);
            byte[] salt = new byte[16];
            Buffer.BlockCopy(hashBytes, 0, salt, 0, 16);

            byte[] hash = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 32);

            for (int i = 0; i < 32; i++)
            {
                if (hashBytes[i + 16] != hash[i])
                    return false;
            }
            return true;
        }

        public static ClaimsPrincipal CreateClaimsPrincipal(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.Id.ToString())
            };

            var identity = new ClaimsIdentity(claims, "CustomAuth");
            return new ClaimsPrincipal(identity);
        }
    }
}
