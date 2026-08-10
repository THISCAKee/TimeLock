import { signInAdmin } from "./actions";

const messages: Record<string, string> = {
  credentials: "Username หรือ Password ไม่ถูกต้อง",
  configuration: "ยังไม่ได้ตั้งค่า SUPABASE_ADMIN_EMAIL บน Vercel",
  "not-admin": "บัญชีนี้ไม่มีสิทธิ์ Admin",
};

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  const error = (await searchParams).error;
  return (
    <main className="auth-page">
      <section className="auth-card">
        <div className="brand-mark">⏱</div>
        <p className="eyebrow">TIMELOCK</p>
        <h1>Admin Console</h1>
        <p className="muted">จัดการผู้ใช้และเวลาใช้งานจากระยะไกล</p>
        {error && <div className="alert error">{messages[error] ?? "ไม่สามารถเข้าสู่ระบบได้"}</div>}
        <form action={signInAdmin} className="stack">
          <label htmlFor="username">Admin username</label>
          <input id="username" name="username" defaultValue="admin" autoComplete="username" autoFocus required />
          <label htmlFor="password">Password</label>
          <input id="password" name="password" type="password" autoComplete="current-password" required />
          <button className="primary-button" type="submit">เข้าสู่ระบบ</button>
        </form>
      </section>
    </main>
  );
}
