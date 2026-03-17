<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – คู่มือการรวมระบบเซิร์ฟเวอร์

อ้างอิงจากการวิเคราะห์ซอร์สโค้ดจริงของเซิร์ฟเวอร์ 40250 ที่
`Server/metin2_server+src/metin2/src/server/`

## การค้นพบสำคัญ: เซิร์ฟเวอร์ไม่ใช้ EterPack

เซิร์ฟเวอร์ 40250 มีการอ้างอิง EterPack, ไฟล์ `.eix` หรือ `.epk` **เป็นศูนย์**
การดำเนินการ I/O ทั้งหมดของเซิร์ฟเวอร์ใช้ `fopen()`/`fread()` ธรรมดา หรือ `std::ifstream`:

| Loader | ใช้โดย | ไฟล์ซอร์ส |
|--------|--------|-----------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, ตาราง DragonSoul | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| `fopen()` โดยตรง | CONFIG, ไฟล์แผนที่, regen, locale, fishing, cube | หลากหลาย |

เซิร์ฟเวอร์อ้างอิงถึงแพ็คเพียงที่เดียว: **`ClientPackageCryptInfo.cpp`**
ซึ่งโหลดคีย์การเข้ารหัสจาก `package.dir/` และส่งไปยัง **ไคลเอนต์**
สำหรับถอดรหัสไฟล์ `.epk` เซิร์ฟเวอร์เองไม่เคยเปิดไฟล์แพ็ค

## ตัวเลือกการรวมระบบ

### ตัวเลือก A: เก็บไฟล์เซิร์ฟเวอร์เป็นไฟล์ดิบ (แนะนำ)

เนื่องจากเซิร์ฟเวอร์อ่านไฟล์ดิบจากดิสก์อยู่แล้ว วิธีที่ง่ายที่สุดคือ:
- เก็บไฟล์ข้อมูลเซิร์ฟเวอร์ทั้งหมด (proto, config, แผนที่) เป็นไฟล์ดิบ
- ใช้ VPK เฉพาะฝั่งไคลเอนต์
- ลบหรืออัปเดต `ClientPackageCryptInfo` เพื่อไม่ส่งคีย์ EPK

นี่คือวิธีที่แนะนำสำหรับการตั้งค่าส่วนใหญ่

### ตัวเลือก B: รวมข้อมูลเซิร์ฟเวอร์ในอาร์ไคฟ์ VPK

หากต้องการไฟล์ข้อมูลเซิร์ฟเวอร์ที่ป้องกันการแก้ไขและเข้ารหัส ใช้ `CVpkHandler`
เพื่ออ่านจากอาร์ไคฟ์ VPK แทนไฟล์ดิบ

## ไฟล์

| ไฟล์ | วัตถุประสงค์ |
|------|-------------|
| `VpkHandler.h` | ตัวอ่าน VPK แบบสแตนด์อโลน (ไม่มีการพึ่งพา EterPack) |
| `VpkHandler.cpp` | การใช้งานเต็มรูปแบบพร้อมระบบเข้ารหัสในตัว |

ตัวจัดการของเซิร์ฟเวอร์เป็นแบบสแตนด์อโลนทั้งหมด – รวมการใช้งาน
AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard และ Brotli
ของตัวเองไว้ภายใน ไม่มีการพึ่งพาโค้ด EterPack ฝั่งไคลเอนต์ใดๆ

## ไลบรารีที่ต้องการ

### Linux (เซิร์ฟเวอร์ทั่วไป)

```bash
# Debian/Ubuntu
sudo apt-get install libssl-dev liblz4-dev libzstd-dev libbrotli-dev

# CentOS/RHEL
sudo yum install openssl-devel lz4-devel libzstd-devel brotli-devel

# FreeBSD
pkg install openssl liblz4 zstd brotli
```

### BLAKE3

```bash
git clone https://github.com/BLAKE3-team/BLAKE3.git
cp BLAKE3/c/blake3.h BLAKE3/c/blake3.c BLAKE3/c/blake3_dispatch.c \
   BLAKE3/c/blake3_portable.c game/src/
```

### เพิ่มเติมใน Makefile

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## ตัวอย่างการรวมระบบ

### ตัวอย่างที่ 1: โหลดข้อมูล Proto จาก VPK

เซิร์ฟเวอร์ DB โหลด proto จากไฟล์ CSV/ไบนารีใน `db/src/ClientManagerBoot.cpp`:

**ก่อน (ไฟล์ดิบ):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**หลัง (VPK):**
```cpp
#include "VpkHandler.h"

static CVpkHandler g_localeVpk;

bool InitLocaleVpk(const char* locale, const char* passphrase)
{
    char path[256];
    snprintf(path, sizeof(path), "pack/locale_%s.vpk", locale);
    return g_localeVpk.Open(path, passphrase);
}

bool LoadItemProto()
{
    std::vector<unsigned char> data;
    if (!g_localeVpk.ReadFile("locale/en/item_proto", data))
    {
        sys_err("Failed to read item_proto from VPK");
        return false;
    }

    int count = data.size() / sizeof(TItemTable);
    const TItemTable* items = reinterpret_cast<const TItemTable*>(data.data());
    // ประมวลผลรายการ...
    return true;
}
```

### ตัวอย่างที่ 2: อ่านไฟล์ข้อความจาก VPK

สำหรับไฟล์แบบ `CTextFileLoader` (mob_group.txt ฯลฯ):

