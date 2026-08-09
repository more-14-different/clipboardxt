# SettingsWindow Structure

`SettingsWindow` is split into partial files by settings area. Keep `SettingsWindow.xaml.cs` focused on shared pending state, construction, initial control population, and window lifecycle.

## File Map

| File | Responsibility |
|---|---|
| `Views/SettingsWindow.xaml.cs` | Shared pending settings state, constructor, initial UI population, close/load lifecycle hooks. |
| `Views/SettingsWindow.CustomDialogs.cs` | Custom file-dialog rule list reload, delete, wizard launch, import, replace, and export. |
| `Views/SettingsWindow.Hotkeys.cs` | Hotkey modifier detection and all hotkey recording text-box handlers. |
| `Views/SettingsWindow.GeneralOptions.cs` | Theme, popup position, click-hide, startup/admin/update, Win+V replacement, and clear-on-exit toggles. |
| `Views/SettingsWindow.ExclusionApps.cs` | Exclusion app list rendering, opening the process picker, selected-process insertion, and deletion. |
| `Views/SettingsWindow.FileJumpOptions.cs` | Shell injection, file-jump follow/open/auto-sync/Everything options, and Explorer quick-find toggles. |
| `Views/SettingsWindow.ClipboardOptions.cs` | Panel modifier, paste simulation, double-click paste, batch paste toggles, opacity, and clear-history command. |
| `Views/SettingsWindow.SaveCancel.cs` | Validation, applying pending settings to `AppSettings`, and cancel theme rollback. |

## Placement Rules

- New XAML event handlers should go into the partial for the tab or setting area they belong to.
- Keep hotkey recording decisions in `Services/HotkeyRecordingController.cs`; `SettingsWindow.Hotkeys.cs` should only coordinate UI state and pending settings fields.
- Keep save-time parsing, range checks, and hotkey conflict checks in `Services/SettingsSaveValidator.cs`; `SettingsWindow.SaveCancel.cs` should apply validated values and handle window close/cancel behavior.
- Keep validation and final assignment in `SaveCancel` unless it becomes reusable settings-domain logic.
- Keep process-picker UI in `ExclusionApps` until it needs reuse outside settings.
- Keep process enumeration and filtering in `Services/ProcessNameCatalog.cs`; keep the picker UI in `Views/ProcessPickerDialog.cs`.
- Do not add new feature logic to `SettingsWindow.xaml.cs` unless it is constructor setup or shared pending state.
