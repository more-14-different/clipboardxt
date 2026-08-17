namespace ClipboardManager;

public partial class App
{
    private FolderFavoriteCommandServer? _folderFavoriteCommandServer;
    private bool _folderFavoriteCommandOnly;

    private void ShutdownFolderFavoriteCommand(int exitCode)
    {
        _folderFavoriteCommandOnly = true;
        Shutdown(exitCode);
    }

    private void StartFolderFavoriteCommandServer()
    {
        _folderFavoriteCommandServer = new FolderFavoriteCommandServer(path =>
            Dispatcher.Invoke(() =>
            {
                FolderFavoriteCommand.TryAdd(_settings, path, out var response);
                return response;
            }));
    }
}
