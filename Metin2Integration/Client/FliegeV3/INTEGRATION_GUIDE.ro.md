<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Ghid de integrare client FliegeV3

Înlocuitor direct pentru sistemul EterPack (EIX/EPK). Bazat pe codul sursă
real al clientului FliegeV3 binary-src. Toate căile de fișiere fac referire
la arborele sursă real FliegeV3.

## Ce primiți

| Caracteristică | EterPack (FliegeV3) | VPK (nou) |
|---------------|---------------------|-----------|
| Criptare | XTEA (cheie 128-bit, 32 runde) | AES-256-GCM (accelerat hardware) |
| Compresie | LZ4 (migrat de la LZO) | LZ4 / Zstandard / Brotli |
| Integritate | CRC32 per fișier | BLAKE3 per fișier + HMAC-SHA256 arhivă |
| Format fișier | pereche .eix + .epk | Un singur fișier .vpk |
| Gestionare chei | Chei XTEA statice în cod | Frază de acces PBKDF2-SHA512 |
| Intrări index | `TEterPackIndex` de 192 octeți | Intrări VPK cu lungime variabilă |

## FliegeV3 vs 40250 - De ce un profil separat?

FliegeV3 diferă semnificativ de sistemul 40250:

| Aspect | 40250 | FliegeV3 |
|--------|-------|----------|
| Sistem criptografic | Hibrid Camellia/Twofish/XTEA (HybridCrypt) | Doar XTEA |
| Livrare chei | Panama IV de la server + blocuri SDB | Chei statice compilate |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` |
| Metode manager | `DecryptPackIV`, `WriteHybridCryptPackInfo`, `RetrieveHybridCryptPackKeys/SDB` | Niciuna din acestea |
| Căutare fișiere | Permite întotdeauna fișiere locale | Condițional `ENABLE_LOCAL_FILE_LOADING` |
| Structură index | 169 octeți (#pragma pack(push,4) cu nume de 161 caractere) | 192 octeți (aceeași structură, aliniere/padding diferit) |

Codul de integrare VPK se adaptează la aceste diferențe menținând aceeași
suprafață API (`RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase`).

## Arhitectură

VPK se integrează prin `CEterFileDict` - același `boost::unordered_multimap`
pe care EterPack-ul FliegeV3 original îl folosește. Când `RegisterPackAuto()`
găsește un fișier `.vpk`, creează un `CVpkPack` în loc de `CEterPack`.
`CVpkPack` înregistrează intrările sale în `m_FileDict` partajat cu un
marcator sentinel (`compressed_type == -1`).

Când `GetFromPack()` rezolvă un nume de fișier, verifică marcatorul și
redirecționează transparent către `CEterPack::Get2()` sau `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Fișiere

### Fișiere noi de adăugat în `source/EterPack/`

| Fișier | Scop |
|--------|------|
| `VpkLoader.h` | Clasa `CVpkPack` - înlocuitor direct pentru `CEterPack` |
| `VpkLoader.cpp` | Implementare completă: parsare header, tabel de intrări, decriptare+decompresie, verificare BLAKE3 |
| `VpkCrypto.h` | Utilitar criptografic: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementări folosind OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Header `CEterPackManager` modificat cu suport VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificat - fără HybridCrypt, logică de căutare FliegeV3 |

### Fișiere de modificat

| Fișier | Modificare |
|--------|-----------|
| `source/EterPack/EterPackManager.h` | Înlocuiți cu `EterPackManager_Vpk.h` (sau îmbinați) |
| `source/EterPack/EterPackManager.cpp` | Înlocuiți cu `EterPackManager_Vpk.cpp` (sau îmbinați) |
| `source/UserInterface/UserInterface.cpp` | Modificare de 2 linii (vezi mai jos) |

## Biblioteci necesare

