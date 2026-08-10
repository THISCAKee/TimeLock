import Link from "next/link";

export default function DashboardPage() {
  return (
    <>
      <div className="page-heading">
        <div><p className="eyebrow">ADMIN CONSOLE</p><h1>Dashboard</h1><p className="muted">จัดการระบบ TimeLock จากภายนอก</p></div>
      </div>
      <div className="dashboard-grid">
        <Link className="panel" href="/users"><span className="feature-icon">👥</span><h2>Users</h2><p className="muted">เพิ่ม แก้ไข เปิดปิด และลบผู้ใช้</p></Link>
        <Link className="panel" href="/sessions"><span className="feature-icon">◷</span><h2>Sessions</h2><p className="muted">ดูประวัติการใช้งานล่าสุด</p></Link>
      </div>
    </>
  );
}
