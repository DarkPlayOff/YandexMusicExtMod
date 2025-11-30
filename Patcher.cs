using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;

namespace YandexMusicPatcherGui;

public sealed record ProcessResult(int ExitCode, string Output, string Error);

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
        return Update.GetInstalledVersion() != null;
    }
    
    public static async Task DownloadLatestMusic()
    {
        var tempFolder = Program.TempPath;
        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(Program.ModPath);

        await Program.PlatformService.DownloadLatestMusic(tempFolder);
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
        await stream.CopyToAsync(fileStream);

        return sevenZipPath;
    }

    public static async Task DownloadModifiedAsar()
    {
        var tempFolder = Program.TempPath;
        var downloadedGzFile = Path.Combine(tempFolder, "app.asar.zst");

        await DownloadFileWithProgress(GithubUrl, downloadedGzFile, "Загрузка мода");
        
        var asarPath = Program.PlatformService.GetAsarPath();
        var resourcesPath = Path.GetDirectoryName(asarPath)!;
        var oldAsarPath = Path.Combine(resourcesPath, "oldapp.asar");

        if (File.Exists(asarPath))
        {
            File.Move(asarPath, oldAsarPath, true);
        }

        Directory.CreateDirectory(resourcesPath);
        
        ReportProgress(50, "Распаковка asar...");

        await Program.PlatformService.InstallMod(downloadedGzFile, tempFolder);
        
        ReportProgress(100, "Готово!");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await BypassAsarIntegrity();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await DownloadAndUnpackUnpackedAsar();
        }
    }
    
    private static async Task DownloadAndUnpackUnpackedAsar()
    {
        var tempFolder = Program.TempPath;
        var unpackedAsarArchive = Path.Combine(tempFolder, "app.asar.unpacked.zip");
        
        await DownloadFileWithProgress(AsarUnpackedUrl, unpackedAsarArchive, "Загрузка app.asar.unpacked");
        
        var asarPath = Program.PlatformService.GetAsarPath();
        var resourcesPath = Path.GetDirectoryName(asarPath)!;
        var unpackedAsarPath = Path.Combine(resourcesPath, "app.asar.unpacked");

        if (Directory.Exists(unpackedAsarPath))
        {
            Directory.Delete(unpackedAsarPath, true);
        }

        ReportProgress(100, "Распаковка app.asar.unpacked...");
        await Program.PlatformService.InstallModUnpacked(unpackedAsarArchive, tempFolder);
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
        var result = await RunProcess(sevenZipPath, $"x \"{archivePath}\" -o\"{outputPath}\" -y", operationType);
        if (result.ExitCode != 0)
        {
            throw new Exception($"Ошибка выполнения операции '{operationType}'. Код выхода: {result.ExitCode}. {result.Error}");
        }
    }
    
    public static async Task<ProcessResult> RunProcess(string executable, string arguments, string operationType, string? workingDirectory = null)
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

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return new ProcessResult(process.ExitCode, output, error);
    }

    public static void CleanupTempFiles()
    {
        var tempFolder = Program.TempPath;
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
                await PerformDownload(url, destinationPath, progressStatusPrefix, cancellationToken);
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

    private static async Task PerformDownload(string url, string destinationPath, string progressStatusPrefix, CancellationToken cancellationToken)
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
        
        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            return;
        }

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var readBytes = resumePosition;
        var isResume = resumePosition > 0 && response.StatusCode == HttpStatusCode.PartialContent;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, isResume ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
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