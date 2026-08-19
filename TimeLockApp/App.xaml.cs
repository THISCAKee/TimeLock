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

        TimelockConfigurationService configurationService = new();
        TimelockDeviceConfiguration? configuration = configurationService.Load();
        if (configuration is null)
        {
            ProvisioningWindow provisioning = new();
            if (provisioning.ShowDialog() != true || provisioning.Configuration is null)
            {
                Shutdown();
                return;
            }
            configuration = provisioning.Configuration;
            configurationService.Save(configuration);
        }

        var mainWindow = new MainWindow(configuration);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceGuard?.Dispose();
        base.OnExit(e);
    }
}
