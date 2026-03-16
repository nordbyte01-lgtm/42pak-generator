<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Server Integration Guide

Based on analysis of the actual 40250 server source code at
`Server/metin2_server+src/metin2/src/server/`.

## Key Finding: The Server Does Not Use EterPack

The 40250 server has **zero** references to EterPack, `.eix`, or `.epk` files.
All server file I/O uses plain `fopen()`/`fread()` or `std::ifstream`:

| Loader | Used By | Source File |
|--------|---------|-------------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, DragonSoul tables | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| Direct `fopen()` | CONFIG, map files, regen, locale, fishing, cube | Various |

The server only references packs in one place: **`ClientPackageCryptInfo.cpp`**,
which loads encryption keys from `package.dir/` and sends them to the **client**
for decrypting `.epk` files. The server itself never opens pack files.

## Integration Options

### Option A: Keep Server Files as Raw Files (Recommended)

Since the server already reads raw files from disk, the simplest approach is:
- Keep all server data files (protos, configs, maps) as raw files
- Use VPK only on the client side
- Remove or update `ClientPackageCryptInfo` to not send EPK keys

This is the recommended approach for most setups.

### Option B: Bundle Server Data in VPK Archives

If you want tamper-resistant, encrypted server data files, use `CVpkHandler`
to read from VPK archives instead of raw files.

## Files

| File | Purpose |
|------|---------|
| `VpkHandler.h` | Self-contained VPK reader (no EterPack dependency) |
| `VpkHandler.cpp` | Full implementation with built-in crypto |

The server handler is completely standalone - it includes its own AES-GCM,
PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard, and Brotli implementations
inline. No dependency on any client-side EterPack code.

## Required Libraries

### Linux (typical server)

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

### Makefile additions

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## Integration Examples

### Example 1: Loading Proto Data from VPK

The DB server loads protos from CSV/binary files in `db/src/ClientManagerBoot.cpp`:

**Before (raw file):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**After (VPK):**
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
    // process items...
    return true;
}
```

### Example 2: Reading Text Files from VPK

For `CTextFileLoader`-style files (mob_group.txt, etc.):

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
    // parse as before...
    return true;
}
```

### Example 3: Updating ClientPackageCryptInfo

The 40250 server sends EPK encryption keys to the client via
`game/src/ClientPackageCryptInfo.cpp` and `game/src/panama.cpp`.

When using VPK, you have two options:

**Option A: Remove pack key sending entirely**
If all packs are converted to VPK, the client no longer needs server-sent
EPK keys. Remove or disable `LoadClientPackageCryptInfo()` and `PanamaLoad()`
from the boot sequence in `game/src/main.cpp`.

**Option B: Keep hybrid support**
If some packs remain as EPK while others are VPK, keep the existing
key-sending code for EPK packs. VPK packs use their own passphrase
mechanism and don't need server-sent keys.

### Example 4: Admin Commands

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

## Passphrase Management

On the server, store the VPK passphrase securely:

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

## Compression Support

The server handler supports all three compression algorithms:

| Algorithm | ID | Library | Speed | Ratio |
|-----------|----|---------|-------|-------|
| LZ4 | 1 | liblz4 | Fastest | Good |
| Zstandard | 2 | libzstd | Fast | Better |
| Brotli | 3 | libbrotli | Slower | Best |

The algorithm is stored in the VPK header and detected automatically.
No configuration needed on the reading side.

## Directory Layout

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← locale data
│   ├── locale_de.vpk
│   ├── proto.vpk             ← item/mob proto tables
│   └── scripts.vpk           ← quest scripts, configs
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## Performance Notes

- VPK file reads are sequential I/O (one seek + one read per file)
- Entry table is parsed once at `Open()` and cached in memory
- For frequently accessed files, consider caching the `ReadFile()` output
- BLAKE3 hash verification adds ~0.1ms per MB of data

## Tamper Detection

The HMAC-SHA256 at the end of each VPK file ensures:
- Modifications to any file content are detected
- Added or removed entries are detected
- Header tampering is detected

If `Open()` returns false for an encrypted VPK, the archive may have been
tampered with or the passphrase is wrong.
- LZ4 decompression adds ~0.5ms per MB of compressed data
