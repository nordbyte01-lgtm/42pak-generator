<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Guida all'integrazione client (MartySama 5.8)

> **Profilo:** MartySama 5.8 - destinato all'architettura multi-cifratura HybridCrypt basata su Boost.
> Per l'integrazione 40250/ClientVS22, vedere `../40250/INTEGRATION_GUIDE.md`.
> Per l'integrazione FliegeV3, vedere `../FliegeV3/INTEGRATION_GUIDE.md`.

Sostituto diretto per il sistema EterPack (EIX/EPK). Basato sul codice
sorgente reale del client MartySama 5.8 (`Binary & S-Source/Binary/Client/EterPack/`).
Tutti i percorsi dei file fanno riferimento all'albero sorgente reale di MartySama.

## Cosa si ottiene

| Funzionalità | EterPack (vecchio) | VPK (nuovo) |
|-------------|-------------------|-------------|
| Crittografia | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (accelerato hardware) |
| Compressione | LZO | LZ4 / Zstandard / Brotli |
| Integrità | CRC32 per file | BLAKE3 per file + HMAC-SHA256 archivio |
| Formato file | coppia .eix + .epk | Singolo file .vpk |
| Gestione chiavi | Panama IV inviato dal server + HybridCrypt SDB | Passphrase PBKDF2-SHA512 |

## MartySama 5.8 vs 40250 vs FliegeV3

| Aspetto | 40250 | MartySama 5.8 | FliegeV3 |
|---------|-------|---------------|----------|
| Sistema crittografico | Camellia/Twofish/XTEA (HybridCrypt) | Uguale a 40250 | Solo XTEA |
| Consegna chiavi | Panama IV inviato dal server + SDB | Uguale a 40250 | Chiavi statiche compilate |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| Tipi di container | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| Stile header | Include guards | `#pragma once` | Include guards |
| Anti-manomissione | Nessuno | `#ifdef __THEMIDA__` VM_START/VM_END | Nessuno |
| Metodi del manager | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | Uguale a 40250 | Nessuno |
| Compressione | LZO | LZO | LZ4 |
| Struttura indice | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` |

Il codice di integrazione VPK si adatta ai container Boost di MartySama e ai
marcatori Themida mantenendo la stessa interfaccia API (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`).

## Architettura

