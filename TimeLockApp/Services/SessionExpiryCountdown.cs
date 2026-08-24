using System.Diagnostics;

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
    private readonly TimeSpan _duration;
    private readonly Func<TimeSpan> _elapsed;

    internal SessionExpiryCountdown(
        int seconds = 60,
        Func<TimeSpan>? elapsed = null)
    {
        if (seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        _duration = TimeSpan.FromSeconds(seconds);
        if (elapsed is null)
        {
            long startedAt = Stopwatch.GetTimestamp();
            _elapsed = () => Stopwatch.GetElapsedTime(startedAt);
        }
        else
        {
            _elapsed = elapsed;
        }
    }

    internal int RemainingSeconds
    {
        get
        {
            double seconds = Math.Ceiling((_duration - _elapsed()).TotalSeconds);
            return seconds <= 0 ? 0 : (int)Math.Min(seconds, int.MaxValue);
        }
    }

    internal SessionExpiryAction Tick()
    {
        if (_terminal)
        {
            return SessionExpiryAction.None;
        }

        if (RemainingSeconds > 0)
        {
            return SessionExpiryAction.None;
        }

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

    internal bool TryCompleteExtension()
    {
        if (_terminal || !_extensionPending || RemainingSeconds <= 0)
        {
            return false;
        }

        _extensionPending = false;
        _terminal = true;
        return true;
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
