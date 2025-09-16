using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;

namespace YandexMusicPatcherGui;

public static class Patcher
{
    private const int MaxRetries = 3;
    private const string GithubUrl = "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest/download/app.asar.gz";
    private const int BufferSize = 81920;

    private const string YandexMusicAppName = "Яндекс Музыка.app";
    private const string YandexMusicExeName = "Яндекс Музыка.exe";

    private static readonly HttpClient HttpClient = Utils.HttpClient;

    public static event EventHandler<(int Progress, string Status)>? OnDownloadProgress;

    public static bool IsModInstalled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Directory.Exists(Path.Combine("/Applications", YandexMusicAppName));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var targetPath = Program.ModPath;
            var exePath = Path.Combine(targetPath, YandexMusicExeName);
            var asarPath = Path.Combine(targetPath, "resources", "app.asar");
            return Directory.Exists(targetPath) && File.Exists(exePath) && File.Exists(asarPath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Directory.Exists("/opt/Яндекс Музыка");
        }

        return false;
    }

    public static async Task DownloadLatestMusic(CancellationToken cancellationToken = default)
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(Program.ModPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await DownloadLatestMusicWindows(tempFolder, cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await DownloadLatestMusicMac(tempFolder, cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await DownloadLatestMusicLinux(tempFolder, cancellationToken);
        }
    }

    private static async Task DownloadLatestMusicWindows(string tempFolder, CancellationToken cancellationToken)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.exe";
        var stableExePath = Path.Combine(tempFolder, "stable.exe");
        await DownloadFileWithProgress(latestUrl, stableExePath, "Загрузка клиента", cancellationToken);

        ReportProgress(100, "Распаковка...");
        await ExtractArchive(stableExePath, Program.ModPath, tempFolder, "распаковки архива");
        ReportProgress(100, "Распаковка завершена");
    }

    private static async Task DownloadLatestMusicMac(string tempFolder, CancellationToken cancellationToken)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.dmg";
        var dmgPath = Path.Combine(tempFolder, "Yandex_Music.dmg");
        var mountPath = Path.Combine(tempFolder, "mount");
        
        Directory.CreateDirectory(mountPath);

        await DownloadFileWithProgress(latestUrl, dmgPath, "Загрузка клиента", cancellationToken);

        ReportProgress(100, "Распаковка DMG...");
        await RunProcess("hdiutil", $"attach -mountpoint \"{mountPath}\" \"{dmgPath}\"", "Монтирования DMG", cancellationToken: cancellationToken);

        var appPath = Path.Combine(mountPath, YandexMusicAppName);
        var targetAppPath = Path.Combine("/Applications", YandexMusicAppName);

        if (Directory.Exists(targetAppPath))
        {
            Directory.Delete(targetAppPath, true);
        }

        await RunProcess("cp", $"-R \"{appPath}\" \"/Applications/\"", "Копирования приложения", cancellationToken: cancellationToken);
        await RunProcess("hdiutil", $"detach \"{mountPath}\"", "Размонтирование DMG", cancellationToken: cancellationToken);

        ReportProgress(100, "Распаковка завершена");
    }

    private static async Task DownloadLatestMusicLinux(string tempFolder, CancellationToken cancellationToken)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.deb";
        var debPath = Path.Combine(tempFolder, "Yandex_Music.deb");
        
        await DownloadFileWithProgress(latestUrl, debPath, "Загрузка клиента", cancellationToken);
        
        ReportProgress(50, "Установка .deb пакета...");
        var installCommand = $"apt install -y ./{Path.GetFileName(debPath)}";
        await RunProcess("/bin/bash", $"-c \"{installCommand.Replace("\"", "\\\"")}\"", "Установка .deb пакета", tempFolder, cancellationToken);
        ReportProgress(100, "Пакет установлен");
    }

    private static async Task<string> Ensure7ZipExists(string tempFolder)
    {
        var sevenZipPath = Path.Combine(tempFolder, "7za.exe");
        if (File.Exists(sevenZipPath))
        {
            return sevenZipPath;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("7za.exe", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(resourceName))
        {
            throw new FileNotFoundException("Не удалось найти ресурс 7za.exe в сборке.");
        }

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException("Не удалось получить поток для ресурса 7za.exe.");
        }

        Directory.CreateDirectory(tempFolder);
        await using var fileStream = new FileStream(sevenZipPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream).ConfigureAwait(false);

        return sevenZipPath;
    }

    public static async Task DownloadModifiedAsar(CancellationToken cancellationToken = default)
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        var downloadedGzFile = Path.Combine(tempFolder, "app.asar.gz");

        await DownloadFileWithProgress(GithubUrl, downloadedGzFile, "Загрузка мода", cancellationToken);
        
        var asarPath = GetAsarPath();
        var tempAsarPath = Path.Combine(tempFolder, "app.asar");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var gunzipCommand = $"gunzip -c \"{downloadedGzFile}\" > \"{tempAsarPath}\"";
            await RunProcess("/bin/bash", $"-c \"{gunzipCommand.Replace("\"", "\\\"")}\"", "распаковки asar.gz", cancellationToken: cancellationToken);

            ReportProgress(50, "Замена asar...");
            var moveCommand = $"mv -f \"{tempAsarPath}\" \"{asarPath}\"";
            await RunProcess("/bin/bash", $"-c \"{moveCommand.Replace("\"", "\\\"")}\"", "замены asar", cancellationToken: cancellationToken);
        }
        else
        {
            var resourcesPath = Path.GetDirectoryName(asarPath)!;
            var oldAsarPath = Path.Combine(resourcesPath, "oldapp.asar");

            if (File.Exists(asarPath))
            {
                File.Move(asarPath, oldAsarPath, true);
            }

            Directory.CreateDirectory(resourcesPath);
            await ExtractArchive(downloadedGzFile, resourcesPath, tempFolder, "распаковки gzip архива");
        }

        ReportProgress(100, "Готово!");

        await BypassAsarIntegrity();
    }
    
    private static string GetAsarPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine("/Applications", YandexMusicAppName, "Contents", "Resources", "app.asar");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "/opt/Яндекс Музыка/resources/app.asar";
        }
        return Path.Combine(Program.ModPath, "resources", "app.asar");
    }

    private static async Task BypassAsarIntegrity()
    {
        var patcher = AsarIntegrity.CreatePatcher();
        ReportProgress(80, "Обход проверки целостности asar...");
        var success = await patcher.BypassAsarIntegrity();
        ReportProgress(success ? 100 : -1, success ? "Проверка целостности asar успешно обойдена" : "Ошибка обхода проверки целостности asar");
    }

    private static async Task ExtractArchive(string archivePath, string outputPath, string tempFolder, string operationType)
    {
        var sevenZipPath = await Ensure7ZipExists(tempFolder);
        await RunProcess(sevenZipPath, $"x \"{archivePath}\" -o\"{outputPath}\" -y", operationType);
    }
    
    private static ProcessStartInfo CreateProcessStartInfo(string executable, string arguments, string? workingDirectory = null)
    {
        return new ProcessStartInfo(executable)
        {
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable) ?? string.Empty
        };
    }
    
    private static async Task RunProcess(string executable, string arguments, string operationType, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var processInfo = CreateProcessStartInfo(executable, arguments, workingDirectory);
        using var process = Process.Start(processInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Не удалось запустить процесс {processInfo.FileName}");
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(error))
            {
                error = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
            throw new Exception($"Ошибка выполнения операции '{operationType}'. Код выхода: {process.ExitCode}. {error}");
        }
    }

    public static void CleanupTempFiles()
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        if (Directory.Exists(tempFolder))
        {
            Directory.Delete(tempFolder, true);
        }

        var oldAsarPath = Path.Combine(Program.ModPath, "resources", "oldapp.asar");
        if (File.Exists(oldAsarPath))
        {
            File.Delete(oldAsarPath);
        }
    }

    public static void CleanInstall()
    {
        if (Directory.Exists(Program.ModPath))
        {
            Directory.Delete(Program.ModPath, true);
        }
    }

    private static void ReportProgress(int progress, string status)
    {
        OnDownloadProgress?.Invoke("Patcher", (progress, status));
    }

    private static async Task DownloadFileWithProgress(string url, string destinationPath, string progressStatusPrefix, CancellationToken cancellationToken = default)
    {
        for (var retryCount = 0; retryCount < MaxRetries; retryCount++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long resumePosition = 0;
            if (File.Exists(destinationPath))
            {
                resumePosition = new FileInfo(destinationPath).Length;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (resumePosition > 0)
            {
                request.Headers.Range = new RangeHeaderValue(resumePosition, null);
            }

            try
            {
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    return;
                }

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var readBytes = resumePosition;
                var isResume = resumePosition > 0 && response.StatusCode == HttpStatusCode.PartialContent;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = new FileStream(destinationPath, isResume ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                        readBytes += bytesRead;
                        if (totalBytes > 0)
                        {
                            var progress = (int)((readBytes * 100) / (totalBytes + resumePosition));
                            ReportProgress(progress, $"{progressStatusPrefix}: {progress}%");
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                if (retryCount >= MaxRetries - 1)
                {
                    ReportProgress(-1, "Ошибка загрузки");
                    throw;
                }
                ReportProgress(0, $"Повторная попытка загрузки ({retryCount + 1}/{MaxRetries})...");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
            }
        }
    }
}