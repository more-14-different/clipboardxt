# Paste Latency Architecture and Optimization Baseline

> Status: living baseline
>
> Last validated: 2026-07-25, commit `19dac7c`
>
> Scope: `Ctrl+Alt+V` clipboard popup and `Ctrl+G` file-dialog folder picker

本文档记录 ClipboardX 粘贴面板的延迟认识、已确认瓶颈、架构约束和后续优化顺序。它是可随程序迭代更新的工作基线，不是一次性的故障报告。

本文中的耗时数据来自一次本机日志快照，只用于判断数量级和定位长尾；程序流程、日志格式或测试环境变化后，应重新测量，不应把这些数字当成永久性能指标。

## Architecture Decision

### 保留独立剪贴板 provider 进程

文本粘贴每次启动一个独立 ClipboardX 子进程作为剪贴板 provider，是参照 CopyQ 采用的设计模式，也是当前应保留的架构约束。

这个进程边界承担的不只是“把文本写进剪贴板”：

- 将剪贴板所有权和生命周期从 popup 主进程隔离出来。
- 在目标程序延迟读取剪贴板时，允许 provider 短暂持有数据。
- 降低 WPF/OLE 剪贴板竞争、退出和回退行为对主进程稳定性的影响。
- 让标准 `Ctrl+V` / `Shift+Insert` 仍是主要投递方式，保留目标应用兼容性。

因此，后续优化不得仅为了减少冷启动耗时，就默认改回由主进程直接写剪贴板。主进程写入仍可作为失败回退，但不应在缺少新的稳定性证据和明确架构决策时变成默认快路径。

允许并鼓励在这一模式内部改进：

- 让 provider 命令模式更早进入，跳过与 provider 无关的 WPF 初始化。
- 缩小子进程启动面，减少程序集、配置、日志和路径初始化成本。
- 在保持独立进程和剪贴板持有语义的前提下，改进临时文件轮询和结果通知。
- 评估专用轻量 helper、预启动或常驻 provider，但必须同时评估发布、升级、内存、生命周期和安全面的代价。
- 补充阶段化 telemetry，用数据区分子进程启动、剪贴板写入、主进程观察和目标程序消费。

“更早进入 provider 命令模式”最接近低代价优化；新增 helper 可执行文件和常驻 provider 都有明确 trade-off，不能归为无代价改进。

## Current Flow

### `Ctrl+Alt+V` 普通文本粘贴

1. Popup 创建 `AltVTextPasteSession`。
2. Session 启动当前 ClipboardX 可执行文件的 provider 命令模式。
3. Provider 写入文本并短暂持有剪贴板。
4. 主进程向原目标窗口发送标准粘贴快捷键。
5. 短暂 settle 后，主进程通知 provider 退出并回收 session。

主流程会设置 `_pasteInProgress` / `_isSettingClipboard`，并记录自身写入序列，以避免 ClipboardX 把自己的 provider 写入误认为外部剪贴板更新。

### `Ctrl+G` 文件夹选择面板

选中文件夹后，picker 将路径作为文本，通过同一类 provider session 写入剪贴板并向文件对话框投递粘贴快捷键。

当前这条 standalone 路径与普通文本粘贴的生命周期没有完全对齐：自身写入抑制和 provider session 回收存在缺口。这是实现问题，不是独立 provider 架构本身的问题。

## Confirmed Bottlenecks and Issues

### Implementation Progress

Phase 1 已于 2026-07-25 实现，自动化验证已通过，仍需用实际文件对话框和粘贴目标完成运行时日志验收：

- `Ctrl+G` standalone 在 provider 启动前建立 self-write 抑制，成功后记录 clipboard sequence，并在所有退出路径 dispose session。
- 文件对话框类名识别改为单次 `EnumChildWindows`；分类仍保持 `GeneralDirectUi`、`SysListView`、`ShellDefView` 的原优先级。
- 前台窗口处理不再对同一 HWND 连续执行第二次 resolver。
- 新增 standalone provider 成功/异常回收测试和窗口类识别优先级测试。

