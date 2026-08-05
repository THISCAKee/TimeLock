using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeLockApp.Data;

namespace TimeLockApp;

public partial class MainWindow : Window
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int LlkhfAltDown = 0x20;
    private const int VkTab = 0x09;
    private const int VkEscape = 0x1B;
    private const int VkF4 = 0x73;
    private const int VkLwin = 0x5B;
    private const int VkRwin = 0x5C;
    private const int VkControl = 0x11;

    private readonly DatabaseService _databaseService = new();
    private readonly LowLevelKeyboardProc _keyboardProc;

    private DispatcherTimer? _timer;
    private UsageWindow? _usageWindow;
    private int _remainingSeconds;
    private bool _isSessionActive;
    private bool _isAlertOpen;
    private bool _isAdminPanelOpen;
    private int _currentSessionId;
    private int _sessionTotalSeconds;
    private bool _sessionEnded;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private bool _isNetworkAuthOpen;
    private readonly InternetConnectivityService _connectivityService = new();
    private bool _startupConnectivityChecked;

    public MainWindow()
    {
        InitializeComponent();

        _keyboardProc = KeyboardHookCallback;
        _keyboardHook = InstallKeyboardHook(_keyboardProc);

        _databaseService.InitializeDatabase();

        Loaded += MainWindow_Loaded;


    }
    private void NetworkAuthButton_Click(
     object sender,
     RoutedEventArgs e)
    {
        OpenNetworkAuthentication();
    }
    private void OpenNetworkAuthentication()
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
                    "เชื่อมต่ออินเทอร์เน็ตสำเร็จ สามารถเข้าสู่ระบบได้";
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
    private bool IsLoginLocked => !_isSessionActive && !_isAdminPanelOpen;

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
            IsLoginLocked &&
            (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown))
        {
            KeyboardHookData keyboardData = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            int virtualKey = unchecked((int)keyboardData.VirtualKeyCode);
            bool altPressed = (keyboardData.Flags & LlkhfAltDown) != 0;
            bool controlPressed = (GetAsyncKeyState(VkControl) & 0x8000) != 0;

            bool isBlockedShortcut =
                virtualKey == VkLwin ||
                virtualKey == VkRwin ||
                (altPressed && (virtualKey == VkTab || virtualKey == VkEscape || virtualKey == VkF4)) ||
                (controlPressed && virtualKey == VkEscape);

            if (isBlockedShortcut)
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
            NetworkStatusTextBlock.Text =
                "เชื่อมต่ออินเทอร์เน็ตแล้ว";

            UsernameTextBox.Focus();
            return;
        }

        NetworkStatusTextBlock.Text =
            "ยังไม่ได้ Authen Internet";

        OpenNetworkAuthentication();
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

        AdminWindow adminWindow = new AdminWindow(_databaseService);
        adminWindow.ShowDialog();

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

        _currentSessionId = _databaseService.StartSession(user);

        Hide();

        _usageWindow = new UsageWindow();
        _usageWindow.UpdateRemainingTime(_remainingSeconds);
        _usageWindow.Show();

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isAlertOpen)
        {
            return;
        }

        _remainingSeconds--;

        _usageWindow?.UpdateRemainingTime(_remainingSeconds);

        if (_remainingSeconds == 10)
        {
            ShowBlockingAlert(
                "แจ้งเตือน",
                "เหลือเวลาใช้งานอีก 10 วินาที"
            );
        }

        if (_remainingSeconds <= 0)
        {
            EndSession();
        }
    }

    private void EndSession()
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

        if (_currentSessionId > 0)
        {
            _databaseService.EndSession(
                _currentSessionId,
                usedSeconds,
                "completed"
            );
        }

        _isSessionActive = false;

        if (_usageWindow != null)
        {
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

        ShowBlockingAlert(
            "หมดเวลา",
            "หมดเวลาใช้งานแล้ว กรุณากด OK เพื่อกลับสู่หน้า Login"
        );

        ActivateLoginWindow();
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
}
