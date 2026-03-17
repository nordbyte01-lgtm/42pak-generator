<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Ghid de Integrare Server

Bazat pe analiza codului sursă real al serverului 40250 din
`Server/metin2_server+src/metin2/src/server/`.

## Descoperire Cheie: Serverul Nu Folosește EterPack

Serverul 40250 are **zero** referințe la EterPack, fișiere `.eix` sau `.epk`.
Toate operațiunile I/O ale serverului folosesc `fopen()`/`fread()` simplu sau `std::ifstream`:

| Loader | Folosit De | Fișier Sursă |
|--------|-----------|--------------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, tabele DragonSoul | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| `fopen()` direct | CONFIG, fișiere hartă, regen, locale, fishing, cube | Diverse |

Serverul face referire la pachete doar într-un singur loc: **`ClientPackageCryptInfo.cpp`**,
care încarcă cheile de criptare din `package.dir/` și le trimite **clientului**
pentru decriptarea fișierelor `.epk`. Serverul însuși nu deschide niciodată fișiere pachet.

## Opțiuni de Integrare

### Opțiunea A: Păstrează Fișierele Serverului Ca Fișiere Raw (Recomandat)

Deoarece serverul citește deja fișiere raw de pe disc, cea mai simplă abordare este:
- Păstrează toate fișierele de date ale serverului (proto, configurări, hărți) ca fișiere raw
- Folosește VPK doar pe partea de client
- Elimină sau actualizează `ClientPackageCryptInfo` pentru a nu trimite chei EPK

Aceasta este abordarea recomandată pentru majoritatea configurărilor.

### Opțiunea B: Împachetează Datele Serverului în Arhive VPK

Dacă dorești fișiere de date server rezistente la manipulare și criptate, folosește `CVpkHandler`
pentru a citi din arhive VPK în loc de fișiere raw.

## Fișiere

| Fișier | Scop |
|--------|------|
| `VpkHandler.h` | Cititor VPK autonom (fără dependență EterPack) |
| `VpkHandler.cpp` | Implementare completă cu criptografie integrată |

Handler-ul serverului este complet autonom – include propriile implementări
AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard și Brotli
integrate. Nicio dependență de vreun cod EterPack de pe partea clientului.

## Biblioteci Necesare

### Linux (server tipic)

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

### Adăugiri la Makefile

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## Exemple de Integrare

### Exemplul 1: Încărcarea Datelor Proto din VPK

Serverul DB încarcă proto-urile din fișiere CSV/binare în `db/src/ClientManagerBoot.cpp`:

**Înainte (fișier raw):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**După (VPK):**
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
    // procesare elemente...
    return true;
}
```

### Exemplul 2: Citirea Fișierelor Text din VPK

Pentru fișiere în stil `CTextFileLoader` (mob_group.txt, etc.):

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
    // parsare ca înainte...
    return true;
}
```

### Exemplul 3: Actualizarea ClientPackageCryptInfo

Serverul 40250 trimite chei de criptare EPK către client prin
`game/src/ClientPackageCryptInfo.cpp` și `game/src/panama.cpp`.

Când folosești VPK, ai două opțiuni:

**Opțiunea A: Eliminarea completă a trimiterii cheilor de pachet**
Dacă toate pachetele au fost convertite la VPK, clientul nu mai are nevoie de
chei EPK trimise de server. Elimină sau dezactivează `LoadClientPackageCryptInfo()` și `PanamaLoad()`
din secvența de pornire în `game/src/main.cpp`.

**Opțiunea B: Păstrarea suportului hibrid**
Dacă unele pachete rămân ca EPK în timp ce altele sunt VPK, păstrează codul
existent de trimitere a cheilor pentru pachetele EPK. Pachetele VPK folosesc propriul
mecanism de passphrase și nu au nevoie de chei trimise de server.

### Exemplul 4: Comenzi de Admin

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

## Gestionarea Passphrase-ului

Pe server, stochează passphrase-ul VPK în siguranță:

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

## Suport Compresie

Handler-ul serverului suportă toți cei trei algoritmi de compresie:

| Algoritm | ID | Bibliotecă | Viteză | Raport |
|----------|----|-----------|--------|--------|
| LZ4 | 1 | liblz4 | Cel mai rapid | Bun |
| Zstandard | 2 | libzstd | Rapid | Mai bun |
| Brotli | 3 | libbrotli | Mai lent | Cel mai bun |

Algoritmul este stocat în header-ul VPK și detectat automat.
Nu este necesară nicio configurare pe partea de citire.

## Structura Directoarelor

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← date de localizare
│   ├── locale_de.vpk
│   ├── proto.vpk             ← tabele item/mob proto
│   └── scripts.vpk           ← scripturi quest, configurări
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## Note de Performanță

- Citirile fișierelor VPK sunt I/O secvențial (o căutare + o citire per fișier)
- Tabela de intrări este parsată o dată la `Open()` și stocată în cache în memorie
- Pentru fișierele accesate frecvent, ia în considerare stocarea în cache a rezultatului `ReadFile()`
- Verificarea hash-ului BLAKE3 adaugă ~0,1ms per MB de date

## Detectarea Manipulării

HMAC-SHA256 de la sfârșitul fiecărui fișier VPK asigură:
- Modificările oricărui conținut de fișier sunt detectate
- Intrările adăugate sau eliminate sunt detectate
- Manipularea header-ului este detectată

Dacă `Open()` returnează false pentru un VPK criptat, arhiva ar fi putut fi
manipulată sau passphrase-ul este greșit.
- Decompresia LZ4 adaugă ~0,5ms per MB de date comprimate
