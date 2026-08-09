namespace ClipboardManager;

public partial class AppSettings
{
    public bool ExplorerEverythingQuickFindEnabled { get; set; } = true;

    /// <summary>Everything IPC 单次最大返回条数。</summary>
    public int ExplorerEverythingQuickFindMaxResults { get; set; } = 150;

    /// <summary>文件对话框「跳转到文件夹」列表内检索时，用 Everything 补充匹配文件夹（需本机运行 Everything）。</summary>
    public bool FileJumpPickerEverythingFolderSearch { get; set; } = true;

    /// <summary>已保留字段：配置仍可反序列化；当前版本始终走 Everything，保存设置时会写回 false。</summary>
    public bool UseFindXSearch { get; set; } = false;

    /// <summary>资源管理器 Everything 筛选后选中操作模式："Explorer"（默认，在资源管理器内就地导航并选中文件）或 "DirectOpen"（将前台文件对话框导航到目标路径）。</summary>
    public string ExplorerQuickFindOpenMode { get; set; } = "Explorer";
}

