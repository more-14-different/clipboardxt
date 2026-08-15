namespace ClipboardManager;

internal static partial class EnglishTranslations
{
    private static readonly (string Chinese, string English)[] AdditionalFragments =
    [
        ("版本 ", "Version "),
        (" 已发布，托盘右键「检查更新…」可下载安装。（当前 ", " is available. Right-click the tray icon and choose Check for Updates… to download and install it. (Current: "),
        ("当前已是最新版本（", "You already have the latest version ("),
        ("发现新版本 ", "A new version "),
        ("当前 ", "current: "),
        ("说明：", "Release notes: "),
        ("将下载：", "Download: "),
        ("大小约 ", "Approximate size: "),
        ("安装目录：", "Install directory:"),
        ("压缩包内未找到 ", "The archive does not contain "),
        ("，已中止。", "; the operation was canceled."),
        ("更新未成功：", "The update did not complete:"),
        ("识别图片中的文字需要 Windows 的「", "Recognizing text in images requires the Windows “"),
        ("」OCR 组件（约几十 MB，从 Windows 更新下载）。", "” OCR component (tens of MB, downloaded through Windows Update)."),
        ("热键 ", "Hotkey "),
        ("文件对话框跳转快捷键 ", "File-dialog jump shortcut "),
        ("批量模式切换快捷键 ", "Batch-mode shortcut "),
        ("快捷键 ", "Shortcut "),
        (" 条结果", " results"),
        (" 个文件", " files"),
        (" 张图片", " images"),
        (" 图片", " image"),
        (" 条有效规则。", " valid rules."),
        (" 条规则。", " rules."),
        ("已合并写入 ", "Merged and saved "),
        ("已替换为 ", "Replaced with "),
        ("作者：", "Author: "),
        ("邮箱：", "Email: "),
        ("版本：", "Version: "),
        ("删除此规则？", "Delete this rule?"),
        ("已保存。已锁定优先策略：", "Saved. Preferred strategy: "),
        ("卸载 ClipboardX（版本 ", "Uninstall ClipboardX (version "),
        ("卸载 ClipboardX — ", "Uninstall ClipboardX — "),
        ("无法启动清理任务。请手动删除文件夹：", "Unable to start the cleanup task. Delete this folder manually:"),
        ("（已复制到剪贴板）", "(copied to clipboard)"),
        ("存储文件：", "Storage file: "),
        ("共 ", "Total: "),
        (" 项", " items"),
        ("=== ClipboardX 窗口信息采集 ===", "=== ClipboardX Window Information ==="),
        ("句柄:     ", "Handle:    "),
        ("类名:     ", "Class:     "),
        ("标题:     ", "Title:     "),
        ("进程名:   ", "Process:   "),
        ("进程PID:  ", "Process ID:"),
        ("进程路径: ", "Process path: "),
        ("UIA名称: ", "UIA name: "),
        ("对话框识别: ", "Dialog classification: "),
        ("按序尝试", "Try in order"),
        ("自定义跳转: 已保存（优先：", "Custom navigation: saved (preferred: "),
        ("自定义跳转: 未保存（设置 → 自定义文件对话框，或托盘向导）", "Custom navigation: not saved (Settings → Custom File Dialogs, or the tray wizard)"),
        ("子窗口 (", "Child windows ("),
        (" 个):", "):"),
        ("子窗口: (无)", "Child windows: (none)"),
        ("UIA 子节点:", "UIA child nodes:"),
        ("  (无)", "  (none)"),
        ("添加自定义文件对话框…", "Add Custom File Dialog…"),
        ("将卸载 ClipboardX（版本 ", "This will uninstall ClipboardX (version "),
        ("）、移除开始菜单快捷方式、开机启动与「应用和功能」条目，并删除安装目录中的程序文件。", "), remove its Start-menu shortcut, startup entry, and Apps & Features entry, and delete program files from the install directory."),
        ("是否同时删除配置与历史记录？（%AppData%\\ClipboardX，旧版可能在 ClipboardManager）", "Also delete settings and history? (%AppData%\\ClipboardX; older versions may use ClipboardManager.)"),
        ("「是」删除程序与配置；「否」只删程序；「取消」中止。", "Yes deletes the app and data; No deletes only the app; Cancel stops.")
    ];

