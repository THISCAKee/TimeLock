# Supabase setup

1. Create a Supabase project.
2. Run `migrations/202608100001_initial_admin_schema.sql` in the Supabase SQL Editor.
3. Create the first Admin user in Supabase Auth.
4. Insert that Auth user's ID into `public.admin_profiles` using the SQL comment at the end of the migration.
5. Keep the Supabase service-role key only in the server deployment environment.

The WPF application still uses its existing SQLite/Google Sheets flow in this phase. Data migration and WPF synchronization are phase 2 work.
