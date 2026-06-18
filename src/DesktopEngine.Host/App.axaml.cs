using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DesktopEngine.Harness;
using DesktopEngine.Platform.Windows;

namespace DesktopEngine.Host;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new OverlayWindow(new WindowsWindowEffects(), SpriteScene.Default);
            desktop.MainWindow = window;
            if (Program.HarnessPipe is { } pipe)
                new HarnessServer(pipe, window, () => desktop.Shutdown()).Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
