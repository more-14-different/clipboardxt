using System.IO;
using System.Text.Json;

namespace ClipboardManager;

public class QuickPasteEntry
{
    public string Phrase { get; set; } = "";
    public string Content { get; set; } = "";
}

public partial class AppSettings
{
    /// <summary>前端界面语言：zh-CN（简体中文）或 en-US（English）。</summary>
    public string UiLanguage { get; set; } = ClipboardManager.UiLanguage.Chinese;

    /// <summary>关闭并再次打开同一面板时，恢复本次应用会话内的搜索文字和筛选状态。</summary>
    public bool RememberPanelOperationState { get; set; } = true;

    internal PanelOperationStateStore PanelOperationStates { get; } = new();

    public int MaxItems { get; set; } = 2000;

    /// <summary>图片条目专项上限，避免图片历史占用过多内存。</summary>
    public int MaxImageItems { get; set; } = 150;

    /// <summary>单张图片最大 PNG 字节数，默认 15 MB。</summary>
    public long MaxImageSizeBytes { get; set; } = 15 * 1024 * 1024;

    /// <summary>剪贴板搜索结果不足时，是否继续检索冷归档桶。关闭后只查询热表。</summary>
    public bool SearchColdArchives { get; set; } = true;
    public uint HotkeyModifiers { get; set; } = Win32.MOD_CONTROL;
    public uint HotkeyKey { get; set; } = Win32.VK_OEM_3;

    /// <summary>「打开/保存」对话框中跳转到文件夹的全局快捷键（默认 Ctrl+G）。</summary>
    public uint FileJumpHotkeyModifiers { get; set; } = Win32.MOD_CONTROL;
    public uint FileJumpHotkeyKey { get; set; } = Win32.VK_G;

    /// <summary>多候选时跳转列表弹出前的延时（毫秒）；0 表示立即弹出。自动弹出时仍会合并极短防抖（约一帧），见 PopupWindow。</summary>
    public int FileJumpPickerShowDelayMs { get; set; } = 0;

    /// <summary>Mouse：跳转列表跟随鼠标附近；Dialog：紧贴文件对话框并随窗口移动。</summary>
    public string FileJumpPickerFollowMode { get; set; } = FileJumpPickerFollowModes.Dialog;

    /// <summary>
    /// 历史字段：是否弹出跳转列表。当前版本与 <see cref="FileJumpPickerOpenWhenDialogForeground"/> 同义，
    /// 保存时强制与之相等；读取旧配置若不一致则取 OR。保留以兼容旧 settings.json。
    /// </summary>
    public bool FileJumpPickerAutoPopup { get; set; } = true;

    /// <summary>
    /// 「对话框打开时自动弹出列表」：检测到文件对话框成为前台时（含焦点首次进框兜底），自动采集候选并弹出跳转列表。
    /// 开启时跳转列表（含 Ctrl+G 弹出）始终贴对话框；关闭时按 <see cref="FileJumpPickerFollowMode"/>。
    /// 同一对话框 root 仅自动弹一次；手动 Ctrl+G 不受影响。与 <see cref="FileJumpAutoOnFirstClick"/> 同时开
    /// 时为 A 方案：先直跳最佳路径再弹列表，用户可在列表内再换。
    /// </summary>
    public bool FileJumpPickerOpenWhenDialogForeground { get; set; } = true;

    /// <summary>
    /// 从资源管理器/TC 切回已打开的文件对话框时，重新采集候选列表；
    /// 若最新外部文件夹与对话框当前路径不同，则自动跳转（可关闭）。
    /// 与 <see cref="FileJumpPickerOpenWhenDialogForeground"/> 的区别：后者只在「首次」到前台时触发一次，
    /// 此选项在「每次」切回时重新采集并比较。
    /// </summary>
    public bool FileJumpAutoSyncOnReturn { get; set; } = true;

    /// <summary>
    /// 系统公共文件对话框内跳转时，是否尝试将 Shell 导航 DLL 注入宿主进程（IShellBrowser::BrowseObject）。
    /// 关闭后仅走地址栏/键入模拟，兼容部分杀软或宿主拦截注入的环境；WPS 等自定义对话框始终不注入。
    /// </summary>
    public bool EnableShellNavigateInject { get; set; } = true;

    /// <summary>
    /// 「自动跳转到最佳路径」：对话框成为前台时直接跳到候选首条（不依赖快捷键、不依赖点击）。
    /// 同一对话框 root 仅成功一次。配合内部低级鼠标钩兜底：部分宿主（如微信）不发前台事件时，
    /// 会在对话框内首次左键时触发等价直跳；钩子仅在本开关开启时武装。
    /// 与 <see cref="FileJumpPickerOpenWhenDialogForeground"/> 同时开时为 A 方案：先直跳再弹列表。
    /// 字段名沿用历史 (First Click)，语义已升级为"自动跳转"，保留名以兼容旧 settings.json。
    /// </summary>
    public bool FileJumpAutoOnFirstClick { get; set; } = false;

