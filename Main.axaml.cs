using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;

namespace YandexMusicPatcherGui
{
    public class ProgressToWidthConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 2 && values[0] is double progress && values[1] is double width)
            {
                return (progress / 100.0) * width;
            }
            return 0.0;
        }

        public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class Main : Window
    {
        private readonly ProgressBar? _downloadProgress;
        private readonly Button? _patchButton;
        private readonly Button? _runButton;
        private readonly Button? _reportButton;
        private readonly Button? _updateButton;
        private readonly Button? _closeButton;
        private bool _patchingCompleted = false;
        

        public Main()
        {
            AvaloniaXamlLoader.Load(this);
            _downloadProgress = this.FindControl<ProgressBar>("DownloadProgress");
            _patchButton      = this.FindControl<Button>("PatchButton");
            _runButton        = this.FindControl<Button>("RunButton");
            _reportButton     = this.FindControl<Button>("ReportButton");
            _updateButton     = this.FindControl<Button>("UpdateButton");
            _closeButton      = this.FindControl<Button>("CloseButton");

            DataContext = this;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            SubscribeToPatcherLog();
            UpdateButtonsState();
            Dispatcher.UIThread.InvokeAsync(CheckForUpdates);
        }

        private void Window_MouseDown(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void CloseButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

        private void ShowProgressBar()
        {
            if (_downloadProgress != null && !_downloadProgress.IsVisible)
            {
                _downloadProgress.IsVisible = true;
            }
        }

        private void UpdateButtonsState()
        {
            if (_patchButton == null || _runButton == null) return;

            bool isModInstalled = Patcher.IsModInstalled();

            _patchButton.Content = isModInstalled ? "Переустановить мод" : "Установить мод";
            _runButton.IsEnabled = isModInstalled;
        }

        private async Task CheckForUpdates()
        {
            if (_updateButton == null) return;

            var version = await Update.GetLastVersion();
            bool hasUpdate = !string.IsNullOrWhiteSpace(version) && version != Program.Version;
            _updateButton.IsVisible = hasUpdate;
        }

        private void SubscribeToPatcherLog()
        {
            Patcher.OnDownloadProgress += async (s, progress) =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (progress.Progress < 0)
                    {
                        SetIndeterminate(progress.Status);
                    }
                    else
                    {
                        SetProgress(progress.Progress, progress.Status);
                    }
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
                _patchButton, _runButton, _reportButton, _updateButton, _closeButton
            }.Where(b => b != null).ToList();

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
                {
                    if (button != null) button.IsVisible = false;
                }
            }
        }

        private async void PatchButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await AnimateButtonsVisibility(false);
            try
            {
                await KillYandexMusicProcess();
                await Task.Delay(500);
                //await CleanupDirectories();
                await InstallMod();
                await CreateDesktopShortcut();
                _patchingCompleted = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка запуска патчера: {ex}");
            }
            finally
            {
                await UpdateUIAfterPatch();
            }
        }

        private async Task CreateDesktopShortcut()
        {
            await Task.Run(() =>
            {
                if (OperatingSystem.IsWindows())
                {
                    Utils.CreateDesktopShortcut("Яндекс Музыка", Path.Combine(Program.ModPath, "Яндекс Музыка.exe"));
                }
            });
        }

        private async Task UpdateUIAfterPatch()
        {
            UpdateButtonsState();
            await CheckForUpdates();

            if (_downloadProgress != null && _patchingCompleted)
            {
                SetProgress(100, "Ярлык создан на рабочем столе!");
            }

            _patchButton?.SetCurrentValue(IsVisibleProperty, true);
            _runButton?.SetCurrentValue(IsVisibleProperty, true);
            _reportButton?.SetCurrentValue(IsVisibleProperty, true);
            _closeButton?.SetCurrentValue(IsVisibleProperty, true);
            if (_updateButton != null) _updateButton.IsVisible = false;
            await AnimateButtonsVisibility(true);
        }

        private async Task KillYandexMusicProcess()
        {
            await Task.Run(() =>
            {
                foreach (var p in Process.GetProcessesByName("Яндекс Музыка"))
                {
                    try { p.Kill(); } catch { }
                }
            }).ConfigureAwait(false);
        }

        private async Task CleanupDirectories()
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(Program.ModPath);
                foreach (var item in Directory.GetFileSystemEntries(Program.ModPath))
                {
                    try
                    {
                        if (Directory.Exists(item))
                        {
                            Directory.Delete(item, true);
                        }
                        else if (File.Exists(item))
                        {
                            File.Delete(item);
                        }
                    }
                    catch {}
                }
            }).ConfigureAwait(false);
        }

        private async Task InstallMod()
        {
            await Patcher.DownloadLastestMusic();
            await Patcher.DownloadModifiedAsar();
            Patcher.CleanupTempFiles();
        }

        private void RunButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_runButton == null || !_runButton.IsEnabled) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Program.ModPath, "Яндекс Музыка.exe"),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка запуска Яндекс Музыки: {ex}");
            }
            
            Close();
        }

        private void ReportButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenUrlSafely("https://github.com/DarkPlayOff/YandexMusicExtMod/issues/new?assignees=&labels=bug&template=bug_report.yml");
        }

        private void UpdateButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenUrlSafely("https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest");
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
}