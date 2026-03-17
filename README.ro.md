<p align="center">
  <img src="assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

<p align="center">
  <strong>🌐 Language / Język / Limbă / Dil / ภาษา / Idioma / Lingua / Idioma:</strong><br>
  <a href="README.md">🇬🇧 English</a> •
  <a href="README.pl.md">🇵🇱 Polski</a> •
  <a href="README.ro.md">🇷🇴 Română</a> •
  <a href="README.tr.md">🇹🇷 Türkçe</a> •
  <a href="README.th.md">🇹🇭 ไทย</a> •
  <a href="README.pt.md">🇧🇷 Português</a> •
  <a href="README.it.md">🇮🇹 Italiano</a> •
  <a href="README.es.md">🇪🇸 Español</a>
</p>

# 42pak-generator

Un manager modern și open-source pentru fișiere pak, destinat comunității de servere private Metin2. Înlocuiește formatul vechi de arhive EIX/EPK cu noul format **VPK**, oferind criptare AES-256-GCM, compresie LZ4/Zstandard/Brotli, hashing BLAKE3 și detectare a manipulării HMAC-SHA256.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

> **Capturi de ecran:** Vezi [PREVIEW.md](PREVIEW.md) pentru galeria completă a GUI-ului în temele întunecată și luminoasă.

---

## Funcționalități

- **Creare arhive VPK** — împachetarea directoarelor în fișiere `.vpk` unice cu criptare și compresie opționale
- **Gestionare arhive existente** — navigare, căutare, extragere și validare a arhivelor VPK
- **Conversie EIX/EPK în VPK** — migrare cu un singur clic din formatul EterPack vechi (suportă variantele 40250, FliegeV3 și MartySama 5.8)
- **Criptare AES-256-GCM** — criptare autentificată per fișier cu nonce unice
- **Compresie LZ4 / Zstandard / Brotli** — alege algoritmul potrivit nevoilor tale
- **Hashing conținut BLAKE3** — verificare criptografică a integrității fiecărui fișier
- **HMAC-SHA256** — detectarea manipulării la nivel de arhivă
- **Mascare nume fișiere** — ofuscare opțională a căilor din arhivă
- **CLI complet** — instrument independent `42pak-cli` cu comenzile pack, unpack, list, info, verify, diff, migrate, search, check-duplicates și watch
- **Integrare C++ Metin2** — cod loader drop-in pentru client și server, pentru arborii sursă 40250, FliegeV3 și MartySama 5.8

## Comparație VPK vs EIX/EPK

| Caracteristică | EIX/EPK (Legacy) | VPK (42pak) |
|----------------|:-:|:-:|
| Criptare | TEA / Panama / HybridCrypt | AES-256-GCM |
| Compresie | LZO | LZ4 / Zstandard / Brotli |
| Integritate | CRC32 | BLAKE3 + HMAC-SHA256 |
| Format fișier | Dual (.eix + .epk) | Fișier unic (.vpk) |
| Nr. intrări | Max. 512 | Nelimitat |
| Lungime nume fișier | 160 octeți | 512 octeți (UTF-8) |
| Derivare chei | Chei hardcodate | PBKDF2-SHA512 (200k iterații) |
| Detectare manipulare | Absent | HMAC-SHA256 pe întreaga arhivă |

---

## Start rapid

