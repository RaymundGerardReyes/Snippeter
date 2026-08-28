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

        public async Task AddAsync(ClipboardItem item, string safeSearchText)
        {
            await AddAsync(new ClipboardRecord
            {
                Item = item,
                Projection = new SearchProjection
                {
                    SearchText = safeSearchText,
                    ContainsSensitiveMaterial = item.IsSensitive
                }
            });
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
                    (id, windows_id, created_at, content_type, is_sensitive, is_pinned, masked_preview, primary_category, storage_state, expires_at)
                    VALUES ($id, $windows_id, $created_at, $content_type, $is_sensitive, $is_pinned, $masked_preview, $primary_category, $storage_state, $expires_at)
                    RETURNING rowid;";
                
                insertCmd.Parameters.AddWithValue("$id", record.Item.Id);
                insertCmd.Parameters.AddWithValue("$windows_id", record.Item.WindowsId);
                insertCmd.Parameters.AddWithValue("$created_at", record.Item.CreatedAt.ToString("O"));
                insertCmd.Parameters.AddWithValue("$content_type", record.Item.ContentType);
                insertCmd.Parameters.AddWithValue("$is_sensitive", record.Item.IsSensitive ? 1 : 0);
                insertCmd.Parameters.AddWithValue("$is_pinned", record.Item.IsPinned ? 1 : 0);
                insertCmd.Parameters.AddWithValue("$masked_preview", record.Item.MaskedPreview);
                insertCmd.Parameters.AddWithValue("$primary_category", record.Item.PrimaryCategory.ToString());
                insertCmd.Parameters.AddWithValue("$storage_state", record.Item.StorageState.ToString());
                insertCmd.Parameters.AddWithValue("$expires_at", record.Item.ExpiresAt.HasValue ? record.Item.ExpiresAt.Value.ToString("O") : DBNull.Value);
                
                var rowIdObj = await insertCmd.ExecuteScalarAsync();
                long rowId = rowIdObj != null ? (long)rowIdObj : 0;

                var ftsCmd = connection.CreateCommand();
                ftsCmd.Transaction = transaction;
                ftsCmd.CommandText = @"
                    INSERT INTO clipboard_fts (rowid, search_text) 
                    VALUES ($rowid, $search_text)";
                
                ftsCmd.Parameters.AddWithValue("$rowid", rowId);
                ftsCmd.Parameters.AddWithValue("$search_text", record.Projection.SearchText);
                await ftsCmd.ExecuteNonQueryAsync();

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
                var getRowIdCmd = connection.CreateCommand();
                getRowIdCmd.Transaction = transaction;
                getRowIdCmd.CommandText = "SELECT rowid FROM clipboard_items WHERE id = $id";
                getRowIdCmd.Parameters.AddWithValue("$id", id);
                var rowIdObj = await getRowIdCmd.ExecuteScalarAsync();
                
                if (rowIdObj != null)
                {
                    long rowId = (long)rowIdObj;

                    var deleteFtsCmd = connection.CreateCommand();
                    deleteFtsCmd.Transaction = transaction;
                    deleteFtsCmd.CommandText = "DELETE FROM clipboard_fts WHERE rowid = $rowid";
                    deleteFtsCmd.Parameters.AddWithValue("$rowid", rowId);
                    await deleteFtsCmd.ExecuteNonQueryAsync();

                    var deleteCoreCmd = connection.CreateCommand();
                    deleteCoreCmd.Transaction = transaction;
                    deleteCoreCmd.CommandText = "DELETE FROM clipboard_items WHERE rowid = $rowid";
                    deleteCoreCmd.Parameters.AddWithValue("$rowid", rowId);
                    await deleteCoreCmd.ExecuteNonQueryAsync();
                }

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
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT i.id, i.windows_id, i.created_at, i.content_type, i.is_sensitive, i.is_pinned, i.masked_preview, i.primary_category, i.storage_state, i.expires_at
                FROM clipboard_items i
                JOIN clipboard_fts f ON i.rowid = f.rowid
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
                SELECT id, windows_id, created_at, content_type, is_sensitive, is_pinned, masked_preview, primary_category, storage_state, expires_at
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
                SELECT id, windows_id, created_at, content_type, is_sensitive, is_pinned, masked_preview, primary_category, storage_state, expires_at
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
                var getRowIdsCmd = connection.CreateCommand();
                getRowIdsCmd.Transaction = transaction;
                getRowIdsCmd.CommandText = "SELECT rowid FROM clipboard_items WHERE is_pinned = 0";
                
                var rowIds = new List<long>();
                using (var reader = await getRowIdsCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rowIds.Add(reader.GetInt64(0));
                    }
                }

                foreach (var rowId in rowIds)
                {
                    var deleteFtsCmd = connection.CreateCommand();
                    deleteFtsCmd.Transaction = transaction;
                    deleteFtsCmd.CommandText = "DELETE FROM clipboard_fts WHERE rowid = $rowid";
                    deleteFtsCmd.Parameters.AddWithValue("$rowid", rowId);
                    await deleteFtsCmd.ExecuteNonQueryAsync();

                    var deleteCoreCmd = connection.CreateCommand();
                    deleteCoreCmd.Transaction = transaction;
                    deleteCoreCmd.CommandText = "DELETE FROM clipboard_items WHERE rowid = $rowid";
                    deleteCoreCmd.Parameters.AddWithValue("$rowid", rowId);
                    await deleteCoreCmd.ExecuteNonQueryAsync();
                }

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
                var getRowIdsCmd = connection.CreateCommand();
                getRowIdsCmd.Transaction = transaction;
                getRowIdsCmd.CommandText = "SELECT rowid FROM clipboard_items WHERE expires_at IS NOT NULL AND expires_at <= $now";
                getRowIdsCmd.Parameters.AddWithValue("$now", DateTimeOffset.Now.ToString("O"));

                var rowIds = new List<long>();
                using (var reader = await getRowIdsCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rowIds.Add(reader.GetInt64(0));
                    }
                }

                foreach (var rowId in rowIds)
                {
                    var deleteFtsCmd = connection.CreateCommand();
                    deleteFtsCmd.Transaction = transaction;
                    deleteFtsCmd.CommandText = "DELETE FROM clipboard_fts WHERE rowid = $rowid";
                    deleteFtsCmd.Parameters.AddWithValue("$rowid", rowId);
                    await deleteFtsCmd.ExecuteNonQueryAsync();

                    var deleteCoreCmd = connection.CreateCommand();
                    deleteCoreCmd.Transaction = transaction;
                    deleteCoreCmd.CommandText = "DELETE FROM clipboard_items WHERE rowid = $rowid";
                    deleteCoreCmd.Parameters.AddWithValue("$rowid", rowId);
                    await deleteCoreCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private ClipboardItem MapFromReader(SqliteDataReader reader)
        {
            return new ClipboardItem
            {
                Id = reader.GetString(0),
                WindowsId = reader.GetString(1),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(2)),
                ContentType = reader.GetString(3),
                IsSensitive = reader.GetInt32(4) == 1,
                IsPinned = reader.GetInt32(5) == 1,
                MaskedPreview = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                PrimaryCategory = Enum.TryParse<PrivacyCategory>(reader.GetString(7), out var cat) ? cat : PrivacyCategory.Normal,
                StorageState = Enum.TryParse<StorageState>(reader.GetString(8), out var st) ? st : StorageState.WindowsOnly,
                ExpiresAt = reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9))
            };
        }
        
        public Task SetPinnedAsync(string id, bool isPinned) => throw new NotImplementedException();
    }
}
