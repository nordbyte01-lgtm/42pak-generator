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

Nowoczesny, otwartoźródłowy menedżer plików pak dla społeczności prywatnych serwerów Metin2. Zastępuje przestarzały format archiwów EIX/EPK nowym formatem **VPK** oferującym szyfrowanie AES-256-GCM, kompresję LZ4/Zstandard/Brotli, hashowanie BLAKE3 oraz wykrywanie manipulacji HMAC-SHA256.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

> **Zrzuty ekranu:** Zobacz [PREVIEW.md](PREVIEW.md) — pełna galeria GUI w ciemnym i jasnym motywie.

---

## Funkcje

- **Tworzenie archiwów VPK** — pakowanie katalogów do pojedynczych plików `.vpk` z opcjonalnym szyfrowaniem i kompresją
- **Zarządzanie archiwami** — przeglądanie, wyszukiwanie, wyodrębnianie i walidacja archiwów VPK
- **Konwersja EIX/EPK do VPK** — jednoklikowa migracja z formatu EterPack (obsługa wariantów 40250 i FliegeV3)
- **Szyfrowanie AES-256-GCM** — uwierzytelnione szyfrowanie per-plik z unikalnymi wartościami nonce
- **Kompresja LZ4 / Zstandard / Brotli** — wybierz algorytm dopasowany do potrzeb
- **Hashowanie zawartości BLAKE3** — kryptograficzna weryfikacja integralności każdego pliku
- **HMAC-SHA256** — wykrywanie manipulacji na poziomie archiwum
- **Maskowanie nazw plików** — opcjonalne zaciemnianie ścieżek w archiwum
- **Pełne CLI** — samodzielne narzędzie `42pak-cli` z komendami pack, unpack, list, info, verify, diff, migrate, search, check-duplicates i watch
- **Integracja C++ z Metin2** — gotowy kod loadera klienta i serwera dla drzew źródłowych 40250 i FliegeV3

## Porównanie VPK z EIX/EPK

| Cecha | EIX/EPK (Legacy) | VPK (42pak) |
|-------|:-:|:-:|
| Szyfrowanie | TEA / Panama / HybridCrypt | AES-256-GCM |
| Kompresja | LZO | LZ4 / Zstandard / Brotli |
| Integralność | CRC32 | BLAKE3 + HMAC-SHA256 |
| Format pliku | Dwa pliki (.eix + .epk) | Jeden plik (.vpk) |
| Liczba wpisów | Maks. 512 | Bez limitu |
| Długość nazwy pliku | 160 bajtów | 512 bajtów (UTF-8) |
| Generowanie kluczy | Klucze zakodowane na stałe | PBKDF2-SHA512 (200k iteracji) |
| Wykrywanie manipulacji | Brak | HMAC-SHA256 całe archiwum |

---

## Szybki start

