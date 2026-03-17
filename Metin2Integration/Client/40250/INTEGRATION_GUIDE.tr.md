<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - İstemci Entegrasyon Kılavuzu (40250 / ClientVS22)

> **Profil:** 40250 - çoklu şifreleme HybridCrypt mimarisini hedefler.
> FliegeV3 (XTEA/LZ4) entegrasyonu için `../FliegeV3/INTEGRATION_GUIDE.md` dosyasına bakın.

EterPack (EIX/EPK) sistemi için doğrudan yedek. Gerçek 40250 istemci kaynak
koduna dayanır. Tüm dosya yolları gerçek kaynak ağacına atıfta bulunur.

## Ne Elde Edersiniz

| Özellik | EterPack (eski) | VPK (yeni) |
|---------|----------------|------------|
| Şifreleme | TEA / Panama / HybridCrypt | AES-256-GCM (donanım hızlandırmalı) |
| Sıkıştırma | LZO | LZ4 / Zstandard / Brotli |
| Bütünlük | Dosya başına CRC32 | Dosya başına BLAKE3 + HMAC-SHA256 arşiv |
| Dosya formatı | .eix + .epk çifti | Tek .vpk dosyası |
| Anahtar yönetimi | Sunucu gönderimli Panama IV + HybridCrypt SDB | PBKDF2-SHA512 parola |

## Mimari

VPK, `CEterFileDict` aracılığıyla entegre olur - orijinal EterPack'in kullandığı
aynı hash araması. `CEterPackManager::RegisterPackAuto()` bir `.vpk` dosyası
bulduğunda, `CEterPack` yerine `CVpkPack` oluşturur. `CVpkPack`, girişlerini
paylaşılan `m_FileDict`'e bir sentinel işaretçisi ile kaydeder. `GetFromPack()`
bir dosya adını çözümlediğinde, işaretçiyi kontrol eder ve şeffaf bir şekilde
`CEterPack::Get2()` veya `CVpkPack::Get2()`'ye yönlendirir.

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
| `EterPackManager_Vpk.cpp` | `RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase` ile yamalı `CEterPackManager` |

### Değiştirilecek dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `source/EterPack/EterPackManager.h` | `EterPackManager_Vpk.h` ile değiştirin (veya eklemeleri birleştirin) |
| `source/EterPack/EterPackManager.cpp` | `EterPackManager_Vpk.cpp` ile değiştirin (veya eklemeleri birleştirin) |
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
4. OpenSSL, LZ4, Zstd, Brotli için include yollarını **Additional Include Directories**'e ekleyin
5. Kütüphane yollarını **Additional Library Directories**'e ekleyin
6. `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` dosyalarını **Additional Dependencies**'e ekleyin

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

### Adım 3: UserInterface.cpp'yi yamalayın

`source/UserInterface/UserInterface.cpp` dosyasında, paket kayıt döngüsü (yaklaşık satır 220):

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

### Adım 4: Derleme

Önce EterPack projesini, ardından tüm çözümü derleyin. VPK kodu mevcut
EterPack kodunun yanında derlenir - hiçbir şey kaldırılmaz.

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
  ├─ ConvertFileName → "d:/ymir work/item/weapon.gr2"
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
  │            └─ CMappedFile belleğe sahiptir (yıkıcıda serbest bırakılır)
  │
  └─ değilse (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [orijinal açma: LZO / Panama / HybridCrypt]
```

### Bellek Yönetimi

CVpkPack, `CMappedFile::AppendDataBlock()` kullanır - CEterPack'in HybridCrypt
verileri için kullandığı aynı mekanizma. Açılmış veriler, CMappedFile'ın sahip
olduğu bir tampona kopyalanır ve CMappedFile kapsam dışına çıktığında otomatik
olarak serbest bırakılır. Manuel temizlik gerekmez.

## Paketleri Dönüştürme

Mevcut paket dosyalarınızı dönüştürmek için 42pak-generator aracını kullanın:

```bash
# Tek bir EPK çiftini VPK'ya dönüştürme
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "gizli-anahtarınız"

# Bir klasörden VPK oluşturma
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "gizli-anahtarınız" --algorithm Zstandard --compression 6

# Bir dizindeki tüm EIX/EPK dosyalarını toplu dönüştürme
42pak-cli migrate ./pack/ ./vpk/ --passphrase "gizli-anahtarınız" --format Standard

# Bir dizini izleme ve otomatik yeniden oluşturma
42pak-cli watch ./gamedata --output game.vpk --passphrase "gizli-anahtarınız"
```

Veya klasörlerden VPK arşivleri oluşturmak için GUI'nin Create görünümünü kullanın.

## Geçiş Stratejisi

1. **Kütüphaneleri kurun** - projenize OpenSSL, LZ4, Zstd, Brotli, BLAKE3 ekleyin
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
