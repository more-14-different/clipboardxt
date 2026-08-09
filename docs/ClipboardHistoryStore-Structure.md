# ClipboardHistoryStore Structure

`ClipboardHistoryStore` is split into partial files by SQLite responsibility. Keep `ClipboardHistoryStore.cs` focused on shared constants, database paths, construction, and top-level state.

## File Map

| File | Responsibility |
|---|---|
| `Services/ClipboardHistoryStore.cs` | Shared constants, pinyin mode, database path/connection string, constructor. |
| `Services/ClipboardHistoryStore.Schema.cs` | Schema creation, migrations, connection opening, timestamp helpers, and command creation. |
| `Services/ClipboardHistoryStore.Archive.cs` | Archive bucket table creation, bucket selection, and archive/delete core flow. |
| `Services/ClipboardHistoryStore.Search.cs` | Newest/history loading, FTS/search queries, managed pinyin search, search clauses, and LIKE escaping. |
| `Services/ClipboardHistoryStore.PruneRead.cs` | Prune/archive excess entries and materialize `ClipboardEntry` rows from SQLite readers. |
| `Services/ClipboardHistoryStore.Write.cs` | Insert, delete/archive by id, copied-at/text/star/shortcut updates. |
| `Services/ClipboardHistoryStore.Pinyin.cs` | Pinyin blob rebuild and searchable text construction helpers. |
| `Services/ClipboardHistoryStore.Delete.cs` | Bulk deletion of non-shortcut history entries. |

## Placement Rules

- Keep SQL schema and migrations in `Schema`.
- Keep archive bucket metadata and cold-storage movement in `Archive`.
- Keep query construction and search filtering in `Search`.
- Keep row-to-model materialization in `PruneRead` unless it grows enough to deserve its own reader partial.
- Keep mutation methods in `Write`.
- Keep pinyin index rebuild and searchable text helpers in `Pinyin`.