    public string Theme { get; set; } = "System";
    public string PopupPosition { get; set; } = "Caret";
    public double PopupOpacity { get; set; } = 1.0;
    public bool HideOnSameAppClick { get; set; } = true;

    /// <summary>开启时：单击列表仅选中、不粘贴，双击才粘贴；关闭时：单击即粘贴（默认）。</summary>
    public bool PasteRequiresDoubleClick { get; set; } = false;
    /// <summary>登录 Windows 时自动启动本程序（默认开启）。</summary>
    public bool RunAtStartup { get; set; } = true;

    /// <summary>为 true 时，手动启动请求 UAC；开机自启通过最高权限计划任务运行，不在登录时弹出 UAC。</summary>
    public bool RunAsAdministrator { get; set; } = true;

    /// <summary>向目标窗口模拟粘贴：<see cref="PasteSimulationModes.CtrlV"/>（Ctrl+V）或 <see cref="PasteSimulationModes.ShiftInsert"/>。</summary>
    public string PasteSimulationMode { get; set; } = PasteSimulationModes.CtrlV;

    /// <summary>启动后静默访问 GitHub Releases，若有新版本则在托盘气泡提示（不弹阻断窗）。</summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>用 ClipboardX 替换系统 Win+V 快捷键（拦截系统剪贴板历史，触发 ClipboardX 弹窗）。</summary>
    public bool ReplaceSystemWinV { get; set; } = false;

    /// <summary>退出时自动清空剪贴板历史记录（保留快捷短语）。</summary>
    public bool ClearHistoryOnExit { get; set; } = false;

    /// <summary>复制图片后使用 Windows 内置 OCR 识别文字。</summary>
    public bool ImageOcrEnabled { get; set; } = true;

    /// <summary>中文 ASCII 检索展开方式：Traditional（全拼 + 首字母）或 Xiaohe（小鹤全码 + 小鹤声母串）。</summary>
    public string PinyinFilterMode { get; set; } = PinyinFilterModes.Traditional;

    /// <summary>当前持久化拼音检索字段所用算法版本；算法变更时用于触发重建。</summary>
    public int PinyinFilterIndexVersion { get; set; } = PinyinFilterModes.CurrentIndexVersion;

    /// <summary>启动检测已提示过的发行 tag（如 v1.2.0），避免同一版本重复气泡；升级或已最新时会清空。</summary>
    public string? LastStartupUpdateNotifiedTag { get; set; }
    public int PreviewMaxLines { get; set; } = 2;

    /// <summary>剪贴板弹窗宽度（DIP），默认与内置 XAML 一致。</summary>
    public double PopupPanelWidth { get; set; } = 420;

    /// <summary>剪贴板弹窗最大高度（DIP，列表区域随内容增高直至该上限）。</summary>
    public double PopupPanelMaxHeight { get; set; } = 560;

    /// <summary>剪贴板弹窗实际高度（DIP），0 表示未手动调整过，使用 SizeToContent。</summary>
    public double PopupPanelHeight { get; set; }

    /// <summary>文件跳转弹窗宽度（DIP），默认 500。</summary>
    public double FileJumpPickerWidth { get; set; } = 500;

    /// <summary>文件跳转弹窗最大高度（DIP），默认 620。</summary>
    public double FileJumpPickerMaxHeight { get; set; } = 620;

    /// <summary>文件跳转弹窗实际高度（DIP），0 表示未手动调整过。</summary>
    public double FileJumpPickerHeight { get; set; }

    /// <summary>资源管理器快速筛选弹窗宽度（DIP），默认 540。</summary>
    public double ExplorerQuickFindWidth { get; set; } = 540;

    /// <summary>资源管理器快速筛选弹窗最大高度（DIP），默认 520。</summary>
    public double ExplorerQuickFindMaxHeight { get; set; } = 520;

    /// <summary>资源管理器快速筛选弹窗实际高度（DIP），0 表示未手动调整过。</summary>
    public double ExplorerQuickFindHeight { get; set; }

    /// <summary>列表每次翻过的条目数（PgUp/Dn、←→ 及翻页快捷键共用，原固定为 8）。</summary>
    public int PopupPageItems { get; set; } = 8;

    /// <summary>列表向上翻页组合键（须含修饰键，默认 Ctrl+-）。</summary>
    public uint PanelPageScrollUpModifiers { get; set; } = Win32.MOD_CONTROL;

    public uint PanelPageScrollUpKey { get; set; } = 0xBD;

    /// <summary>列表向下翻页组合键（须含修饰键，默认 Ctrl+=）。</summary>
    public uint PanelPageScrollDownModifiers { get; set; } = Win32.MOD_CONTROL;

    public uint PanelPageScrollDownKey { get; set; } = 0xBB;

    /// <summary>主剪贴板面板内收藏/取消收藏当前选中条目的快捷键，默认 Ctrl+D。</summary>
    public uint StarToggleHotkeyModifiers { get; set; } = Win32.MOD_CONTROL;

    public uint StarToggleHotkeyKey { get; set; } = 0x44;