VPK si integra tramite `CEterFileDict` - la stessa ricerca hash
`boost::unordered_multimap` utilizzata dall'EterPack originale di MartySama.
Quando `CEterPackManager::RegisterPackAuto()` trova un file `.vpk`, crea un
`CVpkPack` invece di `CEterPack`. `CVpkPack` registra le sue voci nel
`m_FileDict` condiviso con un marcatore sentinel. Quando `GetFromPack()`
risolve un nome file, controlla il marcatore e reindirizza in modo trasparente
a `CEterPack::Get2()` o `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## File

### Nuovi file da aggiungere a `Client/EterPack/`

| File | Scopo |
|------|-------|
| `VpkLoader.h` | Classe `CVpkPack` - sostituto diretto per `CEterPack` |
| `VpkLoader.cpp` | Implementazione completa: parsing header, tabella voci, decrittazione+decompressione, verifica BLAKE3 |
| `VpkCrypto.h` | Utilità crittografiche: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementazioni con OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Header `CEterPackManager` modificato con VPK + HybridCrypt + Boost |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificato con dispatch VPK, HybridCrypt, iteratori Boost, Themida |

### File da modificare

| File | Modifica |
|------|----------|
| `Client/EterPack/EterPackManager.h` | Sostituire con `EterPackManager_Vpk.h` (o unire) |
| `Client/EterPack/EterPackManager.cpp` | Sostituire con `EterPackManager_Vpk.cpp` (o unire) |
| `Client/UserInterface/UserInterface.cpp` | Modifica di 2 righe (vedi sotto) |

## Librerie necessarie

### OpenSSL 1.1+
- Windows: Scaricare da https://slproweb.com/products/Win32OpenSSL.html
- Header: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Link: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Link: `lz4.lib`
- **Nota:** MartySama 5.8 NON include LZ4 di default. Questa è una nuova dipendenza.

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

### Dipendenze esistenti (già incluse in MartySama 5.8)

MartySama 5.8 include già queste librerie che **non** sono necessarie per VPK
ma rimangono nel progetto per il percorso di codice EPK legacy:

| Libreria | Utilizzata da |
|----------|-------------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), cifratura Panama, funzioni hash |
| Boost | `boost::unordered_map`/`boost::unordered_multimap` in EterPack, EterPackManager, CEterFileDict |
| LZO | Decompressione EPK (tipi di pack 1, 2) |

Queste continuano a funzionare insieme a VPK. Nessuna modifica necessaria.

## Integrazione passo dopo passo

### Passo 1: Aggiungere i file al progetto EterPack in VS

1. Copiare `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` in `Client/EterPack/`
2. Copiare i file C di BLAKE3 in `Client/EterPack/` (o in una posizione condivisa)
3. Aggiungere tutti i nuovi file al progetto EterPack in Visual Studio
4. Aggiungere i percorsi di inclusione per OpenSSL, LZ4, Zstd, Brotli in **Additional Include Directories**
5. Aggiungere i percorsi delle librerie in **Additional Library Directories**
6. Aggiungere `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` in **Additional Dependencies**

### Passo 2: Sostituire EterPackManager

**Opzione A (sostituzione pulita):**
- Sostituire `Client/EterPack/EterPackManager.h` con `EterPackManager_Vpk.h`
- Sostituire `Client/EterPack/EterPackManager.cpp` con `EterPackManager_Vpk.cpp`
- Rinominare entrambi in `EterPackManager.h` / `EterPackManager.cpp`

**Opzione B (unire):**
Aggiungere questo all'`EterPackManager.h` esistente:

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

**Importante:** Notare l'uso di `boost::unordered_map` anziché `std::unordered_map`
per corrispondere alla convenzione di MartySama. Il file `EterPackManager_Vpk.h`
fornito utilizza già container Boost in tutto il codice.

Quindi unire le implementazioni da `EterPackManager_Vpk.cpp` nel file `.cpp` esistente.

### Passo 3: Modificare UserInterface.cpp

In `Client/UserInterface/UserInterface.cpp`, il ciclo di registrazione dei pack
(circa riga 557–579):

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

Vedere `UserInterface_VpkPatch.cpp` per la patch completa annotata con prima/dopo.

### Passo 4: Compilare

Compilare prima il progetto EterPack, poi l'intera soluzione. Il codice VPK si compila
insieme al codice EterPack esistente - nulla viene rimosso.

**Note di compilazione specifiche per MartySama 5.8:**
- MartySama usa Boost in tutto il progetto. Il `boost::unordered_multimap` in
  `CEterFileDict` è completamente compatibile con VPK - `InsertItem()` funziona in modo identico.
- Assicurarsi che `StdAfx.h` in EterPack includa `<boost/unordered_map.hpp>` (dovrebbe
  essere già presente se il progetto MartySama compila).
- CryptoPP è già nella soluzione per HybridCrypt - nessun conflitto con OpenSSL.
  Servono a scopi completamente diversi (CryptoPP per EPK legacy, OpenSSL per VPK).
- Se si usa Themida, i marcatori `#ifdef __THEMIDA__` in `EterPackManager_Vpk.cpp`
  sono già predisposti. Definire `__THEMIDA__` nella configurazione di compilazione se si
  desidera che i marcatori VM_START/VM_END siano attivi.

## Come funziona

### Flusso di registrazione

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

### Flusso di accesso ai file

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

### Coesistenza con HybridCrypt

MartySama 5.8 riceve le chiavi di crittografia dal server al login:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

Questi metodi iterano `m_PackMap` (che contiene solo oggetti `CEterPack*`).
I pack VPK sono memorizzati in `m_VpkPackMap` separatamente, quindi la consegna
delle chiavi HybridCrypt è completamente trasparente - entrambi i sistemi coesistono
senza modifiche al codice di rete.