### Cerințe preliminare

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 versiunea 1809+ (pentru WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (de obicei preinstalat)

### Compilare

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### Rulare GUI

```bash
dotnet run --project src/FortyTwoPak.UI
```

### Rulare CLI

```bash
dotnet run --project src/FortyTwoPak.CLI -- <comandă> [opțiuni]
```

### Rulare teste

```bash
dotnet test
```

### Publicare (portabilă / instalator)

```powershell
# CLI executabil unic (~65 MB)
.\publish.ps1 -Target CLI

# GUI folder portabil (~163 MB)
.\publish.ps1 -Target GUI

# Ambele + instalator Inno Setup
.\publish.ps1 -Target All
```

---

## Utilizare CLI

Instrumentul CLI independent (`42pak-cli`) suportă toate operațiunile:

```
42pak-cli pack <DIR_SURSĂ> [--output <FIȘIER>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <PAROLĂ>] [--threads <N>] [--dry-run]
42pak-cli unpack <ARHIVĂ> <DIR_IEȘIRE> [--passphrase <PAROLĂ>] [--filter <PATTERN>]
42pak-cli list <ARHIVĂ> [--passphrase <PAROLĂ>] [--filter <PATTERN>] [--json]
42pak-cli info <ARHIVĂ> [--passphrase <PAROLĂ>] [--json]
42pak-cli verify <ARHIVĂ> [--passphrase <PAROLĂ>] [--filter <PATTERN>] [--json]
42pak-cli diff <ARHIVĂ_A> <ARHIVĂ_B> [--passphrase <PAROLĂ>] [--json]
42pak-cli migrate <ARHIVĂ_LEGACY> [--output <FIȘIER>] [--compression <TIP>] [--passphrase <PAROLĂ>]
42pak-cli search <DIR_LUCRU> <NUME_SAU_PATTERN>
42pak-cli check-duplicates <DIR_LUCRU> [--read-index]
42pak-cli watch <DIR_SURSĂ> [--output <FIȘIER>] [--debounce <MS>]
```

**Flaguri:** `-q` / `--quiet` suprimă toate ieșirile, cu excepția erorilor. `--json` produce JSON structurat pentru pipeline-uri scriptabile.

**Coduri de ieșire:** 0 = succes, 1 = eroare, 2 = eșec integritate, 3 = parolă incorectă.

---

## Structura proiectului

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # Bibliotecă principală: citire/scriere VPK, cripto, compresie, import legacy
│   │   ├── VpkFormat/               # Header, intrare, clase arhivă VPK
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # Compresoare LZ4 / Zstandard / Brotli
│   │   ├── Legacy/                  # Cititor și convertor EIX/EPK (40250 + FliegeV3 + MartySama 5.8)
│   │   ├── Cli/                     # Handler CLI partajat (12 comenzi)
│   │   └── Utils/                   # Mascare nume fișiere, raportare progres
│   ├── FortyTwoPak.CLI/             # Instrument CLI independent (42pak-cli)
│   ├── FortyTwoPak.UI/              # Aplicație desktop WebView2
│   │   ├── MainWindow.cs            # Gazdă WinForms cu control WebView2
│   │   ├── BridgeService.cs         # Punte JavaScript <-> C#
│   │   └── wwwroot/                 # Frontend HTML/CSS/JS (6 tab-uri, temă întunecată/luminoasă)
│   └── FortyTwoPak.Tests/           # Suită de teste xUnit (22 teste)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # Integrare C++ pentru 40250/ClientVS22 (HybridCrypt)
│   │   ├── MartySama58/             # Integrare C++ pentru MartySama 5.8 (Boost + HybridCrypt)
│   │   └── FliegeV3/                # Integrare C++ pentru FliegeV3 (XTEA/LZ4)
│   └── Server/                      # Handler VPK partajat server-side
├── docs/
│   └── FORMAT_SPEC.md               # Specificația formatului binar VPK
├── publish.ps1                      # Script publicare (CLI/GUI/Instalator/Toate)
├── installer.iss                    # Script instalator Inno Setup 6
├── assets/                          # Capturi de ecran și imagini banner
└── build/                           # Ieșire compilare
```

---

## Formatul fișierului VPK

Arhivă cu fișier unic, având următorul layout binar:

```
+-------------------------------------+
| VpkHeader (512 octeți, fix)         |  Magic "42PK", versiune, nr. intrări,
|                                     |  flag criptare, sare, autor etc.
+-------------------------------------+
| Bloc date 0 (aliniat la 4096)       |  Conținut fișier (compresat + criptat)
+-------------------------------------+
| Bloc date 1 (aliniat la 4096)       |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| Tabel intrări (dimensiune variabilă)|  Matrice de înregistrări VpkEntry. Dacă e criptat,
|                                     |  învelit în AES-GCM (nonce + tag + date).
+-------------------------------------+
| HMAC-SHA256 (32 octeți)             |  Acoperă totul de mai sus. Zero dacă nesemnat.
+-------------------------------------+
```

### Pipeline-ul de criptare

Pentru fiecare fișier: `Original -> Compresie LZ4 -> Criptare AES-256-GCM -> Stocare`

La extragere: `Citire -> Decriptare AES-256-GCM -> Decompresie LZ4 -> Verificare BLAKE3`

Derivare chei: `PBKDF2-SHA512("42PK-v1:" + parolă, sare, 100000 iterații) -> 64 octeți`
- Primii 32 octeți: cheie AES-256
- Ultimii 32 octeți: cheie HMAC-SHA256

Pentru specificația binară completă, vezi [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md).

---

## Utilizare

### Crearea unei arhive VPK (GUI)

1. Deschide 42pak-generator
2. Mergi la tab-ul **Create Pak**
3. Selectează directorul sursă cu fișierele de împachetat
4. Alege calea fișierului `.vpk` de ieșire
5. Configurează opțiunile:
   - **Criptare**: Activează și introdu o parolă
   - **Nivel compresie**: 0 (niciunul) la 12 (maxim)
   - **Mascare nume fișiere**: Ofuscare căi în arhivă
   - **Autor / Comentariu**: Metadate opționale
6. Clic pe **Build**

### Conversie EIX/EPK în VPK

1. Mergi la tab-ul **Convert**
2. Selectează fișierul `.eix` (fișierul `.epk` trebuie să fie în același director)
3. Alege calea `.vpk` de ieșire
4. Opțional, setează parola de criptare
5. Clic pe **Convert**

### Integrare client Metin2

Trei profiluri de integrare sunt furnizate pentru arbori sursă diferiți:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **MartySama 5.8** (Boost, HybridCrypt, LZO): [Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md](Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### Integrare server Metin2

Vezi [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) pentru citirea VPK pe server.

---

## Tehnologii

| Component | Tehnologie |
|-----------|------------|
| Runtime | .NET 8.0 (C#) |
| UI | WebView2 (gazdă WinForms) |
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Criptare | AES-256-GCM prin `System.Security.Cryptography` |
| Derivare chei | PBKDF2-SHA512 (200.000 iterații) |
| Hashing | BLAKE3 prin [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| Compresie | LZ4 prin [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard prin [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (încorporat) |
| Detectare manipulare | HMAC-SHA256 |
| C++ Crypto | OpenSSL 1.1+ |
| Testare | xUnit |

---

## Licență

Acest proiect este licențiat sub Licența MIT — vezi fișierul [LICENSE](LICENSE) pentru detalii.

## Contribuții

Contribuțiile sunt binevenite! Vezi [CONTRIBUTING.md](CONTRIBUTING.md) pentru ghid.

## Mulțumiri

- Comunitatea de servere private Metin2 pentru că menține jocul în viață
- Formatul original EterPack de la YMIR Entertainment pentru fundația oferită
- [Echipa BLAKE3](https://github.com/BLAKE3-team/BLAKE3) pentru funcția hash rapidă
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) pentru binding-ul .NET LZ4
