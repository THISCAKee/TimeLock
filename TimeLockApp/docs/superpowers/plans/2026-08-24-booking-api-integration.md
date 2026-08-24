# Booking API Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add contract-accurate machine event polling/acknowledgement and server-authoritative three-hour session extensions to the WPF TimeLockApp.

**Architecture:** `TimelockApiClient` owns HTTP serialization, `MachineEventPoller` owns cancellable background polling, and `SessionExtensionController` owns the idempotent expiry state machine. `MainWindow` coordinates these services with a dedicated `SessionExpiredWindow`, while a UI-independent countdown model makes the 60-second timeout deterministic and testable.

**Tech Stack:** C# 14, .NET 10, WPF, `System.Net.Http.Json`, `System.Text.Json`, existing console-style test runner

**Spec:** `docs/superpowers/specs/2026-08-24-booking-api-integration-design.md`

## Global Constraints

- All backend URLs must use HTTPS.
- Every request sends `x-machine-code` and `x-device-token` headers.
- Credentials and tokens never appear in query strings or logs.
- JSON uses camelCase property names and ISO 8601 timestamps with an offset.
- WPF never persists booking-event credentials and authenticates only through `POST /api/timelock/login`.
- A supported event is acknowledged with its original `eventId`; unknown event types are not acknowledged.
- The server is authoritative for `endAt`, `extensionCount`, and `allowedMinutes`; the client never adds three hours locally.
- A confirmation retry reuses the same idempotency key until success or explicit rejection.
- No new NuGet dependency is introduced.
- Preserve all unrelated uncommitted files in the working tree.
- Baseline verification on 2026-08-24 has one unrelated existing failure:
  `sheet deletion reports a change and keeps local users` with message
  `Local-only users must remain.` Do not change user synchronization as part of
  this feature; every new test must pass and no additional failure may appear.

---

## File Structure

- Create `Services/TimelockMachineApiModels.cs`: event, acknowledgement, extension-check, and extension-confirm contract records.
- Modify `Services/TimelockApiClient.cs`: implement the four new HTTP operations, status-bearing safe exceptions, and a narrow service interface.
- Create `Services/MachineEventPoller.cs`: immediate and periodic event processing with cancellation.
- Create `Services/SessionExtensionController.cs`: expiry-state and idempotency-key lifecycle.
- Create `Services/SessionExpiryCountdown.cs`: deterministic 60-second single-terminal-action model.
- Create `SessionExpiredWindow.xaml`: modal topmost expiry UI.
- Create `SessionExpiredWindow.xaml.cs`: countdown timer and extension/logout events.
- Modify `Services/LanguageService.cs`: Thai and English expiry/denial strings.
- Modify `MainWindow.xaml.cs`: service lifecycle, expiry flow, authoritative countdown update, and cleanup.
- Create `TimeLockApp.Tests/TimelockApiClientTests.cs`: wire-contract and error tests.
- Create `TimeLockApp.Tests/MachineEventPollerTests.cs`: supported/unknown/malformed/idempotent acknowledgement tests.
- Create `TimeLockApp.Tests/SessionExtensionControllerTests.cs`: state and idempotency tests.
- Create `TimeLockApp.Tests/SessionExpiryCountdownTests.cs`: exact timeout and terminal-action tests.
- Modify `TimeLockApp.Tests/Program.cs`: register new suites.

---

### Task 1: Machine API Models and HTTP Contract

