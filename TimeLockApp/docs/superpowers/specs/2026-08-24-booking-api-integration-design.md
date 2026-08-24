# Booking API Integration Design

**Date:** 2026-08-24

**Status:** Approved in chat; awaiting review of this written specification

## Goal

Extend the WPF TimeLockApp to consume the BookingAiLab machine-event and
session-extension API contract while preserving the existing login, logout,
offline-cache, and heartbeat behavior.

This implementation covers:

- machine heartbeat compatibility with the agreed contract;
- polling and acknowledging machine events;
- checking and confirming three-hour session extensions;
- a non-dismissible 60-second expiry popup; and
- automated tests for HTTP contracts, background processing, idempotency, and
  extension state transitions.

Machine registration, BookingAiLab server routes, and booking session-status
reporting are outside this implementation scope.

## Contract Rules

- All backend URLs must use HTTPS.
- Every request sends `x-machine-code` and `x-device-token` headers.
- Credentials and tokens never appear in query strings or logs.
- JSON uses camelCase property names and ISO 8601 timestamps with an offset.
- The server is authoritative for extension eligibility and the resulting
  `endAt` and `allowedMinutes` values.
- WPF never stores credentials received in a booking event. Authentication
  remains the responsibility of `POST /api/timelock/login`.
- WPF acknowledges a supported event with the same `eventId` it received.
- WPF does not acknowledge unknown event types so a future version can process
  them.

## Architecture

### HTTP client

`TimelockApiClient` remains the only component that knows endpoint paths,
headers, JSON bodies, and response envelopes. It will add methods for:

- `GET /api/machines/{machineCode}/events?limit=20`;
- `POST /api/machine-events/{eventId}/ack`;
- `POST /api/timelock/extension/check`; and
- `POST /api/timelock/extension/confirm`.

The existing heartbeat method remains in this client and continues sending the
device token only in the header. Model records will live in focused service
files rather than as nested UI types.

`TimelockApiException` will expose the server error code and HTTP status code.
It will not retain a response body that could contain booking credentials.

### Machine event poller

`MachineEventPoller` is a background component with an explicit start and
dispose lifecycle. It polls every 15 seconds, requests no more than 20 events,
and processes the returned list sequentially.

The first supported event type is `booking_confirmed`. Processing this event
does not create an account, persist its username or password, or change the UI.
After recognizing a structurally valid event, the poller acknowledges it as:

```json
{
  "status": "processed",
  "processedAt": "<current ISO-8601 timestamp>",
  "message": null
}
```

An unknown event type is left unacknowledged. A malformed response or malformed
supported event is also left unacknowledged.

### Session extension controller

`SessionExtensionController` owns the expiry and confirmation state machine.
It prevents overlapping expiry flows, supplies the current session ID to the
API client, and owns the idempotency key for an in-progress confirmation
attempt. It exposes results to `MainWindow` without depending on WPF controls.

The controller creates one UUID when the user first requests confirmation. A
transient failure preserves that UUID so a retry of the same confirmation sends
the same value. Success or an explicit server rejection completes the attempt
and clears the UUID. A later extension action creates a new UUID.

### Session expiry window

`SessionExpiredWindow` is a modal, topmost WPF window that cannot be dismissed
with the close button or Alt+F4. It counts down from 60 seconds and always shows
an explicit logout button.

When extension eligibility is true, the window also shows a "Extend 3 hours"
button. When eligibility is false or the eligibility request cannot reach the
server, only logout is shown. The extension button is disabled while a confirm
request is in flight.

The countdown behavior is represented by a UI-independent model so boundary
conditions can be tested without a WPF dispatcher.

### Main window integration

`MainWindow` creates and starts the event poller alongside the existing status
reporter. Both stop during shutdown.

When the normal session countdown reaches zero, `MainWindow` pauses that timer
and asks the extension controller to check eligibility. The current session and
heartbeat remain active while the expiry window is visible.

If confirmation succeeds, `MainWindow` closes the popup and rebuilds the
remaining countdown using the server response. It uses `endAt` relative to the
current UTC time and retains `allowedMinutes` as the authoritative cumulative
allowance for logout/session accounting. It does not add three hours locally.

If the user logs out, the popup reaches zero, or confirmation is explicitly
rejected, `MainWindow` closes the popup and executes the existing session-end
flow. Because the original allowance has expired, a rejection always ends the
session, including `EXTENSION_NEXT_BOOKING_CONFLICT`.

## Data Flow

### Event polling and acknowledgement

1. The poller starts after the application window has loaded.
2. It immediately polls, then waits 15 seconds between subsequent attempts.
3. The machine code path segment is URL-escaped and the request limit is 20.
4. Events are considered one at a time.
5. A valid `booking_confirmed` event is acknowledged with its original
   `eventId`.
