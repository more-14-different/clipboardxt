using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow
{
    private Task<ExternalTextSinkClient.Lease?>? _externalTextSinkLeaseTask;

    private void BeginExternalTextSinkLease()
    {
        EndExternalTextSinkLease();
        _externalTextSinkLeaseTask = BeginExternalTextSinkLeaseCoreAsync();
    }

    private static async Task<ExternalTextSinkClient.Lease?> BeginExternalTextSinkLeaseCoreAsync()
    {
        var lease = await ExternalTextSinkClient.TryBeginOverlayAsync("ClipboardX");
        ClipboardDiagnosticsLog.Write(lease is { } accepted
            ? $"externalTextSink begin accepted session={accepted.LauncherSessionId} lease={accepted.LeaseId}"
            : "externalTextSink begin unavailable; normal paste fallback remains active");
        return lease;
    }

    private async Task<bool> TryDeliverTextToExternalSinkAsync(ClipboardEntry item)
    {
        if (item.Type != EntryType.Text || string.IsNullOrEmpty(item.TextContent))
            return false;

        var leaseTask = _externalTextSinkLeaseTask;
        var lease = leaseTask == null ? null : await leaseTask;
        if (lease == null)
        {
            lease = await BeginExternalTextSinkLeaseCoreAsync();
            if (lease == null)
                return false;
            _externalTextSinkLeaseTask = Task.FromResult<ExternalTextSinkClient.Lease?>(lease);
        }

        var accepted = await ExternalTextSinkClient.TryInsertTextAsync(lease.Value, item.TextContent);
        ClipboardDiagnosticsLog.Write(
            $"externalTextSink insert accepted={accepted} session={lease.Value.LauncherSessionId} len={item.TextContent.Length}");
        return accepted;
    }

    private void EndExternalTextSinkLease()
    {
        var leaseTask = Interlocked.Exchange(ref _externalTextSinkLeaseTask, null);
        if (leaseTask != null)
            _ = EndExternalTextSinkLeaseCoreAsync(leaseTask);
    }

    private static async Task EndExternalTextSinkLeaseCoreAsync(
        Task<ExternalTextSinkClient.Lease?> leaseTask)
    {
        try
        {
            var lease = await leaseTask;
            if (lease is not { } accepted)
                return;
            await ExternalTextSinkClient.EndOverlayAsync(accepted);
            ClipboardDiagnosticsLog.Write(
                $"externalTextSink end session={accepted.LauncherSessionId} lease={accepted.LeaseId}");
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write(
                $"externalTextSink end failed {ex.GetType().Name}: {ex.Message}");
        }
    }
}
