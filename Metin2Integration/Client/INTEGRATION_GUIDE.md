# Metin2 Client Integration Guide - 42pak VPK Format

This guide explains how to integrate the VPK archive loader into your Metin2 client,
replacing or supplementing the legacy EIX/EPK pack system.

## Overview

The 42pak VPK format uses a single `.vpk` file instead of the dual `.eix`/`.epk` files.
It provides:
- AES-256-GCM per-file encryption (replaces TEA / Panama / HybridCrypt)
- LZ4 compression (replaces LZO - faster decompression)
- BLAKE3 content hashing (replaces CRC32 - stronger integrity)
- HMAC-SHA256 archive-level tamper detection

## Files to Add

Copy these files into your `source/EterPack/` directory:

| File | Description |
|------|-------------|
| `VpkLoader.h` | CVpkPack class declaration |
| `VpkLoader.cpp` | CVpkPack implementation |
| `VpkCrypto.h` | Crypto/compression utility declarations |
| `VpkCrypto.cpp` | Crypto/compression implementations |

## Required Libraries

### OpenSSL (1.1+)
- Download: https://slproweb.com/products/Win32OpenSSL.html (for Windows)
- Include: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Link: `libssl.lib`, `libcrypto.lib`

### LZ4
- Download: https://github.com/lz4/lz4/releases
- Include: `<lz4.h>`
- Link: `lz4.lib`

### BLAKE3
- Download: https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copy `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c` into your project
- On x86/x64: also add `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

## Integration Steps

### Step 1: Add to Visual Studio Project

1. Add the 4 VPK files to your EterPack project (or a new VpkPack project).
2. Add OpenSSL, LZ4, and BLAKE3 include paths to **Additional Include Directories**.
3. Add OpenSSL and LZ4 library paths to **Additional Library Directories**.
4. Add `libssl.lib`, `libcrypto.lib`, `lz4.lib` to **Additional Dependencies**.

### Step 2: Modify CEterPackManager to support VPK

Open `source/EterPack/EterPackManager.h` and add:

```cpp
#include "VpkLoader.h"

class CEterPackManager : public CSingleton<CEterPackManager>
{
public:
    // ... existing members ...

    // Add VPK support
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory,
                         const char* passphrase = nullptr);

protected:
    // ... existing members ...

    // Add VPK pack storage
    typedef std::list<CVpkPack*> TVpkPackList;
    TVpkPackList m_VpkPackList;
};
```

### Step 3: Implement RegisterVpkPack

In `source/EterPack/EterPackManager.cpp`:

```cpp
bool CEterPackManager::RegisterVpkPack(const char* c_szName,
                                       const char* c_szDirectory,
                                       const char* passphrase)
{
    CVpkPack* pPack = new CVpkPack;

    if (passphrase && passphrase[0])
        pPack->SetPassphrase(passphrase);

    if (!pPack->Create(m_FileDict, c_szName, c_szDirectory))
    {
        delete pPack;
        return false;
    }

    m_VpkPackList.push_back(pPack);
    return true;
}
```

### Step 4: Modify GetFromPack to check VPK packs

In `CEterPackManager::GetFromPack()`, add a VPK check before the file-dict lookup:

```cpp
bool CEterPackManager::GetFromPack(CMappedFile& rMappedFile,
                                   const char* c_szFileName,
                                   LPCVOID* pData)
{
    // --- NEW: Check VPK packs first ---
    for (auto* pVpk : m_VpkPackList)
    {
        if (pVpk->IsExist(c_szFileName))
        {
            if (pVpk->Get(rMappedFile, c_szFileName, pData))
                return true;
        }
    }

    // --- Existing EIX/EPK lookup below (unchanged) ---
    // ... existing CEterFileDict-based code ...
}
```

### Step 5: Modify PackInitialize to load VPK packs

In `source/UserInterface/UserInterface.cpp`, modify the `PackInitialize` function:

```cpp
bool PackInitialize(const char* c_szFolder)
{
    // ... existing code until the pack-registration loop ...

    // In the loop where packs are registered:
    while (std::getline(indexFile, line))
    {
        // ... existing code to parse directory and pack name ...

        std::string vpkPath = "pack/" + packName + ".vpk";
        std::string eixPath = "pack/" + packName + ".eix";

        // Prefer VPK if it exists, otherwise fall back to EIX/EPK
        if (FileExists(vpkPath.c_str()))
        {
            CEterPackManager::Instance().RegisterVpkPack(
                vpkPath.c_str(), directory.c_str(), g_vpkPassphrase);
        }
        else if (FileExists(eixPath.c_str()))
        {
            CEterPackManager::Instance().RegisterPack(
                ("pack/" + packName).c_str(), directory.c_str());
        }
    }

    // ... rest of existing code ...
}
```

### Step 6: Handle Memory Management

The existing Metin2 client uses `CMappedFile` for memory-mapped reads. Since VPK uses
encrypted/compressed data that must be decoded at read time, `CVpkPack::Get()` allocates
a buffer with `new[]`. You have two options:

**Option A (Simple):** Modify `CMappedFile` to accept heap-allocated buffers:

```cpp
class CMappedFile
{
public:
    // Add alongside existing Create methods:
    void AssignHeapBuffer(unsigned char* data, int size)
    {
        m_pHeapData = data;
        m_dataSize = size;
    }

