# Automatic Google Sheet Synchronization Design

## Objective

Keep TimeLockApp's local user database and an open Admin Panel aligned with Google Sheet edits without requiring the operator to restart the application or press `Sync Users`.

The application will normally observe a Sheet edit within 30 seconds. This is change-detection polling rather than a webhook, so it requires no public HTTPS service or additional cloud infrastructure.

## Approved Behavior

The application requests a full user synchronization:

1. when the application starts and internet access is already available;
2. immediately after Internet Authentication succeeds;
3. every 30 seconds while the application remains open;
4. immediately after a session expires; and
5. immediately after a user requests Logout.

The existing Admin Panel `Sync Users` button remains available.

If a Google Sheet user is changed or removed during an active session, the current session continues until expiry or Logout. The synchronized state applies to subsequent login attempts. A removed user disappears from an open Admin Panel after the successful synchronization while historical sessions remain intact under the existing nullable user-reference design.

## Architecture

### Synchronization execution

`UserSyncService` remains the single owner of the remote-to-local synchronization sequence:

1. send pending user deactivations to Google Sheet;
2. retrieve the current Sheet rows;
3. transactionally synchronize those rows into SQLite; and
4. return a result containing success, user count, and whether local user data actually changed.

Its existing semaphore serializes automatic and manual requests. The 30-second timer stops before awaiting a periodic request and restarts after completion, so a slow request cannot enqueue additional periodic requests. Startup, post-authentication, expiry, Logout, and manual requests wait for the same serialization gate rather than running concurrently.

### Change detection

`DatabaseService.SynchronizeUsers` will report whether its transaction inserted, materially updated, or deleted any Sheet-owned user. No-op upserts will not count as changes. Local-only users, including the local administrator, remain outside Sheet deletion logic.

The application still performs a full synchronization on every trigger. The change result controls only whether an already-open Admin Panel needs to reload its grid; it does not skip database reconciliation based on an in-memory hash.

### Application timer and lifecycle

`MainWindow` owns a dedicated 30-second `DispatcherTimer` separate from the one-second session countdown. It starts after application initialization and is stopped when the window closes. The timer continues to request synchronization whether the Login screen, Admin Panel, or a normal user session is active.

The Internet Authentication path will become asynchronous. A successful authentication awaits synchronization before reporting that the application is ready. The unused duplicate authentication method will be removed so there is one authoritative flow.

Session expiry and Logout use the same end-session path. After the local transaction closes and deactivates the user, that path requests a full synchronization. This guarantees that pending deactivation is sent before the latest Sheet rows are read back.

### Admin Panel refresh

`MainWindow` keeps the currently open `AdminWindow` reference. After a successful automatic synchronization whose result reports changes, it asks that window to reload users from SQLite. A no-change result updates status only and does not reset the grid.

When the grid reloads, an existing selected user is reselected when still present. If that user was removed, the selection and edit form are cleared. An empty add-user form is not cleared merely because the grid refreshes.

The manual `Sync Users` button keeps its current explicit success/failure feedback and uses the same change-aware synchronization result.

## Status and Error Handling

- Successful automatic synchronization updates the Login screen with the last successful synchronization time and user count.
- When the Admin Panel is open, it displays the automatic result there as well.
- A synchronization failure does not display a modal dialog and does not interrupt an active session.
- A failure leaves the last committed local data intact, displays a concise status message, and is retried by the next trigger.
- Connectivity or Google API errors returned by `UserSyncService` are no longer visible only through `Debug.WriteLine`.
- Timer restart occurs in `finally`, so an exception cannot permanently disable periodic synchronization.

## Testing

Automated tests will verify:

1. a Sheet insert is reported as a change;
2. a material Sheet update is reported as a change;
3. a missing Sheet user deletion is reported as a change while history remains;
4. synchronizing identical Sheet data is reported as no change;
5. local-only users do not make repeated synchronization appear changed;
6. periodic synchronization cannot overlap itself;
7. startup, post-authentication, periodic, expiry, and Logout requests use the same synchronization path;
8. failure preserves committed local data and produces a visible failure result; and
9. the complete existing C# test suite and WPF build remain successful.

Manual verification will edit and delete Sheet rows while Login, Admin Panel, and a normal session are open. The Admin grid must update within 30 seconds, new credentials must apply to the next login, and a currently active user must not be forced out by that edit.

## Acceptance Criteria

- Sheet changes become effective locally within approximately 30 seconds while TimeLockApp is running and online.
- A successful Internet Authentication causes an immediate synchronization without restarting the application.
- Expiry and Logout send pending deactivation and then retrieve current Sheet data.
- Only one synchronization operation accesses Google Sheet or mutates synchronized users at a time.
- An open Admin Panel refreshes only after a material data change.
- Synchronization errors are visible but never interrupt or shorten an active session.
- A user removed or disabled during an active session completes that session but cannot start another one.
