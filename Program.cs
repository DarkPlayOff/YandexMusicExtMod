using System.Reflection;
using Avalonia;

namespace YandexMusicPatcherGui;

internal static class Program
{
    public static readonly string RepoUrl = "https://github.com/DarkPlayOff/YandexMusicExtMod";

    public static readonly string ModPath = GetModPath();
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    
    private static string GetModPath()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "YandexMusic");
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share", "YandexMusicPatcherGui");
        }
    }

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