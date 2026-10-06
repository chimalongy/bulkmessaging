using BulkMessaging.Models;
using BulkMessaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkMessaging.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class SenderAccountsController : Controller
    {
        private readonly SenderAccountService _senderAccountService;

        public SenderAccountsController(SenderAccountService senderAccountService)
        {
            _senderAccountService = senderAccountService;
        }

        private string CurrentUsername => User.Identity?.Name ?? "system";
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        private string? CurrentIp => HttpContext.Connection.RemoteIpAddress?.ToString();

        // GET /SenderAccounts or /SenderAccounts/Index
        public async Task<IActionResult> Index()
        {
            var accounts = await _senderAccountService.GetAllSenderAccountsAsync();
            return View(accounts);
        }

        // GET /SenderAccounts/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateSenderAccountViewModel());
        }

        // POST /SenderAccounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSenderAccountViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, message) = await _senderAccountService.CreateSenderAccountAsync(
                model, CurrentUsername, CurrentRole, CurrentIp);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        // GET /SenderAccounts/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var account = await _senderAccountService.GetByIdAsync(id);
            if (account == null)
                return NotFound();

            var model = new EditSenderAccountViewModel
            {
                Id = account.Id,
                FromEmail = account.FromEmail,
                FromName = account.FromName,
                IsActive = account.IsActive
                // ApiKey intentionally left blank; only overwritten if re-entered
            };

            return View(model);
        }

        // POST /SenderAccounts/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditSenderAccountViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, message) = await _senderAccountService.UpdateSenderAccountAsync(
                model, CurrentUsername, CurrentRole, CurrentIp);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        // POST /SenderAccounts/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var (success, message) = await _senderAccountService.DeleteSenderAccountAsync(
                id, CurrentUsername, CurrentRole, CurrentIp);

            if (!success)
                TempData["ErrorMessage"] = message;
            else
                TempData["SuccessMessage"] = message;

            return RedirectToAction(nameof(Index));
        }
    }
}