using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeLockApp.Data;
using TimeLockApp.Services;

namespace TimeLockApp;

public partial class MainWindow : Window
{
    private const string UserWebsiteUrl =
        "https://libmsu-ai.vercel.app/";

    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int LlkhfAltDown = 0x20;
    private const int VkControl = 0x11;

    private readonly UserSyncService _userSyncService;
    private readonly AutomaticSyncOrchestrator _automaticSync;
    private readonly DispatcherTimer _automaticSyncTimer;
    private readonly ChromeLauncherService _chromeLauncherService = new();

    private readonly DatabaseService _databaseService = new();
    private readonly LowLevelKeyboardProc _keyboardProc;

    private DispatcherTimer? _timer;
    private UsageWindow? _usageWindow;
    private int _remainingSeconds;
    private bool _isSessionActive;
    private bool _isAlertOpen;
    private bool _isAdminPanelOpen;
    private int _currentSessionId;
    private int _currentUserId;
    private int _sessionTotalSeconds;
    private bool _sessionEnded;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private bool _isNetworkAuthOpen;
    private readonly InternetConnectivityService _connectivityService = new();
    private bool _startupConnectivityChecked;
    private AdminWindow? _adminWindow;
    private bool _isShuttingDown;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MainWindow()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
        InitializeComponent();

        _keyboardProc = KeyboardHookCallback;

        _databaseService.InitializeDatabase();
        _databaseService.RecoverInterruptedSessions(DateTime.Now);

        var googleSheetsUserService =
            new GoogleSheetsUserService();

        _userSyncService = new UserSyncService(
            googleSheetsUserService,
            _databaseService);

        _automaticSync = new AutomaticSyncOrchestrator(
            _userSyncService.SynchronizeAsync);
        _automaticSync.Completed += AutomaticSync_Completed;

        _automaticSyncTimer = new DispatcherTimer
        {
            Interval = AutomaticSyncOrchestrator.Interval
        };
        _automaticSyncTimer.Tick += AutomaticSyncTimer_Tick;

