namespace ClipboardManager;

/// <summary>
/// 面板关闭后、应用退出前保留的人机交互状态。查询内容只留在内存中，不写入 settings.json。
/// </summary>
internal sealed class PanelOperationStateStore
{
    public PanelSearchOperationState? FileJumpPicker { get; set; }

    public string ExplorerQuickFindQuery { get; set; } = "";

    public void Clear()
    {
        FileJumpPicker = null;
        ExplorerQuickFindQuery = "";
    }
}

internal sealed record PanelSearchOperationState(
    string Text,
    int CaretIndex,
    int SelectionAnchor,
    int FilterMode,
    string? SelectedKey);
