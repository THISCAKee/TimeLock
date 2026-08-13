# Task 2 Report

## Status

DONE_WITH_CONCERNS

## Change

Modified only `deployment/installer/README.md` in the requested product documentation scope. Replaced the manual application-folder removal instruction with wording stating that Windows does not expose an uninstall entry and that an authenticated administrator must use `Admin Panel → Uninstall Program`. Installation, credential, and startup instructions were left unchanged.

## Verification

Command:

```powershell
rg -n -i "uninstall|uninstaller|remove|admin panel|folder manually|apps & features" -- deployment/installer/README.md deployment/installer/TimeLock.iss AdminWindow.xaml AdminWindow.xaml.cs MainWindow.xaml MainWindow.xaml.cs
git diff --check
git diff --check -- deployment/installer/README.md
```

Results:

- `rg`: passed (`RG_EXIT=0`); the README contains the Admin Panel uninstall instruction, and the installer/UI references were found.
- Full `git diff --check`: failed (`DIFF_CHECK_EXIT=2`) on pre-existing unrelated generated/cache/source worktree changes with trailing whitespace.
- README-only `git diff --check`: passed.

## Commit

No commit was created. Both `git add`/`git commit` were blocked because Git could not create `D:/TimeOut/.git/index.lock` (`Permission denied`).

## Concerns

- The full-worktree `git diff --check` failure is unrelated to this README change and was preserved as requested.
- The worktree contained many unrelated modifications before this task; they were not altered or staged.
