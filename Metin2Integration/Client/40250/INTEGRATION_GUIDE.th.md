<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - คู่มือการรวมระบบไคลเอนต์ (40250 / ClientVS22)

> **โปรไฟล์:** 40250 - มุ่งเป้าไปที่สถาปัตยกรรม HybridCrypt แบบหลายรหัสลับ
> สำหรับการรวมระบบ FliegeV3 (XTEA/LZ4) ดูที่ `../FliegeV3/INTEGRATION_GUIDE.md`

ทดแทนระบบ EterPack (EIX/EPK) โดยตรง อ้างอิงจากซอร์สโค้ดจริงของไคลเอนต์ 40250
เส้นทางไฟล์ทั้งหมดอ้างอิงถึงแผนผังซอร์สจริง

## สิ่งที่คุณได้รับ

| คุณสมบัติ | EterPack (เดิม) | VPK (ใหม่) |
|----------|----------------|------------|
| การเข้ารหัส | TEA / Panama / HybridCrypt | AES-256-GCM (เร่งด้วยฮาร์ดแวร์) |
| การบีบอัด | LZO | LZ4 / Zstandard / Brotli |
| ความสมบูรณ์ | CRC32 ต่อไฟล์ | BLAKE3 ต่อไฟล์ + HMAC-SHA256 อาร์ไคฟ์ |
| รูปแบบไฟล์ | คู่ .eix + .epk | ไฟล์ .vpk เดียว |
| การจัดการคีย์ | Panama IV จากเซิร์ฟเวอร์ + HybridCrypt SDB | รหัสผ่าน PBKDF2-SHA512 |

## สถาปัตยกรรม

VPK รวมระบบผ่าน `CEterFileDict` - การค้นหาแฮชเดียวกันที่ EterPack ดั้งเดิมใช้
เมื่อ `CEterPackManager::RegisterPackAuto()` พบไฟล์ `.vpk` จะสร้าง `CVpkPack`
แทน `CEterPack` `CVpkPack` ลงทะเบียนรายการของตนใน `m_FileDict` ที่ใช้ร่วมกัน
พร้อมเครื่องหมาย sentinel เมื่อ `GetFromPack()` แก้ไขชื่อไฟล์ จะตรวจสอบเครื่องหมาย
และส่งต่อไปยัง `CEterPack::Get2()` หรือ `CVpkPack::Get2()` อย่างโปร่งใส

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## ไฟล์

### ไฟล์ใหม่ที่ต้องเพิ่มใน `source/EterPack/`

| ไฟล์ | วัตถุประสงค์ |
|------|-------------|
| `VpkLoader.h` | คลาส `CVpkPack` - ทดแทน `CEterPack` โดยตรง |
| `VpkLoader.cpp` | การใช้งานเต็มรูปแบบ: การแยกวิเคราะห์เฮดเดอร์, ตารางรายการ, ถอดรหัส+คลายการบีบอัด, ตรวจสอบ BLAKE3 |
| `VpkCrypto.h` | เครื่องมือเข้ารหัส: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | การใช้งานด้วย OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | เฮดเดอร์ `CEterPackManager` ที่แก้ไขแล้วพร้อมรองรับ VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` ที่แก้ไขแล้วพร้อม `RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase` |

### ไฟล์ที่ต้องแก้ไข

| ไฟล์ | การเปลี่ยนแปลง |
|------|---------------|
| `source/EterPack/EterPackManager.h` | แทนที่ด้วย `EterPackManager_Vpk.h` (หรือรวมการเพิ่มเติม) |
| `source/EterPack/EterPackManager.cpp` | แทนที่ด้วย `EterPackManager_Vpk.cpp` (หรือรวมการเพิ่มเติม) |
| `source/UserInterface/UserInterface.cpp` | เปลี่ยน 2 บรรทัด (ดูด้านล่าง) |

## ไลบรารีที่ต้องการ

### OpenSSL 1.1+
- Windows: ดาวน์โหลดจาก https://slproweb.com/products/Win32OpenSSL.html
- เฮดเดอร์: `<openssl/evp.h>`, `<openssl/hmac.h>`
- ลิงก์: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- เฮดเดอร์: `<lz4.h>`
- ลิงก์: `lz4.lib`

### Zstandard
- https://github.com/facebook/zstd/releases
- เฮดเดอร์: `<zstd.h>`
- ลิงก์: `zstd.lib` (หรือ `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- เฮดเดอร์: `<brotli/decode.h>`
- ลิงก์: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- คัดลอกเข้าโปรเจกต์: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: เพิ่ม `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c` ด้วย

