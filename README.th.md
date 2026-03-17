<p align="center">
  <img src="assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

<p align="center">
  <strong>🌐 Language / Język / Limbă / Dil / ภาษา / Idioma / Lingua / Idioma:</strong><br>
  <a href="README.md">🇬🇧 English</a> •
  <a href="README.pl.md">🇵🇱 Polski</a> •
  <a href="README.ro.md">🇷🇴 Română</a> •
  <a href="README.tr.md">🇹🇷 Türkçe</a> •
  <a href="README.th.md">🇹🇭 ไทย</a> •
  <a href="README.pt.md">🇧🇷 Português</a> •
  <a href="README.it.md">🇮🇹 Italiano</a> •
  <a href="README.es.md">🇪🇸 Español</a>
</p>

# 42pak-generator

ตัวจัดการไฟล์ pak แบบโอเพนซอร์สสมัยใหม่สำหรับชุมชนเซิร์ฟเวอร์ส่วนตัว Metin2 แทนที่รูปแบบไฟล์เก็บถาวร EIX/EPK แบบเดิมด้วยรูปแบบ **VPK** ใหม่ที่มีการเข้ารหัส AES-256-GCM, การบีบอัด LZ4/Zstandard/Brotli, การแฮช BLAKE3 และการตรวจจับการดัดแปลง HMAC-SHA256

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

> **ภาพหน้าจอ:** ดู [PREVIEW.md](PREVIEW.md) สำหรับแกลเลอรีภาพ GUI ครบถ้วนทั้งธีมมืดและธีมสว่าง

---

## คุณสมบัติ

- **สร้างไฟล์เก็บถาวร VPK** — แพ็คไดเรกทอรีเป็นไฟล์ `.vpk` เดียวพร้อมการเข้ารหัสและการบีบอัดเสริม
- **จัดการไฟล์เก็บถาวรที่มีอยู่** — เรียกดู ค้นหา แตกไฟล์ และตรวจสอบไฟล์เก็บถาวร VPK
- **แปลง EIX/EPK เป็น VPK** — ย้ายจากรูปแบบ EterPack เดิมด้วยคลิกเดียว (รองรับรุ่น 40250, FliegeV3 และ MartySama 5.8)
- **การเข้ารหัส AES-256-GCM** — การเข้ารหัสแบบยืนยันตัวตนต่อไฟล์ด้วย nonce เฉพาะ
- **การบีบอัด LZ4 / Zstandard / Brotli** — เลือกอัลกอริทึมที่เหมาะกับความต้องการ
- **การแฮชเนื้อหา BLAKE3** — การตรวจสอบความสมบูรณ์เชิงการเข้ารหัสสำหรับทุกไฟล์
- **HMAC-SHA256** — การตรวจจับการดัดแปลงระดับไฟล์เก็บถาวร
- **การปิดบังชื่อไฟล์** — ตัวเลือกซ่อนเส้นทางไฟล์ภายในไฟล์เก็บถาวร
- **CLI เต็มรูปแบบ** — เครื่องมือ `42pak-cli` แยกส่วนพร้อมคำสั่ง pack, unpack, list, info, verify, diff, migrate, search, check-duplicates และ watch
- **การรวม C++ กับ Metin2** — โค้ดโหลดเดอร์สำเร็จรูปสำหรับไคลเอนต์และเซิร์ฟเวอร์สำหรับซอร์สทรี 40250, FliegeV3 และ MartySama 5.8

## เปรียบเทียบ VPK กับ EIX/EPK

| คุณลักษณะ | EIX/EPK (เดิม) | VPK (42pak) |
|------------|:-:|:-:|
| การเข้ารหัส | TEA / Panama / HybridCrypt | AES-256-GCM |
| การบีบอัด | LZO | LZ4 / Zstandard / Brotli |
| ความสมบูรณ์ | CRC32 | BLAKE3 + HMAC-SHA256 |
| รูปแบบไฟล์ | ไฟล์คู่ (.eix + .epk) | ไฟล์เดียว (.vpk) |
| จำนวนรายการ | สูงสุด 512 | ไม่จำกัด |
| ความยาวชื่อไฟล์ | 160 ไบต์ | 512 ไบต์ (UTF-8) |
| การสร้างคีย์ | คีย์ฮาร์ดโค้ด | PBKDF2-SHA512 (200k รอบ) |
| การตรวจจับการดัดแปลง | ไม่มี | HMAC-SHA256 ทั้งไฟล์เก็บถาวร |

---

## เริ่มต้นใช้งาน

