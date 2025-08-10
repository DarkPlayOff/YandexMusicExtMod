using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using WindowsShortcutFactory;

namespace YandexMusicPatcherGui;

public static class Utils
{
    public static readonly HttpClient HttpClient;

    static Utils()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = true
        };

        HttpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
    }

    [SupportedOSPlatform("windows")]
    public static void CreateDesktopShortcut(string linkName, string path)
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

    [SupportedOSPlatform("macos")]
    public static bool IsSipEnabled()
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
}