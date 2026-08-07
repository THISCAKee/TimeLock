# Automatic Google Sheet Synchronization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize Google Sheet users at startup, after Internet Authentication, every 30 seconds, and after expiry or Logout, while refreshing an open Admin Panel only for material changes.

**Architecture:** Make SQLite synchronization report real insert/update/delete changes, propagate that result through `UserSyncService`, and route all automatic triggers through a small serialized orchestrator. `MainWindow` owns the WPF timer and displays status; `AdminWindow` reloads its grid only when the orchestrator reports changed data.

**Tech Stack:** C# 14, .NET 10 WPF, `DispatcherTimer`, `SemaphoreSlim`, Google Sheets API v4, Microsoft.Data.Sqlite, dependency-free console test runner.

## Global Constraints

- Poll exactly every 30 seconds; no webhook or new cloud service.
- Keep the current session alive when its Sheet user is changed, disabled, or removed.
- Run a full synchronization at every approved trigger; change detection controls UI refresh only.
- Never run two Sheet synchronization operations concurrently.
- Failures preserve committed local data, do not show a modal dialog, and retry on the next trigger.
- Preserve local-only users and historical sessions under the existing synchronization rules.
- Work directly on `master` as approved; stage only named source, test, and documentation files.

---

### Task 1: Report Material SQLite Synchronization Changes

**Files:**
- Create: `TimeLockApp.Tests/UserSynchronizationChangeTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`
- Modify: `data/DatabaseService.cs`

**Interfaces:**
- Consumes: `GoogleSheetUser`, the existing SQLite transaction, and `DatabaseService.GetAllUsers()`.
- Produces: `public bool DatabaseService.SynchronizeUsers(IReadOnlyList<GoogleSheetUser> sheetUsers)` where `true` means at least one Sheet-owned user was inserted, materially updated, or deleted.

- [x] **Step 1: Add change-result tests**

Create `UserSynchronizationChangeTests.All()` with isolated `TestDatabase` fixtures covering these exact assertions:

```csharp
bool inserted = database.SynchronizeUsers(new[] { SheetUser(501, "new-user") });
AssertTrue(inserted, "A Sheet insert must report a material change.");

bool identical = database.SynchronizeUsers(new[] { SheetUser(501, "new-user") });
AssertFalse(identical, "Identical Sheet data must not report a change.");

GoogleSheetUser updated = SheetUser(501, "new-user", password: "changed");
bool changed = database.SynchronizeUsers(new[] { updated });
AssertTrue(changed, "A changed Sheet field must report a material change.");

bool deleted = database.SynchronizeUsers(Array.Empty<GoogleSheetUser>());
AssertTrue(deleted, "A missing Sheet user must report a deletion.");
AssertTrue(database.GetAllUsers().Any(user => user.IsLocalOnly),
    "Local-only users must remain.");
```

Add the suite to `Program.cs` with `.Concat(UserSynchronizationChangeTests.All())`.

- [x] **Step 2: Run RED**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj --no-restore
```

Expected: compilation fails because `SynchronizeUsers` returns `void`.

- [x] **Step 3: Return affected state from the database transaction**

Change the public signature and accumulate affected rows:

```csharp
public bool SynchronizeUsers(IReadOnlyList<GoogleSheetUser> sheetUsers)
{
    // existing validation/open/transaction setup
    int affectedRows = 0;

    foreach (GoogleSheetUser user in sheetUsers)
    {
        affectedRows += UpsertSheetUser(connection, transaction, user);
    }

    affectedRows += DeleteMissingSheetUsers(connection, transaction, sheetUsers);
    transaction.Commit();
    return affectedRows > 0;
}
```

Change both helpers to return `int`. Return `command.ExecuteNonQuery()` for insertion/update/deletion. Add a no-op guard to the upsert so identical data does not execute an update:

```sql
WHERE users.is_local_only = 0
  AND (
      users.external_user_id IS NOT excluded.external_user_id OR
      users.password IS NOT excluded.password OR
      users.allowed_minutes IS NOT excluded.allowed_minutes OR
      users.role IS NOT excluded.role OR
      users.is_active IS NOT CASE
          WHEN users.is_consumed = 1 THEN 0
          ELSE excluded.is_active
      END
  );
