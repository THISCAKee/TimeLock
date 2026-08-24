using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeLockApp.Services;

namespace TimeLockApp;

public partial class SessionExpiredWindow : Window
{
    private readonly SessionExpiryCountdown _countdown = new(60);
    private readonly DispatcherTimer _timer;
    private bool _allowClose;

    internal SessionExpiredWindow(ExtensionEligibilityResult eligibility)
    {
        InitializeComponent();

        ExtendButton.Visibility = eligibility.CanExtend
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReasonTextBlock.Text = eligibility.CanExtend
            ? ""
            : ExtensionReasonText(eligibility.Reason);
        UpdateCountdownText();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        Loaded += (_, _) => _timer.Start();
    }

    internal event EventHandler? ExtensionRequested;
    internal event EventHandler? LogoutRequested;

    internal bool HasTimeRemaining => _countdown.RemainingSeconds > 0;

    internal void SetConfirmationPending(bool pending)
    {
        ExtendButton.IsEnabled = !pending;
        if (pending)
        {
            ReasonTextBlock.Text = App.Language.Get("ExtensionChecking");
        }
        else
        {
            _countdown.AllowExtensionRetry();
        }
    }

    internal void ShowRetryableError()
    {
        ReasonTextBlock.Text = App.Language.Get("ExtensionRetry");
        SetConfirmationPending(false);
    }

    internal bool TryClaimExtension() => _countdown.TryCompleteExtension();

    internal void CloseAfterExtension() => CloseFromCode();

    internal void CloseForLogout() => CloseFromCode();

    internal static string ExtensionReasonText(string reason) =>
        reason switch
        {
            "EXTENSION_LIMIT_REACHED" => App.Language.Get("ExtensionLimitReached"),
            "EXTENSION_CROSSES_MIDNIGHT" => App.Language.Get("ExtensionCrossesMidnight"),
            "EXTENSION_NEXT_BOOKING_CONFLICT" => App.Language.Get("ExtensionNextBookingConflict"),
            "EXTENSION_BOOKING_INACTIVE" => App.Language.Get("ExtensionBookingInactive"),
            "EXTENSION_ACCOUNT_MISMATCH" => App.Language.Get("ExtensionAccountMismatch"),
            _ => App.Language.Get("ExtensionUnavailable")
        };

    private void Timer_Tick(object? sender, EventArgs e)
    {
        SessionExpiryAction action = _countdown.Tick();
        UpdateCountdownText();
        if (action == SessionExpiryAction.Logout)
        {
            _timer.Stop();
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ExtendButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_countdown.TryBeginExtension())
        {
            return;
        }

        SetConfirmationPending(true);
        ExtensionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_countdown.SelectLogout() != SessionExpiryAction.Logout)
        {
            return;
        }

        _timer.Stop();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCountdownText()
    {
        int remaining = _countdown.RemainingSeconds;
        CountdownTextBlock.Text = $"{remaining / 60:00}:{remaining % 60:00}";
    }

    private void CloseFromCode()
    {
        _timer.Stop();
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = !_allowClose;
        base.OnClosing(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt &&
            e.SystemKey == Key.F4)
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
