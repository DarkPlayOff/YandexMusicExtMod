namespace YandexMusicPatcherGui.Services;

public interface IPlatformService
{
    string GetModPath();
    
    string GetAsarPath();

    Task DownloadLatestMusic(string tempFolder);
    
    Task<(bool, string)> IsSupported();
    Task InstallMod(string archivePath, string tempFolder);
    
    Task InstallModUnpacked(string archivePath, string tempFolder);

    Task FinishInstallation(string tempFolder);
    
    Task CreateDesktopShortcut(string linkName, string path);
    
    void RunApplication();

    string GetApplicationExecutablePath();
}