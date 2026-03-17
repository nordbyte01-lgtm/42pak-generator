<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - İstemci Entegrasyon Kılavuzu (MartySama 5.8)

> **Profil:** MartySama 5.8 - Boost tabanlı çoklu şifreleme HybridCrypt mimarisini hedefler.
> 40250/ClientVS22 entegrasyonu için `../40250/INTEGRATION_GUIDE.md` dosyasına bakın.
> FliegeV3 entegrasyonu için `../FliegeV3/INTEGRATION_GUIDE.md` dosyasına bakın.

EterPack (EIX/EPK) sistemi için doğrudan yerine geçen çözüm. Gerçek
MartySama 5.8 istemci kaynak koduna (`Binary & S-Source/Binary/Client/EterPack/`)
dayanmaktadır. Tüm dosya yolları gerçek MartySama kaynak ağacına referans verir.

## Ne Elde Edersiniz

| Özellik | EterPack (eski) | VPK (yeni) |
|---------|---------------|-----------|
| Şifreleme | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (donanım hızlandırmalı) |
| Sıkıştırma | LZO | LZ4 / Zstandard / Brotli |
| Bütünlük | Dosya başına CRC32 | Dosya başına BLAKE3 + HMAC-SHA256 arşiv |
| Dosya formatı | .eix + .epk çifti | Tek .vpk dosyası |
| Anahtar yönetimi | Sunucudan gelen Panama IV + HybridCrypt SDB | PBKDF2-SHA512 parola |

## MartySama 5.8 vs 40250 vs FliegeV3

| Husus | 40250 | MartySama 5.8 | FliegeV3 |
|-------|-------|---------------|----------|
| Kripto sistemi | Camellia/Twofish/XTEA (HybridCrypt) | 40250 ile aynı | Yalnızca XTEA |
| Anahtar teslimi | Sunucudan gelen Panama IV + SDB | 40250 ile aynı | Statik derlenmiş anahtarlar |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| Container türleri | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| Header stili | Include guards | `#pragma once` | Include guards |
| Kurcalama koruması | Yok | `#ifdef __THEMIDA__` VM_START/VM_END | Yok |
| Manager metotları | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | 40250 ile aynı | Yok |
| Sıkıştırma | LZO | LZO | LZ4 |
| İndeks yapısı | 192 bayt `#pragma pack(push, 4)` | 192 bayt `#pragma pack(push, 4)` | 192 bayt `#pragma pack(push, 4)` |

VPK entegrasyon kodu, aynı API arayüzünü (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`) koruyarak MartySama'nın Boost
container'larına ve Themida işaretçilerine uyum sağlar.

## Mimari

VPK, `CEterFileDict` üzerinden entegre olur - MartySama'nın orijinal
EterPack'inin kullandığı aynı `boost::unordered_multimap` hash araması.
`CEterPackManager::RegisterPackAuto()` bir `.vpk` dosyası bulduğunda,
`CEterPack` yerine `CVpkPack` oluşturur. `CVpkPack`, girişlerini paylaşılan
`m_FileDict`'e sentinel işaretçisi ile kaydeder. `GetFromPack()` bir dosya
adını çözdüğünde, işaretçiyi kontrol eder ve şeffaf olarak
`CEterPack::Get2()` veya `CVpkPack::Get2()`'ye yönlendirir.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Dosyalar

### `Client/EterPack/` dizinine eklenecek yeni dosyalar

| Dosya | Amaç |
|-------|------|
| `VpkLoader.h` | `CVpkPack` sınıfı - `CEterPack` için doğrudan yerine geçen çözüm |
| `VpkLoader.cpp` | Tam uygulama: header ayrıştırma, giriş tablosu, şifre çözme+sıkıştırma açma, BLAKE3 doğrulama |
| `VpkCrypto.h` | Kripto araçları: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli kullanılarak uygulamalar |
| `EterPackManager_Vpk.h` | VPK + HybridCrypt + Boost ile düzenlenmiş `CEterPackManager` header dosyası |
| `EterPackManager_Vpk.cpp` | VPK dispatch, HybridCrypt, Boost iteratörleri, Themida ile düzenlenmiş `CEterPackManager` |

### Değiştirilecek dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `Client/EterPack/EterPackManager.h` | `EterPackManager_Vpk.h` ile değiştirin (veya birleştirin) |
| `Client/EterPack/EterPackManager.cpp` | `EterPackManager_Vpk.cpp` ile değiştirin (veya birleştirin) |
| `Client/UserInterface/UserInterface.cpp` | 2 satır değişikliği (aşağıya bakın) |

## Gerekli Kütüphaneler

### OpenSSL 1.1+
- Windows: https://slproweb.com/products/Win32OpenSSL.html adresinden indirin
- Header'lar: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Bağlama: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Bağlama: `lz4.lib`
- **Not:** MartySama 5.8 varsayılan olarak LZ4 içermez. Bu yeni bir bağımlılıktır.

### Zstandard
- https://github.com/facebook/zstd/releases
- Header: `<zstd.h>`
- Bağlama: `zstd.lib` (veya `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Header'lar: `<brotli/decode.h>`
- Bağlama: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Projenize kopyalayın: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: ayrıca `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c` ekleyin

### Mevcut bağımlılıklar (MartySama 5.8'de zaten mevcut)

MartySama 5.8, VPK için **gerekli olmayan** ancak eski EPK kod yolu için
projede kalan şu kütüphaneleri zaten içerir:

| Kütüphane | Kullanan |
|-----------|---------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), Panama şifresi, hash fonksiyonları |
| Boost | EterPack, EterPackManager, CEterFileDict'te `boost::unordered_map`/`boost::unordered_multimap` |
| LZO | EPK sıkıştırma açma (pack türleri 1, 2) |