**Files:**
- Create: `Services/TimelockMachineApiModels.cs`
- Modify: `Services/TimelockApiClient.cs`
- Create: `TimeLockApp.Tests/TimelockApiClientTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Produces: `ITimelockMachineApi.GetEventsAsync(int, CancellationToken)` returning `IReadOnlyList<TimelockMachineEvent>`
- Produces: `ITimelockMachineApi.AcknowledgeEventAsync(string, TimelockEventAcknowledgement, CancellationToken)`
- Produces: `ITimelockMachineApi.CheckExtensionAsync(string, CancellationToken)` returning `TimelockExtensionCheck`
- Produces: `ITimelockMachineApi.ConfirmExtensionAsync(string, Guid, CancellationToken)` returning `TimelockExtensionConfirmation`
- Produces: `TimelockApiException.Code` and nullable `TimelockApiException.StatusCode`

- [ ] **Step 1: Register and write failing HTTP contract tests**

Add `TimelockApiClientTests.All()` to `TimeLockApp.Tests/Program.cs`, then create tests that use a recording `HttpMessageHandler`. The key assertions must express the public wire behavior:

```csharp
private static void GetEventsUsesMachineScopedEndpointAndHeaders()
{
    RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
        {"events":[{"eventId":"event-id","eventType":"booking_confirmed","bookingId":"booking-id","bookingNumber":"BK-1","machineCode":"PC 001","startAt":"2026-08-20T10:00:00+07:00","endAt":"2026-08-20T12:00:00+07:00","username":"booking_0001","password":"secret"}]}
        """);
    using TimelockApiClient client = CreateClient(handler, machineCode: "PC 001");

    IReadOnlyList<TimelockMachineEvent> events =
        client.GetEventsAsync(20).GetAwaiter().GetResult();

    AssertTrue(handler.Request!.Method == HttpMethod.Get);
    AssertTrue(handler.Request.RequestUri!.PathAndQuery ==
        "/api/machines/PC%20001/events?limit=20");
    AssertTrue(handler.Header("x-machine-code") == "PC 001");
    AssertTrue(handler.Header("x-device-token") == "token");
    AssertTrue(events.Single().EventId == "event-id");
}
```

Add separate tests for ACK JSON, extension check JSON, extension confirm JSON, response deserialization, `EVENT_ALREADY_PROCESSED`, invalid JSON, and absent secret values in URLs. For confirmation, assert the exact body:

```json
{"sessionId":"session-id","idempotencyKey":"11111111-1111-1111-1111-111111111111"}
```

For ACK, assert `status`, `processedAt`, and explicit `message: null` are present and that the URL contains the original escaped event ID.

- [ ] **Step 2: Run the new suite and verify RED**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj
```

Expected: compilation fails because `TimelockMachineEvent`, `ITimelockMachineApi`, and the four client methods do not exist. This is the intended failure.

- [ ] **Step 3: Add the contract records and narrow API interface**

Create `Services/TimelockMachineApiModels.cs` with these signatures and explicit JSON names:

```csharp
internal interface ITimelockMachineApi
{
    Task<IReadOnlyList<TimelockMachineEvent>> GetEventsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task AcknowledgeEventAsync(
        string eventId,
        TimelockEventAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default);

    Task<TimelockExtensionCheck> CheckExtensionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<TimelockExtensionConfirmation> ConfirmExtensionAsync(
        string sessionId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);
}

internal sealed record TimelockMachineEvent(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("bookingId")] string BookingId,
    [property: JsonPropertyName("bookingNumber")] string BookingNumber,
    [property: JsonPropertyName("machineCode")] string MachineCode,
    [property: JsonPropertyName("startAt")] DateTimeOffset StartAt,
    [property: JsonPropertyName("endAt")] DateTimeOffset EndAt,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

internal sealed record TimelockEventAcknowledgement(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("processedAt")] DateTimeOffset ProcessedAt,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record TimelockExtensionCheck(
    bool CanExtend,
    string Reason,
    DateTimeOffset CurrentEndAt,
    DateTimeOffset? ProposedEndAt,
    int ExtensionCount,
    int MaxExtensionCount);

internal sealed record TimelockExtensionConfirmation(
    string BookingId,
    DateTimeOffset EndAt,
    int ExtensionCount,
    int AllowedMinutes);
```

- [ ] **Step 4: Implement the minimal HTTP methods**

Make `TimelockApiClient : ITimelockMachineApi, IDisposable`. Add private response envelopes matching `{ "events": [...] }` and `{ "ok": true, "data": ... }`. Build request paths using `Uri.EscapeDataString`, validate `limit` is in `1..20`, and reuse `ReadAsync<T>`/`EnsureSuccessAsync`.

Change the exception constructor to:

```csharp
internal sealed class TimelockApiException(
    string code,
    HttpStatusCode? statusCode = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal HttpStatusCode? StatusCode { get; } = statusCode;
}
```

Pass `response.StatusCode` when throwing server errors. Keep only the parsed code; do not attach response content to the exception.

- [ ] **Step 5: Run tests and verify GREEN**

Run the test command from Step 2. Expected: every new HTTP contract test passes
and no failure is added beyond the documented baseline user-sync failure.

- [ ] **Step 6: Commit the HTTP contract slice**

```powershell
git add Services\TimelockMachineApiModels.cs Services\TimelockApiClient.cs TimeLockApp.Tests\TimelockApiClientTests.cs TimeLockApp.Tests\Program.cs
git commit -m "feat: add machine event and extension API contracts"
```

---

### Task 2: Idempotent Machine Event Polling

**Files:**
- Create: `Services/MachineEventPoller.cs`
- Create: `TimeLockApp.Tests/MachineEventPollerTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Consumes: all `ITimelockMachineApi` event methods from Task 1
- Produces: `MachineEventPoller.Start()` and synchronous `Dispose()` that cancels without capturing a UI synchronization context
- Produces: internal `ProcessOnceAsync(CancellationToken)` for deterministic testing

- [ ] **Step 1: Write failing event behavior tests**

Register `MachineEventPollerTests.All()`. Use a fake `ITimelockMachineApi` that records acknowledgements but never records password values. Cover these behaviors with separate tests:

```csharp
private static void BookingConfirmedUsesOriginalEventId()
{
    FakeMachineApi api = new(Event("event-123", "booking_confirmed"));
    using MachineEventPoller poller = new(api, () => FixedNow);

    poller.ProcessOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

    AssertTrue(api.Acknowledgements.Single().EventId == "event-123");
    AssertTrue(api.Acknowledgements.Single().Body ==
        new TimelockEventAcknowledgement("processed", FixedNow, null));
}
```

Add tests proving unknown event types and structurally invalid supported events are not ACKed, `EVENT_ALREADY_PROCESSED` is swallowed, other API errors leave the call retryable, and disposal cancels a blocked request within one second.

- [ ] **Step 2: Run tests and verify RED**

Run the full test executable. Expected: compilation fails because `MachineEventPoller` does not exist.

- [ ] **Step 3: Implement one-cycle processing**

Create `MachineEventPoller` with injected `ITimelockMachineApi`, `Func<DateTimeOffset> now`, and an optional 15-second interval. `ProcessOnceAsync` fetches 20 events sequentially. Validate supported events with non-empty `EventId`, `BookingId`, `BookingNumber`, `MachineCode`, `Username`, and `Password`, plus `EndAt > StartAt`. ACK only valid `booking_confirmed` events.

Catch `TimelockApiException` with `Code == "EVENT_ALREADY_PROCESSED"` around ACK only and treat it as completion. Allow other exceptions to escape from `ProcessOnceAsync` so the run loop can uniformly defer them.

- [ ] **Step 4: Implement the cancellable run loop**

Use the same lifecycle pattern as `TimelockStatusReporter`: start with `Task.Run`, poll immediately, catch non-cancellation failures without logging payloads, then await a `PeriodicTimer`. `Dispose` cancels, synchronously joins the worker, and disposes its token source.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test executable. Expected: all event-poller tests pass and no
failure is added beyond the documented baseline user-sync failure.

- [ ] **Step 6: Commit the polling slice**

```powershell
git add Services\MachineEventPoller.cs TimeLockApp.Tests\MachineEventPollerTests.cs TimeLockApp.Tests\Program.cs
git commit -m "feat: poll and acknowledge booking events"
```

---

### Task 3: Extension State Machine and Expiry Countdown

**Files:**
- Create: `Services/SessionExtensionController.cs`
- Create: `Services/SessionExpiryCountdown.cs`
- Create: `TimeLockApp.Tests/SessionExtensionControllerTests.cs`
- Create: `TimeLockApp.Tests/SessionExpiryCountdownTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Consumes: `ITimelockMachineApi.CheckExtensionAsync` and `ConfirmExtensionAsync`
- Produces: `BeginExpiryAsync(string, CancellationToken)` returning `ExtensionEligibilityResult`
- Produces: `ConfirmAsync(CancellationToken)` returning `ExtensionConfirmationResult`
- Produces: `CompleteExpiry()` to release the flow after logout/timeout
- Produces: `SessionExpiryCountdown.Tick()` returning `SessionExpiryAction.None` or `SessionExpiryAction.Logout`

- [ ] **Step 1: Write failing controller tests**

Register both suites and express each state transition independently. Inject `Func<Guid>` so IDs are deterministic:

```csharp
private static void TransientRetryReusesIdempotencyKey()
{
    Guid key = Guid.Parse("11111111-1111-1111-1111-111111111111");
    FakeMachineApi api = FakeMachineApi.EligibleThenTransientThenSuccess();
    SessionExtensionController controller = new(api, () => key);

    controller.BeginExpiryAsync("session-id").GetAwaiter().GetResult();
    ExtensionConfirmationResult first =
        controller.ConfirmAsync().GetAwaiter().GetResult();
    ExtensionConfirmationResult second =
        controller.ConfirmAsync().GetAwaiter().GetResult();

    AssertTrue(first.Outcome == ExtensionConfirmOutcome.RetryableFailure);
    AssertTrue(second.Outcome == ExtensionConfirmOutcome.Extended);
    AssertTrue(api.ConfirmKeys.SequenceEqual(new[] { key, key }));
}
```

Add tests for overlapping `BeginExpiryAsync`, ineligible check, check transport failure, explicit confirm rejection clearing the attempt, and a later completed flow receiving the next injected UUID.

For `SessionExpiryCountdown`, assert construction starts at 60, the 59th tick is not terminal, the 60th tick returns logout, later ticks return none, and an explicit action suppresses timeout.

- [ ] **Step 2: Run tests and verify RED**

Run the full test executable. Expected: compilation fails because controller/countdown types do not exist.

- [ ] **Step 3: Implement result records and controller state**

Use explicit result enums rather than exceptions for UI decisions:

```csharp
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
```

`BeginExpiryAsync` accepts a non-empty session ID only when no flow is active. A transport/timeout failure returns `CanExtend=false` with `EXTENSION_CHECK_UNAVAILABLE`. `ConfirmAsync` is legal only for an active eligible flow. Preserve the generated UUID across `HttpRequestException`, timeout, and `RATE_LIMITED`; clear it and complete the flow on success or all other `TimelockApiException` codes.

- [ ] **Step 4: Implement the countdown model**

`SessionExpiryCountdown` stores remaining seconds and a terminal flag. `Tick` decrements once, returns logout only when crossing to zero, and never returns another terminal action. `SelectExtension` and `SelectLogout` atomically claim the terminal action so timer/button races cannot produce two outcomes.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test executable. Expected: all controller/countdown tests pass and
no failure is added beyond the documented baseline user-sync failure.

- [ ] **Step 6: Commit the state-machine slice**

```powershell
git add Services\SessionExtensionController.cs Services\SessionExpiryCountdown.cs TimeLockApp.Tests\SessionExtensionControllerTests.cs TimeLockApp.Tests\SessionExpiryCountdownTests.cs TimeLockApp.Tests\Program.cs
git commit -m "feat: add idempotent session extension state"
```

---

### Task 4: Localized Session Expiry Window

**Files:**
- Create: `SessionExpiredWindow.xaml`
- Create: `SessionExpiredWindow.xaml.cs`
- Modify: `Services/LanguageService.cs`
- Modify: `TimeLockApp.Tests/LanguageServiceTests.cs`

**Interfaces:**
- Consumes: `SessionExpiryCountdown`
- Produces: `ExtensionRequested` and `LogoutRequested` events
- Produces: `SetConfirmationPending(bool)`, `ShowRetryableError()`, and `CloseAfterExtension()`

- [ ] **Step 1: Write failing localization tests**

Extend `LanguageServiceTests` to assert Thai and English values exist and are non-empty for:

```text
SessionExpiredHeading
SessionExpiredCountdown
ExtendThreeHours
ExtensionChecking
ExtensionRetry
ExtensionLimitReached
ExtensionCrossesMidnight
ExtensionNextBookingConflict
ExtensionBookingInactive
ExtensionAccountMismatch
ExtensionUnavailable
```

- [ ] **Step 2: Run tests and verify RED**

Run the full test executable. Expected: localization tests fail because the keys are currently returned unchanged.

- [ ] **Step 3: Add localized strings**

Add natural Thai and English copy to both dictionaries. Use the exact Thai primary actions `ต่อเวลา 3 ชั่วโมง` and `ออกจากระบบ`. Map each server denial reason to a distinct explanation and use `ไม่สามารถตรวจสอบสิทธิ์ต่อเวลาได้ กรุณาออกจากระบบ` for check transport failure.

- [ ] **Step 4: Create the WPF window**

Create a centered, topmost, no-resize modal window with a visible `00:60` countdown, denial/status text, logout button, and conditionally visible extension button. Bind visible text through `DynamicResource` keys already managed by `LanguageService`. Prevent normal close and Alt+F4; allow the code-behind to close only after a terminal action.

The code-behind owns a one-second `DispatcherTimer`, delegates terminal arbitration to `SessionExpiryCountdown`, raises one event only, disables the extension button during confirm, and disposes the timer when closed.

- [ ] **Step 5: Build and run tests**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj
dotnet build TimeLockApp.csproj
```

Expected: localization tests pass, no test failure is added beyond the
documented baseline failure, and the WPF XAML compiles without warnings
introduced by this task.

- [ ] **Step 6: Commit the UI slice**

```powershell
git add SessionExpiredWindow.xaml SessionExpiredWindow.xaml.cs Services\LanguageService.cs TimeLockApp.Tests\LanguageServiceTests.cs
git commit -m "feat: add localized session expiry prompt"
```

---

### Task 5: Main Window Lifecycle Integration

**Files:**
- Modify: `MainWindow.xaml.cs`
- Modify: `Services/TimelockGatewayModels.cs`
- Create: `TimeLockApp.Tests/SessionTimeTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Consumes: `MachineEventPoller`, `SessionExtensionController`, and `SessionExpiredWindow`
- Produces: pure `SessionTime.FromExtension(...)` calculation for server-authoritative remaining/total seconds

- [ ] **Step 1: Write failing authoritative-time tests**

Add a small pure `SessionTime` helper test suite before changing `MainWindow`:

```csharp
private static void ExtensionUsesServerEndTimeAndAllowance()
{
    DateTimeOffset now = DateTimeOffset.Parse("2026-08-24T04:30:00Z");
    TimelockExtensionConfirmation confirmation = new(
        "booking-id",
        DateTimeOffset.Parse("2026-08-24T07:30:00Z"),
        1,
        360);

    SessionTime result = SessionTime.FromExtension(confirmation, now);

    AssertTrue(result.RemainingSeconds == 10_800);
    AssertTrue(result.TotalAllowedSeconds == 21_600);
}
```

Add a case that clamps an already-past `endAt` to zero and a case that rejects negative `allowedMinutes`.

- [ ] **Step 2: Run tests and verify RED**

Run the full test executable. Expected: compilation fails because `SessionTime` does not exist.

- [ ] **Step 3: Implement `SessionTime` and update session allowance**

Place `SessionTime` beside the gateway models. `FromExtension` uses `Math.Ceiling((confirmation.EndAt - now).TotalSeconds)`, clamps remaining seconds to `[0, int.MaxValue]`, and uses checked conversion of `AllowedMinutes * 60`. When extension succeeds, replace `_currentGatewaySession` with a copy containing the returned `AllowedMinutes`; this keeps logout and pending reconciliation aligned with the server.

- [ ] **Step 4: Wire event poller lifecycle**

Construct `MachineEventPoller` from `_timelockApi`, start it immediately after `_statusReporter.Start()` in `MainWindow_Loaded`, and dispose it before `_timelockApi.Dispose()` in `OnClosed`.

- [ ] **Step 5: Replace immediate expiry with the extension flow**

Guard the zero transition so only one async expiry operation runs. Stop the session timer, call `BeginExpiryAsync`, construct `SessionExpiredWindow` using the eligibility result, and keep `_heartbeatUsername` unchanged.

On `ExtensionRequested`, await `ConfirmAsync`:

- `Extended`: apply `SessionTime`, update `_sessionTotalSeconds`, `_remainingSeconds`, `_currentGatewaySession`, and `UsageWindow`; close the popup and restart the timer only when remaining time is positive.
- `RetryableFailure`: keep the popup open, show retry copy, and re-enable extension while popup time remains.
- `Rejected`: close the popup, localize the returned code, and call `EndSessionAsync("completed", showExpiredAlert: false)`.

On logout or popup timeout, call `CompleteExpiry()` and the same existing `EndSessionAsync("completed", showExpiredAlert: false)` exactly once. Offline sessions cannot be extended: open the logout-only popup with reason `EXTENSION_CHECK_UNAVAILABLE` without calling the API.

- [ ] **Step 6: Run tests and build**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj
dotnet build TimeLockApp.csproj
```

Expected: all tests introduced by this feature pass, no failure is added beyond
the documented baseline failure, and the WPF application builds.

- [ ] **Step 7: Commit the integration slice**

```powershell
git add MainWindow.xaml.cs Services\TimelockGatewayModels.cs TimeLockApp.Tests\SessionTimeTests.cs TimeLockApp.Tests\Program.cs
git commit -m "feat: integrate booking events and session extensions"
```

---

### Task 6: Contract and Regression Verification

**Files:**
- Modify only files required to correct a failing test or build found in this task

**Interfaces:**
- Consumes: all deliverables from Tasks 1-5
- Produces: verified build and test evidence

- [ ] **Step 1: Run the full automated test executable**

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj
```

Expected for the current baseline: exit code 1 solely because
`sheet deletion reports a change and keeps local users` remains an unrelated
known failure. Every booking API integration test reports `PASS`, and there are
no additional failures. If the baseline failure has been corrected by its
owner before execution, expect exit code 0 instead.

- [ ] **Step 2: Build the WPF application**

```powershell
dotnet build TimeLockApp.csproj
```

Expected: exit code 0 with no new compiler or XAML warnings.

- [ ] **Step 3: Inspect security-sensitive output and diffs**

```powershell
rg -n "Console\.|Debug\.|Trace\.|password|DeviceToken|event payload" Services MainWindow.xaml.cs SessionExpiredWindow.xaml.cs
git diff --check
git status --short
git diff -- Services MainWindow.xaml.cs SessionExpiredWindow.xaml SessionExpiredWindow.xaml.cs TimeLockApp.Tests
```

Expected: no logging of credentials/tokens/event payloads, no whitespace errors, and no unrelated source changes staged by this implementation.

- [ ] **Step 4: Verify endpoint names and body ownership against the spec**

Confirm by inspection that confirm JSON contains only `sessionId` and `idempotencyKey`, check JSON contains only `sessionId`, event ACK uses the original ID, unknown events are unacknowledged, and no client code adds 180 minutes.

- [ ] **Step 5: Commit any verification-only corrections**

If Step 1-4 required a source correction, first add a regression test that demonstrates the failure, watch it fail, implement the minimal correction, rerun both verification commands, then commit only those corrected files:

```powershell
git commit -m "fix: align booking API integration contract"
```

If no corrections were required, do not create an empty commit.
