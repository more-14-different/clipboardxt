# AppSettings Structure

`AppSettings` is split into partial files by settings responsibility. Keep `AppSettings.cs` focused on data fields and default values.

## File Map

| File | Responsibility |
|---|---|
| `Models/AppSettings.cs` | `QuickPasteEntry`, core settings fields, the operation-state toggle/session store owner, file-jump settings fields, popup sizing fields, batch settings fields, recent-folder data fields, and exclusion app fields. |
| `Models/AppSettings.RecentFolders.cs` | Recent file-dialog folder confirmation, insertion, normalization, app install-directory guard, and removal. |
| `Models/AppSettings.ExplorerQuickFind.cs` | Explorer quick-find and file-jump Everything feature settings fields. |
| `Models/AppSettings.Display.cs` | Hotkey display-name properties and virtual-key formatting. |
| `Models/AppSettings.Normalize.cs` | Runtime normalization/default repair for popup sizes, page keys, and star toggle keys. |
| `Models/AppSettings.Persistence.cs` | Settings file locations, load path, legacy settings migration, and recent-folder migration. |
| `Models/AppSettings.Save.cs` | Debounced async save, synchronous save, and pending-save flush. |
| `Models/AppSettings.Copy.cs` | Shallow copy used for debounced save snapshots. |

## Placement Rules

- Add new persisted setting fields to `AppSettings.cs` or the closest feature-field partial.
- Keep load-time compatibility fixes in `Persistence`.
- Keep range/default repair in `Normalize`.
- Keep save scheduling and file writes in `Save`.
- Keep display-only formatting in `Display`.
