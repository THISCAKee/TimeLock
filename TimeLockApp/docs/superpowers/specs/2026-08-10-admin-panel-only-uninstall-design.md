# Admin Panel–Only Uninstall Design

## Goal

ให้การถอนการติดตั้ง TimeLock ทำได้จากปุ่มใน Admin Panel เท่านั้น ผู้ใช้ทั่วไปต้องไม่มีปุ่มหรือเส้นทาง UI สำหรับถอนการติดตั้ง และ Windows ต้องไม่แสดงรายการถอนการติดตั้งของแอป

## Design

- `AdminWindow` เป็นจุดเรียก `ApplicationUninstaller.TryStart` เพียงจุดเดียว
- `ApplicationUninstaller` จะค้นหา `unins000.exe` ในโฟลเดอร์ติดตั้งและเรียกด้วย `runas` เพื่อให้ Windows ขอสิทธิ์ยกระดับตามปกติ
- Inno Setup ใช้ `Uninstallable=yes` ร่วมกับ `CreateUninstallRegKey=no` เพื่อสร้าง `unins000.exe` สำหรับ Admin Panel โดยไม่สร้าง Windows Add/Remove Programs entry
- ไม่เพิ่ม logic หรือ UI ใน `MainWindow` และหน้าผู้ใช้ทั่วไป

## Error handling

หากไม่พบ uninstaller หรือเริ่ม process ไม่ได้ ให้แสดงข้อความผิดพลาดใน Admin Panel และไม่ปิดแอป ผู้ใช้ต้องยืนยันผ่าน dialog ก่อนเริ่มถอนการติดตั้ง เมื่อเริ่มสำเร็จจึงปิดแอปเพื่อให้ uninstaller ทำงานต่อได้

## Verification

- ตรวจ source ว่า uninstall handler มีเฉพาะใน `AdminWindow`
- ตรวจ installer script ว่า `Uninstallable=yes` และ `CreateUninstallRegKey=no` รวมทั้งไม่มี uninstaller shortcut
- รัน test suite ของโปรเจกต์และ build WPF project
- ตรวจ `git diff --check` และจำกัด diff ให้เฉพาะไฟล์ที่เกี่ยวข้องกับงานนี้
