# Login Lock, Session Alerts, and Windows Home Lockdown Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reliably lock Windows shortcuts on Login/Auth/alerts, warn at 30/10/1 minutes with a paused countdown, convert interrupted sessions to forced logout, and provide a reversible Windows Home watchdog/Task Manager mitigation.

**Architecture:** Keep native hook ownership in `MainWindow`, but move state and shortcut decisions plus warning thresholds into pure internal services. Add transactional interrupted-session recovery to `DatabaseService`. Keep machine/user changes outside the WPF process in a separately tested PowerShell package under `deployment/windows-home-lockdown`.

**Tech Stack:** C# 14, .NET 10 WPF, Microsoft.Data.Sqlite 10.0.10, Win32 low-level keyboard hook, Windows PowerShell 5.1-compatible scripts, Registry HKCU Run/Policies

## Global Constraints

- Block protected shortcuts only on Login/locked screen, Internet Authentication, and application alert dialogs.
- Permit protected shortcuts during an ordinary active session and Admin Panel unless an alert is open.
- Pause countdown processing until OK closes each warning.
- Warn once at 1,800, 600, and 60 remaining seconds; remove the 10-second warning.
- Treat every orphaned active session after End task, crash, power loss, or reboot as `forced_logout` and deactivate its user.
- Do not claim absolute End-task prevention on Windows Home.
- Do not modify Registry or start the watchdog from the WPF application.
- Preserve and restore exact prior HKCU values during setup/removal.
- Keep unrelated generated/WebView/database changes untouched.

---

### Task 1: Pure Shortcut and Warning Policies

**Files:**
- Create: `Services/SystemShortcutPolicy.cs`
- Create: `Services/SessionWarningSchedule.cs`
- Create: `TimeLockApp.Tests/LockAndWarningTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

**Interfaces:**
- Produces: `SystemShortcutPolicy.ShouldBlock(bool, bool, bool, bool) -> bool`.
- Produces: `SystemShortcutPolicy.IsBlockedShortcut(int, bool, bool) -> bool`.
- Produces: `SessionWarningSchedule.GetCrossedWarning(int, int) -> SessionWarning?`.
- Produces: `SessionWarning(int RemainingSeconds, string Message)`.

- [x] **Step 1: Add failing policy tests**

Create `TimeLockApp.Tests/LockAndWarningTests.cs`:

```csharp
using TimeLockApp.Services;

internal static class LockAndWarningTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("login blocks shortcuts", () =>
            AssertTrue(SystemShortcutPolicy.ShouldBlock(
                isSessionActive: false,
                isAdminPanelOpen: false,
                isNetworkAuthOpen: false,
                isAlertOpen: false)));
        yield return ("internet auth blocks shortcuts", () =>
            AssertTrue(SystemShortcutPolicy.ShouldBlock(
                true, false, true, false)));
        yield return ("active alert blocks shortcuts", () =>
            AssertTrue(SystemShortcutPolicy.ShouldBlock(
                true, false, false, true)));
        yield return ("normal active session permits shortcuts", () =>
            AssertFalse(SystemShortcutPolicy.ShouldBlock(
                true, false, false, false)));
        yield return ("admin permits shortcuts", () =>
            AssertFalse(SystemShortcutPolicy.ShouldBlock(
                false, true, false, false)));
        yield return ("protected combinations are recognized",
            ProtectedCombinationsAreRecognized);
        yield return ("ordinary keys are permitted",
            OrdinaryKeysArePermitted);
        yield return ("warnings occur at approved thresholds",
            WarningsOccurAtApprovedThresholds);
        yield return ("short sessions skip higher warnings",
            ShortSessionsSkipHigherWarnings);
        yield return ("ten second warning is absent",
            TenSecondWarningIsAbsent);
    }

    private static void ProtectedCombinationsAreRecognized()
    {
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x09, true, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x1B, true, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x73, true, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x1B, false, true));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x5B, false, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x5C, false, false));
    }

    private static void OrdinaryKeysArePermitted()
    {
        AssertFalse(SystemShortcutPolicy.IsBlockedShortcut(0x09, false, false));
        AssertFalse(SystemShortcutPolicy.IsBlockedShortcut(0x41, false, false));
    }

    private static void WarningsOccurAtApprovedThresholds()
    {
        AssertWarning(1801, 1800, 1800, "เหลือเวลาใช้งานอีก 30 นาที");
        AssertWarning(601, 600, 600, "เหลือเวลาใช้งานอีก 10 นาที");
        AssertWarning(61, 60, 60, "เหลือเวลาใช้งานอีก 1 นาที");
    }

    private static void ShortSessionsSkipHigherWarnings()
    {
        AssertNull(SessionWarningSchedule.GetCrossedWarning(1200, 1199));
        AssertNull(SessionWarningSchedule.GetCrossedWarning(300, 299));
    }

    private static void TenSecondWarningIsAbsent()
    {
        AssertNull(SessionWarningSchedule.GetCrossedWarning(11, 10));
    }

    private static void AssertWarning(
        int previous,
        int current,
        int expectedSeconds,
        string expectedMessage)
    {
        SessionWarning warning =
            SessionWarningSchedule.GetCrossedWarning(previous, current)
            ?? throw new InvalidOperationException("Expected warning.");
        AssertTrue(warning.RemainingSeconds == expectedSeconds);
        AssertTrue(warning.Message == expectedMessage);
    }

    private static void AssertTrue(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected true.");
    }

    private static void AssertFalse(bool condition) => AssertTrue(!condition);

    private static void AssertNull(object? value)
    {
        if (value != null) throw new InvalidOperationException("Expected null.");
    }
}
```

Extend the test list in `TimeLockApp.Tests/Program.cs` with:

```csharp
tests = tests
    .Concat(LockAndWarningTests.All())
    .ToArray();
