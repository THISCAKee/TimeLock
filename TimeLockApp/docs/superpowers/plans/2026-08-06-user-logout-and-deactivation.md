# User Logout and Permanent Deactivation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a confirmed, unobtrusive logout button to the usage timer and permanently deactivate timed users locally and in Google Sheets after logout or natural expiration.

**Architecture:** `UsageWindow` emits a confirmed logout event, while `MainWindow` owns session termination. `DatabaseService` atomically ends the session and marks the user consumed with a durable pending-write flag; `UserSyncService` serializes immediate and retried Google Sheet deactivation through `GoogleSheetsUserService`.

**Tech Stack:** .NET 10, C#, WPF, Microsoft.Data.Sqlite, Google Sheets API v4

## Global Constraints

- Password storage and validation remain unchanged.
- Keep the Google Sheet layout `Users!A2:F`; column F remains `IsActive`.
- Early logout forfeits all remaining time and records status `logged_out`.
- Natural expiration records status `completed`.
- Both outcomes permanently block another login.
- Local blocking must survive Google API/network failures and later Sheet pulls.
- Codex must not build, run, or test the application; the user performs runtime verification.
- Preserve unrelated working-tree changes and do not commit implementation files unless requested.

---

## File Map

- Modify `data/DatabaseService.cs`: schema migration, atomic session/user deactivation, pending-write query/clear, consumed-aware authentication and sync upsert.
- Modify `Services/GoogleSheetsUserService.cs`: writable scope and column-F update by external user ID.
- Modify `Services/UserSyncService.cs`: serialize and retry pending deactivation writes before normal pulls.
- Modify `UsageWindow.xaml`: small neutral logout control.
- Modify `UsageWindow.xaml.cs`: confirmation and `LogoutRequested` event.
- Modify `MainWindow.xaml.cs`: current-user tracking and unified logout/expiration lifecycle.

### Task 1: Persist consumed users and pending Sheet writes

**Files:**
- Modify: `data/DatabaseService.cs`

**Interfaces:**
- Produces: `EndSessionAndDeactivateUser(int sessionId, int userId, int usedSeconds, string status) -> void`.
- Produces: `GetPendingUserDeactivations() -> List<PendingUserDeactivation>`.
- Produces: `MarkUserDeactivationSynchronized(int userId) -> void`.
- Produces: `PendingUserDeactivation.LocalUserId` and `.ExternalUserId`.

- [ ] **Step 1: Add migration-safe columns**

After the existing `is_local_only` migration, add:

```csharp
AddColumnIfMissing(
    connection,
    "users",
    "is_consumed",
    "INTEGER NOT NULL DEFAULT 0");

AddColumnIfMissing(
    connection,
    "users",
    "deactivation_pending",
    "INTEGER NOT NULL DEFAULT 0");
```

- [ ] **Step 2: Preserve consumed state during Sheet upsert**

Include `is_consumed` and `deactivation_pending` as zero for new Sheet users. In the conflict update, replace direct active assignment with:

```sql
is_active = CASE
    WHEN users.is_consumed = 1 THEN 0
    ELSE excluded.is_active
END
```

Do not overwrite existing `is_consumed` or `deactivation_pending` on conflict. Exclude consumed users from `DeleteMissingSheetUsers` by adding `AND is_consumed = 0` to both deletion branches.

- [ ] **Step 3: Block consumed users during authentication**

Add this predicate to `GetUserByUsernameAndPassword`:

```sql
AND is_consumed = 0
```

Extend user SELECTs and `UserRecord` with:

```csharp
public bool IsConsumed { get; set; }
public bool DeactivationPending { get; set; }
```

- [ ] **Step 4: Atomically end a session and consume the user**

Implement one SQLite transaction that updates the session and then the non-local user:

```sql
UPDATE sessions
SET end_time = $end_time,
    used_seconds = $used_seconds,
    status = $status
WHERE id = $session_id;

UPDATE users
SET is_active = 0,
    is_consumed = 1,
    deactivation_pending = CASE
        WHEN external_user_id IS NULL THEN 0
        ELSE 1
    END
WHERE id = $user_id
  AND is_local_only = 0;
```