        Loaded += MainWindow_Loaded;

    }
    private async void NetworkAuthButton_Click(
     object sender,
     RoutedEventArgs e)
    {
        await OpenNetworkAuthenticationAsync();
    }
    private async Task OpenNetworkAuthenticationAsync()
    {
        if (_isNetworkAuthOpen)
        {
            return;
        }

        _isNetworkAuthOpen = true;
        NetworkAuthButton.IsEnabled = false;

        NetworkStatusTextBlock.Text =
            "กำลังเปิดระบบ Authen Internet...";

        Topmost = false;
        Hide();

        try
        {
            NetworkAuthWindow networkAuthWindow =
                new NetworkAuthWindow();

            bool? result = networkAuthWindow.ShowDialog();

            if (result == true &&
                networkAuthWindow.AuthenticationCompleted)
            {
                NetworkStatusTextBlock.Text =
                    "เชื่อมต่ออินเทอร์เน็ตสำเร็จ กำลังซิงค์ข้อมูล...";

                await _automaticSync.RunAsync(
                    AutomaticSyncTrigger.InternetAuthenticated);
            }
            else
            {
                NetworkStatusTextBlock.Text =
                    "ยกเลิก Authen กรุณาเชื่อมต่ออีกครั้งเมื่อต้องการใช้อินเทอร์เน็ต";
            }
        }
        catch (Exception ex)
        {
            NetworkStatusTextBlock.Text =
                $"ไม่สามารถเปิดระบบ Authen ได้: {ex.Message}";
        }
        finally
        {
            _isNetworkAuthOpen = false;
            NetworkAuthButton.IsEnabled = true;

            Show();
            WindowState = WindowState.Maximized;
            Topmost = true;
            Activate();
            Focus();

            UsernameTextBox.Focus();
        }
    }
    private static IntPtr InstallKeyboardHook(LowLevelKeyboardProc keyboardProc)
    {
        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessModule? currentModule = currentProcess.MainModule;
        IntPtr moduleHandle = GetModuleHandle(currentModule?.ModuleName);

        return SetWindowsHookEx(WhKeyboardLl, keyboardProc, moduleHandle, 0);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 &&
            (wParam == (IntPtr)WmKeyDown ||
             wParam == (IntPtr)WmSysKeyDown))
        {
            KeyboardHookData keyboardData = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            int virtualKey = unchecked((int)keyboardData.VirtualKeyCode);
            bool altPressed = (keyboardData.Flags & LlkhfAltDown) != 0;
            bool controlPressed = (GetAsyncKeyState(VkControl) & 0x8000) != 0;

            bool shouldBlock =
                SystemShortcutPolicy.ShouldBlock(
                    _isSessionActive,
                    _isAdminPanelOpen,
                    _isNetworkAuthOpen,
                    _isAlertOpen);

            bool isBlockedShortcut =
                SystemShortcutPolicy.IsBlockedShortcut(
                    virtualKey,
                    altPressed,
                    controlPressed);

            if (shouldBlock && isBlockedShortcut)
            {
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }
    private async void MainWindow_Loaded(
    object sender,
    RoutedEventArgs e)
    {
        if (!EnsureKeyboardHookInstalled())
        {
            return;
        }

        _automaticSyncTimer.Start();

        if (_startupConnectivityChecked)
        {
            return;
        }

        _startupConnectivityChecked = true;

        NetworkStatusTextBlock.Text =
            "กำลังตรวจสอบการเชื่อมต่ออินเทอร์เน็ต...";

        bool hasInternet =
            await _connectivityService.HasInternetAccessAsync();

        if (hasInternet)
        {
            await _automaticSync.RunAsync(
                AutomaticSyncTrigger.Startup);

            UsernameTextBox.Focus();
            return;
        }

        NetworkStatusTextBlock.Text =
            "ยังไม่ได้ Authen Internet";

        await OpenNetworkAuthenticationAsync();
    }

    private bool EnsureKeyboardHookInstalled()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            return true;
        }

        _keyboardHook = InstallKeyboardHook(_keyboardProc);

        if (_keyboardHook != IntPtr.Zero)
        {
            return true;
        }

        int errorCode = Marshal.GetLastWin32Error();

        MessageBox.Show(
            $"ไม่สามารถเปิดระบบล็อกแป้นพิมพ์ได้ (Win32: {errorCode})",
            "เริ่มระบบล็อกไม่สำเร็จ",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Application.Current.Shutdown(-1);
        return false;
    }
    private void UsernameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UsernamePlaceholderTextBlock.Visibility =
            string.IsNullOrWhiteSpace(UsernameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        PasswordPlaceholderTextBlock.Visibility =
            string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
    private void OpenAdminPanel()
    {
        _isAdminPanelOpen = true;

        Hide();

        AdminWindow adminWindow = new(
            _databaseService,
            _userSyncService);

        _adminWindow = adminWindow;

        try
        {
            adminWindow.ShowDialog();
        }
        finally
        {
            _adminWindow = null;
        }

        _isAdminPanelOpen = false;

        UsernameTextBox.Text = "";
        PasswordBox.Password = "";
        MessageTextBlock.Text = "";

        UsernamePlaceholderTextBlock.Visibility = Visibility.Visible;
        PasswordPlaceholderTextBlock.Visibility = Visibility.Visible;

        if (adminWindow.BackToLoginRequested)
        {
            Show();
            ActivateLoginWindow();
        }
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string username = UsernameTextBox.Text.Trim();
        string password = PasswordBox.Password;

        UserRecord? user = _databaseService.GetUserByUsernameAndPassword(username, password);

        if (user == null)
        {
            MessageTextBlock.Text = "Username หรือ Password ไม่ถูกต้อง";
            return;
        }

        if (user.Role == "admin")
        {
            OpenAdminPanel();
            return;
        }

        StartSession(user);
    }

    private void StartSession(UserRecord user)
    {
        _isSessionActive = true;
        _sessionEnded = false;

        _sessionTotalSeconds = user.AllowedMinutes * 60;
        _remainingSeconds = _sessionTotalSeconds;
        _currentUserId = user.Id;

        _currentSessionId = _databaseService.StartSession(user);

        Hide();

        _usageWindow = new UsageWindow();
        _usageWindow.LogoutRequested +=
            UsageWindow_LogoutRequested;

        _usageWindow.UpdateRemainingTime(_remainingSeconds);
        _usageWindow.Show();

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        ChromeLaunchResult launchResult =
            _chromeLauncherService.TryOpen(UserWebsiteUrl);

        if (!launchResult.IsSuccessful && _usageWindow != null)
        {
            MessageBox.Show(
                _usageWindow,
                launchResult.ErrorMessage,
                "ไม่สามารถเปิดเว็บไซต์ได้",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void UsageWindow_LogoutRequested(
        object? sender,
        EventArgs e)
    {
        await EndSessionAsync(
            "logged_out",
            showExpiredAlert: false);
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isAlertOpen)
        {
            return;
        }

        int previousSeconds = _remainingSeconds;
        _remainingSeconds--;

        _usageWindow?.UpdateRemainingTime(_remainingSeconds);

        SessionWarning? warning =
            SessionWarningSchedule.GetCrossedWarning(
                previousSeconds,
                _remainingSeconds);

        if (warning != null)
        {
            ShowBlockingAlert(
                "แจ้งเตือน",
                warning.Message
            );
        }

        if (_remainingSeconds <= 0)
        {
            await EndSessionAsync(
                "completed",
                showExpiredAlert: true);
        }
    }

    private async Task EndSessionAsync(
        string status,
        bool showExpiredAlert)
    {
        if (_sessionEnded)
        {
            return;
        }

        _sessionEnded = true;

        _timer?.Stop();
        _timer = null;

        int usedSeconds = _sessionTotalSeconds - _remainingSeconds;

        if (usedSeconds < 0)
        {
            usedSeconds = 0;
        }

        if (_currentSessionId > 0 &&
            _currentUserId > 0)
        {
            _databaseService.EndSessionAndDeactivateUser(
                _currentSessionId,
                _currentUserId,
                usedSeconds,
                status);
        }

        _currentSessionId = 0;
        _currentUserId = 0;

        _isSessionActive = false;

        if (_usageWindow != null)
        {
            _usageWindow.LogoutRequested -=
                UsageWindow_LogoutRequested;

            _usageWindow.Hide();
            _usageWindow = null;
        }

        UsernameTextBox.Text = "";
        PasswordBox.Password = "";
        MessageTextBlock.Text = "";

        UsernamePlaceholderTextBlock.Visibility = Visibility.Visible;
        PasswordPlaceholderTextBlock.Visibility = Visibility.Visible;

        Show();

        Activate();
        Focus();
        Topmost = false;
        Topmost = true;

        if (showExpiredAlert)
        {
            ShowBlockingAlert(
                "หมดเวลา",
                "หมดเวลาใช้งานแล้ว กรุณากด OK เพื่อกลับสู่หน้า Login"
            );
        }

        ActivateLoginWindow();

        AutomaticSyncTrigger syncTrigger =
            status == "logged_out"
                ? AutomaticSyncTrigger.Logout
                : AutomaticSyncTrigger.SessionExpired;

        await _automaticSync.RunAsync(syncTrigger);
    }

    private void ShowBlockingAlert(string title, string message)
    {
        _isAlertOpen = true;

        AlertWindow alertWindow = new AlertWindow(title, message)
        {
            Owner = this
        };

        alertWindow.ShowDialog();

        _isAlertOpen = false;
    }

    private void ActivateLoginWindow()
    {
        if (_isSessionActive ||
         _isAdminPanelOpen ||
         _isNetworkAuthOpen)
        {
            return;
        }

        Show();
        WindowState = WindowState.Maximized;
        Topmost = true;
        Activate();
        Focus();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // ดึงหน้า Login กลับมาเมื่อยังไม่ได้ login และไม่ได้อยู่หน้า Admin
        if (!_isSessionActive &&
         !_isAlertOpen &&
         !_isAdminPanelOpen &&
         !_isNetworkAuthOpen)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActivateLoginWindow();
            }), DispatcherPriority.ApplicationIdle);
        }
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // กัน Alt+F4
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            return;
        }

        // พยายามกัน Alt+Tab ระหว่างอยู่หน้า Login
        if (!_isSessionActive &&
            (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt &&
            e.SystemKey == Key.Tab)
        {
            e.Handled = true;
            ActivateLoginWindow();
            return;
        }

        // กันปุ่ม Windows บางกรณีไม่ได้จาก WPF ปกติ
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _isShuttingDown = true;
        _automaticSyncTimer.Stop();
        _automaticSync.Completed -= AutomaticSync_Completed;

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        base.OnClosed(e);
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int code,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    private async void AutomaticSyncTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _automaticSyncTimer.Stop();

        try
        {
            await _automaticSync.RunAsync(
                AutomaticSyncTrigger.Periodic);
        }
        finally
        {
            if (!_isShuttingDown)
            {
                _automaticSyncTimer.Start();
            }
        }
    }

    private void AutomaticSync_Completed(
        object? sender,
        AutomaticSyncCompletedEventArgs e)
    {
        DateTime completedAt = DateTime.Now;
        string status = AutomaticSyncStatus.Format(
            e.Result,
            completedAt);

        NetworkStatusTextBlock.Text = status;
        _adminWindow?.ApplyAutomaticSyncResult(
            e.Result,
            completedAt);

        Debug.WriteLine(
            $"Automatic sync ({e.Trigger}): {status}");
    }
}
