using System.Windows;
using System.Windows.Controls;
using TimeLockApp.Data;
using TimeLockApp.Models;
using TimeLockApp.Services;

namespace TimeLockApp;

public partial class AdminWindow : Window
{
    public static readonly DependencyProperty IsPasswordVisibleProperty =
        DependencyProperty.Register(
            nameof(IsPasswordVisible),
            typeof(bool),
            typeof(AdminWindow),
            new PropertyMetadata(false));

    private readonly DatabaseService _databaseService;
    private UserRecord? _selectedUser;
    private readonly Func<CancellationToken, Task<UserSyncResult>> _synchronize;

    public bool BackToLoginRequested { get; private set; }

    public bool IsPasswordVisible
    {
        get => (bool)GetValue(IsPasswordVisibleProperty);
        set => SetValue(IsPasswordVisibleProperty, value);
    }

    public AdminWindow(
        DatabaseService databaseService,
        Func<CancellationToken, Task<UserSyncResult>> synchronize)
    {
        InitializeComponent();

        _databaseService = databaseService;
        _synchronize = synchronize;

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

    private IReadOnlyList<UserRecord> LoadUsers()
    {
        IReadOnlyList<UserRecord> users =
            _databaseService.GetAllUsers();

        UsersDataGrid.ItemsSource = null;
        UsersDataGrid.ItemsSource = users;

        return users;
    }

    internal void ApplyAutomaticSyncResult(
        UserSyncResult result,
        DateTime completedAt)
    {
        MessageTextBlock.Text =
            AutomaticSyncStatus.Format(result, completedAt);

        if (!result.IsSuccessful || !result.HasChanges)
        {
            return;
        }

        int? selectedUserId = _selectedUser?.Id;
        IReadOnlyList<UserRecord> users = LoadUsers();

        if (!selectedUserId.HasValue)
        {
            return;
        }

        UserRecord? selectedUser = users.FirstOrDefault(
            user => user.Id == selectedUserId.Value);

        if (selectedUser == null)
        {
            ClearForm();
            return;
        }

        UsersDataGrid.SelectedItem = selectedUser;
        UsersDataGrid.ScrollIntoView(selectedUser);
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

    private void PasswordVisibilityButton_Click(object sender, RoutedEventArgs e)
    {
        IsPasswordVisible = !IsPasswordVisible;
        PasswordVisibilityButton.Content =
            IsPasswordVisible
                ? App.Language.Get("Hide")
                : App.Language.Get("Show");
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
            MessageTextBlock.Text = App.Language.Get("AddFailed");
            return;
        }

        MessageTextBlock.Text = App.Language.Get("AddSuccess");
        ClearForm();
        LoadUsers();
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null)
        {
            MessageTextBlock.Text = App.Language.Get("SelectEditUser");
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
            MessageTextBlock.Text = App.Language.Get("EditFailed");
            return;
        }

        MessageTextBlock.Text = App.Language.Get("EditSuccess");
        ClearForm();
        LoadUsers();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null)
        {
            MessageTextBlock.Text = App.Language.Get("SelectDeleteUser");
            return;
        }

        if (_selectedUser.Role == "admin")
        {
            MessageTextBlock.Text = App.Language.Get("DeleteAdminWarning");
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            App.Language.Get("DeleteConfirm", _selectedUser.Username),
            App.Language.Get("DeleteConfirmTitle"),
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
            MessageTextBlock.Text = App.Language.Get("DeleteFailed");
            return;
        }

        MessageTextBlock.Text = App.Language.Get("DeleteSuccess");
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
        RequestApplicationShutdown();
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            App.Language.Get("UninstallConfirm"),
            App.Language.Get("UninstallConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (!ApplicationUninstaller.TryStart(
                AppContext.BaseDirectory,
                out string errorMessage))
        {
            MessageTextBlock.Text =
                App.Language.Get("UninstallFailed", errorMessage);
            return;
        }

        RequestApplicationShutdown();
    }

    private void RequestApplicationShutdown()
    {
        if (Owner is MainWindow mainWindow)
        {
            mainWindow.RequestApplicationShutdown();
            return;
        }

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
            MessageTextBlock.Text = App.Language.Get("EnterUsername");
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            MessageTextBlock.Text = App.Language.Get("EnterPassword");
            return false;
        }

        if (!int.TryParse(AllowedMinutesTextBox.Text.Trim(), out allowedMinutes))
        {
            MessageTextBlock.Text = App.Language.Get("MinutesNumber");
            return false;
        }

        if (allowedMinutes < 0)
        {
            MessageTextBlock.Text = App.Language.Get("MinutesNonNegative");
            return false;
        }

        if (role == "user" && allowedMinutes <= 0)
        {
            MessageTextBlock.Text = App.Language.Get("MinutesPositive");
            return false;
        }

        return true;
    }

    private async void SyncUsersButton_Click(
     object sender,
     RoutedEventArgs e)
    {
        MessageTextBlock.Text = App.Language.Get("SyncingUsers");

        SyncUsersButton.IsEnabled = false;

        try
        {
            UserSyncResult result =
                await _synchronize(CancellationToken.None);

            if (!result.IsSuccessful)
            {
                MessageTextBlock.Text =
                    App.Language.Get("SyncFailed", result.ErrorMessage);

                return;
            }

            LoadUsers();

            MessageTextBlock.Text =
                App.Language.Get("SyncSuccess", result.UserCount);
        }
        finally
        {
            SyncUsersButton.IsEnabled = true;
        }
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
