using Microsoft.Web.WebView2.Core;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace TimeLockApp;

public partial class NetworkAuthWindow : Window
{
    private const string AuthenticationUrl =
        "http://10.99.92.1/webAuth/index.htm?www.msftconnecttest.com/redirect";

    private const string SuccessUrl =
        "https://authen.msu.ac.th/";

    private bool _allowClose;
    private bool _authenticationCompleted;
    private readonly InternetConnectivityService _connectivityService = new();
    private bool _isCheckingConnection;
    public bool AuthenticationCompleted => _authenticationCompleted;

    public NetworkAuthWindow()
    {

        InitializeComponent();

        Loaded += NetworkAuthWindow_Loaded;
    }

    private async void NetworkAuthWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async System.Threading.Tasks.Task InitializeWebViewAsync()
    {
        try
        {
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;

            await AuthWebView.EnsureCoreWebView2Async();

            AuthWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            AuthWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            AuthWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            AuthWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
            AuthWebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;

            AuthWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            AuthWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            AuthWebView.CoreWebView2.Navigate(AuthenticationUrl);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void CoreWebView2_NavigationStarting(
    object? sender,
    CoreWebView2NavigationStartingEventArgs e)
    {
        StatusTextBlock.Text = App.Language.Get("ConnectingAuth");

        if (IsAuthenticationSuccessUrl(e.Uri))
        {
            CompleteAuthentication();
        }
    }

    private void CoreWebView2_NavigationCompleted(
     object? sender,
     CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;

        if (!e.IsSuccess)
        {
            ShowError(App.Language.Get("LoadWebFailed", e.WebErrorStatus));
            return;
        }

        string currentUrl = AuthWebView.Source?.ToString() ?? "";

        if (IsAuthenticationSuccessUrl(currentUrl))
        {
            CompleteAuthentication();
            return;
        }

        StatusTextBlock.Text = App.Language.Get("NetworkLoginPrompt");
    }
    private static bool IsAuthenticationSuccessUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.StartsWith(
            SuccessUrl,
            StringComparison.OrdinalIgnoreCase);
    }

    private async void CompleteAuthentication()
    {
        if (_authenticationCompleted || _isCheckingConnection)
        {
            return;
        }

        _isCheckingConnection = true;

        StatusTextBlock.Text = App.Language.Get("AuthSuccessChecking");

        try
        {
            await System.Threading.Tasks.Task.Delay(1500);

            bool hasInternet =
                await _connectivityService.HasInternetAccessAsync();

            if (!hasInternet)
            {
                StatusTextBlock.Text = App.Language.Get("AuthSuccessNoInternet");

                _isCheckingConnection = false;
                return;
            }

            _authenticationCompleted = true;
            _allowClose = true;

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text =
                App.Language.Get("InternetCheckFailed", ex.Message);

            _isCheckingConnection = false;
        }
    }

    private async void RetryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _allowClose = true;
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;

        ErrorTextBlock.Text = message;
        StatusTextBlock.Text = App.Language.Get("NetworkConnectionFailed");
    }

    private void Window_PreviewKeyDown(
     object sender,
     KeyEventArgs e)
    {
        bool isAltPressed =
            (Keyboard.Modifiers & ModifierKeys.Alt) ==
            ModifierKeys.Alt;

        if (isAltPressed &&
            (e.SystemKey == Key.F4 || e.SystemKey == Key.Tab))
        {
            e.Handled = true;

            Activate();
            Topmost = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            WindowState = WindowState.Maximized;
            Topmost = true;
            Activate();
            Focus();
        }), DispatcherPriority.ApplicationIdle);
    }

    protected override void OnClosing(
        System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
