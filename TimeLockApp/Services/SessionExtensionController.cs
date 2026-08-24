using System.Net.Http;

namespace TimeLockApp.Services;

internal enum ExtensionConfirmOutcome
{
    Extended,
    RetryableFailure,
    Rejected
}

internal sealed record ExtensionEligibilityResult(
    bool CanExtend,
    string Reason);

internal sealed record ExtensionConfirmationResult(
    ExtensionConfirmOutcome Outcome,
    TimelockExtensionConfirmation? Confirmation,
    string? ErrorCode);

internal sealed class SessionExtensionController
{
    private readonly ITimelockMachineApi _api;
    private readonly Func<Guid> _newIdempotencyKey;
    private readonly object _gate = new();

    private bool _flowActive;
    private bool _canExtend;
    private bool _confirming;
    private string? _sessionId;
    private Guid? _idempotencyKey;

    internal SessionExtensionController(
        ITimelockMachineApi api,
        Func<Guid>? newIdempotencyKey = null)
    {
        _api = api;
        _newIdempotencyKey = newIdempotencyKey ?? Guid.NewGuid;
    }

    internal async Task<ExtensionEligibilityResult> BeginExpiryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_gate)
        {
            if (_flowActive)
            {
                throw new InvalidOperationException("A session expiry flow is already active.");
            }

            _flowActive = true;
            _sessionId = sessionId;
            _canExtend = false;
            _confirming = false;
            _idempotencyKey = null;
        }

        try
        {
            TimelockExtensionCheck check =
                await _api.CheckExtensionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _canExtend = check.CanExtend;
            }
            return new ExtensionEligibilityResult(check.CanExtend, check.Reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CompleteExpiry();
            throw;
        }
        catch (HttpRequestException)
        {
            return new ExtensionEligibilityResult(false, "EXTENSION_CHECK_UNAVAILABLE");
        }
        catch (TaskCanceledException)
        {
            return new ExtensionEligibilityResult(false, "EXTENSION_CHECK_UNAVAILABLE");
        }
        catch (TimelockApiException)
        {
            return new ExtensionEligibilityResult(false, "EXTENSION_CHECK_UNAVAILABLE");
        }
    }

    internal async Task<ExtensionConfirmationResult> ConfirmAsync(
        CancellationToken cancellationToken = default)
    {
        string sessionId;
        Guid idempotencyKey;
        lock (_gate)
        {
            if (!_flowActive || !_canExtend || string.IsNullOrWhiteSpace(_sessionId))
            {
                throw new InvalidOperationException("No eligible session expiry flow is active.");
            }
            if (_confirming)
            {
                throw new InvalidOperationException("Extension confirmation is already in progress.");
            }

            _confirming = true;
            sessionId = _sessionId;
            if (_idempotencyKey is null)
            {
                Guid created = _newIdempotencyKey();
                if (created == Guid.Empty)
                {
                    _confirming = false;
                    throw new InvalidOperationException("The idempotency key factory returned an empty UUID.");
                }
                _idempotencyKey = created;
            }
            idempotencyKey = _idempotencyKey.Value;
        }

        try
        {
            TimelockExtensionConfirmation confirmation =
                await _api.ConfirmExtensionAsync(
                    sessionId,
                    idempotencyKey,
                    cancellationToken).ConfigureAwait(false);
            CompleteExpiry();
            return new ExtensionConfirmationResult(
                ExtensionConfirmOutcome.Extended,
                confirmation,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_gate)
            {
                _confirming = false;
            }
            throw;
        }
        catch (HttpRequestException)
        {
            MarkRetryable();
            return Retryable("EXTENSION_CONFIRM_UNAVAILABLE");
        }
        catch (TaskCanceledException)
        {
            MarkRetryable();
            return Retryable("EXTENSION_CONFIRM_UNAVAILABLE");
        }
        catch (TimelockApiException error) when (error.Code == "RATE_LIMITED")
        {
            MarkRetryable();
            return Retryable(error.Code);
        }
        catch (TimelockApiException error)
        {
            CompleteExpiry();
            return new ExtensionConfirmationResult(
                ExtensionConfirmOutcome.Rejected,
                null,
                error.Code);
        }
    }

    internal void CompleteExpiry()
    {
        lock (_gate)
        {
            _flowActive = false;
            _canExtend = false;
            _confirming = false;
            _sessionId = null;
            _idempotencyKey = null;
        }
    }

    private void MarkRetryable()
    {
        lock (_gate)
        {
            _confirming = false;
        }
    }

    private static ExtensionConfirmationResult Retryable(string code) =>
        new(ExtensionConfirmOutcome.RetryableFailure, null, code);
}
