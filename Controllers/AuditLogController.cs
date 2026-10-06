using BulkMessaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BulkMessaging.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class AuditLogController : Controller
    {
        private readonly AuditLogService _auditLogService;

        public AuditLogController(AuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _auditLogService.GetAllAsync();
            return View(logs);
        }
    }
}