```

- [x] **Step 2: Verify RED**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj --no-restore
```

Expected: compilation fails because `SystemShortcutPolicy`, `SessionWarningSchedule`, and `SessionWarning` do not exist. No production integration changes are allowed yet.

- [x] **Step 3: Implement the pure policies**

Create `Services/SystemShortcutPolicy.cs`:

```csharp
namespace TimeLockApp.Services;

internal static class SystemShortcutPolicy
{
    internal static bool ShouldBlock(
        bool isSessionActive,
        bool isAdminPanelOpen,
        bool isNetworkAuthOpen,
        bool isAlertOpen)
    {
        return isNetworkAuthOpen ||
               isAlertOpen ||
               (!isSessionActive && !isAdminPanelOpen);
    }

    internal static bool IsBlockedShortcut(
        int virtualKey,
        bool altPressed,
        bool controlPressed)
    {
        return virtualKey == 0x5B ||
               virtualKey == 0x5C ||
               (altPressed &&
                (virtualKey == 0x09 ||
                 virtualKey == 0x1B ||
                 virtualKey == 0x73)) ||
               (controlPressed && virtualKey == 0x1B);
    }
}
```

Create `Services/SessionWarningSchedule.cs`:

```csharp
namespace TimeLockApp.Services;

internal sealed record SessionWarning(
    int RemainingSeconds,
    string Message);

internal static class SessionWarningSchedule
{
    private static readonly SessionWarning[] Warnings =
    {
        new(1800, "เหลือเวลาใช้งานอีก 30 นาที"),
        new(600, "เหลือเวลาใช้งานอีก 10 นาที"),
        new(60, "เหลือเวลาใช้งานอีก 1 นาที")
    };

    internal static SessionWarning? GetCrossedWarning(
        int previousSeconds,
        int currentSeconds)
    {
        if (currentSeconds >= previousSeconds)
        {
            return null;
        }

        return Warnings.FirstOrDefault(warning =>
            previousSeconds > warning.RemainingSeconds &&
            currentSeconds <= warning.RemainingSeconds);
    }
}
```

- [x] **Step 4: Verify GREEN and commit**

Run the test command again. Expected: all existing database tests and all new policy tests pass.

```powershell
git add -- Services/SystemShortcutPolicy.cs Services/SessionWarningSchedule.cs TimeLockApp.Tests/LockAndWarningTests.cs TimeLockApp.Tests/Program.cs
git commit -m "test: define lock and warning policies"
```

---

### Task 2: Interrupted Session Recovery

**Files:**
- Create: `TimeLockApp.Tests/InterruptedSessionRecoveryTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`
- Modify: `data/DatabaseService.cs`
- Modify: `MainWindow.xaml.cs`

