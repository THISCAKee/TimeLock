# Windows Shell Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ให้ TimeLock เปิดแทน Windows Explorer สำหรับบัญชีผู้ใช้ปัจจุบันหลัง sign in พร้อมติดตั้ง/ถอนและกู้คืนค่าเดิมได้

**Architecture:** เพิ่ม PowerShell installer/remover แยกจาก home-lockdown เดิม โดยใช้ helper สำหรับ Registry snapshot และ JSON state ร่วมกัน. Shell mode เขียนค่า `HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell` ของผู้ใช้ปัจจุบันเท่านั้น และเก็บ backup ไว้ใน `%LOCALAPPDATA%\TimeLockApp\Shell`.

**Tech Stack:** PowerShell 5.1+, Windows Registry, existing `TimeLockHomeLockdown.psm1`, PowerShell regression runner.

## Global Constraints

- ห้ามเปลี่ยน Shell ของผู้ใช้รายอื่นหรือทั้งเครื่อง
- `-ValidateOnly` ต้องไม่เปลี่ยน Registry, ไฟล์ หรือ process
- ห้ามเขียนทับ backup ที่ยังไม่ได้ถอน
- ต้องตรวจ SID ก่อนถอนการตั้งค่า
- ต้องคืนค่า Explorer เดิมก่อนลบ backup
- ห้ามแก้ไข unrelated generated files ใน `obj/` หรือ `bin/`

---

### Task 1: Add shell install/remove scripts

**Files:**
- Create: `deployment/windows-shell/Install-TimeLockShell.ps1`
- Create: `deployment/windows-shell/Remove-TimeLockShell.ps1`
- Create: `deployment/windows-shell/README.md`
- Reuse: `deployment/windows-home-lockdown/TimeLockHomeLockdown.psm1`

**Interfaces:**
- `Install-TimeLockShell.ps1 -AppPath <exe> [-StateDirectory <path>] [-ValidateOnly] [-RegistryRoot <HKCU path>]`
- `Remove-TimeLockShell.ps1 [-StateDirectory <path>]`

- [x] **Step 1: Write failing PowerShell assertions** for validate-only, duplicate backup, SID mismatch, rollback, and exact snapshot restoration in `TimeLockApp.Tests/WindowsShell.Tests.ps1`.
- [x] **Step 2: Run the focused script test** and confirm failure because the new scripts do not exist.
- [x] **Step 3: Implement install script** using `Assert-NormalizedExecutablePath`, `Get-RegistryValueSnapshot`, `Write-JsonAtomically`, and `Restore-RegistryValueSnapshot`; snapshot `Winlogon\Shell`, write a quoted executable path, and rollback on failure.
- [x] **Step 4: Implement remove script** with backup existence and current SID checks, restore the snapshot before deleting only shell state files.
- [x] **Step 5: Add README** with install, sign-out/sign-in, removal, Administrator recovery, and exact Registry path.
- [x] **Step 6: Run focused PowerShell tests** and confirm all shell-mode cases pass.

### Task 2: Integrate deployment documentation and packaging guidance

**Files:**
- Modify: `deployment/installer/README.md`
- Modify: `deployment/windows-home-lockdown/README.md`

- [x] **Step 1: Document that the installer’s Startup shortcut is not shell replacement** and link to the shell-mode setup.
- [x] **Step 2: Document recommended order**: publish app, validate path, install Shell as intended Standard user, sign out/in, and keep an Administrator account.
- [x] **Step 3: Run `git diff --check`** and inspect docs for consistent paths and commands.

### Task 3: Verify project and scripts

**Files:**
- Test: `TimeLockApp.Tests/WindowsShell.Tests.ps1`

- [x] **Step 1: Parse both new PowerShell scripts** with `[System.Management.Automation.Language.Parser]::ParseFile` and fail on parse errors.
- [x] **Step 2: Run `dotnet test TimeLockApp.Tests/TimeLockApp.Tests.csproj --no-restore`**.
- [x] **Step 3: Run `dotnet build TimeLockApp.csproj --no-restore`**.
- [x] **Step 4: Run `git diff --check` and review `git status --short`** to ensure only intended source/docs/test files changed.
