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
);

CREATE TABLE IF NOT EXISTS privacy_settings (
    key TEXT PRIMARY KEY,
    json_value TEXT NOT NULL
);
