# Hide Admin User ID Design

## Objective

Remove the internal user ID from the Admin Panel user table without changing user-selection, update, or deletion behavior.

## Design

Delete the `DataGridTextColumn` whose header is `ID` and whose binding is `Id` from `AdminWindow.xaml`. Keep `UserRecord.Id`, the DataGrid item objects, and all code-behind logic unchanged. The remaining columns continue using their existing widths and naturally occupy the released space.

## Scope

- Change only the visible column definition in `AdminWindow.xaml`.
- Do not modify the database schema, models, queries, selection handling, update logic, or deletion logic.
- Do not run the application, build, or tests; the user will verify the UI.

## Verification

The user will open the Admin Panel and confirm that the ID column is absent, other columns remain readable, and selecting, updating, and deleting a user still targets the correct record.
