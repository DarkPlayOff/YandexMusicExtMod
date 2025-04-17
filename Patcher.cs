using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace YandexMusicPatcherGui
{
    public static class Patcher
    {
        public static event EventHandler<string> Onlog;

        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Проверяет, установлен ли мод
        /// </summary>
        public static bool IsModInstalled()
        {
            if (!Directory.Exists(Program.ModPath))
                return false;

            var filesToCheck = new string[]
            {
                    "Яндекс Музыка.exe",
                    "resources/app.asar"
            }
            .Select(x => Path.GetFullPath(Path.Combine(Program.ModPath, x)));

            return filesToCheck.All(File.Exists);
        }

        /// <summary>
        /// Устанавливает моды. Копирует js моды в папку ресурсов приложeния а также патчит некоторые существующие js файлы
        /// </summary>
        public static void InstallMods(string appPath)
        {
            Onlog?.Invoke("Patcher", "Копирую моды...");

            var newModsPath = Path.GetFullPath(Path.Combine(appPath, "app/_next/static/yandex_mod"));
            Utils.CopyFilesRecursively(Path.GetFullPath("mods"), newModsPath);

            Onlog?.Invoke("Patcher", "Устанавливаю моды...");

            // Вставить конфиг патчера в скрипт инициализации патчера в приложении
            ReplaceFileContents(
                Path.Combine(newModsPath, "index.js"),
                "//%PATCHER_CONFIG_OVERRIDE%",
                "modConfig = " +
                JsonConvert.SerializeObject(Program.Config.Mods.ToDictionary(x => x.Tag, x => x.Enabled),
                    Formatting.Indented));

            // Добавить _appIndex.js в исходный index.js приложения
            if (Program.Config.HasMod("disableTracking"))
                ReplaceFileContents(
                    Path.Combine(appPath, "main/index.js"),
                    "createWindow_js_1.createWindow)();",
                    "createWindow_js_1.createWindow)();\n\n" + File.ReadAllText("mods/inject/_appIndex.js"));

            // Добавить _appPreload.js в исходный preload.js приложения
            var preloadPath = Path.Combine(appPath, "main/lib/preload.js");
            File.WriteAllText(preloadPath, File.ReadAllText(preloadPath) + File.ReadAllText("mods/inject/_appPreload.js"));

            // Ручной инжект инициализатора модов в html страницы, тк электроновский preload скрипт не всегда работает
            var htmlFiles = Directory.GetFiles(Path.Combine(appPath, "app"), "*.html", SearchOption.AllDirectories);
            var injectHtml = File.ReadAllText("mods/inject/_appIndexHtml.js");
            foreach (var file in htmlFiles)
            {
                var content = File.ReadAllText(file)
                    .Replace("<!DOCTYPE html><html><head>",
                             $"<!DOCTYPE html><html><head><script>{injectHtml}</script>");
                File.WriteAllText(file, content);
            }

            // Включить верхнее меню
            var systemMenuPath = Path.Combine(appPath, "main/lib/systemMenu.js");
            var menuContent = File.ReadAllText(systemMenuPath)
                .Replace("if (node_os_1.default.platform() === platform_js_1.Platform.MACOS)",
                    $"if ({Program.Config.HasMod("useDevTools").ToString().ToLower()})")
                .Replace("if (config_js_1.config.enableDevTools)",
                    $"if ({Program.Config.HasMod("useDevTools").ToString().ToLower()})");
            File.WriteAllText(systemMenuPath, menuContent);

            // Включить системную рамку окна
            if (Program.Config.HasMod("useDevTools"))
            {
                var createWindowPath = Path.Combine(appPath, "main/lib/createWindow.js");
                File.WriteAllText(createWindowPath,
                    File.ReadAllText(createWindowPath).Replace("titleBarStyle: 'hidden',", "//titleBarStyle: 'hidden',"));
            }

            // Удалить видео-заставку
            Directory.Delete(Path.Combine(appPath, "app/media/splash_screen"), true);

            Onlog?.Invoke("Patcher", "Моды установлены");
        }

        private static void ReplaceFileContents(string path, string replace, string replaceTo)
        {
            File.WriteAllText(path, File.ReadAllText(path).Replace(replace, replaceTo));
        }

        /// <summary>
        /// Скачивает последний билд музыки через ванильный механизм обновления, вытаскивает портативку из экзешника.
        /// </summary>
        public static async Task DownloadLastestMusic()
        {
            Directory.CreateDirectory("temp");

            var musicS3 = "https://music-desktop-application.s3.yandex.net";

            Onlog?.Invoke("Patcher", "Получаю последний билд Яндекс Музыки...");

            var yamlRaw = await httpClient.GetStringAsync($"{musicS3}/stable/latest.yml");

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var lastest = deserializer.Deserialize<dynamic>(yamlRaw);
            var lastestUrl = $"{musicS3}/stable/{(string)lastest["path"]}";

            Onlog?.Invoke("Patcher", "Ссылка получена, скачиваю билд...");

            using (var response = await httpClient.GetAsync(lastestUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream("temp/stable.exe", FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }

            Onlog?.Invoke("Patcher", "Распаковка...");

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

            Onlog?.Invoke("Patcher", "Успешно распаковано");
        }

        public static class Asar
        {
            /// <summary>
            /// Распаковывает app.asar
            /// </summary>
            public static async Task Unpack(string asarPath, string destPath)
            {
                Onlog?.Invoke("Patcher", "Загрузка файла app.asar...");

                // Создаем временную директорию для скачанного файла
                string tempFolder = Path.GetFullPath("temp");
                Directory.CreateDirectory(tempFolder);
                string downloadedFile = Path.Combine(tempFolder, "app.asar");

                // Загрузка файла с GitHub с использованием потокового копирования
                string downloadUrl = "https://github.com/TheKing-OfTime/YandexMusicModClient/releases/latest/download/app.asar";
                try
                {
                    using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(downloadedFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Onlog?.Invoke("Patcher", $"Ошибка загрузки app.asar: {ex.Message}");
                    throw;
                }

                Onlog?.Invoke("Patcher", "Файл загружен, распаковка asar...");

                var processStartInfo = new ProcessStartInfo(Path.GetFullPath("7zip\\7z.exe"))
                {
                    Arguments = $"x \"{Path.GetFullPath(downloadedFile)}\" -o{destPath} -y",
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

                Onlog?.Invoke("Patcher", "Распаковано");
            }

            /// <summary>
            /// Запаковывает app.asar
            /// </summary>
            public static async Task Pack(string asarFolderPath, string destPath)
            {
                Onlog?.Invoke("Patcher", "Упаковываю обратно asar...");

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

                Onlog?.Invoke("Patcher", "Упаковано");
                Directory.Delete("temp", true);
            }
        }
    }
}