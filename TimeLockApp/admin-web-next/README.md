# TimeLock Admin Next

Next.js Admin Panel for Vercel. It uses Supabase Auth, SSR cookies, and the existing RLS policies.

## Local run

```powershell
Copy-Item .env.example .env.local
# fill NEXT_PUBLIC_SUPABASE_URL, NEXT_PUBLIC_SUPABASE_ANON_KEY, SUPABASE_ADMIN_EMAIL
npm install
npm run dev
```

Open `http://localhost:3000` and sign in with username `admin` and the password of the Auth user configured in `SUPABASE_ADMIN_EMAIL`.

## Vercel

Import the repository in Vercel and set the project Root Directory to `TimeLockApp/admin-web-next` (or select this folder if the repository root is already `TimeLockApp`). Add the three variables above in Project Settings → Environment Variables for Production and Preview.

Do not add a Supabase service-role key. The app uses the anon key with the signed-in admin JWT and the existing `admin_profiles` RLS policies.