### ข้อกำหนดเบื้องต้น

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 เวอร์ชัน 1809+ (สำหรับ WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (ปกติติดตั้งมาแล้ว)

### คอมไพล์

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### เรียกใช้ GUI

```bash
dotnet run --project src/FortyTwoPak.UI
```

### เรียกใช้ CLI

```bash
dotnet run --project src/FortyTwoPak.CLI -- <คำสั่ง> [ตัวเลือก]
```

### เรียกใช้ทดสอบ

```bash
dotnet test
```

### เผยแพร่ (แบบพกพา / ตัวติดตั้ง)

```powershell
# CLI ไฟล์ exe เดียว (~65 MB)
.\publish.ps1 -Target CLI

# GUI โฟลเดอร์แบบพกพา (~163 MB)
.\publish.ps1 -Target GUI

# ทั้งสอง + ตัวติดตั้ง Inno Setup
.\publish.ps1 -Target All
```

---

## การใช้งาน CLI

เครื่องมือ CLI แยกส่วน (`42pak-cli`) รองรับทุกการทำงาน:

```
42pak-cli pack <ไดเรกทอรีต้นทาง> [--output <ไฟล์>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <รหัสผ่าน>] [--threads <N>] [--dry-run]
42pak-cli unpack <ไฟล์เก็บถาวร> <ไดเรกทอรีปลายทาง> [--passphrase <รหัสผ่าน>] [--filter <รูปแบบ>]
42pak-cli list <ไฟล์เก็บถาวร> [--passphrase <รหัสผ่าน>] [--filter <รูปแบบ>] [--json]
42pak-cli info <ไฟล์เก็บถาวร> [--passphrase <รหัสผ่าน>] [--json]
42pak-cli verify <ไฟล์เก็บถาวร> [--passphrase <รหัสผ่าน>] [--filter <รูปแบบ>] [--json]
42pak-cli diff <ไฟล์เก็บถาวร_A> <ไฟล์เก็บถาวร_B> [--passphrase <รหัสผ่าน>] [--json]
42pak-cli migrate <ไฟล์เก็บถาวรเดิม> [--output <ไฟล์>] [--compression <ประเภท>] [--passphrase <รหัสผ่าน>]
42pak-cli search <ไดเรกทอรีทำงาน> <ชื่อไฟล์หรือรูปแบบ>
42pak-cli check-duplicates <ไดเรกทอรีทำงาน> [--read-index]
42pak-cli watch <ไดเรกทอรีต้นทาง> [--output <ไฟล์>] [--debounce <MS>]
```

**แฟล็ก:** `-q` / `--quiet` ระงับเอาต์พุตทั้งหมดยกเว้นข้อผิดพลาด `--json` ส่งออก JSON ที่มีโครงสร้างสำหรับไปป์ไลน์สคริปต์

**รหัสออก:** 0 = สำเร็จ, 1 = ข้อผิดพลาด, 2 = ความล้มเหลวด้านความสมบูรณ์, 3 = รหัสผ่านไม่ถูกต้อง

---

## โครงสร้างโปรเจกต์

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # ไลบรารีหลัก: อ่าน/เขียน VPK, คริปโต, การบีบอัด, นำเข้ารูปแบบเดิม
│   │   ├── VpkFormat/               # เฮดเดอร์ รายการ คลาสไฟล์เก็บถาวร VPK
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # ตัวบีบอัด LZ4 / Zstandard / Brotli
│   │   ├── Legacy/                  # ตัวอ่านและตัวแปลง EIX/EPK (40250 + FliegeV3 + MartySama 5.8)
│   │   ├── Cli/                     # ตัวจัดการ CLI ที่ใช้ร่วม (12 คำสั่ง)
│   │   └── Utils/                   # การปิดบังชื่อไฟล์ การรายงานความคืบหน้า
│   ├── FortyTwoPak.CLI/             # เครื่องมือ CLI แยกส่วน (42pak-cli)
│   ├── FortyTwoPak.UI/              # แอปพลิเคชันเดสก์ท็อป WebView2
│   │   ├── MainWindow.cs            # โฮสต์ WinForms พร้อมคอนโทรล WebView2
│   │   ├── BridgeService.cs         # สะพาน JavaScript <-> C#
│   │   └── wwwroot/                 # ส่วนหน้า HTML/CSS/JS (6 แท็บ, ธีมมืด/สว่าง)
│   └── FortyTwoPak.Tests/           # ชุดทดสอบ xUnit (22 ทดสอบ)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # การรวม C++ สำหรับ 40250/ClientVS22 (HybridCrypt)
│   │   ├── MartySama58/             # การรวม C++ สำหรับ MartySama 5.8 (Boost + HybridCrypt)
│   │   └── FliegeV3/                # การรวม C++ สำหรับ FliegeV3 (XTEA/LZ4)
│   └── Server/                      # ตัวจัดการ VPK ฝั่งเซิร์ฟเวอร์ที่ใช้ร่วม
├── docs/
│   └── FORMAT_SPEC.md               # ข้อกำหนดรูปแบบไบนารี VPK
├── publish.ps1                      # สคริปต์เผยแพร่ (CLI/GUI/ตัวติดตั้ง/ทั้งหมด)
├── installer.iss                    # สคริปต์ตัวติดตั้ง Inno Setup 6
├── assets/                          # ภาพหน้าจอและรูปแบนเนอร์
└── build/                           # เอาต์พุตการคอมไพล์
```

---

## รูปแบบไฟล์ VPK

ไฟล์เก็บถาวรเดียวที่มีโครงสร้างไบนารีดังนี้:

```
+-------------------------------------+
| VpkHeader (512 ไบต์, คงที่)         |  Magic "42PK", เวอร์ชัน, จำนวนรายการ,
|                                     |  แฟล็กเข้ารหัส, ซอลต์, ผู้เขียน ฯลฯ
+-------------------------------------+
| บล็อกข้อมูล 0 (จัดเรียง 4096)       |  เนื้อหาไฟล์ (บีบอัด + เข้ารหัส)
+-------------------------------------+
| บล็อกข้อมูล 1 (จัดเรียง 4096)       |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| ตารางรายการ (ขนาดไม่คงที่)          |  อาร์เรย์ของเรคคอร์ด VpkEntry หากเข้ารหัส
|                                     |  จะห่อด้วย AES-GCM (nonce + tag + ข้อมูล)
+-------------------------------------+
| HMAC-SHA256 (32 ไบต์)               |  ครอบคลุมทุกส่วนด้านบน ศูนย์หากไม่ลงนาม
+-------------------------------------+
```

### ไปป์ไลน์การเข้ารหัส

สำหรับแต่ละไฟล์: `ต้นฉบับ -> บีบอัด LZ4 -> เข้ารหัส AES-256-GCM -> จัดเก็บ`

เมื่อแตกไฟล์: `อ่าน -> ถอดรหัส AES-256-GCM -> คลายการบีบอัด LZ4 -> ตรวจสอบ BLAKE3`

การสร้างคีย์: `PBKDF2-SHA512("42PK-v1:" + รหัสผ่าน, ซอลต์, 100000 รอบ) -> 64 ไบต์`
- 32 ไบต์แรก: คีย์ AES-256
- 32 ไบต์สุดท้าย: คีย์ HMAC-SHA256

สำหรับข้อกำหนดไบนารีฉบับเต็ม ดู [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md)

---

## การใช้งาน

### สร้างไฟล์เก็บถาวร VPK (GUI)

1. เปิด 42pak-generator
2. ไปที่แท็บ **Create Pak**
3. เลือกไดเรกทอรีต้นทางที่มีไฟล์ที่จะแพ็ค
4. เลือกเส้นทางไฟล์ `.vpk` ปลายทาง
5. กำหนดค่าตัวเลือก:
   - **การเข้ารหัส**: เปิดใช้งานและป้อนรหัสผ่าน
   - **ระดับการบีบอัด**: 0 (ไม่มี) ถึง 12 (สูงสุด)
   - **การปิดบังชื่อไฟล์**: ซ่อนเส้นทางภายในไฟล์เก็บถาวร
   - **ผู้เขียน / ความเห็น**: ข้อมูลเมตาเสริม
6. คลิก **Build**

### แปลง EIX/EPK เป็น VPK

1. ไปที่แท็บ **Convert**
2. เลือกไฟล์ `.eix` (ไฟล์ `.epk` ต้องอยู่ในไดเรกทอรีเดียวกัน)
3. เลือกเส้นทาง `.vpk` ปลายทาง
4. เลือกตั้งรหัสผ่านการเข้ารหัส (ไม่บังคับ)
5. คลิก **Convert**

### การรวมกับไคลเอนต์ Metin2

มีโปรไฟล์การรวมสามแบบสำหรับซอร์สทรีที่แตกต่างกัน:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **MartySama 5.8** (Boost, HybridCrypt, LZO): [Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md](Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### การรวมกับเซิร์ฟเวอร์ Metin2

ดู [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) สำหรับการอ่าน VPK ฝั่งเซิร์ฟเวอร์

---

## เทคโนโลยี

| ส่วนประกอบ | เทคโนโลยี |
|------------|------------|
| รันไทม์ | .NET 8.0 (C#) |
| UI | WebView2 (โฮสต์ WinForms) |
| ส่วนหน้า | HTML5, CSS3, Vanilla JavaScript |
| การเข้ารหัส | AES-256-GCM ผ่าน `System.Security.Cryptography` |
| การสร้างคีย์ | PBKDF2-SHA512 (200,000 รอบ) |
| การแฮช | BLAKE3 ผ่าน [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| การบีบอัด | LZ4 ผ่าน [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard ผ่าน [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (ในตัว) |
| การตรวจจับการดัดแปลง | HMAC-SHA256 |
| C++ คริปโต | OpenSSL 1.1+ |
| การทดสอบ | xUnit |

---

## สัญญาอนุญาต

โปรเจกต์นี้อยู่ภายใต้สัญญาอนุญาต MIT — ดูรายละเอียดในไฟล์ [LICENSE](LICENSE)

## การมีส่วนร่วม

ยินดีต้อนรับการมีส่วนร่วม! ดูแนวทางใน [CONTRIBUTING.md](CONTRIBUTING.md)

## กิตติกรรมประกาศ

- ชุมชนเซิร์ฟเวอร์ส่วนตัว Metin2 ที่ช่วยให้เกมยังคงมีชีวิตอยู่
- รูปแบบ EterPack ดั้งเดิมจาก YMIR Entertainment ที่เป็นรากฐาน
- [ทีม BLAKE3](https://github.com/BLAKE3-team/BLAKE3) สำหรับฟังก์ชันแฮชที่รวดเร็ว
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) สำหรับ binding .NET LZ4
