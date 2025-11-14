using System.Reflection;
using Avalonia;
using YandexMusicPatcherGui.Services;

namespace YandexMusicPatcherGui;

internal static class Program
{
    public static readonly string RepoUrl = "https://github.com/DarkPlayOff/YandexMusicExtMod";
    
    public static readonly IPlatformService PlatformService = PlatformServiceFactory.Create();

    public static readonly string ModPath = PlatformService.GetModPath();
    public static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
    
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<AppMain>()
            .UsePlatformDetect();
    }
}