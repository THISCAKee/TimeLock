# TimeLock Windows Shell Mode

Shell mode makes TimeLock appear instead of the normal Windows Desktop for the current Windows user after sign in. It changes only the current user's registry hive.

Keep a separate Administrator account before installing this mode. TimeLock does not replace Windows security controls and an Administrator can recover the account if the app or sign-in configuration fails.

## Validate

After publishing TimeLockApp, validate the executable path. Validation changes no Registry values, files, or processes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-TimeLockShell.ps1 `
  -AppPath 'C:\Apps\TimeLockApp\TimeLockApp.exe' `
  -ValidateOnly
```

## Install

Run as the intended Windows user:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-TimeLockShell.ps1 `
  -AppPath 'C:\Apps\TimeLockApp\TimeLockApp.exe'
```

Sign out and sign in again. TimeLock will start as the user's shell instead of Explorer. State is saved in `%LOCALAPPDATA%\TimeLockApp\Shell\backup.json`.

## Remove

Run as the same Windows user:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Remove-TimeLockShell.ps1
```

Removal restores the exact previous value at `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell` and then deletes the backup. Do not delete `backup.json` manually before removal succeeds.

## Recovery

If the user cannot reach a usable desktop, sign in with the separate Administrator account and restore the `Shell` value in the affected user's registry hive to `explorer.exe`. The backup file records the original value and registry root. Do not change the shell for all users or delete the user's profile.
