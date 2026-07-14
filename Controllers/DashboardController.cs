using BulkMessaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkMessaging.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserService _userService;
        private readonly AuditLogService _auditLogService;

        public DashboardController(UserService userService, AuditLogService auditLogService)
        {
            _userService = userService;
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
            var username = User.Identity?.Name ?? "";

            ViewBag.Role = role;
            ViewBag.Username = username;
            ViewBag.IsAdmin = role == "SuperAdmin" || role == "Admin";
            ViewBag.IsSuperAdmin = role == "SuperAdmin";

            // Load stats for dashboard cards
            var users = await _userService.GetAllUsersAsync();
            var logs = await _auditLogService.GetAllAsync();

            ViewBag.TotalUsers = users.Count;
            ViewBag.ActiveUsers = users.Count(u => u.IsActive);
            ViewBag.TotalLogs = logs.Count;
            ViewBag.RecentLogs = logs.Take(10).ToList();

            return View();
        }
    }
}
