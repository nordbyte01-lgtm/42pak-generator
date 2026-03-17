<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Przewodnik integracji klienta (MartySama 5.8)

> **Profil:** MartySama 5.8 - skierowany na architekturę wieloszyfrową HybridCrypt opartą na Boost.
> Dla integracji 40250/ClientVS22, zobacz `../40250/INTEGRATION_GUIDE.md`.
> Dla integracji FliegeV3, zobacz `../FliegeV3/INTEGRATION_GUIDE.md`.

Bezpośredni zamiennik systemu EterPack (EIX/EPK). Oparty na rzeczywistym kodzie
źródłowym klienta MartySama 5.8 (`Binary & S-Source/Binary/Client/EterPack/`).
Wszystkie ścieżki plików odnoszą się do rzeczywistego drzewa źródłowego MartySama.

## Co otrzymujesz

| Funkcja | EterPack (stary) | VPK (nowy) |
|---------|-----------------|------------|
| Szyfrowanie | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (przyspieszane sprzętowo) |
| Kompresja | LZO | LZ4 / Zstandard / Brotli |
| Integralność | CRC32 per plik | BLAKE3 per plik + HMAC-SHA256 archiwum |
| Format pliku | para .eix + .epk | Pojedynczy plik .vpk |
| Zarządzanie kluczami | Panama IV wysyłany przez serwer + HybridCrypt SDB | Hasło PBKDF2-SHA512 |

## MartySama 5.8 vs 40250 vs FliegeV3

| Aspekt | 40250 | MartySama 5.8 | FliegeV3 |
|--------|-------|---------------|----------|
| System kryptograficzny | Camellia/Twofish/XTEA (HybridCrypt) | Taki sam jak 40250 | Tylko XTEA |
| Dostarczanie kluczy | Panama IV wysyłany przez serwer + SDB | Taki sam jak 40250 | Statyczne klucze wkompilowane |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| Typy kontenerów | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| Styl nagłówka | Include guards | `#pragma once` | Include guards |
| Ochrona przed modyfikacją | Brak | `#ifdef __THEMIDA__` VM_START/VM_END | Brak |
| Metody managera | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | Takie same jak 40250 | Brak |
| Kompresja | LZO | LZO | LZ4 |
| Struktura indeksu | 192 bajty `#pragma pack(push, 4)` | 192 bajty `#pragma pack(push, 4)` | 192 bajty `#pragma pack(push, 4)` |

