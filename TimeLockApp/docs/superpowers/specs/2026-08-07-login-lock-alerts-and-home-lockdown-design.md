# Login Lock, Session Alerts, and Windows Home Lockdown Design

## Objective

Make the Windows lock behavior reliable after session expiry or logout, add advance warnings suitable for the usual three-hour session, and reduce a Standard user's ability to end the application on Windows Home while automatically reopening it after termination.

## Scope

The application change will:

- install the existing low-level Windows keyboard hook for the lifetime of `MainWindow`;
- block Alt+Tab, Alt+Escape, Alt+F4, Ctrl+Escape, and both Windows keys on the Login/locked screen, Internet Authentication window, and application alert dialogs;
- allow those shortcuts during a normal user session and in the Admin Panel;
- show one warning when the remaining time crosses 30 minutes, 10 minutes, and 1 minute;
- pause countdown processing while a warning dialog is open and resume after OK;
- replace the existing 10-second warning;
- fail closed if the keyboard hook cannot be installed by showing an error and shutting down the application.
- recover any session left `active` by a previous process termination as `forced_logout` and deactivate its user before accepting a new login.

The Windows Home deployment change will:

- provide an opt-in setup for the dedicated Standard user account;
- disable Task Manager only for that Windows user through the documented per-user policy;
- start a hidden watchdog when that user signs in;
- check every two seconds and reopen TimeLockApp when it is not running;
- preserve prior Registry values before applying changes;
- provide a removal path that stops the watchdog and restores only the values changed by this package.

The change will not:

- claim that Windows Home can prevent every process-termination method;
- attempt to defeat an Administrator, `taskkill`, PowerShell, debuggers, or security tools;
- mark the WPF application as a protected or critical Windows process;
- modify Windows policy automatically when the WPF application starts;
- install a Windows service;
- block Ctrl+Alt+Delete, which Windows handles as a secure attention sequence;
- alter per-user allowed minutes stored in Google Sheets or SQLite.

## Keyboard-Hook Lifecycle

`MainWindow` retains the low-level hook delegate in `_keyboardProc`, installs the hook once during the window's loaded lifecycle, and stores the returned handle in `_keyboardHook`. Keeping the delegate referenced prevents garbage collection while native Windows code still calls it. `OnClosed` continues to unhook the stored handle.

If `SetWindowsHookEx` returns a zero handle, the application obtains the Win32 error, shows a clear blocking error, and shuts down. A lock application must not continue in a state where system shortcuts appear protected but are not.

The callback delegates state decisions to a pure lock policy. System shortcuts are blocked when any of these conditions is true:

- the Login/locked screen is active;
- Internet Authentication is open;
- an application alert dialog is open.

The policy returns false during an ordinary active session and while the Admin Panel is open, unless an application alert is explicitly active. The native callback remains responsible only for translating keyboard data and returning `1` for shortcuts the policy rejects.

## Session Warning Schedule

The warning thresholds are 1,800, 600, and 60 remaining seconds. The timer compares the previous remaining value with the new value and emits a warning only when it crosses a threshold from above. This prevents duplicate dialogs and avoids showing a higher-threshold warning when a short session starts below that threshold.

Warnings use the existing blocking `AlertWindow`. While it is open, `_isAlertOpen` remains true; timer ticks return without decrementing the remaining time. After the user presses OK, normal countdown resumes. At zero, the existing session-ending and expired-alert flow remains unchanged.

Warning messages are:

- `เหลือเวลาใช้งานอีก 30 นาที`
- `เหลือเวลาใช้งานอีก 10 นาที`
- `เหลือเวลาใช้งานอีก 1 นาที`

## Windows Home End-Task Mitigation

The current device reports `EditionID: CoreSingleLanguage`, which does not support the multi-app Assigned Access design. Windows Home also provides no supported way for an ordinary WPF process to make itself unkillable. The fallback therefore combines a per-user Task Manager policy with automatic recovery.

The setup script runs interactively as the dedicated Standard user. It validates the TimeLockApp executable, watchdog script, and backup location before making changes. It records whether these Registry values existed and their exact previous values:

