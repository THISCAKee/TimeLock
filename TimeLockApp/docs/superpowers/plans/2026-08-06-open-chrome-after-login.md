# Open Chrome After User Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Open `https://libmsu-ai.vercel.app/` in Google Chrome once after each successful ordinary-user login without disrupting the timed session when launch fails.

**Architecture:** A dedicated `ChromeLauncherService` discovers Chrome through Windows App Paths and standard install directories, then starts it with the approved URL. `MainWindow` starts the timer first, calls the launcher once, and shows a user-safe warning owned by `UsageWindow` on failure.

**Tech Stack:** .NET 10, C#, WPF, `System.Diagnostics.Process`, `Microsoft.Win32.Registry`

## Global Constraints

- Launch only for ordinary timed users, never administrator login.
- Launch exactly `https://libmsu-ai.vercel.app/` once per successful session.
- Do not fall back to another browser.
- Browser failure must not stop, roll back, or delay the timer/session.
- Codex must not launch Chrome, run the application, build, or test; the user verifies runtime behavior.
- Preserve unrelated working-tree changes and do not commit implementation files unless requested.

---

### Task 1: Implement Chrome discovery and launch

**Files:**
- Create: `Services/ChromeLauncherService.cs`

**Interfaces:**
- Produces: `ChromeLauncherService.TryOpen(string url) -> ChromeLaunchResult`.
- Produces: `ChromeLaunchResult.IsSuccessful` and `.ErrorMessage`.

- [ ] **Step 1: Define the result type**

Create an immutable result with factories:

```csharp
public sealed class ChromeLaunchResult
{
    public bool IsSuccessful { get; private init; }
    public string ErrorMessage { get; private init; } = "";

    public static ChromeLaunchResult Success() =>
        new() { IsSuccessful = true };

    public static ChromeLaunchResult Failure(string message) =>
        new() { ErrorMessage = message };
}
```

- [ ] **Step 2: Read Windows App Paths safely**

Implement a private iterator over `RegistryHive.CurrentUser` and `RegistryHive.LocalMachine`, and `RegistryView.Registry64` and `RegistryView.Registry32`. Open:

```text
SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe
```

Return the unnamed default value only when it is a non-empty path and `File.Exists` is true. Catch access/platform exceptions per candidate and continue.

- [ ] **Step 3: Add standard install candidates**

Append these candidates when their base folder is non-empty:

```text
%ProgramFiles%\Google\Chrome\Application\chrome.exe
%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe
%LocalAppData%\Google\Chrome\Application\chrome.exe
```

Deduplicate candidates with `StringComparer.OrdinalIgnoreCase` and select the first existing file.

- [ ] **Step 4: Start Chrome with a safe argument list**

Validate that `url` is an absolute HTTPS URI. Start the absolute executable directly:

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = chromePath,
    UseShellExecute = false,
    CreateNoWindow = true
};

startInfo.ArgumentList.Add(url);
Process? process = Process.Start(startInfo);
```

Return “ไม่พบ Google Chrome ในเครื่องนี้” when no executable is found. Catch process-start exceptions and return “ไม่สามารถเปิด Google Chrome ได้”. Treat a null process as failure.

- [ ] **Step 5: Static verification only**

Confirm no default-browser shell execution exists, registry keys are disposed, the URL is passed as one argument, and internal paths/exceptions are not returned to the user. Do not start Chrome or run tests.

### Task 2: Launch after the timed session starts

**Files:**
- Modify: `MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `ChromeLauncherService.TryOpen(string)` from Task 1.
- Consumes: the existing `_usageWindow` created by `StartSession`.

- [ ] **Step 1: Add the service and URL**

Add:

```csharp
private const string UserWebsiteUrl =
    "https://libmsu-ai.vercel.app/";

private readonly ChromeLauncherService _chromeLauncherService = new();
```

- [ ] **Step 2: Launch after starting the timer**

At the end of `StartSession`, after `_timer.Start()`, call `TryOpen(UserWebsiteUrl)`. Because only ordinary users call `StartSession`, Admin login remains unchanged.

- [ ] **Step 3: Warn without ending the session**

When the result fails and `_usageWindow` is non-null, show:

```csharp
MessageBox.Show(
    _usageWindow,
    result.ErrorMessage,
    "ไม่สามารถเปิดเว็บไซต์ได้",
    MessageBoxButton.OK,
    MessageBoxImage.Warning);
```

Do not call `EndSessionAsync`, stop the timer, or restore the login window.

- [ ] **Step 4: Static verification only**

Trace Admin login to `OpenAdminPanel` and user login to `StartSession`. Confirm exactly one Chrome launch call occurs after timer startup and that the failure branch only displays a message. Do not run the application.

### Task 3: User-operated verification handoff

**Files:**
- Review only: `Services/ChromeLauncherService.cs`, `MainWindow.xaml.cs`

**Interfaces:**
- Produces: build and runtime checklist for the user.

- [ ] **Step 1: Review scoped diffs**

Run static text checks and scoped `git diff --check`. Confirm the URL is exact, no password/session deactivation behavior changed, and no generated files are intentionally edited.

- [ ] **Step 2: Ask the user to build**

Provide:

```powershell
dotnet build TimeLockApp.csproj --no-restore
```

- [ ] **Step 3: Ask the user to verify runtime behavior**

Verify ordinary user login with Chrome closed and already running, Admin login, and a missing/broken Chrome installation. Confirm the timer remains active after a launch warning.
