using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace YandexMusicPatcherGui
{
    public static class Patcher
    {
        public static event EventHandler<(int Progress, string Status)> OnDownloadProgress;

        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Проверяет установлен ли мод
        /// </summary>
        public static bool IsModInstalled()
        {
            string targetPath = Path.Combine(Program.ModPath);

            if (!Directory.Exists(targetPath))
                return false;

            var filesToCheck = new string[]
            {
                "Яндекс Музыка.exe",
                "resources/app.asar"
            }
            .Select(x => Path.GetFullPath(Path.Combine(targetPath, x)));

            return filesToCheck.All(File.Exists);
        }

        /// <summary>
        /// Устанавливает моды. Копирует js моды в папку ресурсов приложeния а также патчит некоторые существующие js файлы
        /// </summary>
        public static void InstallMods(string appPath)
        {
            OnDownloadProgress?.Invoke("Patcher", (0, "Копирование модов..."));

            var newModsPath = Path.GetFullPath(Path.Combine(appPath, "app/_next/static/yandex_mod"));
            Utils.CopyFilesRecursively(Path.GetFullPath("mods"), newModsPath);

            OnDownloadProgress?.Invoke("Patcher", (50, "Установка модов..."));

            // Вставить конфиг патчера в скрипт инициализации патчера в приложении
            ReplaceFileContents(
                Path.Combine(newModsPath, "index.js"),
                "//%PATCHER_CONFIG_OVERRIDE%",
                File.ReadAllText(Path.Combine("mods", "index.js")));

            // Добавить _appIndex.js в исходный index.js приложения
            ReplaceFileContents(
                Path.Combine(appPath, "main/index.js"),
                "createWindow_js_1.createWindow)();",
                "createWindow_js_1.createWindow)();\n\n" + File.ReadAllText("mods/inject/_appIndex.js"));

            // Ручной инжект инициализатора модов в html страницы
            var htmlFiles = Directory.GetFiles(Path.Combine(appPath, "app"), "*.html", SearchOption.AllDirectories);
            var injectHtml = File.ReadAllText("mods/inject/_appIndexHtml.js");
            foreach (var file in htmlFiles)
            {
                var content = File.ReadAllText(file)
                    .Replace("<!DOCTYPE html><html><head>",
                             $"<!DOCTYPE html><html><head><script>{injectHtml}</script>");
                File.WriteAllText(file, content);
            }

            // Удалить видео-заставку
            Directory.Delete(Path.Combine(appPath, "app/media/splash_screen"), true);

            OnDownloadProgress?.Invoke("Patcher", (100, "Установка модов завершена"));
        }

        private static void ReplaceFileContents(string path, string replace, string replaceTo)
        {
            string content = File.ReadAllText(path);
            if (!content.Contains(replace))
            {
            }

            string newContent = content.Replace(replace, replaceTo);
        }

        /// <summary>
        /// Скачивает последний билд музыки через ванильный механизм обновления, вытаскивает портативку из экзешника.
        /// </summary>
        public static async Task DownloadLastestMusic()
        {
            Directory.CreateDirectory("temp");

            var musicS3 = "https://music-desktop-application.s3.yandex.net";

            OnDownloadProgress?.Invoke("Patcher", (0, "Получение информации о последней версии..."));

            var yamlRaw = await httpClient.GetStringAsync($"{musicS3}/stable/latest.yml");

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var lastest = deserializer.Deserialize<dynamic>(yamlRaw);
            var lastestUrl = $"{musicS3}/stable/{(string)lastest["path"]}";

            OnDownloadProgress?.Invoke("Patcher", (0, "Начало скачивания..."));

            using (var response = await httpClient.GetAsync(lastestUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var readBytes = 0L;

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream("temp/stable.exe", FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        readBytes += bytesRead;

                        if (totalBytes != -1)
                        {
                            var progress = (int)((readBytes * 100) / totalBytes);
                            OnDownloadProgress?.Invoke("Patcher", (progress, $"Скачивание клиента: {progress}%"));
                        }
                    }
                }
            }

            OnDownloadProgress?.Invoke("Patcher", (-1, "Распаковка клиента..."));

            var processStartInfo = new ProcessStartInfo(Path.GetFullPath("7zip\\7za.exe"))
            {
                Arguments = $"x \"{Path.GetFullPath("temp/stable.exe")}\" -o{Program.ModPath} -y",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                await process.WaitForExitAsync();
            }

            OnDownloadProgress?.Invoke("Patcher", (100, "Успешно распаковано!"));
        }

        public static class Asar
        {
            public static async Task Unpack(string asarPath, string destPath)
            {
                string tempFolder = Path.GetFullPath("temp");
                string downloadedGzFile = Path.Combine(tempFolder, "app.asar.gz");
                string downloadUrl = "https://github.com/TheKing-OfTime/YandexMusicModClient/releases/latest/download/app.asar.gz";
                try
                {
                    using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var readBytes = 0L;

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(downloadedGzFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                        {
                            var buffer = new byte[8192];
                            int bytesRead;
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                readBytes += bytesRead;

                                if (totalBytes != -1)
                                {
                                    var progress = (int)((readBytes * 100) / totalBytes);
                                    OnDownloadProgress?.Invoke("Patcher", (progress, $"Загрузка мода: {progress}%"));
                                }
                            }
                        }
                    }
                }
                catch
                {
                    throw;
                }

                OnDownloadProgress?.Invoke("Patcher", (-1, "Распаковка gzip..."));

                string decompressedAsarFile = Path.Combine(tempFolder, "app.asar");
                using (var originalFileStream = new FileStream(downloadedGzFile, FileMode.Open, FileAccess.Read))
                using (var decompressedFileStream = new FileStream(decompressedAsarFile, FileMode.Create, FileAccess.Write))
                using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                {
                    await decompressionStream.CopyToAsync(decompressedFileStream);
                }

                OnDownloadProgress?.Invoke("Patcher", (-1, "Распаковка asar..."));

                var processStartInfo = new ProcessStartInfo(Path.GetFullPath("7zip\\7z.exe"))
                {
                    Arguments = $"x \"{Path.GetFullPath(decompressedAsarFile)}\" -o{destPath} -y",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                }

                OnDownloadProgress?.Invoke("Patcher", (100, "Распаковка завершена"));
            }

            /// Запаковывает app.asar
            public static async Task Pack(string asarFolderPath, string destPath)
            {
                OnDownloadProgress?.Invoke("Patcher", (-1, "Упаковка asar..."));

                var processStartInfo = new ProcessStartInfo(Path.GetFullPath("7zip\\7z.exe"))
                {
                    Arguments = $"a \"{Path.GetFullPath(destPath)}\" \"{Path.GetFullPath(asarFolderPath)}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                }

                OnDownloadProgress?.Invoke("Patcher", (100, "Установка завершена!"));
                Directory.Delete("temp", true);
            }
        }
    }
}