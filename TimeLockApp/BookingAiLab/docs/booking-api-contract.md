# BookingAiLab — WPF API Contract Draft

เอกสารนี้เป็นข้อตกลงเบื้องต้นระหว่าง Booking Web และ WPF TimeLockApp ก่อนเริ่ม Implement จริง ทั้งสองโปรเจกต์ต้องใช้ชื่อ Field, Status และ Timezone ให้ตรงกัน

## หลักการ

- ทุก Endpoint ใช้ HTTPS
- WPF ส่ง `machine_code` และ Device Token ใน Header
- Server ตรวจสอบ Token ก่อนคืนข้อมูล
- Payload ใช้ JSON และเวลาใช้ ISO 8601 พร้อม Timezone
- Event ต้องประมวลผลแบบ Idempotent
- WPF ต้องตอบกลับด้วย `event_id` เดิมเพื่อป้องกันการประมวลผลซ้ำ

## Endpoints

### Register Machine

```http
POST /api/machines/register
```

ใช้สำหรับลงทะเบียนหรือยืนยันเครื่องกับระบบกลาง โดยควรทำผ่านขั้นตอน Admin หรือ Setup Token ที่ออกให้เฉพาะเครื่อง

### Heartbeat

```http
POST /api/machines/heartbeat
```

Request:

```json
{
  "machineCode": "PC-001",
  "appVersion": "1.0.0",
  "osVersion": "Windows",
  "reportedAt": "2026-08-20T09:55:00+07:00"
}
```

### ดึง Event ของเครื่อง

```http
GET /api/machines/PC-001/events?limit=20
```

Response:

```json
{
  "events": [
    {
      "eventId": "event-id",
      "eventType": "booking_confirmed",
      "bookingId": "booking-id",
      "bookingNumber": "BK-20260820-0001",
      "machineCode": "PC-001",
      "startAt": "2026-08-20T10:00:00+07:00",
      "endAt": "2026-08-20T12:00:00+07:00",
      "username": "booking_0001",
      "password": "one-time-password"
    }
  ]
}
```

### ตอบรับ Event

```http
POST /api/machine-events/event-id/ack
```

Request:

```json
{
  "status": "processed",
  "processedAt": "2026-08-20T09:56:00+07:00",
  "message": null
}
```

### รายงานสถานะ Session

```http
POST /api/bookings/booking-id/session-status
```

Request:

```json
{
  "status": "active",
  "reportedAt": "2026-08-20T10:00:02+07:00",
  "usedSeconds": 2
}
```

## Error Codes

```text
MACHINE_NOT_REGISTERED
MACHINE_TOKEN_INVALID
BOOKING_NOT_FOUND
EVENT_ALREADY_PROCESSED
BOOKING_EXPIRED
INVALID_STATUS_TRANSITION
RATE_LIMITED
```

## ข้อกำหนดด้านความปลอดภัย

- ห้ามส่ง Supabase Service Role Key ให้ WPF
- ห้ามส่ง Credential ผ่าน Query String
- ห้ามบันทึก Password ลง Log
- จำกัด Event ตาม `machine_id` ของ Token
- หมุน Device Token ได้จาก Admin
- ยกเลิก Token ได้เมื่อเครื่องถูกถอดออกจากระบบ
