# ExplorerQuickFindController Structure

`ExplorerQuickFindController` is split into partial files by runtime responsibility. Keep `ExplorerQuickFindController.cs` focused on shared state, construction, hook installation, disposal, and top-level lifecycle.

## File Map

| File | Responsibility |
|---|---|
| `Services/ExplorerQuickFindController.cs` | Shared state, constructor, keyboard hook install/uninstall, disposal cleanup. |
| `Services/ExplorerQuickFindController.Hooks.cs` | Low-level keyboard hook callback, fast-path session key routing, session start detection, modifier-key detection. |
| `Services/ExplorerQuickFindController.Session.cs` | Dispatcher-thread session startup, typed query editing, picker window lifecycle, item activation, and session reset. |
| `Services/ExplorerQuickFindController.Query.cs` | Debounced staged Everything queries and UI result posting. |
| `Services/ExplorerQuickFindController.QueryHelpers.cs` | Everything query construction, path filtering, relative/searchable path helpers, and result-list merging. |
| `Services/ExplorerQuickFindController.Navigation.cs` | Explorer/file-dialog navigation, Shell COM selection, shell-window matching, and Shell API fallback. |
| `Services/ExplorerQuickFindController.ContextKeyboardDiagnostics.cs` | Fast focus/context checks, keyboard state conversion, Everything error formatting, and diagnostics logging. |

## Placement Rules

- Keep low-level hook callbacks fast; no COM, UI Automation, or UI work in `Hooks`.
- Keep Everything IPC/query composition in `Query` and `QueryHelpers`.
- Keep Shell COM and Explorer selection behavior in `Navigation`.
- Keep window state and session lifecycle in `Session`.
- If a helper becomes useful outside Explorer quick find, move it to a standalone service with focused tests.
