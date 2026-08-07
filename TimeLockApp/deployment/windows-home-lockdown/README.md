# Windows Home Lockdown

This package is a mitigation for a dedicated Windows Standard user. It disables Task Manager for that user and runs a watchdog that reopens TimeLockApp within approximately two seconds. It cannot defeat administrators, `taskkill`, PowerShell, debuggers, or security software.

## Recovery first

Keep a separate Windows Administrator account. To remove the lockdown, run `Remove-TimeLockHomeLockdown.ps1` in the configured Standard user's context. If that account cannot run the script, sign in as Administrator, load that user's registry hive for offline repair, restore the two values recorded in `%LOCALAPPDATA%\TimeLockApp\Lockdown\backup.json`, then remove the Run entry and watchdog files.

The affected values are:

- `HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System\DisableTaskMgr`
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TimeLockWatchdog`

## Validate

Build or publish TimeLockApp, then run as the intended Standard user:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-TimeLockHomeLockdown.ps1 `
  -AppPath 'C:\Apps\TimeLockApp\TimeLockApp.exe' `
  -ValidateOnly
```

Validation changes no Registry values, files, or processes.

## Install

Run as the dedicated Standard user, not Administrator:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-TimeLockHomeLockdown.ps1 `
  -AppPath 'C:\Apps\TimeLockApp\TimeLockApp.exe'
```

Sign out and back in. Task Manager remains disabled for this Windows user throughout the login session. The watchdog starts from the current user's Run key.

## Remove

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Remove-TimeLockHomeLockdown.ps1
```

Removal restores the exact Registry values saved before installation. Do not delete `backup.json` manually before removal succeeds.
