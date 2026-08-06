# Automatic User Sync Design

## Objective

Keep local users synchronized with Google Sheets whenever internet connectivity becomes available and after every successful login, while retaining the Admin Panel button for manual synchronization.

Password storage and validation remain unchanged. The implementation will not introduce password hashing.

## Scope

The change will:

- synchronize once when the application starts with working internet access;
- synchronize when Windows reports that network connectivity has returned, after verifying real internet access;
- synchronize after every successful normal-user or administrator login when internet access is available;
- synchronize after successful internet authentication;
- retain manual synchronization in the Admin Panel;
- prevent overlapping synchronization operations;
- restore initialization behavior removed by the current uncommitted changes;
- move the sync service source out of the ignored `Secrets` directory.

The change will not:

- hash, encrypt, or otherwise change password storage;
- periodically synchronize while connectivity remains continuously online;
- block a successful login solely because synchronization failed;
- change the Google Sheet column format.

## Architecture

`MainWindow` owns application-level connectivity monitoring. It subscribes to `NetworkChange.NetworkAvailabilityChanged` after initialization and unsubscribes when the window closes. Network callbacks are marshalled onto the WPF dispatcher before touching UI or application state.

Every automatic trigger delegates to one synchronization coordinator method. The coordinator verifies real internet access through `InternetConnectivityService`, tracks the last verified connectivity state, and calls `UserSyncService`. Connectivity events synchronize only on a verified offline-to-online transition, while login and successful-authentication triggers synchronize whenever internet access is verified.

`UserSyncService` remains responsible for reading Google Sheets and applying the returned users transactionally through `DatabaseService`. It owns an asynchronous synchronization gate so two callers cannot modify users concurrently. `MainWindow` passes the same `UserSyncService` instance to `AdminWindow`, making the guard shared by automatic and manual synchronization. Its source file moves from `Secrets/UserSyncService.cs` to `Services/UserSyncService.cs`; only credentials remain under `Secrets`.

## Trigger Behavior

### Application startup

The application initializes the SQLite schema and keyboard hook before checking connectivity. If internet access is working, it performs one automatic synchronization.

### Connectivity restored

When Windows reports an available network, the application verifies external internet access. A successful transition triggers one synchronization. Repeated availability events while synchronization is active are ignored, and remaining continuously online does not cause periodic synchronization.

### Successful login

After local credentials have been accepted for either a normal user or an administrator, the application checks connectivity and attempts synchronization before continuing to the appropriate window or session flow. A sync failure is recorded for diagnostics but does not reject the already-valid login.

### Internet authentication

After the network authentication window reports success, the application checks internet access and attempts synchronization.

### Manual Admin synchronization

The Admin Panel retains the `Sync Users` button. The button uses the same synchronization guard, disables itself while work is active, refreshes the user grid on success, and presents success or failure text to the administrator.

## Error Handling

- Connectivity-check exceptions are treated as unavailable internet and do not close the application.
- Automatic synchronization failures are written to diagnostic output and do not interrupt login or navigation.
- Manual synchronization failures are shown in `MessageTextBlock`.
- Cancellation during application shutdown is allowed to propagate through the sync layer and is not presented as a data error.
- Database synchronization remains transactional so a failed import does not partially replace users.

## Related Regression Repairs

The implementation restores the behaviors accidentally removed in the current working changes:

- call `DatabaseService.InitializeDatabase()` during `MainWindow` initialization;
- initialize and install the low-level keyboard hook;
- wire the Admin window loaded handler and use its injected `DatabaseService`;
- give the Admin sync button the XAML name referenced by code-behind;
- remove the unnecessary obsolete marker that currently creates a compiler warning;
- remove or consolidate the unused alternative network-authentication method so there is one active flow.

## Repository Hygiene

This feature moves only the misplaced sync source file out of the ignored directory. Broader removal of already tracked `bin`, `obj`, database, and WebView profile files is outside this feature's implementation scope and can be handled separately to avoid mixing repository cleanup with behavior changes.

## Verification Contract

Codex will not build, run, or test the application. The user will verify manually:

1. The project builds without the current missing-button error.
2. Starting online synchronizes users once.
3. Starting offline and reconnecting synchronizes users once.
4. Every successful normal-user and administrator login attempts synchronization when online.
5. Successful internet authentication triggers synchronization.
6. The Admin Panel button still synchronizes and refreshes the grid.
7. Failed synchronization does not prevent login.
8. Database initialization and shortcut blocking still work.
