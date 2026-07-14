using BulkMessaging.Models;
using BulkMessaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkMessaging.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class UsersController : Controller
    {
        private readonly UserService _userService;
        private readonly AuditLogService _auditLogService;

        public UsersController(UserService userService, AuditLogService auditLogService)
        {
            _userService = userService;
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isSuperAdmin = role == "SuperAdmin";

            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.CurrentUsername = User.Identity?.Name;

            var users = await _userService.GetAllUsersAsync();

            // Admins can only see regular users, SuperAdmins see everyone
            if (!isSuperAdmin)
            {
                users = users.Where(u => u.Role == "User").ToList();
            }

            return View(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            ViewBag.IsSuperAdmin = role == "SuperAdmin";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            var creatorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var creatorUsername = User.Identity?.Name ?? "system";

            ViewBag.IsSuperAdmin = creatorRole == "SuperAdmin";

            if (!ModelState.IsValid)
                return View(model);

            // Authorization checks
            if (creatorRole == "Admin" && model.Role != "User")
            {
                ModelState.AddModelError(string.Empty, "Admins can only create regular users");
                return View(model);
            }

            if (creatorRole != "SuperAdmin" && model.Role == "SuperAdmin")
            {
                ModelState.AddModelError(string.Empty, "Only super admins can create super admin accounts");
                return View(model);
            }

            var (success, message) = await _userService.CreateUserAsync(model.Username, model.Password, model.Role, creatorUsername);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var actionBy = User.Identity?.Name ?? "system";
            var (success, message) = await _userService.ToggleUserStatusAsync(id, actionBy);

            if (!success)
                TempData["ErrorMessage"] = message;
            else
                TempData["SuccessMessage"] = message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var actionBy = User.Identity?.Name ?? "system";
            var (success, message) = await _userService.DeleteUserAsync(id, actionBy);

            if (!success)
                TempData["ErrorMessage"] = message;
            else
                TempData["SuccessMessage"] = message;

            return RedirectToAction(nameof(Index));
        }
    }
}
