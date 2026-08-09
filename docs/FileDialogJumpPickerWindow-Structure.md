# FileDialogJumpPickerWindow Structure

`FileDialogJumpPickerWindow` is split into partial files by responsibility. Keep `FileDialogJumpPickerWindow.xaml.cs` focused on shared state, construction, and lifecycle wiring.

## File Map

| File | Responsibility |
|---|---|
| `Views/FileDialogJumpPickerWindow.xaml.cs` | Shared state, constructor, initialization, close/size/activation hooks, and basic focus retry helpers. |
| `Views/FileDialogJumpPickerWindow.Hooks.cs` | Outside-click dismissal hooks, foreground/dock WinEvent hooks, keyboard hook installation, and external editable-focus detection. |
| `Views/FileDialogJumpPickerWindow.Keyboard.cs` | Keyboard command routing, search refresh scheduling, character mapping, and key interception checks. |
| `Views/FileDialogJumpPickerWindow.Search.cs` | Candidate list construction, filtering, Everything folder query scheduling, quick indices, and search editor operations. |
| `Views/FileDialogJumpPickerWindow.PreviewFooter.cs` | Preview popup content/placement, footer hints, panel modifier matching, star hotkey matching, and filter mode cycling. |
| `Views/FileDialogJumpPickerWindow.Placement.cs` | HWND source initialization, WndProc, rendered-time placement, focus steal scheduling, dock follow, DPI-aware positioning, and dock owner hooks. |
| `Views/FileDialogJumpPickerWindow.ListInteraction.cs` | List mouse actions, row context menu commands, favorite toggles, quick-index jumps, selection movement, and scroll tracking. |
| `Views/FileDialogJumpPickerWindow.RefreshNavigation.cs` | Selection commit, external refresh, deferred refresh, candidate equivalence checks, path normalization, and keep-open navigation. |

## Placement Rules

- Do not add feature logic back into `FileDialogJumpPickerWindow.xaml.cs` unless it is shared state or lifecycle wiring.
- Keep hook callbacks thin; put behavior in focused helper methods in the closest partial.
- Keep file-dialog navigation and dock placement logic in `FileJump/` services when it can be expressed without direct WPF state.
- Keep UI-only search, preview, and list interaction behavior in the picker partials until a service boundary is clear.