Bunlar VPK ile birlikte çalışmaya devam eder. Değişiklik gerekmez.

## Adım Adım Entegrasyon

### Adım 1: EterPack VS projesine dosyaları ekleyin

1. `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` dosyalarını `Client/EterPack/` dizinine kopyalayın
2. BLAKE3 C dosyalarını `Client/EterPack/` dizinine (veya paylaşılan bir konuma) kopyalayın
3. Tüm yeni dosyaları Visual Studio'da EterPack projesine ekleyin
4. OpenSSL, LZ4, Zstd, Brotli için include yollarını **Additional Include Directories**'e ekleyin
5. Kütüphane yollarını **Additional Library Directories**'e ekleyin
6. `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` dosyalarını **Additional Dependencies**'e ekleyin

### Adım 2: EterPackManager'ı Değiştirin

**Seçenek A (temiz değiştirme):**
- `Client/EterPack/EterPackManager.h` dosyasını `EterPackManager_Vpk.h` ile değiştirin
- `Client/EterPack/EterPackManager.cpp` dosyasını `EterPackManager_Vpk.cpp` ile değiştirin
- Her ikisini `EterPackManager.h` / `EterPackManager.cpp` olarak yeniden adlandırın

**Seçenek B (birleştirme):**
Mevcut `EterPackManager.h` dosyasına şunları ekleyin:

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

**Önemli:** MartySama kurallarına uymak için `std::unordered_map` yerine
`boost::unordered_map` kullanımına dikkat edin. Sağlanan `EterPackManager_Vpk.h`
zaten tüm kodda Boost container'larını kullanmaktadır.

Ardından `EterPackManager_Vpk.cpp`'deki uygulamaları mevcut `.cpp` dosyanıza birleştirin.

### Adım 3: UserInterface.cpp'yi Düzenleyin

`Client/UserInterface/UserInterface.cpp` dosyasında, pack kayıt döngüsü
(satır 557–579 civarı):

**Önce:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**Sonra:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

Ve kayıt döngüsünden **önce** şu satırı ekleyin:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Hepsi bu. Toplamda iki değişiklik:
1. Döngüden önce `SetVpkPassphrase()`
2. `RegisterPack()` → `RegisterPackAuto()` (2 yer)

Önce/sonra açıklamalı tam yama için `UserInterface_VpkPatch.cpp` dosyasına bakın.

### Adım 4: Derleme

Önce EterPack projesini, ardından tam çözümü derleyin. VPK kodu mevcut
EterPack koduyla birlikte derlenir - hiçbir şey kaldırılmaz.

**MartySama 5.8'e özel derleme notları:**
- MartySama projenin her yerinde Boost kullanır. `CEterFileDict`'teki
  `boost::unordered_multimap` VPK ile tamamen uyumludur - `InsertItem()` aynı şekilde çalışır.
- EterPack'teki `StdAfx.h`'nin `<boost/unordered_map.hpp>` içerdiğinden emin olun (MartySama
  projesi derleniyorsa zaten içeriyor olmalıdır).
- CryptoPP HybridCrypt için zaten çözümde bulunuyor - OpenSSL ile çakışma yok.
  Tamamen farklı amaçlara hizmet ederler (CryptoPP eski EPK için, OpenSSL VPK için).
- Themida kullanıyorsanız, `EterPackManager_Vpk.cpp`'deki `#ifdef __THEMIDA__`
  işaretçileri zaten yerindedir. VM_START/VM_END işaretçilerinin etkin olmasını istiyorsanız
  derleme yapılandırmanızda `__THEMIDA__`'yı tanımlayın.

## Nasıl Çalışır

### Kayıt Akışı

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

### Dosya Erişim Akışı

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

### HybridCrypt ile Birlikte Çalışma

MartySama 5.8, giriş sırasında sunucudan şifreleme anahtarlarını alır:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

