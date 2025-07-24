using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using YandexMusicPatcher;

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
        return await Update.GetLatestAppVersion().ConfigureAwait(false);
    }

    public static bool IsModInstalled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Directory.Exists("/Applications/Яндекс Музыка.app");
        }
        
        var targetPath = Program.ModPath;

        if (!Directory.Exists(targetPath))
            return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var exePath = Path.Combine(targetPath, "Яндекс Музыка.exe");
            var asarPath = Path.Combine(targetPath, "resources", "app.asar");
            return File.Exists(exePath) && File.Exists(asarPath);
        }

        return false;
    }

    public static async Task DownloadLastestMusic(bool useLatest)
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(Program.ModPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var sevenZipPath = await Ensure7ZipExists(tempFolder).ConfigureAwait(false);
            var latestUrl = useLatest
                ? "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.exe"
                : "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music_x64_5.58.0.exe";

            if (string.IsNullOrEmpty(latestUrl))
            {
                throw new Exception("Не удалось получить URL последней версии клиента.");
            }

            var stableExePath = Path.Combine(tempFolder, "stable.exe");
            await DownloadFileWithProgressAsync(latestUrl, stableExePath, "Загрузка клиента").ConfigureAwait(false);

            ReportProgress(100, "Распаковка...");
            await ExtractArchiveAsync(stableExePath, Program.ModPath, tempFolder).ConfigureAwait(false);
            ReportProgress(100, "Распаковка завершена");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var dmgPath = Path.Combine(tempFolder, "Yandex_Music.dmg");
            var mountPath = Path.Combine(tempFolder, "mount");

            Directory.CreateDirectory(mountPath);

            var latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.dmg";
            await DownloadFileWithProgressAsync(latestUrl, dmgPath, "Загрузка клиента").ConfigureAwait(false);

            ReportProgress(100, "Распаковка DMG...");
            await RunProcessAsync(CreateProcessStartInfo("hdiutil", $"attach -mountpoint \"{mountPath}\" \"{dmgPath}\""), "монтирования DMG").ConfigureAwait(false);

            var appPath = Path.Combine(mountPath, "Яндекс Музыка.app");
            var targetAppPath = Path.Combine("/Applications", "Яндекс Музыка.app");

            if (Directory.Exists(targetAppPath))
            {
                Directory.Delete(targetAppPath, true);
            }
            
            await RunProcessAsync(CreateProcessStartInfo("cp", $"-R \"{appPath}\" \"/Applications/\""), "копирования приложения").ConfigureAwait(false);
            
            await RunProcessAsync(CreateProcessStartInfo("hdiutil", $"detach \"{mountPath}\""), "размонтирования DMG").ConfigureAwait(false);
            
            ReportProgress(100, "Распаковка завершена");
        }
    }

    

    private static async Task<string> Ensure7ZipExists(string tempFolder)
    {
        var sevenZipPath = Path.Combine(tempFolder, "7za.exe");

        if (File.Exists(sevenZipPath))
            return sevenZipPath;

        var assembly = Assembly.GetExecutingAssembly();

        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("7za.exe", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(resourceName))
            throw new FileNotFoundException("Не удалось найти ресурс 7za.exe в сборке");

        await using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new InvalidOperationException("Не удалось получить поток для ресурса 7za.exe");

            Directory.CreateDirectory(tempFolder);

            await using var fileStream = new FileStream(sevenZipPath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
        }

        return sevenZipPath;
    }

    


    public static async Task DownloadModifiedAsar(bool useLatest)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await DownloadModifiedAsarWindows(useLatest).ConfigureAwait(false);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await DownloadModifiedAsarMac(useLatest).ConfigureAwait(false);
        }
    }

    private static async Task DownloadModifiedAsarWindows(bool useLatest)
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

        await DownloadFileWithProgressAsync(GithubUrl, downloadedGzFile, "Загрузка мода").ConfigureAwait(false);

        await ExtractGzipAsync(downloadedGzFile, resourcesPath, tempFolder).ConfigureAwait(false);

        if (useLatest)
        {
            var patcher = new HashPatcher();
            await patcher.PatchExecutable(Program.ModPath).ConfigureAwait(false);
        }

        CleanupTempFiles();
    }

    private static async Task DownloadModifiedAsarMac(bool useLatest)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "ym-patcher");
        var downloadedGzFile = Path.Combine(tempFolder, "app.asar.gz");
        var appPath = Path.Combine("/Applications", "Яндекс Музыка.app");
        var resourcesPath = Path.Combine(appPath, "Contents", "Resources");
        var asarPath = Path.Combine(resourcesPath, "app.asar");
        var oldAsarPath = Path.Combine(resourcesPath, "app.asar.bak");

        if (File.Exists(asarPath))
        {
            if (File.Exists(oldAsarPath)) File.Delete(oldAsarPath);
            File.Move(asarPath, oldAsarPath);
        }

        Directory.CreateDirectory(resourcesPath);
        Directory.CreateDirectory(tempFolder);

        await DownloadFileWithProgressAsync(GithubUrl, downloadedGzFile, "Загрузка мода").ConfigureAwait(false);

        await ExtractGzipAsync(downloadedGzFile, resourcesPath, tempFolder).ConfigureAwait(false);

        var patcher = new YMPatcher();
        await patcher.Initialize().ConfigureAwait(false);
        await patcher.BypassAsarIntegrity(appPath, (progress, message, error) =>
        {
            if (error != null)
            {
                ReportProgress(-1, $"Ошибка: {message} - {error}");
            }
            else
            {
                ReportProgress((int)(progress * 100), message);
            }
        }).ConfigureAwait(false);

        CleanupTempFiles();
    }

    private static async Task ExtractArchiveAsync(string archivePath, string outputPath, string tempFolder)
    {
        var sevenZipPath = await Ensure7ZipExists(tempFolder).ConfigureAwait(false);

        var processInfo = CreateProcessStartInfo(sevenZipPath,
            $"x \"{archivePath}\" -o\"{outputPath}\" -y");

        await RunProcessAsync(processInfo, "архива").ConfigureAwait(false);
    }

    private static async Task ExtractGzipAsync(string gzipPath, string outputPath, string tempFolder)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var outputFilePath = Path.Combine(outputPath, "app.asar");
            var command = $"gunzip -c \"{gzipPath}\" > \"{outputFilePath}\"";
            var processInfo = CreateProcessStartInfo("/bin/bash", $"-c \"{command.Replace("\"", "\\\"")}\"") ;
            await RunProcessAsync(processInfo, "gzip архива").ConfigureAwait(false);
        }
        else
        {
            var sevenZipPath = await Ensure7ZipExists(tempFolder).ConfigureAwait(false);
            var processInfo = CreateProcessStartInfo(sevenZipPath,
                $"x \"{gzipPath}\" -o\"{outputPath}\" -y");
            await RunProcessAsync(processInfo, "gzip архива").ConfigureAwait(false);
        }
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

        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(error))
                error = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

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
        }).ConfigureAwait(false);
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
                    await Task.Delay(TimeSpan.FromSeconds(delay)).ConfigureAwait(false);

                    if (File.Exists(destinationPath))
                        resumePosition = new FileInfo(destinationPath).Length;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (resumePosition > 0)
                    request.Headers.Range = new RangeHeaderValue(resumePosition, null);

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    downloadComplete = true;
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                var readBytes = resumePosition;
                var isResume = resumePosition > 0 && response.StatusCode == HttpStatusCode.PartialContent;

                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
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

                while ((bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
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