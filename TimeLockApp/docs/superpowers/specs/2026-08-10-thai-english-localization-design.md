# Thai/English Localization Design

## Goal

ให้ผู้ใช้เลือกภาษาไทยหรือภาษาอังกฤษจากหน้า Login แล้วให้ข้อความ UI และข้อความแจ้งเตือนของทั้งระบบใช้ภาษาที่เลือกทันที โดยไม่บันทึกภาษาไว้หลังปิดโปรแกรม

## Design

สร้าง `LanguageService` กลางที่เก็บภาษาปัจจุบันและสร้าง `ResourceDictionary` ในหน่วยความจำสำหรับภาษาที่เลือก เมื่อเปลี่ยนภาษา service จะสลับ dictionary ใน `Application.Current.Resources` จากนั้น XAML จะใช้ `DynamicResource` เพื่ออัปเดตข้อความโดยไม่ต้องสร้างหน้าต่างใหม่ ส่วนข้อความที่สร้างจาก C# จะเรียก `LanguageService.Get(key, args)` เพื่อให้รองรับ placeholder และอัปเดตตามภาษาปัจจุบัน

เพิ่ม ComboBox เลือกภาษาในหน้า Login ค่าเริ่มต้นเป็นภาษาไทยทุกครั้งที่เริ่มโปรแกรม การเลือกภาษาจะเปลี่ยน resource dictionary กลาง จึงมีผลกับหน้าต่างที่เปิดอยู่และหน้าต่างที่จะเปิดภายหลัง

## Scope

- ครอบคลุม Main/Login, Admin, Network Auth, Usage, Alert และ Session History
- ครอบคลุม title, label, button, placeholder, DataGrid header และข้อความแจ้งเตือนจาก C#
- ชื่อผู้ใช้, password, URL, status code และข้อมูลจากฐานข้อมูลไม่ถูกแปล
- ไม่บันทึกค่าภาษาในไฟล์หรือฐานข้อมูล
- ถ้าหาคีย์แปลไม่พบ ให้คืน key เพื่อไม่ให้ UI ว่าง

## Verification

เพิ่ม unit tests ให้ `LanguageService` ตรวจค่าเริ่มต้น, การเปลี่ยนภาษา, การแทน placeholder และ fallback จาก key ที่ไม่มี จากนั้น build และ test ทั้งโปรเจกต์
