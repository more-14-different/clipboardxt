using WinForms = System.Windows.Forms;

namespace ClipboardManager;

internal static class UpdateProgressDialog
{
    public static Task RunAsync(string waitText, Func<Task> work)
    {
        var completion = new TaskCompletionSource();
        Exception? captured = null;
        using var form = new WinForms.Form
        {
            Text = "ClipboardX",
            FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
            ControlBox = false,
            Width = 380,
            Height = 110,
            StartPosition = WinForms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        var label = new WinForms.Label
        {
            Text = waitText,
            Dock = WinForms.DockStyle.Top,
            AutoSize = true,
            Padding = new WinForms.Padding(8, 12, 8, 4),
        };
        var progressBar = new WinForms.ProgressBar
        {
            Style = WinForms.ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 40,
            Dock = WinForms.DockStyle.Fill,
            Height = 22,
            Padding = new WinForms.Padding(8, 4, 8, 12),
        };
        form.Controls.Add(label);
        form.Controls.Add(progressBar);

        form.FormClosed += (_, _) =>
        {
            if (captured != null)
                completion.TrySetException(captured);
            else
                completion.TrySetResult();
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                await work().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                captured = exception;
            }
            finally
            {
                if (form.IsHandleCreated && !form.IsDisposed)
                    form.BeginInvoke(() => form.Close());
            }
        };

        form.ShowDialog();
        return completion.Task;
    }
}