### OpenSSL 1.1+
- Windows: Descărcați de la https://slproweb.com/products/Win32OpenSSL.html
- Headere: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Linkare: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Linkare: `lz4.lib`
- **Notă:** FliegeV3 folosește deja LZ4 pentru compresie. Dacă proiectul
  deja linkează `lz4.lib`, nu este necesară configurare suplimentară.

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

## Integrare pas cu pas

### Pasul 1: Adăugați fișierele în proiectul EterPack din VS

1. Copiați `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` în `source/EterPack/`
2. Copiați fișierele C BLAKE3 în `source/EterPack/` (sau într-o locație partajată)
3. Adăugați toate fișierele noi în proiectul EterPack din Visual Studio
4. Adăugați căile include pentru OpenSSL, Zstd, Brotli în **Additional Include Directories**
   - LZ4 ar trebui să fie deja configurat dacă build-ul FliegeV3 îl folosește
5. Adăugați căile bibliotecilor în **Additional Library Directories**
6. Adăugați `libssl.lib`, `libcrypto.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` în **Additional Dependencies**
   - `lz4.lib` ar trebui să fie deja linkat

### Pasul 2: Înlocuiți EterPackManager

**Opțiunea A (înlocuire curată):**
- Înlocuiți `source/EterPack/EterPackManager.h` cu `EterPackManager_Vpk.h`
- Înlocuiți `source/EterPack/EterPackManager.cpp` cu `EterPackManager_Vpk.cpp`
- Redenumiți ambele în `EterPackManager.h` / `EterPackManager.cpp`

**Opțiunea B (îmbinare):**
Adăugați acestea la `EterPackManager.h` existent:

```cpp
#include "VpkLoader.h"

// În declarația clasei, adăugați la public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// În declarația clasei, adăugați la protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Apoi îmbinați implementările din `EterPackManager_Vpk.cpp` în fișierul `.cpp` existent.

**Important:** NU adăugați `DecryptPackIV`, `WriteHybridCryptPackInfo`,
`RetrieveHybridCryptPackKeys` sau `RetrieveHybridCryptPackSDB` - FliegeV3
nu are aceste metode.

### Pasul 3: Modificați UserInterface.cpp

În `source/UserInterface/UserInterface.cpp`, bucla de înregistrare a pachetelor:

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

Adăugați această linie **înainte** de bucla de înregistrare:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Atât. Două modificări în total:
1. `SetVpkPassphrase()` înainte de buclă
2. `RegisterPack()` → `RegisterPackAuto()` (2 apariții)

**Notă:** Spre deosebire de integrarea 40250, FliegeV3 NU necesită modificări
ale codului de rețea. Nu există apeluri `RetrieveHybridCryptPackKeys` sau
`RetrieveHybridCryptPackSDB` de care să vă faceți griji.

### Pasul 4: Compilare

Compilați mai întâi proiectul EterPack, apoi întreaga soluție. Codul VPK se
compilează alături de codul EterPack existent - nimic nu este eliminat.

**Note specifice FliegeV3:**
- FliegeV3 folosește Boost. `boost::unordered_multimap` din `CEterFileDict`
  este compatibil cu VPK - `InsertItem()` funcționează identic.
- Asigurați-vă că `StdAfx.h` din EterPack include `<boost/unordered_map.hpp>`
  (ar trebui deja dacă proiectul FliegeV3 se compilează).
- Dacă vedeți erori de linker despre `boost::unordered_multimap`, verificați
  căile include Boost în proprietățile proiectului EterPack.

## Cum funcționează

### Flux de înregistrare

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     stochează m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") există?
                 "xyz/")              ├─ DA → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() pentru fiecare fișier
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ NU → RegisterPack() [flux EPK original]
```

### Flux de acces la fișiere

```
Orice cod apelează:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → litere mici, normalizare separatori
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ dacă compressed_type == -1 (sentinel VPK):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Decriptare AES-GCM (dacă este criptat)
  │       │    ├─ Decompresie LZ4/Zstd/Brotli (dacă este comprimat)
  │       │    └─ Verificare hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │
  └─ altfel (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [decompresie originală: LZ4 + XTEA / header MCOZ]
```

