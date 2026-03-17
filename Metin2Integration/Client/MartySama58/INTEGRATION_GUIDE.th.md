<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - คู่มือการรวมไคลเอนต์ (MartySama 5.8)

> **โปรไฟล์:** MartySama 5.8 - มุ่งเป้าไปที่สถาปัตยกรรมเข้ารหัสหลายชั้น HybridCrypt บน Boost
> สำหรับการรวม 40250/ClientVS22 ดูที่ `../40250/INTEGRATION_GUIDE.md`
> สำหรับการรวม FliegeV3 ดูที่ `../FliegeV3/INTEGRATION_GUIDE.md`

ตัวแทนที่แบบ drop-in สำหรับระบบ EterPack (EIX/EPK) อ้างอิงจาก
ซอร์สโค้ดจริงของไคลเอนต์ MartySama 5.8 (`Binary & S-Source/Binary/Client/EterPack/`)
เส้นทางไฟล์ทั้งหมดอ้างอิงจากโครงสร้างซอร์สจริงของ MartySama

## สิ่งที่คุณได้รับ

| ฟีเจอร์ | EterPack (เก่า) | VPK (ใหม่) |
|---------|---------------|-----------|
| การเข้ารหัส | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (เร่งด้วยฮาร์ดแวร์) |
| การบีบอัด | LZO | LZ4 / Zstandard / Brotli |
| ความสมบูรณ์ | CRC32 ต่อไฟล์ | BLAKE3 ต่อไฟล์ + HMAC-SHA256 อาร์ไคว์ |
| รูปแบบไฟล์ | คู่ .eix + .epk | ไฟล์เดียว .vpk |
| การจัดการคีย์ | Panama IV ส่งจากเซิร์ฟเวอร์ + HybridCrypt SDB | รหัสผ่าน PBKDF2-SHA512 |

## MartySama 5.8 vs 40250 vs FliegeV3

| ด้าน | 40250 | MartySama 5.8 | FliegeV3 |
|------|-------|---------------|----------|
| ระบบเข้ารหัส | Camellia/Twofish/XTEA (HybridCrypt) | เหมือน 40250 | XTEA เท่านั้น |
| การส่งมอบคีย์ | Panama IV ส่งจากเซิร์ฟเวอร์ + SDB | เหมือน 40250 | คีย์แบบคงที่ที่คอมไพล์ |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| ประเภทคอนเทนเนอร์ | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| สไตล์เฮดเดอร์ | Include guards | `#pragma once` | Include guards |
| การป้องกันการดัดแปลง | ไม่มี | `#ifdef __THEMIDA__` VM_START/VM_END | ไม่มี |
| เมธอดของ manager | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | เหมือน 40250 | ไม่มี |
| การบีบอัด | LZO | LZO | LZ4 |
| โครงสร้างดัชนี | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` |

โค้ดการรวม VPK ปรับให้เข้ากับคอนเทนเนอร์ Boost ของ MartySama และ
มาร์กเกอร์ Themida โดยรักษาอินเทอร์เฟซ API เดิม (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`)

## สถาปัตยกรรม

VPK รวมเข้าผ่าน `CEterFileDict` - การค้นหาแฮช `boost::unordered_multimap`
เดียวกับที่ EterPack ดั้งเดิมของ MartySama ใช้ เมื่อ
`CEterPackManager::RegisterPackAuto()` พบไฟล์ `.vpk` จะสร้าง
`CVpkPack` แทน `CEterPack` โดย `CVpkPack` ลงทะเบียนรายการของตนใน
`m_FileDict` ที่ใช้ร่วมกันพร้อมมาร์กเกอร์ sentinel เมื่อ `GetFromPack()`
แก้ไขชื่อไฟล์ จะตรวจสอบมาร์กเกอร์และส่งต่อไปยัง `CEterPack::Get2()`
หรือ `CVpkPack::Get2()` อย่างโปร่งใส

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## ไฟล์

### ไฟล์ใหม่ที่ต้องเพิ่มใน `Client/EterPack/`

