namespace YandexMusicPatcherGui
{
    public static class Update
    {
        private static readonly HttpClient httpClient;

        static Update()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            httpClient = new HttpClient(handler);
        }

        public static async Task<string?> GetLastVersion()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest");
                var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                
                return response.Headers.Location?.ToString().Split('/').LastOrDefault()?.Replace("v", "");
            }
            catch
            {
                throw;
            }
        }
    }
}