- `HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System\DisableTaskMgr`
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TimeLockWatchdog`

After writing the backup atomically, setup sets `DisableTaskMgr` to `1`, registers the watchdog in the current user's Run key, and starts the watchdog. It refuses to overwrite a backup from an unresolved prior installation.

The watchdog receives the absolute TimeLockApp executable path, a two-second polling interval, and a package-owned PID-file path. It writes its process ID atomically, identifies the application by normalized executable path rather than process name alone, and launches only one replacement instance when none exists. Invalid paths or unexpected runtime errors are written to a package-owned log; the watchdog continues after transient errors.

When the replacement application starts, `DatabaseService` transactionally recovers every session left with `status = 'active'`. It sets `end_time` to the recovery time, calculates `used_seconds` from the stored start time capped to the session's allowed duration, and changes the status to `forced_logout`. When the referenced non-local user still exists, the same transaction sets `is_active = 0`, `is_consumed = 1`, and `deactivation_pending = 1` when an external user ID exists. The application completes this recovery before accepting login input and then remains on the Login screen.

Windows does not provide enough evidence to distinguish End task from an application crash, power loss, or computer restart. The approved policy intentionally treats every orphaned active session as forced logout and consumes/deactivates that user in all of those cases.

The removal script verifies the backup belongs to the current user, restores or removes each Registry value according to its recorded pre-install state, stops only the watchdog PID recorded by this package after confirming its command line/path, and removes the package-owned PID file. The backup is retained until all restore operations succeed.

Task Manager remains disabled for the Standard user throughout their Windows login session, including while a normal TimeLock session is active. Permanent application shutdown requires removal under that Standard account or maintenance through the separate Administrator account.

This fallback is mitigation, not absolute protection. A determined user who can execute another process-control tool may still terminate both processes. The watchdog's contract is to reopen TimeLockApp within approximately two seconds when it remains running.

## Components

### Shortcut lock policy

A small internal policy component accepts application state flags and keyboard modifier/key facts. It exposes deterministic methods for deciding whether the current screen is locked and whether a shortcut must be swallowed. It has no WPF or native dependencies and is covered by automated tests.

### Warning schedule

A small internal schedule component owns the three remaining-time thresholds and returns at most one warning for a countdown transition. `MainWindow` supplies the previous and current remaining seconds and renders the returned message.

### MainWindow integration

`MainWindow` owns native hook installation/uninstallation, maps its current state into the lock policy, and calls the warning schedule from `Timer_Tick`. Existing login, logout, expiry, Internet Authentication, and alert flows remain the sources of state truth.

At startup, immediately after database initialization, `MainWindow` invokes orphaned-session recovery before connectivity synchronization or login interaction. Any pending Google Sheet deactivation then uses the existing synchronization path.

### Home-lockdown package

PowerShell setup, watchdog, and removal scripts plus a concise deployment guide own all user-policy and recovery changes. Setup supports a validation-only mode that checks paths and current-user constraints without changing Registry, startup configuration, or processes.

## Error Handling and Recovery

- Hook installation failure displays the native error and shuts down.
- Hook removal is attempted only for a nonzero handle.
- Warning display remains modal; timer state is preserved across OK.
- A setup validation error makes no Registry or process changes and returns a nonzero exit code.
- Orphaned-session recovery updates the session and user in one transaction; any failure rolls back both changes and prevents normal startup.
- Setup creates the backup successfully before changing either Registry value.
- A partial setup failure invokes rollback from the completed backup.
- Removal stops only the verified package watchdog and restores exact prior values.
- The removal path is documented before the setup command.
- The separate Administrator account remains the recovery boundary.

## Testing

Automated tests will verify:

1. Login/locked state blocks system shortcuts.
2. Internet Authentication blocks system shortcuts.
3. An application alert blocks system shortcuts, including during an active session.
4. A normal active session permits the shortcuts.
5. The Admin Panel permits the shortcuts when no alert is active.
6. Alt+Tab, Alt+Escape, Alt+F4, Ctrl+Escape, and both Windows keys are recognized as blocked shortcuts.
7. Ordinary keys and unmodified Tab remain permitted.
8. Countdown transitions emit warnings at 1,800, 600, and 60 seconds exactly once.
9. Short sessions do not emit thresholds they start below.
10. The former 10-second warning is absent.
11. PowerShell validation-only mode rejects missing executables and makes no Registry changes.
12. Watchdog process matching uses the normalized executable path and does not mistake a same-named executable elsewhere for TimeLockApp.
13. Setup backup data distinguishes missing values from existing zero, one, and string values.
14. Removal restores fixture Registry state and targets only the recorded watchdog process in an isolated test hive/process fixture.
15. Startup recovery closes active sessions as `forced_logout`, preserves already-ended sessions, caps elapsed usage at the allowed duration, and deactivates the referenced user with pending Sheet synchronization.
16. Recovery rolls back all changes when any session/user update fails.

Manual Windows verification will confirm that the native hook blocks real shell shortcuts on Login, Internet Authentication, and alert dialogs; releases them during a normal session and Admin Panel; pauses the countdown while warnings are open; disables Task Manager for the Standard user; and reopens TimeLockApp after End task while the watchdog remains alive.

## Acceptance Criteria

- After logout or expiry, Alt+Tab and the other protected shortcuts no longer escape the Login screen.
- The same shortcuts are blocked in Internet Authentication and alert dialogs.
- Normal sessions and the Admin Panel retain expected keyboard switching behavior.
- A typical three-hour session warns at 30, 10, and 1 minute remaining and pauses for OK each time.
- Task Manager is unavailable to the configured Standard user and remains available to the separate Administrator.
- Ending TimeLockApp causes it to reopen within approximately two seconds while the watchdog is running.
- The restarted app converts the interrupted session to `forced_logout`, disables that user, and displays Login; the same rule applies after a crash, power loss, or reboot.
- Removal restores the Standard user's previous Task Manager and startup settings.
