<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Client Integration Guide (MartySama 5.8)

> **Profile:** MartySama 5.8 - targets the Boost-based multi-cipher HybridCrypt architecture.
> For 40250/ClientVS22 integration, see `../40250/INTEGRATION_GUIDE.md`.
> For FliegeV3 integration, see `../FliegeV3/INTEGRATION_GUIDE.md`.

Drop-in replacement for the EterPack (EIX/EPK) system. Based on the actual
MartySama 5.8 client source code (`Binary & S-Source/Binary/Client/EterPack/`).
All file paths reference the real MartySama source tree.

## What You Get

| Feature | EterPack (old) | VPK (new) |
|---------|---------------|-----------|
| Encryption | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (hardware-accelerated) |
| Compression | LZO | LZ4 / Zstandard / Brotli |
| Integrity | CRC32 per-file | BLAKE3 per-file + HMAC-SHA256 archive |
| File format | .eix + .epk pair | Single .vpk file |
| Key management | Server-sent Panama IV + HybridCrypt SDB | PBKDF2-SHA512 passphrase |

## MartySama 5.8 vs 40250 vs FliegeV3

| Aspect | 40250 | MartySama 5.8 | FliegeV3 |
|--------|-------|---------------|----------|
| Crypto system | Camellia/Twofish/XTEA (HybridCrypt) | Same as 40250 | XTEA-only |
| Key delivery | Server-sent Panama IV + SDB | Same as 40250 | Static compiled keys |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| Container types | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| Header style | Include guards | `#pragma once` | Include guards |
| Anti-tamper | None | `#ifdef __THEMIDA__` VM_START/VM_END | None |
| Manager methods | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | Same as 40250 | None |
| Compression | LZO | LZO | LZ4 |
| Index struct | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` |

The VPK integration code adapts to MartySama's Boost containers and Themida
markers while maintaining the same API surface (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`).

## Architecture

VPK integrates via `CEterFileDict` - the same `boost::unordered_multimap`
hash lookup the original MartySama EterPack uses. When
`CEterPackManager::RegisterPackAuto()` finds a `.vpk` file, it creates a
`CVpkPack` instead of `CEterPack`. The `CVpkPack` registers its entries into
the shared `m_FileDict` with a sentinel marker. When `GetFromPack()` resolves
a filename, it checks the marker and dispatches to either `CEterPack::Get2()`
or `CVpkPack::Get2()` transparently.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Files

### New files to add to `Client/EterPack/`

| File | Purpose |
|------|---------|
| `VpkLoader.h` | `CVpkPack` class - drop-in replacement for `CEterPack` |
| `VpkLoader.cpp` | Full implementation: header parsing, entry table, decrypt+decompress, BLAKE3 verify |
| `VpkCrypto.h` | Crypto utilities: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementations using OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Patched `CEterPackManager` header with VPK + HybridCrypt + Boost |
| `EterPackManager_Vpk.cpp` | Patched `CEterPackManager` with VPK dispatch, HybridCrypt, Boost iterators, Themida |

### Files to modify

| File | Change |
|------|--------|
| `Client/EterPack/EterPackManager.h` | Replace with `EterPackManager_Vpk.h` (or merge) |
| `Client/EterPack/EterPackManager.cpp` | Replace with `EterPackManager_Vpk.cpp` (or merge) |
| `Client/UserInterface/UserInterface.cpp` | 2-line change (see below) |

## Required Libraries

### OpenSSL 1.1+
- Windows: Download from https://slproweb.com/products/Win32OpenSSL.html
- Headers: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Link: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Header: `<lz4.h>`
- Link: `lz4.lib`
- **Note:** MartySama 5.8 does NOT include LZ4 by default. This is a new dependency.

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

### Existing Dependencies (already in MartySama 5.8)

MartySama 5.8 already ships with these libraries that are **not** needed for VPK
but remain in your project for the legacy EPK codepath:

| Library | Used by |
|---------|---------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), Panama cipher, hash functions |
| Boost | `boost::unordered_map`/`boost::unordered_multimap` in EterPack, EterPackManager, CEterFileDict |
| LZO | EPK decompression (pack types 1, 2) |

These continue to work alongside VPK. No changes needed.

## Step-by-Step Integration

### Step 1: Add files to the EterPack VS project

1. Copy `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` into `Client/EterPack/`
2. Copy the BLAKE3 C files into `Client/EterPack/` (or a shared location)
3. Add all new files to the EterPack project in Visual Studio
4. Add include paths for OpenSSL, LZ4, Zstd, Brotli to **Additional Include Directories**
5. Add library paths to **Additional Library Directories**
6. Add `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` to **Additional Dependencies**

### Step 2: Replace EterPackManager

**Option A (clean replacement):**
- Replace `Client/EterPack/EterPackManager.h` with `EterPackManager_Vpk.h`
- Replace `Client/EterPack/EterPackManager.cpp` with `EterPackManager_Vpk.cpp`
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
    typedef boost::unordered_map<std::string, CVpkPack*> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