    /// <summary>剪贴板条目右键动作快捷键；无修饰键也是有效配置。</summary>
    public uint ClipboardPasteHotkeyModifiers { get; set; }
    public uint ClipboardPasteHotkeyKey { get; set; } = Win32.VK_RETURN;
    public uint ClipboardPasteAsFileHotkeyModifiers { get; set; } = Win32.MOD_CONTROL | Win32.MOD_SHIFT;
    public uint ClipboardPasteAsFileHotkeyKey { get; set; } = Win32.VK_RETURN;
    public uint ClipboardPasteJsonHotkeyModifiers { get; set; } = Win32.MOD_CONTROL | Win32.MOD_ALT;
    public uint ClipboardPasteJsonHotkeyKey { get; set; } = Win32.VK_RETURN;
    public uint ClipboardEditTextHotkeyModifiers { get; set; }
    public uint ClipboardEditTextHotkeyKey { get; set; } = 0x71; // F2
    public uint ClipboardShortcutPhraseHotkeyModifiers { get; set; } = Win32.MOD_CONTROL | Win32.MOD_SHIFT;
    public uint ClipboardShortcutPhraseHotkeyKey { get; set; } = 0x53; // S
    public uint ClipboardDeleteHotkeyModifiers { get; set; }
    public uint ClipboardDeleteHotkeyKey { get; set; } = Win32.VK_DELETE;

    /// <summary>文件跳转条目右键动作快捷键。</summary>
    public uint FileJumpFavoriteHotkeyModifiers { get; set; } = Win32.MOD_CONTROL;
    public uint FileJumpFavoriteHotkeyKey { get; set; } = 0x44; // D
    public uint FileJumpEditPhraseHotkeyModifiers { get; set; }
    public uint FileJumpEditPhraseHotkeyKey { get; set; } = 0x71; // F2
    public uint FileJumpRemoveRecentHotkeyModifiers { get; set; }
    public uint FileJumpRemoveRecentHotkeyKey { get; set; } = Win32.VK_DELETE;

    public string PanelModifierKey { get; set; } = "Ctrl";

    /// <summary>批量粘贴：Off / Fifo / Lifo（与 <see cref="BatchPasteQueueMode"/> 枚举名一致）。</summary>
    public string BatchPasteMode { get; set; } = nameof(BatchPasteQueueMode.Off);

    /// <summary>面板未打开时也可用的「批量模式」循环切换快捷键（默认 Alt+/）。须与其它全局热键不同。</summary>
    public uint BatchModeCycleHotkeyModifiers { get; set; } = Win32.MOD_ALT;

    public uint BatchModeCycleHotkeyKey { get; set; } = Win32.VK_OEM_2;

    /// <summary>多选且条目全部为文本时，是否拼成一段一次写入剪贴板并粘贴（关则逐条粘贴，便于多步撤销）。</summary>
    public bool BatchPasteMergeText { get; set; } = true;

    /// <summary>
    /// FIFO / LIFO 下队列已贴完后，在<strong>下一次</strong>他处 Ctrl+V / Shift+Insert 时自动切回普通模式（避免长期停留在队列模式）。
    /// </summary>
    public bool BatchQueueAutoSwitchToNormalAfterQueueDone { get; set; } = true;

    public List<QuickPasteEntry> QuickPastes { get; set; } = new();

    /// <summary>Ctrl+G 跳转列表顶部展示的收藏目录（Phrase 为关键词/别名，供检索）。</summary>
    public List<FolderFavoriteEntry> FolderFavorites { get; set; } = new();

    /// <summary>最近一次在「打开/保存」对话框中记录到的文件夹（与 <see cref="RecentFileDialogFolders"/> 首项同步），供兼容旧逻辑。</summary>
    public string LastFileDialogFolder { get; set; } = "";

    /// <summary>最近明确使用过的路径（打开、导航、确认或粘贴），最多 <see cref="RecentFolderMaxCount"/> 条（新的在前）。</summary>
    public List<string> RecentFileDialogFolders { get; set; } = new();

    /// <summary>常用路径 MRU 历史的最大数量（默认及上限均为 50）。</summary>
    public int RecentFolderMaxCount { get; set; } = 50;

    /// <summary>旧版兼容字段；常用路径现在首次明确使用即加入。</summary>
    public int RecentFolderAutoAddMinCount { get; set; } = 1;

    /// <summary>排除应用列表：前台属于这些进程时不触发 ClipboardX 全局快捷键（进程名不含 .exe 后缀，不区分大小写）。</summary>
    public List<string> ExclusionApps { get; set; } = new();

    /// <summary>面板打开时将指定按键交给 AutoHotkey 等外部低级钩子。</summary>
    public bool KeyPassthroughEnabled { get; set; }

    public uint KeyPassthroughModifierMask { get; set; } = Win32.MOD_CAPS;

    public bool KeyPassthroughKeepPanelKeys { get; set; } = true;

    public List<KeyPassthroughRule> KeyPassthroughRules { get; set; } = new();

    /// <summary>旧版兼容数据；常用排序不再使用累计次数。</summary>
    public Dictionary<string, int> FolderConfirmCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
