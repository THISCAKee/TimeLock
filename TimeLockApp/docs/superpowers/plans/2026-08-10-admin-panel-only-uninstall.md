# Admin Panel–Only Uninstall Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** Ensure TimeLock can be uninstalled only through the authenticated WPF Admin Panel.

**Architecture:** Reuse the existing `AdminWindow` uninstall button and `ApplicationUninstaller` service. Configure Inno Setup with `Uninstallable=yes` and `CreateUninstallRegKey=no` so it generates `unins000.exe` without creating a Windows Apps & Features entry or an uninstaller shortcut. Add regression coverage for the installer and UI ownership, and align installer documentation with the actual flow.

**Tech Stack:** .NET 10 WPF, C#, custom console test harness, Inno Setup script, PowerShell verification.

## Global Constraints

- The uninstall action must be available only from `AdminWindow`.
- `ApplicationUninstaller` must launch `unins000.exe` with `Verb = "runas"`.
- `deployment/installer/TimeLock.iss` must set `Uninstallable=yes` and `CreateUninstallRegKey=no`.
- Do not add uninstall UI or logic to `MainWindow` or ordinary-user flows.
- Preserve unrelated existing worktree changes.

---

### Task 1: Add regression checks for uninstall ownership and installer exposure

**Files:**
- Modify: `TimeLockApp.Tests/ApplicationUninstallerTests.cs`
- Modify: `TimeLockApp.Tests/Program.cs` only if the new test collection is not already registered

**Interfaces:**
- Consumes: `ApplicationUninstaller.FindUninstaller` and repository source files.
- Produces: test cases that fail if the uninstaller becomes exposed outside Admin Panel or if Inno Setup enables an external uninstall entry.

- [ ] **Step 1: Write the failing tests first**

Add tests to read the repository files using a path derived from the test assembly's repository root. Assert that:

```csharp
AssertTrue(
    installerScript.Contains("Uninstallable=yes", StringComparison.Ordinal),
    "The installer must generate unins000.exe for the Admin Panel.");
AssertTrue(
    installerScript.Contains("CreateUninstallRegKey=no", StringComparison.Ordinal),
    "The installer must not expose an Apps & Features uninstall entry.");
AssertTrue(
    adminWindowMarkup.Contains("Click=\"UninstallButton_Click\"", StringComparison.Ordinal),
    "The Admin Panel must own the uninstall button.");
AssertTrue(
    !mainWindowMarkup.Contains("Uninstall", StringComparison.OrdinalIgnoreCase),
    "The ordinary-user window must not expose uninstall UI.");
```

Register the tests in `All()` with names describing each behavior. Run the focused test harness and confirm the tests fail for the intended missing assertion before making any implementation or documentation changes. If the current worktree already satisfies the assertions, record that the behavior is already implemented and use the tests as regression coverage; do not manufacture a production change solely to force a failure.

- [ ] **Step 2: Run the focused test harness**

Run:

```powershell
dotnet run --project TimeLockApp.Tests/TimeLockApp.Tests.csproj
```

Expected: the new checks execute and report their actual result; any failure must identify a missing Admin Panel-only or installer-only constraint rather than a test setup error.

- [ ] **Step 3: Make the minimal implementation adjustment only if a check fails**

If a check fails, change only the relevant source/configuration file:

- restore `Uninstallable=yes` and `CreateUninstallRegKey=no` in `deployment/installer/TimeLock.iss`;
- remove any external uninstaller shortcut or ordinary-user uninstall control;
- keep `AdminWindow.xaml` and `AdminWindow.xaml.cs` as the sole UI invocation path.

Do not refactor `ApplicationUninstaller` unless the failing check demonstrates that its current `runas` launch path is missing.

- [ ] **Step 4: Re-run the focused test harness**

Run the same command and expect all tests to pass, including the existing uninstaller path tests.

- [ ] **Step 5: Commit the focused change if repository write permissions allow it**

```powershell
git add TimeLockApp.Tests/ApplicationUninstallerTests.cs TimeLockApp.Tests/Program.cs deployment/installer/TimeLock.iss
git commit -m "test: guard admin-panel-only uninstall"
```

If `.git/index.lock` remains read-only, leave the working tree intact and report the permission blocker.

### Task 2: Align installer documentation with Admin Panel-only uninstall

**Files:**
- Modify: `deployment/installer/README.md`

**Interfaces:**
- Consumes: the installer behavior from `deployment/installer/TimeLock.iss` and the Admin Panel flow.
- Produces: documentation that tells operators to use Admin Panel → Uninstall Program and does not recommend manual folder deletion as the normal path.

- [ ] **Step 1: Update the installation instructions**

Replace the sentence claiming that the application should be removed manually with wording that states Windows does not expose an uninstall entry and that an authenticated administrator must use the Admin Panel's `Uninstall Program` button. Keep the existing installation, credential, and startup instructions unchanged.

- [ ] **Step 2: Verify the documentation and source diff**

Run:

```powershell
rg -n -i "uninstall|ถอนการติดตั้ง" deployment/installer/README.md deployment/installer/TimeLock.iss AdminWindow.xaml AdminWindow.xaml.cs MainWindow.xaml MainWindow.xaml.cs
git diff --check
```

Expected: README points to Admin Panel, installer has `Uninstallable=yes` and `CreateUninstallRegKey=no`, uninstall references remain absent from ordinary-user files, and `git diff --check` is clean.

- [ ] **Step 3: Commit the documentation if repository write permissions allow it**

```powershell
git add deployment/installer/README.md
git commit -m "docs: document admin-panel-only uninstall"
```

### Task 3: Run final verification

**Files:**
- Verify only: `TimeLockApp.csproj`, `TimeLockApp.Tests/TimeLockApp.Tests.csproj`, `AdminWindow.xaml`, `AdminWindow.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Services/ApplicationUninstaller.cs`, `deployment/installer/TimeLock.iss`, `deployment/installer/README.md`

- [ ] **Step 1: Run all automated tests**

```powershell
dotnet run --project TimeLockApp.Tests/TimeLockApp.Tests.csproj
```

Expected: exit code 0 and all test cases pass.

- [ ] **Step 2: Build the WPF application**

```powershell
dotnet build TimeLockApp.csproj --no-restore
```

Expected: build succeeds without compilation errors.

- [ ] **Step 3: Inspect final ownership and configuration**

```powershell
rg -n -i "Uninstall|unins000|Uninstallable|UninstallDelete" --glob '!bin/**' --glob '!obj/**' --glob '!publish/**' .
```

Confirm the only executable UI handler is in `AdminWindow`, the normal window has no uninstall control, and the installer does not publish an external uninstall entry.

- [ ] **Step 4: Report verification evidence**

Record test and build results, note any inability to commit caused by `.git` permissions, and do not claim completion until the commands have returned successfully.
