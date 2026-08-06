using System.Windows;
using System.Windows.Input;

namespace TimeLockApp;

public partial class AlertWindow : Window
{
    public AlertWindow(string title, string message)
    {
        InitializeComponent();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        Loaded += AlertWindow_Loaded;
    }

    private void AlertWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // อนุญาตให้ปิดได้เฉพาะตอนกด OK เท่านั้น
        if (DialogResult != true)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // กัน Alt+F4, Esc
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnDeactivated(System.EventArgs e)
    {
        base.OnDeactivated(e);

        // ถ้ามีการหลุด focus ให้ดึงกลับ
        Activate();
        Topmost = false;
        Topmost = true;
    }


}