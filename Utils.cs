using System.Globalization;
using System.Net;
using Avalonia.Data.Converters;

namespace YandexMusicPatcherGui;
public class ProgressToWidthConverter : IValueConverter
{
    public static readonly ProgressToWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not (double progress and >= 0 and <= 100) || parameter is not double width) return 0.0;

        return progress / 100.0 * width;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
public static class Utils
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36";
    public static readonly HttpClient HttpClient;
    public static readonly HttpClient NoRedirectHttpClient;

    static Utils()
    {
        HttpClient = CreateHttpClient(true);
        NoRedirectHttpClient = CreateHttpClient(false);
    }

    private static HttpClient CreateHttpClient(bool allowAutoRedirect)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = allowAutoRedirect
        };
        
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        
        return client;
    }


}
