using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using ZstdSharp;

namespace YandexMusicPatcherGui;
public static class Patcher
{
    private const int MaxRetries = 3;
    private const string GithubUrl = "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest/download/app.asar.zst";
    private const string AsarUnpackedUrl = "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest/download/app.asar.unpacked.zip";
    private const int BufferSize = 81920;

    private static readonly HttpClient HttpClient = Utils.HttpClient;

    public static event EventHandler<(int Progress, string Status)>? OnDownloadProgress;

    public static bool IsModInstalled()
    {
        var targetPath = Program.ModPath;
        var asarPath = Program.PlatformService.GetAsarPath();
        
        return Directory.Exists(targetPath) && File.Exists(asarPath);
    }
    
    public static async Task DownloadLatestMusic(CancellationToken cancellationToken = default)
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(Program.ModPath);

        await Program.PlatformService.DownloadLatestMusic(tempFolder, cancellationToken);
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
        var downloadedGzFile = Path.Combine(tempFolder, "app.asar.zst");

        await DownloadFileWithProgress(GithubUrl, downloadedGzFile, "Загрузка мода", cancellationToken);
        
        var asarPath = Program.PlatformService.GetAsarPath();
        var resourcesPath = Path.GetDirectoryName(asarPath)!;
        var oldAsarPath = Path.Combine(resourcesPath, "oldapp.asar");

        if (File.Exists(asarPath))
        {
            File.Move(asarPath, oldAsarPath, true);
        }

        Directory.CreateDirectory(resourcesPath);
        
        ReportProgress(50, "Распаковка asar...");

        await Program.PlatformService.InstallMod(downloadedGzFile, tempFolder, cancellationToken);
        
        ReportProgress(100, "Готово!");

        await BypassAsarIntegrity();
        
        await DownloadAndUnpackUnpackedAsar(cancellationToken);
    }
    
    private static async Task DownloadAndUnpackUnpackedAsar(CancellationToken cancellationToken)
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        var unpackedAsarArchive = Path.Combine(tempFolder, "app.asar.unpacked.zip");
        
        await DownloadFileWithProgress(AsarUnpackedUrl, unpackedAsarArchive, "Загрузка app.asar.unpacked", cancellationToken);
        
        var asarPath = Program.PlatformService.GetAsarPath();
        var resourcesPath = Path.GetDirectoryName(asarPath)!;
        var unpackedAsarPath = Path.Combine(resourcesPath, "app.asar.unpacked");

        if (Directory.Exists(unpackedAsarPath))
        {
            Directory.Delete(unpackedAsarPath, true);
        }

        ReportProgress(100, "Распаковка app.asar.unpacked...");
        await Program.PlatformService.InstallModUnpacked(unpackedAsarArchive, tempFolder, cancellationToken);
        ReportProgress(100, "Распаковка app.asar.unpacked завершена");
    }


    private static async Task BypassAsarIntegrity()
    {
        var patcher = AsarIntegrity.CreatePatcher();
        ReportProgress(80, "Обход проверки целостности asar...");
        var success = await patcher.BypassAsarIntegrity();
        if (!success)
        {
            const string errorMessage = "Ошибка обхода проверки целостности asar";
            ReportProgress(-1, errorMessage);
            throw new Exception(errorMessage);
        }
        ReportProgress(100, "Проверка целостности asar успешно обойдена");
    }

    
    public static async Task ExtractArchive(string archivePath, string outputPath, string tempFolder, string operationType)
    {
        var sevenZipPath = await Ensure7ZipExists(tempFolder);
        await RunProcess(sevenZipPath, $"x \"{archivePath}\" -o\"{outputPath}\" -y", operationType);
    }
    
    public static async Task RunProcess(string executable, string arguments, string operationType, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var processInfo = new ProcessStartInfo(executable)
        {
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable) ?? string.Empty
        };
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

    public static void ReportProgress(int progress, string status)
    {
        OnDownloadProgress?.Invoke("Patcher", (progress, status));
    }

    public static async Task DownloadFileWithProgress(string url, string destinationPath, string progressStatusPrefix, CancellationToken cancellationToken = default)
    {
        for (var retryCount = 0; retryCount < MaxRetries; retryCount++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await PerformDownloadAsync(url, destinationPath, progressStatusPrefix, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                if (retryCount >= MaxRetries - 1)
                {
                    ReportProgress(-1, "Ошибка загрузки");
                    throw;
                }
                
                var message = ex is HttpRequestException or IOException ? "Ошибка сети/записи" : "Неизвестная ошибка";
                ReportProgress(0, $"{message}. Повтор ({retryCount + 1}/{MaxRetries})...");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
            }
        }
    }

    private static async Task PerformDownloadAsync(string url, string destinationPath, string progressStatusPrefix, CancellationToken cancellationToken)
    {
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
    }
}