独立 ClipboardX provider 子进程模式保持不变。Phase 1 的运行时验收重点是确认日志中不再出现 8 秒 provider hold，以及 `fg_ui` 的秒级窗口树扫描长尾消失。

### P0: 前台窗口识别阻塞 WPF UI 线程

`PopupWindow.ForegroundWatcher.ProcessForegroundChangedUi` 在 Dispatcher 上执行文件对话框识别。Phase 1 之前的识别会：

- 在一次处理内可能重复调用 `ResolveFileDialogHwndFromWindowOrAncestor`。
- 通过 `EnumChildWindows` 枚举后，又对每个子窗口递归调用枚举。

Win32 的 [`EnumChildWindows`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumchildwindows) 本身会枚举后代窗口，额外递归会重复扫描子树。由于整个识别发生在 WPF UI 线程上，扫描期间 Enter、Ctrl+Enter 和 popup 响应都会排队；这与“按键后等一会儿才有反应”的长尾表现吻合。

诊断快照中的 `fg_ui` 记录具有阈值偏差，不能视为所有事件的分布，但足以证明存在明显长尾：

| Metric | Observed |
|---|---:|
| Recorded samples | 458 |
| P50 | 65 ms |
| P90 | 553 ms |
| P99 | 2252 ms |
| Maximum | 13326 ms |
| Samples >= 1000 ms | 27 |

Phase 1 实现：

1. 对窗口树只做一次 `EnumChildWindows`，命中后让 callback 返回 `FALSE` 提前停止。
2. 消除同一事件内的重复 resolver 调用。
3. 为同一 HWND 的分类结果增加可失效缓存，或把完整分类移出 UI 线程。
4. 保留结果一致性日志，以验证优化前后识别结果相同。

前两项已经完成，可在不改变识别语义的前提下减少重复工作，属于最明确的低风险、近乎无 trade-off 优化。缓存和移出 UI 线程尚未实施，需要额外处理 HWND 失效、竞态和结果时序。

### P0: `Ctrl+G` standalone 粘贴缺少完整生命周期

Phase 1 之前，`FileJumpStandalonePasteAsync` 没有像普通粘贴路径一样，在 provider 写入前完整建立自身写入抑制；同时没有在 `finally` 中可靠 dispose provider session。

可观察后果：

- Provider 写入可能被 clipboard monitor 当作新的外部文本，触发历史去重、SQLite 写入和 popup 刷新。
- 日志中会出现 ClipboardX 到 ClipboardX 的 duplicate preserve。
- Provider 可能一直存活到约 8 秒 hold timeout，而普通路径通常会在 settle 后主动退出。

Phase 1 已按以下原则修复：

1. 在 provider 可能写入之前设置 `_pasteInProgress` / `_isSettingClipboard`。
2. 写入成功后正确记录 self-write sequence。
3. 无论成功、失败还是取消，都在 `finally` 中 settle/dispose session。
4. 尽量把普通文本粘贴和 standalone 路径收敛到共享的生命周期协调器，避免再次漂移。

这是资源和状态管理缺口。当前修复不改变用户可见语义，也不放弃独立 provider 模式，属于无明显 trade-off 的优先改进；仍需通过真实 clipboard monitor 日志确认回波被正确跳过。

### P1: Provider 子进程冷启动

诊断快照中 provider 写入均成功，没有出现 provider result timeout。普通文本路径从主进程开始到 provider `SetDataObject` 的样本约为：

| Metric | Observed |
|---|---:|
| Samples | 40 |
| Median | 182 ms |
| P90 | 369 ms |
| Maximum | 1858 ms |

这说明冷启动通常是数百毫秒，但存在秒级长尾。它是真实成本，应优化，但不能仅凭这一数据移除进程边界。

推荐顺序：

