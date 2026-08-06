# User Logout and Permanent Deactivation Design

## Objective

Add a small, low-emphasis logout control to the active usage timer. Confirm the action before ending the session, permanently prevent that user from logging in again, and update the Google Sheet `IsActive` value to `FALSE`. Apply the same permanent deactivation when allotted time expires.

## User Interface

`UsageWindow` gains a compact `ออก` button beside the existing minimize button. The button uses the same neutral gray palette as the timer controls, is visually smaller than the remaining-time display, and has no high-emphasis or destructive color in its resting state.

Pressing the button opens a Yes/No confirmation owned by `UsageWindow`. Choosing No leaves the session unchanged. Choosing Yes raises a logout-request event; the timer window does not access the database or Google API directly.

## Session Flow

`MainWindow` remains the owner of session lifecycle and handles the logout-request event. Early logout and natural expiration share one termination path with a reason parameter:

- early logout records session status `logged_out`;
- natural expiration records session status `completed`;
- both stop the timer, calculate and save used seconds, permanently deactivate the current user, hide the usage window, clear login fields, and return to the Login screen;
- only natural expiration displays the existing “time expired” alert.

The remaining allowance is forfeited on early logout. It is not paused or available for a later login.

## Durable Local Deactivation

The `users` table gains two migration-safe columns:

- `is_consumed INTEGER NOT NULL DEFAULT 0` marks an account whose one-time usage has ended;
- `deactivation_pending INTEGER NOT NULL DEFAULT 0` records that `FALSE` still needs to be written to Google Sheets.

Ending a session updates the user atomically to:

```text
is_active = 0
is_consumed = 1
deactivation_pending = 1
```

Authentication requires both `is_active = 1` and `is_consumed = 0`. Google Sheet synchronization must preserve `is_active = 0` for consumed users even if the remote row temporarily remains `TRUE`.

Emergency/local-only administrator behavior is unchanged. The logout timer is shown only for ordinary timed users.

## Google Sheets Writeback

The Google service credential scope changes from `SpreadsheetsReadonly` to `Spreadsheets`. The service account must have Editor access to the configured spreadsheet.

`GoogleSheetsUserService` gains an operation that locates the row whose column A `UserId` matches the local `external_user_id`, then updates column F (`IsActive`) to `FALSE` using the Sheets Values API.

After local deactivation, `UserSyncService` attempts this write immediately. A network or API failure never restores local access and never traps the user on the timer screen. The pending marker remains set.

Before a later pull-based synchronization, `UserSyncService` processes pending external user IDs first. Each successful Sheet update clears that user’s `deactivation_pending` marker. Failed updates remain pending for the next synchronization.

Users without an `external_user_id` can still be deactivated locally but cannot be matched to a Google Sheet row; their pending marker is not repeatedly sent to the API.

## Error Handling

- Selecting No in the confirmation performs no action.
- Repeated logout requests are ignored after session termination begins.
- Local deactivation and session completion occur even when internet access is unavailable.
- A missing Google Sheet row leaves the update pending and writes diagnostic output.
- Google API failures are recorded diagnostically and retried on a later synchronization.
- A consumed user cannot be reactivated by the normal Sheet pull/upsert path.

## Data and Security Constraints

- Password storage and validation remain unchanged.
- The Google Sheet format remains `Users!A2:F`.
- Column F continues to represent `IsActive`.
- No user or session-history row is deleted.

## Verification Contract

Codex will not build, run, or test the application. The user will verify:

1. The timer shows a small, unobtrusive `ออก` button.
2. Choosing No keeps the timer and session running.
3. Choosing Yes returns to Login and records status `logged_out`.
4. The logged-out user cannot log in again.
5. Natural expiration records status `completed` and also prevents another login.
6. Column F in the matching Google Sheet row becomes `FALSE` when online.
7. Offline logout still blocks local login and writes `FALSE` after a later successful Sync.
8. The service account has Editor permission and write failures do not close the application.
