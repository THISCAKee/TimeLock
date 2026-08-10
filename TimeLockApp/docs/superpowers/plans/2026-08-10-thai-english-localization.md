# Thai/English Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ให้ผู้ใช้เลือกภาษาไทย/อังกฤษจากหน้า Login และให้ข้อความทั้งระบบเปลี่ยนตามภาษาที่เลือกโดยไม่บันทึกค่าข้ามการเปิดโปรแกรม

**Architecture:** เพิ่ม `LanguageService` แบบ application-scoped ที่โหลด `ResourceDictionary` ของภาษาไทยหรืออังกฤษและ expose `Get` สำหรับข้อความจาก C#; ข้อความใน XAML ใช้ `DynamicResource` และ ComboBox บน Login เรียก service เมื่อ selection เปลี่ยน

**Tech Stack:** WPF ResourceDictionary, C#, .NET 10 Windows, existing executable test project

## Global Constraints

- ค่าเริ่มต้นทุกครั้งที่เริ่มโปรแกรมคือภาษาไทย
- ไม่บันทึกภาษาที่เลือกลง disk/database
- ครอบคลุมทุกหน้าต่างและข้อความแจ้งเตือนใน scope
- คีย์ที่ไม่มีคำแปลต้องแสดง key แทนข้อความว่าง

---

### Task 1: Create the language service and resource dictionaries

**Files:**
- Create: `Services/LanguageService.cs`
- Create: `Resources/Strings.th.xaml`
- Create: `Resources/Strings.en.xaml`
- Modify: `App.xaml.cs` ลงทะเบียน/ตั้งค่าภาษาเริ่มต้น
- Test: `TimeLockApp.Tests/LanguageServiceTests.cs`

**Interfaces:**
- Produces: `LanguageService.CurrentLanguage`, `SetLanguage(string language)`, and `Get(string key, params object[] args)`

- [x] **Step 1: Write failing tests**

  เพิ่ม tests ที่ assert ว่า service เริ่มต้นเป็น `th`, `SetLanguage("en")` เปลี่ยนเป็น `en`, `Get("LoginButton")` คืนค่าภาษาอังกฤษ, placeholder ถูกแทนค่า และ key ที่ไม่มีคืนค่า key

- [x] **Step 2: Run tests to verify failure**

  Run: `dotnet test TimeLockApp.Tests/TimeLockApp.Tests.csproj --no-restore`

  Expected: FAIL เพราะยังไม่มี `LanguageService`

- [x] **Step 3: Implement the minimal service**

  สร้าง service ที่เก็บ dictionaries แบบ in-memory, ค่าเริ่มต้น `th`, เปลี่ยน dictionary/resource และใช้ `string.Format` เมื่อมี arguments; ห้ามเขียนไฟล์หรือ registry

- [x] **Step 4: Add Thai and English resource keys**

  ใส่ key ครบสำหรับทุกข้อความที่ถูกย้ายจาก XAML/C# ในหน้าต่างทั้ง 6 และจัดรูปแบบ placeholder เช่น `{0}` ให้ตรงกันทั้งสองภาษา

- [x] **Step 5: Run tests to verify they pass**

  Run: `dotnet test TimeLockApp.Tests/TimeLockApp.Tests.csproj --no-restore`

  Expected: all tests pass

### Task 2: Localize all XAML windows and add the language picker

**Files:**
- Modify: `App.xaml` ให้ merge dictionary เริ่มต้น
- Modify: `MainWindow.xaml` เพิ่ม ComboBox และเปลี่ยนข้อความเป็น `DynamicResource`
- Modify: `MainWindow.xaml.cs` เรียก `LanguageService.SetLanguage` เมื่อเลือกภาษา
- Modify: `AdminWindow.xaml`, `NetworkAuthWindow.xaml`, `AlertWindow.xaml`, `SessionHistoryWindow.xaml`, `UsageWindow.xaml` เปลี่ยน static UI strings เป็น `DynamicResource`

**Interfaces:**
- Consumes: `LanguageService` จาก Task 1
- Produces: language picker on Login that updates all open/future windows

- [x] **Step 1: Add language picker**

  เพิ่ม ComboBox ที่มี `ไทย`/`English`, ค่าเริ่มต้น `th`, และ event `LanguageComboBox_SelectionChanged` ใน MainWindow

- [x] **Step 2: Replace XAML literals**

  ย้าย title, TextBlock, Button, placeholder และ DataGrid header ทั้งหมดใน scope ไปใช้ `{DynamicResource KeyName}`; คงค่าข้อมูลและไอคอนไว้ตามเดิม

- [x] **Step 3: Verify resource refresh behavior**

  เรียก `SetLanguage` จาก selection event และตรวจว่า DynamicResource อัปเดตหน้าต่างที่ยังเปิดอยู่ โดยไม่สร้าง window ซ้ำหรือเปลี่ยนข้อมูลฟอร์ม

### Task 3: Localize runtime messages and verify the application

**Files:**
- Modify: `MainWindow.xaml.cs`, `AdminWindow.xaml.cs`, `NetworkAuthWindow.xaml.cs`, `SessionHistoryWindow.xaml.cs`, `UsageWindow.xaml.cs`
- Modify: `Services/AutomaticSyncStatus.cs`, `Services/SessionWarningSchedule.cs` ถ้ามีข้อความที่ผู้ใช้เห็น

**Interfaces:**
- Consumes: `LanguageService.Get`

- [x] **Step 1: Replace hard-coded runtime messages**

  เปลี่ยนข้อความของ validation, sync, network, login error, logout confirmation, expiry alert และ MessageBox เป็น key + arguments ผ่าน service

- [x] **Step 2: Preserve technical values**

  คง username, error message จาก exception, Win32 code, count, time และ status code เป็น arguments ที่ไม่ถูกแปล

- [x] **Step 3: Run full verification**

  Run: `dotnet test TimeLockApp.Tests/TimeLockApp.Tests.csproj --no-restore; dotnet build TimeLockApp.csproj --no-restore; git diff --check`

  Expected: tests and build pass, no whitespace errors, and no user-facing Thai literals remain outside the dictionaries except intentionally preserved data/status values
