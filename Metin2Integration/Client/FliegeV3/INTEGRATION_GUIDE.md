# 42pak VPK — FliegeV3 Client Integration Guide

Drop-in replacement for the EterPack (EIX/EPK) system. Based on the actual
FliegeV3 binary-src client source code. All file paths reference the real
FliegeV3 source tree.

## What You Get

| Feature | EterPack (FliegeV3) | VPK (new) |
|---------|---------------------|-----------|
| Encryption | XTEA (128-bit key, 32 rounds) | AES-256-GCM (hardware-accelerated) |
| Compression | LZ4 (migrated from LZO) | LZ4 / Zstandard / Brotli |
| Integrity | CRC32 per-file | BLAKE3 per-file + HMAC-SHA256 archive |
| File format | .eix + .epk pair | Single .vpk file |
| Key management | Static hardcoded XTEA keys | PBKDF2-SHA512 passphrase |
| Index entries | 192-byte `TEterPackIndex` | Variable-length VPK entries |

## FliegeV3 vs 40250 — Why a Separate Profile?

FliegeV3 differs significantly from the 40250 system:

| Aspect | 40250 | FliegeV3 |
|--------|-------|----------|
| Crypto system | Camellia/Twofish/XTEA hybrid (HybridCrypt) | XTEA-only |
| Key delivery | Server-sent Panama IV + SDB blocks | Static compiled keys |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` |
| Manager methods | `DecryptPackIV`, `WriteHybridCryptPackInfo`, `RetrieveHybridCryptPackKeys/SDB` | None of these |
| File search | Always allows loose files | `ENABLE_LOCAL_FILE_LOADING` conditional |
| Index struct | 169 bytes (#pragma pack(push,4) with 161-char filename) | 192 bytes (same struct but different alignment/padding) |

The VPK integration code adapts to these differences while maintaining the
same API surface (`RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase`).

## Architecture

VPK integrates via `CEterFileDict` — the same `boost::unordered_multimap`
lookup that the original FliegeV3 EterPack uses. When `RegisterPackAuto()`
finds a `.vpk` file, it creates a `CVpkPack` instead of `CEterPack`. The
`CVpkPack` registers its entries into the shared `m_FileDict` with a
sentinel marker (`compressed_type == -1`).

When `GetFromPack()` resolves a filename, it checks the marker and dispatches
to either `CEterPack::Get2()` or `CVpkPack::Get2()` transparently.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Files

### New files to add to `source/EterPack/`

| File | Purpose |
|------|---------|
| `VpkLoader.h` | `CVpkPack` class — drop-in replacement for `CEterPack` |
| `VpkLoader.cpp` | Full implementation: header parsing, entry table, decrypt+decompress, BLAKE3 verify |
| `VpkCrypto.h` | Crypto utilities: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementations using OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Patched `CEterPackManager` header with VPK support |
| `EterPackManager_Vpk.cpp` | Patched `CEterPackManager` — no HybridCrypt, FliegeV3 search logic |

### Files to modify

| File | Change |
|------|--------|
| `source/EterPack/EterPackManager.h` | Replace with `EterPackManager_Vpk.h` (or merge) |
| `source/EterPack/EterPackManager.cpp` | Replace with `EterPackManager_Vpk.cpp` (or merge) |
| `source/UserInterface/UserInterface.cpp` | 2-line change (see below) |

## Required Libraries

### OpenSSL 1.1+
- Windows: Download from https://slproweb.com/products/Win32OpenSSL.html
- Headers: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Link: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Link: `lz4.lib`
- **Note:** FliegeV3 already uses LZ4 for pack compression. If your project
  already links `lz4.lib`, no additional setup is needed for this library.

### Zstandard
- https://github.com/facebook/zstd/releases
- Header: `<zstd.h>`
- Link: `zstd.lib` (or `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Headers: `<brotli/decode.h>`
- Link: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copy into your project: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: also add `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

## Step-by-Step Integration

### Step 1: Add files to the EterPack VS project

1. Copy `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` into `source/EterPack/`
2. Copy the BLAKE3 C files into `source/EterPack/` (or a shared location)
3. Add all new files to the EterPack project in Visual Studio
4. Add include paths for OpenSSL, Zstd, Brotli to **Additional Include Directories**
   - LZ4 should already be configured if your FliegeV3 build uses it
5. Add library paths to **Additional Library Directories**
6. Add `libssl.lib`, `libcrypto.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` to **Additional Dependencies**
   - `lz4.lib` should already be linked

### Step 2: Replace EterPackManager

**Option A (clean replacement):**
- Replace `source/EterPack/EterPackManager.h` with `EterPackManager_Vpk.h`
- Replace `source/EterPack/EterPackManager.cpp` with `EterPackManager_Vpk.cpp`
- Rename both to `EterPackManager.h` / `EterPackManager.cpp`

**Option B (merge):**
Add these to the existing `EterPackManager.h`:

```cpp
#include "VpkLoader.h"

