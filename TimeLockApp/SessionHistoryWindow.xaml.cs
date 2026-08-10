using System.Windows;
using TimeLockApp.Data;

namespace TimeLockApp;

public partial class SessionHistoryWindow : Window
{
    private readonly DatabaseService _databaseService;

    //ล้างหน้าต่างนี้จะเป็นหน้าต่างที่แสดงประวัติการใช้งานของผู้ใช้ทั้งหมด โดยจะมีตารางที่แสดงข้อมูลการเข้าสู่ระบบและออกจากระบบของผู้ใช้แต่ละคน รวมถึงเวลาที่ใช้ในการใช้งานด้วย
    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            App.Language.Get("ClearHistoryConfirm"),
            App.Language.Get("ClearHistoryConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _databaseService.ClearAllSessions();
        LoadSessions();

        MessageBox.Show(
            App.Language.Get("HistoryCleared"),
            App.Language.Get("Success"),
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }
    // สร้างหน้าต่างประวัติการใช้งาน
    public SessionHistoryWindow(DatabaseService databaseService)
    {
        InitializeComponent();

        _databaseService = databaseService;

        Loaded += SessionHistoryWindow_Loaded;
    }

    private void SessionHistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSessions();
    }

    private void LoadSessions()
    {
        SessionsDataGrid.ItemsSource = null;
        SessionsDataGrid.ItemsSource = _databaseService.GetAllSessions();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadSessions();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