    private static string TranslateAdditional(string value, bool normalizePunctuation)
    {
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^按住面板主键 (.+)，直接粘贴列表第 1～9 条。$",
            "Hold the panel modifier $1 to paste list item 1–9 directly.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^先看核心操作；其余按任务分组。当前面板主键：(.+)。$",
            "Core actions come first; the rest are grouped by task. Current panel modifier: $1.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^(.+)条目动作“(.+)”与“(.+)”不能使用相同快捷键。$",
            "The $1 item actions “$2” and “$3” cannot use the same shortcut.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^热键 (.+) 注册失败，可能被其他程序占用$",
            "Hotkey $1 could not be registered; it may be in use by another application.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^热键 (.+) 注册失败，已恢复原快捷键$",
            "Hotkey $1 could not be registered; the previous shortcut was restored.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^快捷键 (.+)（文件对话框跳转）注册失败，可能与其他软件冲突$",
            "File-dialog jump shortcut $1 could not be registered; it may conflict with another application.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^文件对话框跳转快捷键 (.+) 注册失败，已恢复原快捷键$",
            "File-dialog jump shortcut $1 could not be registered; the previous shortcut was restored.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^批量模式切换快捷键 (.+) 注册失败，可能与其他软件冲突$",
            "Batch-mode shortcut $1 could not be registered; it may conflict with another application.");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^批量模式切换快捷键 (.+) 注册失败，已恢复原快捷键$",
            "Batch-mode shortcut $1 could not be registered; the previous shortcut was restored.");
        value = System.Text.RegularExpressions.Regex.Replace(value, "^(\\d+) 条结果$", "$1 results");
        value = System.Text.RegularExpressions.Regex.Replace(value, "^(\\d+) 个文件$", "$1 files");
        value = System.Text.RegularExpressions.Regex.Replace(value, "^(\\d+)×(\\d+) 图片$", "$1×$2 image");
        value = System.Text.RegularExpressions.Regex.Replace(value, "^(\\d+) 项$", "$1 items");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^共 (\\d+) 个文件 · (\\d+) 张图片$", "$1 files · $2 images");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^版本 (.+) 已发布，托盘右键「检查更新…」可下载安装。（当前 (.+)）$",
            "Version $1 is available. Right-click the tray icon and choose Check for Updates… to download and install it. (Current: $2)");
        value = System.Text.RegularExpressions.Regex.Replace(value,
            "^识别图片中的文字需要 Windows 的「(.+)」OCR 组件（约几十 MB，从 Windows 更新下载）。",
            "Recognizing text in images requires the Windows “$1” OCR component (tens of MB, downloaded through Windows Update).");
        foreach (var (chinese, english) in AdditionalFragments)
            value = value.Replace(chinese, english, StringComparison.Ordinal);
        if (!normalizePunctuation) return value;
        return value
            .Replace('，', ',')
            .Replace('。', '.')
            .Replace('（', '(')
            .Replace('）', ')')
            .Replace('：', ':')
            .Replace('；', ';')
            .Replace('「', '“')
            .Replace('」', '”')
            .Replace('～', '–');
    }

    private static readonly Dictionary<string, string> AdditionalExact = new(StringComparer.Ordinal)
    {
        ["界面语言"] = "Interface Language",
        ["剪贴板面板"] = "Clipboard Panel",
        ["显示语言"] = "Display Language",
        ["简体中文"] = "Simplified Chinese",
        ["英文"] = "English",
        ["一键切换简体中文与 English；保存后下次启动继续使用所选语言。"] = "Switch between Simplified Chinese and English with one click. Save to use the selected language next time.",
        ["（空文本）"] = "(empty text)",
        ["（无法解码该图片）"] = "(unable to decode this image)",
        ["（无路径）"] = "(no path)",
        ["  · 全盘"] = "  · all drives",
        ["↑↓ 选择 · ←→ 翻页 · Ctrl+N 快选 · Enter 定位 · Esc 关闭"] = "↑↓ Select · ←→ Page · Ctrl+N Quick select · Enter Locate · Esc Close",
        ["★ 收藏"] = "★ Favorite",
        ["☆ 取消收藏"] = "☆ Unfavorite",
        ["⚡ 修改快捷短语"] = "⚡ Edit Quick Phrase",
        ["按层级关闭菜单或预览、撤销删除线、清空搜索，最后关闭面板。"] = "Close menus or previews, undo pending deletion, clear search, and finally close the panel, one level at a time.",
        ["按层级关闭详情、清空搜索，最后关闭面板。"] = "Close details, clear search, and finally close the panel, one level at a time.",
        ["按字符或词段移动搜索光标；配合 Shift 可扩展选区。"] = "Move the search caret by character or word; hold Shift to extend the selection.",
        ["把所选文件夹的完整路径粘贴回打开面板前的原窗口。"] = "Paste the selected folder's full path back into the window that was active before the panel opened.",
        ["操作菜单"] = "Actions Menu",
        ["从常用路径中移除当前文件夹；不会删除磁盘内容。"] = "Remove the current folder from recent paths. Nothing is deleted from disk.",
        ["打开"] = "Open",
        ["打开收藏、关键词和常用路径管理菜单。"] = "Open the menu for managing favorites, keywords, and recent paths.",
        ["打开文件夹，或把完整路径送回原窗口。"] = "Open the folder or send its full path back to the original window.",
        ["导出自定义文件对话框规则"] = "Export Custom File Dialog Rules",
        ["导入规则（完全替换）"] = "Import Rules (Replace All)",
        ["导入规则（与本地合并）"] = "Import Rules (Merge with Local)",
        ["点选"] = "Toggle Selection",
        ["点选或取消当前条目；之后移动焦点仍保留已选项，最后按粘贴键执行。"] = "Select or deselect the current item. Moving focus preserves selected items; press the paste key when ready.",
        ["短语"] = "Phrases",
        ["翻页"] = "Page",
        ["返回 / 关闭"] = "Back / Close",
        ["复制路径"] = "Copy Path",
        ["复制所选文件夹的完整路径，不改变文件对话框目录。"] = "Copy the selected folder's full path without changing the file dialog directory.",
        ["共 {entry.FilePaths!.Length} 个文件 · {_previewImageFiles.Length} 张图片"] = "{entry.FilePaths!.Length} files · {_previewImageFiles.Length} images",
        ["关键词"] = "Keyword",
        ["管理菜单"] = "Management Menu",
        ["过滤"] = "Filter",
        ["换行连贴"] = "Paste with Newlines",
        ["打开网址"] = "Open Web URLs",
        ["打开当前文本中的合法 http/https 网址；来源是浏览器时优先用来源浏览器，否则使用默认浏览器。"] = "Open valid http/https URLs from the current text. Prefer the source browser when available; otherwise use the default browser.",
        ["仅看短语"] = "Phrases Only",
        ["可试拼音全拼或首字母，如「nihao」「nh」"] = "Try full Pinyin or initials, such as “nihao” or “nh”.",
        ["可试小鹤双拼全码或声母串，如「nihc」「nh」"] = "Try full Xiaohe Shuangpin codes or initial strings, such as “nihc” or “nh”.",
        ["控制连贴方式、收藏、删除和操作菜单。"] = "Control sequential pasting, favorites, deletion, and action menus.",
        ["扩展多选"] = "Extend Selection",
        ["扩展连续选区；FIFO/LIFO 模式中新复制内容会按当前模式自动入队，并显示队列角标、置顶队列项。"] = "Extend the contiguous selection. In FIFO/LIFO modes, newly copied content is queued automatically; queued items show badges and stay at the top.",
        ["类型筛选"] = "Type Filter",
        ["连续按两次删除当前条目；第一次显示删除线，第二次确认。"] = "Press Delete twice to remove the current item. The first press marks it; the second confirms.",
        ["列表向上或向下翻页；快捷键可在设置中修改。"] = "Move the list one page up or down. Shortcuts can be changed in Settings.",
        ["列表向上或向下翻页。"] = "Move the list one page up or down.",
        ["批量模式"] = "Batch Mode",
        ["批量与管理"] = "Batch and Management",
        ["普通模式多选顺序连贴时，在每条文本后发送软换行。"] = "When sequentially pasting multiple selections in Normal mode, send a soft line break after each text item.",
        ["普通模式多选顺序连贴时，在每条文本末尾附带换行。"] = "When sequentially pasting multiple selections in Normal mode, append a newline to each text item.",
        ["切换目录"] = "Change Directory",
        ["切换文件对话框目录；面板保持贴靠并继续跟随对话框。"] = "Change the file dialog directory while keeping the panel docked to and following the dialog.",
        ["让当前文件对话框跳转到所选文件夹。"] = "Navigate the current file dialog to the selected folder.",
        ["软换行连贴"] = "Paste with Soft Line Breaks",
        ["筛选与查看"] = "Filter and Inspect",
        ["筛选与预览"] = "Filter and Preview",
        ["收藏关键词（用于 everything 筛选）"] = "Favorite keyword (used by Everything filtering)",
        ["收藏或取消收藏当前条目；多选时批量切换。"] = "Favorite or unfavorite the current item; toggle all selected items when multi-selecting.",
        ["收藏或取消收藏当前文件夹。"] = "Favorite or unfavorite the current folder.",
        ["收藏与管理"] = "Favorites and Management",
        ["首尾"] = "First / Last",
        ["输入 / 空格"] = "Type / Space",
        ["数字快贴"] = "Quick Paste by Number",
        ["数字直达"] = "Jump by Number",
        ["搜索"] = "Search",
        ["搜索光标"] = "Search Caret",
        ["搜索剪贴板内容；空格分词按 AND 匹配，也可检索来源与快捷短语。"] = "Search clipboard content. Space-separated terms use AND matching; sources and quick phrases are searchable too.",
        ["搜索进程名…"] = "Search process names…",
        ["搜索路径、关键词和来源；Everything 补充结果不支持拼音。"] = "Search paths, keywords, and sources. Supplemental Everything results do not support Pinyin.",
        ["缩小结果范围，并查看条目完整内容。"] = "Narrow the results and inspect an item's full content.",
        ["缩小路径范围，并查看当前文件夹详情。"] = "Narrow the path results and inspect details for the current folder.",
        ["添加排除应用"] = "Add Excluded Application",
        ["添加选中"] = "Add Selected",
        ["跳转"] = "Navigate",
        ["退出与恢复"] = "Exit and Recovery",
        ["为当前文件对话框选择目标目录。"] = "Choose a target directory for the current file dialog.",
        ["为文件对话框切换目录；选择后面板仍保持跟随。"] = "Change the file dialog directory; the panel continues following it afterward.",
        ["无搜索时移动到列表首项或末项；搜索时移动文本光标。"] = "Without a search, move to the first or last list item; during search, move the text caret.",
        ["先看核心操作；其余按任务分组。当前面板主键：{m}。"] = "Core actions come first; the rest are grouped by task. Current panel modifier: {m}.",
        ["显示当前文件夹的标签、来源和完整路径。"] = "Show the current folder's label, source, and full path.",
        ["详情"] = "Details",
        ["向上或向下移动当前选择。"] = "Move the current selection up or down.",
        ["修改当前文件夹用于搜索的自定义关键词。"] = "Edit the custom search keyword for the current folder.",
        ["选择"] = "Select",
        ["选择与浏览"] = "Selection and Navigation",
        ["循环切换普通 → LIFO → FIFO；与顶栏批量标签左键操作相同，可在设置中修改快捷键。"] = "Cycle Normal → LIFO → FIFO, the same as left-clicking the batch label in the header. The shortcut is configurable in Settings.",
        ["循环切换全部、文本、图片、文件等类型。"] = "Cycle through All, Text, Image, Files, and other types.",
        ["依次切换：全部 → 收藏 → 常用。"] = "Cycle All → Favorites → Recent.",
        ["移除常用"] = "Remove Recent",
        ["移动当前焦点；已点选的多选项会继续保留。"] = "Move focus while preserving items already selected for multi-selection.",
        ["移动到列表首项或末项。"] = "Move to the first or last list item.",
        ["移动焦点、翻页与构建多选。"] = "Move focus, page through results, and build a multi-selection.",
        ["有队列时打开批量一次性粘贴菜单；否则打开当前条目的操作菜单。"] = "Open the paste-entire-queue menu when a queue exists; otherwise open the current item's action menu.",
        ["预览"] = "Preview",
        ["预览当前条目的全文、图片或文件详情。"] = "Preview the current item's full text, image, or file details.",
        ["在列表与搜索文本中快速定位。"] = "Quickly locate items in the list and search text.",
        ["在资源管理器中打开当前选中的文件夹。"] = "Open the selected folder in File Explorer.",
        ["粘贴、数字快贴和搜索是最高频入口。"] = "Paste, number-based quick paste, and search are the most common actions.",
        ["粘贴当前条目；有队列时优先粘贴队首。普通模式多选会顺序连贴，FIFO/LIFO 多选会入队；在其它窗口按 Ctrl+V 或 Shift+Insert 可逐条出队。"] = "Paste the current item, or the queue head when a queue exists. In Normal mode, multiple selections paste sequentially; in FIFO/LIFO they are queued. Use Ctrl+V or Shift+Insert in another window to dequeue one at a time.",
        ["粘贴路径"] = "Paste Path",
        ["整理收藏、关键词与常用路径。"] = "Organize favorites, keywords, and recent paths.",
        ["直接跳转到列表第 1～9 项，无需先移动选择。"] = "Jump directly to list item 1–9 without moving the selection first.",
        ["只显示快捷短语；再按一次恢复完整列表。"] = "Show only quick phrases; press again to restore the full list.",
        ["Esc 会优先撤销当前层级，不会立刻粗暴关窗。"] = "Esc first backs out of the current layer instead of closing the window immediately.",
        ["Esc 会优先撤销当前层级，再关闭面板。"] = "Esc backs out of the current layer before closing the panel.",
        ["F1 / 中键"] = "F1 / Middle Click",
        ["{_displayItems.Count} 条结果"] = "{_displayItems.Count} results",
        ["{_displayRows.Count} 条结果"] = "{_displayRows.Count} results",
        ["{entry.FilePaths!.Length} 个文件"] = "{entry.FilePaths!.Length} files",
        ["{entry.ImageWidth}×{entry.ImageHeight} 图片"] = "{entry.ImageWidth}×{entry.ImageHeight} image",
        ["{items.Count} 项"] = "{items.Count} items",
        ["{panel}条目动作“{actions[i].Label}”与“{actions[j].Label}”不能使用相同快捷键。"] = "The {panel} item actions “{actions[i].Label}” and “{actions[j].Label}” cannot use the same shortcut.",
        ["将删除当前所有自定义规则，并替换为文件中的列表。\n确定继续？"] = "All current custom rules will be deleted and replaced by the list in the file.\nContinue?",
        ["确定要清空所有剪切板历史？\n（快捷短语不受影响）"] = "Clear all clipboard history?\n(Quick phrases will not be affected.)",
        ["文件中没有可导入的有效规则（需包含非空的 windowClass）。"] = "The file contains no valid rules to import (windowClass must not be empty).",
        ["推荐：点击「打开系统设置」，在语言选项中勾选「光学字符识别」后安装。"] = "Recommended: click Open System Settings, then enable Optical Character Recognition in the language options and install it.",
        ["无法启动安装程序。请尝试「打开系统设置」手动安装，或以管理员身份运行 ClipboardX。"] = "Unable to start the installer. Try installing manually through Open System Settings, or run ClipboardX as administrator.",
        ["删除此规则？\n{r.SummaryLine}"] = "Delete this rule?\n{r.SummaryLine}",
        ["已合并写入 {n} 条有效规则。"] = "Merged and saved {n} valid rules.",
        ["已替换为 {n} 条规则。"] = "Replaced the rules with {n} entries.",
        ["存储文件："] = "Storage file: ",
        ["导入失败：\n"] = "Import failed:\n",
        ["导出失败：\n"] = "Export failed:\n",
        ["图片作为文件粘贴"] = "Paste Image as File",
        ["收藏/取消收藏"] = "Favorite/Unfavorite",
        ["文件跳转"] = "Folder Jump",
        ["按住面板主键 {m}，直接粘贴列表第 1～9 条。"] = "Hold the panel modifier {m} to paste list item 1–9 directly.",
        ["识别图片中的文字需要 Windows 的「{display}」OCR 组件（约几十 MB，从 Windows 更新下载）。\n\n"] = "Recognizing text in images requires the Windows “{display}” OCR component (tens of MB, downloaded through Windows Update).\n\n",
        ["开启时优先注入宿主进程用 IShellBrowser 切换目录（仅系统公共对话框）。关闭则仅用地址栏/键入模拟，可减少杀软误报或宿主拦截导致的问题。"] = "When enabled, ClipboardX first injects into the host process and uses IShellBrowser to change directories (system common dialogs only). When disabled, it only simulates address-bar input, which can reduce antivirus false positives and host blocking.",
        ["检测到打开/保存对话框成为前台时，自动采集候选并弹出跳转列表（紧贴对话框），让你直接选目标，无需快捷键。同一对话框只自动弹一次；手动 Ctrl+G 不受影响。开启时跳转列表（含 Ctrl+G 弹出）始终贴对话框，「跳转列表跟随」选项隐藏。"] = "When an Open/Save dialog enters the foreground, automatically collect candidates and show a jump list docked to the dialog. Each dialog opens the list only once; manual Ctrl+G is unaffected. While enabled, all jump lists stay docked to the dialog and the placement option is hidden.",
        ["对话框成为前台时直接跳到候选首条（与 Ctrl+G 列表第一条一致），不需要任何点击。同一对话框只自动跳一次。部分宿主（如微信）不发前台切换事件，会在框内首次左键时兜底触发同样动作。与「对话框打开时自动弹出列表」同时开启：先直跳最佳路径再弹列表，你仍能在列表里改换目标。默认关闭，避免未确认即改路径。"] = "When a dialog enters the foreground, jump directly to the first candidate (the same as the first Ctrl+G result) without requiring a click. Each dialog auto-jumps only once. Hosts that do not emit a foreground event use the first click inside the dialog as a fallback. If the automatic list is also enabled, ClipboardX jumps first and then shows the list so you can choose another target. Disabled by default.",
        ["仅在「对话框打开时自动弹出列表」关闭时可见并生效；该项开启时列表始终贴对话框。跟随对话框：按 Ctrl+G 弹出的列表紧贴文件对话框并随窗口移动。跟随鼠标：按 Ctrl+G 弹出的列表在鼠标附近弹出。"] = "Visible and effective only when automatic list opening is disabled; otherwise the list always stays with the dialog. Follow Dialog docks the Ctrl+G list to the dialog and moves with it. Follow Mouse opens the list near the pointer.",
        ["从资源管理器/TC 等切回已打开的文件对话框时，会重新采集并刷新候选列表；开启时，若最近一次外部文件夹与对话框当前目录不同，则自动跳转过去（仅当前一次前台在「能采到路径的文件管理器」上时才按外部目录同步，避免在对话框里改路径后到其它程序再切回被误拉回资源管理器旧路径）。与「对话框打开时自动弹出列表」配合：后者偏首次到前台，本项偏每次切回。"] = "When returning from File Explorer, Total Commander, or another manager to an open file dialog, recollect and refresh candidates. If enabled and the most recent external folder differs from the dialog directory, navigate there automatically. Synchronization occurs only when the previous foreground app was a supported file manager, preventing stale paths from being restored after visiting unrelated apps. Automatic list opening handles the first activation; this option handles each return.",
        ["在跳转列表内输入检索时，除收藏与候选路径外，再用 voidtools Everything 补充匹配的文件夹并追加显示（来源标签为 everything）。本地收藏/候选支持中文拼音检索；Everything 补充结果只按原文件夹名/路径检索，不支持拼音展开。需本机已安装并运行 Everything；单次最大条数与「实验性功能」页中 everything 筛选条数相同。"] = "While searching in the jump list, use voidtools Everything to append matching folders in addition to favorites and collected paths (source label: everything). Local favorites and candidates support Chinese Pinyin search; supplemental Everything results match only original folder names and paths. Everything must be installed and running. The per-query limit matches the Experimental tab setting.",
        ["在系统资源管理器、焦点不在地址栏/搜索框时，直接键入字符：用 voidtools Everything 将结果限定在当前文件夹（含子文件夹），小窗列出匹配项；Enter 在资源管理器中定位选中项。需本机已安装并运行 Everything；依赖 Everything64.dll（通常随 Everything 安装）。"] = "In Windows File Explorer, when focus is outside the address and search bars, type directly to query the current folder and its subfolders through voidtools Everything. A small window lists matches; Enter locates the selected item in Explorer. Everything must be installed and running, and Everything64.dll is required.",
        ["选中搜索结果后的操作：在资源管理器内就地导航选中文件，或直接打开前台文件对话框跳转到该路径。"] = "After selecting a result, either locate it in File Explorer or navigate the foreground file dialog directly to its path.",
        ["针对内置识别为「无」的文件对话框：按窗口类名与进程名匹配。导入/导出为 JSON；合并导入时相同键的规则会被覆盖。探测向导也可从托盘菜单运行。"] = "For file dialogs not recognized by built-in rules, match by window class and process name. Import and export use JSON; merge import overwrites rules with the same key. The detection wizard is also available from the tray menu.",
        ["点击后按下组合键（须含 Ctrl/Shift/Alt/Win 之一），默认 Ctrl+-"] = "Click, then press a key combination containing Ctrl, Shift, Alt, or Win. Default: Ctrl+-.",
        ["点击后按下组合键（须含修饰键），默认 Ctrl+="] = "Click, then press a key combination containing a modifier. Default: Ctrl+=.",
        ["点击右侧区域后，按下要绑定的组合键。须与「剪贴板」页中的呼出快捷键、批量模式切换键各不相同。"] = "Click the field on the right, then press the desired key combination. It must differ from the Clipboard tab's open-panel and batch-mode shortcuts."
        ,
        ["开启时：全部纯文本会合并成一段一次粘贴。若和图片、文件等混合，则只合并「彼此相邻」的纯文本段，非文本仍分段粘贴。关闭则每条分段粘贴，目标里通常可多步撤销。普通模式下多选按 Enter 为顺序连贴；Alt+Enter 会在每条文本后追加换行；Shift+Enter 会逐条粘贴并在每条文本后发送软换行。FIFO/LIFO 模式下多选 Enter 为入队而不立即连贴；在他处每次 Ctrl+V 或 Shift+Insert 会出队一条并把下一项写入剪贴板。"] = "When enabled, all-text selections are merged and pasted once. In mixed selections, only adjacent text items are merged; non-text items remain separate. When disabled, every item is pasted separately, usually allowing step-by-step undo in the target. In Normal mode, Enter pastes multiple selections in order; Alt+Enter appends a newline to each text item; Shift+Enter sends a soft line break after each text item. In FIFO/LIFO modes, Enter queues selected items instead of pasting immediately; each Ctrl+V or Shift+Insert elsewhere dequeues one item and places the next on the clipboard."
        ,
        ["FIFO / LIFO：队列中条目在他处已全部粘贴完毕（队列为空）后，再按一次 Ctrl+V 或 Shift+Insert 时自动切回「普通」批量模式，避免长期停留在队列模式。关闭则队列为空后仍保持当前 FIFO 或 LIFO，需手动切换模式。"] = "FIFO / LIFO: after every queued item has been pasted elsewhere and the queue is empty, the next Ctrl+V or Shift+Insert automatically returns to Normal batch mode. This prevents the app from remaining in queue mode indefinitely. When disabled, an empty queue remains in the current FIFO or LIFO mode until you switch manually."
        ,
        ["ClipboardX — 发现新版本"] = "ClipboardX — New Version Available"
        ,
        ["版本 {ver} 已发布，托盘右键「检查更新…」可下载安装。（当前 {current}）"] = "Version {ver} is available. Right-click the tray icon and choose Check for Updates… to download and install it. (Current: {current})"
        ,
        ["当前已是最新版本（{current}）。"] = "You already have the latest version ({current})."
        ,
        ["无法获取更新信息（请检查网络或稍后重试）：\n{ex.Message}"] = "Unable to retrieve update information. Check your connection or try again later:\n{ex.Message}"
        ,
        ["已按当前运行方式匹配：**框架依赖**（与本机 dotnet 共享运行时，包较小）。"] = "Matched the current deployment: **framework-dependent** (shares the installed .NET runtime; smaller package)."
        ,
        ["已按当前运行方式匹配：**自带运行时**（单文件内含 .NET，包较大）。"] = "Matched the current deployment: **self-contained** (.NET included in the single file; larger package)."
        ,
        ["发现新版本 {verRemote}（当前 {current}）。\n\n"] = "A new version {verRemote} is available (current: {current}).\n\n"
        ,
        ["说明：{note}\n\n"] = "Release notes: {note}\n\n"
        ,
        ["将下载：{asset.Name}\n大小约 {GitHubUpdateService.FormatSizeMb(asset.Size)}\n\n"] = "Download: {asset.Name}\nApproximate size: {GitHubUpdateService.FormatSizeMb(asset.Size)}\n\n"
        ,
        ["安装目录：\n{installDir}\n\n"] = "Install directory:\n{installDir}\n\n"
        ,
        ["程序将关闭后自动替换并重新启动。\n是否继续？"] = "ClipboardX will close, replace its files, and restart automatically.\nContinue?"
        ,
        ["ClipboardX 更新"] = "ClipboardX Update"
        ,
        ["正在从 GitHub 下载更新…"] = "Downloading update from GitHub…"
        ,
        ["压缩包内未找到 {AppInfo.PrimaryExecutableFileName}，已中止。"] = "{AppInfo.PrimaryExecutableFileName} was not found in the archive. The update was canceled."
        ,
        ["下载完成。是否立即退出并完成安装？（将自动重启 ClipboardX）"] = "Download complete. Exit now and finish installation? ClipboardX will restart automatically."
        ,
        ["更新"] = "Update"
        ,
        ["更新未成功：\n{ex.Message}"] = "The update did not complete:\n{ex.Message}"
        ,
        ["开始菜单或搜索打开时，剪贴板窗口可能被系统界面挡住，属系统限制。请先按 Esc 关闭开始菜单或搜索，再按热键呼出。"] = "The Start menu or Windows Search may cover the clipboard window due to a system limitation. Press Esc to close it, then use the ClipboardX shortcut again."
        ,
        ["3 秒后采集前台窗口信息，请切换到目标窗口…"] = "Foreground-window information will be captured in 3 seconds. Switch to the target window…"
        ,
        ["3 秒后采集前台窗口并尝试多种跳转校验，请先打开目标文件对话框并切到该窗口…"] = "In 3 seconds ClipboardX will capture the foreground window and test navigation methods. Open the target file dialog and switch to it…"
        ,
        ["未获取到前台窗口。"] = "Unable to get the foreground window."
        ,
        ["采集窗口"] = "Capture Window"
        ,
        ["ClipboardX 窗口信息采集"] = "ClipboardX Window Information"
        ,
        ["当前窗口已被内置识别为文件对话框（对话框识别不是 None），不会走自定义规则。\n请仅对内置识别为「无」的窗口使用本功能。"] = "The current window is already recognized as a file dialog by a built-in rule, so custom rules will not be used.\nUse this feature only for windows whose built-in classification is None."
        ,
        ["无法确定用于校验的有效文件夹路径。\n请先在任意已支持跳转的对话框里浏览到目标文件夹（更新「上次路径」），或复制某个已存在目录的完整路径到剪贴板后再试。"] = "Unable to determine a valid folder for verification.\nFirst browse to the target folder in any supported dialog (updating the last path), or copy the full path of an existing folder to the clipboard, then try again."
        ,
        ["将依次尝试多种跳转方式，并通过读取当前路径判断是否已进入下列文件夹：\n\n"] = "ClipboardX will try several navigation methods and read the current path to verify that it reached this folder:\n\n"
        ,
        ["\n\n请确认该文件对话框当前**不在**此文件夹内，否则会误判。\n\n确定开始探测？"] = "\n\nMake sure the file dialog is **not currently in** this folder, or verification will produce a false result.\n\nStart detection?"
        ,
        ["已保存。已锁定优先策略：{rule.PinnedStrategy}"] = "Saved. Preferred strategy: {rule.PinnedStrategy}"
        ,
        ["已保存。未能自动校验出有效策略，跳转时将按顺序依次尝试。\n建议：把对话框切换到其他文件夹后，可从托盘再运行一次本向导。"] = "Saved, but no strategy could be verified automatically. Navigation methods will be tried in order.\nTip: move the dialog to another folder, then run this wizard again from the tray."
        ,
        ["使用全拼和首字母过滤中文，例如 nihao / nh。"] = "Filter Chinese text using full Pinyin and initials, such as nihao / nh."
        ,
        ["使用小鹤双拼全码和声母串过滤中文。"] = "Filter Chinese text using full Xiaohe Shuangpin codes and initial strings."
        ,
        ["已在用户「程序」目录安装 ClipboardX。请从开始菜单或安装位置启动本程序后使用托盘「卸载」，或先卸载后再从此副本安装。"] = "ClipboardX is already installed in your user Programs folder. Launch that copy and use Uninstall from its tray menu, or uninstall it before installing from this copy."
        ,
        ["当前为调试构建，不会复制到「程序」安装目录。请使用 Release 产物测试安装菜单。"] = "This is a debug build and will not be copied to the Programs installation directory. Use a Release build to test installation."
        ,
        ["关于 ClipboardX"] = "About ClipboardX"
        ,
        ["版本：{AppInfo.DisplayVersion}\n"] = "Version: {AppInfo.DisplayVersion}\n"
        ,
        ["作者：mact\n"] = "Author: mact\n"
        ,
        ["邮箱：chaoji000010@163.com"] = "Email: chaoji000010@163.com"
        ,
        ["未能复制程序到安装目录（可能被杀软拦截、旧进程未退出导致文件被占用、或无权写入）：\n"] = "Unable to copy the application to the install directory. Antivirus software, a running older process, or insufficient permissions may be blocking it:\n"
        ,
        ["\n\n可在任务管理器中结束「ClipboardX」后重试，或注销/重启后再试。\n"] = "\n\nEnd ClipboardX in Task Manager and try again, or sign out/restart Windows and retry.\n"
        ,
        ["将尝试从当前位置继续运行。"] = "ClipboardX will continue running from the current location."
        ,
        ["已安装到：\n"] = "Installed to:\n"
        ,
        ["\n\n但无法启动，请手动运行该路径下程序：\n"] = "\n\nThe installed copy could not be launched. Run it manually from this path:\n"
        ,
        ["将卸载 ClipboardX（版本 {version}）、移除开始菜单快捷方式、开机启动与「应用和功能」条目，并删除安装目录中的程序文件。\n\n是否同时删除配置与历史记录？（%AppData%\\ClipboardX，旧版可能在 ClipboardManager）\n\n「是」删除程序与配置；「否」只删程序；「取消」中止。"] = "This will uninstall ClipboardX {version}, remove its Start-menu shortcut, startup entry, and Apps & Features entry, and delete program files from the install directory.\n\nAlso delete settings and history? (%AppData%\\ClipboardX; older versions may use ClipboardManager.)\n\nYes deletes the app and data; No deletes only the app; Cancel stops."
        ,
        ["卸载 ClipboardX — {version}"] = "Uninstall ClipboardX — {version}"
        ,
        ["已取消卸载。"] = "Uninstallation canceled."
        ,
        ["无法启动清理任务。请手动删除文件夹：\n"] = "Unable to start the cleanup task. Delete this folder manually:\n"
        ,
        ["卸载已完成或即将完成。程序将退出。"] = "Uninstallation is complete or finishing. The app will exit."
        ,
        ["卸载已完成或即将完成。程序即将退出。"] = "Uninstallation is complete or finishing. The app is about to exit."
    };
}
