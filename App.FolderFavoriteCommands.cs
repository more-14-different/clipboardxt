namespace ClipboardManager;

public partial class App
{
    private FolderFavoriteCommandServer? _folderFavoriteCommandServer;

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