| ไฟล์ | วัตถุประสงค์ |
|------|------------|
| `VpkLoader.h` | คลาส `CVpkPack` - ตัวแทนที่แบบ drop-in สำหรับ `CEterPack` |
| `VpkLoader.cpp` | การนำไปใช้เต็มรูปแบบ: การแยกวิเคราะห์เฮดเดอร์, ตารางรายการ, ถอดรหัส+คลายการบีบอัด, การตรวจสอบ BLAKE3 |
| `VpkCrypto.h` | ยูทิลิตี้เข้ารหัส: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | การนำไปใช้ด้วย OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | เฮดเดอร์ `CEterPackManager` ที่แก้ไขพร้อม VPK + HybridCrypt + Boost |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` ที่แก้ไขพร้อม dispatch VPK, HybridCrypt, ตัววนซ้ำ Boost, Themida |

### ไฟล์ที่ต้องแก้ไข

| ไฟล์ | การเปลี่ยนแปลง |
|------|---------------|
| `Client/EterPack/EterPackManager.h` | แทนที่ด้วย `EterPackManager_Vpk.h` (หรือรวม) |
| `Client/EterPack/EterPackManager.cpp` | แทนที่ด้วย `EterPackManager_Vpk.cpp` (หรือรวม) |
| `Client/UserInterface/UserInterface.cpp` | เปลี่ยน 2 บรรทัด (ดูด้านล่าง) |

## ไลบรารีที่จำเป็น

### OpenSSL 1.1+
- Windows: ดาวน์โหลดจาก https://slproweb.com/products/Win32OpenSSL.html
- เฮดเดอร์: `<openssl/evp.h>`, `<openssl/hmac.h>`
- ลิงก์: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- เฮดเดอร์: `<lz4.h>`
- ลิงก์: `lz4.lib`
- **หมายเหตุ:** MartySama 5.8 ไม่ได้รวม LZ4 โดยค่าเริ่มต้น นี่คือ dependency ใหม่

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
- คัดลอกไปยังโปรเจกต์: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: เพิ่มด้วย `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

### dependency ที่มีอยู่แล้ว (รวมมาแล้วใน MartySama 5.8)

MartySama 5.8 มีไลบรารีเหล่านี้อยู่แล้ว ซึ่ง **ไม่** จำเป็นสำหรับ VPK
แต่ยังคงอยู่ในโปรเจกต์สำหรับเส้นทางโค้ด EPK เดิม:

| ไลบรารี | ใช้โดย |
|---------|-------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), Panama cipher, ฟังก์ชันแฮช |
| Boost | `boost::unordered_map`/`boost::unordered_multimap` ใน EterPack, EterPackManager, CEterFileDict |
| LZO | การคลายการบีบอัด EPK (ประเภท pack 1, 2) |

สิ่งเหล่านี้ยังคงทำงานร่วมกับ VPK ได้ ไม่ต้องเปลี่ยนแปลงอะไร

## การรวมทีละขั้นตอน

### ขั้นตอนที่ 1: เพิ่มไฟล์ในโปรเจกต์ EterPack ใน VS

1. คัดลอก `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` ไปยัง `Client/EterPack/`
2. คัดลอกไฟล์ C ของ BLAKE3 ไปยัง `Client/EterPack/` (หรือตำแหน่งที่ใช้ร่วม)
3. เพิ่มไฟล์ใหม่ทั้งหมดในโปรเจกต์ EterPack ใน Visual Studio
4. เพิ่มเส้นทาง include สำหรับ OpenSSL, LZ4, Zstd, Brotli ใน **Additional Include Directories**
5. เพิ่มเส้นทางไลบรารีใน **Additional Library Directories**
6. เพิ่ม `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` ใน **Additional Dependencies**

### ขั้นตอนที่ 2: แทนที่ EterPackManager

**ตัวเลือก A (แทนที่แบบสะอาด):**
- แทนที่ `Client/EterPack/EterPackManager.h` ด้วย `EterPackManager_Vpk.h`
- แทนที่ `Client/EterPack/EterPackManager.cpp` ด้วย `EterPackManager_Vpk.cpp`
- เปลี่ยนชื่อทั้งสองเป็น `EterPackManager.h` / `EterPackManager.cpp`

**ตัวเลือก B (รวมเข้า):**
เพิ่มสิ่งนี้ใน `EterPackManager.h` ที่มีอยู่:

