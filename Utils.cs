using System.Runtime.Versioning;
using WindowsShortcutFactory;

namespace YandexMusicPatcherGui
{
    public static class Utils
    {
        [SupportedOSPlatform("windows")]
        public static void CreateDesktopShortcut(string linkName, string path)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopPath, $"{linkName}.lnk");
            string targetDirectory = Path.GetDirectoryName(path) ?? "";

            using var shortcut = new WindowsShortcut
            {
                Path = path,
                WorkingDirectory = targetDirectory
            };
            shortcut.Save(shortcutPath);
        }
    }
}