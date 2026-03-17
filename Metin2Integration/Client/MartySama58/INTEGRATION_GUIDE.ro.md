<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Ghid de integrare client (MartySama 5.8)

> **Profil:** MartySama 5.8 - vizează arhitectura multi-criptare HybridCrypt bazată pe Boost.
> Pentru integrarea 40250/ClientVS22, vezi `../40250/INTEGRATION_GUIDE.md`.
> Pentru integrarea FliegeV3, vezi `../FliegeV3/INTEGRATION_GUIDE.md`.

Înlocuitor direct pentru sistemul EterPack (EIX/EPK). Bazat pe codul
sursă real al clientului MartySama 5.8 (`Binary & S-Source/Binary/Client/EterPack/`).
Toate căile de fișiere fac referire la arborele sursă real al MartySama.

## Ce obțineți

| Funcționalitate | EterPack (vechi) | VPK (nou) |
|----------------|-----------------|-----------|
| Criptare | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (accelerat hardware) |
| Compresie | LZO | LZ4 / Zstandard / Brotli |
| Integritate | CRC32 per fișier | BLAKE3 per fișier + HMAC-SHA256 arhivă |
| Format fișier | pereche .eix + .epk | Fișier unic .vpk |
| Gestionare chei | Panama IV trimis de server + HybridCrypt SDB | Parolă PBKDF2-SHA512 |

## MartySama 5.8 vs 40250 vs FliegeV3

| Aspect | 40250 | MartySama 5.8 | FliegeV3 |
|--------|-------|---------------|----------|
| Sistem criptografic | Camellia/Twofish/XTEA (HybridCrypt) | La fel ca 40250 | Doar XTEA |
| Livrare chei | Panama IV trimis de server + SDB | La fel ca 40250 | Chei statice compilate |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| Tipuri de container | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| Stil header | Include guards | `#pragma once` | Include guards |
| Anti-modificare | Niciunul | `#ifdef __THEMIDA__` VM_START/VM_END | Niciunul |
| Metode manager | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | La fel ca 40250 | Niciunul |
| Compresie | LZO | LZO | LZ4 |
| Structură index | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` |

Codul de integrare VPK se adaptează la containerele Boost ale MartySama și la
marcatorii Themida, menținând aceeași interfață API (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`).

## Arhitectură