```cpp
#include "VpkLoader.h"

// Inside the class declaration, add to public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Inside the class declaration, add to protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef boost::unordered_map<std::string, CVpkPack*> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

**สำคัญ:** สังเกตการใช้ `boost::unordered_map` แทน `std::unordered_map`
เพื่อให้ตรงกับธรรมเนียมของ MartySama `EterPackManager_Vpk.h` ที่ให้มา
ใช้คอนเทนเนอร์ Boost ตลอดทั้งโค้ดแล้ว

จากนั้นรวมการนำไปใช้จาก `EterPackManager_Vpk.cpp` เข้ากับไฟล์ `.cpp` ที่มีอยู่

### ขั้นตอนที่ 3: แก้ไข UserInterface.cpp

ใน `Client/UserInterface/UserInterface.cpp` ลูปการลงทะเบียน pack
(ประมาณบรรทัด 557–579):

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

และเพิ่มบรรทัดนี้ **ก่อน** ลูปการลงทะเบียน:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

เท่านั้น สองการเปลี่ยนแปลงทั้งหมด:
1. `SetVpkPassphrase()` ก่อนลูป
2. `RegisterPack()` → `RegisterPackAuto()` (2 ตำแหน่ง)

ดู `UserInterface_VpkPatch.cpp` สำหรับ patch ฉบับเต็มพร้อมคำอธิบายก่อน/หลัง

### ขั้นตอนที่ 4: คอมไพล์

คอมไพล์โปรเจกต์ EterPack ก่อน จากนั้นคอมไพล์ solution ทั้งหมด โค้ด VPK คอมไพล์
ร่วมกับโค้ด EterPack ที่มีอยู่ - ไม่มีอะไรถูกลบ

**หมายเหตุการคอมไพล์เฉพาะ MartySama 5.8:**
- MartySama ใช้ Boost ตลอดทั้งโปรเจกต์ `boost::unordered_multimap` ใน
  `CEterFileDict` เข้ากันได้อย่างสมบูรณ์กับ VPK - `InsertItem()` ทำงานเหมือนกัน
- ตรวจสอบให้แน่ใจว่า `StdAfx.h` ใน EterPack รวม `<boost/unordered_map.hpp>` (ควร
  มีอยู่แล้วถ้าโปรเจกต์ MartySama คอมไพล์ได้)
- CryptoPP อยู่ใน solution แล้วสำหรับ HybridCrypt - ไม่มีข้อขัดแย้งกับ OpenSSL
  ทั้งสองใช้เพื่อวัตถุประสงค์ที่แตกต่างกันโดยสิ้นเชิง (CryptoPP สำหรับ EPK เดิม, OpenSSL สำหรับ VPK)
- ถ้าใช้ Themida มาร์กเกอร์ `#ifdef __THEMIDA__` ใน `EterPackManager_Vpk.cpp`
  ถูกตั้งค่าไว้แล้ว กำหนด `__THEMIDA__` ในการตั้งค่าคอมไพล์ถ้าต้องการ
  ให้มาร์กเกอร์ VM_START/VM_END ทำงาน

## วิธีการทำงาน

