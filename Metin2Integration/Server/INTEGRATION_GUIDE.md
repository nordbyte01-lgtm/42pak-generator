# Metin2 Server Integration Guide - 42pak VPK Format

This guide explains how to use VPK archives on the Metin2 game server for
proto files, locale data, and other server-side resources.

## Overview

The game server typically reads these from the filesystem or legacy pak files:
- `locale/XX/item_proto` - Item definitions
- `locale/XX/mob_proto` - Monster definitions  
- `locale/XX/item_names.txt`, `mob_names.txt` - Display names
- Various config and script files

With VPK, you can bundle these into encrypted, tamper-resistant archives.

## Files to Add

Copy into your server source directory (e.g. `game/src/`):

| File | Description |
|------|-------------|
| `VpkHandler.h` | CVpkHandler class declaration |
| `VpkHandler.cpp` | Self-contained implementation (no EterPack dependency) |

The server-side handler is fully self-contained - it includes its own crypto
routines and does not depend on any client-side EterPack headers.

## Required Libraries

Same as client-side:
- **OpenSSL 1.1+** - `libssl`, `libcrypto`
- **LZ4** - `liblz4`
- **BLAKE3** - C reference implementation

### Linux Build (typical server environment)

```bash
# Install dependencies
sudo apt-get install libssl-dev liblz4-dev

# Clone BLAKE3 C reference
git clone https://github.com/BLAKE3-team/BLAKE3.git
cp BLAKE3/c/blake3.h BLAKE3/c/blake3.c BLAKE3/c/blake3_dispatch.c \
   BLAKE3/c/blake3_portable.c game/src/

# Add to Makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

### FreeBSD Build

```bash
pkg install openssl liblz4
# Same BLAKE3 setup as Linux
```

## Integration Steps

### Step 1: Loading Proto Data from VPK

Replace direct filesystem reads with VPK reads. Example for `item_proto`:

**Before (existing code in `db/src/ProtoReader.cpp` or similar):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**After (VPK integration):**
```cpp
#include "VpkHandler.h"

// At server startup, open the locale VPK
static CVpkHandler g_localeVpk;

bool LoadLocaleVpk(const char* locale)
{
    char path[256];
    snprintf(path, sizeof(path), "pack/locale_%s.vpk", locale);
    return g_localeVpk.Open(path, "your-server-passphrase");
}

// In the proto loading function:
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

    for (int i = 0; i < count; ++i)
        // process items[i]...

    return true;
}
```

### Step 2: Admin Validation Tool

Add a simple validation command to verify VPK integrity:

```cpp
// In your admin command handler:
ACMD(do_vpk_validate)
{
    const char* vpkPath = argument; // e.g. "pack/locale_en.vpk"

    CVpkHandler handler;
    if (!handler.Open(vpkPath, g_serverPassphrase))
    {
        ch->ChatPacket(CHAT_TYPE_INFO, "Failed to open VPK: %s", vpkPath);
        return;
    }

    auto result = handler.Validate();
    ch->ChatPacket(CHAT_TYPE_INFO, "VPK: %d/%d files valid, %d errors",
                   result.ValidEntries, result.TotalEntries,
                   (int)result.Errors.size());

    for (const auto& err : result.Errors)
        ch->ChatPacket(CHAT_TYPE_INFO, "  Error: %s", err.c_str());
}
```

### Step 3: File Listing for Debug

```cpp
ACMD(do_vpk_list)
{
    CVpkHandler handler;
    if (!handler.Open(argument, g_serverPassphrase))
        return;

    handler.ForEachEntry([&](const char* name, long long size, bool compressed, bool encrypted)
    {
        ch->ChatPacket(CHAT_TYPE_INFO, "  %s (%lld bytes, %s%s)",
                       name, size,
                       compressed ? "LZ4" : "raw",
                       encrypted ? "+AES" : "");
    });
}
```

## Security Considerations

### Passphrase Management

On the server, the VPK passphrase should be:

1. **Stored in a config file** with restricted permissions:
   ```bash
   chmod 600 /usr/metin2/conf/vpk.conf
   ```
   ```
   # vpk.conf
   VPK_PASSPHRASE=your-strong-passphrase-here
   ```

2. **Loaded at startup** and not logged:
   ```cpp
   std::string LoadVpkPassphrase()
   {
       std::ifstream f("conf/vpk.conf");
       std::string line;
       while (std::getline(f, line))
       {
           if (line.substr(0, 15) == "VPK_PASSPHRASE=")
               return line.substr(15);
       }
       return "";
   }
   ```

3. **Never hardcoded** in server source (unlike the client, where some
   obfuscation is acceptable for anti-cheat purposes).

### Tamper Detection

The HMAC-SHA256 at the end of each VPK file ensures:
- Modifications to any file content are detected
- Added or removed entries are detected
- Header tampering is detected

If `Open()` returns false for an encrypted VPK, the archive may have been
tampered with or the passphrase is wrong.

## Directory Layout

```
/usr/metin2/
├── conf/
│   └── vpk.conf            ← passphrase config
├── pack/
│   ├── locale_en.vpk       ← locale data
│   ├── locale_de.vpk
│   ├── proto.vpk            ← item/mob proto tables
│   └── scripts.vpk          ← quest scripts, configs
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
- LZ4 decompression adds ~0.5ms per MB of compressed data
