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

Metin2 özel sunucu topluluğu için modern, açık kaynaklı bir pak dosya yöneticisi. Eski EIX/EPK arşiv formatını, AES-256-GCM şifreleme, LZ4/Zstandard/Brotli sıkıştırma, BLAKE3 hashleme ve HMAC-SHA256 müdahale algılama özelliklerine sahip yeni **VPK** formatıyla değiştirir.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

> **Ekran görüntüleri:** Koyu ve açık temadaki GUI'nin tam galerisini görmek için [PREVIEW.md](PREVIEW.md) dosyasına bakın.

---

## Özellikler

- **VPK arşivleri oluşturma** — Dizinleri isteğe bağlı şifreleme ve sıkıştırma ile tek `.vpk` dosyalarına paketleme
- **Mevcut arşivleri yönetme** — VPK arşivlerini görüntüleme, arama, çıkarma ve doğrulama
- **EIX/EPK'yı VPK'ya dönüştürme** — Eski EterPack formatından tek tıkla geçiş (40250 ve FliegeV3 varyantlarını destekler)
- **AES-256-GCM şifreleme** — Benzersiz nonce'larla dosya bazında doğrulanmış şifreleme
- **LZ4 / Zstandard / Brotli sıkıştırma** — İhtiyaçlarınıza uygun algoritmayı seçin
- **BLAKE3 içerik hashleme** — Her dosya için kriptografik bütünlük doğrulaması
- **HMAC-SHA256** — Arşiv düzeyinde müdahale algılama
- **Dosya adı maskeleme** — Arşiv içindeki dosya yollarının isteğe bağlı gizlenmesi
- **Tam CLI** — pack, unpack, list, info, verify, diff, migrate, search, check-duplicates ve watch komutlarıyla bağımsız `42pak-cli` aracı
- **Metin2 C++ entegrasyonu** — 40250 ve FliegeV3 kaynak ağaçları için hazır istemci ve sunucu yükleyici kodu

## VPK vs EIX/EPK Karşılaştırması

| Özellik | EIX/EPK (Eski) | VPK (42pak) |
|---------|:-:|:-:|
| Şifreleme | TEA / Panama / HybridCrypt | AES-256-GCM |
| Sıkıştırma | LZO | LZ4 / Zstandard / Brotli |
| Bütünlük | CRC32 | BLAKE3 + HMAC-SHA256 |
| Dosya formatı | Çift dosya (.eix + .epk) | Tek dosya (.vpk) |
| Arşiv sayısı | Maks. 512 giriş | Sınırsız |
| Dosya adı uzunluğu | 160 bayt | 512 bayt (UTF-8) |
| Anahtar türetme | Sabit kodlu anahtarlar | PBKDF2-SHA512 (200k iterasyon) |
| Müdahale algılama | Yok | HMAC-SHA256 tüm arşiv |

---

## Hızlı Başlangıç

### Ön Koşullar

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 sürüm 1809+ (WebView2 için)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (genellikle önceden yüklü)

### Derleme

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### GUI'yi Çalıştırma

```bash
dotnet run --project src/FortyTwoPak.UI
```

### CLI'yi Çalıştırma

```bash
dotnet run --project src/FortyTwoPak.CLI -- <komut> [seçenekler]
```

### Testleri Çalıştırma

```bash
dotnet test
```

### Yayınlama (Taşınabilir / Yükleyici)

```powershell
# CLI tek exe (~65 MB)
.\publish.ps1 -Target CLI

# GUI taşınabilir klasör (~163 MB)
.\publish.ps1 -Target GUI

# Her ikisi + Inno Setup yükleyici
.\publish.ps1 -Target All
```

---

## CLI Kullanımı

Bağımsız CLI (`42pak-cli`) tüm işlemleri destekler:

