# TimeLock Inno Setup Installer Design

## Goal

สร้าง Windows Installer แบบ Inno Setup สำหรับ TimeLock WPF โดยให้ผู้ใช้ติดตั้งและถอนการติดตั้งได้ง่าย และไม่แจกไฟล์ credential ของ Google Sheets ไปกับชุดติดตั้ง

## Scope

- Publish แอปเป็น `win-x64`, `Release`, self-contained สำหรับ `net10.0-windows`
- สร้างไฟล์ติดตั้ง `.exe` ด้วย Inno Setup
- สร้าง shortcut บน Desktop และ Start Menu
- ลงทะเบียนรายการถอนการติดตั้งผ่าน Inno Setup
- วางไฟล์ผลลัพธ์ไว้ที่ `deployment/installer/output/TimeLock-Setup.exe`
- ไม่รวม `Secrets/service-account.json`
- ไม่รวมฐานข้อมูล runtime, WebView2 cache, ไฟล์ debug และไฟล์ build ที่ไม่จำเป็น

## Files

- `deployment/installer/TimeLock.iss` — สคริปต์ Inno Setup สำหรับกำหนด metadata, ไฟล์, shortcut และ uninstall
- `deployment/installer/Build-Installer.ps1` — publish แอปและเรียก Inno Setup compiler
- `deployment/installer/README.md` — ข้อกำหนดการ build, การติดตั้ง และการเตรียม credential หลังติดตั้ง

## Packaging flow

```text
TimeLockApp.csproj
        |
        v
dotnet publish (Release, win-x64, self-contained)
        |
        v
deployment/installer/publish/
        |
        v
Inno Setup compiler
        |
        v
deployment/installer/output/TimeLock-Setup.exe
```

สคริปต์ build จะตรวจสอบว่า `dotnet` และ Inno Setup compiler มีอยู่ก่อนเริ่มงาน และจะล้มเหลวทันทีเมื่อ publish หรือ compile ไม่สำเร็จ

## Installation behavior

- ติดตั้งแอปลงใน `Program Files\TimeLock`
- สร้าง Start Menu shortcut และ Desktop shortcut ที่ผู้ใช้เลือกได้
- ใช้ชื่อแอปและ publisher เป็น TimeLock
- ไม่เขียนหรือสร้าง `Secrets/service-account.json` โดยอัตโนมัติ
- README จะแจ้งให้ผู้ดูแลเตรียม credential แยกต่างหากในโฟลเดอร์ที่แอปคาดหวัง ก่อนใช้งาน Google Sheets

## Verification

- `dotnet publish` ต้องสำเร็จโดยไม่มี error
- Inno Setup compiler ต้องสร้าง `TimeLock-Setup.exe` สำเร็จ
- ตรวจสอบว่าไฟล์ credential ไม่อยู่ใน publish output หรือ installer manifest
- ตรวจสอบว่าไฟล์ output มีอยู่จริงและมีขนาดมากกว่าศูนย์

## Out of scope

- การเซ็นโค้ดด้วย certificate
- การดาวน์โหลดหรือติดตั้ง WebView2 Runtime แยกต่างหาก
- การฝัง Google service-account credential ใน Installer
- การสร้าง MSI หรือ MSIX
