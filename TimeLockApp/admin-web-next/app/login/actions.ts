"use server";

import { redirect } from "next/navigation";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export async function signInAdmin(formData: FormData) {
  const username = String(formData.get("username") ?? "").trim().toLowerCase();
  const password = String(formData.get("password") ?? "");
  if (username !== "admin" || !password) redirect("/login?error=credentials");

  const email = process.env.SUPABASE_ADMIN_EMAIL;
  if (!email) redirect("/login?error=configuration");

  const supabase = await createSupabaseServerClient();
  const { data, error } = await supabase.auth.signInWithPassword({ email, password });
  if (error || !data.user) redirect("/login?error=credentials");

  const { data: profile } = await supabase
    .from("admin_profiles")
    .select("role")
    .eq("user_id", data.user.id)
    .maybeSingle();
  if (profile?.role !== "admin") {
    await supabase.auth.signOut();
    redirect("/login?error=not-admin");
  }

  redirect("/");
}
