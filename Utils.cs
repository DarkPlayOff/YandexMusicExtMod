using System.Diagnostics;
using System.Runtime.Versioning;
using WindowsShortcutFactory;

namespace YandexMusicPatcherGui;

public static class Utils
{
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