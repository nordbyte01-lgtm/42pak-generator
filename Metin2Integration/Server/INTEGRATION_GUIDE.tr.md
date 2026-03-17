<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Sunucu Entegrasyon Kılavuzu

40250 sunucu kaynak kodunun gerçek analizine dayanmaktadır:
`Server/metin2_server+src/metin2/src/server/`.

## Temel Bulgu: Sunucu EterPack Kullanmıyor

40250 sunucusunda EterPack, `.eix` veya `.epk` dosyalarına **hiçbir** referans yoktur.
Tüm sunucu dosya I/O işlemleri düz `fopen()`/`fread()` veya `std::ifstream` kullanır:

| Yükleyici | Kullanan | Kaynak Dosya |
|-----------|----------|--------------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, DragonSoul tabloları | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| Doğrudan `fopen()` | CONFIG, harita dosyaları, regen, locale, fishing, cube | Çeşitli |

Sunucu paketlere yalnızca tek bir yerde referans verir: **`ClientPackageCryptInfo.cpp`**,
bu dosya şifreleme anahtarlarını `package.dir/` dizininden yükler ve bunları **istemciye**
`.epk` dosyalarını çözmesi için gönderir. Sunucu kendisi asla paket dosyalarını açmaz.

## Entegrasyon Seçenekleri

### Seçenek A: Sunucu Dosyalarını Ham Dosya Olarak Tut (Önerilen)

Sunucu zaten diskten ham dosyaları okuduğundan, en basit yaklaşım şudur:
- Tüm sunucu veri dosyalarını (proto, yapılandırma, haritalar) ham dosya olarak tut
- VPK'yı yalnızca istemci tarafında kullan
- `ClientPackageCryptInfo`'yu EPK anahtarları göndermeyecek şekilde kaldır veya güncelle

Bu, çoğu kurulum için önerilen yaklaşımdır.

### Seçenek B: Sunucu Verilerini VPK Arşivlerinde Paketle

Kurcalamaya dayanıklı, şifreli sunucu veri dosyaları istiyorsanız, ham dosyalar yerine
VPK arşivlerinden okumak için `CVpkHandler` kullanın.

## Dosyalar

| Dosya | Amaç |
|-------|------|
| `VpkHandler.h` | Bağımsız VPK okuyucu (EterPack bağımlılığı yok) |
| `VpkHandler.cpp` | Yerleşik kriptografi ile tam uygulama |

Sunucu handler'ı tamamen bağımsızdır – kendi AES-GCM,
PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard ve Brotli uygulamalarını
içinde barındırır. İstemci tarafındaki herhangi bir EterPack koduna bağımlılık yoktur.

## Gerekli Kütüphaneler

### Linux (tipik sunucu)

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

### Makefile Eklemeleri

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## Entegrasyon Örnekleri

### Örnek 1: VPK'dan Proto Verilerini Yükleme

DB sunucusu proto'ları `db/src/ClientManagerBoot.cpp` dosyasındaki CSV/ikili dosyalardan yükler:

**Önce (ham dosya):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**Sonra (VPK):**
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
    // öğeleri işle...
    return true;
}
```

### Örnek 2: VPK'dan Metin Dosyalarını Okuma

`CTextFileLoader` stilindeki dosyalar için (mob_group.txt, vb.):

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
    // önceki gibi ayrıştır...
    return true;
}
```

### Örnek 3: ClientPackageCryptInfo Güncelleme

40250 sunucusu EPK şifreleme anahtarlarını istemciye
`game/src/ClientPackageCryptInfo.cpp` ve `game/src/panama.cpp` aracılığıyla gönderir.

VPK kullanırken iki seçeneğiniz vardır:

**Seçenek A: Paket anahtarı gönderimini tamamen kaldır**
Tüm paketler VPK'ya dönüştürüldüyse, istemci artık sunucu tarafından gönderilen
EPK anahtarlarına ihtiyaç duymaz. `game/src/main.cpp` dosyasındaki başlatma dizisinden
`LoadClientPackageCryptInfo()` ve `PanamaLoad()` fonksiyonlarını kaldırın veya devre dışı bırakın.

**Seçenek B: Hibrit desteği koru**
Bazı paketler EPK olarak kalırken diğerleri VPK ise, EPK paketleri için
mevcut anahtar gönderme kodunu koruyun. VPK paketleri kendi passphrase
mekanizmalarını kullanır ve sunucu tarafından gönderilen anahtarlara ihtiyaç duymaz.

### Örnek 4: Yönetici Komutları

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

## Passphrase Yönetimi

Sunucuda VPK passphrase'ini güvenli bir şekilde saklayın:

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

## Sıkıştırma Desteği

Sunucu handler'ı üç sıkıştırma algoritmasını da destekler:

| Algoritma | ID | Kütüphane | Hız | Oran |
|-----------|----|-----------|-----|------|
| LZ4 | 1 | liblz4 | En hızlı | İyi |
| Zstandard | 2 | libzstd | Hızlı | Daha iyi |
| Brotli | 3 | libbrotli | Daha yavaş | En iyi |

Algoritma VPK başlığında saklanır ve otomatik olarak algılanır.
Okuma tarafında yapılandırma gerekmez.

## Dizin Düzeni

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← yerelleştirme verileri
│   ├── locale_de.vpk
│   ├── proto.vpk             ← item/mob proto tabloları
│   └── scripts.vpk           ← görev betikleri, yapılandırmalar
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## Performans Notları

- VPK dosya okumaları sıralı I/O'dur (dosya başına bir arama + bir okuma)
- Giriş tablosu `Open()` sırasında bir kez ayrıştırılır ve bellekte önbelleğe alınır
- Sık erişilen dosyalar için `ReadFile()` çıktısını önbelleğe almayı düşünün
- BLAKE3 hash doğrulaması MB veri başına ~0,1ms ekler

## Kurcalama Tespiti

Her VPK dosyasının sonundaki HMAC-SHA256 şunları sağlar:
- Herhangi bir dosya içeriğindeki değişiklikler tespit edilir
- Eklenen veya kaldırılan girişler tespit edilir
- Başlık kurcalaması tespit edilir

Şifrelenmiş bir VPK için `Open()` false döndürürse, arşiv kurcalanmış olabilir
veya passphrase yanlış olabilir.
- LZ4 açma işlemi sıkıştırılmış veri MB'ı başına ~0,5ms ekler
