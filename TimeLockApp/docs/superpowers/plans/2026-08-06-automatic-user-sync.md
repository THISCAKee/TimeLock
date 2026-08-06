# Automatic User Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize Google Sheet users whenever verified internet connectivity returns and after every successful normal or administrator login, while retaining manual Admin synchronization.

**Architecture:** `MainWindow` owns Windows network monitoring and verified connectivity state. A single `UserSyncService` instance, shared with `AdminWindow`, serializes all database synchronization through an asynchronous gate so automatic and manual operations never overlap.

**Tech Stack:** .NET 10, C#, WPF, `System.Net.NetworkInformation`, Google Sheets API, Microsoft.Data.Sqlite

## Global Constraints

- Password storage and validation remain unchanged; do not add hashing or encryption.
- Do not periodically synchronize while connectivity remains continuously online.
- A synchronization failure must not reject credentials that were valid before synchronization.
- Keep the existing Google Sheet `Users!A2:F` format.
- Codex must not run `dotnet build`, `dotnet test`, the application, or any test command. The user performs all runtime verification.
- Preserve unrelated working-tree changes and do not commit implementation files unless the user explicitly requests a commit.

---

## File Map

- Create `Services/UserSyncService.cs`: tracked sync orchestration source and shared asynchronous gate.
- Delete `Secrets/UserSyncService.cs`: remove misplaced ignored source after copying its implementation.
- Modify `Services/GoogleSheetsUserService.cs`: remove the accidental obsolete annotation and warning.
- Modify `MainWindow.xaml.cs`: restore initialization, monitor network transitions, sync after login/authentication, share sync service with Admin, and remove the unused authentication path.
- Modify `AdminWindow.xaml.cs`: use injected database and sync services, restore initial grid loading, and keep manual synchronization.
- Modify `AdminWindow.xaml`: name the manual sync button referenced by code-behind.

### Task 1: Make synchronization source tracked and concurrency-safe

**Files:**
- Create: `Services/UserSyncService.cs`
- Delete: `Secrets/UserSyncService.cs`
- Modify: `Services/GoogleSheetsUserService.cs:50-55`

**Interfaces:**
- Consumes: `GoogleSheetsUserService.GetUsersAsync(CancellationToken)` and `DatabaseService.SynchronizeUsers(IReadOnlyList<GoogleSheetUser>)`.
- Produces: `UserSyncService.SynchronizeAsync(CancellationToken cancellationToken = default) -> Task<UserSyncResult>`.
- Produces: `UserSyncResult.Success(int)` and `UserSyncResult.Failure(string)`.

- [ ] **Step 1: Move the source into the tracked Services directory**

Create `Services/UserSyncService.cs` from the ignored source and add `System.Threading.SemaphoreSlim`:

```csharp
private readonly SemaphoreSlim _syncGate = new(1, 1);
```

Delete `Secrets/UserSyncService.cs` only after the tracked replacement contains the complete `UserSyncService` and `UserSyncResult` types.

- [ ] **Step 2: Serialize synchronization operations**

Wrap the existing fetch-and-transaction flow with the shared gate:

```csharp
await _syncGate.WaitAsync(cancellationToken);

try
{
    IReadOnlyList<GoogleSheetUser> users =
        await _googleSheetsUserService.GetUsersAsync(cancellationToken);

    _databaseService.SynchronizeUsers(users);
    return UserSyncResult.Success(users.Count);
}
catch (OperationCanceledException)
{
    throw;
}
catch (Exception ex)
{
    return UserSyncResult.Failure(ex.Message);
}
finally
{
    _syncGate.Release();
}
```

- [ ] **Step 3: Remove the self-generated obsolete warning**

Remove `[Obsolete]` from `GoogleSheetsUserService.GetSheetsServiceAsync`; do not change its authentication behavior or credential path.

- [ ] **Step 4: Perform static verification only**

Confirm by inspection that only one `UserSyncService` declaration exists outside ignored/generated folders, every successful gate acquisition reaches `Release()` in `finally`, and cancellation still propagates.

Do not run a compiler or tests.

### Task 2: Restore application initialization and add connectivity-triggered sync

**Files:**
- Modify: `MainWindow.xaml.cs:1-190`

**Interfaces:**
- Consumes: `InternetConnectivityService.HasInternetAccessAsync(CancellationToken) -> Task<bool>`.
- Consumes: `UserSyncService.SynchronizeAsync(CancellationToken) -> Task<UserSyncResult>` from Task 1.
- Produces: `SynchronizeWhenInternetAvailableAsync(string trigger) -> Task<bool>`.
- Produces: `NetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) -> void`.

