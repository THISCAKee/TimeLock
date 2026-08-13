# Hide TimeLockApp from the Taskbar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hide TimeLockApp's normal WPF windows from the Windows taskbar.

**Architecture:** Configure the two existing user-facing WPF windows declaratively with `ShowInTaskbar="False"`. Verify the setting with a focused source-level regression test and a normal project build.

**Tech Stack:** WPF, XAML, PowerShell, .NET 10 Windows.

## Global Constraints

- Preserve existing window behavior and controls.
- Do not add a notification-area icon or change application lifecycle.
- Do not modify generated `bin` or `obj` output files.

### Task 1: Add taskbar visibility regression test

**Files:**
- Create: `TimeLockApp.Tests/TaskbarVisibility.Tests.ps1`

- [ ] **Step 1: Write the failing test**

  Read `MainWindow.xaml` and `UsageWindow.xaml`, then assert each root `Window` contains `ShowInTaskbar="False"`.

- [ ] **Step 2: Run the test to verify it fails**

  Run `pwsh -NoProfile -File .\TimeLockApp.Tests\TaskbarVisibility.Tests.ps1`.
  Expected: FAIL because both XAML files currently omit the setting.

### Task 2: Configure both windows

**Files:**
- Modify: `MainWindow.xaml` root `Window` declaration.
- Modify: `UsageWindow.xaml` root `Window` declaration.

- [ ] **Step 1: Add `ShowInTaskbar="False"`**

  Add the property to each root `Window` without changing any other window property.

- [ ] **Step 2: Run the regression test**

  Run `pwsh -NoProfile -File .\TimeLockApp.Tests\TaskbarVisibility.Tests.ps1`.
  Expected: PASS for both windows.

### Task 3: Build and review the change

**Files:**
- Review only: `MainWindow.xaml`, `UsageWindow.xaml`, `TimeLockApp.Tests/TaskbarVisibility.Tests.ps1`.

- [ ] **Step 1: Build the project**

  Run `dotnet build .\TimeLockApp.csproj --no-restore`.
  Expected: exit code 0 with no compilation errors.

- [ ] **Step 2: Check the diff**

  Run `git diff --check -- MainWindow.xaml UsageWindow.xaml TimeLockApp.Tests/TaskbarVisibility.Tests.ps1` and confirm only the intended files contain source changes.
