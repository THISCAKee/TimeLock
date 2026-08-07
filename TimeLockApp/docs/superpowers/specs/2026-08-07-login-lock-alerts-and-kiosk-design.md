# Login Lock, Session Alerts, and Kiosk Protection Design

## Objective

Make the Windows lock behavior reliable after session expiry or logout, add advance warnings suitable for the usual three-hour session, and prevent a standard kiosk user from ending the application through Windows Task Manager.

## Scope

The application change will:

- install the existing low-level Windows keyboard hook for the lifetime of `MainWindow`;
- block Alt+Tab, Alt+Escape, Alt+F4, Ctrl+Escape, and both Windows keys on the Login/locked screen, Internet Authentication window, and application alert dialogs;
- allow those shortcuts during a normal user session and in the Admin Panel;
- show one warning when the remaining time crosses 30 minutes, 10 minutes, and 1 minute;
- pause countdown processing while a warning dialog is open and resume after OK;
- replace the existing 10-second warning;
- fail closed if the keyboard hook cannot be installed by showing an error and shutting down the application.

The deployment change will:

- provide an administrator-run, parameterized Windows multi-app kiosk setup for a dedicated Standard user;
- allow only TimeLockApp and Google Chrome in the restricted user experience;
- disable Task Manager End task for the kiosk user through supported Windows policy;
- provide a documented administrator escape and removal procedure;
- validate configuration inputs before changing Windows configuration.

The change will not:

- claim to prevent an Administrator from terminating the application;
- mark the WPF application as a protected or critical Windows process;
- modify Windows policy automatically when the application starts;
- install a watchdog service;
- block Ctrl+Alt+Delete, which Windows handles as a secure attention sequence;
- alter per-user allowed minutes stored in Google Sheets or SQLite.

## Keyboard-Hook Lifecycle

`MainWindow` retains the low-level hook delegate in `_keyboardProc`, installs the hook once during construction after WPF initialization, and stores the returned handle in `_keyboardHook`. Keeping the delegate referenced prevents garbage collection while native Windows code still calls it. `OnClosed` continues to unhook the stored handle.

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

## Kiosk and End-Task Protection

A normal desktop process cannot reliably deny termination to an Administrator. Microsoft reserves protected Windows service modes for Windows components or specially signed anti-malware services. End-task protection therefore belongs to Windows deployment policy, not application runtime code.

The repository will include a parameterized PowerShell setup script and matching removal script. The setup requires an elevated administrator session, a pre-existing Standard user profile, a published TimeLockApp executable path, and a Chrome executable path. It configures an Assigned Access restricted user experience that allows those two desktop applications and applies the Task Manager policy that denies End task to non-administrators. The setup must stop before applying configuration when the Windows edition, account, or executable paths are invalid.

The removal script clears only the configuration created by this deployment package and restores Task Manager policy for the kiosk account. Neither script runs from the WPF application. The administrator account remains outside the kiosk restriction and is the supported recovery path.

This design follows Microsoft's supported boundaries for [Assigned Access](https://learn.microsoft.com/en-us/windows/configuration/assigned-access/configure-single-app-kiosk), [Assigned Access policy settings](https://learn.microsoft.com/en-us/windows/configuration/assigned-access/policy-settings), and [Task Manager AllowEndTask](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-taskmanager).

## Components

### Shortcut lock policy

A small internal policy component accepts application state flags and keyboard modifier/key facts. It exposes deterministic methods for deciding whether the current screen is locked and whether a shortcut must be swallowed. It has no WPF or native dependencies and is covered by automated tests.

### Warning schedule

A small internal schedule component owns the three remaining-time thresholds and returns at most one warning for a countdown transition. `MainWindow` supplies the previous and current remaining seconds and renders the returned message.

### MainWindow integration

`MainWindow` owns native hook installation/uninstallation, maps its current state into the lock policy, and calls the warning schedule from `Timer_Tick`. Existing login, logout, expiry, Internet Authentication, and alert flows remain the sources of state truth.

### Kiosk deployment package

PowerShell setup/removal scripts and a concise deployment guide own all machine-level changes. The scripts support a validation-only mode so paths, account type, Windows edition, and generated Assigned Access configuration can be checked without applying policy.

## Error Handling and Recovery

- Hook installation failure displays the native error and shuts down.
- Hook removal is attempted only for a nonzero handle.
- Warning display remains modal; timer state is preserved across OK.
- A kiosk setup validation error makes no machine changes and returns a nonzero exit code.
- A partial kiosk setup failure reports the failed phase and directs the administrator to the removal script.
- The removal path is documented before the setup command so an administrator has recovery instructions before applying restrictions.
- Administrator privileges remain an explicit recovery boundary; the feature does not attempt to defeat them.

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
11. PowerShell validation mode accepts valid fixture inputs and rejects invalid account/path/configuration inputs without applying policy.

Manual Windows verification will confirm that the native hook blocks real shell shortcuts on Login, Internet Authentication, and alert dialogs; releases them during a normal session and Admin Panel; pauses the countdown while warnings are open; and that a Standard kiosk user cannot use Task Manager End task while an Administrator can remove the kiosk configuration.

## Acceptance Criteria

- After logout or expiry, Alt+Tab and the other protected shortcuts no longer escape the Login screen.
- The same shortcuts are blocked in Internet Authentication and alert dialogs.
- Normal sessions and the Admin Panel retain expected keyboard switching behavior.
- A typical three-hour session warns at 30, 10, and 1 minute remaining and pauses for OK each time.
- A dedicated Standard kiosk account cannot end TimeLockApp through Task Manager.
- An Administrator can remove the kiosk configuration and terminate the application for maintenance.