6. `EVENT_ALREADY_PROCESSED` from the acknowledgement endpoint is treated as a
   completed acknowledgement.
7. A network failure, timeout, rate limit, or other retryable response leaves
   the event for a future polling cycle.
8. The poller never writes an event payload to a log.

### Extension

1. The session countdown reaches zero and stops.
2. WPF calls `POST /api/timelock/extension/check` with only `sessionId`.
3. WPF opens the 60-second window. The extension button is visible only when
   `canExtend` is true.
4. On extension, WPF calls `POST /api/timelock/extension/confirm` with only
   `sessionId` and the attempt's `idempotencyKey`.
5. A transient failure leaves the popup open and permits retry with the same
   key while time remains.
6. A success applies `endAt`, `extensionCount`, and `allowedMinutes` from the
   response and resumes the session countdown.
7. An explicit rejection closes the popup and ends the session.
8. Logout or expiry of the popup's 60 seconds ends the session through the
   existing logout/offline reconciliation path.

## Error Handling

- `MACHINE_TOKEN_INVALID` and `MACHINE_NOT_REGISTERED` stop only the current
  background operation. The next scheduled cycle retries and the active user
  session is not interrupted.
- `RATE_LIMITED`, transport errors, and timeouts defer event work to the next
  poll.
- `EVENT_ALREADY_PROCESSED` from an acknowledgement is treated as success.
- Invalid JSON or a missing required response field raises
  `INVALID_SERVER_RESPONSE` and does not acknowledge the affected event.
- Eligibility-denial reasons are presented as localized user-facing messages:
  `EXTENSION_LIMIT_REACHED`, `EXTENSION_CROSSES_MIDNIGHT`,
  `EXTENSION_NEXT_BOOKING_CONFLICT`, `EXTENSION_BOOKING_INACTIVE`, and
  `EXTENSION_ACCOUNT_MISMATCH`.
- An extension check transport failure is fail-closed: WPF shows the 60-second
  popup with logout only.
- An explicit confirmation failure is fail-closed because the original session
  time is already exhausted.
- Existing heartbeat failures remain non-fatal and are retried automatically.

## Security

- The device token exists only in protected device configuration and request
  headers.
- No Google service-account key, Apps Script secret, password, event payload,
  or device token is logged.
- Event polling is scoped by the machine code and device-token headers.
- Booking-event credentials are held only long enough for JSON deserialization;
  they are neither copied into application state nor persisted.
- Confirm requests do not accept client-supplied booking IDs, machine codes,
  end times, or allowed-minute values.

## Testing Strategy

Development follows red-green-refactor. Tests use the existing custom test
runner and controlled `HttpMessageHandler` implementations; no new package is
required.

### HTTP contract tests

- Assert endpoint paths, URL escaping, `limit=20`, headers, and HTTP methods.
- Assert camelCase request JSON and timestamp serialization.
- Assert response-envelope deserialization for events, eligibility, and
  confirmation.
- Assert server error-code/status propagation and invalid-response handling.
- Assert no credential or token is included in request URLs.

### Event poller tests

- A valid `booking_confirmed` event is acknowledged exactly with its original
  `eventId`.
- An unknown type is not acknowledged.
- A malformed supported event is not acknowledged.
- `EVENT_ALREADY_PROCESSED` completes the event.
- A cancellation/disposal request stops an in-flight poll without waiting on a
  UI synchronization context.

### Extension tests

- Only one expiry flow can be active.
- Eligibility controls whether the extension action is exposed.
- The first confirm action creates a UUID.
- A transient retry reuses the same UUID.
- Success or explicit rejection completes the attempt; a later action receives
  a different UUID.
- Success returns and applies the server's `endAt` and `allowedMinutes`.
- Rejection, logout, and 60-second expiry select the existing session-end path.
- The popup countdown expires at exactly 60 elapsed seconds and cannot produce
  multiple terminal actions.

### Verification

- Run the complete `TimeLockApp.Tests` test executable.
- Run `dotnet build` for the WPF application.
- Review the final diff for accidental credential logging, unrelated changes,
  generated artifacts, and contract-name mismatches.

## Files Expected to Change

- `Services/TimelockApiClient.cs`
- `Services/TimelockGatewayModels.cs` or focused new API-model files
- new `Services/MachineEventPoller.cs`
- new `Services/SessionExtensionController.cs`
- new UI-independent expiry countdown model
- `MainWindow.xaml.cs`
- new `SessionExpiredWindow.xaml` and `SessionExpiredWindow.xaml.cs`
- `Services/LanguageService.cs`
- new or extended files under `TimeLockApp.Tests/`
- `TimeLockApp.Tests/Program.cs`

The implementation must avoid unrelated refactoring and must preserve the
user's existing uncommitted work.
