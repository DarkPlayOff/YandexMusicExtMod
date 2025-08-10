using System.Text.RegularExpressions;

namespace YandexMusicPatcherGui;

public static class Update
{
    private static readonly HttpClient httpClient = Utils.HttpClient;


    public static async Task<string?> GetLatestModVersion()
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest");
        var response = await httpClient.SendAsync(request);

        var versionString = response.Headers.Location?.ToString().Split('/').LastOrDefault();
        return versionString != null ? Regex.Replace(versionString, "[^0-9.]", "") : null;
    }

    public static async Task<string?> GetLatestAppVersion()
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{Program.RepoUrl}/releases/latest");
        var response = await httpClient.SendAsync(request);

        return response.Headers.Location?.ToString().Split('/').LastOrDefault()?.Replace("v", "");
    }
}