    ~CMappedFile()
    {
        delete[] m_pHeapData; // safe even if null
    }

private:
    unsigned char* m_pHeapData = nullptr;
};
```

**Option B (Cache):** Maintain a decompressed-data cache in `CVpkPack`:

```cpp
// In CVpkPack, keep a cache of recently decompressed files:
std::unordered_map<unsigned int, std::vector<unsigned char>> m_cache;
```

## Directory Layout - pack/ folder

After converting your pak files with 42pak-generator:

```
pack/
├── Index                          ← modify to list .vpk packs
├── root.vpk                       ← replaces root.eix + root.epk
├── metin2_patch_etc.vpk           ← replaces .eix + .epk pair
├── metin2_patch_etc_texcache.vpk
├── locale_en.vpk
└── ...
```

Your `pack/Index` file becomes:

```
PACK
metin2_patch_etc	metin2_patch_etc
metin2_patch_etc_texcache	metin2_patch_etc
locale_en	locale_en
```

The `PackInitialize` code checks for `.vpk` first, then falls back to `.eix`/`.epk`.
This means you can convert packs one at a time.

## Passphrase Configuration

For encrypted VPKs, you'll need to embed or derive the passphrase. Options:

1. **Hardcoded** (simplest for private servers):
   ```cpp
   static const char* g_vpkPassphrase = "your-server-secret";
   ```

2. **Derived from hardware ID** (prevents sharing paks between machines):
   ```cpp
   std::string GetMachinePassphrase()
   {
       // Combine machine GUID, CPU ID, etc.
       // Hash with BLAKE3 to produce a deterministic passphrase
   }
   ```

3. **Server-provided** (downloaded at login): The server sends the passphrase
   after successful authentication, used only for the current session.

## Performance Notes

- **LZ4 decompression** is ~3x faster than LZO for typical game assets
- **BLAKE3 hashing** is ~5x faster than SHA-256 and ~10x faster than MD5
- **AES-256-GCM** uses hardware AES-NI on modern CPUs - minimal overhead
- First access to a VPK entry requires reading + decrypting the entry table
  (cached after first read). Subsequent file reads are O(1) lookup.

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "Not a valid VPK file" | Wrong magic bytes | Ensure file was built with 42pak-generator |
| "HMAC verification failed" | Wrong passphrase or tampered file | Check passphrase matches |
| Authentication failed on decrypt | Corrupted data or wrong nonce | Rebuild the VPK archive |
| LZ4 decompression returns negative | Data corruption | Validate with 42pak-generator tool first |
| Files not found after VPK registration | Filename case mismatch | VPK uses forward-slash `/` paths |
