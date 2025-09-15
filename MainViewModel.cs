using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YandexMusicPatcherGui;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private bool _isProgressBarVisible;

    [ObservableProperty]
    private bool _isPatchButtonEnabled = true;

    [ObservableProperty]
    private bool _isRunButtonEnabled;

    [ObservableProperty]
    private bool _isUpdateButtonVisible;
    
    [ObservableProperty]
    private bool _areButtonsVisible = true;

    [ObservableProperty]
    private string? _versionText;

    [ObservableProperty]
    private string? _patchButtonContent = "Установить мод";
    
    [ObservableProperty]
    private bool _isPatchNotesVisible;

    [ObservableProperty]
    private string? _patchNotesContent;

    public MainViewModel()
    {
        Patcher.OnDownloadProgress += OnPatcherOnDownloadProgress;
        Dispatcher.UIThread.InvokeAsync(Initialize);
    }

    private async void OnPatcherOnDownloadProgress(object? sender, (int Progress, string Status) e)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (e.Progress < 0)
            {
                IsProgressIndeterminate = true;
                StatusText = e.Status;
            }
            else
            {
                IsProgressIndeterminate = false;
                ProgressValue = e.Progress;
                StatusText = e.Status;
            }
            IsProgressBarVisible = true;
        });
    }

    private async Task Initialize()
    {
        if (OperatingSystem.IsLinux() && Environment.UserName != "root")
        {
            IsPatchButtonEnabled = false;
            StatusText = "Для установки на Linux требуются права root. Пожалуйста, перезапустите программу с 'sudo'.";
            IsProgressBarVisible = true;
            return;
        }

        if (OperatingSystem.IsMacOS() && Utils.IsSipEnabled())
        {
            IsPatchButtonEnabled = false;
            StatusText = "Отключите SIP для продолжения";
            IsProgressBarVisible = true;
            return;
        }

        await CheckForUpdates();
        await UpdateVersionInfo();
    }

    private async Task CheckForUpdates()
    {
        var version = await Update.GetLatestAppVersion().ConfigureAwait(false);
        var hasUpdate = false;
        if (Version.TryParse(version, out var githubVersion) &&
            Version.TryParse(Program.Version, out var currentVersion))
        {
            hasUpdate = githubVersion > currentVersion;
        }
        IsUpdateButtonVisible = hasUpdate;
    }
    
    [RelayCommand]
    private async Task Patch()
    {
        IsPatchButtonEnabled = false;
        AreButtonsVisible = false;

        try
        {
            await KillYandexMusicProcess();
            await Task.Delay(500); 
            await Patcher.DownloadLatestMusic();
            await Patcher.DownloadModifiedAsar();
            var latestVersion = await VersionManager.GetLatestModVersion();
            if (latestVersion != null)
            {
                VersionManager.SetInstalledVersion(latestVersion);
            }
            await CreateDesktopShortcut();

            var message = "Ярлык создан на рабочем столе!";
            if (OperatingSystem.IsMacOS())
            {
                message = "Клиент установлен в папку программ!";
            }
            else if (OperatingSystem.IsLinux())
            {
                message = "Пакет установлен!.";
            }
            StatusText = message;
            ProgressValue = 100;
            IsProgressIndeterminate = false;
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
            ProgressValue = 100;
            IsProgressIndeterminate = false;
        }
        finally
        {
            IsPatchButtonEnabled = true;
            await UpdateVersionInfo();
            Patcher.CleanupTempFiles();
            AreButtonsVisible = true;
        }
    }

    [RelayCommand]
    private void Run()
    {
        if (!IsRunButtonEnabled) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Program.ModPath, "Яндекс Музыка.exe"),
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", "-a \"Яндекс Музыка\"");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска Яндекс Музыки: {ex}");
        }
    }

    [RelayCommand]
    private void Report()
    {
        OpenUrlSafely("https://github.com/DarkPlayOff/YandexMusicExtMod/issues/new?assignees=&labels=bug&template=bug_report.yml");
    }

    [RelayCommand]
    private void OpenUpdateUrl()
    {
        OpenUrlSafely("https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest");
    }

    private async Task UpdateVersionInfo()
    {
        var installedVersion = VersionManager.GetInstalledVersion();
        var latestVersion = await VersionManager.GetLatestModVersion().ConfigureAwait(false);

        if (installedVersion == "Не установлено")
        {
            VersionText = $"Версия мода: {latestVersion ?? "Неизвестно"}";
            PatchButtonContent = "Установить мод";
        }
        else if (installedVersion != latestVersion)
        {
            VersionText = $"Доступно обновление мода: {installedVersion} -> {latestVersion ?? "Неизвестно"}";
            PatchButtonContent = "Обновить мод";
        }
        else
        {
            VersionText = $"Версия мода: {installedVersion}";
            PatchButtonContent = "Переустановить мод";
        }

        IsRunButtonEnabled = Patcher.IsModInstalled();
    }
    
    private async Task CreateDesktopShortcut()
    {
        await Task.Run(() =>
        {
            if (OperatingSystem.IsWindows())
            {
                Utils.CreateDesktopShortcut("Яндекс Музыка", Path.Combine(Program.ModPath, "Яндекс Музыка.exe"));
            }
        }).ConfigureAwait(false);
    }
    
    private async Task KillYandexMusicProcess()
    {
        await Task.Run(() =>
        {
            foreach (var p in Process.GetProcessesByName("Яндекс Музыка"))
            {
                try
                {
                    p.Kill();
                }
                catch
                {
                    // Ignore
                }
            }
        });
    }

    private static void OpenUrlSafely(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось открыть ссылку {url}: {ex}");
        }
    }
    
    [RelayCommand]
    private async Task TogglePatchNotes()
    {
        if (!IsPatchNotesVisible)
        {
            IsPatchNotesVisible = true;
            PatchNotesContent = "Загрузка...";
            try
            {
                var patchNotesUrl =
                    "https://raw.githubusercontent.com/DarkPlayOff/YandexMusicAsar/master/PATCHNOTES.md";
                var markdownContent = await new HttpClient().GetStringAsync(patchNotesUrl).ConfigureAwait(false);
                PatchNotesContent = markdownContent;
            }
            catch (Exception ex)
            {
                PatchNotesContent = $"Не удалось загрузить изменения: {ex.Message}";
            }
        }
        else
        {
            IsPatchNotesVisible = false;
        }
    }
}