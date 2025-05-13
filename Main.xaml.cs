using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace YandexMusicPatcherGui
{
    public class ProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double progress && values[1] is double width)
            {
                return (progress / 100.0) * width;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class Main : Window
    {
        public Main()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AnimateWindowIn();
            EnableBlur();
            SubscribeToPatcherLog();
            UpdateButtonsState();
            await CheckForUpdates();
        }

        #region Acrylic Blur

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private enum AccentState { ACCENT_DISABLED = 0, ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 }
        private enum WindowCompositionAttribute { WCA_ACCENT_POLICY = 19 }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private void EnableBlur()
        {
            var helper = new WindowInteropHelper(this);
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 0,
                GradientColor = unchecked((int)0x400000000),
                AnimationId = 0
            };
            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = size,
                    Data = ptr
                };
                SetWindowCompositionAttribute(helper.Handle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        #endregion

        #region Логирование

        private void ShowProgressBar()
        {
            if (DownloadProgress.Visibility != Visibility.Visible)
            {
                DownloadProgress.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                DownloadProgress.BeginAnimation(OpacityProperty, fadeIn);
            }
        }

        private void SetProgress(double value, string status)
        {
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = value;
            DownloadProgress.Tag = status;
            double opacity = 0.3 + 0.7 * (value / 100.0);
            var anim = new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(200));
            DownloadProgress.BeginAnimation(OpacityProperty, anim);
            ShowProgressBar();
        }

        private void SetIndeterminate(string status)
        {
            DownloadProgress.IsIndeterminate = true;
            DownloadProgress.Tag = status;
            ShowProgressBar();
        }

        private void SubscribeToPatcherLog()
        {
            Patcher.OnDownloadProgress += async (s, progress) =>
            {
                await Dispatcher.InvokeAsync(() =>
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

        #endregion

        #region Установка/Запуск

        private async void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            await FadeOutButtons();
            try
            {
                await KillYandexMusicProcess();
                await Task.Delay(500);
                await CleanupDirectories();
                await InstallMod();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска патчера:\n{ex}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await UpdateUIAfterPatch();
            }
        }

        private async Task FadeOutButtons()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) =>
            {
                this.PatchButton.Visibility = Visibility.Collapsed;
                this.RunButton.Visibility = Visibility.Collapsed;
                this.UpdateButton.Visibility = Visibility.Collapsed;
                this.ReportButton.Visibility = Visibility.Collapsed;
                this.CloseButton.Visibility = Visibility.Collapsed;
            };
            this.PatchButton.BeginAnimation(OpacityProperty, fadeOut);
            this.RunButton.BeginAnimation(OpacityProperty, fadeOut);
            this.UpdateButton.BeginAnimation(OpacityProperty, fadeOut);
            this.ReportButton.BeginAnimation(OpacityProperty, fadeOut);
            this.CloseButton.BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(200);
        }

        private async Task KillYandexMusicProcess()
        {
            await Task.Run(() =>
            {
                foreach (var p in Process.GetProcessesByName("Яндекс Музыка"))
                {
                    p.Kill();
                }
            });
        }

        private async Task CleanupDirectories()
        {
            await Task.Run(() =>
            {
                if (Directory.Exists("temp")) Directory.Delete("temp", true);
                if (Directory.Exists(Program.ModPath)) Directory.Delete(Program.ModPath, true);
            });
        }

        private async Task InstallMod()
        {
            await Patcher.DownloadLastestMusic();
            await Patcher.Asar.Unpack(Path.Combine(Program.ModPath, "resources", "app.asar"),
                                     Path.Combine(Program.ModPath, "resources", "app"));
            File.Delete(Path.Combine(Program.ModPath, "resources", "app.asar"));
            Patcher.InstallMods(Path.Combine(Program.ModPath, "resources", "app"));
            await Patcher.Asar.Pack(Path.Combine(Program.ModPath, "resources", "app", "*"),
                                    Path.Combine(Program.ModPath, "resources", "app.asar"));

            string appFolderPath = Path.Combine(Program.ModPath, "resources", "app");
            if (Directory.Exists(appFolderPath))
            {
                Directory.Delete(appFolderPath, true);
            }

            Utils.CreateDesktopShortcut("Яндекс Музыка",
                Path.GetFullPath(Path.Combine(Program.ModPath, "Яндекс Музыка.exe")));
        }

        private async Task UpdateUIAfterPatch()
        {
            UpdateButtonsState();

            var version = await Update.GetLastVersion();
            bool hasUpdate = !string.IsNullOrWhiteSpace(version) && version != "error" && version != Program.Version;

            this.PatchButton.Visibility = Visibility.Visible;
            this.RunButton.Visibility = Visibility.Visible;
            this.ReportButton.Visibility = Visibility.Visible;
            this.CloseButton.Visibility = Visibility.Visible;
            if (hasUpdate)
            {
                this.UpdateButton.Visibility = Visibility.Visible;
            }

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            this.PatchButton.BeginAnimation(OpacityProperty, fadeIn);
            this.RunButton.BeginAnimation(OpacityProperty, fadeIn);
            this.ReportButton.BeginAnimation(OpacityProperty, fadeIn);
            this.CloseButton.BeginAnimation(OpacityProperty, fadeIn);
            if (hasUpdate)
            {
                this.UpdateButton.BeginAnimation(OpacityProperty, fadeIn);
            }
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePath = Path.Combine(Program.ModPath, "Яндекс Музыка.exe");
                await Task.Run(() => Process.Start(exePath));
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска Музыки:\n\n{ex}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ReportButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/DarkPlayOff/YandexMusicExtMod/issues/new",
                UseShellExecute = true
            }));
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest",
                UseShellExecute = true
            }));
        }

        #endregion

        #region Обновления и состояние кнопок

        private async Task CheckForUpdates()
        {
            var version = await Update.GetLastVersion();
            if (!string.IsNullOrWhiteSpace(version) && version != "error" && version != Program.Version)
            {
                UpdateButton.Visibility = Visibility.Visible;
                UpdateButton.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                UpdateButton.BeginAnimation(OpacityProperty, fadeIn);

                ReportButton.HorizontalAlignment = HorizontalAlignment.Right;
                ReportButton.Margin = new Thickness(0, 0, 19, 19);
            }
            else
            {
                ReportButton.HorizontalAlignment = HorizontalAlignment.Center;
                ReportButton.Margin = new Thickness(0, 0, 0, 19);
            }
        }

        private void UpdateButtonsState()
        {
            if (Patcher.IsModInstalled())
            {
                this.PatchButton.Content = "Обновить мод";
                this.RunButton.IsEnabled = true;
            }
            else
            {
                this.PatchButton.Content = "Установить мод";
                this.RunButton.IsEnabled = false;
            }
            this.PatchButton.IsEnabled = true;
        }

        #endregion

        #region Анимация окна

        private void AnimateWindowIn()
        {
            var sb = new Storyboard();
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            Storyboard.SetTarget(fade, this);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);
            this.Opacity = 0;
            sb.Begin();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            AnimateWindowOutAndClose();
        }

        private void AnimateWindowOutAndClose()
        {
            var sb = new Storyboard();
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (s, e) => this.Close();
            Storyboard.SetTarget(fade, this);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);
            sb.Begin();
        }

        #endregion
    }
}