Bu metotlar `m_PackMap` üzerinde iterasyon yapar (yalnızca `CEterPack*` nesneleri içerir).
VPK paketleri `m_VpkPackMap`'de ayrı olarak saklanır, bu nedenle HybridCrypt
anahtar teslimi tamamen şeffaftır - her iki sistem ağ kodunda herhangi bir
değişiklik yapılmadan birlikte çalışır.

### Bellek Yönetimi

CVpkPack, `CMappedFile::AppendDataBlock()` kullanır - CEterPack'in HybridCrypt
verileri için kullandığı aynı mekanizma. Sıkıştırması açılmış veriler,
CMappedFile'a ait bir arabelleğe kopyalanır ve CMappedFile kapsam dışına çıktığında
otomatik olarak serbest bırakılır. Manuel temizleme gerekmez.

## Paketlerin Dönüştürülmesi

Mevcut MartySama pack dosyalarınızı dönüştürmek için 42pak-generator aracını kullanın:

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

Veya GUI'nin Oluştur görünümünü kullanarak klasörlerden VPK arşivleri oluşturun.

## Geçiş Stratejisi

1. **Kütüphaneleri kurun** - OpenSSL, LZ4, Zstd, Brotli, BLAKE3'ü projenize ekleyin
2. **VPK dosyalarını ekleyin** - 6 yeni kaynak dosyayı `Client/EterPack/` dizinine kopyalayın
3. **EterPackManager'ı düzenleyin** - header ve uygulamayı birleştirin veya değiştirin
4. **UserInterface.cpp'yi düzenleyin** - 2 satırlık değişiklik
5. **Derleyin** - her şeyin derlendiğini doğrulayın
6. **Bir paketi dönüştürün** - örn. `metin2_patch_etc` → doğru yüklendiğini test edin
7. **Kalan paketleri dönüştürün** - teker teker veya hepsini birden
8. **Eski EPK dosyalarını kaldırın** - tüm paketler dönüştürüldükten sonra

`RegisterPackAuto` VPK yoksa EPK'ya geri döndüğünden, hiçbir şeyi bozmadan
paketleri kademeli olarak dönüştürebilirsiniz.

## Parola Yapılandırması

| Yöntem | Ne zaman kullanılır |
|--------|-------------------|
| Kaynak kodda sabit | Özel sunucular, en basit yaklaşım |
| Yapılandırma dosyası (`metin2.cfg`) | Yeniden derleme olmadan değiştirmesi kolay |
| Girişte sunucudan gönderilen | Maksimum güvenlik - parola her oturum değişir |

Sunucudan gönderilen parola için, `CAccountConnector`'ı kimlik doğrulama
yanıtında parolayı alacak şekilde değiştirin ve herhangi bir pack erişiminden
önce `CEterPackManager::Instance().SetVpkPassphrase()` çağrısı yapın.
MartySama'nın `AccountConnector.cpp` dosyası zaten özel sunucu paketlerini
işler - mevcut `RetrieveHybridCryptPackKeys` çağrısının yanına yeni bir
handler ekleyin.

## Sorun Giderme

| Belirti | Neden | Çözüm |
|---------|-------|-------|
| Pack dosyaları bulunamadı | `.vpk` uzantısı eksik | Pack adının uzantı içermediğinden emin olun - `RegisterPackAuto` `.vpk` ekler |
| "HMAC verification failed" | Yanlış parola | `SetVpkPassphrase`'in `RegisterPackAuto`'dan önce çağrıldığını kontrol edin |
| VPK'da dosyalar bulunamadı | Yolda büyük/küçük harf uyumsuzluğu | VPK, `/` ayırıcılarıyla küçük harfe normalleştirir |
| `Get2()`'de çökme | `compressed_type` sentinel çakışması | Hiçbir EPK dosyasının `compressed_type == -1` kullanmadığından emin olun (standart Metin2'de hiçbiri kullanmaz) |
| LZ4/Zstd/Brotli bağlama hatası | Eksik kütüphane | Sıkıştırma açma kütüphanesini Additional Dependencies'e ekleyin |
| BLAKE3 derleme hatası | Eksik kaynak dosyalar | Tüm `blake3_*.c` dosyalarının projede olduğundan emin olun |
| Boost linker hataları | Eksik Boost include'ları | EterPack proje özelliklerinde Boost include yollarını doğrulayın |
| CryptoPP + OpenSSL çakışması | Yinelenen sembol adları | Olmamalıdır - ayrı semboller tanımlarlar. Olursa, `using namespace` kirliliğini kontrol edin |
| Themida hataları | `__THEMIDA__` tanımlanmamış | Derleme yapılandırmanızda tanımlayın veya `EterPackManager_Vpk.cpp`'den `#ifdef __THEMIDA__` bloklarını kaldırın |
