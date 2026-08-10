# Admin Table Password Visibility Design

## Goal

เพิ่มปุ่มในหัวคอลัมน์ Password ของ Admin panel เพื่อสลับการแสดง password จริงกับข้อความปกปิด โดยค่าเริ่มต้นต้องปกปิด password

## Design

เปลี่ยนคอลัมน์ Password จาก `DataGridTextColumn` เป็น `DataGridTemplateColumn` ภายใน cell จะมีข้อความ password จริงและข้อความปกปิด โดยใช้สถานะ `IsPasswordVisible` ของ `AdminWindow` เป็น `DependencyProperty` และใช้ `DataTrigger` สลับการมองเห็นของข้อความทั้งสองแบบ

ปุ่ม toggle จะอยู่ใน header ของคอลัมน์ Password เมื่อกดจะสลับสถานะและเปลี่ยน label ระหว่าง “แสดง” กับ “ซ่อน” การโหลดข้อมูลใหม่ไม่เปลี่ยนสถานะที่ผู้ใช้เลือก ส่วนการเปิดหน้าต่างใหม่เริ่มต้นด้วยการซ่อนเสมอ

## Scope and safety

- เปลี่ยนเฉพาะการแสดงผลในตาราง ไม่เปลี่ยนช่องกรอก password ในฟอร์ม
- ไม่เปลี่ยนข้อมูล password ในฐานข้อมูลหรือ model
- ค่าเริ่มต้นต้องไม่แสดง password จริงจนกว่าจะกดปุ่ม

## Verification

ตรวจสอบด้วยการ build โปรเจกต์ และตรวจ diff ให้แน่ใจว่ามีเฉพาะการเปลี่ยนแปลงของ Admin table กับเอกสารที่เกี่ยวข้อง
