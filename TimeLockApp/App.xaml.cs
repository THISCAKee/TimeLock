using System.Windows;
using TimeLockApp.Services;

namespace TimeLockApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static LanguageService Language { get; } = LanguageService.Default;

    private SingleInstanceGuard? _singleInstanceGuard;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Language.SetLanguage("th");

        _singleInstanceGuard = SingleInstanceGuard.TryAcquire(
            @"Local\TimeLockApp.SingleInstance");

        if (!_singleInstanceGuard.IsOwner)
        {
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceGuard?.Dispose();
        base.OnExit(e);
    }
}
