using System.Reflection;
using Avalonia;

namespace YandexMusicPatcherGui;

internal static class Program
{
    public static readonly string ModPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs",
            "YandexMusic");

    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<AppMain>()
            .UsePlatformDetect();
    }
}