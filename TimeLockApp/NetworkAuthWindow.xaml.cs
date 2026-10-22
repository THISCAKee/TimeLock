using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeLockApp.Services;

namespace TimeLockApp;

public partial class NetworkAuthWindow : Window
{
    private const string WiredAuthenticationUrl =
        "http://10.10.10.10/webAuth/";

    private const string WirelessAuthenticationUrl =
        "https://captive.msu.ac.th:8443/portal/PortalSetup.action?portal=c1bfd1e3-dcdd-4f8c-93ea-1e3d294cf4a4&sessionId=0B0B710A000E749D62D97EBF&action=cwa&redirect=http%3A%2F%2Fwww.msftconnecttest.com%2Fredirect";

    private const string ConnectivityProbeUrl =
        "http://www.msftconnecttest.com/connecttest.txt";

    private const string ConnectivitySuccessHost =
        "www.msftconnecttest.com";

    private const string ConnectivitySuccessPath =
        "/connecttest.txt";

    private const string ConnectivityRedirectPath =
        "/redirect";

    private bool _allowClose;
    private bool _authenticationCompleted;
    private bool _isWirelessAuthentication;
    private bool _isDiscoveringWirelessPortal;
    private string? _discoveredWirelessPortalUrl;
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
            SuccessPanel.Visibility = Visibility.Collapsed;
            AuthWebView.Visibility = Visibility.Visible;

            string userDataFolder = WebView2ProfilePath.GetUserDataFolder();
            Directory.CreateDirectory(userDataFolder);

            CoreWebView2Environment environment =
                await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);

            await AuthWebView.EnsureCoreWebView2Async(environment);

            AuthWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            AuthWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            AuthWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            AuthWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
            AuthWebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;

            AuthWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            AuthWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            AuthWebView.CoreWebView2.Navigate(GetAuthenticationUrl());
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

        if (_isWirelessAuthentication && IsWirelessPortalUrl(e.Uri))
        {
            _discoveredWirelessPortalUrl = e.Uri;
        }

        if (IsConnectivityRedirectUrl(e.Uri))
        {
            e.Cancel = true;
            AuthWebView.CoreWebView2.Navigate(ConnectivityProbeUrl);
        }
    }

    private void CoreWebView2_NavigationCompleted(
     object? sender,
     CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        string currentUrl = AuthWebView.Source?.ToString() ?? "";

        if (!e.IsSuccess)
        {
            if (_isWirelessAuthentication &&
                _isDiscoveringWirelessPortal &&
                IsConnectivitySuccessUrl(currentUrl))
            {
                NavigateToWirelessPortal();
                return;
            }

            ShowError(App.Language.Get("LoadWebFailed", e.WebErrorStatus));
            return;
        }

        if (IsConnectivitySuccessUrl(currentUrl))
        {
            CompleteAuthentication();
            return;
        }

        StatusTextBlock.Text = App.Language.Get("NetworkLoginPrompt");
    }

    private string GetAuthenticationUrl()
    {
        _isWirelessAuthentication = IsWirelessNetworkSelected();
        _isDiscoveringWirelessPortal = _isWirelessAuthentication;
        _discoveredWirelessPortalUrl = null;

        if (_isWirelessAuthentication)
        {
            // Let the captive network redirect to its current portal URL and session ID.
            return ConnectivityProbeUrl;
        }

        return IsWiredNetworkConnected()
            ? WiredAuthenticationUrl
            : ConnectivityProbeUrl;
    }

    private static bool IsWirelessNetworkSelected()
    {
        NetworkInterface? routedInterface = GetDefaultRouteNetworkInterface();

        if (routedInterface?.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
        {
            return true;
        }

        if (routedInterface is not null &&
            IsWiredNetworkType(routedInterface.NetworkInterfaceType))
        {
            return false;
        }

        return IsWirelessNetworkConnected() && !IsWiredNetworkConnected();
    }

    private static NetworkInterface? GetDefaultRouteNetworkInterface()
    {
        try
        {
            using Socket routeSocket = new(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);

            // Connecting a UDP socket selects the OS route without sending a packet.
            routeSocket.Connect(new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53));

            if (routeSocket.LocalEndPoint is not IPEndPoint localEndpoint)
            {
                return null;
            }

            return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(networkInterface =>
                networkInterface.GetIPProperties().UnicastAddresses.Any(address =>
                    address.Address.Equals(localEndpoint.Address)));
        }
        catch (SocketException)
        {
            return null;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static bool IsWiredNetworkConnected()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(networkInterface =>
                IsWiredNetworkType(networkInterface.NetworkInterfaceType) &&
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.GetIPProperties().UnicastAddresses.Any(address =>
                    address.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address.Address)));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static bool IsWirelessNetworkConnected()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(networkInterface =>
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.GetIPProperties().UnicastAddresses.Any(address =>
                    address.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address.Address)));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static bool IsWiredNetworkType(NetworkInterfaceType type)
    {
        return type is NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.Ethernet3Megabit or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.FastEthernetFx or
            NetworkInterfaceType.GigabitEthernet;
    }

    private static bool IsConnectivitySuccessUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            uri.Host.Equals(ConnectivitySuccessHost, StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Equals(ConnectivitySuccessPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConnectivityRedirectUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.Host.Equals(ConnectivitySuccessHost, StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Equals(ConnectivityRedirectPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWirelessPortalUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.Host.Equals("captive.msu.ac.th", StringComparison.OrdinalIgnoreCase);
    }

    private void NavigateToWirelessPortal()
    {
        if (AuthWebView.CoreWebView2 is null)
        {
            return;
        }

        _isDiscoveringWirelessPortal = false;
        LoadingPanel.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        SuccessPanel.Visibility = Visibility.Collapsed;
        AuthWebView.Visibility = Visibility.Visible;
        StatusTextBlock.Text = App.Language.Get("ConnectingAuth");

        AuthWebView.CoreWebView2.Navigate(
            _discoveredWirelessPortalUrl ?? WirelessAuthenticationUrl);
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

                if (_isWirelessAuthentication)
                {
                    NavigateToWirelessPortal();
                }

                _isCheckingConnection = false;
                return;
            }

            _authenticationCompleted = true;
            _isCheckingConnection = false;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            AuthWebView.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            StatusTextBlock.Text = App.Language.Get("AuthSuccessReady");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text =
                App.Language.Get("InternetCheckFailed", ex.Message);

            _isCheckingConnection = false;
        }
    }

    private void BackToLoginButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _allowClose = true;
        DialogResult = _authenticationCompleted;
        Close();
    }

    private async void RetryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
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
