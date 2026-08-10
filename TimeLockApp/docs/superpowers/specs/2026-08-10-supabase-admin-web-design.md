# Supabase Admin Web Design

## Goal

เพิ่มเว็บ Admin ที่เข้าจากภายนอกได้ โดยใช้ Supabase Auth สำหรับการเข้าสู่ระบบ, Supabase PostgreSQL เป็นฐานข้อมูลกลาง และไม่เปิดเผย service role key ใน browser หรือ WPF

## Phase 1 scope

- สร้าง SQL schema สำหรับ users, sessions และ audit_logs
- เปิด RLS และกำหนด policy ให้เฉพาะ admin ที่ authenticate แล้วจัดการข้อมูลได้
- สร้าง ASP.NET Core Razor Pages สำหรับ login, users และ session history
- ใช้ Supabase Data API ผ่าน server-side HTTP client
- รองรับข้อมูล config ผ่าน environment variables เท่านั้น
- ยังไม่เปลี่ยน WPF จาก SQLite/Google Sheets ใน phase นี้

## Security

- ใช้ Supabase Auth JWT สำหรับ admin session
- service role key อยู่เฉพาะ server environment และห้ามส่งไป client
- password ของ user app จะไม่ถูกแสดงในเว็บโดย default
- ทุก mutation บันทึก audit log พร้อมผู้กระทำและเวลา
- RLS ใช้ `auth.uid()` และตรวจ role จากข้อมูลฝั่ง server ที่แก้โดย end user ไม่ได้

## Phase 2

ย้าย WPF ให้ sync users และ sessions กับ Supabase โดยมี migration/import จาก SQLite และ Google Sheets ก่อนปิดการ sync แบบเดิม
