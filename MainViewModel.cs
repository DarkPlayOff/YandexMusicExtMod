using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YandexMusicPatcherGui.Services;

namespace YandexMusicPatcherGui;

public partial class MainViewModel : ObservableObject
{
    private readonly PatcherService _patcherService = new();
    
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
    private bool _isRunButtonVisible = !OperatingSystem.IsLinux();

    [ObservableProperty]
    private bool _isUpdateButtonVisible;
    
    [ObservableProperty]
    private bool _areButtonsVisible = true;

    [ObservableProperty]
    private string? _versionText;

    [ObservableProperty]
    private bool _isVersionTextVisible;

    [ObservableProperty]
    private string? _patchButtonContent = "Установить мод";

    public MainViewModel()
    {
        Patcher.OnDownloadProgress += OnPatcherOnDownloadProgress;
        Dispatcher.UIThread.InvokeAsync(Initialize);
    }

    private void OnPatcherOnDownloadProgress(object? sender, (int Progress, string Status) e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (e.Progress < 0)
            {
                IsProgressIndeterminate = true;
            }
            else
            {
                IsProgressIndeterminate = false;
                ProgressValue = e.Progress;
            }
            StatusText = e.Status;
            IsProgressBarVisible = true;
        });
    }

    private async Task Initialize()
    {
        try
        {
            var (isSupported, message) = await Program.PlatformService.IsSupported();
            if (!isSupported)
            {
                IsPatchButtonEnabled = false;
                StatusText = message;
                AreButtonsVisible = false;
                IsProgressBarVisible = true;
                return;
            }

            await CheckForUpdates();
            await UpdateVersionInfo();
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка инициализации: " + ex.Message;
            AreButtonsVisible = false;
            IsProgressBarVisible = true;
        }
    }

    private async Task CheckForUpdates()
    {
        var githubVersion = await Update.GetLatestAppVersion();
        var hasUpdate = false;
        if (githubVersion != null && Version.TryParse(Program.Version, out var currentVersion))
        {
            hasUpdate = githubVersion > currentVersion;
        }
        IsUpdateButtonVisible = hasUpdate;
    }

    [RelayCommand]
    public async Task Patch()
    {
        AreButtonsVisible = false;
        IsProgressBarVisible = true;
        IsPatchButtonEnabled = false;

        try
        {
            var progress = new Progress<(int, string)>(e => OnPatcherOnDownloadProgress(this, e));
            await _patcherService.Patch(progress);

            await UpdateVersionInfo();
            StatusText = "Установка завершена!";
            AreButtonsVisible = true;
            IsPatchButtonEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
            ProgressValue = 0;
            IsProgressIndeterminate = false;
            Trace.TraceError("Ошибка патча: {0}", ex.ToString());
        }
        finally
        {
            Patcher.CleanupTempFiles();
        }
    }

    [RelayCommand]
    public void Run()
    {
        if (!IsRunButtonEnabled) return;

        try
        {
            Program.PlatformService.RunApplication();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска Яндекс Музыки: {ex}");
        }
        
        Environment.Exit(0);
    }

    [RelayCommand]
    public void OpenUpdateUrl()
    {
        OpenUrlSafely(Program.RepoUrl + "/releases/latest");
    }

    private async Task UpdateVersionInfo()
    {
        var installedVersion = Update.GetInstalledVersion();
        var latestVersion = await Update.GetLatestModVersion();

        IsRunButtonEnabled = installedVersion != null;
        IsVersionTextVisible = installedVersion != null;

        if (installedVersion == null)
        {
            PatchButtonContent = "Установить мод";
            return;
        }
    
        VersionText = $"Версия мода: {installedVersion}";
    
        if (latestVersion != null && installedVersion < latestVersion)
        {
            VersionText = $"Доступно обновление мода: {installedVersion} -> {latestVersion}";
            PatchButtonContent = "Обновить мод";
        }
        else
        {
            PatchButtonContent = "Переустановить";
        }
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
    
}