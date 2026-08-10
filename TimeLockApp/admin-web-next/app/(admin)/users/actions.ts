"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { hashPassword } from "@/lib/password";
import { requireAdmin } from "@/lib/auth";

function text(formData: FormData, key: string) {
  return String(formData.get(key) ?? "").trim();
}

function go(message: string) {
  redirect(`/users?message=${encodeURIComponent(message)}`);
}

export async function saveUser(formData: FormData) {
  const { supabase, user: actor } = await requireAdmin();
  const id = text(formData, "id");
  const username = text(formData, "username");
  const password = String(formData.get("password") ?? "");
  const role = text(formData, "role");
  const allowedMinutes = Number(formData.get("allowed_minutes"));
  if (!username || !Number.isInteger(allowedMinutes) || allowedMinutes < 0 || !["user", "admin"].includes(role)) go("ข้อมูลผู้ใช้ไม่ถูกต้อง");
  if (!id && !password) go("ผู้ใช้ใหม่ต้องมี Password");

  const payload: Record<string, unknown> = { username, allowed_minutes: allowedMinutes, role };
  if (password) payload.password_hash = hashPassword(password);
  const result = id
    ? await supabase.from("users").update(payload).eq("id", id)
    : await supabase.from("users").insert({ ...payload, is_active: true });
  if (result.error) go(`บันทึกไม่สำเร็จ: ${result.error.message}`);

  await supabase.from("audit_logs").insert({ actor_user_id: actor.id, action: id ? "update" : "create", entity_type: "user", entity_id: id || username });
  revalidatePath("/users");
  go(id ? "แก้ไขผู้ใช้แล้ว" : "เพิ่มผู้ใช้แล้ว");
}

export async function toggleUser(formData: FormData) {
  const { supabase, user: actor } = await requireAdmin();
  const id = text(formData, "id");
  const active = text(formData, "active") === "true";
  const { error } = await supabase.from("users").update({ is_active: active }).eq("id", id);
  if (error) go(`เปลี่ยนสถานะไม่สำเร็จ: ${error.message}`);
  await supabase.from("audit_logs").insert({ actor_user_id: actor.id, action: active ? "activate" : "deactivate", entity_type: "user", entity_id: id });
  revalidatePath("/users");
  go(active ? "เปิดใช้งานผู้ใช้แล้ว" : "ปิดใช้งานผู้ใช้แล้ว");
}

export async function deleteUser(formData: FormData) {
  const { supabase, user: actor } = await requireAdmin();
  const id = text(formData, "id");
  const { data: target, error: lookupError } = await supabase.from("users").select("role").eq("id", id).maybeSingle();
  if (lookupError || !target) {
    go("ไม่พบผู้ใช้");
    return;
  }
  if (target.role === "admin") go("ไม่อนุญาตให้ลบผู้ใช้ Admin");

  const { error } = await supabase.from("users").delete().eq("id", id);
  if (error) go(`ลบไม่สำเร็จ: ${error.message}`);
  await supabase.from("audit_logs").insert({ actor_user_id: actor.id, action: "delete", entity_type: "user", entity_id: id });
  revalidatePath("/users");
  go("ลบผู้ใช้ถาวรแล้ว");
}