**Interfaces:**
- Produces: `DatabaseService.RecoverInterruptedSessions(DateTime recoveryTime) -> int` (internal).
- Consumes: current `sessions.start_time` format `yyyy-MM-dd HH:mm:ss` and existing user deactivation columns.

- [x] **Step 1: Add failing recovery tests**

Add tests using isolated `TestDatabase` fixtures that:

```csharp
// Active session started at 10:00, recovered at 10:20.
// Expected: status forced_logout, end_time 10:20, used_seconds 1200,
// user inactive + consumed + deactivation_pending.

// Active 10-minute session started at 10:00, recovered at 11:00.
// Expected used_seconds is capped at 600.

// Existing completed session remains byte-for-byte unchanged.

// BEFORE UPDATE trigger on users executes RAISE(ABORT, 'forced failure').
// Expected recovery throws and the session remains active with null end_time.
```

Use literal timestamps and direct fixture SQL; do not compute expected values with the production helper. Register all tests through `InterruptedSessionRecoveryTests.All()` in `Program.cs`.

- [x] **Step 2: Verify RED**

Run the C# test executable. Expected: compilation fails because `RecoverInterruptedSessions` does not exist.

- [x] **Step 3: Implement transactional recovery**

Add internal `RecoverInterruptedSessions(DateTime recoveryTime)` to `DatabaseService`. It must:

```csharp
// 1. Begin one SQLite transaction.
// 2. Read id, nullable user_id, start_time, allowed_minutes for status='active'.
// 3. Parse start_time exactly with CultureInfo.InvariantCulture.
// 4. Calculate max(0, min(elapsedSeconds, allowed_minutes * 60)).
// 5. UPDATE the session to end_time, used_seconds, status='forced_logout'.
// 6. For a non-null user ID, UPDATE non-local user:
//    is_active=0, is_consumed=1,
//    deactivation_pending=CASE WHEN external_user_id IS NULL THEN 0 ELSE 1 END.
// 7. Commit and return recovered count; rollback and rethrow on any error.
```

Copy active rows into an in-memory list before issuing updates so the reader is disposed. Use the existing timestamp format `yyyy-MM-dd HH:mm:ss`. Reject unparseable legacy timestamps rather than partially recovering.

- [x] **Step 4: Integrate startup recovery**

Immediately after `_databaseService.InitializeDatabase()` in `MainWindow` constructor, call:

```csharp
_databaseService.RecoverInterruptedSessions(DateTime.Now);
```

This occurs before Google synchronization and before the user can interact with Login.

- [x] **Step 5: Verify GREEN and commit**

Run all C# tests and build the WPF project. Expected: recovery tests pass, existing session tests pass, build has zero errors.

```powershell
git add -- data/DatabaseService.cs MainWindow.xaml.cs TimeLockApp.Tests/InterruptedSessionRecoveryTests.cs TimeLockApp.Tests/Program.cs
git commit -m "feat: force logout interrupted sessions"
```

---

### Task 3: Native Hook and Warning Integration

