using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace YandexMusicPatcherGui;

public static class Patcher
{
    private const int MaxRetries = 5;

    private const string GithubUrl =
        "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest/download/app.asar.gz";

    private const int BufferSize = 81920;
    private const int ProgressUpdateThreshold = 51200;

    private static readonly HttpClient httpClient;

    static Patcher()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    public static event EventHandler<(int Progress, string Status)>? OnDownloadProgress;

    public static async Task<string?> GetLatestModVersion()
    {
        return await Update.GetLatestAppVersion();
    }

    public static bool IsModInstalled()
    {
        var targetPath = Program.ModPath;

        if (!Directory.Exists(targetPath))
            return false;

        var exePath = Path.Combine(targetPath, "Яндекс Музыка.exe");
        var asarPath = Path.Combine(targetPath, "resources", "app.asar");

        return File.Exists(exePath) && File.Exists(asarPath);
    }

    public static async Task DownloadLastestMusic(bool useLatest)
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(Program.ModPath);

        await Extract7ZaToFolder(tempFolder);

        var latestUrl = useLatest
            ? "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.exe"
            : "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music_x64_5.58.0.exe";

        if (string.IsNullOrEmpty(latestUrl))
        {
            throw new Exception("Не удалось получить URL последней версии клиента.");
        }

        var stableExePath = Path.Combine(tempFolder, "stable.exe");
        await DownloadFileWithProgressAsync(latestUrl, stableExePath, "Загрузка клиента");

        ReportProgress(100, "Распаковка...");
        await ExtractArchiveAsync(stableExePath, Program.ModPath, tempFolder);
        ReportProgress(100, "Распаковка завершена");
    }

    public static async Task DownloadModifiedAsar(bool useLatest)
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        var downloadedGzFile = Path.Combine(tempFolder, "app.asar.gz");
        var resourcesPath = Path.GetFullPath(Path.Combine(Program.ModPath, "resources"));
        var asarPath = Path.Combine(resourcesPath, "app.asar");
        var oldAsarPath = Path.Combine(resourcesPath, "oldapp.asar");

        if (useLatest && File.Exists(asarPath))
        {
            if (File.Exists(oldAsarPath)) File.Delete(oldAsarPath);
            File.Move(asarPath, oldAsarPath);
        }

        Directory.CreateDirectory(resourcesPath);
        Directory.CreateDirectory(tempFolder);

        await DownloadFileWithProgressAsync(GithubUrl, downloadedGzFile, "Загрузка мода");

        await ExtractGzipAsync(downloadedGzFile, resourcesPath, tempFolder);

        if (useLatest)
        {
            var patcher = new HashPatcher();
            await patcher.PatchExecutable(Program.ModPath);
        }

        CleanupTempFiles();
    }

    private static async Task ExtractArchiveAsync(string archivePath, string outputPath, string tempFolder)
    {
        var sevenZipPath = await Extract7ZaToFolder(tempFolder);

        var processInfo = CreateProcessStartInfo(sevenZipPath,
            $"x \"{archivePath}\" -o\"{outputPath}\" -y");

        await RunProcessAsync(processInfo, "архива");
    }

    private static async Task ExtractGzipAsync(string gzipPath, string outputPath, string tempFolder)
    {
        var sevenZipPath = await Extract7ZaToFolder(tempFolder);

        var processInfo = CreateProcessStartInfo(sevenZipPath,
            $"x \"{gzipPath}\" -o\"{outputPath}\" -y");

        await RunProcessAsync(processInfo, "gzip архива");
    }

    private static async Task<string> Extract7ZaToFolder(string folder)
    {
        var extracted7zaPath = Path.Combine(folder, "7za.exe");

        if (File.Exists(extracted7zaPath))
            return extracted7zaPath;

        var assembly = Assembly.GetExecutingAssembly();

        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("7za.exe", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(resourceName))
            throw new FileNotFoundException("Не удалось найти ресурс 7za.exe в сборке");

        await using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new InvalidOperationException("Не удалось получить поток для ресурса 7za.exe");

            Directory.CreateDirectory(folder);

            await using var fileStream = new FileStream(extracted7zaPath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);
        }


        return extracted7zaPath;
    }

    private static ProcessStartInfo CreateProcessStartInfo(string executablePath, string arguments)
    {
        var directoryPath = Path.GetDirectoryName(executablePath);

        return new ProcessStartInfo(executablePath)
        {
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directoryPath ?? AppContext.BaseDirectory ?? string.Empty
        };
    }

    private static async Task RunProcessAsync(ProcessStartInfo processInfo, string operationType)
    {
        using var process = Process.Start(processInfo);
        if (process == null)
            throw new InvalidOperationException($"Не удалось запустить процесс {processInfo.FileName}");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            if (string.IsNullOrEmpty(error))
                error = await process.StandardOutput.ReadToEndAsync();

            throw new Exception($"Ошибка распаковки {operationType}. Код выхода: {process.ExitCode}. {error}");
        }
    }

    private static void CleanupDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    public static void CleanupTempFiles()
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        CleanupDirectory(tempFolder);

        var oldAsarPath = Path.Combine(Program.ModPath, "resources", "oldapp.asar");
        if (File.Exists(oldAsarPath))
        {
            File.Delete(oldAsarPath);
        }
    }

    public static async Task CleanInstall()
    {
        await Task.Run(() =>
        {
            var directory = new DirectoryInfo(Program.ModPath);
            if (!directory.Exists) return;

            foreach (var file in directory.GetFiles())
            {
                file.Delete();
            }

            foreach (var subDirectory in directory.GetDirectories())
            {
                subDirectory.Delete(true);
            }
        });
    }

    private static void ReportProgress(int progress, string status)
    {
        OnDownloadProgress?.Invoke("Patcher", (progress, status));
    }


    private static async Task DownloadFileWithProgressAsync(string url, string destinationPath,
        string progressStatusPrefix)
    {
        var retryCount = 0;
        long resumePosition = 0;
        var downloadComplete = false;

        while (!downloadComplete && retryCount < MaxRetries)
            try
            {
                if (retryCount > 0)
                {
                    if (retryCount == 1 || retryCount == MaxRetries - 1)
                        ReportProgress(0, $"Повторная попытка загрузки {retryCount}...");

                    var delay = Math.Min(30, Math.Pow(2, retryCount - 1));
                    await Task.Delay(TimeSpan.FromSeconds(delay));

                    if (File.Exists(destinationPath))
                        resumePosition = new FileInfo(destinationPath).Length;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (resumePosition > 0)
                    request.Headers.Range = new RangeHeaderValue(resumePosition, null);

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    downloadComplete = true;
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                var readBytes = resumePosition;
                var isResume = resumePosition > 0 && response.StatusCode == HttpStatusCode.PartialContent;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(
                    destinationPath,
                    isResume ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    true);

                var buffer = new byte[BufferSize];
                int bytesRead;
                long lastReportedBytes = 0;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    readBytes += bytesRead;
                    if (totalBytes.HasValue && readBytes - lastReportedBytes >= ProgressUpdateThreshold)
                    {
                        var progress = (int)(readBytes * 100 / (totalBytes.Value + resumePosition));
                        if (progress % 1 == 0 || progress == 99)
                            ReportProgress(progress, $"{progressStatusPrefix}: {progress}%");

                        lastReportedBytes = readBytes;
                    }
                }

                downloadComplete = true;
            }
            catch (Exception)
            {
                retryCount++;

                if (retryCount >= MaxRetries)
                {
                    ReportProgress(-1, "Ошибка загрузки");
                    throw;
                }
            }
    }
}