using System;
using System.IO;
using System.Threading.Tasks;
using BulkMessaging.Models;
using BulkMessaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BulkMessaging.Controllers
{
    [Authorize]
    public class TemplatesController : Controller
    {
        private readonly TemplateService _templateService;

        public TemplatesController(TemplateService templateService)
        {
            _templateService = templateService;
        }

        // GET /Templates/Index — lists every saved template as a card.
        public async Task<IActionResult> Index()
        {
            var templates = await _templateService.GetAllAsync();
            return View(templates);
        }

        // GET /Templates/NewTemplate — blank editor.
        [HttpGet]
        public IActionResult NewTemplate()
        {
            return View("NewTemplate/Index", new TemplateModel());
        }

        // GET /Templates/EditTemplate/{id} — reopens a saved template in the
        // same editor view, pre-filled.
        [HttpGet]
        public async Task<IActionResult> EditTemplate(string id)
        {
            var template = await _templateService.GetByIdAsync(id);
            if (template == null)
                return NotFound();

            return View("NewTemplate/Index", template);
        }

        // POST /Templates/SaveTemplate — handles both create (Id empty) and
        // update (Id set) since the form posts here either way.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTemplate(TemplateModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Template name is required.");
                return View("NewTemplate/Index", model);
            }

            await _templateService.SaveAsync(model);

            return RedirectToAction("Index");
        }

        // POST /Templates/DeleteTemplate — removes the JSON file.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTemplate(string id)
        {
            await _templateService.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        // POST /Templates/UploadImage — used by the "Choose from computer"
        // control in the editor. Saves into the same folder Program.cs
        // maps as static files at the "/uploads" URL prefix:
        //
        //   var uploadsPath = @"C:\BulkMessager\Templates\Uploads";
        //   app.UseStaticFiles(new StaticFileOptions
        //   {
        //       FileProvider = new PhysicalFileProvider(uploadsPath),
        //       RequestPath = "/uploads"
        //   });
        //
        // Both the folder AND the returned URL prefix have to match that
        // mapping or the browser 404s trying to load the image.
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded" });

            var uploadsFolder = @"C:\BulkMessager\Templates\Uploads";
            Directory.CreateDirectory(uploadsFolder);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/{fileName}";

            return Ok(new { url });
        }
    }
}