## การรวมระบบทีละขั้นตอน

### ขั้นตอนที่ 1: เพิ่มไฟล์ในโปรเจกต์ EterPack ใน VS

1. คัดลอก `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` ไปยัง `source/EterPack/`
2. คัดลอกไฟล์ C ของ BLAKE3 ไปยัง `source/EterPack/` (หรือตำแหน่งที่ใช้ร่วมกัน)
3. เพิ่มไฟล์ใหม่ทั้งหมดในโปรเจกต์ EterPack ใน Visual Studio
4. เพิ่มเส้นทาง include สำหรับ OpenSSL, LZ4, Zstd, Brotli ใน **Additional Include Directories**
5. เพิ่มเส้นทางไลบรารีใน **Additional Library Directories**
6. เพิ่ม `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` ใน **Additional Dependencies**

### ขั้นตอนที่ 2: แทนที่ EterPackManager

**ตัวเลือก A (แทนที่สะอาด):**
- แทนที่ `source/EterPack/EterPackManager.h` ด้วย `EterPackManager_Vpk.h`
- แทนที่ `source/EterPack/EterPackManager.cpp` ด้วย `EterPackManager_Vpk.cpp`
- เปลี่ยนชื่อทั้งสองเป็น `EterPackManager.h` / `EterPackManager.cpp`

**ตัวเลือก B (รวม):**
เพิ่มสิ่งเหล่านี้ใน `EterPackManager.h` ที่มีอยู่:

```cpp
#include "VpkLoader.h"

// ภายในการประกาศคลาส เพิ่มใน public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// ภายในการประกาศคลาส เพิ่มใน protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

จากนั้นรวมการใช้งานจาก `EterPackManager_Vpk.cpp` เข้ากับไฟล์ `.cpp` ที่มีอยู่

### ขั้นตอนที่ 3: แก้ไข UserInterface.cpp

ใน `source/UserInterface/UserInterface.cpp` ลูปลงทะเบียนแพ็ค (ประมาณบรรทัด 220):

**ก่อน:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**หลัง:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

และเพิ่มบรรทัดนี้ **ก่อน** ลูปลงทะเบียน:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

เท่านี้เอง การเปลี่ยนแปลงทั้งหมดสองจุด:
1. `SetVpkPassphrase()` ก่อนลูป
2. `RegisterPack()` → `RegisterPackAuto()` (2 ตำแหน่ง)

### ขั้นตอนที่ 4: คอมไพล์

คอมไพล์โปรเจกต์ EterPack ก่อน จากนั้นคอมไพล์โซลูชันทั้งหมด โค้ด VPK คอมไพล์
ควบคู่กับโค้ด EterPack ที่มีอยู่ - ไม่มีอะไรถูกลบ

## วิธีการทำงาน

### ขั้นตอนการลงทะเบียน

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     เก็บ m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") มีอยู่?
                 "xyz/")              ├─ ใช่ → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() สำหรับแต่ละไฟล์
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ ไม่ → RegisterPack() [ขั้นตอน EPK ดั้งเดิม]
```

### ขั้นตอนการเข้าถึงไฟล์

```
โค้ดใดๆ เรียก:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → "d:/ymir work/item/weapon.gr2"
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ ถ้า compressed_type == -1 (VPK sentinel):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ ถอดรหัส AES-GCM (ถ้าเข้ารหัส)
  │       │    ├─ คลายการบีบอัด LZ4/Zstd/Brotli (ถ้าบีบอัด)
  │       │    └─ ตรวจสอบแฮช BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │            └─ CMappedFile เป็นเจ้าของหน่วยความจำ (ถูกปล่อยใน destructor)
  │
  └─ มิฉะนั้น (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [การคลายการบีบอัดดั้งเดิม: LZO / Panama / HybridCrypt]
```

### การจัดการหน่วยความจำ

CVpkPack ใช้ `CMappedFile::AppendDataBlock()` - กลไกเดียวกันที่ CEterPack ใช้
สำหรับข้อมูล HybridCrypt ข้อมูลที่คลายการบีบอัดจะถูกคัดลอกไปยังบัฟเฟอร์ที่
CMappedFile เป็นเจ้าของ ซึ่งจะถูกปล่อยอัตโนมัติเมื่อ CMappedFile ออกจากขอบเขต
ไม่จำเป็นต้องทำความสะอาดด้วยตนเอง

## การแปลงแพ็ค

ใช้เครื่องมือ 42pak-generator เพื่อแปลงไฟล์แพ็คที่มีอยู่:

```bash
# แปลงคู่ EPK เดียวเป็น VPK
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "รหัสลับของคุณ"

# สร้าง VPK จากโฟลเดอร์
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "รหัสลับของคุณ" --algorithm Zstandard --compression 6

# แปลง EIX/EPK ทั้งหมดในไดเรกทอรีเป็นชุด
42pak-cli migrate ./pack/ ./vpk/ --passphrase "รหัสลับของคุณ" --format Standard

# เฝ้าดูไดเรกทอรีและสร้างใหม่อัตโนมัติ
42pak-cli watch ./gamedata --output game.vpk --passphrase "รหัสลับของคุณ"
```

หรือใช้มุมมอง Create ใน GUI เพื่อสร้างอาร์ไคฟ์ VPK จากโฟลเดอร์

## กลยุทธ์การย้ายระบบ

1. **ตั้งค่าไลบรารี** - เพิ่ม OpenSSL, LZ4, Zstd, Brotli, BLAKE3 ในโปรเจกต์
2. **เพิ่มไฟล์ VPK** - คัดลอกไฟล์ซอร์สใหม่ 6 ไฟล์ไปยัง `source/EterPack/`
3. **แก้ไข EterPackManager** - รวมหรือแทนที่เฮดเดอร์และการใช้งาน
4. **แก้ไข UserInterface.cpp** - เปลี่ยน 2 บรรทัด
5. **คอมไพล์** - ตรวจสอบว่าทุกอย่างคอมไพล์ได้
6. **แปลงแพ็คหนึ่งอัน** - เช่น `metin2_patch_etc` -> ทดสอบว่าโหลดถูกต้อง
7. **แปลงแพ็คที่เหลือ** - ทีละอันหรือทั้งหมดพร้อมกัน
8. **ลบไฟล์ EPK เก่า** - หลังจากแปลงแพ็คทั้งหมดแล้ว

เนื่องจาก `RegisterPackAuto` จะกลับไปใช้ EPK เมื่อไม่มี VPK คุณสามารถ
แปลงแพ็คทีละน้อยโดยไม่ทำให้อะไรเสียหาย

## การกำหนดค่ารหัสผ่าน

| วิธีการ | เมื่อใดที่ควรใช้ |
|--------|-----------------|
| ฝังในซอร์สโค้ด | เซิร์ฟเวอร์ส่วนตัว วิธีที่ง่ายที่สุด |
| ไฟล์กำหนดค่า (`metin2.cfg`) | เปลี่ยนได้ง่ายโดยไม่ต้องคอมไพล์ใหม่ |
| ส่งจากเซิร์ฟเวอร์เมื่อล็อกอิน | ความปลอดภัยสูงสุด - รหัสผ่านเปลี่ยนทุกเซสชัน |

สำหรับรหัสผ่านที่ส่งจากเซิร์ฟเวอร์ ให้แก้ไข `CAccountConnector` เพื่อรับรหัสผ่าน
ในคำตอบการยืนยันตัวตน และเรียก `CEterPackManager::Instance().SetVpkPassphrase()`
ก่อนการเข้าถึงแพ็คใดๆ

## การแก้ไขปัญหา

| อาการ | สาเหตุ | การแก้ไข |
|-------|--------|---------|
| ไม่พบไฟล์แพ็ค | ไม่มีนามสกุล `.vpk` | ตรวจสอบให้แน่ใจว่าชื่อแพ็คไม่มีนามสกุล - `RegisterPackAuto` เพิ่ม `.vpk` |
| "HMAC verification failed" | รหัสผ่านผิด | ตรวจสอบว่า `SetVpkPassphrase` ถูกเรียกก่อน `RegisterPackAuto` |
| ไม่พบไฟล์ใน VPK | ความไม่ตรงกันของตัวพิมพ์ | VPK ทำให้เป็นตัวพิมพ์เล็กด้วยตัวคั่น `/` |
| Crash ใน `Get2()` | การชนกันของ sentinel `compressed_type` | ตรวจสอบให้แน่ใจว่าไม่มีไฟล์ EPK ใช้ `compressed_type == -1` (ไม่มีใน Metin2 มาตรฐาน) |
| ข้อผิดพลาดการลิงก์ LZ4/Zstd/Brotli | ไลบรารีหายไป | เพิ่มไลบรารีคลายการบีบอัดใน Additional Dependencies |
| ข้อผิดพลาดการคอมไพล์ BLAKE3 | ไฟล์ซอร์สหายไป | ตรวจสอบให้แน่ใจว่าไฟล์ `blake3_*.c` ทั้งหมดอยู่ในโปรเจกต์ |