### ขั้นตอนการลงทะเบียน

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     stores m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") exists?
                 "xyz/")              ├─ YES → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() for each file
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ NO → RegisterPack() [original EPK flow]
```

### ขั้นตอนการเข้าถึงไฟล์

```
Any code calls:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → lowercase, normalize separators
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ if compressed_type == -1 (VPK sentinel):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ AES-GCM decrypt (if encrypted)
  │       │    ├─ LZ4/Zstd/Brotli decompress (if compressed)
  │       │    └─ BLAKE3 hash verify
  │       └─ mappedFile.AppendDataBlock(data, size)
  │            └─ CMappedFile owns the memory (freed in destructor)
  │
  └─ else (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [original decompression: LZO / Panama / HybridCrypt]
```

### การอยู่ร่วมกับ HybridCrypt

MartySama 5.8 รับคีย์เข้ารหัสจากเซิร์ฟเวอร์เมื่อเข้าสู่ระบบ:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

เมธอดเหล่านี้วนซ้ำ `m_PackMap` (ซึ่งมีเฉพาะอ็อบเจกต์ `CEterPack*`)
แพ็ค VPK ถูกเก็บใน `m_VpkPackMap` แยกต่างหาก ดังนั้นการส่งมอบคีย์
HybridCrypt เป็นไปอย่างโปร่งใส - ทั้งสองระบบอยู่ร่วมกันได้
โดยไม่ต้องเปลี่ยนแปลงโค้ดเครือข่าย

### การจัดการหน่วยความจำ

CVpkPack ใช้ `CMappedFile::AppendDataBlock()` - กลไกเดียวกับที่
CEterPack ใช้สำหรับข้อมูล HybridCrypt ข้อมูลที่คลายการบีบอัดจะถูกคัดลอกลงใน
บัฟเฟอร์ที่เป็นเจ้าของโดย CMappedFile ซึ่งจะถูกปล่อยอัตโนมัติเมื่อ
CMappedFile ออกจากขอบเขต ไม่ต้องทำความสะอาดด้วยตนเอง

## การแปลง pack

ใช้เครื่องมือ 42pak-generator เพื่อแปลงไฟล์ pack MartySama ที่มีอยู่:

```bash
# Convert a single EPK pair to VPK
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "your-secret"

# Build a VPK from a folder
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "your-secret" --algorithm Zstandard --compression 6

# Batch-convert all EIX/EPK in a directory
42pak-cli migrate ./pack/ ./vpk/ --passphrase "your-secret" --format Standard

# Watch a directory for changes and auto-rebuild
42pak-cli watch ./gamedata --output game.vpk --passphrase "your-secret"
```

หรือใช้มุมมองสร้างของ GUI เพื่อสร้างอาร์ไคว์ VPK จากโฟลเดอร์

## กลยุทธ์การย้ายระบบ

1. **ตั้งค่าไลบรารี** - เพิ่ม OpenSSL, LZ4, Zstd, Brotli, BLAKE3 ในโปรเจกต์
2. **เพิ่มไฟล์ VPK** - คัดลอก 6 ไฟล์ซอร์สใหม่ไปยัง `Client/EterPack/`
3. **แก้ไข EterPackManager** - รวมหรือแทนที่เฮดเดอร์และการนำไปใช้
4. **แก้ไข UserInterface.cpp** - การเปลี่ยนแปลง 2 บรรทัด
5. **คอมไพล์** - ตรวจสอบว่าทุกอย่างคอมไพล์ได้
6. **แปลง pack หนึ่งตัว** - เช่น `metin2_patch_etc` → ทดสอบว่าโหลดได้ถูกต้อง
7. **แปลง pack ที่เหลือ** - ทีละตัวหรือทั้งหมดในครั้งเดียว
8. **ลบไฟล์ EPK เก่า** - เมื่อแปลง pack ทั้งหมดแล้ว

เนื่องจาก `RegisterPackAuto` ย้อนกลับไปใช้ EPK เมื่อไม่มี VPK คุณสามารถ
แปลง pack ทีละขั้นโดยไม่ทำให้อะไรเสียหาย

## การกำหนดค่ารหัสผ่าน

| วิธี | เมื่อใดที่ใช้ |
|------|-------------|
| ฝังในซอร์สโค้ด | เซิร์ฟเวอร์ส่วนตัว วิธีที่ง่ายที่สุด |
| ไฟล์กำหนดค่า (`metin2.cfg`) | เปลี่ยนได้ง่ายโดยไม่ต้องคอมไพล์ใหม่ |
| ส่งจากเซิร์ฟเวอร์เมื่อเข้าสู่ระบบ | ความปลอดภัยสูงสุด - รหัสผ่านเปลี่ยนต่อเซสชัน |

สำหรับรหัสผ่านที่ส่งจากเซิร์ฟเวอร์ ให้แก้ไข `CAccountConnector` เพื่อรับรหัสผ่าน
ในการตอบกลับการยืนยันตัวตน และเรียก `CEterPackManager::Instance().SetVpkPassphrase()`
ก่อนการเข้าถึง pack ใดๆ `AccountConnector.cpp` ของ MartySama จัดการ
แพ็คเก็ตกำหนดเองจากเซิร์ฟเวอร์อยู่แล้ว - เพิ่ม handler ใหม่ข้างๆ การเรียก
`RetrieveHybridCryptPackKeys` ที่มีอยู่

## การแก้ไขปัญหา

| อาการ | สาเหตุ | วิธีแก้ไข |
|-------|--------|----------|
| ไม่พบไฟล์ pack | ไม่มีนามสกุล `.vpk` | ตรวจสอบว่าชื่อ pack ไม่รวมนามสกุล - `RegisterPackAuto` เพิ่ม `.vpk` |
| "HMAC verification failed" | รหัสผ่านไม่ถูกต้อง | ตรวจสอบว่า `SetVpkPassphrase` ถูกเรียกก่อน `RegisterPackAuto` |
| ไม่พบไฟล์ใน VPK | ตัวพิมพ์ใหญ่เล็กไม่ตรงกันในเส้นทาง | VPK ปรับเป็นตัวพิมพ์เล็กด้วยตัวคั่น `/` |
| Crash ใน `Get2()` | การชนกันของ sentinel `compressed_type` | ตรวจสอบว่าไม่มีไฟล์ EPK ที่ใช้ `compressed_type == -1` (ไม่มีใน Metin2 มาตรฐาน) |
| ข้อผิดพลาดการลิงก์ LZ4/Zstd/Brotli | ไลบรารีหายไป | เพิ่มไลบรารีคลายการบีบอัดใน Additional Dependencies |
| ข้อผิดพลาดการคอมไพล์ BLAKE3 | ไฟล์ซอร์สหายไป | ตรวจสอบว่าไฟล์ `blake3_*.c` ทั้งหมดอยู่ในโปรเจกต์ |
| ข้อผิดพลาด linker Boost | ไม่มี include Boost | ตรวจสอบเส้นทาง include Boost ในคุณสมบัติโปรเจกต์ EterPack |
| ข้อขัดแย้ง CryptoPP + OpenSSL | ชื่อสัญลักษณ์ซ้ำ | ไม่ควรเกิดขึ้น - กำหนดสัญลักษณ์แยกกัน ถ้าเกิดขึ้น ตรวจสอบการปนเปื้อนจาก `using namespace` |
| ข้อผิดพลาด Themida | `__THEMIDA__` ไม่ได้กำหนด | กำหนดในการตั้งค่าคอมไพล์ หรือลบบล็อก `#ifdef __THEMIDA__` ออกจาก `EterPackManager_Vpk.cpp` |
