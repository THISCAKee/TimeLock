# BookingAiLab — Booking System Design Spec

## 1. ภาพรวม

BookingAiLab เป็นระบบเว็บสำหรับจองเครื่องคอมพิวเตอร์ โดยพัฒนาด้วย Next.js และ Tailwind CSS 4 ใช้ Supabase เป็นฐานข้อมูลและระบบ Authentication ระบบทำงานร่วมกับ TimeLockApp ซึ่งเป็นโปรแกรม WPF ที่ติดตั้งอยู่บนเครื่องคอมพิวเตอร์แต่ละเครื่อง

ระบบ Booking และ WPF แยก Repository และแยกการ Deploy กัน แต่ใช้ Supabase Project กลางและ API Contract เดียวกัน

## 2. ข้อกำหนดที่ยืนยันแล้ว

- ผู้จองทุกคนที่มีบัญชี Google Email ลงท้ายด้วย `@msu.ac.th` สามารถจองได้
- ใช้ Google OAuth ของมหาวิทยาลัย
- การจองไม่มีค่าใช้จ่ายและยืนยันผลทันที
- เมื่อจองสำเร็จ ระบบสร้าง Username/Password สำหรับเข้า TimeLockApp ทันที
- ส่งรายละเอียดการจองและ Credential ให้ผู้จองทาง Email
- แจ้ง Admin ว่าใครจองเครื่องใด ช่วงเวลาใด และ Credential ถูกสร้างแล้ว
- ส่งข้อมูลการจองให้ WPF ผ่าน API ไม่ส่งผ่าน Email
- ระบบต้องป้องกันการจองเครื่องเดียวกันในช่วงเวลาซ้อนกัน

## 3. สถาปัตยกรรม

```text
Customer Browser
    |
    v
Next.js App Router + Tailwind CSS 4
    |-- Supabase Auth: Google OAuth
    |-- Server Actions / Route Handlers
    |-- Email Provider
    v
Supabase PostgreSQL + RLS
    |-- customers
    |-- machines
    |-- bookings
    |-- app_credentials
    |-- machine_events
    |-- notifications
    `-- audit_logs
    ^
    |
