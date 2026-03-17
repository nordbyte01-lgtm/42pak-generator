<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Guida all'Integrazione Server

Basato sull'analisi del codice sorgente reale del server 40250 in
`Server/metin2_server+src/metin2/src/server/`.

## Scoperta Chiave: Il Server Non Usa EterPack

Il server 40250 ha **zero** riferimenti a EterPack, file `.eix` o `.epk`.
Tutte le operazioni I/O del server usano `fopen()`/`fread()` semplice o `std::ifstream`:

| Loader | Usato Da | File Sorgente |
|--------|---------|---------------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, tabelle DragonSoul | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| `fopen()` diretto | CONFIG, file mappa, regen, locale, fishing, cube | Vari |

Il server fa riferimento ai pack solo in un punto: **`ClientPackageCryptInfo.cpp`**,
che carica le chiavi di crittografia da `package.dir/` e le invia al **client**
per decrittare i file `.epk`. Il server stesso non apre mai file pack.

## Opzioni di Integrazione

### Opzione A: Mantieni i File del Server Come File Raw (Consigliato)

Poiché il server legge già file raw dal disco, l'approccio più semplice è:
- Mantenere tutti i file dati del server (proto, config, mappe) come file raw
- Usare VPK solo lato client
- Rimuovere o aggiornare `ClientPackageCryptInfo` per non inviare chiavi EPK

Questo è l'approccio consigliato per la maggior parte delle configurazioni.

### Opzione B: Impacchettare i Dati del Server in Archivi VPK

Se desideri file dati server resistenti alle manomissioni e crittografati, usa `CVpkHandler`
per leggere da archivi VPK invece che da file raw.

## File

| File | Scopo |
|------|-------|
| `VpkHandler.h` | Lettore VPK autonomo (nessuna dipendenza da EterPack) |
| `VpkHandler.cpp` | Implementazione completa con crittografia integrata |

L'handler del server è completamente autonomo – include le sue implementazioni
di AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard e Brotli
integrate. Nessuna dipendenza da codice EterPack lato client.

## Librerie Necessarie

### Linux (server tipico)

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

### Aggiunte al Makefile

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## Esempi di Integrazione

### Esempio 1: Caricare Dati Proto da VPK

Il server DB carica i proto da file CSV/binari in `db/src/ClientManagerBoot.cpp`:

**Prima (file raw):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**Dopo (VPK):**
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
    // elabora elementi...
    return true;
}
```

### Esempio 2: Leggere File di Testo da VPK

Per file in stile `CTextFileLoader` (mob_group.txt, ecc.):

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
    // parsing come prima...
    return true;
}
```

### Esempio 3: Aggiornare ClientPackageCryptInfo

Il server 40250 invia chiavi di crittografia EPK al client tramite
`game/src/ClientPackageCryptInfo.cpp` e `game/src/panama.cpp`.

Quando si usa VPK, hai due opzioni:

**Opzione A: Rimuovere completamente l'invio delle chiavi di pack**
Se tutti i pack sono stati convertiti in VPK, il client non ha più bisogno di
chiavi EPK inviate dal server. Rimuovi o disabilita `LoadClientPackageCryptInfo()` e `PanamaLoad()`
dalla sequenza di avvio in `game/src/main.cpp`.

**Opzione B: Mantenere il supporto ibrido**
Se alcuni pack rimangono come EPK mentre altri sono VPK, mantieni il
codice di invio chiavi esistente per i pack EPK. I pack VPK usano il proprio
meccanismo di passphrase e non necessitano di chiavi inviate dal server.

### Esempio 4: Comandi Admin

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

## Gestione della Passphrase

Sul server, conserva la passphrase VPK in modo sicuro:

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

## Supporto Compressione

L'handler del server supporta tutti e tre gli algoritmi di compressione:

| Algoritmo | ID | Libreria | Velocità | Rapporto |
|-----------|----|----------|----------|----------|
| LZ4 | 1 | liblz4 | Il più veloce | Buono |
| Zstandard | 2 | libzstd | Veloce | Migliore |
| Brotli | 3 | libbrotli | Più lento | Il migliore |

L'algoritmo è memorizzato nell'header VPK e rilevato automaticamente.
Non è necessaria alcuna configurazione lato lettura.

## Struttura delle Directory

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← dati di localizzazione
│   ├── locale_de.vpk
│   ├── proto.vpk             ← tabelle item/mob proto
│   └── scripts.vpk           ← script quest, configurazioni
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## Note sulle Prestazioni

- Le letture di file VPK sono I/O sequenziale (una ricerca + una lettura per file)
- La tabella delle voci viene analizzata una volta in `Open()` e memorizzata nella cache
- Per file ad accesso frequente, considera di memorizzare nella cache l'output di `ReadFile()`
- La verifica dell'hash BLAKE3 aggiunge ~0,1ms per MB di dati

## Rilevamento Manomissioni

L'HMAC-SHA256 alla fine di ogni file VPK garantisce:
- Le modifiche a qualsiasi contenuto di file vengono rilevate
- Le voci aggiunte o rimosse vengono rilevate
- Le manomissioni dell'header vengono rilevate

Se `Open()` restituisce false per un VPK crittografato, l'archivio potrebbe essere stato
manomesso o la passphrase è sbagliata.
- La decompressione LZ4 aggiunge ~0,5ms per MB di dati compressi
