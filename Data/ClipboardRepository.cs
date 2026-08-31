using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ClipboardManager.Models;

namespace ClipboardManager.Data
{
    public class ClipboardRepository : IClipboardRepository
    {
        private readonly string _connectionString;

        public ClipboardRepository(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task AddAsync(ClipboardRecord record)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO clipboard_items 
                    (id, windows_id, created_at, content_type, protection_state, safe_text, primary_category, expires_at, is_pinned)
                    VALUES ($id, $windows_id, $created_at, $content_type, $protection_state, $safe_text, $primary_category, $expires_at, $is_pinned);";

                insertCmd.Parameters.AddWithValue("$id", record.Item.Id);
                insertCmd.Parameters.AddWithValue("$windows_id", (object?)record.Item.WindowsId ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("$created_at", record.Item.CreatedAt.ToString("O"));
                insertCmd.Parameters.AddWithValue("$content_type", record.Item.ContentType);
                insertCmd.Parameters.AddWithValue("$protection_state", record.Item.ProtectionState.ToString());
                insertCmd.Parameters.AddWithValue("$safe_text", (object?)record.Item.SafeText ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("$primary_category", record.Item.PrimaryCategory.ToString());
                insertCmd.Parameters.AddWithValue("$expires_at", record.Item.ExpiresAt.HasValue ? record.Item.ExpiresAt.Value.ToString("O") : DBNull.Value);
                insertCmd.Parameters.AddWithValue("$is_pinned", record.Item.IsPinned ? 1 : 0);

                await insertCmd.ExecuteNonQueryAsync();

                // Core Security Invariant: Protected/Failed records NEVER enter the FTS index
                if (record.Item.ProtectionState == ClipboardProtectionState.Normal && !string.IsNullOrWhiteSpace(record.Projection.SearchText))
                {
                    var ftsCmd = connection.CreateCommand();
                    ftsCmd.Transaction = transaction;
                    ftsCmd.CommandText = @"
                        INSERT INTO clipboard_fts (item_id, search_text) 
                        VALUES ($item_id, $search_text);";

                    ftsCmd.Parameters.AddWithValue("$item_id", record.Item.Id);
                    ftsCmd.Parameters.AddWithValue("$search_text", record.Projection.SearchText);
                    await ftsCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(string id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var deleteFtsCmd = connection.CreateCommand();
                deleteFtsCmd.Transaction = transaction;
                deleteFtsCmd.CommandText = "DELETE FROM clipboard_fts WHERE item_id = $id;";
                deleteFtsCmd.Parameters.AddWithValue("$id", id);
                await deleteFtsCmd.ExecuteNonQueryAsync();

                var deleteCoreCmd = connection.CreateCommand();
                deleteCoreCmd.Transaction = transaction;
                deleteCoreCmd.CommandText = "DELETE FROM clipboard_items WHERE id = $id;";
                deleteCoreCmd.Parameters.AddWithValue("$id", id);
                await deleteCoreCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<ClipboardItem>> SearchAsync(ParsedSearchQuery query)
        {
            var list = new List<ClipboardItem>();
            if (string.IsNullOrWhiteSpace(query.FtsSafeExpression))
                return list;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT i.id, i.windows_id, i.created_at, i.content_type, i.protection_state, 
                       i.safe_text, i.primary_category, i.expires_at, i.is_pinned
                FROM clipboard_items i
                JOIN clipboard_fts f ON i.id = f.item_id
                WHERE clipboard_fts MATCH $query;";
            cmd.Parameters.AddWithValue("$query", query.FtsSafeExpression);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapFromReader(reader));
            }
            return list;
        }

        public async Task<ClipboardItem?> GetByIdAsync(string id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, windows_id, created_at, content_type, protection_state, 
                       safe_text, primary_category, expires_at, is_pinned
                FROM clipboard_items
                WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapFromReader(reader);
            }
            return null;
        }

        public async Task<List<ClipboardItem>> GetRecentAsync(int limit = 50, int offset = 0)
        {
            var list = new List<ClipboardItem>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, windows_id, created_at, content_type, protection_state, 
                       safe_text, primary_category, expires_at, is_pinned
                FROM clipboard_items
                ORDER BY is_pinned DESC, created_at DESC
                LIMIT $limit OFFSET $offset;";
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.Parameters.AddWithValue("$offset", offset);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapFromReader(reader));
            }
            return list;
        }

        public async Task ClearUnpinnedAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var deleteFtsCmd = connection.CreateCommand();
                deleteFtsCmd.Transaction = transaction;
                deleteFtsCmd.CommandText = @"
                    DELETE FROM clipboard_fts 
                    WHERE item_id IN (SELECT id FROM clipboard_items WHERE is_pinned = 0);";
                await deleteFtsCmd.ExecuteNonQueryAsync();

                var deleteCoreCmd = connection.CreateCommand();
                deleteCoreCmd.Transaction = transaction;
                deleteCoreCmd.CommandText = "DELETE FROM clipboard_items WHERE is_pinned = 0;";
                await deleteCoreCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteExpiredAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var now = DateTimeOffset.UtcNow.ToString("O");

                var deleteFtsCmd = connection.CreateCommand();
                deleteFtsCmd.Transaction = transaction;
                deleteFtsCmd.CommandText = @"
                    DELETE FROM clipboard_fts 
                    WHERE item_id IN (SELECT id FROM clipboard_items WHERE expires_at IS NOT NULL AND expires_at <= $now);";
                deleteFtsCmd.Parameters.AddWithValue("$now", now);
                await deleteFtsCmd.ExecuteNonQueryAsync();

                var deleteCoreCmd = connection.CreateCommand();
                deleteCoreCmd.Transaction = transaction;
                deleteCoreCmd.CommandText = "DELETE FROM clipboard_items WHERE expires_at IS NOT NULL AND expires_at <= $now;";
                deleteCoreCmd.Parameters.AddWithValue("$now", now);
                await deleteCoreCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SetPinnedAsync(string id, bool isPinned)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE clipboard_items SET is_pinned = $is_pinned WHERE id = $id;";
            cmd.Parameters.AddWithValue("$is_pinned", isPinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private static ClipboardItem MapFromReader(SqliteDataReader reader)
        {
            return new ClipboardItem
            {
                Id = reader.GetString(0),
                WindowsId = reader.IsDBNull(1) ? null : reader.GetString(1),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(2)),
                ContentType = reader.GetString(3),
                ProtectionState = Enum.TryParse<ClipboardProtectionState>(reader.GetString(4), out var st) ? st : ClipboardProtectionState.Normal,
                SafeText = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                PrimaryCategory = Enum.TryParse<PrivacyCategory>(reader.GetString(6), out var cat) ? cat : PrivacyCategory.Normal,
                ExpiresAt = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
                IsPinned = reader.GetInt32(8) == 1
            };
        }
    }
}
