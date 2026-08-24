namespace TimeLockApp.Services;

internal enum SessionExpiryAction
{
    None,
    Logout
}

internal sealed class SessionExpiryCountdown
{
    private bool _terminal;
    private bool _extensionPending;

    internal SessionExpiryCountdown(int seconds = 60)
    {
        if (seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        RemainingSeconds = seconds;
    }

    internal int RemainingSeconds { get; private set; }

    internal SessionExpiryAction Tick()
    {
        if (_terminal || _extensionPending)
        {
            return SessionExpiryAction.None;
        }

        RemainingSeconds--;
        if (RemainingSeconds > 0)
        {
            return SessionExpiryAction.None;
        }

        RemainingSeconds = 0;
        _terminal = true;
        return SessionExpiryAction.Logout;
    }

    internal bool TryBeginExtension()
    {
        if (_terminal || _extensionPending || RemainingSeconds <= 0)
        {
            return false;
        }

        _extensionPending = true;
        return true;
    }

    internal void AllowExtensionRetry()
    {
        if (!_terminal)
        {
            _extensionPending = false;
        }
    }

    internal void CompleteExtension()
    {
        _extensionPending = false;
        _terminal = true;
    }

    internal SessionExpiryAction SelectLogout()
    {
        if (_terminal)
        {
            return SessionExpiryAction.None;
        }

        _terminal = true;
        _extensionPending = false;
        return SessionExpiryAction.Logout;
    }
}
