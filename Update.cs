using System.Text.RegularExpressions;

namespace YandexMusicPatcherGui;

public static class Update
{
    private static readonly HttpClient httpClient = Utils.HttpClient;


    public static async Task<string?> GetLatestModVersion()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest");
        var response = await client.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Redirect || response.StatusCode == System.Net.HttpStatusCode.Found)
        {
            var versionString = response.Headers.Location?.ToString().Split('/').LastOrDefault();
            return versionString != null ? Regex.Replace(versionString, "[^0-9.]", "") : null;
        }

        return null;
    }

    public static async Task<string?> GetLatestAppVersion()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler);
        
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{Program.RepoUrl}/releases/latest");
        var response = await client.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Redirect || response.StatusCode == System.Net.HttpStatusCode.Found)
        {
            return response.Headers.Location?.ToString().Split('/').LastOrDefault()?.Replace("v", "");
        }

        return null;
    }
}