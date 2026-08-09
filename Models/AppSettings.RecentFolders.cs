using System.IO;
using System.Text.Json;

namespace ClipboardManager;

public partial class AppSettings
{
    /// <summary>
    /// 记录一次明确的路径使用（打开、导航、确认或粘贴路径）。
    /// 常用路径是 MRU 历史：首次使用即加入，再次使用移到列表头部。
    /// 调用方已经完成导航校验，或正在执行无需目录存在的纯文本粘贴；此处不得访问文件系统。
    /// </summary>
    public void RecordRecentFolderUse(string folder)
    {
        PushRecentFileDialogFolderCore(folder, requireExistingDirectory: false);
    }

    /// <summary>兼容旧调用名称；确认目录现在与其他明确路径使用遵循同一套 MRU 逻辑。</summary>
    public void RecordFolderConfirmation(string folder) => RecordRecentFolderUse(folder);

    /// <summary>直接将路径写入常用列表（跳过阈值检查），供手动添加或迁移等场景使用。</summary>
    public void PushRecentFileDialogFolder(string folder)
    {
        PushRecentFileDialogFolderCore(folder, requireExistingDirectory: true);
    }

    private void PushRecentFileDialogFolderCore(string folder, bool requireExistingDirectory)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        string normalized;
        try
        {
            normalized = Path.GetFullPath(folder.Trim());
        }
        catch
        {
            return;
        }

        if (requireExistingDirectory)
        {
            try
            {
                if (!Directory.Exists(normalized)) return;
            }
            catch
            {
                return;
            }
        }

        if (IsApplicationInstallDirectory(normalized)) return;

        PushToRecentFileDialogFolders(normalized);
    }

    private void PushToRecentFileDialogFolders(string normalized)
    {
        RecentFileDialogFolders ??= new List<string>();
        RecentFileDialogFolders.RemoveAll(p =>
        {
            if (string.IsNullOrWhiteSpace(p)) return true;
            try
            {
                return string.Equals(Path.GetFullPath(p.Trim()), normalized, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });
        RecentFileDialogFolders.Insert(0, normalized);
        var maxCount = Math.Clamp(RecentFolderMaxCount, 1, 50);
        while (RecentFileDialogFolders.Count > maxCount)
            RecentFileDialogFolders.RemoveAt(RecentFileDialogFolders.Count - 1);

        LastFileDialogFolder = RecentFileDialogFolders.Count > 0 ? RecentFileDialogFolders[0] : "";
        Save();
    }

    /// <summary>设置中的数量上限变小时，立即裁剪最久未使用的常用路径。</summary>
    public void ApplyRecentFolderLimit()
    {
        RecentFolderMaxCount = Math.Clamp(RecentFolderMaxCount, 1, 50);
        RecentFileDialogFolders ??= new List<string>();
        while (RecentFileDialogFolders.Count > RecentFolderMaxCount)
            RecentFileDialogFolders.RemoveAt(RecentFileDialogFolders.Count - 1);
        LastFileDialogFolder = RecentFileDialogFolders.Count > 0 ? RecentFileDialogFolders[0] : "";
    }

    private static bool IsApplicationInstallDirectory(string path)
    {
        try
        {
            var programDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "ClipboardX");
            var programDirFull = Path.GetFullPath(programDir);
            var pathFull = Path.GetFullPath(path);
            return string.Equals(pathFull, programDirFull, StringComparison.OrdinalIgnoreCase) ||
                   pathFull.StartsWith(programDirFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>从常用路径列表中移除指定路径并保存。</summary>
    public void RemoveRecentFileDialogFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        string normalized;
        try
        {
            normalized = Path.GetFullPath(folder.Trim());
        }
        catch
        {
            return;
        }

        RecentFileDialogFolders ??= new List<string>();
        RecentFileDialogFolders.RemoveAll(p =>
        {
            if (string.IsNullOrWhiteSpace(p)) return true;
            try
            {
                return string.Equals(Path.GetFullPath(p.Trim()), normalized, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });

        LastFileDialogFolder = RecentFileDialogFolders.Count > 0 ? RecentFileDialogFolders[0] : "";
        Save();
    }
}

