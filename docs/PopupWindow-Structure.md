# PopupWindow Structure

`PopupWindow` is intentionally split into partial files by responsibility. Keep `PopupWindow.xaml.cs` small: it should hold shared fields, construction, initialization, and lifecycle wiring. New behavior should go into the closest existing partial file.

## File Map

| File | Responsibility |
|---|---|
| `Views/PopupWindow.xaml.cs` | Constructor wiring and top-level mouse preview hook. |
| `Views/PopupWindow.State.cs` | Shared fields, hook owner thunks, dependency properties, events, and diagnostic state. |
| `Views/PopupWindow.SettingsLifecycle.cs` | Thin placeholder for settings/lifecycle grouping; behavior lives in the focused settings partials below. |
| `Views/PopupWindow.SettingsInitialization.cs` | Popup initialization, initial settings load, hooks/listeners, and startup UI state. |
| `Views/PopupWindow.SettingsCleanup.cs` | Popup cleanup, hook uninstallation, hotkey unregistering, and listener removal. |
| `Views/PopupWindow.SettingsHotkeys.cs` | Runtime clipboard and file-jump hotkey updates. |
| `Views/PopupWindow.SettingsApply.cs` | Runtime settings application and popup panel layout settings. |
| `Views/PopupWindow.SettingsHistory.cs` | History clearing and queue cleanup. |
| `Views/PopupWindow.SettingsPinyin.cs` | Pinyin search index rebuild and version migration. |
| `Views/PopupWindow.WindowMessages.cs` | `WndProc` and low-level window message routing. |
| `Views/PopupWindow.PopupVisibility.cs` | Thin placeholder for popup visibility grouping; behavior lives in the focused visibility partials below. |
| `Views/PopupWindow.PopupVisibility.Toggle.cs` | Popup toggle, show, hide, and close-time cleanup flow. |
| `Views/PopupWindow.PopupVisibility.ZOrder.cs` | Pending-position SetWindowPos, topmost reassertion, and shell foreground Z-order fix. |
| `Views/PopupWindow.PopupVisibility.Shell.cs` | Shell foreground detection and foreground app exclusion checks. |
| `Views/PopupWindow.PopupVisibility.Size.cs` | Persisted popup size saving. |
| `Views/PopupWindow.PopupPlacement.cs` | Popup placement source selection, caret/mouse fallback, UI Automation caret lookup, and screen-boundary clamping. |
| `Views/PopupWindow.PopupDpi.cs` | Applies mapped popup coordinates and synchronizes HWND/WPF position across DPI changes. |
| `Views/PopupWindow.KeyboardHook.cs` | Thin low-level keyboard hook callback dispatcher. |
| `Views/PopupWindow.KeyboardHook.Global.cs` | Global keyboard-hook pre-popup handling: Win+V replacement, FIFO/LIFO paste advance, injected-key pass-through, and Alt cleanup while hidden. |
| `Views/PopupWindow.KeyboardHook.Helpers.cs` | Shared keyboard-hook modifier detection, registered hotkey matching, synthetic key cleanup, and Ctrl+H/J/K/L navigation. |
| `Views/PopupWindow.KeyboardHook.Editors.cs` | Keyboard handling for inline text-entry and quick-phrase edit popups. |
| `Views/PopupWindow.KeyboardHook.Menus.cs` | Keyboard handling for Alt-triggered batch/context menus and menu navigation. |
| `Views/PopupWindow.KeyboardHook.MainPanel.cs` | Main popup keyboard handling for search, selection, navigation, paste-by-index, preview, and close/delete commands. |
| `Views/PopupWindow.MouseHook.cs` | Low-level mouse hook static dispatch and popup mouse hook installation. |
| `Views/PopupWindow.MouseHook.Popup.cs` | Popup mouse hook callback: drag movement, drag-end position sync, and outside-click close handling. |
| `Views/PopupWindow.MouseHook.FileJumpAuto.cs` | File-jump first-click auto-navigation mouse hook installation and callback. |
| `Views/PopupWindow.MouseHook.FileJumpPersist.cs` | File-dialog confirm-click mouse hook and recent-folder persistence. |
| `Views/PopupWindow.EventHandlers.cs` | Thin placeholder for UI event-handler grouping; keep behavior in the focused event partials below. |
| `Views/PopupWindow.ListEvents.cs` | Clipboard list selection, scroll, left/middle/right mouse handling, and context-menu opening from rows. |
| `Views/PopupWindow.ContextMenu.cs` | Thin placeholder for context-menu grouping; behavior lives in the focused context-menu partials below. |
| `Views/PopupWindow.ContextMenu.Sync.cs` | Context-menu state synchronization for the active entry. |
| `Views/PopupWindow.ContextMenu.Navigation.cs` | Keyboard-open context menu placement, nav row rebuild, highlight movement, activation, and close. |
| `Views/PopupWindow.ContextMenu.Actions.cs` | Context-menu command implementations for paste, export, shortcut, star, and delete. |
| `Views/PopupWindow.ContextMenu.ClickHandlers.cs` | Mouse click event bridges for context-menu commands. |
| `Views/PopupWindow.EntryActions.cs` | Star toggling, entry removal, and two-step delete confirmation. |
| `Views/PopupWindow.FilterCommands.cs` | Type/quick-phrase filter cycling, paste-by-index, and settings command bridge. |
| `Views/PopupWindow.HeaderDrag.cs` | Header drag start state and drag diagnostics setup. |
| `Views/PopupWindow.FooterNavigation.cs` | Thin placeholder for footer/navigation grouping; behavior lives in the focused footer/navigation partials below. |
| `Views/PopupWindow.FooterHints.cs` | Footer hint text, compact hint labels, and shortcut-help popup toggle. |
| `Views/PopupWindow.KeyboardTextInput.cs` | Low-level virtual-key to character conversion for popup keyboard text input. |
| `Views/PopupWindow.ListNavigation.cs` | List selection movement, page scrolling, visible index updates, and list scroll viewer lookup. |
| `Views/PopupWindow.KeyboardPointSelection.cs` | Keyboard point-selection cursor, point toggling, and selected-index replacement. |
| `Views/PopupWindow.Search.cs` | Thin placeholder for search/filter grouping; behavior lives in the focused search partials below. |
| `Views/PopupWindow.SearchFilter.cs` | Popup filtering, result ordering, selected-row restoration, and empty-state text. |
| `Views/PopupWindow.SearchMetadata.cs` | Source metadata search matching and preview chip construction. |
| `Views/PopupWindow.SearchEditor.cs` | Search text editor state, caret/selection movement, deletion, highlight text, and inline rendering. |
| `Views/PopupWindow.ClipboardMonitoring.cs` | Thin placeholder for clipboard monitoring grouping; behavior lives in the focused monitor partials below. |
| `Views/PopupWindow.ClipboardMonitoring.ReadRetry.cs` | Monitor-side clipboard read retry helpers for transient clipboard lock failures. |
| `Views/PopupWindow.ClipboardMonitoring.SelfWrite.cs` | Self-write echo tracking and insertion of batch-merged clipboard entries. |
| `Views/PopupWindow.ClipboardMonitoring.Update.cs` | Clipboard update event handling and text/file/image entry capture. |
| `Views/PopupWindow.ClipboardMonitoring.Dedup.cs` | Duplicate removal, source preservation, volatile-source detection, and history trimming. |
| `Views/PopupWindow.ClipboardMonitoring.FileSummary.cs` | Compact file-drop summaries for diagnostics logging. |
| `Views/PopupWindow.QuickPaste.cs` | Thin placeholder for quick paste grouping; behavior lives in the focused quick-paste partials below. |
| `Views/PopupWindow.QuickPasteLoad.cs` | History loading and quick-paste entry projection into the popup item list. |
| `Views/PopupWindow.QuickPastePhraseEdit.cs` | Quick phrase popup editor, phrase caret/selection editing, and quick-paste persistence. |
| `Views/PopupWindow.EntryTextEdit.cs` | Inline text-entry edit popup, focus restoration, and save/cancel handlers. |
| `Views/PopupWindow.EntryPreview.cs` | Thin placeholder for entry preview grouping; behavior lives in the focused preview partials below. |
| `Views/PopupWindow.EntryPreviewPopup.cs` | Entry preview bubble open/close, positioning, and selection synchronization. |
| `Views/PopupWindow.EntryPreviewContent.cs` | Entry preview content reset, type dispatch, text rendering, and shared text-block clearing. |
| `Views/PopupWindow.EntryPreviewSource.cs` | Entry preview source app/window/path metadata rendering. |
| `Views/PopupWindow.EntryPreviewImage.cs` | Entry preview bitmap loading for stored images and image files. |
| `Views/PopupWindow.PasteClipboard.cs` | Text paste session creation and the `PopupWindow` adapter for local `SetText` writes. |
| `Views/PopupWindow.PasteEntry.cs` | Single-entry paste orchestration, shared paste state, and dispatch to type-specific clipboard preparation. |
| `Views/PopupWindow.PasteEntry.Selection.cs` | Current selected-item paste entry point. |
| `Views/PopupWindow.PasteEntry.Text.cs` | Single text-entry clipboard preparation and non-clipboard text fallback. |
| `Views/PopupWindow.PasteEntry.Image.cs` | Single image-entry clipboard preparation and FileDrop fallback. |
| `Views/PopupWindow.PasteEntry.Files.cs` | Single file-entry FileDropList clipboard preparation. |
| `Views/PopupWindow.PasteDispatch.cs` | Thin placeholder for paste dispatch grouping; behavior lives in the focused paste-dispatch partials below. |
| `Views/PopupWindow.PasteDispatch.Keys.cs` | Ctrl+V, Shift+Insert, Shift+Enter, modifier release, and Unicode SendInput helpers. |
| `Views/PopupWindow.PasteDispatch.DirectText.cs` | Non-clipboard text insertion through direct Unicode typing and Win32 edit-control replacement. |
| `Views/PopupWindow.PasteDispatch.UiAutomation.cs` | UI Automation ValuePattern/TextPattern text replacement fallback and offset mapping. |
| `Views/PopupWindow.PasteFileExport.cs` | Thin placeholder for temp-file export paste grouping. |
| `Views/PopupWindow.PasteFileExport.Complete.cs` | Shared temp-file FileDropList clipboard write and paste dispatch. |
| `Views/PopupWindow.PasteFileExport.Image.cs` | Image history entry export to temporary PNG for Explorer paste. |
| `Views/PopupWindow.PasteFileExport.Json.cs` | JSON validation and text export to temporary `.json` for Explorer paste. |
| `Views/PopupWindow.BatchPaste.cs` | Thin placeholder for batch mode grouping; behavior lives in the focused batch-mode partials below. |
| `Views/PopupWindow.BatchPasteMode.cs` | Batch mode get/set/cycle state transitions and mode-change event dispatch. |
| `Views/PopupWindow.BatchPasteChrome.cs` | Batch mode header chrome, selection brushes, and mode-color helpers. |
| `Views/PopupWindow.BatchPasteOrdering.cs` | Queue order property updates and queued-item list reordering. |
| `Views/PopupWindow.BatchPasteHooks.cs` | Batch-related keyboard hook synchronization and Alt chord cleanup expiry. |
| `Views/PopupWindow.BatchPasteQueue.cs` | Thin placeholder for FIFO/LIFO queue UI integration; behavior lives in the focused queue partials below. |
| `Views/PopupWindow.BatchPasteQueue.Enqueue.cs` | Queue enqueue entry points for selected items and clipboard-monitor captures. |
| `Views/PopupWindow.BatchPasteQueue.Advance.cs` | Queue advance after target paste and auto-switch-to-normal arming. |
| `Views/PopupWindow.BatchPasteQueue.HeadClipboard.cs` | Queue-head clipboard synchronization for text, image, and file entries. |
| `Views/PopupWindow.BatchPasteQueue.Resync.cs` | Queue-head resync scheduling after dedup or head changes. |
| `Views/PopupWindow.BatchPasteQueue.Stubs.cs` | Non-clipboard build stubs for queue operations. |
| `Views/PopupWindow.BatchPasteMenu.cs` | Batch menu popup navigation and "paste all" entry point. |
| `Views/PopupWindow.BatchPasteSelection.cs` | Multi-selection paste dispatch and adjacent segment grouping. |
| `Views/PopupWindow.BatchPasteExecution.cs` | Thin placeholder for batch paste execution grouping; behavior lives in the focused execution partials below. |
| `Views/PopupWindow.BatchPasteExecution.Ordered.cs` | Ordered mixed-entry batch paste execution and adjacent-run dispatch. |
| `Views/PopupWindow.BatchPasteExecution.Text.cs` | One-shot all-text batch paste through a single clipboard text write. |
| `Views/PopupWindow.BatchPasteExecution.FileDrop.cs` | One-shot image/file batch paste through a combined FileDropList. |
| `Views/PopupWindow.BatchPasteSequential.cs` | Sequential paste timing, target clipboard-consume wait, and deferred history reorder. |
| `Views/PopupWindow.BatchPasteKeyboardMenu.cs` | Keyboard entry into batch/context menu selection. |
| `Views/PopupWindow.HotkeysAndFileJump.cs` | Global hook installation, foreground watcher, foreground event coalescing, and top-level file-jump coordination. |
| `Views/PopupWindow.FileJumpHotkey.cs` | Manual file-dialog jump hotkey flow and candidate collection. |
| `Views/PopupWindow.FileJumpExternalRefresh.cs` | Refreshing an open file-jump picker from external file-manager foreground changes. |
| `Views/PopupWindow.FileJumpAutoOpen.cs` | Auto-open/auto-navigate behavior when a file dialog becomes foreground. |
| `Views/PopupWindow.FileJumpAutoSync.cs` | Auto-syncing dialog paths when returning from an external file manager. |
| `Views/PopupWindow.FileJumpPicker.cs` | File-jump picker scheduling, open/close state, double-tap state, and click-to-navigate arming. |