1. 将 provider 特殊命令模式的分派尽量提前，测量并跳过无关初始化。
2. 为 `process start -> provider entry -> clipboard set -> result observed` 分阶段计时。
3. 在保持进程边界的前提下，评估用事件或 pipe 替代临时文件加固定间隔轮询。
4. 只有在数据证明前述措施不够时，再评估轻量 helper 或 warm provider。

Provider settle 通常发生在粘贴快捷键已经发出之后，主要影响方法完成和 provider 生命周期，并不等同于用户看到内容前的全部等待时间。诊断时必须把它和真正的 dispatch 延迟分开。

### P1: 面板关闭时仍触发无用搜索刷新

普通 popup 在消费选择前清空查询；文件夹 picker 也会在提交和关闭附近清空查询。清空操作可能调度新的 filter/数据库刷新，但面板随后马上隐藏，这些工作通常不会给当前交互带来可见价值，反而可能与粘贴协调争用 UI 或数据库资源。

优化目标是在保留 `RememberPanelOperationState` 语义的同时：

- 成功消费后清空查询模型。
- 不在即将隐藏的窗口上执行一轮无用 filter。
- 下次显示时按最新状态刷新。
- 无效操作或未成功消费时，不错误清空用户查询。

可通过取消 close-time refresh、标记结果 stale 并在下次 show 时刷新来实现。需要对查询状态语义做回归验证。

### P1: 文件夹 MRU 记录发生在隐藏窗口之前

文件夹 picker 当前先执行 `RecordRecentFolderUse(path)`，再隐藏窗口。如果路径位于缓慢或离线的网络位置，`Directory.Exists` 或设置持久化可能阻塞可见关闭。

低风险方向是先完成 UI 隐藏和粘贴启动，再延后记录 MRU，同时保证最终一致性。实施前需确认设置对象的线程要求和失败处理。

### P2: 文件剪贴板的 WPF/OLE fallback

文件粘贴日志曾出现 native 路径失败后，两次 WPF fallback 各阻塞约一秒，总计约 2.1 秒。直接移除 fallback 会损失兼容性，不是无 trade-off 的优化。

可以在独立 STA 上降低 UI 卡顿，但目标应用何时收到可用文件剪贴板仍取决于 fallback 完成。该问题应与文本和文件夹粘贴分别测量、分别治理。

### Expected Safeguards

`WaitForModifiersReleased(500)` 最多可增加约 500 ms，但它用于避免修饰键未释放时产生错误快捷键或裸 `V`。缩短或移除会引入稳定性 trade-off，不应把它当作首要“免费”优化。

## Optimization Order

在不改变独立 provider 架构的前提下，建议按以下顺序执行：

1. 修复 `Ctrl+G` standalone 的 self-write 抑制和 session dispose。
2. 将文件对话框窗口树识别改成单次枚举，并消除重复 resolver。
3. 取消面板关闭时不会被消费的搜索刷新。
4. 把 MRU 等非关键工作移到可见交互完成之后。
5. 提前 provider 命令模式入口，补齐冷启动分段计时。
6. 依据新数据决定是否值得改 IPC、拆轻量 helper 或采用 warm provider。
7. 单独处理文件剪贴板 fallback，不与文本 provider 优化混为一谈。

前四项主要消除重复工作、生命周期缺口和错误时序，最接近无用户功能 trade-off。第 5 项需注意启动日志和错误报告初始化顺序。第 6、7 项都有实现或兼容性代价，必须以测量和回归结果决定。

## Validation and Acceptance

每次相关优化至少验证：

- `Ctrl+Alt+V` 和 `Ctrl+G` 的 Enter / Ctrl+Enter 均能投递正确文本。
- `Ctrl+G` 成功、失败、取消后都没有 provider 留到 8 秒 timeout。
- Provider 自身写入被 monitor 跳过，不创建、重排或重复持久化历史项。
- 文件对话框识别优化前后分类结果一致，且不再由重复遍历产生秒级 `fg_ui` 长尾。
- 成功粘贴后查询状态符合 `RememberPanelOperationState` 约定，下次打开结果正确。
- 浏览器、终端、普通编辑器和系统文件对话框的 `Ctrl+V` / `Shift+Insert` 策略保持有效。
- 修饰键未释放、剪贴板暂时被占用、目标程序延迟读取时仍无裸 `V`、错误字符或 provider 提前退出。
- 图片和文件粘贴路径没有被文本优化意外改变。