VPK se integrează prin `CEterFileDict` - aceeași căutare hash
`boost::unordered_multimap` pe care o folosește EterPack-ul original al MartySama.
Când `CEterPackManager::RegisterPackAuto()` găsește un fișier `.vpk`, creează
un `CVpkPack` în loc de `CEterPack`. `CVpkPack` înregistrează intrările sale
în `m_FileDict` partajat cu un marcator sentinel. Când `GetFromPack()`
rezolvă un nume de fișier, verifică marcatorul și redirecționează transparent
către `CEterPack::Get2()` sau `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Fișiere

### Fișiere noi de adăugat în `Client/EterPack/`

| Fișier | Scop |
|--------|------|
| `VpkLoader.h` | Clasa `CVpkPack` - înlocuitor direct pentru `CEterPack` |
| `VpkLoader.cpp` | Implementare completă: parsare header, tabel de intrări, decriptare+decompresie, verificare BLAKE3 |
| `VpkCrypto.h` | Utilități criptografice: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementări cu OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Header `CEterPackManager` modificat cu VPK + HybridCrypt + Boost |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificat cu dispatch VPK, HybridCrypt, iteratori Boost, Themida |

### Fișiere de modificat

| Fișier | Modificare |
|--------|-----------|
| `Client/EterPack/EterPackManager.h` | Înlocuiți cu `EterPackManager_Vpk.h` (sau fuzionați) |
| `Client/EterPack/EterPackManager.cpp` | Înlocuiți cu `EterPackManager_Vpk.cpp` (sau fuzionați) |
| `Client/UserInterface/UserInterface.cpp` | Modificare de 2 linii (vezi mai jos) |

## Biblioteci necesare

### OpenSSL 1.1+
- Windows: Descărcați de la https://slproweb.com/products/Win32OpenSSL.html
- Headere: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Linkare: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Linkare: `lz4.lib`
- **Notă:** MartySama 5.8 NU include LZ4 implicit. Aceasta este o dependență nouă.

### Zstandard
- https://github.com/facebook/zstd/releases
- Header: `<zstd.h>`
- Linkare: `zstd.lib` (sau `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Headere: `<brotli/decode.h>`
- Linkare: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copiați în proiect: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: adăugați și `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

### Dependențe existente (deja incluse în MartySama 5.8)

MartySama 5.8 include deja aceste biblioteci care **nu** sunt necesare pentru VPK
dar rămân în proiect pentru calea de cod EPK moștenită:

| Bibliotecă | Utilizată de |
|-----------|-------------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), cifrare Panama, funcții hash |
| Boost | `boost::unordered_map`/`boost::unordered_multimap` în EterPack, EterPackManager, CEterFileDict |
| LZO | Decompresie EPK (tipuri de pack 1, 2) |

Acestea continuă să funcționeze alături de VPK. Nu sunt necesare modificări.

## Integrare pas cu pas

### Pasul 1: Adăugați fișierele în proiectul EterPack în VS

1. Copiați `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` în `Client/EterPack/`
2. Copiați fișierele C BLAKE3 în `Client/EterPack/` (sau o locație partajată)
3. Adăugați toate fișierele noi în proiectul EterPack din Visual Studio
4. Adăugați căile de includere pentru OpenSSL, LZ4, Zstd, Brotli în **Additional Include Directories**
5. Adăugați căile bibliotecilor în **Additional Library Directories**
6. Adăugați `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` în **Additional Dependencies**

### Pasul 2: Înlocuiți EterPackManager

**Opțiunea A (înlocuire curată):**
- Înlocuiți `Client/EterPack/EterPackManager.h` cu `EterPackManager_Vpk.h`
- Înlocuiți `Client/EterPack/EterPackManager.cpp` cu `EterPackManager_Vpk.cpp`
- Redenumiți ambele în `EterPackManager.h` / `EterPackManager.cpp`

**Opțiunea B (fuzionare):**
Adăugați aceasta la `EterPackManager.h` existent:

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

**Important:** Rețineți utilizarea `boost::unordered_map` în loc de `std::unordered_map`
pentru a respecta convenția MartySama. `EterPackManager_Vpk.h` furnizat
folosește deja containere Boost în tot codul.

Apoi fuzionați implementările din `EterPackManager_Vpk.cpp` în fișierul `.cpp` existent.

### Pasul 3: Modificați UserInterface.cpp

În `Client/UserInterface/UserInterface.cpp`, bucla de înregistrare a pack-urilor
(în jurul liniei 557–579):

**Înainte:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**După:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

Și adăugați această linie **înainte** de bucla de înregistrare:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Atât. Două modificări în total:
1. `SetVpkPassphrase()` înainte de buclă
2. `RegisterPack()` → `RegisterPackAuto()` (2 apariții)

Vedeți `UserInterface_VpkPatch.cpp` pentru patch-ul complet adnotat cu înainte/după.

### Pasul 4: Compilare

Compilați mai întâi proiectul EterPack, apoi soluția completă. Codul VPK se compilează
alături de codul EterPack existent - nimic nu este eliminat.

**Note de compilare specifice MartySama 5.8:**
- MartySama folosește Boost în tot proiectul. `boost::unordered_multimap` din
  `CEterFileDict` este complet compatibil cu VPK - `InsertItem()` funcționează identic.
- Asigurați-vă că `StdAfx.h` din EterPack include `<boost/unordered_map.hpp>` (ar trebui
  să o facă deja dacă proiectul MartySama compilează).
- CryptoPP este deja în soluție pentru HybridCrypt - fără conflict cu OpenSSL.
  Servesc scopuri complet diferite (CryptoPP pentru EPK moștenit, OpenSSL pentru VPK).
- Dacă folosiți Themida, marcatorii `#ifdef __THEMIDA__` din `EterPackManager_Vpk.cpp`
  sunt deja configurați. Definiți `__THEMIDA__` în configurația de compilare dacă doriți
  ca marcatorii VM_START/VM_END să fie activi.

## Cum funcționează

### Fluxul de înregistrare

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

### Fluxul de acces la fișiere

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

### Coexistența cu HybridCrypt