### Wymagania

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 wersja 1809+ (dla WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (zazwyczaj preinstalowany)

### Kompilacja

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### Uruchomienie GUI

```bash
dotnet run --project src/FortyTwoPak.UI
```

### Uruchomienie CLI

```bash
dotnet run --project src/FortyTwoPak.CLI -- <komenda> [opcje]
```

### Uruchomienie testów

```bash
dotnet test
```

### Publikacja (przenośna / instalator)

```powershell
# CLI pojedynczy exe (~65 MB)
.\publish.ps1 -Target CLI

# GUI folder przenośny (~163 MB)
.\publish.ps1 -Target GUI

# Oba + instalator Inno Setup
.\publish.ps1 -Target All
```

---

## Użycie CLI

Samodzielne narzędzie CLI (`42pak-cli`) obsługuje wszystkie operacje:

```
42pak-cli pack <KATALOG_ŹRÓDŁOWY> [--output <PLIK>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <HASŁO>] [--threads <N>] [--dry-run]
42pak-cli unpack <ARCHIWUM> <KATALOG_WYJŚCIOWY> [--passphrase <HASŁO>] [--filter <WZORZEC>]
42pak-cli list <ARCHIWUM> [--passphrase <HASŁO>] [--filter <WZORZEC>] [--json]
42pak-cli info <ARCHIWUM> [--passphrase <HASŁO>] [--json]
42pak-cli verify <ARCHIWUM> [--passphrase <HASŁO>] [--filter <WZORZEC>] [--json]
42pak-cli diff <ARCHIWUM_A> <ARCHIWUM_B> [--passphrase <HASŁO>] [--json]
42pak-cli migrate <ARCHIWUM_LEGACY> [--output <PLIK>] [--compression <TYP>] [--passphrase <HASŁO>]
42pak-cli search <KATALOG_ROBOCZY> <NAZWA_LUB_WZORZEC>
42pak-cli check-duplicates <KATALOG_ROBOCZY> [--read-index]
42pak-cli watch <KATALOG_ŹRÓDŁOWY> [--output <PLIK>] [--debounce <MS>]
```

**Flagi:** `-q` / `--quiet` wycisza wszystkie komunikaty oprócz błędów. `--json` zwraca strukturalny JSON do skryptowania.

**Kody wyjścia:** 0 = sukces, 1 = błąd, 2 = błąd integralności, 3 = nieprawidłowe hasło.

---

## Struktura projektu

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # Biblioteka rdzenna: odczyt/zapis VPK, krypto, kompresja, import legacy
│   │   ├── VpkFormat/               # Nagłówek, wpis, klasy archiwum VPK
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # Kompresory LZ4 / Zstandard / Brotli
│   │   ├── Legacy/                  # Czytnik i konwerter EIX/EPK (40250 + FliegeV3)
│   │   ├── Cli/                     # Współdzielony handler CLI (12 komend)
│   │   └── Utils/                   # Maskowanie nazw plików, raportowanie postępu
│   ├── FortyTwoPak.CLI/             # Samodzielne narzędzie CLI (42pak-cli)
│   ├── FortyTwoPak.UI/              # Aplikacja desktopowa WebView2
│   │   ├── MainWindow.cs            # Host WinForms z kontrolką WebView2
│   │   ├── BridgeService.cs         # Most JavaScript <-> C#
│   │   └── wwwroot/                 # Frontend HTML/CSS/JS (6 zakładek, ciemny/jasny motyw)
│   └── FortyTwoPak.Tests/           # Zestaw testów xUnit (22 testy)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # Integracja C++ dla 40250/ClientVS22 (HybridCrypt)
│   │   └── FliegeV3/                # Integracja C++ dla FliegeV3 (XTEA/LZ4)
│   └── Server/                      # Współdzielony handler VPK po stronie serwera
├── docs/
│   └── FORMAT_SPEC.md               # Specyfikacja formatu binarnego VPK
├── publish.ps1                      # Skrypt publikacji (CLI/GUI/Instalator/Wszystko)
├── installer.iss                    # Skrypt instalatora Inno Setup 6
├── assets/                          # Zrzuty ekranu i obrazy bannerowe
└── build/                           # Katalog wyjściowy kompilacji
```

---

## Format pliku VPK

Jednoplikowe archiwum o następującym układzie binarnym:

```
+-------------------------------------+
| VpkHeader (512 bajtów, stały)       |  Magic "42PK", wersja, liczba wpisów,
|                                     |  flaga szyfrowania, sól, autor itp.
+-------------------------------------+
| Blok danych 0 (wyrównany do 4096)   |  Zawartość pliku (skompresowana + zaszyfrowana)
+-------------------------------------+
| Blok danych 1 (wyrównany do 4096)   |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| Tablica wpisów (zmienny rozmiar)    |  Tablica rekordów VpkEntry. Jeśli zaszyfrowane,
|                                     |  opakowane AES-GCM (nonce + tag + dane).
+-------------------------------------+
| HMAC-SHA256 (32 bajty)              |  Obejmuje wszystko powyżej. Zero jeśli niepodpisane.
+-------------------------------------+
```

### Potok szyfrowania

Dla każdego pliku: `Oryginał -> Kompresja LZ4 -> Szyfrowanie AES-256-GCM -> Zapis`

Przy wyodrębnianiu: `Odczyt -> Deszyfrowanie AES-256-GCM -> Dekompresja LZ4 -> Weryfikacja BLAKE3`

Generowanie kluczy: `PBKDF2-SHA512("42PK-v1:" + hasło, sól, 100000 iteracji) -> 64 bajty`
- Pierwsze 32 bajty: klucz AES-256
- Ostatnie 32 bajty: klucz HMAC-SHA256

Pełna specyfikacja binarna: [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md).

---

## Użycie

### Tworzenie archiwum VPK (GUI)

1. Otwórz 42pak-generator
2. Przejdź do zakładki **Create Pak**
3. Wybierz katalog źródłowy z plikami do zapakowania
4. Wybierz ścieżkę wyjściowego pliku `.vpk`
5. Skonfiguruj opcje:
   - **Szyfrowanie**: Włącz i wprowadź hasło
   - **Poziom kompresji**: 0 (brak) do 12 (maksimum)
   - **Maskowanie nazw plików**: Zaciemnianie ścieżek w archiwum
   - **Autor / Komentarz**: Opcjonalne metadane
6. Kliknij **Build**

### Konwersja EIX/EPK do VPK

1. Przejdź do zakładki **Convert**
2. Wybierz plik `.eix` (plik `.epk` musi być w tym samym katalogu)
3. Wybierz ścieżkę wyjściowego pliku `.vpk`
4. Opcjonalnie ustaw hasło szyfrowania
5. Kliknij **Convert**

### Integracja z klientem Metin2

Dostępne są dwa profile integracyjne dla różnych drzew źródłowych:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### Integracja z serwerem Metin2

Zobacz [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) — odczyt VPK po stronie serwera.

---

## Technologie

| Komponent | Technologia |
|-----------|------------|
| Środowisko | .NET 8.0 (C#) |
| UI | WebView2 (host WinForms) |
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Szyfrowanie | AES-256-GCM przez `System.Security.Cryptography` |
| Generowanie kluczy | PBKDF2-SHA512 (200 000 iteracji) |
| Hashowanie | BLAKE3 przez [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| Kompresja | LZ4 przez [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard przez [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (wbudowany) |
| Wykrywanie manipulacji | HMAC-SHA256 |
| C++ Crypto | OpenSSL 1.1+ |
| Testy | xUnit |

---

## Licencja

Ten projekt jest objęty licencją MIT — szczegóły w pliku [LICENSE](LICENSE).

## Współpraca

Zapraszamy do współpracy! Zobacz [CONTRIBUTING.md](CONTRIBUTING.md).

## Podziękowania

- Społeczność prywatnych serwerów Metin2 za utrzymywanie gry przy życiu
- Oryginalny format EterPack od YMIR Entertainment za dostarczenie fundamentu
- [Zespół BLAKE3](https://github.com/BLAKE3-team/BLAKE3) za szybką funkcję hashującą
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) za binding .NET LZ4