**Important:** Note the use of `boost::unordered_map` instead of `std::unordered_map`
to match MartySama's convention. The provided `EterPackManager_Vpk.h` already
uses Boost containers throughout.

Then merge the implementations from `EterPackManager_Vpk.cpp` into your existing `.cpp`.

### Step 3: Patch UserInterface.cpp

In `Client/UserInterface/UserInterface.cpp`, the pack registration loop
(around line 557–579):

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

See `UserInterface_VpkPatch.cpp` for the full annotated patch with before/after.

### Step 4: Build

Build the EterPack project first, then the full solution. The VPK code compiles
alongside the existing EterPack code - nothing is removed.

**MartySama 5.8-specific build notes:**
- MartySama uses Boost throughout. The `boost::unordered_multimap` in
  `CEterFileDict` is fully compatible with VPK - `InsertItem()` works identically.
- Ensure `StdAfx.h` in EterPack includes `<boost/unordered_map.hpp>` (it should
  already if the MartySama project compiles).
- CryptoPP is already in the solution for HybridCrypt - no conflict with OpenSSL.
  They serve completely different purposes (CryptoPP for legacy EPK, OpenSSL for VPK).
- If you use Themida, the `#ifdef __THEMIDA__` markers in `EterPackManager_Vpk.cpp`
  are already in place. Define `__THEMIDA__` in your build configuration if you want
  the VM_START/VM_END markers active.

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
  │            └─ CMappedFile owns the memory (freed in destructor)
  │
  └─ else (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [original decompression: LZO / Panama / HybridCrypt]
```

### HybridCrypt Coexistence

MartySama 5.8 receives encryption keys from the server at login:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

These methods iterate `m_PackMap` (which contains only `CEterPack*` objects).
VPK packs are stored in `m_VpkPackMap` separately, so HybridCrypt key delivery
is completely transparent - both systems coexist without any changes to the
networking code.

### Memory Management

CVpkPack uses `CMappedFile::AppendDataBlock()` - the same mechanism that
CEterPack uses for HybridCrypt data. The decompressed data is copied into
a CMappedFile-owned buffer that is automatically freed when the CMappedFile
goes out of scope. No manual cleanup needed.

## Converting Packs

Use the 42pak-generator tool to convert your existing MartySama pack files:

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

Or use the GUI's Create view to build VPK archives from folders.

## Migration Strategy

1. **Set up libraries** - add OpenSSL, LZ4, Zstd, Brotli, BLAKE3 to your project
2. **Add VPK files** - copy the 6 new source files into `Client/EterPack/`
3. **Patch EterPackManager** - merge or replace the header and implementation
4. **Patch UserInterface.cpp** - the 2-line change
5. **Build** - verify everything compiles
6. **Convert one pack** - e.g. `metin2_patch_etc` → test it loads correctly
7. **Convert remaining packs** - one at a time or all at once
8. **Remove old EPK files** - once all packs are converted

Since `RegisterPackAuto` falls back to EPK when no VPK exists, you can
convert packs incrementally without breaking anything.

## Passphrase Configuration

| Method | When to use |
|--------|-------------|
| Hardcoded in source | Private servers, simplest approach |
| Config file (`metin2.cfg`) | Easy to change without recompiling |
| Server-sent at login | Maximum security - passphrase changes per session |

For server-sent passphrase, modify `CAccountConnector` to receive it in the
auth response and call `CEterPackManager::Instance().SetVpkPassphrase()` before
any pack access occurs. MartySama's `AccountConnector.cpp` already handles
custom server packets - add a new handler alongside the existing
`RetrieveHybridCryptPackKeys` call.

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Pack files not found | `.vpk` extension missing | Ensure pack name doesn't include extension - `RegisterPackAuto` appends `.vpk` |
| "HMAC verification failed" | Wrong passphrase | Check `SetVpkPassphrase` is called before `RegisterPackAuto` |
| Files not found in VPK | Path case mismatch | VPK normalizes to lowercase with `/` separators |
| Crash in `Get2()` | `compressed_type` sentinel collision | Ensure no EPK files use `compressed_type == -1` (none do in standard Metin2) |
| LZ4/Zstd/Brotli link error | Missing library | Add the decompression lib to Additional Dependencies |
| BLAKE3 compile error | Missing source files | Ensure all `blake3_*.c` files are in the project |
| Boost linker errors | Missing Boost includes | Verify Boost include paths in EterPack project properties |
| CryptoPP + OpenSSL conflict | Duplicate symbol names | Should not happen - they define separate symbols. If it does, check for `using namespace` pollution |
| Themida errors | `__THEMIDA__` not defined | Define it in your build config, or remove `#ifdef __THEMIDA__` blocks from `EterPackManager_Vpk.cpp` |
