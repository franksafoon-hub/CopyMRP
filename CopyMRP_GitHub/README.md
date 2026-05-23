# CopyMRP

โปรแกรม Copy _AUFTRAG.TXT จาก BU folders มายัง folder ปลายทาง

---

## วิธี Build เป็น .exe ผ่าน GitHub (ไม่ต้องติดตั้งอะไรเลย)

### ขั้นตอน:

**1. สร้าง GitHub account**
- ไปที่ https://github.com → Sign up (ฟรี)

**2. สร้าง Repository ใหม่**
- กด `+` → `New repository`
- ตั้งชื่อ `CopyMRP`
- เลือก `Private`
- กด `Create repository`

**3. Upload ไฟล์**
- กด `uploading an existing file`
- ลาก folder ทั้งหมดใส่ (หรืออัพทีละไฟล์)
  - `.github/workflows/build.yml`
  - `CopyMRP/Form1.cs`
  - `CopyMRP/CopyMRP.csproj`
- กด `Commit changes`

**4. รอ Build อัตโนมัติ (~2 นาที)**
- ไปที่ tab `Actions`
- รอให้ `Build CopyMRP.exe` เป็น ✅ สีเขียว

**5. ดาวน์โหลด .exe**
- คลิกที่ workflow run ที่เสร็จแล้ว
- เลื่อนลงล่าง หัวข้อ `Artifacts`
- กด `CopyMRP-exe` → ดาวน์โหลด zip
- แตก zip → ได้ `CopyMRP.exe` ใช้ได้เลย!

---

## ฟีเจอร์โปรแกรม

- เพิ่ม BU folder ได้ไม่จำกัด
- เปลี่ยน/ลบ BU แต่ละตัวได้
- ตัวกรองชื่อไฟล์ (default `_AUFTRAG`)
- Scan subfolder ลึก 4 ชั้น
- ชื่อซ้ำ → ต่อท้าย `_BU1`, `_BU2` อัตโนมัติ
- Log realtime + progress bar
- รองรับ Network Path `\\server\folder`
