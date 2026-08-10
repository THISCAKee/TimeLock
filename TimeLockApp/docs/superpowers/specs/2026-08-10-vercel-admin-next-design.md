# Vercel Admin Panel Design

## Goal

Replace the ASP.NET Core remote Admin Panel with a Next.js App Router application that deploys directly to Vercel and uses Supabase Auth and the existing RLS-protected tables.

## Architecture

The new app lives in `admin-web-next` and uses Next.js server components plus server actions. Supabase SSR cookies hold the admin session; the browser receives only the anon key, while `SUPABASE_ADMIN_EMAIL` remains server-side and maps the visible `admin` username to the Supabase Auth email. Existing `public.admin_profiles` policies authorize reads and writes, so no service-role key is needed.

The existing ASP.NET Admin Web remains untouched during migration. The Next.js panel reproduces Login, Users CRUD/toggle/permanent-delete, and Sessions views, with the current visual language and responsive tables.

## Security and behavior

- Login accepts only username `admin` and password; the server signs in using `SUPABASE_ADMIN_EMAIL`.
- All protected pages verify the Supabase session and `admin_profiles.role = admin` on the server.
- User deletion is permanent from `public.users`, retains session history through `on delete set null`, and writes an audit record.
- Admin-role rows cannot be deleted from the UI.
- `SUPABASE_SERVICE_ROLE_KEY` is not used or exposed.

## Verification

Run npm lint/build. Verify unauthenticated redirect, admin login, users CRUD actions, delete confirmation, and sessions rendering against a configured Supabase project before switching the production URL.
