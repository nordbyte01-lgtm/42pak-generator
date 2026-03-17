<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Ghid de integrare client (40250 / ClientVS22)

> **Profil:** 40250 - destinat arhitecturii multi-cipher HybridCrypt.
> Pentru integrarea FliegeV3 (XTEA/LZ4), consultați `../FliegeV3/INTEGRATION_GUIDE.md`.

Înlocuitor direct pentru sistemul EterPack (EIX/EPK). Bazat pe codul sursă
real al clientului 40250. Toate căile de fișiere fac referire la arborele sursă real.

## Ce primiți

| Caracteristică | EterPack (vechi) | VPK (nou) |
|---------------|-----------------|-----------|
| Criptare | TEA / Panama / HybridCrypt | AES-256-GCM (accelerat hardware) |
| Compresie | LZO | LZ4 / Zstandard / Brotli |
| Integritate | CRC32 per fișier | BLAKE3 per fișier + HMAC-SHA256 arhivă |
| Format fișier | pereche .eix + .epk | Un singur fișier .vpk |
| Gestionare chei | Panama IV trimis de server + HybridCrypt SDB | Frază de acces PBKDF2-SHA512 |

## Arhitectură

VPK se integrează prin `CEterFileDict` - aceeași căutare hash pe care o
folosește EterPack-ul original. Când `CEterPackManager::RegisterPackAuto()`
găsește un fișier `.vpk`, creează un `CVpkPack` în loc de `CEterPack`.
`CVpkPack` înregistrează intrările sale în `m_FileDict` partajat cu un
marcator sentinel. Când `GetFromPack()` rezolvă un nume de fișier, verifică
marcatorul și redirecționează transparent către `CEterPack::Get2()` sau `CVpkPack::Get2()`.

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
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificat cu `RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase` |

### Fișiere de modificat

| Fișier | Modificare |
|--------|-----------|
| `source/EterPack/EterPackManager.h` | Înlocuiți cu `EterPackManager_Vpk.h` (sau îmbinați adăugirile) |
| `source/EterPack/EterPackManager.cpp` | Înlocuiți cu `EterPackManager_Vpk.cpp` (sau îmbinați adăugirile) |
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
4. Adăugați căile include pentru OpenSSL, LZ4, Zstd, Brotli în **Additional Include Directories**
5. Adăugați căile bibliotecilor în **Additional Library Directories**
6. Adăugați `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` în **Additional Dependencies**

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

### Pasul 3: Modificați UserInterface.cpp

În `source/UserInterface/UserInterface.cpp`, bucla de înregistrare a pachetelor (în jurul liniei 220):

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

### Pasul 4: Compilare

Compilați mai întâi proiectul EterPack, apoi întreaga soluție. Codul VPK se
compilează alături de codul EterPack existent - nimic nu este eliminat.

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
  ├─ ConvertFileName → "d:/ymir work/item/weapon.gr2"
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
  │            └─ CMappedFile deține memoria (eliberată în destructor)
  │
  └─ altfel (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [decompresie originală: LZO / Panama / HybridCrypt]
```

### Gestionarea memoriei

CVpkPack folosește `CMappedFile::AppendDataBlock()` - același mecanism pe care
CEterPack îl folosește pentru datele HybridCrypt. Datele decomprimate sunt copiate
într-un buffer deținut de CMappedFile care este eliberat automat când CMappedFile
iese din domeniul de vizibilitate. Nu necesită curățare manuală.

## Conversia pachetelor

Folosiți instrumentul 42pak-generator pentru a converti fișierele de pachete existente:

```bash
# Conversia unei singure perechi EPK în VPK
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "secretul-tău"

# Construirea unui VPK dintr-un folder
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "secretul-tău" --algorithm Zstandard --compression 6

# Conversia în lot a tuturor EIX/EPK dintr-un director
42pak-cli migrate ./pack/ ./vpk/ --passphrase "secretul-tău" --format Standard

# Monitorizarea unui director și reconstruirea automată
42pak-cli watch ./gamedata --output game.vpk --passphrase "secretul-tău"
```

Sau folosiți vizualizarea Create din GUI pentru a construi arhive VPK din foldere.

## Strategia de migrare

1. **Configurați bibliotecile** - adăugați OpenSSL, LZ4, Zstd, Brotli, BLAKE3 la proiect
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
