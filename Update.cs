using System.Net;

namespace YandexMusicPatcherGui;

public static class Update
{
    private static readonly string VersionFilePath = Path.Combine(Program.ModPath, "version.txt");

    public static Task<Version?> GetLatestAppVersion()
    {
        return GetLatestVersionFromRepo(Program.RepoUrl);
    }

    public static Version? GetInstalledVersion()
    {
        if (!File.Exists(VersionFilePath))
        {
            return null;
        }

        var versionString = File.ReadAllText(VersionFilePath);
        Version.TryParse(versionString, out var version);
        return version;
    }

    public static void SetInstalledVersion(Version version)
    {
        Directory.CreateDirectory(Program.ModPath);
        File.WriteAllText(VersionFilePath, version.ToString());
    }

    public static Task<Version?> GetLatestModVersion()
    {
        return GetLatestVersionFromRepo("https://github.com/DarkPlayOff/YandexMusicAsar");
    }

    public static async Task<Version?> GetLatestVersionFromRepo(string repoUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{repoUrl}/releases/latest");
        var response = await Utils.NoRedirectHttpClient.SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found))
        {
            return null;
        }

        var location = response.Headers.Location?.ToString();
        if (location == null)
        {
            return null;
        }

        var versionString = location.Split('/').LastOrDefault();

        return versionString != null && Version.TryParse(versionString.Replace("v", ""), out var version) ? version : null;
    }
}