using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Interactivity;

namespace YandexMusicPatcherGui;

public partial class Main : Window
{
    public Main()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.None };
        }
        else if (OperatingSystem.IsWindows())
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None };
            Background = new SolidColorBrush(Color.Parse("#CC000000"));
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Blur, WindowTransparencyLevel.None };
            Background = new SolidColorBrush(Color.Parse("#CC000000"));
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Window_MouseDown(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}