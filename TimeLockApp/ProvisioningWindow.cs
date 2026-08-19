using System.Windows;
using System.Windows.Controls;
using TimeLockApp.Services;

namespace TimeLockApp;

internal sealed class ProvisioningWindow : Window
{
    private readonly TextBox _machineCode = new() { Margin = new Thickness(0, 4, 0, 12) };
    private readonly PasswordBox _deviceToken = new() { Margin = new Thickness(0, 4, 0, 12) };
    private readonly TextBox _backendUrl = new() { Text = "https://booking-ai-lab.vercel.app", Margin = new Thickness(0, 4, 0, 12) };
    private readonly PasswordBox _adminPassword = new() { Margin = new Thickness(0, 4, 0, 12) };
    private readonly PasswordBox _confirmPassword = new() { Margin = new Thickness(0, 4, 0, 12) };
    private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    internal TimelockDeviceConfiguration? Configuration { get; private set; }

    internal ProvisioningWindow()
    {
        Title = "ตั้งค่า TimeLockApp ครั้งแรก";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;

        StackPanel panel = new() { Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock { Text = "เชื่อมเครื่องกับ BookingAiLab", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) });
        AddField(panel, "Machine Code เช่น PC-001", _machineCode);
        AddField(panel, "Device Token จากหน้าจัดการเครื่อง", _deviceToken);
        AddField(panel, "Backend URL", _backendUrl);
        AddField(panel, "รหัสผ่าน Local Admin", _adminPassword);
        AddField(panel, "ยืนยันรหัสผ่าน Local Admin", _confirmPassword);
        panel.Children.Add(_error);
        Button save = new() { Content = "บันทึกและเริ่มใช้งาน", Height = 44, Margin = new Thickness(0, 18, 0, 0) };
        save.Click += Save_Click;
        panel.Children.Add(save);
        Content = panel;
    }

    private static void AddField(Panel panel, string label, Control control)
    {
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.Medium });
        panel.Children.Add(control);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_adminPassword.Password.Length < 10)
        {
            _error.Text = "รหัสผ่าน Local Admin ต้องมีอย่างน้อย 10 ตัวอักษร";
            return;
        }
        if (_adminPassword.Password != _confirmPassword.Password)
        {
            _error.Text = "ยืนยันรหัสผ่าน Local Admin ไม่ตรงกัน";
            return;
        }
        try
        {
            Configuration = TimelockDeviceConfiguration.Create(
                _machineCode.Text,
                _deviceToken.Password,
                _backendUrl.Text,
                PasswordVerifier.Create(_adminPassword.Password));
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            _error.Text = ex.Message;
        }
    }
}
