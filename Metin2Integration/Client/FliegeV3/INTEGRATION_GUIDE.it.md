<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Guida all'Integrazione Client FliegeV3

Sostituzione diretta del sistema EterPack (EIX/EPK). Basato sul codice sorgente reale del client
FliegeV3 binary-src. Tutti i percorsi si riferiscono all'albero sorgente FliegeV3 reale.

## Cosa Ottieni

| Funzionalità | EterPack (FliegeV3) | VPK (Nuovo) |
|-------------|---------------------|-------------|
| Crittografia | XTEA (chiave 128-bit, 32 round) | AES-256-GCM (accelerato via hardware) |
| Compressione | LZ4 (migrato da LZO) | LZ4 / Zstandard / Brotli |
| Integrità | CRC32 per file | BLAKE3 per file + HMAC-SHA256 dell'archivio |
| Formato File | Coppia .eix + .epk | Singolo file .vpk |
| Gestione Chiavi | Chiavi XTEA statiche codificate nel codice | Passphrase PBKDF2-SHA512 |
| Voci Indice | `TEterPackIndex` da 192 byte | Voci VPK a lunghezza variabile |

## FliegeV3 vs 40250 – Perché Profili Separati?

FliegeV3 differisce significativamente dal setup 40250:

| Aspetto | 40250 | FliegeV3 |
|---------|-------|----------|
| Sistema di Crittografia | Ibrido Camellia/Twofish/XTEA (HybridCrypt) | Solo XTEA |
| Distribuzione Chiavi | Panama IV dal server + blocchi SDB | Chiavi statiche compilate |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` |
| Metodi del Manager | `DecryptPackIV`, `WriteHybridCryptPackInfo`, `RetrieveHybridCryptPackKeys/SDB` | Nessuno di questi metodi |
| Ricerca File | Sempre permette file locali | Condizionale `ENABLE_LOCAL_FILE_LOADING` |
| Struttura Indice | 169 byte (#pragma pack(push,4) nome 161 char) | 192 byte (stessa struct, allineamento/padding diverso) |

Il codice di integrazione VPK si adatta a queste differenze mantenendo la stessa API
(`RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase`).

## Architettura

Il VPK si integra attraverso `CEterFileDict` – lo stesso `boost::unordered_multimap`
utilizzato dall'EterPack originale di FliegeV3. Quando `RegisterPackAuto()` trova un file
`.vpk`, crea un `CVpkPack` invece di `CEterPack`. `CVpkPack` registra le sue voci
nel `m_FileDict` condiviso con un marcatore sentinel (`compressed_type == -1`).

Quando `GetFromPack()` risolve un nome file, controlla il marcatore e invia
trasparentemente a `CEterPack::Get2()` o `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## File

### Nuovi File da Aggiungere a `source/EterPack/`

| File | Scopo |
|------|-------|
| `VpkLoader.h` | Classe `CVpkPack` – sostituzione diretta di `CEterPack` |
| `VpkLoader.cpp` | Implementazione completa: parsing header, tabella voci, decrittazione+decompressione, verifica BLAKE3 |
| `VpkCrypto.h` | Utility crittografiche: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementazione con OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Header `CEterPackManager` modificato con supporto VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificato – senza HybridCrypt, logica di ricerca FliegeV3 |

### File da Modificare

| File | Modifica |
|------|----------|
| `source/EterPack/EterPackManager.h` | Sostituire con `EterPackManager_Vpk.h` (o unire) |
| `source/EterPack/EterPackManager.cpp` | Sostituire con `EterPackManager_Vpk.cpp` (o unire) |
| `source/UserInterface/UserInterface.cpp` | Cambiare 2 righe (vedi sotto) |

## Librerie Necessarie

### OpenSSL 1.1+
- Windows: Scaricare da https://slproweb.com/products/Win32OpenSSL.html
- Header: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Linkare: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Linkare: `lz4.lib`
- **Nota:** FliegeV3 usa già LZ4 per la compressione dei pack. Se il tuo progetto
  già linka `lz4.lib`, non è necessaria alcuna configurazione aggiuntiva.

