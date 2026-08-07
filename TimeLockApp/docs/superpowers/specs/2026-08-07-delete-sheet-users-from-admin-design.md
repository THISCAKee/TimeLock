# Delete Google Sheet Users from Admin Panel Design

## Objective

When a user row is removed from Google Sheets, remove the corresponding user from the local Admin Panel during the next successful synchronization. Preserve all existing session-history records for that user.

## Scope

The change will:

- delete sheet-managed users whose external user IDs are no longer present in Google Sheets;
- delete those users even when they have already been consumed;
- retain the local emergency administrator;
- retain all rows in the `sessions` table;
- apply the same behavior to automatic and manual synchronization because both use `UserSyncService.SynchronizeAsync`;
- keep synchronization transactional so a failure does not leave a partially updated user list.

The change will not:

- delete session history;
- delete the local-only emergency administrator;
- add an archive or soft-delete mechanism;
- change the Google Sheet schema or synchronization triggers;
- change how users are deactivated after completing a session.

## Architecture and Data Flow

`GoogleSheetsUserService` continues to read the current rows from Google Sheets. `UserSyncService` passes the parsed users to `DatabaseService.SynchronizeUsers` as it does today.

Within the existing database transaction, `DatabaseService` first upserts the users returned by Google Sheets and then removes every non-local user whose `external_user_id` is absent from that result. The deletion no longer excludes rows where `is_consumed = 1`.

Session history already stores the username and session details independently, so historical entries remain available after their corresponding `users` row is removed. The `sessions.user_id` column becomes nullable and its foreign key uses `ON DELETE SET NULL`. Existing databases are migrated transactionally by rebuilding the `sessions` table, copying every history row, and replacing the legacy table. New databases are created directly with the updated schema.

After a successful manual synchronization, `AdminWindow` reloads its user grid through the existing `LoadUsers` call. Automatic synchronization updates the same local database; reopening or refreshing the Admin Panel displays the synchronized list.

## Deletion Rules

- A row with `is_local_only = 0` and an external ID absent from the latest complete Sheet result is deleted.
- A row may be deleted whether `is_consumed` is `0` or `1`.
- A row with `is_local_only = 1` is never deleted by Google Sheet synchronization.
- Deleting a referenced user sets the historical session's `user_id` to null without changing its username, timestamps, duration, or status.
- An empty but successfully read Sheet result removes all sheet-managed users and preserves local-only users.
- Invalid or failed Sheet reads do not reach the database deletion step, so existing local users remain unchanged.

## Error Handling

All upserts and deletions remain inside the existing SQLite transaction. Any validation or database error rolls back the complete synchronization. `UserSyncService` returns the existing failure result, and the manual Admin Panel flow displays that failure without refreshing the grid as a successful sync.

## Testing

Automated database tests will verify that:

1. an unconsumed sheet-managed user missing from the latest Sheet result is deleted;
2. a consumed sheet-managed user missing from the latest Sheet result is deleted;
3. session-history rows remain after their user is deleted;
4. the local-only administrator remains when absent from the Sheet;
5. an empty Sheet result removes all sheet-managed users while preserving local-only users;
6. users still present in the Sheet continue to be upserted normally.
7. a legacy `sessions.user_id NOT NULL` schema migrates without losing history, and the migrated session remains readable after its user is deleted.

The implementation will follow test-driven development: add a regression test that demonstrates the current consumed-user behavior, confirm it fails for the intended reason, make the smallest database change, and run the focused and full test suites.

## Acceptance Criteria

- Removing a user row from Google Sheets and completing the next synchronization removes that user from the Admin Panel.
- The result is the same for previously used and unused users.
- Existing Session History entries for the removed user remain visible.
- The local emergency administrator remains available.
- A failed synchronization does not remove any users.
