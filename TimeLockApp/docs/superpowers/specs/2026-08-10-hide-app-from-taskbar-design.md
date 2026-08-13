# Hide TimeLockApp from the Taskbar

## Goal

Keep TimeLockApp windows usable while preventing the login and active-session windows from creating buttons on the Windows taskbar.

## Design

Set WPF `ShowInTaskbar="False"` on `MainWindow` and `UsageWindow`. These are the two user-facing windows used during normal login and timed sessions. No tray icon or alternative navigation is added; existing close, logout, and minimize behavior remains unchanged.

## Verification

Add a focused PowerShell regression test that reads both XAML files and verifies the explicit setting. Build the application afterward to validate the XAML compiles.
