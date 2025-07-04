using System.Text.RegularExpressions;

namespace YandexMusicPatcherGui;

public static class VersionManager
{
    private static readonly string VersionFilePath = Path.Combine(Program.ModPath, "version.txt");

    public static string GetInstalledVersion()
    {
        if (File.Exists(VersionFilePath))
        {
            var versionString = File.ReadAllText(VersionFilePath);
            return Regex.Replace(versionString, "[^0-9.]", "");
        }

        return "Не установлено";
    }

    public static void SetInstalledVersion(string version)
    {
        var cleanedVersion = Regex.Replace(version, "[^0-9.]", "");
        File.WriteAllText(VersionFilePath, cleanedVersion);
    }

    public static async Task<string?> GetLatestModVersion()
    {
        return await Update.GetLatestModVersion();
    }
}