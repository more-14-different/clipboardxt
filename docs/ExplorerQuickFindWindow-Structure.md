# ExplorerQuickFindWindow Structure

`ExplorerQuickFindWindow` is split by UI responsibility. Keep `ExplorerQuickFindWindow.xaml.cs` focused on shared state, constructor wiring, and events.

## File Map

| File | Responsibility |
|---|---|
| `Views/ExplorerQuickFindWindow.xaml.cs` | Shared brushes/state, constructor, HWND hook registration, close notification, and public events. |
| `Views/ExplorerQuickFindWindow.Sizing.cs` | Applying persisted size settings, saving size, and resize-related WndProc handling. |
| `Views/ExplorerQuickFindWindow.Results.cs` | Brush caching, query text rendering, result row rendering, hint/count text. |
| `Views/ExplorerQuickFindWindow.Selection.cs` | Keyboard/mouse selection movement, selected path access, quick-index path access, and item activation. |
| `Views/ExplorerQuickFindWindow.Placement.cs` | Positioning near Explorer or cursor. |
| `Views/QuickFindResultItem.cs` | Quick-find result item model and scoped/global result builders. |

## Placement Rules

- Keep WPF rendering logic in `Results`.
- Keep navigation-selection helpers in `Selection`.
- Keep monitor/window coordinate logic in `Placement`.
- Keep pure result-shaping logic in `QuickFindResultItem`.
