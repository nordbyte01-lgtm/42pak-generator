# 42pak-generator

A modern, open-source pak file manager for the Metin2 private server community. Replaces the legacy EIX/EPK archive format with the new **VPK** format featuring AES-256-GCM encryption, LZ4/Zstandard/Brotli compression, BLAKE3 hashing, and HMAC-SHA256 tamper detection.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

## Features

- **Create VPK archives** - Pack directories into single `.vpk` files with optional encryption and compression
- **Manage existing archives** - Browse, search, extract, and validate VPK archives
- **Convert EIX/EPK → VPK** - One-click migration from legacy EterPack format
- **AES-256-GCM encryption** - Per-file authenticated encryption with unique nonces
- **LZ4 / Zstandard / Brotli compression** - Choose the algorithm that fits your needs
- **BLAKE3 content hashing** - Cryptographic integrity verification for every file
- **HMAC-SHA256** - Archive-level tamper detection
- **Filename mangling** - Optional obfuscation of file paths within archives
- **Metin2 C++ integration** - Drop-in client and server loader code included

## VPK vs EIX/EPK Comparison

| Feature | EIX/EPK (Legacy) | VPK (42pak) |
|---------|:-:|:-:|
| Encryption | TEA / Panama / HybridCrypt | AES-256-GCM |
| Compression | LZO | LZ4 / Zstandard / Brotli |
| Integrity | CRC32 | BLAKE3 + HMAC-SHA256 |
| File format | Dual file (.eix + .epk) | Single file (.vpk) |
| Archive count | 512 max entries | Unlimited |
| Filename length | 160 bytes | 512 bytes (UTF-8) |
| Key derivation | Hardcoded keys | PBKDF2-SHA512 (200k iterations) |
| Tamper detection | None | HMAC-SHA256 whole-archive |

## Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 version 1809+ (for WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre-installed)

### Build

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### Run

```bash
dotnet run --project src/FortyTwoPak.UI
```

### Run Tests

```bash
dotnet test
```

## Project Structure

```
42pak-generator/
├── 42pak-generator.sln              # Solution file
├── src/
│   ├── FortyTwoPak.Core/            # Core library (format, crypto, compression)
│   │   ├── VpkFormat/               # VPK header, entry, archive classes
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # LZ4 compressor/decompressor
│   │   ├── Legacy/                  # EIX/EPK reader and converter
│   │   └── Utils/                   # File name mangling, progress reporting
│   ├── FortyTwoPak.UI/              # WebView2 desktop application
│   │   ├── MainWindow.cs            # WinForms host with WebView2 control
│   │   ├── BridgeService.cs         # JavaScript ↔ C# interop bridge
│   │   └── wwwroot/                 # HTML/CSS/JS frontend
│   │       ├── index.html            # Single-page app with 6 tabs
│   │       ├── css/app.css           # Dark theme (Steam/Epic-inspired)
│   │       └── js/app.js             # Tab navigation, wizard, file grid
│   └── FortyTwoPak.Tests/           # xUnit test suite
├── Metin2Integration/
│   ├── Client/                      # C++ files for metin2 game client
│   │   ├── VpkLoader.h/cpp          # CVpkPack - replaces CEterPack
│   │   ├── VpkCrypto.h/cpp          # Crypto utilities (OpenSSL + BLAKE3 + LZ4)
│   │   └── INTEGRATION_GUIDE.md     # Step-by-step client integration
│   └── Server/                      # C++ files for metin2 game server
│       ├── VpkHandler.h/cpp         # CVpkHandler - server-side VPK reader
│       └── INTEGRATION_GUIDE.md     # Server integration instructions
└── docs/
    └── FORMAT_SPEC.md               # VPK binary format specification
```

## VPK File Format

Single-file archive with this binary layout:

```
┌─────────────────────────────────────┐
│ VpkHeader (512 bytes, fixed)        │  Magic "42PK", version, entry count,
│                                     │  encryption flag, salt, author, etc.
├─────────────────────────────────────┤
│ Data Block 0 (aligned to 4096)      │  File content (compressed + encrypted)
├─────────────────────────────────────┤
│ Data Block 1 (aligned to 4096)      │
├─────────────────────────────────────┤
│ ...                                 │
├─────────────────────────────────────┤
│ Entry Table (variable size)         │  Array of VpkEntry records. If encrypted,
│                                     │  wrapped in AES-GCM (nonce + tag + data).
├─────────────────────────────────────┤
│ HMAC-SHA256 (32 bytes)              │  Covers everything above. Zero if unsigned.
└─────────────────────────────────────┘
```

### Encryption Pipeline

For each file: `Original → LZ4 Compress → AES-256-GCM Encrypt → Store`

On extraction: `Read → AES-256-GCM Decrypt → LZ4 Decompress → BLAKE3 Verify`

Key derivation: `PBKDF2-SHA512("42PK-v1:" + passphrase, salt, 100000 iterations) → 64 bytes`
- First 32 bytes: AES-256 key
- Last 32 bytes: HMAC-SHA256 key

## Usage

### Creating a VPK Archive

1. Open 42pak-generator
2. Go to **Create Pak** tab
3. Select source directory containing files to pack
4. Choose output `.vpk` file path
5. Configure options:
   - **Encryption**: Toggle on and enter a passphrase
   - **Compression Level**: 0 (none) to 12 (maximum)
   - **Filename Mangling**: Obfuscate paths within the archive
   - **Author / Comment**: Optional metadata
6. Click **Build**

### Converting EIX/EPK to VPK

1. Go to **Convert** tab
2. Select the `.eix` file (the `.epk` must be in the same directory)
3. Choose output `.vpk` path
4. Optionally set encryption passphrase
5. Click **Convert**

### Metin2 Client Integration

See [Metin2Integration/Client/INTEGRATION_GUIDE.md](Metin2Integration/Client/INTEGRATION_GUIDE.md) for detailed instructions on integrating VPK into the Metin2 game client.

### Metin2 Server Integration

See [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) for server-side integration.

## Technologies

| Component | Technology |
|-----------|------------|
| Runtime | .NET 8.0 (C#) |
| UI | WebView2 (WinForms host) |
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Encryption | AES-256-GCM via `System.Security.Cryptography` |
| Key Derivation | PBKDF2-SHA512 (200,000 iterations) |
| Hashing | BLAKE3 via [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| Compression | LZ4 via [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard via [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (built-in) |
| Tamper Detection | HMAC-SHA256 |
| C++ Crypto | OpenSSL 1.1+ |
| Testing | xUnit |

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Acknowledgments

- The Metin2 private server community for keeping the game alive
- The original YMIR Entertainment EterPack format for providing the foundation
- [BLAKE3 team](https://github.com/BLAKE3-team/BLAKE3) for the fast hash function
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) for the .NET LZ4 binding
