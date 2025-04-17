using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using YandexMusicPatcherGui.Models;

namespace YandexMusicPatcherGui
{

    public partial class Main : Window
    {
        private static TextBox _LogTextBox;

        public Main()
        {
            InitializeComponent();
            _LogTextBox = LogBox;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EnableBlur();
            InitConfig();
            SubscribeToPatcherLog();
            UpdateButtonsState();
            _ = CheckForUpdates();
            Log("������...");
            AnimateWindowIn();
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
                GradientColor = unchecked((int)0x99000000), // 60% ���������� ������
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

        #region ������������
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            AnimateWindowOutAndClose();
        }

        private void InitConfig()
        {
            // ������� ������, ���� ���
            if (!File.Exists("config.json"))
                File.WriteAllText("config.json",
                    JsonConvert.SerializeObject(Config.Default(), Formatting.Indented));

            // ���������
            try
            {
                Program.Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"������ ��� ������ ������������:\n{ex.Message}", "������",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
        #endregion

        #region �����������

        private void SubscribeToPatcherLog()
        {
            Patcher.Onlog += (s, msg) =>
            {
                // Patcher ����� ���������� � �������� ������
                Dispatcher.Invoke(() => Log($"Patcher - {msg}"));
            };
        }

        public static void Log(string message)
        {
            _LogTextBox.AppendText($"[ {DateTime.Now:HH:mm:ss} ] {message}\n");
            _LogTextBox.ScrollToEnd();
        }

        #endregion

        #region ���������/������

        private async void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;
            RunButton.IsEnabled = false;

            try
            {
                Log("�������� ���������...");
                foreach (var p in Process.GetProcessesByName("������ ������"))
                {
                    p.Kill();
                }

                await Task.Delay(500);
                if (Directory.Exists("temp")) Directory.Delete("temp", true);
                if (Directory.Exists(Program.ModPath)) Directory.Delete(Program.ModPath, true);

                await Patcher.DownloadLastestMusic();
                await Patcher.Asar.Unpack(Path.Combine(Program.ModPath, "resources", "app.asar"),
                                             Path.Combine(Program.ModPath, "resources", "app"));
                File.Delete(Path.Combine(Program.ModPath, "resources", "app.asar"));
                Patcher.InstallMods(Path.Combine(Program.ModPath, "resources", "app"));
                await Patcher.Asar.Pack(Path.Combine(Program.ModPath, "resources", "app", "*"),
                                        Path.Combine(Program.ModPath, "resources", "app.asar"));

                Utils.CreateDesktopShortcut("������ ������",
                    Path.Combine(Program.ModPath, "������ ������.exe"));

                Log("������! ����� ������ �� ������� �����.");
            }
            catch (Exception ex)
            {
                Log($"������ ������� �������:\n{ex}");
                MessageBox.Show($"������ ������� �������:\n{ex}", "������", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateButtonsState();
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(Path.Combine(Program.ModPath, "������ ������.exe"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"������ ������� ������:\n\n{ex}", "������", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReportButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/DarkPlayOff/YandexMusicExtMod/issues/new",
                UseShellExecute = true
            });
        }

        #endregion

        #region ���������� � ��������� ������

        private async Task CheckForUpdates()
        {
            var version = await Update.GetLastVersion();
            if (!string.IsNullOrWhiteSpace(version) && version != "error" && version != Program.Version)
            {
                var res = MessageBox.Show($"�������� ���������� v{version}!\n�������?", "����������",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (res == MessageBoxResult.OK)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/DarkPlayOff/YandexMusicExtMod/releases/latest",
                        UseShellExecute = true
                    });
                }
            }
        }
        #endregion

        private void UpdateButtonsState()
        {
            if (Patcher.IsModInstalled())
            {
                PatchButton.Content = "�������� ���";
                RunButton.IsEnabled = true;
            }
            else
            {
                PatchButton.Content = "���������� ���";
                RunButton.IsEnabled = false;
            }
            PatchButton.IsEnabled = true;
        }

        #region �������� �������� ����

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

        #endregion

        #region �������� �������� ����

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
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