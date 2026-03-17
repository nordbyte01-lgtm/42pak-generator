<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Guida all'integrazione client (40250 / ClientVS22)

> **Profilo:** 40250 - destinato all'architettura multi-cifratura HybridCrypt.
> Per l'integrazione FliegeV3 (XTEA/LZ4), vedere `../FliegeV3/INTEGRATION_GUIDE.md`.

Sostituto diretto per il sistema EterPack (EIX/EPK). Basato sul codice
sorgente reale del client 40250. Tutti i percorsi dei file fanno riferimento
all'albero sorgente reale.

## Cosa si ottiene

| Funzionalità | EterPack (vecchio) | VPK (nuovo) |
|-------------|-------------------|-------------|
| Crittografia | TEA / Panama / HybridCrypt | AES-256-GCM (accelerato hardware) |
| Compressione | LZO | LZ4 / Zstandard / Brotli |
| Integrità | CRC32 per file | BLAKE3 per file + HMAC-SHA256 archivio |
| Formato file | coppia .eix + .epk | Singolo file .vpk |
| Gestione chiavi | Panama IV inviato dal server + HybridCrypt SDB | Passphrase PBKDF2-SHA512 |

## Architettura

VPK si integra tramite `CEterFileDict` - la stessa ricerca hash utilizzata
dall'EterPack originale. Quando `CEterPackManager::RegisterPackAuto()` trova
un file `.vpk`, crea un `CVpkPack` invece di `CEterPack`. `CVpkPack`
registra le sue voci nel `m_FileDict` condiviso con un marcatore sentinel.
Quando `GetFromPack()` risolve un nome file, controlla il marcatore e
reindirizza in modo trasparente a `CEterPack::Get2()` o `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## File

### Nuovi file da aggiungere a `source/EterPack/`

| File | Scopo |
|------|-------|
| `VpkLoader.h` | Classe `CVpkPack` - sostituto diretto per `CEterPack` |
| `VpkLoader.cpp` | Implementazione completa: parsing header, tabella voci, decrittazione+decompressione, verifica BLAKE3 |
| `VpkCrypto.h` | Utilità crittografiche: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementazioni con OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Header `CEterPackManager` modificato con supporto VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificato con `RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase` |

### File da modificare

| File | Modifica |
|------|----------|
| `source/EterPack/EterPackManager.h` | Sostituire con `EterPackManager_Vpk.h` (o unire le aggiunte) |
| `source/EterPack/EterPackManager.cpp` | Sostituire con `EterPackManager_Vpk.cpp` (o unire le aggiunte) |
| `source/UserInterface/UserInterface.cpp` | Modifica di 2 righe (vedi sotto) |

## Librerie necessarie

### OpenSSL 1.1+
- Windows: Scaricare da https://slproweb.com/products/Win32OpenSSL.html
- Header: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Link: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Link: `lz4.lib`

### Zstandard
- https://github.com/facebook/zstd/releases
- Header: `<zstd.h>`
- Link: `zstd.lib` (o `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Header: `<brotli/decode.h>`
- Link: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copiare nel progetto: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: aggiungere anche `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

## Integrazione passo dopo passo

### Passo 1: Aggiungere i file al progetto EterPack in VS

1. Copiare `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` in `source/EterPack/`
2. Copiare i file C di BLAKE3 in `source/EterPack/` (o in una posizione condivisa)
3. Aggiungere tutti i nuovi file al progetto EterPack in Visual Studio
4. Aggiungere i percorsi di inclusione per OpenSSL, LZ4, Zstd, Brotli in **Additional Include Directories**
5. Aggiungere i percorsi delle librerie in **Additional Library Directories**
6. Aggiungere `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` in **Additional Dependencies**

### Passo 2: Sostituire EterPackManager

**Opzione A (sostituzione pulita):**
- Sostituire `source/EterPack/EterPackManager.h` con `EterPackManager_Vpk.h`
- Sostituire `source/EterPack/EterPackManager.cpp` con `EterPackManager_Vpk.cpp`
- Rinominare entrambi in `EterPackManager.h` / `EterPackManager.cpp`

**Opzione B (unione):**
Aggiungere quanto segue all'`EterPackManager.h` esistente:

```cpp
#include "VpkLoader.h"

// All'interno della dichiarazione della classe, aggiungere a public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// All'interno della dichiarazione della classe, aggiungere a protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Quindi unire le implementazioni da `EterPackManager_Vpk.cpp` nel file `.cpp` esistente.

### Passo 3: Modificare UserInterface.cpp

In `source/UserInterface/UserInterface.cpp`, il ciclo di registrazione dei pacchetti (circa riga 220):

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