MartySama 5.8 primește cheile de criptare de la server la autentificare:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

Aceste metode iterează `m_PackMap` (care conține doar obiecte `CEterPack*`).
Pack-urile VPK sunt stocate în `m_VpkPackMap` separat, deci livrarea cheilor
HybridCrypt este complet transparentă - ambele sisteme coexistă fără nicio
modificare a codului de rețea.

### Gestionarea memoriei

CVpkPack folosește `CMappedFile::AppendDataBlock()` - același mecanism pe care
CEterPack îl folosește pentru datele HybridCrypt. Datele decomprimate sunt copiate
într-un buffer deținut de CMappedFile care este eliberat automat când CMappedFile
iese din domeniul de vizibilitate. Nu este necesară curățare manuală.

## Conversia pack-urilor

Folosiți instrumentul 42pak-generator pentru a converti fișierele pack MartySama existente:

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

Sau folosiți vizualizarea Creare din interfața grafică pentru a construi arhive VPK din foldere.

## Strategia de migrare

1. **Configurați bibliotecile** - adăugați OpenSSL, LZ4, Zstd, Brotli, BLAKE3 în proiect
2. **Adăugați fișierele VPK** - copiați cele 6 fișiere sursă noi în `Client/EterPack/`
3. **Modificați EterPackManager** - fuzionați sau înlocuiți headerul și implementarea
4. **Modificați UserInterface.cpp** - modificarea de 2 linii
5. **Compilați** - verificați că totul compilează
6. **Convertiți un pack** - ex. `metin2_patch_etc` → testați că se încarcă corect
7. **Convertiți pack-urile rămase** - unul câte unul sau toate deodată
8. **Eliminați fișierele EPK vechi** - după ce toate pack-urile sunt convertite

Deoarece `RegisterPackAuto` revine la EPK când nu există un VPK, puteți
converti pack-urile incremental fără a strica nimic.

## Configurarea parolei

| Metodă | Când se folosește |
|--------|------------------|
| Codificată în sursă | Servere private, cea mai simplă abordare |
| Fișier de configurare (`metin2.cfg`) | Ușor de schimbat fără recompilare |
| Trimisă de server la autentificare | Securitate maximă - parola se schimbă per sesiune |

Pentru parola trimisă de server, modificați `CAccountConnector` pentru a o primi
în răspunsul de autentificare și apelați `CEterPackManager::Instance().SetVpkPassphrase()`
înainte de orice acces la pack-uri. `AccountConnector.cpp` al MartySama gestionează deja
pachetele personalizate de server - adăugați un nou handler alături de apelul
existent `RetrieveHybridCryptPackKeys`.

## Depanare

| Simptom | Cauză | Soluție |
|---------|-------|---------|
| Fișiere pack negăsite | Lipsește extensia `.vpk` | Asigurați-vă că numele pack-ului nu include extensia - `RegisterPackAuto` adaugă `.vpk` |
| "HMAC verification failed" | Parolă incorectă | Verificați că `SetVpkPassphrase` este apelat înainte de `RegisterPackAuto` |
| Fișiere negăsite în VPK | Diferență de majuscule/minuscule în cale | VPK normalizează la litere mici cu separatori `/` |
| Crash în `Get2()` | Coliziune sentinel `compressed_type` | Asigurați-vă că niciun fișier EPK nu folosește `compressed_type == -1` (niciunul nu o face în Metin2 standard) |
| Eroare de linkare LZ4/Zstd/Brotli | Bibliotecă lipsă | Adăugați biblioteca de decompresie la Additional Dependencies |
| Eroare de compilare BLAKE3 | Fișiere sursă lipsă | Asigurați-vă că toate fișierele `blake3_*.c` sunt în proiect |
| Erori de linker Boost | Include Boost lipsă | Verificați căile de include Boost în proprietățile proiectului EterPack |
| Conflict CryptoPP + OpenSSL | Nume de simboluri duplicate | Nu ar trebui să se întâmple - definesc simboluri separate. Dacă se întâmplă, verificați poluarea `using namespace` |
| Erori Themida | `__THEMIDA__` nedefinit | Definiți-l în configurația de compilare sau eliminați blocurile `#ifdef __THEMIDA__` din `EterPackManager_Vpk.cpp` |
