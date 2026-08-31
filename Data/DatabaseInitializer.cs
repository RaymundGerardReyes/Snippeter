using System;
using Microsoft.Data.Sqlite;

namespace ClipboardManager.Data
{
    public class DatabaseInitializer
    {
        private const int CurrentSchemaVersion = 2;
        private readonly string _dbPath;

        public DatabaseInitializer(string dbPath) => _dbPath = dbPath;

        public void Initialize()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            int currentVersion = GetUserVersion(connection);

            if (currentVersion == 0)
            {
                CreateSchemaV2(connection);
                SetUserVersion(connection, CurrentSchemaVersion);
            }
            else if (currentVersion == 1)
            {
                MigrateV1ToV2(connection);
                SetUserVersion(connection, CurrentSchemaVersion);
            }
            else if (currentVersion > CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Unsupported database version: {currentVersion}");
            }
        }

        private static int GetUserVersion(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private static void SetUserVersion(SqliteConnection connection, int version)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA user_version = {version};";
            cmd.ExecuteNonQuery();
        }

        private static void CreateSchemaV2(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS clipboard_items (
                    id TEXT PRIMARY KEY,
                    windows_id TEXT NULL,
                    created_at TEXT NOT NULL,
                    content_type TEXT NOT NULL,
                    protection_state TEXT NOT NULL,
                    safe_text TEXT,
                    primary_category TEXT NOT NULL,
                    expires_at TEXT NULL,
                    is_pinned INTEGER NOT NULL DEFAULT 0
                );

                CREATE VIRTUAL TABLE IF NOT EXISTS clipboard_fts 
                USING fts5(
                    item_id UNINDEXED,
                    search_text,
                    tokenize='unicode61'
                );";
            cmd.ExecuteNonQuery();
        }

        private static void MigrateV1ToV2(SqliteConnection connection)
        {
            using var tx = connection.BeginTransaction();
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    CREATE TABLE clipboard_items_v2 (
                        id TEXT PRIMARY KEY,
                        windows_id TEXT NULL,
                        created_at TEXT NOT NULL,
                        content_type TEXT NOT NULL,
                        protection_state TEXT NOT NULL,
                        safe_text TEXT,
                        primary_category TEXT NOT NULL,
                        expires_at TEXT NULL,
                        is_pinned INTEGER NOT NULL DEFAULT 0
                    );

                    INSERT INTO clipboard_items_v2 (id, windows_id, created_at, content_type, protection_state, safe_text, primary_category, expires_at, is_pinned)
                    SELECT 
                        id, 
                        windows_id, 
                        created_at, 
                        content_type, 
                        CASE WHEN is_sensitive = 1 THEN 'Protected' ELSE 'Normal' END,
                        masked_preview, 
                        primary_category,
                        expires_at,
                        is_pinned
                    FROM clipboard_items;

                    DROP TABLE clipboard_items;
                    ALTER TABLE clipboard_items_v2 RENAME TO clipboard_items;

                    DROP TABLE IF EXISTS clipboard_fts;
                    CREATE VIRTUAL TABLE clipboard_fts USING fts5(item_id UNINDEXED, search_text, tokenize='unicode61');";
                
                cmd.ExecuteNonQuery();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
