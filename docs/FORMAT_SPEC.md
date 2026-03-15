# VPK Binary Format Specification — Version 1

This document describes the `.vpk` file format used by 42pak-generator.

## File Layout

```
Offset 0x000                    ┌──────────────────────┐
                                │  VpkHeader (512 B)   │
Offset 0x200 (aligned 4096)     ├──────────────────────┤
                                │  Data Block 0        │
                                ├──────────────────────┤
                                │  Data Block 1        │
                                ├──────────────────────┤
                                │  ...                 │
EntryTableOffset                ├──────────────────────┤
                                │  Entry Table         │
                                │  (variable size)     │
FileSize - 32                   ├──────────────────────┤
                                │  HMAC-SHA256 (32 B)  │
                                └──────────────────────┘
```

## VpkHeader (512 bytes, fixed)

| Offset | Size | Type | Field | Description |
|--------|------|------|-------|-------------|
| 0 | 4 | bytes | Magic | `42PK` (ASCII: `0x34 0x32 0x50 0x4B`) |
| 4 | 2 | uint16 | Version | Format version (currently `1`) |
| 6 | 4 | int32 | EntryCount | Total number of file entries |
| 10 | 8 | int64 | EntryTableOffset | Byte offset of entry table |
| 18 | 4 | int32 | EntryTableSize | Size of serialized entry table (bytes) |
| 22 | 1 | bool | IsEncrypted | `1` = encryption enabled |
| 23 | 4 | int32 | CompressionLevel | 0=none, 1-12=LZ4 level |
| 27 | 1 | bool | FileNamesMangled | `1` = filenames obfuscated |
| 28 | 8 | int64 | CreatedAtUtcTicks | .NET DateTime.Ticks (UTC) |
| 36 | 32 | bytes | Salt | PBKDF2 salt (zeroed if unencrypted) |
| 68 | 64 | bytes | Author | UTF-8, null-padded |
| 132 | 128 | bytes | Comment | UTF-8, null-padded |
| 260 | 252 | bytes | Reserved | Zero-padded to 512 bytes |

All multi-byte values are **little-endian**.

## Data Blocks

Each file's processed data (compressed then encrypted) is stored at an aligned offset. Data blocks are aligned to **4096-byte** boundaries. Padding between blocks is zero-filled.

### Processing Pipeline (Write)

```
Original Data
  │
  ├─ if CompressionLevel > 0:
  │    LZ4 Compress → prepend 4-byte little-endian original size
  │
  ├─ if IsEncrypted:
  │    Generate 12-byte random nonce
  │    AES-256-GCM Encrypt(data, key, nonce) → (ciphertext, 16-byte tag)
  │
  └─ Write to data block at aligned offset
```

### Processing Pipeline (Read)

```
Stored Data
  │
  ├─ if IsEncrypted:
  │    AES-256-GCM Decrypt(data, key, nonce, tag) → plaintext
  │
  ├─ if IsCompressed:
  │    Read 4-byte original size prefix
  │    LZ4 Decompress(data[4:], originalSize)
  │
  ├─ BLAKE3 Hash → verify against ContentHash
  │
  └─ Return original data
```

## Entry Table

The entry table is stored as a sequence of variable-length `VpkEntry` records. If the archive is encrypted, the entire entry table is wrapped in AES-256-GCM:

**Unencrypted:**
```
[VpkEntry0][VpkEntry1]...[VpkEntryN]
```

**Encrypted:**
```
[12-byte Nonce][16-byte AuthTag][AES-GCM Encrypted Entry Data]
```

### VpkEntry Record Format

Each entry is serialized sequentially with no padding:

| Size | Type | Field | Description |
|------|------|-------|-------------|
| 4 | int32 | StoredNameLen | Length of StoredName bytes |
| var | bytes | StoredName | UTF-8 encoded stored filename |
| 4 | int32 | FileNameLen | Length of FileName bytes |
| var | bytes | FileName | UTF-8 encoded original filename |
| 8 | int64 | OriginalSize | Uncompressed file size |
| 8 | int64 | StoredSize | Size of data block (after compress+encrypt) |
| 8 | int64 | DataOffset | Absolute byte offset in VPK file |
| 4 | int32 | ContentHashLen | Length of content hash (32 for BLAKE3) |
| var | bytes | ContentHash | BLAKE3 hash of original uncompressed data |
| 1 | bool | IsCompressed | `1` if LZ4 compressed |
| 1 | bool | IsEncrypted | `1` if AES-GCM encrypted |
| 4 | int32 | NonceLen | Length of nonce (12 if encrypted, 0 if not) |
| var | bytes | Nonce | AES-GCM nonce (per-file, unique) |
| 4 | int32 | AuthTagLen | Length of auth tag (16 if encrypted, 0 if not) |
| var | bytes | AuthTag | AES-GCM authentication tag |

## HMAC-SHA256 Trailer

The last 32 bytes of the file contain an HMAC-SHA256 computed over all preceding bytes (header + data blocks + entry table).

- **For encrypted archives**: HMAC key is derived from the passphrase (bytes 32-63 of the 64-byte PBKDF2 output).
- **For unencrypted archives**: 32 zero bytes (no integrity check).

## Key Derivation

```
Input:  passphrase (string), salt (32 random bytes)
Prefix: "42PK-v1:" prepended to passphrase
Algo:   PBKDF2-SHA512, 100,000 iterations
Output: 64 bytes
  ├─ Bytes  0-31: AES-256 encryption key
  └─ Bytes 32-63: HMAC-SHA256 signing key
```

## Filename Paths

- Filenames use **forward slashes** (`/`) as path separators
- Paths are relative to the archive root (no leading slash)
- Maximum filename length: 512 bytes (UTF-8 encoded)
- Lookups are **case-insensitive**
- Example: `d_ymir_work/item/weapon/sword_01.gr2`

## LZ4 Compressed Data Format

When a file is LZ4-compressed, the stored data has this layout:

```
┌──────────────────────────────────────────┐
│  Original Size (4 bytes, little-endian)  │
├──────────────────────────────────────────┤
│  LZ4 Compressed Data                    │
└──────────────────────────────────────────┘
```

The 4-byte prefix allows the decompressor to allocate the correct output buffer size without consulting the entry table (useful for streaming).

## Compatibility Notes

- Format version 1 is the initial release
- Readers should reject archives with `Version > 1` (no forward compatibility)
- The `Reserved` padding in the header is for future use — must be zero in v1
- Software that only reads unencrypted VPKs can skip all crypto operations
