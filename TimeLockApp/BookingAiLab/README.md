# BookingAiLab

ระบบ Booking แยกจาก TimeLockApp สำหรับให้ผู้ใช้มหาวิทยาลัยมหาสารคามจองเครื่องคอมพิวเตอร์ผ่านเว็บ

## เป้าหมาย

- ใช้ Next.js App Router และ Tailwind CSS 4
- Login ด้วย Google OAuth เฉพาะบัญชี `@msu.ac.th`
- จองเครื่องคอมพิวเตอร์ฟรี
- สร้าง Username/Password สำหรับ TimeLockApp ทันทีหลังจอง
- ส่งข้อมูลการจองให้ผู้จองทาง Email
- แจ้งเตือน Admin
- เชื่อมต่อกับ WPF TimeLockApp ผ่าน API

## เอกสาร

- [Design Spec](docs/booking-design-spec.md)
- [API Contract](docs/booking-api-contract.md)

## ขอบเขตเริ่มต้น

โฟลเดอร์นี้เป็นเอกสารออกแบบสำหรับนำไปสร้างโปรเจกต์ Next.js ต่อบน MacBook ยังไม่มีโค้ดระบบ Production และยังไม่มี Secret ใด ๆ อยู่ในโฟลเดอร์นี้

## การพัฒนาบน MacBook

1. คัดลอกโฟลเดอร์ `BookingAiLab` ไปยังเครื่อง MacBook
2. สร้าง Next.js App ภายในโฟลเดอร์นี้หรือตามโครงสร้างที่ทีมกำหนด
3. ตั้งค่า Environment Variables จากไฟล์ตัวอย่างของระบบจริง โดยห้าม Commit Secret
4. อ่าน Design Spec และ API Contract ก่อนเริ่มแก้ฐานข้อมูลหรือ WPF
