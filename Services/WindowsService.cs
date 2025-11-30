using System.Diagnostics;
using System.Runtime.Versioning;
using WindowsShortcutFactory;

namespace YandexMusicPatcherGui.Services;

public class WindowsService : BasePlatformService
{
    private const string YandexMusicAppName = "YandexMusic";
    private const string YandexMusicExeName = "Яндекс Музыка.exe";
    private const string ResourcesDirName = "resources";
    private const string AppAsarName = "app.asar";
    private const string AppAsarUnpackedName = "app.asar.unpacked";
    private const string ProgramsDirName = "Programs";

    public override string GetModPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProgramsDirName, YandexMusicAppName);
    }

    public override string GetAsarPath()
    {
        return Path.Combine(GetResourcesPath(), AppAsarName);
    }

    public override async Task DownloadLatestMusic(string tempFolder)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.exe";
        var stableExePath = Path.Combine(tempFolder, "stable.exe");
        await Patcher.DownloadFileWithProgress(latestUrl, stableExePath, "Загрузка клиента");

        Patcher.ReportProgress(100, "Распаковка...");
        await Patcher.ExtractArchive(stableExePath, GetModPath(), tempFolder, "распаковки архива");
        Patcher.ReportProgress(100, "Распаковка завершена");
    }

    public override Task<(bool, string)> IsSupported()
    {
        return Task.FromResult((true, string.Empty));
    }

    public override Task CreateDesktopShortcut(string linkName, string path)
    {
#if WINDOWS
        return Task.Run(() => CreateShortcut(linkName, path));
#else
        return Task.CompletedTask;
#endif
    }

    [SupportedOSPlatform("windows")]
    private static void CreateShortcut(string linkName, string path)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktopPath, $"{linkName}.lnk");
        var targetDirectory = Path.GetDirectoryName(path) ?? "";

        using var shortcut = new WindowsShortcut
        {
            Path = path,
            WorkingDirectory = targetDirectory
        };
        shortcut.Save(shortcutPath);
    }

    public override void RunApplication()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = GetApplicationExecutablePath(),
            UseShellExecute = true
        });
    }

    public override string GetApplicationExecutablePath()
    {
        return Path.Combine(GetModPath(), YandexMusicExeName);
    }
    public override Task InstallMod(string archivePath, string tempFolder)
    {
        return Patcher.ExtractArchive(archivePath, GetResourcesPath(), tempFolder, "распаковки asar.zst");
    }

    public override Task InstallModUnpacked(string archivePath, string tempFolder)
    {
        var unpackedAsarPath = Path.Combine(GetResourcesPath(), AppAsarUnpackedName);

        if (Directory.Exists(unpackedAsarPath))
        {
            Directory.Delete(unpackedAsarPath, true);
        }

        return Patcher.ExtractArchive(archivePath, unpackedAsarPath, tempFolder, "распаковки app.asar.unpacked");
    }

    private string GetResourcesPath()
    {
        return Path.Combine(GetModPath(), ResourcesDirName);
    }
}