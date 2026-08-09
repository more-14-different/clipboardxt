# ClipboardXt

An opinionated ClipboardX, maintained as a hard fork of [chaojimct/clipboardx](https://github.com/chaojimct/clipboardx).

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

Compared with the current [upstream](https://github.com/chaojimct/clipboardx), ClipboardXt is not a small patch set. Its main departures are:

- **Architecture** — upstream's multi-thousand-line window and controller classes have been dismantled into task-scoped partial files and small services. ClipboardXt also adds a dedicated test project with about 40 test classes, and extends the SQLite model with hot/cold archive buckets, FTS indexes, lazy hydration, and persisted source metadata.
- **Functionality** — complete Chinese/English UI; Traditional Pinyin or Xiaohe Shuangpin search; cold-archive and source-app/title search; remembered query, caret, and filter state; configurable shortcuts for individual item actions; and a standalone folder panel that can paste or deliver paths, not only navigate dialogs.
- **Interaction and data model** — the three panels now share a richer keyboard-first interaction model: editable search with caret, selection, mouse positioning, and undo; layered `Esc`; contextual shortcut guides; and source/file icons. Settings and history schemas gained new fields, tables, and indexes, while recent-folder behavior changed from threshold-based collection to a larger first-use MRU list.

---

<a id="简体中文"></a>

## 简体中文

ClipboardXt 是一个有明确取舍的 ClipboardX，也是 [chaojimct/clipboardx](https://github.com/chaojimct/clipboardx) 的 hard fork。

ClipboardXt 围绕三个键盘优先的面板构建；三个面板均支持中英文界面、可配置快捷键与尽量不抢焦点的交互。

### 剪贴板面板

在不打断当前应用输入焦点的前提下，检索文本、图片和文件历史。可直接预览、编辑、收藏、OCR 或粘贴条目，并通过多选与 FIFO/LIFO 队列完成连续和批量粘贴。

### 文件夹跳转面板

从资源管理器、最近位置和收藏中收集目录，一键让「打开 / 保存」对话框跳到目标文件夹。也可独立呼出面板来查找目录、在资源管理器中打开，或把所选路径发送给目标应用。

### 资源管理器 Quick Find 面板

在资源管理器中直接键入，通过 [Everything](https://www.voidtools.com/) 检索当前文件夹及其子目录。结果随输入即时过滤，确认后直接在资源管理器中定位所选文件或文件夹。

### 与 upstream 的激进分歧

相较于当前 [upstream](https://github.com/chaojimct/clipboardx)，ClipboardXt 已经不是一组小补丁，主要分歧包括：

- **架构** — 将 upstream 中数千行的窗口与控制器大类拆成按职责组织的 partial 文件和小型服务；新增约 40 个测试类的独立测试工程，并把 SQLite 模型扩展为冷热归档桶、FTS 索引、按需回填及持久化来源元数据。
- **功能** — 增加完整中英文 UI、传统拼音 / 小鹤双拼切换、冷归档与来源应用/窗口标题检索、查询与光标及筛选状态记忆、每项条目动作的可配置快捷键；独立文件夹面板除打开目录和跳转对话框外，还能粘贴或投递路径。
- **交互与数据形式** — 三个面板采用更完整的键盘优先交互：搜索框支持光标、选区、鼠标定位和撤销，`Esc` 按层级退出，并提供随上下文变化的快捷键指南和来源/文件图标。设置与历史库增加了新的字段、表和索引；最近目录也从达到次数阈值后收集，改为首次明确使用即进入更大的 MRU 列表。

---

Source and releases: [more-14-different/clipboardxt](https://github.com/more-14-different/clipboardxt) · License: [MIT](LICENSE)
