<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Przewodnik Integracji Serwera

Na podstawie analizy rzeczywistego kodu źródłowego serwera 40250 w
`Server/metin2_server+src/metin2/src/server/`.

## Kluczowe Odkrycie: Serwer Nie Używa EterPack

Serwer 40250 ma **zero** odniesień do EterPack, plików `.eix` ani `.epk`.
Wszystkie operacje I/O na plikach serwera używają zwykłych `fopen()`/`fread()` lub `std::ifstream`:

| Loader | Używany Przez | Plik Źródłowy |
|--------|---------------|---------------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, tabele DragonSoul | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| Bezpośrednie `fopen()` | CONFIG, pliki map, regen, locale, fishing, cube | Różne |

Serwer odwołuje się do paczek tylko w jednym miejscu: **`ClientPackageCryptInfo.cpp`**,
który ładuje klucze szyfrowania z `package.dir/` i wysyła je do **klienta**
w celu odszyfrowania plików `.epk`. Serwer sam nigdy nie otwiera plików paczek.

## Opcje Integracji

### Opcja A: Zachowaj Pliki Serwera Jako Surowe Pliki (Zalecane)

Ponieważ serwer już czyta surowe pliki z dysku, najprostszym podejściem jest:
- Zachowaj wszystkie pliki danych serwera (proto, konfiguracje, mapy) jako surowe pliki
- Używaj VPK tylko po stronie klienta
- Usuń lub zaktualizuj `ClientPackageCryptInfo`, aby nie wysyłał kluczy EPK

To jest zalecane podejście dla większości konfiguracji.

### Opcja B: Pakowanie Danych Serwera w Archiwach VPK

Jeśli chcesz mieć odporne na manipulacje, zaszyfrowane pliki danych serwera, użyj `CVpkHandler`
do czytania z archiwów VPK zamiast surowych plików.

## Pliki

| Plik | Przeznaczenie |
|------|---------------|
| `VpkHandler.h` | Samodzielny czytnik VPK (bez zależności od EterPack) |
| `VpkHandler.cpp` | Pełna implementacja z wbudowaną kryptografią |

Handler serwera jest całkowicie samodzielny – zawiera własne implementacje
AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard i Brotli
wbudowane. Brak zależności od jakiegokolwiek kodu EterPack po stronie klienta.

## Wymagane Biblioteki

### Linux (typowy serwer)

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

### Dodatki do Makefile

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## Przykłady Integracji

### Przykład 1: Ładowanie Danych Proto z VPK

Serwer DB ładuje proto z plików CSV/binarnych w `db/src/ClientManagerBoot.cpp`:

**Przed (surowy plik):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**Po (VPK):**
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
    // przetwarzanie elementów...
    return true;
}
```

### Przykład 2: Czytanie Plików Tekstowych z VPK

Dla plików w stylu `CTextFileLoader` (mob_group.txt, itd.):

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
    // parsowanie jak poprzednio...
    return true;
}
```

### Przykład 3: Aktualizacja ClientPackageCryptInfo

Serwer 40250 wysyła klucze szyfrowania EPK do klienta przez
`game/src/ClientPackageCryptInfo.cpp` i `game/src/panama.cpp`.

Przy użyciu VPK masz dwie opcje:

**Opcja A: Całkowite usunięcie wysyłania kluczy paczek**
Jeśli wszystkie paczki zostały skonwertowane do VPK, klient nie potrzebuje już
kluczy EPK wysyłanych przez serwer. Usuń lub wyłącz `LoadClientPackageCryptInfo()` i `PanamaLoad()`
z sekwencji uruchamiania w `game/src/main.cpp`.

**Opcja B: Zachowanie wsparcia hybrydowego**
Jeśli niektóre paczki pozostają jako EPK, a inne jako VPK, zachowaj istniejący
kod wysyłania kluczy dla paczek EPK. Paczki VPK używają własnego mechanizmu
passphrase i nie potrzebują kluczy wysyłanych przez serwer.

### Przykład 4: Komendy Admina

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

## Zarządzanie Passphrase

Na serwerze przechowuj passphrase VPK bezpiecznie:

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

## Wsparcie Kompresji

Handler serwera obsługuje wszystkie trzy algorytmy kompresji:

| Algorytm | ID | Biblioteka | Szybkość | Współczynnik |
|----------|----|------------|----------|--------------|
| LZ4 | 1 | liblz4 | Najszybszy | Dobry |
| Zstandard | 2 | libzstd | Szybki | Lepszy |
| Brotli | 3 | libbrotli | Wolniejszy | Najlepszy |

Algorytm jest przechowywany w nagłówku VPK i wykrywany automatycznie.
Nie wymaga konfiguracji po stronie odczytu.

## Układ Katalogów

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← dane lokalizacji
│   ├── locale_de.vpk
│   ├── proto.vpk             ← tabele item/mob proto
│   └── scripts.vpk           ← skrypty questów, konfiguracje
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## Uwagi Dotyczące Wydajności

- Odczyty plików VPK to sekwencyjne I/O (jedno szukanie + jeden odczyt na plik)
- Tabela wpisów jest parsowana raz przy `Open()` i buforowana w pamięci
- Dla często odczytywanych plików rozważ buforowanie wyniku `ReadFile()`
- Weryfikacja skrótu BLAKE3 dodaje ~0,1ms na MB danych

## Wykrywanie Manipulacji

HMAC-SHA256 na końcu każdego pliku VPK zapewnia:
- Modyfikacje zawartości dowolnego pliku są wykrywane
- Dodane lub usunięte wpisy są wykrywane
- Manipulacje nagłówka są wykrywane

Jeśli `Open()` zwraca false dla zaszyfrowanego VPK, archiwum mogło zostać
zmanipulowane lub passphrase jest nieprawidłowe.
- Dekompresja LZ4 dodaje ~0,5ms na MB skompresowanych danych