- [ ] **Step 1: Restore constructor invariants**

Add `using System.Net.NetworkInformation;`, remove `_isSynchronizing` and the nullable-warning pragmas, and initialize in this order after `InitializeComponent()`:

```csharp
_databaseService.InitializeDatabase();

_keyboardProc = KeyboardHookCallback;
_keyboardHook = InstallKeyboardHook(_keyboardProc);

var googleSheetsUserService = new GoogleSheetsUserService();
_userSyncService = new UserSyncService(
    googleSheetsUserService,
    _databaseService);

NetworkChange.NetworkAvailabilityChanged +=
    NetworkAvailabilityChanged;

Loaded += MainWindow_Loaded;
```

Add state for the last verified result:

```csharp
private bool _hasVerifiedInternet;
```

- [ ] **Step 2: Add verified offline-to-online handling**

The Windows callback must not access WPF controls directly. Dispatch to the UI thread:

```csharp
private void NetworkAvailabilityChanged(
    object? sender,
    NetworkAvailabilityEventArgs e)
{
    _ = Dispatcher.InvokeAsync(
        () => HandleNetworkAvailabilityChangedAsync(
            e.IsAvailable));
}
```

Implement the dispatched handler so all connectivity state is owned by the UI thread, startup remains the only trigger before the loaded check completes, and a verified false-to-true transition synchronizes once:

```csharp
private async Task HandleNetworkAvailabilityChangedAsync(
    bool isNetworkAvailable)
{
    if (!isNetworkAvailable)
    {
        _hasVerifiedInternet = false;
        return;
    }

    if (!_startupConnectivityChecked)
    {
        return;
    }

    bool wasOnline = _hasVerifiedInternet;
    bool hasInternet =
        await _connectivityService.HasInternetAccessAsync();

    _hasVerifiedInternet = hasInternet;

    if (hasInternet && !wasOnline)
    {
        await SynchronizeUsersSilentlyAsync(
            "Network connectivity restored");
    }
}
```

- [ ] **Step 3: Centralize verified automatic synchronization**

Replace the current `_isSynchronizing` method with two focused methods:

```csharp
private async Task<bool> SynchronizeWhenInternetAvailableAsync(
    string trigger)
{
    bool hasInternet =
        await _connectivityService.HasInternetAccessAsync();

    _hasVerifiedInternet = hasInternet;

    if (!hasInternet)
    {
        return false;
    }

    await SynchronizeUsersSilentlyAsync(trigger);
    return true;
}

private async Task SynchronizeUsersSilentlyAsync(string trigger)
{
    UserSyncResult result =
        await _userSyncService.SynchronizeAsync();

    string message = result.IsSuccessful
        ? $"{trigger}: synchronized {result.UserCount} users"
        : $"{trigger}: synchronization failed: {result.ErrorMessage}";

    Debug.WriteLine(message);
}
```

The service gate from Task 1, rather than per-window Boolean flags, prevents overlap.

- [ ] **Step 4: Update startup behavior**

Keep `_startupConnectivityChecked`. In `MainWindow_Loaded`, verify connectivity, assign `_hasVerifiedInternet`, synchronize once when online, and otherwise await the unified authentication method. Startup failures must leave the login UI usable.

- [ ] **Step 5: Unsubscribe during shutdown cleanup**

At the start of `OnClosed`, add:

```csharp
NetworkChange.NetworkAvailabilityChanged -=
    NetworkAvailabilityChanged;
```

Keep the existing keyboard unhook logic.

- [ ] **Step 6: Perform static verification only**

Inspect constructor assignments to confirm `_keyboardProc`, `_keyboardHook`, `_userSyncService`, and the database are initialized exactly once. Confirm the Windows event is subscribed once and unsubscribed once.

Do not run a compiler or tests.

### Task 3: Synchronize on authentication and every successful login

**Files:**
- Modify: `MainWindow.xaml.cs:61-260`

**Interfaces:**
- Consumes: `SynchronizeWhenInternetAvailableAsync(string) -> Task<bool>` from Task 2.
- Produces: `OpenNetworkAuthenticationAsync() -> Task`.
- Produces: WPF `LoginButton_Click(object, RoutedEventArgs) -> async void`.

- [ ] **Step 1: Consolidate network authentication into one method**

Change `NetworkAuthButton_Click` to `async void` and await `OpenNetworkAuthenticationAsync()`. Convert the currently active `OpenNetworkAuthentication` flow to `Task`, preserve its `_isNetworkAuthOpen`, visibility, and `finally` restoration behavior, and after `AuthenticationCompleted` call:

```csharp
await SynchronizeWhenInternetAvailableAsync(
    "Internet authentication completed");
```

Remove the later unused `OpenNetworkAuthWindowAsync` method so there is only one authentication-window flow.

- [ ] **Step 2: Synchronize every accepted login**

Change `LoginButton_Click` to `async void`. Guard the handler with `_isLoginInProgress` and reset the guard in `finally` so repeated clicks cannot create multiple sessions while the connectivity check or sync is awaiting. Preserve the initial local credential check. After it succeeds, call:

```csharp
await SynchronizeWhenInternetAvailableAsync(
    $"Login: {user.Username}");
```

Then query `GetUserByUsernameAndPassword` again. If a successful sync removed, disabled, or changed that account, show the existing invalid-credentials message and stop. If sync failed, the transaction leaves the prior local record available, so a previously valid login can continue.

- [ ] **Step 3: Use refreshed user data for navigation**

Use the second query result when checking `Role`, opening Admin, or calling `StartSession`, so synchronized role and allowed-minute changes apply to the current login.

- [ ] **Step 4: Share the sync instance with Admin**

Update Admin construction to:

```csharp
AdminWindow adminWindow = new(
    _databaseService,
    _userSyncService);
```

- [ ] **Step 5: Perform static verification only**

Trace both normal and administrator login branches to confirm each passes through synchronization once after the first credential acceptance. Trace successful internet authentication to confirm it also reaches the same coordinator.

Do not run a compiler or tests.

### Task 4: Preserve and repair manual Admin synchronization

**Files:**
- Modify: `AdminWindow.xaml.cs:1-285`
- Modify: `AdminWindow.xaml:368-377`

**Interfaces:**
- Consumes: shared `DatabaseService` and `UserSyncService` supplied by `MainWindow`.
- Produces: `AdminWindow(DatabaseService databaseService, UserSyncService userSyncService)`.

- [ ] **Step 1: Use injected shared services**

Replace the constructor with:

```csharp
public AdminWindow(
    DatabaseService databaseService,
    UserSyncService userSyncService)
{
    InitializeComponent();

    _databaseService = databaseService;
    _userSyncService = userSyncService;

    Loaded += AdminWindow_Loaded;
}
```

Remove `_googleSheetsUserService`, its local construction, and the unused `TestGoogleSheetButton_Click` handler.

- [ ] **Step 2: Name the manual sync button**

Add the code-behind name without changing its content or click handler:

```xml
<Button x:Name="SyncUsersButton"
        Content="Sync Users"
        ...
        Click="SyncUsersButton_Click"/>
```

- [ ] **Step 3: Keep manual feedback and refresh behavior**

Retain disabling the button in `try/finally`, call the shared `_userSyncService`, display failure text on an unsuccessful result, and call `LoadUsers()` before displaying the success count.

- [ ] **Step 4: Perform static verification only**

Confirm `AdminWindow_Loaded` is wired once, the injected database is not replaced, the button has the name used by code-behind, and the manual path uses the same service instance created in `MainWindow`.

Do not run a compiler or tests.

### Task 5: User-operated verification handoff

**Files:**
- Review only: all files changed in Tasks 1-4

**Interfaces:**
- Consumes: completed source changes.
- Produces: an exact manual verification checklist for the user.

- [ ] **Step 1: Review the source diff without executing code**

Use `git diff` and text searches only. Confirm no password hashing/encryption was added, `Users!A2:F` remains unchanged, and no unrelated generated files are intentionally edited.

- [ ] **Step 2: Ask the user to build**

Provide:

```powershell
dotnet build TimeLockApp.csproj --no-restore
```

Expected result: zero compiler errors. Do not claim the build passes until the user supplies its output.

- [ ] **Step 3: Ask the user to perform runtime scenarios**

Ask the user to verify:

1. Start online: one synchronization occurs and login remains usable.
2. Start offline, then connect: one synchronization occurs on reconnection.
3. Login as a normal user while online: synchronization occurs and the session uses current allowed minutes.
4. Login as Admin while online: synchronization occurs before Admin opens.
5. Complete captive-portal authentication: synchronization occurs afterward.
6. Press `Sync Users`: the button disables temporarily, users refresh, and status text is shown.
7. Force a Sheets failure: a valid local login remains usable and manual sync shows an error.
8. Verify Windows/Alt+Tab blocking and first-run database creation still work.

- [ ] **Step 4: Record user-reported results**

If the user reports a compiler error or runtime failure, capture the full message and reproduce only through static tracing unless the user later authorizes Codex to run a specific command.