```

For the empty-Sheet deletion branch, return its row count rather than returning `void`.

- [x] **Step 4: Run GREEN and existing history tests**

Run the complete C# runner. Expected: all old tests plus the four new change-result tests pass.

- [x] **Step 5: Commit the database slice**

```powershell
git add -- data/DatabaseService.cs TimeLockApp.Tests/UserSynchronizationChangeTests.cs TimeLockApp.Tests/Program.cs
git diff --cached --check
git commit -m "feat: report sheet synchronization changes"
```

---

### Task 2: Propagate Results and Serialize Sheet Access

**Files:**
- Create: `Services/IGoogleSheetsUserService.cs`
- Modify: `Services/GoogleSheetsUserService.cs`
- Modify: `Services/UserSyncService.cs`
- Create: `TimeLockApp.Tests/UserSyncServiceTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Consumes: Task 1's boolean `DatabaseService.SynchronizeUsers` result.
- Produces: `IGoogleSheetsUserService`, `UserSyncResult.HasChanges`, and serialized `UserSyncService.SynchronizeAsync` behavior.

- [x] **Step 1: Add failing service-result and serialization tests**

Define a test fake implementing the wished-for interface. Its `GetUsersAsync` can return configured users, throw a configured exception, or pause on a `TaskCompletionSource`. Add tests asserting:

```csharp
UserSyncResult first = await service.SynchronizeAsync();
AssertTrue(first.IsSuccessful && first.HasChanges,
    "The first imported row must report changes.");

UserSyncResult second = await service.SynchronizeAsync();
AssertTrue(second.IsSuccessful && !second.HasChanges,
    "An identical import must report no changes.");
```

For serialization, start two `SynchronizeAsync` calls against a fake that records active calls. Release both operations and assert `MaximumConcurrentCalls == 1`.

For failure, make `GetUsersAsync` throw, assert `IsSuccessful == false`, and assert the fixture's pre-existing local users are unchanged.

- [x] **Step 2: Run RED**

Run the C# runner. Expected: compilation fails because the interface and `HasChanges` do not exist and the concrete service constructor cannot accept the fake.

- [x] **Step 3: Introduce the narrow Google Sheet interface**

Create:

```csharp
public interface IGoogleSheetsUserService
{
    Task<IReadOnlyList<GoogleSheetUser>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<bool> SetUserActiveAsync(
        int externalUserId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
```

Have `GoogleSheetsUserService` implement it and change `UserSyncService`'s field and constructor parameter to the interface. Do not alter Google API request behavior.

- [x] **Step 4: Propagate change state**

Add `public bool HasChanges { get; private init; }` to `UserSyncResult`. Change success construction to:

```csharp
public static UserSyncResult Success(int userCount, bool hasChanges)
```

In `SynchronizeAsync`, capture the Task 1 result and return it:

```csharp
bool hasChanges = _databaseService.SynchronizeUsers(users);
return UserSyncResult.Success(users.Count, hasChanges);
```

Keep the existing `_syncGate` around pending deactivation, read, and database mutation so every caller is serialized.

- [x] **Step 5: Run GREEN and commit**

Run the complete C# runner, then:

```powershell
git add -- Services/IGoogleSheetsUserService.cs Services/GoogleSheetsUserService.cs Services/UserSyncService.cs TimeLockApp.Tests/UserSyncServiceTests.cs TimeLockApp.Tests/Program.cs
git diff --cached --check
git commit -m "feat: expose serialized sheet sync results"
```

---

### Task 3: Centralize Automatic Synchronization Triggers

**Files:**
- Create: `Services/AutomaticSyncOrchestrator.cs`
- Create: `TimeLockApp.Tests/AutomaticSyncOrchestratorTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Consumes: `Func<CancellationToken, Task<UserSyncResult>>` backed by `UserSyncService.SynchronizeAsync`.
- Produces: `AutomaticSyncTrigger`, `AutomaticSyncCompletedEventArgs`, `AutomaticSyncOrchestrator.Interval`, `RunAsync` and `Completed`.

- [x] **Step 1: Write failing trigger and concurrency tests**

Test that `Interval == TimeSpan.FromSeconds(30)`. Invoke every approved enum value—`Startup`, `InternetAuthenticated`, `Periodic`, `SessionExpired`, and `Logout`—and assert the injected synchronization delegate is called and the `Completed` event carries the same trigger and result.

Start two calls with a blocking delegate and assert the second delegate invocation does not start until the first is released. This proves automatic trigger execution is serialized independently of UI timing.

- [x] **Step 2: Run RED**

Run the C# runner. Expected: compilation fails because `AutomaticSyncOrchestrator` and its trigger types do not exist.

- [x] **Step 3: Implement the minimal orchestrator**

Create these types:

```csharp
internal enum AutomaticSyncTrigger
{
    Startup,
    InternetAuthenticated,
    Periodic,
    SessionExpired,
    Logout
}

