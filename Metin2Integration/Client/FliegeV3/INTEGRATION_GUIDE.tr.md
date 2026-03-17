<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - FliegeV3 İstemci Entegrasyon Kılavuzu

EterPack (EIX/EPK) sistemi için doğrudan yedek. Gerçek FliegeV3 binary-src
istemci kaynak koduna dayanır. Tüm dosya yolları gerçek FliegeV3 kaynak ağacına
atıfta bulunur.

## Ne Elde Edersiniz

| Özellik | EterPack (FliegeV3) | VPK (yeni) |
|---------|---------------------|------------|
| Şifreleme | XTEA (128-bit anahtar, 32 tur) | AES-256-GCM (donanım hızlandırmalı) |
| Sıkıştırma | LZ4 (LZO'dan taşınmış) | LZ4 / Zstandard / Brotli |
| Bütünlük | Dosya başına CRC32 | Dosya başına BLAKE3 + HMAC-SHA256 arşiv |
| Dosya formatı | .eix + .epk çifti | Tek .vpk dosyası |
| Anahtar yönetimi | Statik derlenmiş XTEA anahtarları | PBKDF2-SHA512 parola |
| İndeks girişleri | 192 bayt `TEterPackIndex` | Değişken uzunluklu VPK girişleri |

## FliegeV3 vs 40250 - Neden Ayrı Profil?

FliegeV3, 40250 sisteminden önemli ölçüde farklıdır:

| Yön | 40250 | FliegeV3 |
|-----|-------|----------|
| Kripto sistemi | Camellia/Twofish/XTEA hibrit (HybridCrypt) | Sadece XTEA |
| Anahtar teslimi | Sunucudan Panama IV + SDB blokları | Statik derlenmiş anahtarlar |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` |
| Manager metotları | `DecryptPackIV`, `WriteHybridCryptPackInfo`, `RetrieveHybridCryptPackKeys/SDB` | Bunların hiçbiri yok |
| Dosya arama | Her zaman yerel dosyalara izin verir | Koşullu `ENABLE_LOCAL_FILE_LOADING` |
| İndeks yapısı | 169 bayt (#pragma pack(push,4) ile 161 karakter dosya adı) | 192 bayt (aynı yapı, farklı hizalama/doldurma) |

VPK entegrasyon kodu bu farklılıklara uyum sağlarken aynı API yüzeyini
(`RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase`) korur.

## Mimari

VPK, `CEterFileDict` aracılığıyla entegre olur - orijinal FliegeV3 EterPack'in
kullandığı aynı `boost::unordered_multimap` araması. `RegisterPackAuto()`
bir `.vpk` dosyası bulduğunda, `CEterPack` yerine `CVpkPack` oluşturur.
`CVpkPack`, girişlerini paylaşılan `m_FileDict`'e bir sentinel işaretçisi
(`compressed_type == -1`) ile kaydeder.

`GetFromPack()` bir dosya adını çözümlediğinde, işaretçiyi kontrol eder ve
şeffaf bir şekilde `CEterPack::Get2()` veya `CVpkPack::Get2()`'ye yönlendirir.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Dosyalar

### `source/EterPack/` dizinine eklenecek yeni dosyalar

| Dosya | Amaç |
|-------|------|
| `VpkLoader.h` | `CVpkPack` sınıfı - `CEterPack` için doğrudan yedek |
| `VpkLoader.cpp` | Tam uygulama: başlık ayrıştırma, giriş tablosu, şifre çözme+açma, BLAKE3 doğrulama |
| `VpkCrypto.h` | Kripto araçları: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli kullanan uygulamalar |
| `EterPackManager_Vpk.h` | VPK desteği ile yamalı `CEterPackManager` başlığı |
| `EterPackManager_Vpk.cpp` | Yamalı `CEterPackManager` - HybridCrypt yok, FliegeV3 arama mantığı |

### Değiştirilecek dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `source/EterPack/EterPackManager.h` | `EterPackManager_Vpk.h` ile değiştirin (veya birleştirin) |
| `source/EterPack/EterPackManager.cpp` | `EterPackManager_Vpk.cpp` ile değiştirin (veya birleştirin) |
| `source/UserInterface/UserInterface.cpp` | 2 satırlık değişiklik (aşağıya bakın) |

## Gerekli Kütüphaneler

### OpenSSL 1.1+
- Windows: https://slproweb.com/products/Win32OpenSSL.html adresinden indirin
- Başlıklar: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Bağlantı: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Başlık: `<lz4.h>`
- Bağlantı: `lz4.lib`
- **Not:** FliegeV3 paket sıkıştırma için zaten LZ4 kullanır. Projeniz
  zaten `lz4.lib` bağlıyorsa, ek kurulum gerekmez.

### Zstandard
- https://github.com/facebook/zstd/releases
- Başlık: `<zstd.h>`
- Bağlantı: `zstd.lib` (veya `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Başlıklar: `<brotli/decode.h>`
- Bağlantı: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Projenize kopyalayın: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: ayrıca `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c` ekleyin

## Adım Adım Entegrasyon

### Adım 1: Dosyaları EterPack VS projesine ekleyin

1. `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` dosyalarını `source/EterPack/` dizinine kopyalayın
2. BLAKE3 C dosyalarını `source/EterPack/` dizinine kopyalayın (veya paylaşılan bir konuma)
3. Tüm yeni dosyaları Visual Studio'da EterPack projesine ekleyin
4. OpenSSL, Zstd, Brotli için include yollarını **Additional Include Directories**'e ekleyin
   - FliegeV3 build'iniz LZ4 kullanıyorsa zaten yapılandırılmış olmalı
5. Kütüphane yollarını **Additional Library Directories**'e ekleyin
6. `libssl.lib`, `libcrypto.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` dosyalarını **Additional Dependencies**'e ekleyin
   - `lz4.lib` zaten bağlı olmalı

### Adım 2: EterPackManager'ı değiştirin

**Seçenek A (temiz değiştirme):**
- `source/EterPack/EterPackManager.h` dosyasını `EterPackManager_Vpk.h` ile değiştirin
- `source/EterPack/EterPackManager.cpp` dosyasını `EterPackManager_Vpk.cpp` ile değiştirin
- Her ikisini de `EterPackManager.h` / `EterPackManager.cpp` olarak yeniden adlandırın

**Seçenek B (birleştirme):**
Mevcut `EterPackManager.h`'ye bunları ekleyin:

```cpp
#include "VpkLoader.h"

// Sınıf bildirimi içinde, public'e ekleyin:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Sınıf bildirimi içinde, protected'a ekleyin:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Ardından `EterPackManager_Vpk.cpp`'deki uygulamaları mevcut `.cpp` dosyanıza birleştirin.

**Önemli:** `DecryptPackIV`, `WriteHybridCryptPackInfo`,
`RetrieveHybridCryptPackKeys` veya `RetrieveHybridCryptPackSDB` EKLEMEYİN -
FliegeV3'te bu metotlar yoktur.

### Adım 3: UserInterface.cpp'yi yamalayın

`source/UserInterface/UserInterface.cpp` dosyasında, paket kayıt döngüsü:

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

Ve kayıt döngüsünden **önce** bu satırı ekleyin:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Hepsi bu. Toplamda iki değişiklik:
1. Döngüden önce `SetVpkPassphrase()`
2. `RegisterPack()` → `RegisterPackAuto()` (2 yer)

**Not:** 40250 entegrasyonunun aksine, FliegeV3 ağ kodunda herhangi bir yama
GEREKTİRMEZ. `RetrieveHybridCryptPackKeys` veya `RetrieveHybridCryptPackSDB`
çağrıları yoktur.

### Adım 4: Derleme

Önce EterPack projesini, ardından tüm çözümü derleyin. VPK kodu mevcut
EterPack kodunun yanında derlenir - hiçbir şey kaldırılmaz.

**FliegeV3'e özgü derleme notları:**
- FliegeV3, Boost kullanır. `CEterFileDict`'teki `boost::unordered_multimap`
  VPK ile uyumludur - `InsertItem()` aynı şekilde çalışır.
- EterPack'teki `StdAfx.h`'nin `<boost/unordered_map.hpp>` içerdiğinden emin
  olun (FliegeV3 projesi derleniyor ise zaten içermelidir).
- `boost::unordered_multimap` hakkında linker hataları görürseniz, EterPack
  proje özelliklerinde Boost include yollarını doğrulayın.

## Nasıl Çalışır

### Kayıt Akışı

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     m_strVpkPassphrase'i saklar

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") var mı?
                 "xyz/")              ├─ EVET → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ Her dosya için InsertItem()
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ HAYIR → RegisterPack() [orijinal EPK akışı]
```

### Dosya Erişim Akışı

```
Herhangi bir kod çağırır:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → küçük harf, ayırıcıları normalleştir
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ eğer compressed_type == -1 (VPK sentinel):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ AES-GCM şifre çözme (şifreliyse)
  │       │    ├─ LZ4/Zstd/Brotli açma (sıkıştırılmışsa)
  │       │    └─ BLAKE3 hash doğrulama
  │       └─ mappedFile.AppendDataBlock(data, size)
  │
  └─ değilse (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [orijinal açma: LZ4 + XTEA / MCOZ başlığı]
```

### Bellek Yönetimi

CVpkPack, `CMappedFile::AppendDataBlock()` kullanır - CEterPack'in kullandığı
aynı mekanizma. Açılmış veriler, CMappedFile'ın sahip olduğu bir tampona
kopyalanır ve CMappedFile kapsam dışına çıktığında otomatik olarak serbest bırakılır.

## Paketleri Dönüştürme

Mevcut FliegeV3 paket dosyalarınızı dönüştürmek için 42pak-generator aracını kullanın:

```
# CLI - tek bir EPK çiftini VPK'ya dönüştürme
42pak-generator.exe convert --source metin2_patch_etc.eix --passphrase "gizli-anahtarınız"

# CLI - bir klasörden VPK oluşturma
42pak-generator.exe build --source ./ymir_work/item/ --output pack/item.vpk \
    --passphrase "gizli-anahtarınız" --algorithm lz4 --compression 6
```

Veya klasörlerden VPK arşivleri oluşturmak için GUI'nin Create görünümünü kullanın.

## Geçiş Stratejisi

1. **Kütüphaneleri kurun** - projenize OpenSSL, Zstd, Brotli, BLAKE3 ekleyin (LZ4 muhtemelen zaten mevcut)
2. **VPK dosyalarını ekleyin** - 6 yeni kaynak dosyayı `source/EterPack/` dizinine kopyalayın
3. **EterPackManager'ı yamalayın** - başlığı ve uygulamayı birleştirin veya değiştirin
4. **UserInterface.cpp'yi yamalayın** - 2 satırlık değişiklik
5. **Derleyin** - her şeyin derlendiğini doğrulayın
6. **Bir paketi dönüştürün** - ör. `metin2_patch_etc` -> doğru yüklendiğini test edin
7. **Kalan paketleri dönüştürün** - tek tek veya hepsini birden
8. **Eski EPK dosyalarını silin** - tüm paketler dönüştürüldükten sonra

`RegisterPackAuto`, VPK olmadığında otomatik olarak EPK'ya döndüğünden,
paketleri kademeli olarak hiçbir şeyi bozmadan dönüştürebilirsiniz.

## Parola Yapılandırması

| Yöntem | Ne zaman kullanılır |
|--------|-------------------|
| Kaynak kodda sabit | Özel sunucular, en basit yaklaşım |
| Yapılandırma dosyası (`metin2.cfg`) | Yeniden derleme olmadan kolay değiştirme |
| Girişte sunucu tarafından gönderilen | Maksimum güvenlik - parola oturum başına değişir |

Sunucu tarafından gönderilen parola için, `CAccountConnector`'ı kimlik doğrulama
yanıtında parolayı alacak şekilde değiştirin ve herhangi bir paket erişiminden
önce `CEterPackManager::Instance().SetVpkPassphrase()` çağırın.

## Sorun Giderme

| Belirti | Neden | Çözüm |
|---------|-------|-------|
| Paket dosyaları bulunamadı | `.vpk` uzantısı eksik | Paket adının uzantı içermediğinden emin olun - `RegisterPackAuto` `.vpk` ekler |
| "HMAC verification failed" | Yanlış parola | `SetVpkPassphrase`'in `RegisterPackAuto`'dan önce çağrıldığını kontrol edin |
| VPK'da dosyalar bulunamadı | Büyük/küçük harf uyumsuzluğu | VPK küçük harfe `/` ayırıcılarla normalleştirir |
| `Get2()`'de çökme | `compressed_type` sentinel çakışması | Hiçbir EPK dosyasının `compressed_type == -1` kullanmadığından emin olun (standart Metin2'de hiçbiri kullanmaz) |
| LZ4/Zstd/Brotli bağlantı hatası | Eksik kütüphane | Açma kütüphanesini Additional Dependencies'e ekleyin |
| BLAKE3 derleme hatası | Eksik kaynak dosyalar | Tüm `blake3_*.c` dosyalarının projede olduğundan emin olun |
| Boost linker hataları | Eksik Boost includes | EterPack proje özelliklerinde Boost include yollarını doğrulayın |
| `ENABLE_LOCAL_FILE_LOADING` sorunları | Release'de yerel dosyalar bulunamıyor | Makroyu sadece debug build'lerde tanımlayın - FliegeV3 konvansiyonuna uyar |