// Inside the class declaration, add to public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Inside the class declaration, add to protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Then merge the implementations from `EterPackManager_Vpk.cpp` into your existing `.cpp`.

**Important:** Do NOT add `DecryptPackIV`, `WriteHybridCryptPackInfo`,
`RetrieveHybridCryptPackKeys`, or `RetrieveHybridCryptPackSDB` — FliegeV3
does not have these methods.

### Step 3: Patch UserInterface.cpp

In `source/UserInterface/UserInterface.cpp`, the pack registration loop:

**Before:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**After:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

And add this line **before** the registration loop:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

That's it. Two changes total:
1. `SetVpkPassphrase()` before the loop
2. `RegisterPack()` → `RegisterPackAuto()` (2 occurrences)

**Note:** Unlike the 40250 integration, FliegeV3 does NOT require any patches
to networking code. There are no `RetrieveHybridCryptPackKeys` or
`RetrieveHybridCryptPackSDB` calls to worry about.

### Step 4: Build

Build the EterPack project first, then the full solution. The VPK code compiles
alongside the existing EterPack code — nothing is removed.

**FliegeV3-specific build notes:**
- FliegeV3 uses Boost. The `boost::unordered_multimap` in `CEterFileDict` is
  compatible with VPK — `InsertItem()` works identically.
- Ensure `StdAfx.h` in EterPack includes `<boost/unordered_map.hpp>` (it should
  already if the FliegeV3 project compiles).
- If you see linker errors about `boost::unordered_multimap`, verify Boost
  include paths are set in the EterPack project properties.

## How It Works

### Registration Flow

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

### File Access Flow

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
  │
  └─ else (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [original decompression: LZ4 + XTEA / MCOZ header]
```

### Memory Management

CVpkPack uses `CMappedFile::AppendDataBlock()` — the same mechanism that
CEterPack uses. The decompressed data is copied into a CMappedFile-owned
buffer that is automatically freed when the CMappedFile goes out of scope.

## Converting Packs

Use the 42pak-generator tool to convert your existing FliegeV3 pack files:

```
# CLI — convert a single EPK pair to VPK
42pak-generator.exe convert --source metin2_patch_etc.eix --passphrase "your-secret"

# CLI — build a VPK from a folder
42pak-generator.exe build --source ./ymir_work/item/ --output pack/item.vpk \
    --passphrase "your-secret" --algorithm lz4 --compression 6
```

Or use the GUI's Create view to build VPK archives from folders.

## Migration Strategy

1. **Set up libraries** — add OpenSSL, Zstd, Brotli, BLAKE3 to your project (LZ4 likely already present)
2. **Add VPK files** — copy the 6 new source files into `source/EterPack/`
3. **Patch EterPackManager** — merge or replace the header and implementation
4. **Patch UserInterface.cpp** — the 2-line change
5. **Build** — verify everything compiles
6. **Convert one pack** — e.g. `metin2_patch_etc` → test it loads correctly
7. **Convert remaining packs** — one at a time or all at once
8. **Remove old EPK files** — once all packs are converted

Since `RegisterPackAuto` falls back to EPK when no VPK exists, you can
convert packs incrementally without breaking anything.

## Passphrase Configuration

| Method | When to use |
|--------|-------------|
| Hardcoded in source | Private servers, simplest approach |
| Config file (`metin2.cfg`) | Easy to change without recompiling |
| Server-sent at login | Maximum security — passphrase changes per session |

For server-sent passphrase, modify `CAccountConnector` to receive it
in the auth response and call `CEterPackManager::Instance().SetVpkPassphrase()`
before any pack access occurs.

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Pack files not found | `.vpk` extension missing | Ensure pack name doesn't include extension — `RegisterPackAuto` appends `.vpk` |
| "HMAC verification failed" | Wrong passphrase | Check `SetVpkPassphrase` is called before `RegisterPackAuto` |
| Files not found in VPK | Path case mismatch | VPK normalizes to lowercase with `/` separators |
| Crash in `Get2()` | `compressed_type` sentinel collision | Ensure no EPK files use `compressed_type == -1` (none do in standard Metin2) |
| LZ4/Zstd/Brotli link error | Missing library | Add the decompression lib to Additional Dependencies |
| BLAKE3 compile error | Missing source files | Ensure all `blake3_*.c` files are in the project |
| Boost linker errors | Missing Boost includes | Verify Boost include paths in EterPack project properties |
| `ENABLE_LOCAL_FILE_LOADING` issues | Loose files not found in release | Define the macro in debug builds only — matches FliegeV3 convention |
