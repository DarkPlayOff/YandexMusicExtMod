using System.Diagnostics;
using System.Runtime.Versioning;

namespace YandexMusicPatcherGui.Services;

public class MacOsService : BasePlatformService
{
    private const string YandexMusicAppName = "Яндекс Музыка.app";
    private const string DmgName = "Yandex_Music.dmg";

    public override string GetModPath()
    {
        return Path.Combine("/Applications", YandexMusicAppName);
    }

    public override string GetAsarPath()
    {
        return Path.Combine(GetModPath(), "Contents", "Resources", "app.asar");
    }

    public override async Task DownloadLatestMusic(string tempFolder)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.dmg";
        var dmgPath = Path.Combine(tempFolder, DmgName);
        var mountPath = Path.Combine(tempFolder, "mount");
        
        Directory.CreateDirectory(mountPath);

        await Patcher.DownloadFileWithProgress(latestUrl, dmgPath, "Загрузка клиента");

        Patcher.ReportProgress(100, "Распаковка DMG...");
        var attachResult = await Patcher.RunProcess("hdiutil", $"attach -mountpoint \"{mountPath}\" \"{dmgPath}\"", "Монтирования DMG...");
        if (attachResult.ExitCode != 0)
        {
            throw new Exception($"Ошибка монтирования DMG: {attachResult.Error}");
        }

        var appPath = Path.Combine(mountPath, YandexMusicAppName);
        var targetAppPath = GetModPath();

        if (Directory.Exists(targetAppPath))
        {
            Directory.Delete(targetAppPath, true);
        }

        var copyResult = await Patcher.RunProcess("cp", $"-R \"{appPath}\" \"/Applications/\"", "Копирования приложения...");
        if (copyResult.ExitCode != 0)
        {
            throw new Exception($"Ошибка копирования приложения: {copyResult.Error}");
        }
        
        var detachResult = await Patcher.RunProcess("hdiutil", $"detach \"{mountPath}\"", "Размонтирование DMG...");
        if (detachResult.ExitCode != 0)
        {
            throw new Exception($"Ошибка размонтирования DMG: {detachResult.Error}");
        }

        Patcher.ReportProgress(100, "Распаковка завершена.");
    }
    
    public override async Task<(bool, string)> IsSupported()
    {
#if MACOS
        if (await IsSipEnabled())
        {
            return (false, "Отключите SIP для установки.");
        }
#endif
        return (true, string.Empty);
    }
    
    [SupportedOSPlatform("macos")]
    private static async Task<bool> IsSipEnabled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "csrutil",
                    Arguments = "status",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Contains("System Integrity Protection status: enabled.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error checking SIP status: {e.Message}");
            return true;
        }
    }

    public override Task CreateDesktopShortcut(string linkName, string path)
    {
        return Task.CompletedTask;
    }
    
    public override void RunApplication()
    {
        Process.Start("open", $"-a \"{YandexMusicAppName}\"");
    }
public override string GetApplicationExecutablePath()
{
    return Path.Combine(GetModPath(), YandexMusicAppName);
}

public override Task InstallModUnpacked(string archivePath, string tempFolder)
{
    return Task.CompletedTask;
}

    
}