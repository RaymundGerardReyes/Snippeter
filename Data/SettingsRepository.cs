using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using ClipboardManager.Models;
using Microsoft.Data.Sqlite;

namespace ClipboardManager.Data
{
    public interface ISettingsRepository
    {
        Task<PrivacyMaskingSettings> GetSettingsAsync();
        Task SaveSettingsAsync(PrivacyMaskingSettings settings);
    }

    public class SqliteSettingsRepository : ISettingsRepository
    {
        private readonly string _connectionString;
        private const string SettingsKey = "PrivacyMaskingSettings";

        public SqliteSettingsRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<PrivacyMaskingSettings> GetSettingsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT json_value FROM privacy_settings WHERE key = @key;";
            command.Parameters.AddWithValue("@key", SettingsKey);

            var result = await command.ExecuteScalarAsync();
            if (result is string json && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    return JsonSerializer.Deserialize<PrivacyMaskingSettings>(json) ?? PrivacyMaskingSettings.Default;
                }
                catch
                {
                    return PrivacyMaskingSettings.Default;
                }
            }

            return PrivacyMaskingSettings.Default;
        }

        public async Task SaveSettingsAsync(PrivacyMaskingSettings settings)
        {
            if (settings == null) return;
            string json = JsonSerializer.Serialize(settings);

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO privacy_settings (key, json_value) 
                VALUES (@key, @json)
                ON CONFLICT(key) DO UPDATE SET json_value = @json;";
            command.Parameters.AddWithValue("@key", SettingsKey);
            command.Parameters.AddWithValue("@json", json);

            await command.ExecuteNonQueryAsync();
        }
    }
}
