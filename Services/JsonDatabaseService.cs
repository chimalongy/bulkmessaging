using System.Text.Json;

namespace BulkMessaging.Services
{
    public class JsonDatabaseService<T> where T : class
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonDatabaseService(string fileName)
        {
            var dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "App_Data");
            Directory.CreateDirectory(dataDirectory);
            _filePath = Path.Combine(dataDirectory, fileName);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<List<T>> GetAllAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                    return new List<T>();

                var json = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<T>();

                var result = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions);
                return result ?? new List<T>();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAllAsync(List<T> items)
        {
            await _lock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(items, _jsonOptions);
                await File.WriteAllTextAsync(_filePath, json);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<T?> GetByPredicateAsync(Func<T, bool> predicate)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(predicate);
        }

        public async Task AddAsync(T item)
        {
            var all = await GetAllAsync();
            all.Add(item);
            await SaveAllAsync(all);
        }

        public async Task UpdateAsync(Predicate<T> predicate, T updatedItem)
        {
            var all = await GetAllAsync();
            var index = all.FindIndex(predicate);
            if (index >= 0)
            {
                all[index] = updatedItem;
                await SaveAllAsync(all);
            }
        }

        public async Task DeleteAsync(Predicate<T> predicate)
        {
            var all = await GetAllAsync();
            all.RemoveAll(predicate);
            await SaveAllAsync(all);
        }
    }
}
