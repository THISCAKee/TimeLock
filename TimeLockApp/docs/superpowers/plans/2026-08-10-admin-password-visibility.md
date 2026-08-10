# Admin Table Password Visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** เพิ่มปุ่มสลับแสดง/ซ่อน password ในคอลัมน์ Password ของตาราง Admin panel

**Architecture:** ใช้ `DataGridTemplateColumn` แทนคอลัมน์ข้อความเดิม โดย cell template มีข้อความจริงและข้อความปกปิดที่สลับด้วย `DataTrigger` จาก `AdminWindow.IsPasswordVisible` ปุ่มใน header จะสลับ dependency property และ label ของปุ่ม

**Tech Stack:** WPF, XAML, C#, .NET 10 Windows

## Global Constraints

- ค่าเริ่มต้นต้องปกปิด password
- เปลี่ยนเฉพาะการแสดงผลในตาราง ไม่เปลี่ยนข้อมูลในฐานข้อมูลหรือช่องกรอกในฟอร์ม
- ต้อง build โปรเจกต์หลังแก้ไข

---

### Task 1: Add password visibility toggle to the user table

**Files:**
- Modify: `AdminWindow.xaml:123-125` เปลี่ยน Password column เป็น template column พร้อม toggle button ใน header
- Modify: `AdminWindow.xaml.cs:8-35` เพิ่ม dependency property และ event handler สำหรับ toggle

**Interfaces:**
- Produces: `AdminWindow.IsPasswordVisible`, ค่าเริ่มต้น `false`, และ `PasswordVisibilityButton_Click`

- [x] **Step 1: Add the UI binding and event contract**

  เปลี่ยน Password `DataGridTextColumn` เป็น `DataGridTemplateColumn` ที่มี `TextBlock` แสดง `Password`, `TextBlock` แสดง `••••••` และ `DataTrigger` อ้างอิง `IsPasswordVisible` จาก ancestor `AdminWindow`; เพิ่มปุ่ม header ที่เรียก `PasswordVisibilityButton_Click`

- [x] **Step 2: Add the minimal state and handler**

  เพิ่ม `IsPasswordVisibleProperty` แบบ `DependencyProperty` ค่าเริ่มต้น `false`; ใน handler สลับค่า property และเปลี่ยนข้อความปุ่มเป็น “แสดง” หรือ “ซ่อน” โดยไม่แตะ `UserRecord.Password`

- [x] **Step 3: Build the application**

  Run: `dotnet build TimeLockApp.csproj --no-restore`

  Expected: build succeeds with exit code 0 and no XAML compilation errors

- [x] **Step 4: Inspect the diff**

  Run: `git diff --check; git diff -- AdminWindow.xaml AdminWindow.xaml.cs`

  Expected: no whitespace errors and only the requested table visibility behavior is changed