## Placement Rules

- Do not add feature logic back into `PopupWindow.xaml.cs` unless it is shared state or initialization.
- Prefer an existing partial file over creating a new one. Create a new partial only when the responsibility is distinct and likely to grow.
- If code does not need WPF controls, dispatcher state, or `PopupWindow` fields, prefer moving it into a service/model instead of another partial.
- Keep hook callbacks and message handlers thin. They should validate state and dispatch to focused helpers.
- Keep file-jump strategies in `FileJump/` or `Services/` when they can be expressed without direct popup UI access.
- Keep clipboard storage/search behavior in `Services/ClipboardHistoryStore.cs`, `Search/`, or model types rather than in popup UI files.
- Keep clipboard write retry and owner diagnostics in `Services/ClipboardWriteRetry.cs`.
- Keep FIFO/LIFO queue state transitions in `Services/BatchPasteQueueController.cs`.
- Keep reusable popup DPI coordinate mapping in `Services/PopupDpiMapper.cs`.
- Keep keyboard/mouse multi-selection cursor transitions in `Services/SelectionCursorController.cs`.

## Refactoring Direction

The partial split is a staging step, not the final architecture. Good candidates for later extraction are:

- file-jump auto-open/auto-sync scheduling into a coordinator;
- popup placement and remaining DPI correction into focused helpers where they can be kept UI-independent.
