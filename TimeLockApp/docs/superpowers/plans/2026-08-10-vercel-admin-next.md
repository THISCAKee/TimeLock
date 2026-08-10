# Vercel Admin Next Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Next.js Admin Panel deployable directly to Vercel with Supabase Auth and existing admin data policies.

**Architecture:** Create `admin-web-next` as an isolated Next.js App Router app. Use `@supabase/ssr` cookie-based sessions and server actions for all mutations; keep the ASP.NET Admin Web available during migration.

**Tech Stack:** Next.js, React, TypeScript, `@supabase/ssr`, `@supabase/supabase-js`, Vercel.

## Global Constraints

- Login UI accepts only `admin`; `SUPABASE_ADMIN_EMAIL` remains server-only.
- Never use or expose a Supabase service-role key.
- Preserve permanent user deletion, audit logging, and session-history retention.
- Use the existing Supabase schema and RLS policies.

---

### Task 1: Scaffold the Next.js application

**Files:**
- Create: `admin-web-next/package.json`, `admin-web-next/tsconfig.json`, `admin-web-next/next.config.ts`
- Create: `admin-web-next/app/layout.tsx`, `admin-web-next/app/globals.css`, `admin-web-next/.env.example`
- Create: `admin-web-next/lib/supabase/server.ts`, `admin-web-next/lib/supabase/client.ts`

- [ ] Create package scripts and Supabase dependencies.
- [ ] Add SSR client factories using `NEXT_PUBLIC_SUPABASE_URL` and `NEXT_PUBLIC_SUPABASE_ANON_KEY`.
- [ ] Add shared layout metadata and responsive visual styles.
- [ ] Run `npm install` and `npm run build` after the initial scaffold.

### Task 2: Add admin authentication and shell

**Files:**
- Create: `admin-web-next/app/login/page.tsx`, `admin-web-next/app/login/actions.ts`
- Create: `admin-web-next/app/(admin)/layout.tsx`, `admin-web-next/app/(admin)/page.tsx`
- Create: `admin-web-next/app/auth/signout/route.ts`
- Create: `admin-web-next/lib/auth.ts`

- [ ] Implement server-side `admin` alias login through `SUPABASE_ADMIN_EMAIL`.
- [ ] Verify `admin_profiles.role` before creating/retaining access.
- [ ] Redirect unauthenticated users to `/login` and provide sign-out.
- [ ] Run lint/build and verify protected-route behavior locally.

### Task 3: Implement Users management

**Files:**
- Create: `admin-web-next/app/(admin)/users/page.tsx`, `admin-web-next/app/(admin)/users/actions.ts`
- Modify: `admin-web-next/app/globals.css`

- [ ] Read users through the authenticated SSR client.
- [ ] Add and edit users with PBKDF2-compatible password hashes and audit rows.
- [ ] Add activate/deactivate actions.
- [ ] Add confirmed permanent delete, reject admin rows, and retain audit/session behavior.
- [ ] Run build and manually verify all user actions against Supabase.

### Task 4: Implement Sessions and Vercel deployment docs

**Files:**
- Create: `admin-web-next/app/(admin)/sessions/page.tsx`
- Create: `admin-web-next/README.md`, `admin-web-next/vercel.json`
- Modify: `README.md`

- [ ] Render recent sessions with status and usage fields.
- [ ] Document Vercel environment variables and root-directory deployment.
- [ ] Run final lint/build and report the Vercel deployment steps.
