import { requireAdmin } from "@/lib/auth";

type SessionRow = { id: string; username: string; start_time: string; end_time: string | null; allowed_minutes: number; used_seconds: number; status: string };

export default async function SessionsPage() {
  const { supabase } = await requireAdmin();
  const { data: sessions, error } = await supabase.from("sessions").select("id,username,start_time,end_time,allowed_minutes,used_seconds,status").order("start_time", { ascending: false }).limit(200);
  return (
    <>
      <div className="page-heading"><div><p className="eyebrow">ADMIN CONSOLE</p><h1>Sessions</h1><p className="muted">ประวัติการใช้งานล่าสุด</p></div><span className="status-pill">ล่าสุด 200 รายการ</span></div>
      {error && <div className="alert error">โหลดประวัติไม่สำเร็จ: {error.message}</div>}
      <section className="panel"><div className="table-wrap"><table><thead><tr><th>Username</th><th>Start</th><th>End</th><th>Allowed</th><th>Used</th><th>Status</th></tr></thead><tbody>
        {(sessions as SessionRow[] | null)?.map((session) => <tr key={session.id}><td className="strong">{session.username}</td><td>{new Date(session.start_time).toLocaleString("th-TH")}</td><td>{session.end_time ? new Date(session.end_time).toLocaleString("th-TH") : "-"}</td><td>{session.allowed_minutes} min</td><td>{Math.floor(session.used_seconds / 60)} min {session.used_seconds % 60} sec</td><td>{session.status}</td></tr>)}
      </tbody></table></div></section>
    </>
  );
}
