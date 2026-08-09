# Manual Smoke Test

Use this checklist after refactors that touch `PopupWindow`, paste behavior, clipboard monitoring, batch queues, or file-jump flows.

## Setup

1. Exit any running ClipboardX instance.
2. Start the app from the repository root:

   ```powershell
   dotnet run --project ClipboardManager.csproj
   ```

3. Use a plain text target such as Notepad, VS Code, or another editor for paste checks.

## Checklist

| Area | Steps | Expected Result |
|---|---|---|
| Popup open | Copy a text snippet, then press the configured clipboard hotkey, default `Ctrl+``. | Popup opens, newest item is at the top, original target does not lose input context unexpectedly. |
| Search | Copy several distinct snippets, including Chinese text if possible. Open popup and type a keyword or pinyin initials. | List filters correctly. Clearing search restores the list. |
| Panel operation state | Enable `Settings > General > Remember operation state`. Enter different searches in the clipboard, file-jump, and Explorer Quick Find panels; dismiss and reopen each panel. Then consume valid rows using each supported Enter/Ctrl+Enter/click path and reopen the panels. Finally disable the setting and repeat. | With the setting enabled, each panel restores its own query/filter state after a non-consuming dismissal. A valid consumed action clears that panel's query, while an invalid action does not. With the setting disabled, each panel starts clean. Queries are not written to `settings.json`. |
| Single paste | Open popup over an editor, select one text item, press `Enter`. | Item is pasted into the original target and the popup closes. |
| FIFO queue | Add several visible items to FIFO mode, then paste repeatedly in the target app with `Ctrl+V` or the configured paste shortcut. | Items paste in first-in-first-out order. Queue badges/counts update and the queue drains. |
| LIFO queue | Add several visible items to LIFO mode, then paste repeatedly in the target app. | Items paste in last-in-first-out order. Queue badges/counts update and the queue drains. |
| Paste all | Put multiple text items in the batch queue, open the batch menu, and run paste all. | Items paste according to the configured merge/sequential strategy. Queue clears and the app does not hang. |
| Delete and dedup | Copy duplicate text, delete a history item, and observe queue badges. | Duplicate history is deduplicated. Deleting an item refreshes the list and does not leave stale queue badges. |
| File dialog jump | Open a standard Open/Save dialog, press the configured file-jump hotkey, default `Ctrl+G`, and choose a folder. | Jump picker opens and the dialog navigates to the selected folder. |

## Pass Criteria

- No crash or visible long freeze.
- Popup open, search, single paste, FIFO/LIFO queue paste, paste all, delete/dedup, and file dialog jump all work.
- FIFO order is oldest queued first.
- LIFO order is newest queued first.
- Queue badges and list ordering stay consistent after paste, delete, and dedup.
- No persistent files are created outside normal ClipboardX data/log/temp behavior.

## Out Of Scope

These checks are intentionally not covered by this quick smoke test:

- installer and update flows;
- all custom file-dialog rules;
- every supported third-party file manager;
- real clipboard-lock contention from other apps;
- multi-monitor DPI edge cases.
