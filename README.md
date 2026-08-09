# ClipboardXt

An opinionated ClipboardX. This is a personal project that originated from [chaojimct/clipboardx](https://github.com/chaojimct/clipboardx) and now evolves independently.

**[English](#english)** · **[简体中文](#简体中文)** · [Releases](https://github.com/more-14-different/clipboardxt/releases)

<a id="english"></a>

## English

ClipboardXt is built around three keyboard-first panels. All three support Chinese and English UI, configurable shortcuts, and focus-preserving interaction.

### Clipboard Panel

Open a searchable history of text, images, and files without taking focus from the app you are using. Preview, edit, favorite, OCR, or paste an item directly; multi-selection and FIFO/LIFO queues cover repeated and batch-paste workflows.

### Folder Jump Panel

Jump an Open/Save dialog to a folder collected from File Explorer, recent locations, or favorites. The same panel can also work independently to find folders, open them in File Explorer, or deliver a selected path to the target app.

### File Explorer Quick Find

Start typing in File Explorer to search the current folder and its subfolders through [Everything](https://www.voidtools.com/). The panel filters results as you type and locates the selected file or folder directly in Explorer.

### Radical departures from upstream

ClipboardXt deliberately does not treat compatibility with [upstream](https://github.com/chaojimct/clipboardx) as a design constraint. These are broad, sometimes breaking changes rather than a small patch set:

- **Architecture** — large WPF classes were split into task-focused partial modules and services; history/search now extends deeply into SQLite FTS, cold archives, lazy media loading, native clipboard paths, and a much broader automated test surface.
- **Features** — the original utility grew into three coordinated panels, with bilingual UI, OCR, FIFO/LIFO queues, editable and favoritable history, standalone folder workflows, and Everything-backed search.
- **Product form** — interaction defaults, shortcuts, panel state, themes, packaging flavors, installers, and update/release flow have all been reshaped around this project's keyboard-first preferences. ClipboardXt has its own name, repository, and releases; source branches and release artifacts should not be treated as interchangeable with upstream.

---

<a id="简体中文"></a>

## 简体中文

ClipboardXt 是一个有明确取舍的 ClipboardX。它源自 [chaojimct/clipboardx](https://github.com/chaojimct/clipboardx)，现已作为个人项目独立演进。

ClipboardXt 围绕三个键盘优先的面板构建；三个面板均支持中英文界面、可配置快捷键与尽量不抢焦点的交互。

### 剪贴板面板

在不打断当前应用输入焦点的前提下，检索文本、图片和文件历史。可直接预览、编辑、收藏、OCR 或粘贴条目，并通过多选与 FIFO/LIFO 队列完成连续和批量粘贴。

### 文件夹跳转面板

从资源管理器、最近位置和收藏中收集目录，一键让「打开 / 保存」对话框跳到目标文件夹。也可独立呼出面板来查找目录、在资源管理器中打开，或把所选路径发送给目标应用。

### 资源管理器 Quick Find 面板

在资源管理器中直接键入，通过 [Everything](https://www.voidtools.com/) 检索当前文件夹及其子目录。结果随输入即时过滤，确认后直接在资源管理器中定位所选文件或文件夹。

### 与 upstream 的激进分歧

ClipboardXt 有意不把与 [upstream](https://github.com/chaojimct/clipboardx) 保持兼容作为设计约束。以下改动并非一组便于回移的小补丁，而是范围较广、部分具有破坏性的重塑：

- **架构** — 将大型 WPF 类拆分为按职责组织的 partial 模块与服务；历史和搜索深入扩展到 SQLite FTS、冷归档、媒体懒加载、原生剪贴板路径，并建立了更广的自动化测试面。
- **功能** — 从原有工具扩展为三个协同面板，并加入完整双语 UI、OCR、FIFO/LIFO 队列、历史编辑与收藏、独立文件夹工作流以及 Everything 检索。
- **形式** — 围绕本项目偏好的键盘优先体验，重新塑造默认交互、快捷键、面板状态、主题、多 Flavor 打包、安装与更新发布流程。ClipboardXt 使用独立名称、仓库和发行版，不应将其源码分支或发行包视为可与 upstream 直接互换。

---

Source and releases: [more-14-different/clipboardxt](https://github.com/more-14-different/clipboardxt) · License: [MIT](LICENSE)