建议增加的自动化或可重复测试：

- 抽取 paste coordinator，验证 session 在成功、异常和取消路径都被 dispose。
- 验证 self-write clipboard update 不会进入 history insert/reorder。
- 验证窗口隐藏时清空查询不会立刻运行无用 filter，下次 show 会刷新。
- 为窗口分类抽象枚举输入，比较单次遍历实现与旧实现的分类结果。
- 保留一套基于阶段日志的手工 smoke test，用同一批目标程序比较 P50/P90/P99。

## Evidence and Code Map

主要实现位置：

- [`App.xaml.cs`](../App.xaml.cs): 应用启动和 provider 命令模式入口。
- [`Services/AltVClipboardProvider.cs`](../Services/AltVClipboardProvider.cs)
- [`Services/AltVClipboardProvider.Session.cs`](../Services/AltVClipboardProvider.Session.cs)
- [`Services/AltVTextPasteSession.cs`](../Services/AltVTextPasteSession.cs)
- [`Views/PopupWindow.PasteClipboard.cs`](../Views/PopupWindow.PasteClipboard.cs)
- [`Views/PopupWindow.PasteEntry.cs`](../Views/PopupWindow.PasteEntry.cs)
- [`Views/PopupWindow.PasteEntry.Text.cs`](../Views/PopupWindow.PasteEntry.Text.cs)
- [`Views/PopupWindow.ForegroundWatcher.cs`](../Views/PopupWindow.ForegroundWatcher.cs)
- [`Views/PopupWindow.ClipboardMonitoring.Update.cs`](../Views/PopupWindow.ClipboardMonitoring.Update.cs)
- [`Views/FileDialogJumpPickerWindow.Keyboard.cs`](../Views/FileDialogJumpPickerWindow.Keyboard.cs)
- [`Views/FileDialogJumpPickerWindow.RefreshNavigation.cs`](../Views/FileDialogJumpPickerWindow.RefreshNavigation.cs)
- [`FileJump/FileDialogJumpHelper.Detection.cs`](../FileJump/FileDialogJumpHelper.Detection.cs)

本次诊断使用：

- `%LocalAppData%\ClipboardX\clipboard_diagnostics.log`
- `%LocalAppData%\ClipboardX\shell_navigate.log`
- 当前代码和相关 git history

日志文件会滚动、样本也会随版本变化；更新本页的性能数字时，应同时记录日期、commit、样本数和采样条件。

## Updating This Baseline

发生下列变化时应更新本文档：

- 文本 provider 的进程模型、IPC 或持有生命周期变化。
- 普通 popup 或 file-jump standalone paste 流程收敛或拆分。
- 文件对话框检测线程、缓存或枚举策略变化。
- 搜索状态和 `RememberPanelOperationState` 语义变化。
- 新日志证明瓶颈排序或耗时数量级已经变化。
- 回归测试覆盖了本文列出的风险，或发现新的目标应用兼容性约束。

更新时优先替换已经过时的结论和数字，不要只在末尾累加互相冲突的历史说明。重要架构方向如需改变，应另写明确决策记录，并在本页链接原因、替代方案和迁移条件。

## Revision Notes

| Date | Revision |
|---|---|
| 2026-07-25 | Phase 1：修复 standalone self-write/session 生命周期；窗口树识别改为单次枚举并消除同事件重复 resolver；增加针对性测试，等待运行时日志验收。 |
| 2026-07-25 | 建立初始基线；确认保留 CopyQ 式独立 provider 进程，记录 UI 窗口识别、standalone 生命周期、冷启动、关闭时刷新和文件 fallback 等认识。 |