```cpp
bool LoadMobGroups()
{
    std::string content;
    if (!g_localeVpk.ReadFileAsString("locale/en/mob_group.txt", content))
    {
        sys_err("Failed to read mob_group.txt from VPK");
        return false;
    }

    CMemoryTextFileLoader loader;
    loader.Bind(content.size(), content.c_str());
    // แยกวิเคราะห์เหมือนเดิม...
    return true;
}
```

### ตัวอย่างที่ 3: อัปเดต ClientPackageCryptInfo

เซิร์ฟเวอร์ 40250 ส่งคีย์เข้ารหัส EPK ไปยังไคลเอนต์ผ่าน
`game/src/ClientPackageCryptInfo.cpp` และ `game/src/panama.cpp`

เมื่อใช้ VPK คุณมีสองตัวเลือก:

**ตัวเลือก A: ลบการส่งคีย์แพ็คทั้งหมด**
หากแพ็คทั้งหมดถูกแปลงเป็น VPK แล้ว ไคลเอนต์ไม่ต้องการ
คีย์ EPK ที่ส่งจากเซิร์ฟเวอร์อีกต่อไป ลบหรือปิดการใช้งาน `LoadClientPackageCryptInfo()` และ `PanamaLoad()`
จากลำดับการเริ่มต้นใน `game/src/main.cpp`

**ตัวเลือก B: เก็บการรองรับแบบไฮบริด**
หากบางแพ็คยังคงเป็น EPK ในขณะที่อื่นๆ เป็น VPK ให้คงโค้ด
ส่งคีย์ที่มีอยู่สำหรับแพ็ค EPK แพ็ค VPK ใช้กลไก passphrase
ของตัวเองและไม่ต้องการคีย์ที่ส่งจากเซิร์ฟเวอร์

### ตัวอย่างที่ 4: คำสั่งผู้ดูแลระบบ

```cpp
ACMD(do_vpk_validate)
{
    char arg1[256];
    one_argument(argument, arg1, sizeof(arg1));

    if (!*arg1)
    {
        ch->ChatPacket(CHAT_TYPE_INFO, "Usage: /vpk_validate <path>");
        return;
    }

    CVpkHandler handler;
    if (!handler.Open(arg1, g_vpkPassphrase.c_str()))
    {
        ch->ChatPacket(CHAT_TYPE_INFO, "Failed to open: %s", arg1);
        return;
    }

    auto result = handler.Validate();
    ch->ChatPacket(CHAT_TYPE_INFO, "VPK %s: %d/%d valid, %d errors",
                   arg1, result.ValidEntries, result.TotalEntries,
                   (int)result.Errors.size());
}
```

## การจัดการ Passphrase

บนเซิร์ฟเวอร์ เก็บ passphrase VPK อย่างปลอดภัย:

```bash
# conf/vpk.conf (chmod 600)
VPK_PASSPHRASE=your-strong-passphrase-here
```

```cpp
std::string LoadVpkPassphrase()
{
    std::ifstream f("conf/vpk.conf");
    std::string line;
    while (std::getline(f, line))
    {
        if (line.compare(0, 15, "VPK_PASSPHRASE=") == 0)
            return line.substr(15);
    }
    return "";
}
```

## รองรับการบีบอัด

ตัวจัดการเซิร์ฟเวอร์รองรับอัลกอริทึมการบีบอัดทั้งสามตัว:

| อัลกอริทึม | ID | ไลบรารี | ความเร็ว | อัตราส่วน |
|------------|-----|---------|----------|----------|
| LZ4 | 1 | liblz4 | เร็วที่สุด | ดี |
| Zstandard | 2 | libzstd | เร็ว | ดีกว่า |
| Brotli | 3 | libbrotli | ช้ากว่า | ดีที่สุด |

อัลกอริทึมถูกเก็บไว้ในเฮดเดอร์ VPK และตรวจจับอัตโนมัติ
ไม่ต้องกำหนดค่าฝั่งอ่าน

## โครงสร้างไดเรกทอรี

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← ข้อมูลภาษา
│   ├── locale_de.vpk
│   ├── proto.vpk             ← ตาราง item/mob proto
│   └── scripts.vpk           ← สคริปต์เควส, การกำหนดค่า
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## หมายเหตุด้านประสิทธิภาพ

- การอ่านไฟล์ VPK เป็น I/O แบบลำดับ (หนึ่ง seek + หนึ่ง read ต่อไฟล์)
- ตารางรายการถูกแยกวิเคราะห์ครั้งเดียวที่ `Open()` และแคชในหน่วยความจำ
- สำหรับไฟล์ที่เข้าถึงบ่อย ควรพิจารณาแคชผลลัพธ์ของ `ReadFile()`
- การตรวจสอบแฮช BLAKE3 เพิ่ม ~0.1ms ต่อ MB ของข้อมูล

## การตรวจจับการแก้ไข

HMAC-SHA256 ที่ท้ายไฟล์ VPK แต่ละไฟล์รับประกันว่า:
- การแก้ไขเนื้อหาไฟล์ใดๆ จะถูกตรวจจับ
- รายการที่เพิ่มหรือลบจะถูกตรวจจับ
- การแก้ไขเฮดเดอร์จะถูกตรวจจับ

หาก `Open()` คืนค่า false สำหรับ VPK ที่เข้ารหัส อาร์ไคฟ์อาจถูก
แก้ไขหรือ passphrase ไม่ถูกต้อง
- การคลายการบีบอัด LZ4 เพิ่ม ~0.5ms ต่อ MB ของข้อมูลที่บีบอัด
