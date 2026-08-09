using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class BatchPasteQueueControllerTests
{
    [Fact]
    public void Enqueue_Fifo_AppendsInInputOrder()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        var c = Entry("C");

        queue.Enqueue([a, b, c], BatchPasteQueueMode.Fifo);

        Assert.Same(a, queue.Items[0]);
        Assert.Same(b, queue.Items[1]);
        Assert.Same(c, queue.Items[2]);
    }

    [Fact]
    public void Enqueue_Lifo_PushesNewestToHead()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        var c = Entry("C");

        queue.Enqueue([a, b, c], BatchPasteQueueMode.Lifo);

        Assert.Same(c, queue.Items[0]);
        Assert.Same(b, queue.Items[1]);
        Assert.Same(a, queue.Items[2]);
    }

    [Fact]
    public void Enqueue_Fifo_DuplicateMovesToTail()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");

        queue.Enqueue([a, b], BatchPasteQueueMode.Fifo);
        queue.Enqueue(a, BatchPasteQueueMode.Fifo);

        Assert.Equal(2, queue.Count);
        Assert.Same(b, queue.Items[0]);
        Assert.Same(a, queue.Items[1]);
    }

    [Fact]
    public void Enqueue_Lifo_DuplicateMovesToHead()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");

        queue.Enqueue([a, b], BatchPasteQueueMode.Lifo);
        queue.Enqueue(a, BatchPasteQueueMode.Lifo);

        Assert.Equal(2, queue.Count);
        Assert.Same(a, queue.Items[0]);
        Assert.Same(b, queue.Items[1]);
    }

    [Fact]
    public void Enqueue_Off_DoesNotMutateQueue()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");

        queue.Enqueue(a, BatchPasteQueueMode.Off);

        Assert.Equal(0, queue.Count);
        Assert.Null(queue.Head);
    }

    [Fact]
    public void TryAdvance_ReturnsHeadAndRemovesIt()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        queue.Enqueue([a, b], BatchPasteQueueMode.Fifo);

        var advanced = queue.TryAdvance(out var entry);

        Assert.True(advanced);
        Assert.Same(a, entry);
        Assert.Equal(1, queue.Count);
        Assert.Same(b, queue.Head);
    }

    [Fact]
    public void TryAdvance_EmptyQueue_ReturnsFalse()
    {
        var queue = new BatchPasteQueueController();

        var advanced = queue.TryAdvance(out var entry);

        Assert.False(advanced);
        Assert.Null(entry);
    }

    [Fact]
    public void RestoreHead_ReinsertsEntryAtHead()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        queue.Enqueue([a, b], BatchPasteQueueMode.Fifo);
        Assert.True(queue.TryAdvance(out var advanced));

        queue.RestoreHead(advanced!);

        Assert.Same(a, queue.Items[0]);
        Assert.Same(b, queue.Items[1]);
    }

    [Fact]
    public void HeadStill_RequiresActiveModeAndSameReference()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        queue.Enqueue([a, b], BatchPasteQueueMode.Fifo);

        Assert.True(queue.HeadStill(BatchPasteQueueMode.Fifo, a));
        Assert.False(queue.HeadStill(BatchPasteQueueMode.Fifo, b));
        Assert.False(queue.HeadStill(BatchPasteQueueMode.Off, a));
    }

    [Fact]
    public void ApplyOrderProperties_SetsQueueOrderAndClearsOthers()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        var c = Entry("C");
        c.BatchOrder = 42;
        queue.Enqueue([b, a], BatchPasteQueueMode.Fifo);

        queue.ApplyOrderProperties([a, b, c]);

        Assert.Equal(2, a.BatchOrder);
        Assert.Equal(1, b.BatchOrder);
        Assert.Equal(0, c.BatchOrder);
    }

    [Fact]
    public void ReorderAllItemsQueueFirst_MovesQueuedItemsAndKeepsTailOrder()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        var c = Entry("C");
        var d = Entry("D");
        var allItems = new List<ClipboardEntry> { a, b, c, d };
        queue.Enqueue([c, a], BatchPasteQueueMode.Fifo);

        queue.ReorderAllItemsQueueFirst(allItems);

        Assert.Collection(
            allItems,
            item => Assert.Same(c, item),
            item => Assert.Same(a, item),
            item => Assert.Same(b, item),
            item => Assert.Same(d, item));
    }

    [Fact]
    public void RemoveAll_RemovesMatchingItems()
    {
        var queue = new BatchPasteQueueController();
        var a = Entry("A");
        var b = Entry("B");
        var c = Entry("A");
        queue.Enqueue([a, b, c], BatchPasteQueueMode.Fifo);

        var removed = queue.RemoveAll(entry => entry.TextContent == "A");

        Assert.Equal(2, removed);
        Assert.Single(queue.Items);
        Assert.Same(b, queue.Head);
    }

    private static ClipboardEntry Entry(string text) =>
        new()
        {
            Type = EntryType.Text,
            TextContent = text,
            CopiedAt = DateTime.UnixEpoch
        };
}
