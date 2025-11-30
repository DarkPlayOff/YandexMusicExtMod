using System.Diagnostics;

namespace YandexMusicPatcherGui.Services;

public class PatcherService
{
    private const string YandexMusicProcessName = "Яндекс Музыка";
    private const string ShortcutName = "Яндекс Музыка";

    public async Task Patch(IProgress<(int, string)> progress)
    {
        await KillYandexMusicProcess();
        var tempFolder = Program.TempPath;
        await Patcher.DownloadLatestMusic();
        await Patcher.DownloadModifiedAsar();
        await Program.PlatformService.FinishInstallation(tempFolder);

        var latestVersion = await Update.GetLatestModVersion();
        if (latestVersion != null)
        {
            Update.SetInstalledVersion(latestVersion);
        }

        await CreateDesktopShortcut();
        
    }
    
    private static async Task CreateDesktopShortcut()
    {
        await Program.PlatformService.CreateDesktopShortcut(ShortcutName, Program.PlatformService.GetApplicationExecutablePath());
    }
    
    private static async Task KillYandexMusicProcess()
    {
        await Task.Run(() =>
        {
            foreach (var p in Process.GetProcessesByName(YandexMusicProcessName))
            {
                try
                {
                    p.Kill();
                }
                catch (Exception e)
                {
                    Trace.TraceWarning("Не удалось завершить процесс Яндекс Музыки: {0}", e.Message);
                }
            }
        });
    }
}