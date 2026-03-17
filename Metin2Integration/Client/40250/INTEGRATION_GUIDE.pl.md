<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Przewodnik integracji z klientem (40250 / ClientVS22)

> **Profil:** 40250 - dedykowany dla architektury wieloszyfrowej HybridCrypt.
> Dla integracji FliegeV3 (XTEA/LZ4), zobacz `../FliegeV3/INTEGRATION_GUIDE.md`.

Bezpośredni zamiennik systemu EterPack (EIX/EPK). Oparty na rzeczywistym
kodzie źródłowym klienta 40250. Wszystkie ścieżki odnoszą się do prawdziwego drzewa źródłowego.

## Co otrzymujesz

| Funkcja | EterPack (stary) | VPK (nowy) |
|---------|-----------------|------------|
| Szyfrowanie | TEA / Panama / HybridCrypt | AES-256-GCM (przyspieszany sprzętowo) |
| Kompresja | LZO | LZ4 / Zstandard / Brotli |
| Integralność | CRC32 per-plik | BLAKE3 per-plik + HMAC-SHA256 archiwum |
| Format pliku | para .eix + .epk | Jeden plik .vpk |
| Zarządzanie kluczami | Panama IV z serwera + HybridCrypt SDB | Hasło PBKDF2-SHA512 |

## Architektura

VPK integruje się poprzez `CEterFileDict` - ten sam mechanizm wyszukiwania hash,
którego używa oryginalny EterPack. Gdy `CEterPackManager::RegisterPackAuto()` znajdzie
plik `.vpk`, tworzy `CVpkPack` zamiast `CEterPack`. `CVpkPack` rejestruje swoje wpisy
we wspólnym `m_FileDict` ze znacznikiem sentinel. Gdy `GetFromPack()` rozwiązuje
nazwę pliku, sprawdza znacznik i przekierowuje do `CEterPack::Get2()` lub
`CVpkPack::Get2()` w sposób przezroczysty.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Pliki

### Nowe pliki do dodania do `source/EterPack/`

| Plik | Przeznaczenie |
|------|---------------|
| `VpkLoader.h` | Klasa `CVpkPack` - bezpośredni zamiennik `CEterPack` |
| `VpkLoader.cpp` | Pełna implementacja: parsowanie nagłówka, tablica wpisów, deszyfrowanie+dekompresja, weryfikacja BLAKE3 |
| `VpkCrypto.h` | Narzędzia kryptograficzne: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementacje z użyciem OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Zmodyfikowany nagłówek `CEterPackManager` z obsługą VPK |
| `EterPackManager_Vpk.cpp` | Zmodyfikowany `CEterPackManager` z `RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase` |

### Pliki do modyfikacji

| Plik | Zmiana |
|------|--------|
| `source/EterPack/EterPackManager.h` | Zastąp plikiem `EterPackManager_Vpk.h` (lub scal zmiany) |
| `source/EterPack/EterPackManager.cpp` | Zastąp plikiem `EterPackManager_Vpk.cpp` (lub scal zmiany) |
| `source/UserInterface/UserInterface.cpp` | Zmiana 2 linii (patrz niżej) |

## Wymagane biblioteki

### OpenSSL 1.1+
- Windows: Pobierz z https://slproweb.com/products/Win32OpenSSL.html
- Nagłówki: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Linkowanie: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Nagłówek: `<lz4.h>`
- Linkowanie: `lz4.lib`

