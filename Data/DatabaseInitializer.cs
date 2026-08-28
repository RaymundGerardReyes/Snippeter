using Microsoft.Data.Sqlite;

namespace ClipboardManager.Data
{
    public class DatabaseInitializer
    {
        private readonly string _dbPath;

        public DatabaseInitializer(string dbPath)
        {
            _dbPath = dbPath;
        }

        public void Initialize()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            string schema = @"
CREATE TABLE IF NOT EXISTS clipboard_items (
    id TEXT PRIMARY KEY,
    windows_id TEXT NOT NULL,
    created_at TEXT NOT NULL,
    content_type TEXT NOT NULL,
    is_sensitive INTEGER NOT NULL DEFAULT 0,
    is_pinned INTEGER NOT NULL DEFAULT 0,
    masked_preview TEXT,
    primary_category TEXT NOT NULL,
    storage_state TEXT NOT NULL,
    expires_at TEXT NULL
);

CREATE VIRTUAL TABLE IF NOT EXISTS clipboard_fts 
USING fts5(
    search_text,
    tokenize='unicode61'
);
";
            using var command = connection.CreateCommand();
            command.CommandText = schema;
            command.ExecuteNonQuery();
        }
    }
}
