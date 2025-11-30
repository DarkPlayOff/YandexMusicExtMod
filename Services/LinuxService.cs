namespace YandexMusicPatcherGui.Services;

public class LinuxService : BasePlatformService
{
    private const string AppInstallPath = "/opt/Яндекс Музыка";
    private const string AppExecutableName = "Яндекс Музыка";
    private const string DebName = "Yandex_Music.deb";
    
    public override string GetModPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "YandexMusicPatcherGui");
    }

    public override string GetAsarPath()
    {
        return Path.Combine(AppInstallPath, "resources", "app.asar");
    }

    public override async Task DownloadLatestMusic(string tempFolder)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.deb";
        var debPath = Path.Combine(tempFolder, DebName);
        
        await Patcher.DownloadFileWithProgress(latestUrl, debPath, "Загрузка клиента");
        
        Patcher.ReportProgress(50, "Установка .deb пакета...");
        var installCommand = $"apt-get install --allow-downgrades -y ./{Path.GetFileName(debPath)}";
        await Patcher.RunProcess("/bin/bash", $"-c \"{installCommand.Replace("\"", "\\\"")}\"", "Установка .deb пакета...", tempFolder);
        Patcher.ReportProgress(100, "Пакет установлен");
    }

    public override Task<(bool, string)> IsSupported()
    {
        if (Environment.UserName != "root")
        {
            return Task.FromResult((false, "Для установки требуются права root."));
        }
        return Task.FromResult((true, string.Empty));
    }

    public override Task CreateDesktopShortcut(string linkName, string path)
    {
        return Task.CompletedTask;
    }

    public override void RunApplication()
    {
        Patcher.RunProcess(GetApplicationExecutablePath(), "", "запуска приложения").GetAwaiter().GetResult();
    }
public override string GetApplicationExecutablePath()
{
    return Path.Combine(AppInstallPath, AppExecutableName);
}

public override Task InstallModUnpacked(string archivePath, string tempFolder)
{
    return Task.CompletedTask;
}
}