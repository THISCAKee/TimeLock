using System;
using System.Windows;
using System.Windows.Input;

namespace TimeLockApp;

public partial class UsageWindow : Window
{
    public event EventHandler? LogoutRequested;

    public UsageWindow()
    {
        InitializeComponent();

        Loaded += UsageWindow_Loaded;
    }

    private void UsageWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Right - Width - 20;
        Top = SystemParameters.WorkArea.Top + 20;
    }

    public void UpdateRemainingTime(int remainingSeconds)
    {
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;

        RemainingTimeTextBlock.Text = $"{minutes:00}:{seconds:00}";
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void LogoutButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            this,
            "ต้องการออกจากระบบหรือไม่?",
            "ยืนยันการออกจากระบบ",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}
