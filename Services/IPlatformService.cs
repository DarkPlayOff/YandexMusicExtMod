namespace YandexMusicPatcherGui.Services;

public interface IPlatformService
{
    string GetModPath();
    
    string GetAsarPath();

    Task DownloadLatestMusic(string tempFolder, CancellationToken cancellationToken);
    
    Task<(bool, string)> IsSupported();
    Task InstallMod(string archivePath, string tempFolder, CancellationToken cancellationToken);
    
    Task InstallModUnpacked(string archivePath, string tempFolder, CancellationToken cancellationToken);
    
    Task CreateDesktopShortcut(string linkName, string path);
    
    void RunApplication();

    string GetPatchMessage();

    string GetApplicationExecutablePath();
}