E aggiungere questa riga **prima** del ciclo di registrazione:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Tutto qui. Due modifiche in totale:
1. `SetVpkPassphrase()` prima del ciclo
2. `RegisterPack()` → `RegisterPackAuto()` (2 occorrenze)

### Passo 4: Compilazione

Compilare prima il progetto EterPack, poi l'intera soluzione. Il codice VPK
viene compilato insieme al codice EterPack esistente - nulla viene rimosso.

## Come funziona

### Flusso di registrazione

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     memorizza m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") esiste?
                 "xyz/")              ├─ SÌ → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() per ogni file
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ NO → RegisterPack() [flusso EPK originale]
```

### Flusso di accesso ai file

```
Qualsiasi codice chiama:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → "d:/ymir work/item/weapon.gr2"
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ se compressed_type == -1 (sentinel VPK):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Decrittazione AES-GCM (se crittografato)
  │       │    ├─ Decompressione LZ4/Zstd/Brotli (se compresso)
  │       │    └─ Verifica hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │            └─ CMappedFile possiede la memoria (liberata nel distruttore)
  │
  └─ altrimenti (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [decompressione originale: LZO / Panama / HybridCrypt]
```

### Gestione della memoria

CVpkPack utilizza `CMappedFile::AppendDataBlock()` - lo stesso meccanismo
utilizzato da CEterPack per i dati HybridCrypt. I dati decompressi vengono
copiati in un buffer di proprietà di CMappedFile che viene automaticamente
liberato quando CMappedFile esce dallo scope. Nessuna pulizia manuale necessaria.

## Conversione dei pacchetti

Utilizzare lo strumento 42pak-generator per convertire i file di pacchetto esistenti:

```bash
# Convertire una singola coppia EPK in VPK
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "il-tuo-segreto"

# Costruire un VPK da una cartella
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "il-tuo-segreto" --algorithm Zstandard --compression 6

# Conversione in batch di tutti gli EIX/EPK in una directory
42pak-cli migrate ./pack/ ./vpk/ --passphrase "il-tuo-segreto" --format Standard

# Monitorare una directory e ricostruire automaticamente
42pak-cli watch ./gamedata --output game.vpk --passphrase "il-tuo-segreto"
```

Oppure utilizzare la vista Create della GUI per costruire archivi VPK da cartelle.

## Strategia di migrazione

1. **Configurare le librerie** - aggiungere OpenSSL, LZ4, Zstd, Brotli, BLAKE3 al progetto
2. **Aggiungere i file VPK** - copiare i 6 nuovi file sorgente in `source/EterPack/`
3. **Modificare EterPackManager** - unire o sostituire header e implementazione
4. **Modificare UserInterface.cpp** - la modifica di 2 righe
5. **Compilare** - verificare che tutto si compili
6. **Convertire un pacchetto** - es. `metin2_patch_etc` -> verificare che si carichi correttamente
7. **Convertire i pacchetti rimanenti** - uno alla volta o tutti insieme
8. **Rimuovere i vecchi file EPK** - dopo che tutti i pacchetti sono stati convertiti

Poiché `RegisterPackAuto` ricade automaticamente su EPK quando non esiste un VPK,
è possibile convertire i pacchetti incrementalmente senza compromettere nulla.

## Configurazione della passphrase

| Metodo | Quando usarlo |
|--------|--------------|
| Hardcoded nel sorgente | Server privati, approccio più semplice |
| File di configurazione (`metin2.cfg`) | Facile da cambiare senza ricompilare |
| Inviata dal server al login | Massima sicurezza - la passphrase cambia per sessione |

Per la passphrase inviata dal server, modificare `CAccountConnector` per
riceverla nella risposta di autenticazione e chiamare
`CEterPackManager::Instance().SetVpkPassphrase()` prima di qualsiasi accesso ai pacchetti.

## Risoluzione dei problemi

| Sintomo | Causa | Soluzione |
|---------|-------|-----------|
| File di pacchetto non trovati | Estensione `.vpk` mancante | Assicurarsi che il nome del pacchetto non includa l'estensione - `RegisterPackAuto` aggiunge `.vpk` |
| "HMAC verification failed" | Passphrase errata | Verificare che `SetVpkPassphrase` sia chiamato prima di `RegisterPackAuto` |
| File non trovati nel VPK | Discrepanza maiuscole/minuscole | VPK normalizza in minuscolo con separatori `/` |
| Crash in `Get2()` | Collisione sentinel `compressed_type` | Assicurarsi che nessun file EPK usi `compressed_type == -1` (nessuno nel Metin2 standard lo fa) |
| Errore di link LZ4/Zstd/Brotli | Libreria mancante | Aggiungere la libreria di decompressione alle Additional Dependencies |
| Errore di compilazione BLAKE3 | File sorgente mancanti | Assicurarsi che tutti i file `blake3_*.c` siano nel progetto |
