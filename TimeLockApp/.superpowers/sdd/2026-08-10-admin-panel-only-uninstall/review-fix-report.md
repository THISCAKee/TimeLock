# Review Fix Report: Admin Panel Uninstall

## Finding verification

- `deployment/installer/TimeLock.iss` used `Uninstallable=no`, which prevents Inno Setup from generating `unins000.exe` even though `ApplicationUninstaller` locates and launches that executable from the installation directory.
- The existing `[Icons]` section has no uninstaller shortcut, so the approved correction can preserve that restriction.
- The prior installer regression check required `Uninstallable=no`; after changing the check to require the approved settings, the custom harness failed specifically because `Uninstallable=yes` was absent.

## Resolution

- Set `Uninstallable=yes` and `CreateUninstallRegKey=no` in `deployment/installer/TimeLock.iss`. This generates `unins000.exe` for the authenticated Admin Panel while suppressing the Windows Apps & Features uninstall registration.
- Updated `TimeLockApp.Tests/ApplicationUninstallerTests.cs` to require both settings, while retaining checks that `AdminWindow` owns the uninstall action and `MainWindow` has no uninstall text.
- Updated the approved design and implementation plan to document the corrected settings and their verification expectations.
- Left `deployment/installer/README.md` unchanged because its statement that Windows exposes no uninstall entry and that administrators use the Admin Panel remains accurate with `CreateUninstallRegKey=no`.

## Verification

- Red test: `dotnet run --project TimeLockApp.Tests/TimeLockApp.Tests.csproj` exited 1 after the regression test was changed and before the installer script was corrected. The expected failure was: `The installer must generate unins000.exe for the Admin Panel.`
- Final test harness: `dotnet run --project TimeLockApp.Tests/TimeLockApp.Tests.csproj` exited 0; all reported checks passed, including `installer generates a hidden uninstaller`.
- WPF build: `dotnet build TimeLockApp.csproj --no-restore` exited 0 with 0 warnings and 0 errors.
- Scope check confirms `Uninstallable=yes` and `CreateUninstallRegKey=no` in the installer, tests, design, and plan; no `Uninstallable=no` remains in those approved files.

## Scope and concerns

- Unrelated worktree changes were not modified.
- A repository-wide `git diff --check` reports pre-existing trailing whitespace in generated `bin/` and `obj/` artifacts. The staged review-fix paths passed `git diff --cached --check` before commit.