```
42pak-cli pack <KAYNAK_DİZİN> [--output <DOSYA>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <PAROLA>] [--threads <N>] [--dry-run]
42pak-cli unpack <ARŞİV> <ÇIKIŞ_DİZİNİ> [--passphrase <PAROLA>] [--filter <DESEN>]
42pak-cli list <ARŞİV> [--passphrase <PAROLA>] [--filter <DESEN>] [--json]
42pak-cli info <ARŞİV> [--passphrase <PAROLA>] [--json]
42pak-cli verify <ARŞİV> [--passphrase <PAROLA>] [--filter <DESEN>] [--json]
42pak-cli diff <ARŞİV_A> <ARŞİV_B> [--passphrase <PAROLA>] [--json]
42pak-cli migrate <ESKİ_ARŞİV> [--output <DOSYA>] [--compression <TİP>] [--passphrase <PAROLA>]
42pak-cli search <ÇALIŞMA_DİZİNİ> <DOSYA_ADI_VEYA_DESEN>
42pak-cli check-duplicates <ÇALIŞMA_DİZİNİ> [--read-index]
42pak-cli watch <KAYNAK_DİZİN> [--output <DOSYA>] [--debounce <MS>]
```

**Bayraklar:** `-q` / `--quiet` hatalar dışındaki tüm çıktıyı bastırır. `--json` betiklenebilir pipeline'lar için yapılandırılmış JSON çıktısı verir.

**Çıkış kodları:** 0 = başarılı, 1 = hata, 2 = bütünlük hatası, 3 = parola yanlış.

---

