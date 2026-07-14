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
    /// Stores each message as its own JSON file under
    /// C:/BulkMessaging/Messages/Drafts/{id}.json. Each file carries a
    /// CampaignId so a campaign's messages can be filtered out of the
    /// full set.
    /// </summary>
    public class MessageService
    {
        private readonly string _folderPath;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public MessageService()
        {
            _folderPath = Path.Combine("C:", "BulkMessaging", "Messages", "Drafts");
            Directory.CreateDirectory(_folderPath);
        }

        private string GetFilePath(string id) => Path.Combine(_folderPath, $"{id}.json");

        public async Task<List<MessageModel>> GetAllAsync()
        {
            var messages = new List<MessageModel>();

            if (!Directory.Exists(_folderPath))
                return messages;

            foreach (var file in Directory.GetFiles(_folderPath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var message = JsonSerializer.Deserialize<MessageModel>(json, JsonOptions);
                    if (message != null)
                        messages.Add(message);
                }
                catch
                {
                    // Skip a file that fails to parse instead of failing the whole list.
                }
            }

            return messages.OrderByDescending(m => m.CreatedAt).ToList();
        }

        public async Task<List<MessageModel>> GetByCampaignIdAsync(string campaignId)
        {
            var all = await GetAllAsync();
            return all
                .Where(m => string.Equals(m.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<MessageModel?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var path = GetFilePath(id);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<MessageModel>(json, JsonOptions);
        }

        public async Task<MessageModel> SaveAsync(MessageModel message)
        {
            if (string.IsNullOrWhiteSpace(message.Id))
            {
                message.Id = Guid.NewGuid().ToString("N");
                message.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                var existing = await GetByIdAsync(message.Id);
                message.CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow;
            }

            message.UpdatedAt = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(message, JsonOptions);
            await File.WriteAllTextAsync(GetFilePath(message.Id), json);

            return message;
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