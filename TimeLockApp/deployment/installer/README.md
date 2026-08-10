# TimeLock Installer

This folder builds a self-contained Windows x64 installer using Inno Setup.

## Requirements

- Windows x64
- .NET 10 SDK
- Inno Setup 6 (`ISCC.exe`)
- The compatible Microsoft WebView2 Runtime on the target machine

## Build

From this folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1
```

If Inno Setup is installed in a non-standard location:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1 -InnoSetupPath 'C:\Path\To\ISCC.exe'
```

The script publishes `TimeLockApp` as Release, `win-x64`, self-contained and creates:

```text
output\TimeLock-Setup.exe
```

The publish and output folders are owned by the build script and are recreated on each run.

## Credentials

`Secrets\service-account.json` is intentionally not included in the installer. Provision it separately after installation at the path expected by the application before using Google Sheets synchronization. Do not commit or distribute this credential through the installer artifact.

## Installation

Run `TimeLock-Setup.exe` as an administrator. The installer places the application in `Program Files\TimeLock`, creates a Start Menu shortcut, and offers an optional Desktop shortcut. Windows Apps & Features can be used to uninstall it.