WPF TimeLockApp Machine Agent
```

Next.js เป็นเจ้าของ Workflow การจองและฐานข้อมูล Booking ส่วน WPF เป็น Agent ประจำเครื่อง มีหน้าที่รับคำสั่งของเครื่องตัวเอง เริ่ม/จบ Session และรายงานสถานะกลับมา

## 4. Authentication และสิทธิ์

### 4.1 ผู้จอง

ใช้ Supabase Auth กับ Google Provider ผู้ใช้ต้องผ่านเงื่อนไขทั้งหมด:

- Provider เป็น `google`
- Email ผ่านการยืนยันแล้ว
- Email เป็น Lowercase และลงท้ายด้วย `@msu.ac.th`

การตรวจ Domain ต้องทำที่ Server จาก Supabase Auth User ห้ามเชื่อค่าที่ส่งจาก Browser โดยตรง

### 4.2 Admin

Admin ใช้ Supabase Auth เช่นเดียวกัน แต่สิทธิ์มาจากตาราง `admin_profiles` หรือ Role ที่ควบคุมฝั่ง Server เท่านั้น

### 4.3 WPF Machine Agent

แต่ละเครื่องมี `machine_code` และ Device Token เฉพาะเครื่อง Token จริงห้ามเก็บเป็น Plain Text ในฐานข้อมูล ควรเก็บ Hash และส่งผ่าน HTTPS เท่านั้น

ห้ามใส่ Supabase Service Role Key ใน WPF หรือ Browser

## 5. Booking Workflow

```text
1. ผู้ใช้ Login ด้วย Google
2. ระบบตรวจว่า Email ลงท้ายด้วย @msu.ac.th
3. ผู้ใช้เลือกเครื่อง วัน และเวลา
4. Server ตรวจสอบ Input และเวลาที่อนุญาต
5. Database ตรวจสอบ Booking ที่เวลาซ้อนกัน
6. Transaction สร้าง Booking เป็น confirmed
7. สร้าง Username/Password สำหรับ Booking
8. สร้าง Machine Event เป็น app_pending
9. สร้าง Notification เป็น email_pending
10. ส่ง Email ให้ผู้จองแบบ Async และ Retry ได้
11. แสดง Booking สำเร็จแก่ผู้ใช้
12. Admin Dashboard แสดง Alert ใหม่
13. WPF รับ Event และตอบกลับ app_received
14. เมื่อถึงเวลา WPF เริ่ม Session และตอบกลับ active
15. เมื่อหมดเวลา WPF จบ Session และตอบกลับ completed
```

การสร้าง Booking และการล็อกช่วงเวลาต้องอยู่ใน Transaction เดียวกัน เพื่อป้องกันผู้ใช้หลายคนจองเครื่องเดียวกันพร้อมกัน

## 6. สถานะหลัก

### Booking

```text
confirmed
app_pending
app_received
active
completed
cancelled
expired
```

### Machine Event

```text
pending
delivered
processed
failed
```

### Notification

```text
pending
sent
failed
retrying
```

## 7. ฐานข้อมูลที่ต้องเพิ่ม

### `customer_profiles`

```text
id uuid primary key
auth_user_id uuid unique references auth.users(id)
university_email text unique not null
display_name text not null
created_at timestamptz not null
updated_at timestamptz not null
```

### `machines`

```text
id uuid primary key
machine_code text unique not null
machine_name text not null
location text
device_token_hash text not null
status text not null
last_seen_at timestamptz
created_at timestamptz not null
updated_at timestamptz not null
```

### `bookings`

```text
id uuid primary key
booking_number text unique not null
customer_id uuid references customer_profiles(id)
machine_id uuid references machines(id)
start_at timestamptz not null
end_at timestamptz not null
status text not null
created_at timestamptz not null
updated_at timestamptz not null
```

### `app_credentials`

```text
id uuid primary key
booking_id uuid unique references bookings(id)
username text unique not null
password_hash text not null
password_encrypted text not null
expires_at timestamptz not null
first_login_at timestamptz
created_at timestamptz not null
```

`password_hash` ใช้ตรวจสอบการ Login ส่วน `password_encrypted` ใช้สำหรับส่งครั้งเดียวให้ WPF และต้องเข้ารหัสด้วย Secret ที่อยู่ฝั่ง Server

### `machine_events`

```text
id uuid primary key
machine_id uuid references machines(id)
booking_id uuid references bookings(id)
event_type text not null
payload jsonb not null
status text not null
created_at timestamptz not null
processed_at timestamptz
```

### `notifications`

```text
id uuid primary key
booking_id uuid references bookings(id)
recipient_type text not null
recipient text not null
notification_type text not null
status text not null
provider_message_id text
attempt_count integer not null default 0
last_error text
created_at timestamptz not null
sent_at timestamptz
```

## 8. การส่ง Email และ Alert

ใช้ Email Provider เช่น Resend จากฝั่ง Server ของ Next.js เท่านั้น ผู้จองจะได้รับ:

- เลขที่การจอง
- เครื่องที่จอง
- วันและเวลา
- Username
- Password
- วิธีใช้งาน
- ช่องทางติดต่อ Admin

Admin Dashboard จะแสดง Alert และสถานะการส่ง Email/WPF ส่วน Password ไม่ควรแสดงแบบ Plain Text ตลอดเวลา ควร Mask และเปิดดูได้ครั้งเดียวหรือออก Credential ใหม่ได้

## 9. การป้องกันการใช้งานผิดประเภท

เนื่องจากเป็นการจองฟรี ระบบควรมีตั้งแต่ MVP:

- จำกัดจำนวน Booking ต่อ Email ต่อวัน
- ป้องกัน Email เดิมจองช่วงเวลาซ้อนกัน
- ป้องกันเครื่องเดียวกันถูกจองเวลาซ้อนกัน
- จำกัดจำนวน Request ต่อ IP/บัญชี
- CAPTCHA เมื่อมีพฤติกรรมผิดปกติ
- ยกเลิก Booking ได้ตามนโยบายที่กำหนด
- Expire Booking ที่ไม่เริ่มใช้งานตามเวลาที่กำหนด

## 10. การเชื่อมต่อ WPF

WPF จะ Poll API ทุกช่วงเวลาที่กำหนดและส่ง Heartbeat ไม่ควรให้ Browser เชื่อมต่อเข้าเครื่องโดยตรง

WPF ต้องรองรับ:

- Register เครื่อง
- Heartbeat
- รับ Event แบบ Idempotent
- Retry เมื่อ Network หลุด
- ส่ง `app_received`, `active`, `completed`, `failed`
- เก็บ Credential ใน Memory หรือไฟล์เข้ารหัส
- ไม่เขียน Password ลง Debug Log

## 11. โครงสร้าง Next.js ที่แนะนำ

```text
BookingAiLab/
├── app/
│   ├── login/
│   ├── booking/
│   ├── my-bookings/
│   ├── admin/
│   └── api/
├── components/
├── lib/
│   ├── auth/
│   ├── booking/
│   ├── email/
│   ├── machines/
│   └── supabase/
├── supabase/migrations/
├── types/
├── docs/
└── .env.local
```

## 12. Testing

ต้องมีการทดสอบอย่างน้อย:

- Login ด้วยบัญชี `@msu.ac.th`
- ปฏิเสธบัญชีนอก Domain
- สร้าง Booking สำเร็จ
- ป้องกันเวลาจองซ้อน
- สร้าง Credential ไม่ซ้ำ
- Email ส่งสำเร็จและ Retry เมื่อผิดพลาด
- WPF รับ Event ซ้ำโดยไม่สร้าง Session ซ้ำ
- Customer อ่าน Booking ของคนอื่นไม่ได้
- Admin อ่านและจัดการข้อมูลได้ตามสิทธิ์
- Machine Agent อ่านเฉพาะข้อมูลของเครื่องตัวเอง

## 13. ลำดับการพัฒนา

1. สร้าง Next.js App และติดตั้ง Tailwind CSS 4
2. ตั้งค่า Supabase Auth Google OAuth
3. ทำ Migration ตาราง Booking
4. ทำ Customer Booking Flow
5. ทำ Credential Generator
6. ทำ Email Outbox และ Notification
7. ทำ Admin Dashboard
8. ทำ Machine API และ Machine Token
9. เพิ่ม WPF Booking API Client
10. ทดสอบ End-to-End

## 14. ขอบเขตที่ยังไม่รวมใน MVP

- ระบบชำระเงิน
- ระบบสมาชิกนอก Google OAuth
- การจองหลายเครื่องในรายการเดียว
- ระบบส่วนลด
- Mobile Application
- LINE Notification
- การจัดสรรเครื่องแบบอัตโนมัติขั้นสูง
