using BulkMessaging.Models;
using BulkMessaging.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkMessaging.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly AuthService _authService;
        private readonly UserService _userService;
        private readonly AuditLogService _auditLogService;

        public AuthController(AuthService authService, UserService userService, AuditLogService auditLogService)
        {
            _authService = authService;
            _userService = userService;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _authService.ValidateCredentialsAsync(model.Username, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                await _auditLogService.LogAsync(model.Username, "Unknown", "Login Failed", $"Failed login attempt for '{model.Username}'", HttpContext.Connection.RemoteIpAddress?.ToString());
                return View(model);
            }

            await _userService.RecordLoginAsync(user.Username);

            var principal = AuthService.CreateClaimsPrincipal(user);
            await HttpContext.SignInAsync("CustomAuth", principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

            await _auditLogService.LogAsync(user.Username, user.Role, "Login Success", $"User '{user.Username}' logged in", HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name ?? "Unknown";
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

            await _auditLogService.LogAsync(username, role, "Logout", $"User '{username}' logged out", HttpContext.Connection.RemoteIpAddress?.ToString());
            await HttpContext.SignOutAsync("CustomAuth");

            return RedirectToAction("Login");
        }

        [HttpGet]
        [Authorize]
        public IActionResult UpdatePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdatePassword(UpdatePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var username = User.Identity?.Name ?? "";
            var success = await _authService.UpdatePasswordAsync(username, model.CurrentPassword, model.NewPassword);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Current password is incorrect");
                return View(model);
            }

            TempData["SuccessMessage"] = "Password updated successfully. Please login again with your new password.";
            await HttpContext.SignOutAsync("CustomAuth");
            return RedirectToAction("Login");
        }
    }
}
