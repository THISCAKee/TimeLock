# TimeLock Inno Setup Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Build a reproducible self-contained Windows x64 publish and package it as `deployment/installer/output/TimeLock-Setup.exe` with Inno Setup.

**Architecture:** `Build-Installer.ps1` owns clean publish and compiler discovery. `TimeLock.iss` packages only the generated publish directory, creates Start Menu/Desktop shortcuts, and registers uninstall metadata. `README.md` documents prerequisites, build, credential placement, and verification.

**Tech Stack:** .NET 10 WPF, PowerShell, Inno Setup 6.

## Global Constraints

- Publish target is `net10.0-windows`, `Release`, `win-x64`, self-contained.
- Never package `Secrets/service-account.json`.
- Never package runtime databases, WebView2 cache, debug output, or unrelated build artifacts.
- Installer output is `deployment/installer/output/TimeLock-Setup.exe`.
- MSI/MSIX, code signing, and WebView2 Runtime installation are out of scope.

---

### Task 1: Add the Inno Setup definition

**Files:**
- Create: `deployment/installer/TimeLock.iss`

**Interfaces:**
- Consumes: `deployment/installer/publish/TimeLockApp.exe` and all files below the publish directory.
- Produces: Inno Setup metadata and installer output named `TimeLock-Setup.exe`.

- [x] **Step 1: Define application metadata and directories**

Use `AppName=TimeLock`, `AppPublisher=TimeLock`, `DefaultDirName={autopf}\TimeLock`, `DefaultGroupName=TimeLock`, and `OutputBaseFilename=TimeLock-Setup`. Set `ArchitecturesInstallIn64BitMode=x64` and require 64-bit Windows.

- [x] **Step 2: Package the publish directory**

Use a single recursive `[Files]` entry from `{#SourceDir}\*` to `{app}` with `recursesubdirs createallsubdirs`. Do not add any `Secrets` path or source-tree wildcard.

- [x] **Step 3: Add shortcuts and uninstall registration**

Create a Start Menu shortcut by default and a Desktop shortcut controlled by a task. Point both to `{app}\TimeLockApp.exe`; add an uninstaller shortcut and standard `[UninstallDelete]` cleanup for the installed application directory.

### Task 2: Add the reproducible build script

**Files:**
- Create: `deployment/installer/Build-Installer.ps1`

**Interfaces:**
- Consumes: repository root, `TimeLockApp.csproj`, installed `dotnet`, and an Inno Setup compiler at a standard path or `ISCC.exe` on `PATH`.
- Produces: clean `deployment/installer/publish` and `deployment/installer/output/TimeLock-Setup.exe`.

- [x] **Step 1: Resolve paths and validate tools**

Resolve the repository root relative to the script, verify `TimeLockApp.csproj`, invoke `dotnet --version`, and find `ISCC.exe` from `-InnoSetupPath`, `ISCC.exe` on `PATH`, or the standard Inno Setup installation paths. Stop with a clear error if any prerequisite is absent.

- [x] **Step 2: Publish cleanly**

Remove only the script-owned `publish` and `output` directories after resolving them under `deployment/installer`. Run `dotnet publish` with `-c Release -r win-x64 --self-contained true -o <publish>`. After publishing, fail if `Secrets\service-account.json` exists under the publish directory.

- [x] **Step 3: Compile and validate output**

Invoke `ISCC.exe` with `TimeLock.iss`, fail on nonzero exit code, and verify `output\TimeLock-Setup.exe` exists and has a nonzero length. Print the final path and size.

### Task 3: Document installer usage

**Files:**
- Create: `deployment/installer/README.md`

**Interfaces:**
- Consumes: the script parameters and generated output paths from Tasks 1–2.
- Produces: operator documentation for prerequisites, build, install, credential setup, and verification.

- [x] **Step 1: Document prerequisites and build command**

Document .NET 10 SDK, Inno Setup 6, and the command `powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1` from `deployment\installer`.

- [x] **Step 2: Document security and runtime setup**

State explicitly that `Secrets\service-account.json` is not included and must be provisioned separately at the application’s expected path. Document that the target machine needs the compatible WebView2 Runtime.

### Task 4: Verify the package

**Files:**
- Test: generated `deployment/installer/publish` and `deployment/installer/output/TimeLock-Setup.exe`

- [x] **Step 1: Run the build script**

Run the build script with the installed Inno Setup compiler and capture its exit code and output.

- [x] **Step 2: Verify publish contents**

Confirm `TimeLockApp.exe` exists, `Secrets\service-account.json` does not exist, and no WebView2 cache directory is present in the publish root.

- [x] **Step 3: Verify installer output**

Confirm `TimeLock-Setup.exe` exists and has nonzero length. Run `dotnet build .\TimeLockApp.csproj --no-restore` as a regression check.

- [x] **Step 4: Commit the installer implementation**

```powershell
git add deployment/installer
git commit -m "build: add Inno Setup installer"
```
