using System.Windows;
using System.Windows.Controls;
using TimeLockApp.Data;

namespace TimeLockApp;

public partial class AdminWindow : Window
{
    private readonly DatabaseService _databaseService;
    private UserRecord? _selectedUser;

    public bool BackToLoginRequested { get; private set; }

    public AdminWindow(DatabaseService databaseService)
    {
        InitializeComponent();

        _databaseService = databaseService;

        Loaded += AdminWindow_Loaded;
    }
    private void SessionHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        SessionHistoryWindow sessionHistoryWindow = new SessionHistoryWindow(_databaseService);
        sessionHistoryWindow.Owner = this;
        sessionHistoryWindow.ShowDialog();
    }

    private void AdminWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RoleComboBox.SelectedIndex = 0;
        LoadUsers();
    }

    private void LoadUsers()
    {
        UsersDataGrid.ItemsSource = null;
        UsersDataGrid.ItemsSource = _databaseService.GetAllUsers();
    }

    private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UsersDataGrid.SelectedItem is not UserRecord user)
        {
            return;
        }

        _selectedUser = user;

        UsernameTextBox.Text = user.Username;
        PasswordTextBox.Text = user.Password;
        AllowedMinutesTextBox.Text = user.AllowedMinutes.ToString();

        RoleComboBox.SelectedIndex = user.Role == "admin" ? 1 : 0;
        MessageTextBlock.Text = "";
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadForm(out string username, out string password, out int allowedMinutes, out string role))
        {
            return;
        }

        bool success = _databaseService.AddUser(username, password, allowedMinutes, role);

        if (!success)
        {
            MessageTextBlock.Text = "เพิ่ม user ไม่สำเร็จ อาจมี username นี้อยู่แล้ว";
            return;
        }

        MessageTextBlock.Text = "เพิ่ม user สำเร็จ";
        ClearForm();
        LoadUsers();
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null)
        {
            MessageTextBlock.Text = "กรุณาเลือก user ที่ต้องการแก้ไข";
            return;
        }

        if (!TryReadForm(out string username, out string password, out int allowedMinutes, out string role))
        {
            return;
        }

        bool success = _databaseService.UpdateUser(
            _selectedUser.Id,
            username,
            password,
            allowedMinutes,
            role
        );

        if (!success)
        {
            MessageTextBlock.Text = "แก้ไข user ไม่สำเร็จ";
            return;
        }

        MessageTextBlock.Text = "แก้ไข user สำเร็จ";
        ClearForm();
        LoadUsers();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null)
        {
            MessageTextBlock.Text = "กรุณาเลือก user ที่ต้องการลบ";
            return;
        }

        if (_selectedUser.Role == "admin")
        {
            MessageTextBlock.Text = "ไม่แนะนำให้ลบ admin ผ่านหน้านี้";
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            $"ต้องการลบ user '{_selectedUser.Username}' ใช่หรือไม่?",
            "ยืนยันการลบ",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        bool success = _databaseService.DeleteUser(_selectedUser.Id);

        if (!success)
        {
            MessageTextBlock.Text = "ลบ user ไม่สำเร็จ";
            return;
        }

        MessageTextBlock.Text = "ลบ user สำเร็จ";
        ClearForm();
        LoadUsers();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearForm();
    }

    private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
    {
        BackToLoginRequested = true;
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private bool TryReadForm(out string username, out string password, out int allowedMinutes, out string role)
    {
        username = UsernameTextBox.Text.Trim();
        password = PasswordTextBox.Text.Trim();
        role = "user";
        allowedMinutes = 0;

        if (RoleComboBox.SelectedItem is ComboBoxItem selectedRole &&
            selectedRole.Content is string selectedRoleText)
        {
            role = selectedRoleText;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageTextBlock.Text = "กรุณากรอก username";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            MessageTextBlock.Text = "กรุณากรอก password";
            return false;
        }

        if (!int.TryParse(AllowedMinutesTextBox.Text.Trim(), out allowedMinutes))
        {
            MessageTextBlock.Text = "Allowed Minutes ต้องเป็นตัวเลข";
            return false;
        }

        if (allowedMinutes < 0)
        {
            MessageTextBlock.Text = "Allowed Minutes ต้องไม่ติดลบ";
            return false;
        }

        if (role == "user" && allowedMinutes <= 0)
        {
            MessageTextBlock.Text = "user ปกติต้องมีเวลามากกว่า 0 นาที";
            return false;
        }

        return true;
    }

    private void ClearForm()
    {
        _selectedUser = null;
        UsersDataGrid.SelectedItem = null;

        UsernameTextBox.Text = "";
        PasswordTextBox.Text = "";
        AllowedMinutesTextBox.Text = "";
        RoleComboBox.SelectedIndex = 0;
    }
}