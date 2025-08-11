using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;

namespace YandexMusicPatcherGui;

public static class Patcher
{
    private const int MaxRetries = 5;

    private const string GithubUrl =
        "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest/download/app.asar.gz";

    private const int BufferSize = 81920;
    private const int ProgressUpdateThreshold = 51200;

    private const string YandexMusicAppName = "Яндекс Музыка.app";
    private const string YandexMusicExeName = "Яндекс Музыка.exe";

    private static readonly HttpClient httpClient = Utils.HttpClient;

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

    public static async Task DownloadLastestMusic()
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(Program.ModPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await DownloadLatestMusicWindows(tempFolder);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await DownloadLatestMusicMac(tempFolder);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await DownloadLatestMusicLinux(tempFolder);
        }
    }

    private static async Task DownloadLatestMusicWindows(string tempFolder)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.exe";

        var stableExePath = Path.Combine(tempFolder, "stable.exe");
        await DownloadFileWithProgress(latestUrl, stableExePath, "Загрузка клиента");

        ReportProgress(100, "Распаковка...");
        await ExtractArchive(stableExePath, Program.ModPath, tempFolder);
        ReportProgress(100, "Распаковка завершена");
    }

    private static async Task DownloadLatestMusicMac(string tempFolder)
    {
        var dmgPath = Path.Combine(tempFolder, "Yandex_Music.dmg");
        var mountPath = Path.Combine(tempFolder, "mount");
        Directory.CreateDirectory(mountPath);

        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.dmg";
        await DownloadFileWithProgress(latestUrl, dmgPath, "Загрузка клиента");

        ReportProgress(100, "Распаковка DMG...");
        await RunProcess(CreateProcessStartInfo("hdiutil", $"attach -mountpoint \"{mountPath}\" \"{dmgPath}\""),
            "Монтирования DMG");

        var appPath = Path.Combine(mountPath, YandexMusicAppName);
        var targetAppPath = Path.Combine("/Applications", YandexMusicAppName);

        if (Directory.Exists(targetAppPath)) Directory.Delete(targetAppPath, true);

        await RunProcess(CreateProcessStartInfo("cp", $"-R \"{appPath}\" \"/Applications/\""),
            "Копирования приложения");
        await RunProcess(CreateProcessStartInfo("hdiutil", $"detach \"{mountPath}\""), "Размонтирование DMG");

        ReportProgress(100, "Распаковка завершена");
    }

    private static async Task DownloadLatestMusicLinux(string tempFolder)
    {
        var debPath = Path.Combine(tempFolder, "Yandex_Music.deb");
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.deb";
        await DownloadFileWithProgress(latestUrl, debPath, "Загрузка клиента");
        
        ReportProgress(50, "Установка .deb пакета...");
        var installCommand = $"apt install -y ./{Path.GetFileName(debPath)}";
        var processInfo = CreateProcessStartInfo("/bin/bash", $"-c \"{installCommand.Replace("\"", "\\\"")}\"");
        processInfo.WorkingDirectory = tempFolder;
        await RunProcess(processInfo, "Установка .deb пакета");
        ReportProgress(100, "Пакет установлен");
    }

    private static async Task<string> Ensure7ZipExists(string tempFolder)
    {
        var sevenZipPath = Path.Combine(tempFolder, "7za.exe");
        if (File.Exists(sevenZipPath)) return sevenZipPath;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("7za.exe", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(resourceName))
            throw new FileNotFoundException("Не удалось найти ресурс 7za.exe в сборке");

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException("Не удалось получить поток для ресурса 7za.exe");

        Directory.CreateDirectory(tempFolder);
        await using var fileStream = new FileStream(sevenZipPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);

        return sevenZipPath;
    }

    public static async Task DownloadModifiedAsar()
    {
        var tempFolder = Path.Combine(Program.ModPath, "temp");
        var downloadedGzFile = Path.Combine(tempFolder, "app.asar.gz");

        await DownloadFileWithProgress(GithubUrl, downloadedGzFile, "Загрузка мода");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var asarPath = "/opt/Яндекс Музыка/resources/app.asar";
            var tempAsarPath = Path.Combine(tempFolder, "app.asar");

            var gunzipCommand = $"gunzip -c \"{downloadedGzFile}\" > \"{tempAsarPath}\"";
            var gunzipProcessInfo = CreateProcessStartInfo("/bin/bash", $"-c \"{gunzipCommand.Replace("\"", "\\\"")}\"");
            await RunProcess(gunzipProcessInfo, "распаковки asar.gz");

            ReportProgress(50, "Замена asar...");
            var moveCommand = $"mv -f \"{tempAsarPath}\" \"{asarPath}\"";
            var moveProcessInfo = CreateProcessStartInfo("/bin/bash", $"-c \"{moveCommand.Replace("\"", "\\\"")}\"");
            await RunProcess(moveProcessInfo, "замены asar");
            ReportProgress(100, "Asar заменен");
        }
        else
        {
            string resourcesPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                resourcesPath = Path.Combine(Program.ModPath, "resources");
            }
            else
            {
                var appPath = Path.Combine("/Applications", YandexMusicAppName);
                resourcesPath = Path.Combine(appPath, "Contents", "Resources");
            }

            var asarPath = Path.Combine(resourcesPath, "app.asar");
            var oldAsarPath = Path.Combine(resourcesPath,
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "app.asar.bak" : "oldapp.asar");

            if (File.Exists(asarPath))
            {
                if (File.Exists(oldAsarPath)) File.Delete(oldAsarPath);
                File.Move(asarPath, oldAsarPath);
            }

            Directory.CreateDirectory(resourcesPath);
            await ExtractGzip(downloadedGzFile, resourcesPath, tempFolder);
            
            var patcher = AsarIntegrity.CreatePatcher();
            ReportProgress(50, "Обход проверки целостности asar...");
            var success = await patcher.BypassAsarIntegrity();
            if (success)
            {
                ReportProgress(100, "Проверка целостности asar успешно обойдена");
            }
            else
            {
                ReportProgress(-1, "Ошибка обхода проверки целостности asar");
            }
        }
    }

    private static async Task ExtractArchive(string archivePath, string outputPath, string tempFolder)
    {
        var sevenZipPath = await Ensure7ZipExists(tempFolder);
        var processInfo = CreateProcessStartInfo(sevenZipPath, $"x \"{archivePath}\" -o\"{outputPath}\" -y");
        await RunProcess(processInfo, "архива");
    }

    private static async Task ExtractGzip(string gzipPath, string outputPath, string tempFolder)
    {
        var outputFilePath = Path.Combine(outputPath, "app.asar");
        var sevenZipPath = await Ensure7ZipExists(tempFolder);
        var processInfo = CreateProcessStartInfo(sevenZipPath, $"x \"{gzipPath}\" -o\"{outputPath}\" -y");
        await RunProcess(processInfo, "gzip архива");
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

    private static async Task RunProcess(ProcessStartInfo processInfo, string operationType)
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
            throw new Exception($"Ошибка выполнения операции '{operationType}'. Код выхода: {process.ExitCode}. {error}");
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
        if (File.Exists(oldAsarPath)) File.Delete(oldAsarPath);
    }

    public static async Task CleanInstall()
    {
        await Task.Run(() =>
        {
            var directory = new DirectoryInfo(Program.ModPath);
            if (!directory.Exists) return;

            foreach (var file in directory.GetFiles()) file.Delete();
            foreach (var subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
        });
    }

    private static void ReportProgress(int progress, string status)
    {
        OnDownloadProgress?.Invoke("Patcher", (progress, status));
    }

    private static async Task DownloadFileWithProgress(string url, string destinationPath,
        string progressStatusPrefix)
    {
        var retryCount = 0;
        long resumePosition = 0;
        var downloadComplete = false;

        while (!downloadComplete && retryCount < MaxRetries)
        {
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

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var readBytes = resumePosition;
                var isResume = resumePosition > 0 && response.StatusCode == HttpStatusCode.PartialContent;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(destinationPath,
                    isResume ? FileMode.Append : FileMode.Create,
                    FileAccess.Write, FileShare.None, BufferSize, true);

                var buffer = new byte[BufferSize];
                int bytesRead;
                long lastReportedBytes = 0;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    readBytes += bytesRead;
                    if (totalBytes > 0 && readBytes - lastReportedBytes >= ProgressUpdateThreshold)
                    {
                        var progress = (int)(readBytes * 100 / (totalBytes + resumePosition));
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
}