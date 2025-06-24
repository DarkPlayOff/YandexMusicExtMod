using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using System.Net;

namespace YandexMusicPatcherGui
{
    public static class Patcher
    {
        public static event EventHandler<(int Progress, string Status)>? OnDownloadProgress;

        private static readonly HttpClient httpClient;
        private const int MaxRetries = 5;
        private const string MusicS3Url = "https://music-desktop-application.s3.yandex.net";
        private const string GithubUrl = "https://github.com/DarkPlayOff/YandexMusicAsar/releases/latest/download/app.asar.gz";
        private const int BufferSize = 81920;
        private const int ProgressUpdateThreshold = 51200;

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
        
        public static bool IsModInstalled()
        {
            string targetPath = Program.ModPath;

            if (!Directory.Exists(targetPath))
                return false;

            string exePath = Path.Combine(targetPath, "Яндекс Музыка.exe");
            string asarPath = Path.Combine(targetPath, "resources", "app.asar");

            return File.Exists(exePath) && File.Exists(asarPath);
        }

        public static async Task DownloadLastestMusic()
        {
            string tempFolder = Path.Combine(Program.ModPath, "temp");
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(Program.ModPath);

            try
            {
                await Extract7ZaToFolder(tempFolder);
                
                var yamlRaw = await RetryOnFailAsync(
                    async () => await httpClient.GetStringAsync($"{MusicS3Url}/stable/latest.yml"),
                    "загрузка");
                
                if (string.IsNullOrEmpty(yamlRaw))
                    throw new Exception("Не удалось загрузить latest.yml");
                
                
                var latestPath = ParseYamlPath(yamlRaw);
                var latestUrl = $"{MusicS3Url}/stable/{latestPath}";

                string stableExePath = Path.Combine(tempFolder, "stable.exe");
                await DownloadFileWithProgressAsync(latestUrl, stableExePath, "Загрузка клиента");

                ReportProgress(100, "Распаковка...");
                await ExtractArchiveAsync(stableExePath, Program.ModPath, tempFolder);
                ReportProgress(100, "Установка завершена");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task DownloadModifiedAsar()
        {
            string tempFolder = Path.Combine(Program.ModPath, "temp");
            string downloadedGzFile = Path.Combine(tempFolder, "app.asar.gz");
            string resourcesPath = Path.GetFullPath(Path.Combine(Program.ModPath, "resources"));
            
            try
            {
                Directory.CreateDirectory(resourcesPath);
                Directory.CreateDirectory(tempFolder);
                
                await DownloadFileWithProgressAsync(GithubUrl, downloadedGzFile, "Загрузка мода");
                
                await ExtractGzipAsync(downloadedGzFile, resourcesPath, tempFolder);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static string ParseYamlPath(string yamlContent)
        {
            var match = Regex.Match(yamlContent, @"(?m)^\s*path:\s*(.+)\s*$");
            if (!match.Success)
                throw new FormatException("Не удалось найти поле 'path' в latest.yml");

            return match.Groups[1].Value.Trim().Trim('"');
        }

        private static async Task ExtractArchiveAsync(string archivePath, string outputPath, string tempFolder)
        {
            string sevenZipPath = await Extract7ZaToFolder(tempFolder);
            
            var processInfo = CreateProcessStartInfo(sevenZipPath,
                $"x \"{archivePath}\" -o\"{outputPath}\" -y");

            await RunProcessAsync(processInfo, "архива");
        }

        private static async Task ExtractGzipAsync(string gzipPath, string outputPath, string tempFolder)
        {
            string sevenZipPath = await Extract7ZaToFolder(tempFolder);
            
            var processInfo = CreateProcessStartInfo(sevenZipPath,
                $"x \"{gzipPath}\" -o\"{outputPath}\" -y");

            await RunProcessAsync (processInfo, "gzip архива");
        }

        private static async Task<string> Extract7ZaToFolder(string folder)
        {
            string extracted7zaPath = Path.Combine(folder, "7za.exe");
            
            if (File.Exists(extracted7zaPath))
                return extracted7zaPath;
                
            var assembly = Assembly.GetExecutingAssembly();
            
            string? resourceName = assembly.GetManifestResourceNames()
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
            
            if (!File.Exists(extracted7zaPath))
                throw new FileNotFoundException($"Не удалось создать файл 7za.exe в {folder}");
                
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
                WorkingDirectory = directoryPath ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty
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
            if (Directory.Exists(directory))
            {
                try { Directory.Delete(directory, true); } catch { }
            }
        }
        
        public static void CleanupTempFiles()
        {
            string tempFolder = Path.Combine(Program.ModPath, "temp");
            CleanupDirectory(tempFolder);
        }
        
        private static void SafeDelete(string filePath)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
        }

        private static void ReportProgress(int progress, string status)
        {
            OnDownloadProgress?.Invoke("Patcher", (progress, status));
        }

        private static async Task DownloadFileWithProgressAsync(string url, string destinationPath, string progressStatusPrefix)
        {
            int retryCount = 0;
            long resumePosition = 0;
            bool downloadComplete = false;
            
            while (!downloadComplete && retryCount < MaxRetries)
            {
                try
                {
                    if (retryCount > 0)
                    {
                        if (retryCount == 1 || retryCount == MaxRetries - 1) 
                            ReportProgress(0, $"Повторная попытка {retryCount}...");
                            
                        double delay = Math.Min(30, Math.Pow(2, retryCount - 1));
                        await Task.Delay(TimeSpan.FromSeconds(delay));
                        
                        if (File.Exists(destinationPath))
                            resumePosition = new FileInfo(destinationPath).Length;
                    }
                    
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    
                    if (resumePosition > 0)
                        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumePosition, null);
                    
                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    
                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        downloadComplete = true;
                        continue;
                    }
                    
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength;
                    var readBytes = resumePosition;
                    var isResume = resumePosition > 0 && (response.StatusCode == HttpStatusCode.PartialContent);
                    
                    await using var stream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(
                        destinationPath, 
                        isResume ? FileMode.Append : FileMode.Create, 
                        FileAccess.Write, 
                        FileShare.None, 
                        BufferSize, 
                        true);
                    
                    byte[] buffer = new byte[BufferSize];
                    int bytesRead;
                    long lastReportedBytes = 0;
                    
                    while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        readBytes += bytesRead;
                        if (totalBytes.HasValue && (readBytes - lastReportedBytes) >= ProgressUpdateThreshold)
                        {
                            int progress = (int)((readBytes * 100) / (totalBytes.Value + resumePosition));
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
                        ReportProgress(-1, $"Ошибка загрузки");
                        throw;
                    }
                }
            }
        }

        private static async Task<T> RetryOnFailAsync<T>(Func<Task<T>> action, string operationDescription, int maxRetries = MaxRetries)
        {
            int retryCount = 0;
            
            while (true)
            {
                try
                {
                    if (retryCount > 0)
                    {
                        double delay = Math.Min(30, Math.Pow(2, retryCount - 1));
                        await Task.Delay(TimeSpan.FromSeconds(delay));
                    }
                    
                    return await action();
                }
                catch (Exception)
                {
                    retryCount++;
                    
                    if (retryCount >= maxRetries)
                        throw;
                    
                    if (retryCount == 1);
                }
            }
        }
    }
}