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
    /// Stores each campaign as its own JSON file under
    /// C:/BulkMessaging/Campaigns/Drafts/{id}.json.
    /// </summary>
    public class CampaignService
    {
        private readonly string _folderPath;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public CampaignService()
        {
            _folderPath = Path.Combine("C:", "BulkMessaging", "Campaigns", "Drafts");
            Directory.CreateDirectory(_folderPath);
        }

        private string GetFilePath(string id) => Path.Combine(_folderPath, $"{id}.json");

        public async Task<List<CampaignModel>> GetAllAsync()
        {
            var campaigns = new List<CampaignModel>();

            if (!Directory.Exists(_folderPath))
                return campaigns;

            foreach (var file in Directory.GetFiles(_folderPath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var campaign = JsonSerializer.Deserialize<CampaignModel>(json, JsonOptions);
                    if (campaign != null)
                        campaigns.Add(campaign);
                }
                catch
                {
                    // Skip a file that fails to parse instead of failing the whole list.
                }
            }

            return campaigns.OrderByDescending(c => c.CreatedAt).ToList();
        }

        public async Task<CampaignModel?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var path = GetFilePath(id);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<CampaignModel>(json, JsonOptions);
        }

        public async Task<CampaignModel> SaveAsync(CampaignModel campaign)
        {
            if (string.IsNullOrWhiteSpace(campaign.Id))
            {
                campaign.Id = Guid.NewGuid().ToString("N");
                campaign.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                var existing = await GetByIdAsync(campaign.Id);
                campaign.CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow;
            }

            campaign.UpdatedAt = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(campaign, JsonOptions);
            await File.WriteAllTextAsync(GetFilePath(campaign.Id), json);

            return campaign;
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