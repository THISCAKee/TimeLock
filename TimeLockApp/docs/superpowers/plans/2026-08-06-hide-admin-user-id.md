# Hide Admin User ID Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the internal ID column from the Admin Panel user table while preserving all record operations.

**Architecture:** This is a presentation-only XAML change. The DataGrid continues receiving complete `UserRecord` objects, including `Id`, so code-behind can select, update, and delete records without displaying the identifier.

**Tech Stack:** .NET 10, C#, WPF XAML

## Global Constraints

- Modify only the visible column definition in `AdminWindow.xaml`.
- Keep `UserRecord.Id` and all database/code-behind behavior unchanged.
- Codex must not build, run, or test the application; the user verifies the UI.
- Preserve unrelated working-tree changes and do not commit the XAML change unless requested.

---

### Task 1: Remove the visible Admin ID column

**Files:**
- Modify: `AdminWindow.xaml:119-122`
- Test: User-operated Admin Panel UI verification

**Interfaces:**
- Consumes: `UsersDataGrid.ItemsSource` containing complete `UserRecord` objects.
- Produces: a DataGrid whose first visible column is `Username`, while selected items retain `UserRecord.Id` internally.

- [ ] **Step 1: Record the current UI definition statically**

Confirm `AdminWindow.xaml` currently contains exactly one column with both `Header="ID"` and `Binding="{Binding Id}"`. Do not run the application.

- [ ] **Step 2: Remove only the ID column**

Delete this XAML block:

```xml
<DataGridTextColumn Header="ID"
                    Binding="{Binding Id}"
                    Width="56"/>
```

Do not modify the `Username`, `Password`, `Minutes`, or `Role` columns.

- [ ] **Step 3: Verify the source diff statically**

Inspect the scoped diff and confirm the only behavioral XAML change in this task is removal of the three-line ID column. Search `AdminWindow.xaml.cs` and `data/DatabaseService.cs` to confirm ID-based selection/update/delete logic remains unchanged. Do not run build or tests.

- [ ] **Step 4: Hand off UI verification**

Ask the user to open the Admin Panel and confirm:

1. The ID column is absent.
2. Username is the first visible column.
3. Selecting a user still populates the edit form.
4. Updating and deleting a selected user still affect the intended record.
