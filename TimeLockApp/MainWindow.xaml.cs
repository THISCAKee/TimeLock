using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeLockApp.Data;

namespace TimeLockApp;

public partial class MainWindow : Window
{
    private readonly DatabaseService _databaseService = new();

    private DispatcherTimer? _timer;
    private UsageWindow? _usageWindow;
    private int _remainingSeconds;
    private bool _isSessionActive;
    private bool _isAlertOpen;
    private bool _isAdminPanelOpen;
    private int _currentSessionId;
    private int _sessionTotalSeconds;
    private bool _sessionEnded;

    public MainWindow()
    {
        InitializeComponent();

        _databaseService.InitializeDatabase();
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
        if (_isSessionActive || _isAdminPanelOpen)
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
        if (!_isSessionActive && !_isAlertOpen && !_isAdminPanelOpen)
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
}