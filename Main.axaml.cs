using System.Diagnostics;
using System.Globalization;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace YandexMusicPatcherGui;

public class ProgressToWidthConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is double progress && values[1] is double width)
            return progress / 100.0 * width;
        return 0.0;
    }

    public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class Main : Window
{
    private static readonly HttpClient HttpClient = new();
    private readonly Button? _closeButton;
    private readonly Button? _closePatchNotesButton;
    private readonly ProgressBar? _downloadProgress;
    private readonly Button? _patchButton;
    private readonly Button? _patchNotesButton;
    private readonly TextBlock? _patchNotesContent;
    private readonly Border? _patchNotesPanel;
    private readonly Button? _reportButton;
    private readonly Button? _runButton;
    private readonly Button? _updateButton;
    private readonly Button? _cleanButton;
    private readonly TextBlock? _versionTextBlock;
    private readonly ToggleSwitch? _versionToggle;
    private readonly Border? _warningPanel;
    private readonly Button? _warningOkButton;
    private bool _patchingCompleted;


    public Main()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.None };
        }
        else if (OperatingSystem.IsWindows())
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None };
            Background = new SolidColorBrush(Color.Parse("#CC000000"));
        }

        AvaloniaXamlLoader.Load(this);
        _downloadProgress = this.FindControl<ProgressBar>("DownloadProgress");
        _patchButton = this.FindControl<Button>("PatchButton");
        _runButton = this.FindControl<Button>("RunButton");
        _reportButton = this.FindControl<Button>("ReportButton");
        _updateButton = this.FindControl<Button>("UpdateButton");
        _closeButton = this.FindControl<Button>("CloseButton");
        _versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
        _patchNotesPanel = this.FindControl<Border>("PatchNotesPanel");
        _patchNotesContent = this.FindControl<TextBlock>("PatchNotesContent");
        _patchNotesButton = this.FindControl<Button>("PatchNotesButton");
        _closePatchNotesButton = this.FindControl<Button>("ClosePatchNotesButton");
        _versionToggle = this.FindControl<ToggleSwitch>("VersionToggle");
        _warningPanel = this.FindControl<Border>("WarningPanel");
        _warningOkButton = this.FindControl<Button>("WarningOkButton");
        _cleanButton = this.FindControl<Button>("CleanButton");

        if (OperatingSystem.IsMacOS())
        {
            if (_versionToggle != null) _versionToggle.IsVisible = false;
            if (_cleanButton != null) _cleanButton.IsVisible = false;
        }

        DataContext = this;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        SubscribeToPatcherLog();
        if (_versionToggle != null) _versionToggle.IsCheckedChanged += VersionToggle_IsCheckedChanged;
        if (_warningOkButton != null) _warningOkButton.Click += WarningOkButton_Click;
        Dispatcher.UIThread.InvokeAsync(UpdateUI);
    }

    private void Window_MouseDown(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowProgressBar()
    {
        if (_downloadProgress != null && !_downloadProgress.IsVisible) _downloadProgress.IsVisible = true;
    }

    private async Task UpdateUI()
    {
        if (OperatingSystem.IsMacOS() && Utils.IsSipEnabled())
        {
            if (_patchButton != null) _patchButton.IsEnabled = false;
            SetProgress(100, "Отключите SIP для продолжения");
            return;
        }

        await CheckForUpdates().ConfigureAwait(false);
        await UpdateVersionInfo().ConfigureAwait(false);
    }

    private async Task CheckForUpdates()
    {
        if (_updateButton == null) return;

        var version = await Update.GetLatestAppVersion().ConfigureAwait(false);
        var hasUpdate = false;
        if (System.Version.TryParse(version, out var githubVersion) && System.Version.TryParse(Program.Version, out var currentVersion))
        {
            hasUpdate = githubVersion > currentVersion;
        }
        await Dispatcher.UIThread.InvokeAsync(() => _updateButton.IsVisible = hasUpdate);
    }

    private void SubscribeToPatcherLog()
    {
        Patcher.OnDownloadProgress += async (s, progress) =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (progress.Progress < 0)
                    SetIndeterminate(progress.Status);
                else
                    SetProgress(progress.Progress, progress.Status);
            });
        };
    }

    private void SetProgress(double value, string status)
    {
        if (_downloadProgress == null) return;

        _downloadProgress.IsIndeterminate = false;
        _downloadProgress.Value = value;
        _downloadProgress.Tag = status;
        ShowProgressBar();
    }

    private void SetIndeterminate(string status)
    {
        if (_downloadProgress == null) return;

        _downloadProgress.IsIndeterminate = true;
        _downloadProgress.Tag = status;
        ShowProgressBar();
    }

    private async Task AnimateButtonsVisibility(bool show)
    {
        var buttons = new Control?[]
        {
            _patchButton, _runButton, _reportButton, _updateButton, _closeButton, _cleanButton
        }.Where(b => b != null).ToList();

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = new CubicEaseOut()
            };

            if (show)
            {
                var visibleButtons = buttons.Where(b => b != null && b.IsVisible).ToList();
                animation.Children.Add(new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0) } });
                animation.Children.Add(new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0) } });

                var tasks = visibleButtons.Select(b => animation.RunAsync(b!, CancellationToken.None)).ToList();
                await Task.WhenAll(tasks);
            }
            else
            {
                animation.Children.Add(new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 1.0) } });
                animation.Children.Add(new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.0) } });

                var tasks = buttons.Select(b => animation.RunAsync(b!, CancellationToken.None)).ToList();
                await Task.WhenAll(tasks);

                foreach (var button in buttons)
                    if (button != null)
                        button.IsVisible = false;
            }
        });
    }

    private async void PatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_patchButton != null) _patchButton.IsEnabled = false;
        if (_cleanButton != null) _cleanButton.IsVisible = false;
        if (_versionToggle != null) _versionToggle.IsVisible = false;
        if (_versionTextBlock != null) _versionTextBlock.IsVisible = false;

        await AnimateButtonsVisibility(false);

        try
        {
            await KillYandexMusicProcess();
            await Task.Delay(500);
            await InstallMod();
            var latestVersion = await VersionManager.GetLatestModVersion();
            if (latestVersion != null) VersionManager.SetInstalledVersion(latestVersion);
            await CreateDesktopShortcut();
            _patchingCompleted = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска патчера: {ex}");
        }
        finally
        {
            if (_patchButton != null) _patchButton.IsEnabled = true;
            await UpdateUIAfterAction();
        }
    }

    private async Task CreateDesktopShortcut()
    {
        await Task.Run(() =>
        {
            if (OperatingSystem.IsWindows())
                Utils.CreateDesktopShortcut("Яндекс Музыка", Path.Combine(Program.ModPath, "Яндекс Музыка.exe"));
        }).ConfigureAwait(false);
    }

    private async Task UpdateUIAfterAction()
    {
        await UpdateUI();

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_downloadProgress != null && _patchingCompleted)
            {
                var message = OperatingSystem.IsMacOS()
                    ? "Клиент установлен в папку программ!"
                    : "Ярлык создан на рабочем столе!";
                SetProgress(100, message);
            }

            _patchButton?.SetCurrentValue(IsVisibleProperty, true);
            _runButton?.SetCurrentValue(IsVisibleProperty, true);
            _reportButton?.SetCurrentValue(IsVisibleProperty, true);
            _closeButton?.SetCurrentValue(IsVisibleProperty, true);
            if (_updateButton != null) _updateButton.IsVisible = false;
            if (_versionTextBlock != null) _versionTextBlock.IsVisible = true;
            if (!OperatingSystem.IsMacOS())
            {
                if (_cleanButton != null) _cleanButton.IsVisible = true;
                if (_versionToggle != null) _versionToggle.IsVisible = true;
            }

            await AnimateButtonsVisibility(true);
        });
    }

    private async Task KillYandexMusicProcess()
    {
        await Task.Run(() =>
        {
            foreach (var p in Process.GetProcessesByName("Яндекс Музыка"))
                try
                {
                    p.Kill();
                }
                catch
                {
                }
        });
    }

    private async Task InstallMod()
    {
        var useLatest = OperatingSystem.IsMacOS() || (_versionToggle?.IsChecked ?? false);

        if (useLatest || !Patcher.IsModInstalled())
        {
            await Patcher.DownloadLastestMusic(useLatest);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetProgress(100, "Клиент Яндекс Музыки уже установлен, пропуск загрузки.");
            });
        }

        await Patcher.DownloadModifiedAsar(useLatest);
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runButton == null || !_runButton.IsEnabled) return;

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

        Close();
    }

    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrlSafely(
            "https://github.com/DarkPlayOff/YandexMusicExtMod/issues/new?assignees=&labels=bug&template=bug_report.yml");
    }

    private async void CleanButton_Click(object sender, RoutedEventArgs e)
    {
        await Patcher.CleanInstall().ConfigureAwait(false);
        await UpdateUIAfterAction().ConfigureAwait(false);
    }

    

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrlSafely("https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest");
    }

    private async Task UpdateVersionInfo()
    {
        var installedVersion = VersionManager.GetInstalledVersion();
        var latestVersion = await VersionManager.GetLatestModVersion().ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_versionTextBlock != null)
            {
                if (installedVersion == "Не установлено")
                    _versionTextBlock.Text = string.Empty;
                else if (installedVersion != latestVersion)
                    _versionTextBlock.Text =
                        $"Доступно обновление мода: {installedVersion} -> {latestVersion ?? "Неизвестно"}";
                else
                    _versionTextBlock.Text = $"Версия мода: {installedVersion}";
            }

            if (_patchButton != null)
            {
                if (installedVersion == "Не установлено")
                    _patchButton.Content = "Установить мод";
                else if (installedVersion != latestVersion)
                    _patchButton.Content = "Обновить мод";
                else
                    _patchButton.Content = "Переустановить мод";
            }

            if (_runButton != null) _runButton.IsEnabled = Patcher.IsModInstalled();
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

    private async void PatchNotesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_patchNotesPanel == null || _patchNotesContent == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _patchNotesPanel.IsVisible = true;
            _patchNotesPanel.Classes.Add("Visible");
            _patchNotesContent.Text = "Загрузка...";
        });

        try
        {
            var patchNotesUrl =
                "https://raw.githubusercontent.com/TheKing-OfTime/YandexMusicModClient/master/PATCHNOTES.md";
            var markdownContent = await HttpClient.GetStringAsync(patchNotesUrl).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => _patchNotesContent.Text = markdownContent);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => _patchNotesContent.Text = $"Не удалось загрузить изменения : {ex.Message}");
        }
    }

    private void ClosePatchNotesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_patchNotesPanel == null) return;

        _patchNotesPanel.Classes.Remove("Visible");
    }

    private void VersionToggle_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_versionToggle?.IsChecked == true && _warningPanel != null)
        {
            _warningPanel.IsVisible = true;
        }
    }

    private void WarningOkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_warningPanel != null)
        {
            _warningPanel.IsVisible = false;
        }
    }
}