Use parameters for all values and rollback on any exception.

- [ ] **Step 5: Add pending-write accessors**

Return only rows that can be mapped to Sheets:

```sql
SELECT id, external_user_id
FROM users
WHERE deactivation_pending = 1
  AND external_user_id IS NOT NULL
  AND is_consumed = 1;
```

Clear a successful pending marker with a parameterized update constrained by local user ID and `is_consumed = 1`.

- [ ] **Step 6: Static verification only**

Inspect every users-table SELECT index after adding columns. Confirm authentication requires active and not consumed, the transaction updates session before commit, and admin/local-only rows cannot be consumed by this method. Do not run database migrations or tests.

### Task 2: Write `FALSE` to Google Sheets and retry pending work

**Files:**
- Modify: `Services/GoogleSheetsUserService.cs`
- Modify: `Services/UserSyncService.cs`

**Interfaces:**
- Produces: `GoogleSheetsUserService.SetUserActiveAsync(int externalUserId, bool isActive, CancellationToken cancellationToken = default) -> Task<bool>`; false means no matching row.
- Produces: `UserSyncService.ProcessPendingDeactivationsAsync(CancellationToken cancellationToken = default) -> Task`.
- Consumes: pending database interfaces from Task 1.

- [ ] **Step 1: Upgrade the Google credential scope**

Replace:

```csharp
SheetsService.Scope.SpreadsheetsReadonly
```

with:

```csharp
SheetsService.Scope.Spreadsheets
```

The service account must be shared as Editor on the configured spreadsheet.

- [ ] **Step 2: Locate the Sheet row by external user ID**

Implement `SetUserActiveAsync` by reading `Users!A2:A`, parsing positive integer IDs with invariant culture, and finding the zero-based response index. Calculate the Sheet row as `index + 2`. Return false without an update when no ID matches.

- [ ] **Step 3: Update column F using RAW input**

Create a `ValueRange` containing one Boolean cell and update `Users!F{sheetRow}`:

```csharp
var valueRange = new ValueRange
{
    Values = new List<IList<object>>
    {
        new List<object> { isActive }
    }
};

SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest =
    service.Spreadsheets.Values.Update(
        valueRange,
        GoogleSheetsConfig.SpreadsheetId,
        $"{GoogleSheetsConfig.WorksheetName}!F{sheetRow}");

updateRequest.ValueInputOption =
    SpreadsheetsResource.ValuesResource.UpdateRequest
        .ValueInputOptionEnum.Raw;

await updateRequest.ExecuteAsync(cancellationToken);
```

- [ ] **Step 4: Add serialized pending processing**

`ProcessPendingDeactivationsAsync` acquires the existing `_syncGate`, calls a private core method, and releases in `finally`. The core loops through `GetPendingUserDeactivations()`, writes `false`, and clears the pending marker only when the Google method returns true.

Catch per-user Google exceptions except cancellation. Write the local/external IDs and message to `Debug.WriteLine`, leave the marker pending, and continue processing other users.

- [ ] **Step 5: Flush pending work before every pull**

Inside the already-locked `SynchronizeAsync`, call the private pending-processing core before `GetUsersAsync`. Do not call the public lock-acquiring method from inside `SynchronizeAsync`, which would deadlock.

- [ ] **Step 6: Static verification only**

Confirm there is one semaphore acquisition per public operation, no recursive acquisition, missing Sheet rows stay pending, successful rows clear pending, and the normal pull still executes after a failed write. Do not call Google APIs or run tests.

### Task 3: Add a low-emphasis confirmed logout control

**Files:**
- Modify: `UsageWindow.xaml`
- Modify: `UsageWindow.xaml.cs`

**Interfaces:**
- Produces: `event EventHandler? LogoutRequested`.
- Produces: `LogoutButton_Click(object sender, RoutedEventArgs e) -> void`.