### Gestionarea memoriei

CVpkPack folosește `CMappedFile::AppendDataBlock()` - același mecanism pe care
CEterPack îl folosește. Datele decomprimate sunt copiate într-un buffer deținut
de CMappedFile care este eliberat automat când CMappedFile iese din domeniul
de vizibilitate.

## Conversia pachetelor

Folosiți instrumentul 42pak-generator pentru a converti fișierele de pachete FliegeV3 existente:

```
# CLI - conversia unei singure perechi EPK în VPK
42pak-generator.exe convert --source metin2_patch_etc.eix --passphrase "secretul-tău"

# CLI - construirea unui VPK dintr-un folder
42pak-generator.exe build --source ./ymir_work/item/ --output pack/item.vpk \
    --passphrase "secretul-tău" --algorithm lz4 --compression 6
```

Sau folosiți vizualizarea Create din GUI pentru a construi arhive VPK din foldere.

## Strategia de migrare

1. **Configurați bibliotecile** - adăugați OpenSSL, Zstd, Brotli, BLAKE3 la proiect (LZ4 probabil deja prezent)
2. **Adăugați fișierele VPK** - copiați cele 6 fișiere sursă noi în `source/EterPack/`
3. **Modificați EterPackManager** - îmbinați sau înlocuiți headerul și implementarea
4. **Modificați UserInterface.cpp** - modificarea de 2 linii
5. **Compilați** - verificați că totul se compilează
6. **Convertiți un pachet** - ex. `metin2_patch_etc` -> testați că se încarcă corect
7. **Convertiți pachetele rămase** - unul câte unul sau toate deodată
8. **Eliminați fișierele EPK vechi** - după ce toate pachetele sunt convertite

Deoarece `RegisterPackAuto` revine automat la EPK când nu există VPK, puteți
converti pachetele incremental fără a strica nimic.

## Configurarea frazei de acces

| Metodă | Când se folosește |
|--------|------------------|
| Codificată în sursă | Servere private, abordarea cea mai simplă |
| Fișier de configurare (`metin2.cfg`) | Ușor de schimbat fără recompilare |
| Trimisă de server la autentificare | Securitate maximă - fraza se schimbă per sesiune |

Pentru fraza trimisă de server, modificați `CAccountConnector` pentru a o primi
în răspunsul de autentificare și apelați `CEterPackManager::Instance().SetVpkPassphrase()`
înainte de orice acces la pachete.

## Depanare

| Simptom | Cauză | Soluție |
|---------|-------|---------|
| Fișiere pachet negăsite | Extensia `.vpk` lipsă | Asigurați-vă că numele pachetului nu include extensia - `RegisterPackAuto` adaugă `.vpk` |
| „HMAC verification failed" | Frază de acces greșită | Verificați că `SetVpkPassphrase` este apelat înainte de `RegisterPackAuto` |
| Fișiere negăsite în VPK | Nepotrivire majuscule/minuscule | VPK normalizează la litere mici cu separatori `/` |
| Crash în `Get2()` | Coliziune sentinel `compressed_type` | Asigurați-vă că niciun fișier EPK nu folosește `compressed_type == -1` (niciunul standard Metin2 nu face asta) |
| Eroare linkare LZ4/Zstd/Brotli | Bibliotecă lipsă | Adăugați biblioteca de decompresie în Additional Dependencies |
| Eroare compilare BLAKE3 | Fișiere sursă lipsă | Asigurați-vă că toate fișierele `blake3_*.c` sunt în proiect |
| Erori linker Boost | Include Boost lipsă | Verificați căile include Boost în proprietățile proiectului EterPack |
| Probleme cu `ENABLE_LOCAL_FILE_LOADING` | Fișiere locale nu sunt găsite în release | Definiți macro-ul doar în build-urile debug - conform convenției FliegeV3 |