### Gestione della memoria

CVpkPack utilizza `CMappedFile::AppendDataBlock()` - lo stesso meccanismo che
CEterPack usa per i dati HybridCrypt. I dati decompressi vengono copiati in
un buffer di proprietà di CMappedFile che viene liberato automaticamente quando
CMappedFile esce dallo scope. Nessuna pulizia manuale necessaria.

## Conversione dei pack

Usare lo strumento 42pak-generator per convertire i file pack MartySama esistenti:

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

Oppure usare la vista Crea dell'interfaccia grafica per costruire archivi VPK da cartelle.

## Strategia di migrazione

1. **Configurare le librerie** - aggiungere OpenSSL, LZ4, Zstd, Brotli, BLAKE3 al progetto
2. **Aggiungere i file VPK** - copiare i 6 nuovi file sorgente in `Client/EterPack/`
3. **Modificare EterPackManager** - unire o sostituire header e implementazione
4. **Modificare UserInterface.cpp** - la modifica di 2 righe
5. **Compilare** - verificare che tutto compili
6. **Convertire un pack** - es. `metin2_patch_etc` → testare che si carichi correttamente
7. **Convertire i pack rimanenti** - uno alla volta o tutti insieme
8. **Rimuovere i vecchi file EPK** - una volta convertiti tutti i pack

Poiché `RegisterPackAuto` ricade su EPK quando non esiste un VPK, è possibile
convertire i pack in modo incrementale senza rompere nulla.

## Configurazione della passphrase

| Metodo | Quando usarlo |
|--------|---------------|
| Hardcoded nel sorgente | Server privati, approccio più semplice |
| File di configurazione (`metin2.cfg`) | Facile da cambiare senza ricompilare |
| Inviata dal server al login | Massima sicurezza - la passphrase cambia per sessione |

Per passphrase inviata dal server, modificare `CAccountConnector` per riceverla
nella risposta di autenticazione e chiamare `CEterPackManager::Instance().SetVpkPassphrase()`
prima di qualsiasi accesso ai pack. L'`AccountConnector.cpp` di MartySama gestisce già
pacchetti personalizzati del server - aggiungere un nuovo handler accanto alla chiamata
esistente `RetrieveHybridCryptPackKeys`.

## Risoluzione dei problemi

| Sintomo | Causa | Soluzione |
|---------|-------|----------|
| File pack non trovati | Estensione `.vpk` mancante | Assicurarsi che il nome del pack non includa l'estensione - `RegisterPackAuto` aggiunge `.vpk` |
| "HMAC verification failed" | Passphrase errata | Verificare che `SetVpkPassphrase` sia chiamato prima di `RegisterPackAuto` |
| File non trovati nel VPK | Differenza maiuscole/minuscole nel percorso | VPK normalizza in minuscolo con separatori `/` |
| Crash in `Get2()` | Collisione del sentinel `compressed_type` | Assicurarsi che nessun file EPK usi `compressed_type == -1` (nessuno lo fa nel Metin2 standard) |
| Errore di link LZ4/Zstd/Brotli | Libreria mancante | Aggiungere la libreria di decompressione ad Additional Dependencies |
| Errore di compilazione BLAKE3 | File sorgente mancanti | Assicurarsi che tutti i file `blake3_*.c` siano nel progetto |
| Errori del linker Boost | Include Boost mancanti | Verificare i percorsi degli include Boost nelle proprietà del progetto EterPack |
| Conflitto CryptoPP + OpenSSL | Nomi di simboli duplicati | Non dovrebbe accadere - definiscono simboli separati. Se accade, verificare l'inquinamento da `using namespace` |
| Errori Themida | `__THEMIDA__` non definito | Definirlo nella configurazione di compilazione, o rimuovere i blocchi `#ifdef __THEMIDA__` da `EterPackManager_Vpk.cpp` |