- [ ] **Step 1: Add the compact button**

Keep the timer window compact and place `ออก` beside minimize in a right-aligned horizontal `StackPanel`. Use a 34x20 button, font size 10, neutral `#F3F4F6` background, muted `#6B7280` foreground, no border, and the existing rounded template style.

- [ ] **Step 2: Confirm before raising the event**

Add:

```csharp
public event EventHandler? LogoutRequested;
```

The click handler calls `MessageBox.Show(this, "ต้องการออกจากระบบหรือไม่?", "ยืนยันการออกจากระบบ", MessageBoxButton.YesNo, MessageBoxImage.Question)`. Raise `LogoutRequested` only for `MessageBoxResult.Yes`.

- [ ] **Step 3: Static verification only**

Confirm the button is smaller and less prominent than the time display, No has no side effect, and `UsageWindow` has no database, service, or session-status dependency. Do not open the window.

### Task 4: Unify early logout and natural expiration

**Files:**
- Modify: `MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `UsageWindow.LogoutRequested` from Task 3.
- Consumes: `DatabaseService.EndSessionAndDeactivateUser(...)` from Task 1.
- Consumes: `UserSyncService.ProcessPendingDeactivationsAsync(...)` from Task 2.
- Produces: `EndSessionAsync(string status, bool showExpiredAlert) -> Task`.

- [ ] **Step 1: Track the current local user**

Ensure the `MainWindow` constructor calls `_databaseService.InitializeDatabase()` immediately after `InitializeComponent()` so the migration columns exist before any sync or login query. Add `_currentUserId`. In `StartSession`, assign `user.Id`, create `UsageWindow`, subscribe `UsageWindow_LogoutRequested`, and then show it.

- [ ] **Step 2: Handle confirmed logout**

Add an `async void` WPF event handler that awaits:

```csharp
await EndSessionAsync(
    "logged_out",
    showExpiredAlert: false);
```

- [ ] **Step 3: Route timer expiration through the same path**

Change `Timer_Tick` to `async void`. When remaining time reaches zero, await:

```csharp
await EndSessionAsync(
    "completed",
    showExpiredAlert: true);
```

The existing `_sessionEnded` guard must be set before the first await to prevent duplicate termination.

- [ ] **Step 4: Refactor session cleanup**

Replace `EndSession` with `EndSessionAsync`. Stop the timer, clamp used seconds to zero or greater, call `EndSessionAndDeactivateUser`, unsubscribe the logout event, hide and clear the usage window, clear login fields, show and activate Login, and display the expired alert only when `showExpiredAlert` is true.

After the login UI is restored, await `ProcessPendingDeactivationsAsync`. Google failures are handled inside the service and must not reopen the session.

- [ ] **Step 5: Reset session identifiers**

After local persistence, reset `_currentSessionId` and `_currentUserId` to zero so a late event cannot target the previous session.

- [ ] **Step 6: Static verification only**

Trace both termination triggers. Confirm each reaches the atomic database method once, only timeout displays the expiry alert, and both eventually attempt pending Sheet writes. Do not run the application or tests.

### Task 5: User-operated verification handoff

**Files:**
- Review only: all files changed in Tasks 1-4

**Interfaces:**
- Produces: build and runtime checklist for the user.

- [ ] **Step 1: Review scoped diffs**

Use text search and `git diff --check` scoped to changed source. Confirm no password logic changed, the range remains `A2:F`, and no generated files are intentionally edited.

- [ ] **Step 2: Ask the user to build**

Provide:

```powershell
dotnet build TimeLockApp.csproj --no-restore
```

Do not claim build success until the user supplies zero-error output.

- [ ] **Step 3: Ask the user to verify runtime behavior**

Ask the user to verify confirmation No/Yes, `logged_out` and `completed` session history, blocked repeat login, online Sheet write, offline pending retry, manual Admin Sync retry, and non-consumption of the local administrator.
