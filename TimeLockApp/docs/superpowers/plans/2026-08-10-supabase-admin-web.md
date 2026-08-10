# Supabase Admin Web Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** เพิ่มโครง Supabase schema และ ASP.NET Core Admin Web ระยะแรก โดยยังคง WPF เดิมทำงานได้

**Architecture:** เว็บ Razor Pages เรียก Supabase Data API ผ่าน server-side `HttpClient`; Supabase Auth จัดการ admin session; PostgreSQL เป็น source of truth ของเว็บ และ RLS ป้องกันข้อมูลระดับแถว

**Tech Stack:** ASP.NET Core Razor Pages, .NET 10, Supabase PostgreSQL/Auth/Data API, vanilla CSS/HTML

## Global Constraints

- ห้ามฝัง Supabase service role key ใน browser หรือ repository
- ค่า Supabase URL/key ต้องมาจาก environment variables หรือ user secrets
- Phase นี้ต้องไม่ลบหรือเปลี่ยน flow SQLite/Google Sheets ของ WPF
- Password ใน users table ต้องไม่ถูก render ในหน้าเว็บ

---

### Task 1: Add Supabase database schema and security policies

**Files:**
- Create: `supabase/migrations/202608100001_initial_admin_schema.sql`
- Create: `supabase/README.md`

- [x] Create tables for `admin_profiles`, `users`, `sessions`, and `audit_logs` with UUID/user references and timestamps
- [x] Enable RLS on exposed tables
- [x] Add admin-only policies using `auth.uid()` and `admin_profiles.role`
- [x] Add indexes for username, active users, session start time and audit time
- [x] Document applying migration and creating the first admin profile

### Task 2: Scaffold the Admin Web project

**Files:**
- Create: `TimeLockApp.AdminWeb/TimeLockApp.AdminWeb.csproj`
- Create: `TimeLockApp.AdminWeb/Program.cs`
- Create: `TimeLockApp.AdminWeb/Models/*`
- Create: `TimeLockApp.AdminWeb/Services/SupabaseClient.cs`
- Create: `TimeLockApp.AdminWeb/appsettings.json`
- Create: `TimeLockApp.AdminWeb/Pages/*`

- [x] Add the ASP.NET Core Razor Pages project targeting `net10.0`
- [x] Add config validation for `SUPABASE_URL` and `SUPABASE_ANON_KEY`
- [x] Implement typed server client methods for admin sign-in, user listing, user mutations, session listing and audit insertion
- [x] Add a shared layout with Thai/English-ready labels and no password field in user list

### Task 3: Implement authenticated Admin pages

**Files:**
- Modify: `TimeLockApp.AdminWeb/Pages/Account/Login.cshtml*`
- Create: `TimeLockApp.AdminWeb/Pages/Users/Index.cshtml*`
- Create: `TimeLockApp.AdminWeb/Pages/Sessions/Index.cshtml*`
- Create: `TimeLockApp.AdminWeb/Pages/Shared/_AdminLayout.cshtml`

- [x] Implement login/logout with secure cookie session and server-side token handling
- [x] Implement user list, add, edit active status and allowed minutes
- [x] Implement session history with paging and filters
- [x] Add audit for user mutations
- [x] Add clear error states for missing config, Supabase errors and unauthorized access

### Task 4: Verify and document deployment

**Files:**
- Create: `TimeLockApp.AdminWeb/README.md`
- Create: `TimeLockApp.AdminWeb/appsettings.Development.json.example`
- Modify: `.gitignore`

- [x] Add local run instructions with environment variables and no real secrets
- [x] Build the WPF project and Admin Web project
- [x] Run existing tests
- [x] Run `git diff --check` on source/docs only
- [x] Record remaining phase-2 work for WPF-to-Supabase synchronization