## Proje Yapısı

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # Çekirdek kütüphane: VPK okuma/yazma, kripto, sıkıştırma, eski format içe aktarma
│   │   ├── VpkFormat/               # VPK başlık, giriş, arşiv sınıfları
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # LZ4 / Zstandard / Brotli sıkıştırıcılar
│   │   ├── Legacy/                  # EIX/EPK okuyucu ve dönüştürücü (40250 + FliegeV3)
│   │   ├── Cli/                     # Paylaşılan CLI işleyicisi (12 komut)
│   │   └── Utils/                   # Dosya adı maskeleme, ilerleme raporlama
│   ├── FortyTwoPak.CLI/             # Bağımsız CLI aracı (42pak-cli)
│   ├── FortyTwoPak.UI/              # WebView2 masaüstü uygulaması
│   │   ├── MainWindow.cs            # WebView2 denetimi ile WinForms ana bilgisayar
│   │   ├── BridgeService.cs         # JavaScript <-> C# köprü
│   │   └── wwwroot/                 # HTML/CSS/JS ön yüz (6 sekme, koyu/açık tema)
│   └── FortyTwoPak.Tests/           # xUnit test paketi (22 test)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # 40250/ClientVS22 için C++ entegrasyonu (HybridCrypt)
│   │   └── FliegeV3/                # FliegeV3 için C++ entegrasyonu (XTEA/LZ4)
│   └── Server/                      # Paylaşılan sunucu tarafı VPK işleyicisi
├── docs/
│   └── FORMAT_SPEC.md               # VPK ikili format spesifikasyonu
├── publish.ps1                      # Yayın betiği (CLI/GUI/Yükleyici/Tümü)
├── installer.iss                    # Inno Setup 6 yükleyici betiği
├── assets/                          # Ekran görüntüleri ve banner görselleri
└── build/                           # Derleme çıktısı
```

---

## VPK Dosya Formatı

Aşağıdaki ikili düzene sahip tek dosyalı arşiv:

```
+-------------------------------------+
| VpkHeader (512 bayt, sabit)         |  Magic "42PK", sürüm, giriş sayısı,
|                                     |  şifreleme bayrağı, tuz, yazar vb.
+-------------------------------------+
| Veri Bloğu 0 (4096'ya hizalı)       |  Dosya içeriği (sıkıştırılmış + şifrelenmiş)
+-------------------------------------+
| Veri Bloğu 1 (4096'ya hizalı)       |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| Giriş Tablosu (değişken boyut)      |  VpkEntry kayıtları dizisi. Şifreliyse,
|                                     |  AES-GCM ile sarılmış (nonce + tag + veri).
+-------------------------------------+
| HMAC-SHA256 (32 bayt)               |  Yukarıdaki her şeyi kapsar. İmzasızsa sıfır.
+-------------------------------------+
```

### Şifreleme Pipeline'ı

Her dosya için: `Orijinal -> LZ4 Sıkıştırma -> AES-256-GCM Şifreleme -> Depolama`

Çıkarmada: `Okuma -> AES-256-GCM Şifre Çözme -> LZ4 Açma -> BLAKE3 Doğrulama`

Anahtar türetme: `PBKDF2-SHA512("42PK-v1:" + parola, tuz, 100000 iterasyon) -> 64 bayt`
- İlk 32 bayt: AES-256 anahtarı
- Son 32 bayt: HMAC-SHA256 anahtarı

Tam ikili spesifikasyon için [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md) dosyasına bakın.

---

## Kullanım

### VPK Arşivi Oluşturma (GUI)

1. 42pak-generator'ü açın
2. **Create Pak** sekmesine gidin
3. Paketlenecek dosyaları içeren kaynak dizini seçin
4. Çıktı `.vpk` dosya yolunu belirleyin
5. Seçenekleri yapılandırın:
   - **Şifreleme**: Açın ve bir parola girin
   - **Sıkıştırma Seviyesi**: 0 (yok) ile 12 (maksimum) arası
   - **Dosya Adı Maskeleme**: Arşiv içindeki yolları gizleme
   - **Yazar / Yorum**: İsteğe bağlı meta veriler
6. **Build**'e tıklayın

### EIX/EPK'yı VPK'ya Dönüştürme

1. **Convert** sekmesine gidin
2. `.eix` dosyasını seçin (`.epk` aynı dizinde olmalıdır)
3. Çıktı `.vpk` yolunu seçin
4. İsteğe bağlı olarak şifreleme parolası ayarlayın
5. **Convert**'e tıklayın

### Metin2 İstemci Entegrasyonu

Farklı kaynak ağaçları için iki entegrasyon profili sağlanmaktadır:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### Metin2 Sunucu Entegrasyonu

Sunucu tarafında VPK okuma için [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) dosyasına bakın.

---

## Teknolojiler

| Bileşen | Teknoloji |
|---------|------------|
| Çalışma zamanı | .NET 8.0 (C#) |
| Kullanıcı arayüzü | WebView2 (WinForms ana bilgisayar) |
| Ön yüz | HTML5, CSS3, Vanilla JavaScript |
| Şifreleme | AES-256-GCM, `System.Security.Cryptography` ile |
| Anahtar türetme | PBKDF2-SHA512 (200.000 iterasyon) |
| Hashleme | BLAKE3, [blake3 NuGet](https://www.nuget.org/packages/Blake3/) ile |
| Sıkıştırma | LZ4: [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard: [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (yerleşik) |
| Müdahale algılama | HMAC-SHA256 |
| C++ Kripto | OpenSSL 1.1+ |
| Test | xUnit |

---

## Lisans

Bu proje MIT Lisansı altında lisanslanmıştır — ayrıntılar için [LICENSE](LICENSE) dosyasına bakın.

## Katkıda Bulunma

Katkılarınızı bekliyoruz! Yönergeler için [CONTRIBUTING.md](CONTRIBUTING.md) dosyasına bakın.

## Teşekkürler

- Oyunu yaşatan Metin2 özel sunucu topluluğuna
- Temel oluşturan orijinal YMIR Entertainment EterPack formatına
- Hızlı hash fonksiyonu için [BLAKE3 ekibine](https://github.com/BLAKE3-team/BLAKE3)
- .NET LZ4 binding'i için [K4os'a](https://github.com/MiloszKrajewski/K4os.Compression.LZ4)
