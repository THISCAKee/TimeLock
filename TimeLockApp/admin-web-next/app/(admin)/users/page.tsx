import { saveUser, toggleUser, deleteUser } from "./actions";
import { DeleteUserButton } from "./DeleteUserButton";
import { requireAdmin } from "@/lib/auth";

type UserRow = { id: string; username: string; allowed_minutes: number; role: string; is_active: boolean; created_at: string };

export default async function UsersPage({ searchParams }: { searchParams: Promise<{ edit?: string; message?: string }> }) {
  const { supabase } = await requireAdmin();
  const params = await searchParams;
  const { data: users, error } = await supabase.from("users").select("id,username,allowed_minutes,role,is_active,created_at").order("username");
  const editing = (users as UserRow[] | null)?.find((item) => item.id === params.edit);
  return (
    <>
      <div className="page-heading"><div><p className="eyebrow">ADMIN CONSOLE</p><h1>Users</h1><p className="muted">จัดการบัญชีและเวลาใช้งาน</p></div><span className="status-pill">{users?.length ?? 0} users</span></div>
      {params.message && <div className="alert success">{params.message}</div>}
      {error && <div className="alert error">โหลดผู้ใช้ไม่สำเร็จ: {error.message}</div>}
      <section className="panel">
        <h2>{editing ? "แก้ไขผู้ใช้" : "เพิ่มผู้ใช้"}</h2>
        <form action={saveUser} className="form-grid">
          <input type="hidden" name="id" value={editing?.id ?? ""} />
          <div><label htmlFor="username">Username</label><input id="username" name="username" defaultValue={editing?.username} required /></div>
          <div><label htmlFor="password">Password <span className="muted">(เว้นว่างเพื่อใช้ค่าเดิม)</span></label><input id="password" name="password" type="password" autoComplete="new-password" /></div>
          <div><label htmlFor="allowed_minutes">Allowed minutes</label><input id="allowed_minutes" name="allowed_minutes" type="number" min="0" defaultValue={editing?.allowed_minutes ?? 0} required /></div>
          <div><label htmlFor="role">Role</label><select id="role" name="role" defaultValue={editing?.role ?? "user"}><option value="user">user</option><option value="admin">admin</option></select></div>
          <div className="form-actions"><button className="primary-button" type="submit">บันทึกผู้ใช้</button>{editing && <a className="secondary-button" href="/users">ยกเลิก</a>}</div>
        </form>
      </section>
      <section className="panel"><h2>ผู้ใช้ทั้งหมด</h2><div className="table-wrap"><table><thead><tr><th>Username</th><th>Minutes</th><th>Role</th><th>Status</th><th>Created</th><th>Actions</th></tr></thead><tbody>
        {(users as UserRow[] | null)?.map((item) => <tr key={item.id}><td className="strong">{item.username}</td><td>{item.allowed_minutes}</td><td>{item.role}</td><td><span className={`badge ${item.is_active ? "active" : "inactive"}`}>{item.is_active ? "Active" : "Inactive"}</span></td><td>{new Date(item.created_at).toLocaleString("th-TH")}</td><td className="actions"><a href={`/users?edit=${item.id}`}>แก้ไข</a><form action={toggleUser} className="inline-form"><input type="hidden" name="id" value={item.id} /><input type="hidden" name="active" value={String(!item.is_active)} /><button className="link-button" type="submit">{item.is_active ? "ปิดใช้งาน" : "เปิดใช้งาน"}</button></form><form action={deleteUser} className="inline-form"><input type="hidden" name="id" value={item.id} /><DeleteUserButton /></form></td></tr>)}
      </tbody></table></div></section>
    </>
  );
}
