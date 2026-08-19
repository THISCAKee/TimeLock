# Windows Shell Mode Design

## Goal

ให้ TimeLock เปิดแทน Windows Desktop/Explorer สำหรับบัญชีผู้ใช้เฉพาะ หลัง sign in โดยสามารถถอนการตั้งค่าและคืนค่า Explorer เดิมได้อย่างปลอดภัย

## Scope

- ตั้งค่า Shell เฉพาะ HKCU ของบัญชีผู้ใช้ที่รันสคริปต์
- สำรองค่า Shell เดิมและข้อมูลผู้ใช้ไว้ใน `backup.json`
- ตั้งค่า `TimeLockApp.exe` เป็น Shell
- ถอนการตั้งค่าโดยตรวจสอบ SID และคืนค่าที่สำรองไว้
- ป้องกันการเขียนทับ backup ที่ยังไม่ได้ถูกถอน
- รองรับ `-ValidateOnly` ซึ่งไม่เปลี่ยน Registry, ไฟล์ หรือ process
- อัปเดต README ให้มีคำเตือนเรื่องบัญชี Administrator สำรองและขั้นตอน recovery

## Non-goals

- ไม่เปลี่ยน Shell ของทุกบัญชีในเครื่อง
- ไม่ปิดใช้งาน Winlogon, Secure Attention Sequence หรือสิทธิ์ Administrator
- ไม่ทำ Assigned Access/Shell Launcher ที่ต้องพึ่ง Windows edition หรือ Group Policy
- ไม่ลบ Explorer ออกจากเครื่อง

## Design

`Install-TimeLockShell.ps1` จะตรวจ path ของ executable และตัวตนผู้ใช้ จากนั้นสร้าง state directory ภายใต้ `%LOCALAPPDATA%\TimeLockApp\Shell`, บันทึก SID, Registry root, app path และ snapshot ของค่า `Shell` ใต้ `HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Winlogon` ลงใน JSON ก่อนเปลี่ยนค่า `Shell` เป็น path แบบ quote ของ TimeLock executable

`Remove-TimeLockShell.ps1` จะอ่าน backup, ตรวจว่า SID ปัจจุบันตรงกับ SID ที่บันทึกไว้, คืนค่า snapshot เดิม และลบ state files ที่สร้างโดย installer เท่านั้น หาก backup หายหรือเป็นของผู้ใช้อื่น จะหยุดก่อนเปลี่ยน Registry

ทั้งสองสคริปต์จะ reuse helper จาก `TimeLockHomeLockdown.psm1` สำหรับ path normalization, Registry snapshots และ atomic JSON writes เพื่อให้ behavior สอดคล้องกับระบบ lockdown เดิม

## Failure handling and recovery

- ถ้าเขียน Shell ไม่สำเร็จ จะคืนค่า snapshot เดิมทันที
- ถ้า backup มีอยู่แล้ว จะไม่เขียนทับและแจ้งให้ถอนของเดิมก่อน
- README ต้องบอกให้เก็บ Administrator account แยกไว้ และระบุ Registry path สำหรับ offline recovery
- การถอนต้องไม่ลบ backup ก่อนคืนค่า Registry สำเร็จ

## Verification

- เพิ่ม regression tests แบบ PowerShell สำหรับ validate-only, SID mismatch, duplicate backup และ exact Registry restoration
- รันชุด tests ที่มีอยู่, `dotnet build TimeLockApp.csproj --no-restore` และ `git diff --check`
- ตรวจสคริปต์ด้วย PowerShell parser ก่อนใช้งานจริง