Kod integracji VPK dostosowuje się do kontenerów Boost MartySamy i znaczników
Themida, utrzymując ten sam interfejs API (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`).

## Architektura

VPK integruje się przez `CEterFileDict` - tę samą tablicę haszującą
`boost::unordered_multimap`, której używa oryginalny EterPack MartySamy.
Gdy `CEterPackManager::RegisterPackAuto()` znajdzie plik `.vpk`, tworzy
`CVpkPack` zamiast `CEterPack`. `CVpkPack` rejestruje swoje wpisy we
współdzielonym `m_FileDict` ze znacznikiem sentinel. Gdy `GetFromPack()`
rozwiązuje nazwę pliku, sprawdza znacznik i przekierowuje transparentnie
do `CEterPack::Get2()` lub `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Pliki

### Nowe pliki do dodania do `Client/EterPack/`

| Plik | Przeznaczenie |
|------|--------------|
| `VpkLoader.h` | Klasa `CVpkPack` - bezpośredni zamiennik dla `CEterPack` |
| `VpkLoader.cpp` | Pełna implementacja: parsowanie nagłówka, tabela wpisów, deszyfrowanie+dekompresja, weryfikacja BLAKE3 |
| `VpkCrypto.h` | Narzędzia kryptograficzne: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementacje z OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Zmodyfikowany nagłówek `CEterPackManager` z VPK + HybridCrypt + Boost |
| `EterPackManager_Vpk.cpp` | Zmodyfikowany `CEterPackManager` z dyspatczem VPK, HybridCrypt, iteratorami Boost, Themida |

### Pliki do modyfikacji

| Plik | Zmiana |
|------|--------|
| `Client/EterPack/EterPackManager.h` | Zastąpić plikiem `EterPackManager_Vpk.h` (lub scalić) |
| `Client/EterPack/EterPackManager.cpp` | Zastąpić plikiem `EterPackManager_Vpk.cpp` (lub scalić) |
| `Client/UserInterface/UserInterface.cpp` | Zmiana 2 linii (zobacz poniżej) |

## Wymagane biblioteki

### OpenSSL 1.1+
- Windows: Pobierz z https://slproweb.com/products/Win32OpenSSL.html
- Nagłówki: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Linkowanie: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Nagłówek: `<lz4.h>`
- Linkowanie: `lz4.lib`
- **Uwaga:** MartySama 5.8 NIE zawiera LZ4 domyślnie. To nowa zależność.

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
- Skopiować do projektu: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: dodać również `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

### Istniejące zależności (już zawarte w MartySama 5.8)

MartySama 5.8 zawiera już te biblioteki, które **nie** są potrzebne dla VPK,
ale pozostają w projekcie dla starszej ścieżki kodu EPK:

| Biblioteka | Używana przez |
|-----------|-------------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), szyfr Panama, funkcje haszujące |
| Boost | `boost::unordered_map`/`boost::unordered_multimap` w EterPack, EterPackManager, CEterFileDict |
| LZO | Dekompresja EPK (typy paczek 1, 2) |

Te biblioteki nadal działają obok VPK. Nie są wymagane żadne zmiany.

## Integracja krok po kroku

### Krok 1: Dodaj pliki do projektu EterPack w VS

1. Skopiować `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` do `Client/EterPack/`
2. Skopiować pliki C BLAKE3 do `Client/EterPack/` (lub współdzielonej lokalizacji)
3. Dodać wszystkie nowe pliki do projektu EterPack w Visual Studio
4. Dodać ścieżki dołączania dla OpenSSL, LZ4, Zstd, Brotli w **Additional Include Directories**
5. Dodać ścieżki bibliotek w **Additional Library Directories**
6. Dodać `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` w **Additional Dependencies**

### Krok 2: Zastąpić EterPackManager

**Opcja A (czysta wymiana):**
- Zastąpić `Client/EterPack/EterPackManager.h` plikiem `EterPackManager_Vpk.h`
- Zastąpić `Client/EterPack/EterPackManager.cpp` plikiem `EterPackManager_Vpk.cpp`
- Zmienić nazwy obu na `EterPackManager.h` / `EterPackManager.cpp`

**Opcja B (scalanie):**
Dodać to do istniejącego `EterPackManager.h`:

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

**Ważne:** Zwróć uwagę na użycie `boost::unordered_map` zamiast `std::unordered_map`
aby zachować konwencję MartySamy. Dostarczony `EterPackManager_Vpk.h` już
używa kontenerów Boost w całym kodzie.

Następnie scalić implementacje z `EterPackManager_Vpk.cpp` do istniejącego pliku `.cpp`.

### Krok 3: Zmodyfikować UserInterface.cpp

W `Client/UserInterface/UserInterface.cpp`, pętla rejestracji paczek
(około linii 557–579):

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

I dodać tę linię **przed** pętlą rejestracji:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

To wszystko. Łącznie dwie zmiany:
1. `SetVpkPassphrase()` przed pętlą
2. `RegisterPack()` → `RegisterPackAuto()` (2 wystąpienia)

Zobacz `UserInterface_VpkPatch.cpp` dla pełnej annotowanej łatki z przed/po.

### Krok 4: Kompilacja

Najpierw skompilować projekt EterPack, potem pełne rozwiązanie. Kod VPK kompiluje się
obok istniejącego kodu EterPack - nic nie jest usuwane.

**Uwagi dotyczące kompilacji specyficzne dla MartySama 5.8:**
- MartySama używa Boost w całym projekcie. `boost::unordered_multimap` w
  `CEterFileDict` jest w pełni kompatybilny z VPK - `InsertItem()` działa identycznie.
- Upewnij się, że `StdAfx.h` w EterPack zawiera `<boost/unordered_map.hpp>` (powinien
  już go mieć, jeśli projekt MartySama się kompiluje).
- CryptoPP jest już w rozwiązaniu dla HybridCrypt - brak konfliktu z OpenSSL.
  Służą zupełnie innym celom (CryptoPP dla starszego EPK, OpenSSL dla VPK).
- Jeśli używasz Themida, znaczniki `#ifdef __THEMIDA__` w `EterPackManager_Vpk.cpp`
  są już na miejscu. Zdefiniuj `__THEMIDA__` w konfiguracji kompilacji, jeśli chcesz
  mieć aktywne znaczniki VM_START/VM_END.

## Jak to działa

### Przepływ rejestracji

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

### Przepływ dostępu do plików

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

### Współistnienie z HybridCrypt

MartySama 5.8 otrzymuje klucze szyfrowania z serwera przy logowaniu:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

Te metody iterują `m_PackMap` (który zawiera tylko obiekty `CEterPack*`).
Paczki VPK są przechowywane w `m_VpkPackMap` oddzielnie, więc dostarczanie
kluczy HybridCrypt jest całkowicie transparentne - oba systemy współistnieją
bez żadnych zmian w kodzie sieciowym.

### Zarządzanie pamięcią

CVpkPack używa `CMappedFile::AppendDataBlock()` - tego samego mechanizmu, którego
CEterPack używa dla danych HybridCrypt. Zdekompresowane dane są kopiowane do
bufora należącego do CMappedFile, który jest automatycznie zwalniany, gdy
CMappedFile wychodzi poza zakres. Nie jest wymagane ręczne czyszczenie.

## Konwersja paczek

Użyj narzędzia 42pak-generator do konwersji istniejących plików paczek MartySama:

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

Lub użyj widoku Utwórz w interfejsie graficznym, aby budować archiwa VPK z folderów.

## Strategia migracji

1. **Skonfiguruj biblioteki** - dodaj OpenSSL, LZ4, Zstd, Brotli, BLAKE3 do projektu
2. **Dodaj pliki VPK** - skopiuj 6 nowych plików źródłowych do `Client/EterPack/`
3. **Zmodyfikuj EterPackManager** - scal lub zastąp nagłówek i implementację
4. **Zmodyfikuj UserInterface.cpp** - zmiana 2 linii
5. **Kompiluj** - sprawdź, czy wszystko się kompiluje
6. **Skonwertuj jedną paczkę** - np. `metin2_patch_etc` → sprawdź, czy ładuje się poprawnie
7. **Skonwertuj pozostałe paczki** - po jednej lub wszystkie naraz
8. **Usuń stare pliki EPK** - po konwersji wszystkich paczek

Ponieważ `RegisterPackAuto` przechodzi na EPK, gdy VPK nie istnieje, możesz
konwertować paczki stopniowo bez psowania czegokolwiek.

## Konfiguracja hasła

| Metoda | Kiedy używać |
|--------|-------------|
| Zakodowane w źródle | Serwery prywatne, najprostsze podejście |
| Plik konfiguracyjny (`metin2.cfg`) | Łatwe do zmiany bez rekompilacji |
| Wysyłane przez serwer przy logowaniu | Maksymalne bezpieczeństwo - hasło zmienia się per sesja |

Dla hasła wysyłanego przez serwer, zmodyfikuj `CAccountConnector`, aby odbierał je
w odpowiedzi uwierzytelniania i wywołaj `CEterPackManager::Instance().SetVpkPassphrase()`
przed jakimkolwiek dostępem do paczek. `AccountConnector.cpp` MartySamy już obsługuje
niestandardowe pakiety serwera - dodaj nowy handler obok istniejącego wywołania
`RetrieveHybridCryptPackKeys`.

## Rozwiązywanie problemów

| Objaw | Przyczyna | Rozwiązanie |
|-------|-----------|-------------|
| Pliki paczek nie znalezione | Brak rozszerzenia `.vpk` | Upewnij się, że nazwa paczki nie zawiera rozszerzenia - `RegisterPackAuto` dodaje `.vpk` |
| "HMAC verification failed" | Nieprawidłowe hasło | Sprawdź, czy `SetVpkPassphrase` jest wywoływane przed `RegisterPackAuto` |
| Pliki nie znalezione w VPK | Niezgodność wielkości liter w ścieżce | VPK normalizuje do małych liter z separatorami `/` |
| Crash w `Get2()` | Kolizja sentinela `compressed_type` | Upewnij się, że żaden plik EPK nie używa `compressed_type == -1` (żaden nie używa w standardowym Metin2) |
| Błąd linkowania LZ4/Zstd/Brotli | Brakująca biblioteka | Dodaj bibliotekę dekompresji do Additional Dependencies |
| Błąd kompilacji BLAKE3 | Brakujące pliki źródłowe | Upewnij się, że wszystkie pliki `blake3_*.c` są w projekcie |
| Błędy linkera Boost | Brakujące include Boost | Zweryfikuj ścieżki include Boost we właściwościach projektu EterPack |
| Konflikt CryptoPP + OpenSSL | Zduplikowane nazwy symboli | Nie powinno wystąpić - definiują oddzielne symbole. Jeśli wystąpi, sprawdź zanieczyszczenie `using namespace` |
| Błędy Themida | `__THEMIDA__` niezdefiniowane | Zdefiniuj w konfiguracji kompilacji lub usuń bloki `#ifdef __THEMIDA__` z `EterPackManager_Vpk.cpp` |
