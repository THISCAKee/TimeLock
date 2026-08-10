"use client";

export function DeleteUserButton() {
  return <button className="link-button danger-link" type="submit" onClick={(event) => { if (!window.confirm("ลบผู้ใช้นี้ถาวรหรือไม่? การลบไม่สามารถย้อนกลับได้")) event.preventDefault(); }}>ลบ</button>;
}