**Files:**
- Modify: `MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `SystemShortcutPolicy` and `SessionWarningSchedule` from Task 1.
- Preserves: existing `_isSessionActive`, `_isAdminPanelOpen`, `_isNetworkAuthOpen`, `_isAlertOpen` state ownership.

- [x] **Step 1: Verify the current integration failure**

Run a build and confirm the existing `CS0169` warning for `_keyboardProc` or inspect that no call assigns the delegate/installs the hook. This is the observed regression evidence; policy behavior is already protected by Task 1 tests.

- [x] **Step 2: Install the hook fail-closed**

In the constructor assign:

```csharp
_keyboardProc = KeyboardHookCallback;
```

At the start of `MainWindow_Loaded`, before the connectivity guard, call a new method:

```csharp
private bool EnsureKeyboardHookInstalled()
{
    if (_keyboardHook != IntPtr.Zero)
    {
        return true;
    }

    _keyboardHook = InstallKeyboardHook(_keyboardProc);

    if (_keyboardHook != IntPtr.Zero)
    {
        return true;
    }

    int errorCode = Marshal.GetLastWin32Error();
    MessageBox.Show(
        $"ไม่สามารถเปิดระบบล็อกแป้นพิมพ์ได้ (Win32: {errorCode})",
        "เริ่มระบบล็อกไม่สำเร็จ",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
    Application.Current.Shutdown(-1);
    return false;
}
```

Return from `MainWindow_Loaded` when it returns false.

- [x] **Step 3: Delegate callback decisions to the policy**

Replace `IsLoginLocked` and the inline shortcut expression with:

```csharp
bool shouldBlock = SystemShortcutPolicy.ShouldBlock(
    _isSessionActive,
    _isAdminPanelOpen,
    _isNetworkAuthOpen,
    _isAlertOpen);

bool isBlockedShortcut =
    SystemShortcutPolicy.IsBlockedShortcut(
        virtualKey,
        altPressed,
        controlPressed);
```

Swallow only when both values are true. Keep `CallNextHookEx` for all other keys.

- [x] **Step 4: Replace the 10-second warning**

At the start of the decrementing part of `Timer_Tick`, retain the previous value:

```csharp
int previousSeconds = _remainingSeconds;
_remainingSeconds--;
_usageWindow?.UpdateRemainingTime(_remainingSeconds);

SessionWarning? warning =
    SessionWarningSchedule.GetCrossedWarning(
        previousSeconds,
        _remainingSeconds);

if (warning != null)
{
    ShowBlockingAlert("แจ้งเตือน", warning.Message);
}
```

Delete the old `_remainingSeconds == 10` branch. Keep the existing `_isAlertOpen` early return so nested dispatcher ticks do not decrement while OK is pending.

- [x] **Step 5: Verify and commit**

Run all C# tests and `dotnet build TimeLockApp.csproj --no-restore`. Expected: zero test failures, zero build errors, and no unused `_keyboardProc` warning.

```powershell
git add -- MainWindow.xaml.cs
git commit -m "fix: enforce login shortcuts and session alerts"
```

---

### Task 4: Reversible Windows Home Lockdown Package

**Files:**
- Create: `deployment/windows-home-lockdown/TimeLockHomeLockdown.psm1`
- Create: `deployment/windows-home-lockdown/Install-TimeLockHomeLockdown.ps1`
- Create: `deployment/windows-home-lockdown/TimeLockWatchdog.ps1`
- Create: `deployment/windows-home-lockdown/Remove-TimeLockHomeLockdown.ps1`
- Create: `deployment/windows-home-lockdown/README.md`
- Create: `TimeLockApp.Tests/WindowsHomeLockdown.Tests.ps1`

**Interfaces:**
- Setup consumes absolute `-AppPath` and optional `-StateDirectory`.
- Setup produces atomic `backup.json`, copied watchdog, HKCU `DisableTaskMgr=1`, HKCU Run `TimeLockWatchdog`, and a running watchdog.
- Watchdog consumes absolute app/state paths and `-PollSeconds` (default `2`).
- Removal consumes `backup.json` and restores exact prior Registry state.

- [x] **Step 1: Write non-mutating script tests**

Create a dependency-free PowerShell test runner that imports the module and verifies:

```powershell
# Assert-NormalizedExecutablePath resolves a real fixture .exe and rejects missing/non-.exe paths.
# Get-RegistryValueSnapshot distinguishes Exists=$false from DWORD 0/1 and string values.
# Restore-RegistryValueSnapshot recreates the exact kind/value under HKCU:\Software\TimeLockApp.Tests\<guid>.
# Test-ProcessExecutablePath returns false for a same-named executable at a different path.
# Install script -ValidateOnly performs zero writes under the fixture registry root and state directory.
# Backup JSON round-trips CurrentUserSid, DisableTaskMgr snapshot, Run snapshot, and AppPath.
```

Each test creates a GUID-scoped HKCU fixture and a temp directory, validates resolved absolute paths before cleanup, and removes only those exact fixtures in `finally`.

- [x] **Step 2: Verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File TimeLockApp.Tests\WindowsHomeLockdown.Tests.ps1
```

Expected: failure because the module and scripts do not exist.

- [x] **Step 3: Implement the shared module**

Implement and export these exact functions:

```powershell
Assert-NormalizedExecutablePath -Path <string>        # returns full .exe path or throws
Get-RegistryValueSnapshot -KeyPath <string> -Name <string>
Restore-RegistryValueSnapshot -KeyPath <string> -Name <string> -Snapshot <psobject>
Write-JsonAtomically -Path <string> -Value <object>
Test-ProcessExecutablePath -ProcessId <int> -ExpectedPath <string>
```

Snapshots must contain `Exists`, `Kind`, and `Value`. Atomic JSON writing uses a sibling temporary file followed by `Move-Item -LiteralPath ... -Force`. Process matching reads `Win32_Process.ExecutablePath`, normalizes both paths with `[IO.Path]::GetFullPath`, and compares ordinal-ignore-case.

- [x] **Step 4: Implement setup and watchdog**

`Install-TimeLockHomeLockdown.ps1` must:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppPath,
    [string]$StateDirectory = "$env:LOCALAPPDATA\TimeLockApp\Lockdown",
    [switch]$ValidateOnly,
    [string]$RegistryRoot = 'HKCU:\Software\Microsoft\Windows\CurrentVersion'
)
```

It rejects an Administrator identity, validates paths, and returns after validation when requested. Otherwise it creates the state directory, refuses an existing unresolved backup, snapshots `Policies\System\DisableTaskMgr` and `Run\TimeLockWatchdog`, atomically writes backup JSON, copies the watchdog, sets the two values, and starts the copied watchdog hidden. Catch performs snapshot-based rollback before rethrowing.

`TimeLockWatchdog.ps1` must validate all absolute paths, atomically write its PID, and loop:

```powershell
while ($true) {
    $running = Get-CimInstance Win32_Process |
        Where-Object { $_.ExecutablePath -and
            ([IO.Path]::GetFullPath($_.ExecutablePath) -ieq $AppPath) }
    if (-not $running) { Start-Process -FilePath $AppPath }
    Start-Sleep -Seconds $PollSeconds
}
```

Wrap each iteration in `try/catch`, append errors to the state log, and continue after the polling interval.

- [x] **Step 5: Implement removal and guide**

Removal validates current SID against backup, restores both snapshots, verifies the PID belongs to the copied watchdog command/path before `Stop-Process`, and deletes the PID only after success. Keep backup JSON until every restore step succeeds.

README must put recovery first: sign in as the separate Administrator, then run removal in the Standard user's context or restore the documented HKCU values offline. It must state that the mitigation does not defeat `taskkill`, PowerShell, or administrators.

- [x] **Step 6: Verify scripts and commit**

Run the PowerShell tests, then run setup with `-ValidateOnly` against the built TimeLockApp executable. Confirm Registry values before and after are identical.

```powershell
git add -- deployment/windows-home-lockdown TimeLockApp.Tests/WindowsHomeLockdown.Tests.ps1
git commit -m "feat: add Windows Home lockdown watchdog"
```

---

### Task 5: Final Verification and Documentation Alignment

**Files:**
- Modify: `docs/superpowers/specs/2026-08-07-login-lock-alerts-and-home-lockdown-design.md` only if implementation-discovered facts require correction.
- Create: `Services/SingleInstanceGuard.cs`
- Modify: `App.xaml`
- Modify: `App.xaml.cs`
- Create: `TimeLockApp.Tests/SingleInstanceGuardTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs`

- [x] **Step 0: Prevent duplicate startup recovery**

Add a session-local named mutex before constructing `MainWindow`. A duplicate exits before database initialization so it cannot treat the owner's active session as interrupted. Verify ownership rejection and release with an automated test.

- [x] **Step 1: Run complete verification**

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File TimeLockApp.Tests\WindowsHomeLockdown.Tests.ps1
dotnet build TimeLockApp.csproj --no-restore
git diff --check
```

Expected: every C# and PowerShell test passes, build exits zero, and no scoped whitespace errors exist.

- [ ] **Step 2: Manual Windows checklist**

Verify Login, post-logout Login, post-expiry Login, Internet Auth, and alerts block protected shortcuts. Verify active session and Admin allow them. For a three-hour fixture, test each warning through a test-time configuration or controlled clock rather than waiting three hours. On the Standard test account, apply setup, confirm Task Manager is disabled, end TimeLockApp, confirm restart within approximately two seconds and forced user deactivation, then run removal and confirm prior Registry values return.

- [x] **Step 3: Commit final documentation if changed**

```powershell
git add -- docs/superpowers/specs/2026-08-07-login-lock-alerts-and-home-lockdown-design.md
git commit -m "docs: finalize Windows Home lock verification"
```

Skip this commit when the spec did not change.
