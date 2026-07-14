using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BulkMessaging.Models;

namespace BulkMessaging.Services
{
    /// <summary>
    /// Stores each template as its own JSON file under
    /// C:/BulkMessaging/Templates/Drafts/{id}.json so templates can be
    /// listed, opened, edited, and re-saved later.
    /// </summary>
    public class TemplateService
    {
        private readonly string _folderPath;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public TemplateService()
        {
            _folderPath = Path.Combine("C:", "BulkMessaging", "Templates", "Drafts");
            Directory.CreateDirectory(_folderPath);
        }

        private string GetFilePath(string id) => Path.Combine(_folderPath, $"{id}.json");

        public async Task<List<TemplateModel>> GetAllAsync()
        {
            var templates = new List<TemplateModel>();

            if (!Directory.Exists(_folderPath))
                return templates;

            foreach (var file in Directory.GetFiles(_folderPath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var template = JsonSerializer.Deserialize<TemplateModel>(json, JsonOptions);
                    if (template != null)
                        templates.Add(template);
                }
                catch
                {
                    // Skip a file that fails to parse instead of failing the whole list.
                }
            }

            return templates.OrderByDescending(t => t.UpdatedAt).ToList();
        }

        public async Task<TemplateModel?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var path = GetFilePath(id);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<TemplateModel>(json, JsonOptions);
        }

        /// <summary>
        /// Creates a new template (when Id is empty) or overwrites the
        /// existing one (when Id matches a file already on disk).
        /// </summary>
        public async Task<TemplateModel> SaveAsync(TemplateModel template)
        {
            if (string.IsNullOrWhiteSpace(template.Id))
            {
                template.Id = Guid.NewGuid().ToString("N");
                template.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                var existing = await GetByIdAsync(template.Id);
                template.CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow;
            }

            template.UpdatedAt = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(template, JsonOptions);
            await File.WriteAllTextAsync(GetFilePath(template.Id), json);

            return template;
        }

        public Task DeleteAsync(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                var path = GetFilePath(id);
                if (File.Exists(path))
                    File.Delete(path);
            }

            return Task.CompletedTask;
        }
    }
}