### Zstandard
- https://github.com/facebook/zstd/releases
- Header: `<zstd.h>`
- Linkare: `zstd.lib` (o `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Header: `<brotli/decode.h>`
- Linkare: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copiare nel progetto: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: Aggiungere anche `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

## Integrazione Passo per Passo

### Passo 1: Aggiungere File al Progetto EterPack in VS

1. Copiare `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` in `source/EterPack/`
2. Copiare i file C di BLAKE3 in `source/EterPack/` (o posizione condivisa)
3. Aggiungere tutti i nuovi file al progetto EterPack in Visual Studio
4. Aggiungere percorsi include per OpenSSL, Zstd, Brotli in **Additional Include Directories**
   - LZ4 dovrebbe essere già configurato se la tua build FliegeV3 lo usa
5. Aggiungere percorsi libreria in **Additional Library Directories**
6. Aggiungere `libssl.lib`, `libcrypto.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` in **Additional Dependencies**
   - `lz4.lib` dovrebbe essere già linkato

### Passo 2: Sostituire EterPackManager

**Opzione A (Sostituzione pulita):**
- Sostituire `source/EterPack/EterPackManager.h` con `EterPackManager_Vpk.h`
- Sostituire `source/EterPack/EterPackManager.cpp` con `EterPackManager_Vpk.cpp`
- Rinominare entrambi in `EterPackManager.h` / `EterPackManager.cpp`

**Opzione B (Unione):**
Aggiungere questo al `EterPackManager.h` esistente:

```cpp
#include "VpkLoader.h"

// Dentro la dichiarazione della classe, aggiungere in public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Dentro la dichiarazione della classe, aggiungere in protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Poi unire l'implementazione da `EterPackManager_Vpk.cpp` nel file `.cpp` esistente.

**Importante:** Non aggiungere `DecryptPackIV`, `WriteHybridCryptPackInfo`,
`RetrieveHybridCryptPackKeys` o `RetrieveHybridCryptPackSDB` – FliegeV3
non ha questi metodi.

### Passo 3: Modificare UserInterface.cpp

In `source/UserInterface/UserInterface.cpp`, nel loop di registrazione pack:

**Prima:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**Dopo:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

E aggiungere questa riga **prima** del loop di registrazione:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Questo è tutto. Due modifiche in totale:
1. `SetVpkPassphrase()` prima del loop
2. `RegisterPack()` → `RegisterPackAuto()` (2 posizioni)

**Nota:** A differenza dell'integrazione 40250, FliegeV3 non richiede patch del codice di rete.
Non ci sono chiamate `RetrieveHybridCryptPackKeys` o `RetrieveHybridCryptPackSDB`
di cui preoccuparsi.

### Passo 4: Compilare

Compilare prima il progetto EterPack, poi compilare l'intera soluzione. Il codice VPK compila
insieme al codice EterPack esistente – nulla viene rimosso.

**Note di compilazione specifiche per FliegeV3:**
- FliegeV3 usa Boost per `boost::unordered_multimap` in `CEterFileDict`. Questo è compatibile
  con VPK – `InsertItem()` funziona allo stesso modo.
- Assicurarsi che `StdAfx.h` in EterPack includa `<boost/unordered_map.hpp>`
  (dovrebbe essere già presente se il tuo progetto FliegeV3 compila)
- Se vedi errori linker riguardo `boost::unordered_multimap`, controlla i
  percorsi include di Boost nelle proprietà del progetto EterPack

## Come Funziona

### Flusso di Registrazione

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     Salva m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") esiste?
                 "xyz/")              ├─ Sì → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() per ogni file
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ No → RegisterPack() [percorso EPK originale]
```

### Flusso di Accesso ai File

```
Qualsiasi codice chiama:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → minuscolo, normalizza separatori
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ Se compressed_type == -1 (VPK sentinel):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Decrittazione AES-GCM (se crittografato)
  │       │    ├─ Decompressione LZ4/Zstd/Brotli (se compresso)
  │       │    └─ Verifica hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │
  └─ Altrimenti (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [decompressione originale: LZ4 + XTEA / header MCOZ]
```

### Gestione della Memoria

CVpkPack usa `CMappedFile::AppendDataBlock()` – lo stesso meccanismo usato da CEterPack.
I dati decompressi vengono copiati in un buffer gestito da CMappedFile
e liberati automaticamente quando CMappedFile esce dallo scope.

## Convertire i Pack

Usa lo strumento 42pak-generator per convertire pack FliegeV3 esistenti:

```
# CLI - convertire singola coppia EPK in VPK
42pak-generator.exe convert --source metin2_patch_etc.eix --passphrase "la-tua-passphrase"

# CLI - creare VPK da cartella
42pak-generator.exe build --source ./ymir_work/item/ --output pack/item.vpk \
    --passphrase "la-tua-passphrase" --algorithm lz4 --compression 6
```

Oppure usa la vista Create nel GUI per creare archivi VPK da cartelle.

## Strategia di Migrazione

1. **Configurare librerie** – Aggiungere OpenSSL, Zstd, Brotli, BLAKE3 al progetto (LZ4 probabilmente già presente)
2. **Aggiungere file VPK** – Copiare i 6 nuovi file sorgente in `source/EterPack/`
3. **Modificare EterPackManager** – Unire o sostituire header e implementazione
4. **Modificare UserInterface.cpp** – Cambiare 2 righe
5. **Compilare** – Verificare che tutto compili
6. **Convertire un pack** – Ad es. `metin2_patch_etc` → testare che si carica correttamente
7. **Convertire il resto** – Uno alla volta o tutti insieme
8. **Rimuovere vecchi EPK** – Dopo che tutti i pack sono stati convertiti

Poiché `RegisterPackAuto` ricade sull'EPK quando non esiste un VPK, puoi convertire
i pack incrementalmente senza rompere nulla.

## Configurazione della Passphrase

| Metodo | Quando Usarlo |
|--------|---------------|
| Codificata nel sorgente | Server privato, il più semplice |
| File di configurazione (`metin2.cfg`) | Facile da cambiare senza ricompilare |
| Inviata dal server al login | Massima sicurezza – la passphrase cambia ogni sessione |

Per la passphrase inviata dal server, modifica `CAccountConnector` per ricevere la passphrase
nella risposta di autenticazione e chiama `CEterPackManager::Instance().SetVpkPassphrase()`
prima di qualsiasi accesso ai pack.

## Risoluzione dei Problemi

| Sintomo | Causa | Soluzione |
|---------|-------|-----------|
| Pack non trovato | Manca estensione `.vpk` | Verificare che il nome del pack non abbia estensione – `RegisterPackAuto` aggiunge `.vpk` |
| "HMAC verification failed" | Passphrase errata | Verificare che `SetVpkPassphrase` sia chiamato prima di `RegisterPackAuto` |
| File non trovato nel VPK | Differenza maiuscole/minuscole | VPK normalizza in minuscolo con separatori `/` |
| Crash in `Get2()` | Collisione sentinel `compressed_type` | Verificare che nessun file EPK usi `compressed_type == -1` (nessuno nel Metin2 standard) |
| Errori linker LZ4/Zstd/Brotli | Libreria mancante | Aggiungere librerie di decompressione in Additional Dependencies |
| Errori compilazione BLAKE3 | File sorgente mancanti | Verificare che tutti i file `blake3_*.c` siano nel progetto |
| Errori linker Boost | Boost includes mancanti | Controllare percorsi include Boost nelle proprietà del progetto EterPack |
| Problema `ENABLE_LOCAL_FILE_LOADING` | File locali non trovati in release | Definire la macro solo nelle build debug – convenzione FliegeV3 |
