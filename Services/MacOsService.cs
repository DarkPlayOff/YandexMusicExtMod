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

    public override async Task DownloadLatestMusic(string tempFolder, CancellationToken cancellationToken)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.dmg";
        var dmgPath = Path.Combine(tempFolder, DmgName);
        var mountPath = Path.Combine(tempFolder, "mount");
        
        Directory.CreateDirectory(mountPath);

        await Patcher.DownloadFileWithProgress(latestUrl, dmgPath, "Загрузка клиента", cancellationToken);

        Patcher.ReportProgress(100, "Распаковка DMG...");
        await Patcher.RunProcess("hdiutil", $"attach -mountpoint \"{mountPath}\" \"{dmgPath}\"", "Монтирования DMG", cancellationToken: cancellationToken);

        var appPath = Path.Combine(mountPath, YandexMusicAppName);
        var targetAppPath = GetModPath();

        if (Directory.Exists(targetAppPath))
        {
            Directory.Delete(targetAppPath, true);
        }

        await Patcher.RunProcess("cp", $"-R \"{appPath}\" \"/Applications/\"", "Копирования приложения", cancellationToken: cancellationToken);
        await Patcher.RunProcess("hdiutil", $"detach \"{mountPath}\"", "Размонтирование DMG", cancellationToken: cancellationToken);

        Patcher.ReportProgress(100, "Распаковка завершена");
    }
    
    public override Task<(bool, string)> IsSupported()
    {
#if MACOS
        if (IsSipEnabled())
        {
            return Task.FromResult((false, "Отключите SIP для продолжения"));
        }
#endif
        return Task.FromResult((true, string.Empty));
    }
    
    [SupportedOSPlatform("macos")]
    private static bool IsSipEnabled()
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
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
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

    public override string GetPatchMessage()
    {
        return "Клиент установлен в папку программ!";
    }

    public override string GetApplicationExecutablePath()
    {
        return Path.Combine(GetModPath(), YandexMusicAppName);
    }
}