internal sealed record AutomaticSyncCompletedEventArgs(
    AutomaticSyncTrigger Trigger,
    UserSyncResult Result);

internal sealed class AutomaticSyncOrchestrator
{
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly Func<CancellationToken, Task<UserSyncResult>> _synchronize;
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal event EventHandler<AutomaticSyncCompletedEventArgs>? Completed;

    internal async Task<UserSyncResult> RunAsync(
        AutomaticSyncTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            UserSyncResult result = await _synchronize(cancellationToken);
            Completed?.Invoke(this, new(trigger, result));
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
```

Add a constructor that requires the delegate and validates it for null. The orchestrator must not catch or transform cancellation.

- [x] **Step 4: Run GREEN and commit**

Run all C# tests, then commit only the orchestrator slice:

```powershell
git add -- Services/AutomaticSyncOrchestrator.cs TimeLockApp.Tests/AutomaticSyncOrchestratorTests.cs TimeLockApp.Tests/Program.cs
git diff --cached --check
git commit -m "feat: coordinate automatic sheet sync triggers"
```

---

### Task 4: Wire Startup, Auth, Timer, Expiry, Logout, and Admin Refresh

**Files:**
- Modify: `MainWindow.xaml.cs`
- Modify: `AdminWindow.xaml.cs`
- Create: `TimeLockApp.Tests/AutomaticSyncStatusTests.cs`
- Create: `Services/AutomaticSyncStatus.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Consumes: Task 2's `UserSyncResult.HasChanges` and Task 3's orchestrator/event types.
- Produces: one authoritative automatic-sync path in `MainWindow`, visible status formatting, and `AdminWindow.ApplyAutomaticSyncResult(UserSyncResult result, DateTime completedAt)`.

- [x] **Step 1: Add failing status tests**

Test a small pure formatter before touching WPF:

```csharp
string success = AutomaticSyncStatus.Format(
    UserSyncResult.Success(4, hasChanges: true),
    new DateTime(2026, 8, 7, 16, 5, 0));
AssertContains(success, "16:05:00");
AssertContains(success, "4");

string failure = AutomaticSyncStatus.Format(
    UserSyncResult.Failure("network unavailable"),
    new DateTime(2026, 8, 7, 16, 6, 0));
AssertContains(failure, "network unavailable");
```

Run RED and confirm the missing formatter is the failure.

- [x] **Step 2: Implement the status formatter and run GREEN**

Create `AutomaticSyncStatus.Format(UserSyncResult result, DateTime completedAt)` using `HH:mm:ss`. Success text includes the time and user count; failure text includes the error and states that the next automatic attempt will retry. Keep all Thai UI copy in this formatter so Login and Admin use identical wording.

- [x] **Step 3: Add the orchestrator and periodic timer to MainWindow**

Add fields:

```csharp
private readonly AutomaticSyncOrchestrator _automaticSync;
private readonly DispatcherTimer _automaticSyncTimer;
private AdminWindow? _adminWindow;
private bool _isShuttingDown;
```

After constructing `UserSyncService`, construct the orchestrator from `_userSyncService.SynchronizeAsync`, subscribe to `Completed`, and configure `_automaticSyncTimer.Interval = AutomaticSyncOrchestrator.Interval`.

The periodic handler must stop the timer, await `RunAsync(Periodic)`, and restart it in `finally` unless `_isShuttingDown` is true. Start this timer during `MainWindow_Loaded` after the hook is installed. In `OnClosed`, set `_isShuttingDown = true` and stop the automatic timer before unhooking the keyboard callback.

- [x] **Step 4: Route every approved event through the orchestrator**

Replace startup's direct `SynchronizeUsersSilentlyAsync` call with `RunAsync(Startup)`. Remove the unconditional `"เชื่อมต่ออินเทอร์เน็ตแล้ว"` assignment after the call because the completion handler owns the final success or failure status.

Change `OpenNetworkAuthentication` and its button handler to async. When `AuthenticationCompleted` is true, await `RunAsync(InternetAuthenticated)` and let the completion handler own the final success or failure status. Remove the unused `OpenNetworkAuthWindowAsync` method and the old connected-status assignment that would overwrite a synchronization failure.

At the end of `EndSessionAsync`, replace `ProcessPendingDeactivationsAsync` with:

```csharp
AutomaticSyncTrigger trigger = status == "logged_out"
    ? AutomaticSyncTrigger.Logout
    : AutomaticSyncTrigger.SessionExpired;

await _automaticSync.RunAsync(trigger);
```

This must occur after the local session/user transaction. Do not terminate an active session in response to any periodic result.

Implementation-discovered regression coverage: `UserSynchronizationChangeTests` now verifies that a session can still close as `logged_out` after periodic synchronization removed its Sheet user. `EndSessionAndDeactivateUser` treats an already-missing user row as acceptable while still requiring the session update itself to affect exactly one row.

- [x] **Step 5: Refresh and report to an open Admin Panel**

Store the modal window in `_adminWindow` for its lifetime and clear the field after `ShowDialog` returns.

Implement `AdminWindow.ApplyAutomaticSyncResult`. Always update `MessageTextBlock` with `AutomaticSyncStatus.Format`. Reload `UsersDataGrid` only when `result.IsSuccessful && result.HasChanges`. Preserve the selected user ID across reload; reselect it if present, otherwise call `ClearForm()`.

In the orchestrator `Completed` handler, format the Login status and forward the result to `_adminWindow`. Since calls originate on the WPF dispatcher, do not add a second dispatcher or background thread.

- [x] **Step 6: Verify integration and commit**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj --no-restore
dotnet build TimeLockApp.csproj --no-restore
```

Expected: all tests pass and build has zero errors. Then:

```powershell
git add -- MainWindow.xaml.cs AdminWindow.xaml.cs Services/AutomaticSyncStatus.cs TimeLockApp.Tests/AutomaticSyncStatusTests.cs TimeLockApp.Tests/Program.cs
git diff --cached --check
git commit -m "feat: sync sheet users on app lifecycle events"
```

---

### Task 5: Final Verification and Manual Sheet Check

**Files:**
- Modify: `docs/superpowers/specs/2026-08-07-automatic-google-sheet-sync-design.md` only if implementation-discovered behavior differs.
- Modify: `docs/superpowers/plans/2026-08-07-automatic-google-sheet-sync.md` for checkbox tracking.

**Interfaces:**
- Consumes: all prior task deliverables.
- Produces: verified master commits and a precise manual test handoff.

- [x] **Step 1: Run fresh automated verification**

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj --no-restore
dotnet build TimeLockApp.csproj --no-restore
git diff --check -- MainWindow.xaml.cs AdminWindow.xaml.cs Services data TimeLockApp.Tests docs/superpowers/specs/2026-08-07-automatic-google-sheet-sync-design.md docs/superpowers/plans/2026-08-07-automatic-google-sheet-sync.md
```

Expected: every C# test reports `PASS`, build exits zero, and scoped diff check is clean. Record any pre-existing dependency warning separately rather than claiming it was introduced or fixed here.

- [x] **Step 2: Inspect repository scope**

Use `git status --short` and `git show --stat` to confirm no `bin`, `obj`, WebView profile, or `timelock.db` file entered these commits. Preserve all pre-existing generated changes.

- [ ] **Step 3: Perform or hand off manual Windows verification**

With a test Sheet and test account:

1. start online and confirm immediate status;
2. start behind captive portal, complete Internet Auth, and confirm immediate sync;
3. edit a row and confirm Login/Admin reflects it within 30 seconds;
4. delete a row while that user has an active session and confirm the session continues;
5. Logout and separately allow expiry, confirming Google Sheet deactivation and a subsequent full refresh; and
6. disconnect the network, confirm a non-modal visible error, reconnect, and confirm the next trigger recovers.

- [x] **Step 4: Commit final documentation tracking**

```powershell
git add -- docs/superpowers/specs/2026-08-07-automatic-google-sheet-sync-design.md docs/superpowers/plans/2026-08-07-automatic-google-sheet-sync.md
git diff --cached --check
git commit -m "docs: finalize automatic sheet sync verification"
```

Skip the spec path if it did not change; do not mark the manual checklist complete unless it was actually performed.
