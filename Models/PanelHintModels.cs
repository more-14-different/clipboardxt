using System.Collections.Generic;

namespace ClipboardManager.Models;

/// <summary>说明系统的莫兰迪语义色；同类操作在顶部、底部和 more 中保持一致。</summary>
public enum PanelHintTone
{
    Action,
    Transfer,
    Search,
    Navigation,
    Filter,
    Manage,
    Exit,
}

/// <summary>面板中一项快捷操作：短标签用于上下摘要，详细说明用于 more 指南。</summary>
public sealed record PanelHintItem(
    string Key,
    string Label,
    string Description,
    PanelHintTone Tone = PanelHintTone.Action);

/// <summary>more 指南中的一个任务分组。</summary>
public sealed record PanelHintSection(
    string Icon,
    string Title,
    string Description,
    IReadOnlyList<PanelHintItem> Items,
    PanelHintTone Tone = PanelHintTone.Action);