### Zstandard
- https://github.com/facebook/zstd/releases
- Nagłówek: `<zstd.h>`
- Linkowanie: `zstd.lib` (lub `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Nagłówki: `<brotli/decode.h>`
- Linkowanie: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Skopiuj do projektu: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: dodaj również `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

## Integracja krok po kroku

### Krok 1: Dodaj pliki do projektu EterPack w VS

1. Skopiuj `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` do `source/EterPack/`
2. Skopiuj pliki BLAKE3 C do `source/EterPack/` (lub do współdzielonej lokalizacji)
3. Dodaj wszystkie nowe pliki do projektu EterPack w Visual Studio
4. Dodaj ścieżki include dla OpenSSL, LZ4, Zstd, Brotli do **Additional Include Directories**
5. Dodaj ścieżki bibliotek do **Additional Library Directories**
6. Dodaj `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` do **Additional Dependencies**

### Krok 2: Zastąp EterPackManager

**Opcja A (czyste zastąpienie):**
- Zastąp `source/EterPack/EterPackManager.h` plikiem `EterPackManager_Vpk.h`
- Zastąp `source/EterPack/EterPackManager.cpp` plikiem `EterPackManager_Vpk.cpp`
- Zmień nazwy obu na `EterPackManager.h` / `EterPackManager.cpp`

**Opcja B (scalanie):**
Dodaj następujące elementy do istniejącego `EterPackManager.h`:

```cpp
#include "VpkLoader.h"

// Wewnątrz deklaracji klasy, dodaj do public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Wewnątrz deklaracji klasy, dodaj do protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Następnie scal implementacje z `EterPackManager_Vpk.cpp` do istniejącego pliku `.cpp`.

### Krok 3: Zmodyfikuj UserInterface.cpp

W `source/UserInterface/UserInterface.cpp`, pętla rejestracji paczek (około linii 220):

**Przed:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**Po:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

Dodaj tę linię **przed** pętlą rejestracji:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

To wszystko. Łącznie dwie zmiany:
1. `SetVpkPassphrase()` przed pętlą
2. `RegisterPack()` → `RegisterPackAuto()` (2 wystąpienia)

### Krok 4: Kompilacja

Skompiluj najpierw projekt EterPack, a potem całe rozwiązanie. Kod VPK kompiluje się
obok istniejącego kodu EterPack - nic nie jest usuwane.

## Jak to działa

### Przepływ rejestracji

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     zapisuje m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") istnieje?
                 "xyz/")              ├─ TAK → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() dla każdego pliku
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ NIE → RegisterPack() [oryginalny przepływ EPK]
```

### Przepływ dostępu do plików

```
Dowolny kod wywołuje:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → "d:/ymir work/item/weapon.gr2"
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ jeśli compressed_type == -1 (sentinel VPK):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Deszyfrowanie AES-GCM (jeśli zaszyfrowane)
  │       │    ├─ Dekompresja LZ4/Zstd/Brotli (jeśli skompresowane)
  │       │    └─ Weryfikacja hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │            └─ CMappedFile zarządza pamięcią (zwalniana w destruktorze)
  │
  └─ w przeciwnym razie (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [oryginalna dekompresja: LZO / Panama / HybridCrypt]
```

### Zarządzanie pamięcią

CVpkPack używa `CMappedFile::AppendDataBlock()` - tego samego mechanizmu, co
CEterPack dla danych HybridCrypt. Zdekompresowane dane są kopiowane do bufora
zarządzanego przez CMappedFile, który jest automatycznie zwalniany gdy CMappedFile
wychodzi poza zakres. Nie wymaga ręcznego czyszczenia.

## Konwersja paczek

Użyj narzędzia 42pak-generator do konwersji istniejących plików paczek:

```bash
# Konwersja pojedynczej pary EPK do VPK
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "twoje-hasło"

# Budowanie VPK z folderu
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "twoje-hasło" --algorithm Zstandard --compression 6

# Konwersja wsadowa wszystkich EIX/EPK w katalogu
42pak-cli migrate ./pack/ ./vpk/ --passphrase "twoje-hasło" --format Standard

# Obserwowanie katalogu i automatyczne przebudowywanie
42pak-cli watch ./gamedata --output game.vpk --passphrase "twoje-hasło"
```

Lub użyj widoku Create w GUI do budowania archiwów VPK z folderów.

## Strategia migracji

1. **Skonfiguruj biblioteki** - dodaj OpenSSL, LZ4, Zstd, Brotli, BLAKE3 do projektu
2. **Dodaj pliki VPK** - skopiuj 6 nowych plików źródłowych do `source/EterPack/`
3. **Zmodyfikuj EterPackManager** - scal lub zastąp nagłówek i implementację
4. **Zmodyfikuj UserInterface.cpp** - zmiana 2 linii
5. **Skompiluj** - sprawdź czy wszystko się kompiluje
6. **Skonwertuj jedną paczkę** - np. `metin2_patch_etc` -> przetestuj czy się ładuje
7. **Skonwertuj pozostałe paczki** - po jednej lub wszystkie naraz
8. **Usuń stare pliki EPK** - po konwersji wszystkich paczek

Ponieważ `RegisterPackAuto` automatycznie przełącza się na EPK gdy VPK nie istnieje,
możesz konwertować paczki stopniowo bez przerywania działania.

## Konfiguracja hasła

| Metoda | Kiedy używać |
|--------|-------------|
| Zakodowane w źródle | Prywatne serwery, najprostsze podejście |
| Plik konfiguracyjny (`metin2.cfg`) | Łatwa zmiana bez rekompilacji |
| Wysyłane przez serwer przy logowaniu | Maksymalne bezpieczeństwo - hasło zmienia się per sesja |

Dla hasła wysyłanego przez serwer, zmodyfikuj `CAccountConnector` aby odbierał je
w odpowiedzi autoryzacyjnej i wywoływał `CEterPackManager::Instance().SetVpkPassphrase()`
przed jakimkolwiek dostępem do paczek.

## Rozwiązywanie problemów

| Objaw | Przyczyna | Rozwiązanie |
|-------|-----------|-------------|
| Pliki paczek nie znalezione | Brak rozszerzenia `.vpk` | Upewnij się że nazwa paczki nie zawiera rozszerzenia - `RegisterPackAuto` dodaje `.vpk` |
| "HMAC verification failed" | Złe hasło | Sprawdź czy `SetVpkPassphrase` jest wywoływane przed `RegisterPackAuto` |
| Pliki nie znalezione w VPK | Niezgodność wielkości liter | VPK normalizuje do małych liter z separatorami `/` |
| Crash w `Get2()` | Kolizja sentinela `compressed_type` | Upewnij się że żadne pliki EPK nie używają `compressed_type == -1` (żadne standardowe Metin2 tego nie robią) |
| Błąd linkowania LZ4/Zstd/Brotli | Brak biblioteki | Dodaj bibliotekę dekompresji do Additional Dependencies |
| Błąd kompilacji BLAKE3 | Brakujące pliki źródłowe | Upewnij się że wszystkie pliki `blake3_*.c` są w projekcie |
