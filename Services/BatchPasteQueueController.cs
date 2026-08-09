using ClipboardManager.Models;

namespace ClipboardManager;

internal sealed class BatchPasteQueueController
{
    private readonly List<ClipboardEntry> _items = new();

    public int Count => _items.Count;

    public ClipboardEntry? Head => _items.Count > 0 ? _items[0] : null;

    public IReadOnlyList<ClipboardEntry> Items => _items;

    public List<ClipboardEntry> Snapshot() => _items.ToList();

    public void Clear()
    {
        _items.Clear();
    }

    public void Enqueue(IEnumerable<ClipboardEntry> entries, BatchPasteQueueMode mode)
    {
        if (mode == BatchPasteQueueMode.Off) return;

        foreach (var entry in entries)
        {
            _items.Remove(entry);
            if (mode == BatchPasteQueueMode.Fifo)
                _items.Add(entry);
            else
                _items.Insert(0, entry);
        }
    }

    public void Enqueue(ClipboardEntry entry, BatchPasteQueueMode mode)
    {
        Enqueue([entry], mode);
    }

    public bool TryAdvance(out ClipboardEntry? entry)
    {
        if (_items.Count == 0)
        {
            entry = null;
            return false;
        }

        entry = _items[0];
        _items.RemoveAt(0);
        return true;
    }

    public void RestoreHead(ClipboardEntry entry)
    {
        _items.Remove(entry);
        _items.Insert(0, entry);
    }

    public bool Remove(ClipboardEntry entry) => _items.Remove(entry);

    public int RemoveAll(Predicate<ClipboardEntry> match) => _items.RemoveAll(match);

    public bool HeadStill(BatchPasteQueueMode mode, ClipboardEntry item) =>
        mode != BatchPasteQueueMode.Off
        && _items.Count > 0
        && ReferenceEquals(_items[0], item);

    public void ApplyOrderProperties(IEnumerable<ClipboardEntry> allItems)
    {
        foreach (var entry in allItems)
            entry.BatchOrder = 0;

        var order = 1;
        foreach (var entry in _items)
            entry.BatchOrder = order++;
    }

    public void ReorderAllItemsQueueFirst(List<ClipboardEntry> allItems)
    {
        if (_items.Count == 0) return;

        var queued = new HashSet<ClipboardEntry>(_items);
        var tail = allItems.Where(entry => !queued.Contains(entry)).ToList();
        allItems.Clear();
        allItems.AddRange(_items);
        allItems.AddRange(tail);
    }

    public List<ClipboardEntry> FilterQueued(IReadOnlySet<ClipboardEntry> filteredSet) =>
        _items.Where(filteredSet.Contains).ToList();

    public bool ContainsQueued(ClipboardEntry entry) => _items.Contains(entry);
}
