import Link from "next/link";
import { requireAdmin } from "@/lib/auth";

export default async function AdminLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const { user } = await requireAdmin();
  return (
    <>
      <header className="site-header">
        <nav className="navbar">
          <Link className="navbar-brand" href="/">TimeLock Admin</Link>
          <Link className="nav-link" href="/users">Users</Link>
          <Link className="nav-link" href="/sessions">Sessions</Link>
          <span className="nav-spacer" />
          <span className="user-chip">admin</span>
          <form action="/auth/signout" method="post"><button className="link-button" type="submit">ออกจากระบบ</button></form>
        </nav>
      </header>
      <main className="container">{children}</main>
    </>
  );
}
