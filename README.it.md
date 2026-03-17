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

Un gestore di file pak moderno e open-source per la community dei server privati di Metin2. Sostituisce il vecchio formato di archivio EIX/EPK con il nuovo formato **VPK**, dotato di crittografia AES-256-GCM, compressione LZ4/Zstandard/Brotli, hashing BLAKE3 e rilevamento manomissioni HMAC-SHA256.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

> **Screenshot:** Vedi [PREVIEW.md](PREVIEW.md) per la galleria completa della GUI nei temi scuro e chiaro.

---

## Funzionalità

- **Creazione archivi VPK** — Impacchettare directory in singoli file `.vpk` con crittografia e compressione opzionali
- **Gestione archivi esistenti** — Sfogliare, cercare, estrarre e validare archivi VPK
- **Conversione EIX/EPK in VPK** — Migrazione con un clic dal formato legacy EterPack (supporta le varianti 40250 e FliegeV3)
- **Crittografia AES-256-GCM** — Crittografia autenticata per file con nonce univoci
- **Compressione LZ4 / Zstandard / Brotli** — Scegli l'algoritmo più adatto alle tue esigenze
- **Hashing contenuti BLAKE3** — Verifica crittografica dell'integrità per ogni file
- **HMAC-SHA256** — Rilevamento manomissioni a livello di archivio
- **Offuscamento nomi file** — Occultamento opzionale dei percorsi all'interno degli archivi
- **CLI completa** — Strumento autonomo `42pak-cli` con comandi pack, unpack, list, info, verify, diff, migrate, search, check-duplicates e watch
- **Integrazione C++ Metin2** — Codice loader drop-in per client e server per entrambi gli alberi sorgente 40250 e FliegeV3

## Confronto VPK vs EIX/EPK

| Caratteristica | EIX/EPK (Legacy) | VPK (42pak) |
|----------------|:-:|:-:|
| Crittografia | TEA / Panama / HybridCrypt | AES-256-GCM |
| Compressione | LZO | LZ4 / Zstandard / Brotli |
| Integrità | CRC32 | BLAKE3 + HMAC-SHA256 |
| Formato file | Due file (.eix + .epk) | File singolo (.vpk) |
| Numero voci | Max. 512 | Illimitato |
| Lunghezza nome file | 160 byte | 512 byte (UTF-8) |
| Derivazione chiavi | Chiavi hardcoded | PBKDF2-SHA512 (200k iterazioni) |
| Rilevamento manomissioni | Assente | HMAC-SHA256 intero archivio |

---

## Avvio Rapido

### Prerequisiti

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 versione 1809+ (per WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (solitamente preinstallato)

### Compilazione

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### Avviare la GUI

```bash
dotnet run --project src/FortyTwoPak.UI
```

### Avviare la CLI

```bash
dotnet run --project src/FortyTwoPak.CLI -- <comando> [opzioni]
```

### Eseguire i test

```bash
dotnet test
```

### Pubblicazione (Portatile / Installer)

```powershell
# CLI eseguibile singolo (~65 MB)
.\publish.ps1 -Target CLI

# GUI cartella portatile (~163 MB)
.\publish.ps1 -Target GUI

# Entrambi + installer Inno Setup
.\publish.ps1 -Target All
```

---

## Utilizzo CLI

Lo strumento CLI autonomo (`42pak-cli`) supporta tutte le operazioni:

```
42pak-cli pack <DIR_SORGENTE> [--output <FILE>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <PASSWORD>] [--threads <N>] [--dry-run]
42pak-cli unpack <ARCHIVIO> <DIR_OUTPUT> [--passphrase <PASSWORD>] [--filter <PATTERN>]
42pak-cli list <ARCHIVIO> [--passphrase <PASSWORD>] [--filter <PATTERN>] [--json]
42pak-cli info <ARCHIVIO> [--passphrase <PASSWORD>] [--json]
42pak-cli verify <ARCHIVIO> [--passphrase <PASSWORD>] [--filter <PATTERN>] [--json]
42pak-cli diff <ARCHIVIO_A> <ARCHIVIO_B> [--passphrase <PASSWORD>] [--json]
42pak-cli migrate <ARCHIVIO_LEGACY> [--output <FILE>] [--compression <TIPO>] [--passphrase <PASSWORD>]
42pak-cli search <DIR_LAVORO> <NOME_O_PATTERN>
42pak-cli check-duplicates <DIR_LAVORO> [--read-index]
42pak-cli watch <DIR_SORGENTE> [--output <FILE>] [--debounce <MS>]
```

**Flag:** `-q` / `--quiet` sopprime tutto l'output tranne gli errori. `--json` produce JSON strutturato per pipeline scriptabili.

**Codici di uscita:** 0 = successo, 1 = errore, 2 = fallimento integrità, 3 = password errata.

---

## Struttura del Progetto

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # Libreria principale: lettura/scrittura VPK, critto, compressione, importazione legacy
│   │   ├── VpkFormat/               # Header, voce, classi archivio VPK
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # Compressori LZ4 / Zstandard / Brotli
│   │   ├── Legacy/                  # Lettore e convertitore EIX/EPK (40250 + FliegeV3)
│   │   ├── Cli/                     # Handler CLI condiviso (12 comandi)
│   │   └── Utils/                   # Offuscamento nomi file, report progressi
│   ├── FortyTwoPak.CLI/             # Strumento CLI autonomo (42pak-cli)
│   ├── FortyTwoPak.UI/              # Applicazione desktop WebView2
│   │   ├── MainWindow.cs            # Host WinForms con controllo WebView2
│   │   ├── BridgeService.cs         # Ponte JavaScript <-> C#
│   │   └── wwwroot/                 # Frontend HTML/CSS/JS (6 schede, tema scuro/chiaro)
│   └── FortyTwoPak.Tests/           # Suite test xUnit (22 test)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # Integrazione C++ per 40250/ClientVS22 (HybridCrypt)
│   │   └── FliegeV3/                # Integrazione C++ per FliegeV3 (XTEA/LZ4)
│   └── Server/                      # Handler VPK condiviso lato server
├── docs/
│   └── FORMAT_SPEC.md               # Specifica formato binario VPK
├── publish.ps1                      # Script di pubblicazione (CLI/GUI/Installer/Tutti)
├── installer.iss                    # Script installer Inno Setup 6
├── assets/                          # Screenshot e immagini banner
└── build/                           # Output compilazione
```

---

## Formato File VPK

Archivio a file singolo con il seguente layout binario:

```
+-------------------------------------+
| VpkHeader (512 byte, fisso)         |  Magic "42PK", versione, conteggio voci,
|                                     |  flag crittografia, salt, autore, ecc.
+-------------------------------------+
| Blocco dati 0 (allineato a 4096)    |  Contenuto file (compresso + crittografato)
+-------------------------------------+
| Blocco dati 1 (allineato a 4096)    |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| Tabella voci (dimensione variabile) |  Array di record VpkEntry. Se crittografato,
|                                     |  avvolto in AES-GCM (nonce + tag + dati).
+-------------------------------------+
| HMAC-SHA256 (32 byte)               |  Copre tutto quanto sopra. Zero se non firmato.
+-------------------------------------+
```

### Pipeline di Crittografia

Per ogni file: `Originale -> Compressione LZ4 -> Crittografia AES-256-GCM -> Archiviazione`

All'estrazione: `Lettura -> Decrittografia AES-256-GCM -> Decompressione LZ4 -> Verifica BLAKE3`

Derivazione chiave: `PBKDF2-SHA512("42PK-v1:" + password, salt, 100000 iterazioni) -> 64 byte`
- Primi 32 byte: chiave AES-256
- Ultimi 32 byte: chiave HMAC-SHA256

Per la specifica binaria completa, vedi [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md).

---

## Utilizzo

### Creazione di un Archivio VPK (GUI)

1. Apri 42pak-generator
2. Vai alla scheda **Create Pak**
3. Seleziona la directory sorgente contenente i file da impacchettare
4. Scegli il percorso del file `.vpk` di output
5. Configura le opzioni:
   - **Crittografia**: Attiva e inserisci una password
   - **Livello di Compressione**: 0 (nessuno) a 12 (massimo)
   - **Offuscamento Nomi File**: Occultare i percorsi nell'archivio
   - **Autore / Commento**: Metadati opzionali
6. Clicca su **Build**

### Conversione EIX/EPK in VPK

1. Vai alla scheda **Convert**
2. Seleziona il file `.eix` (il `.epk` deve essere nella stessa directory)
3. Scegli il percorso `.vpk` di output
4. Opzionalmente imposta la password di crittografia
5. Clicca su **Convert**

### Integrazione Client Metin2

Due profili di integrazione sono forniti per diversi alberi sorgente:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### Integrazione Server Metin2

Vedi [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) per la lettura VPK lato server.

---

## Tecnologie

| Componente | Tecnologia |
|-----------|------------|
| Runtime | .NET 8.0 (C#) |
| UI | WebView2 (host WinForms) |
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Crittografia | AES-256-GCM tramite `System.Security.Cryptography` |
| Derivazione chiavi | PBKDF2-SHA512 (200.000 iterazioni) |
| Hashing | BLAKE3 tramite [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| Compressione | LZ4 tramite [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard tramite [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (integrato) |
| Rilevamento manomissioni | HMAC-SHA256 |
| C++ Crypto | OpenSSL 1.1+ |
| Testing | xUnit |

---

## Licenza

Questo progetto è distribuito sotto la Licenza MIT — vedi il file [LICENSE](LICENSE) per i dettagli.

## Contribuire

I contributi sono benvenuti! Vedi [CONTRIBUTING.md](CONTRIBUTING.md) per le linee guida.

## Ringraziamenti

- La community dei server privati di Metin2 per mantenere vivo il gioco
- Il formato originale EterPack di YMIR Entertainment per aver fornito le fondamenta
- [Team BLAKE3](https://github.com/BLAKE3-team/BLAKE3) per la funzione hash veloce
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) per il binding .NET LZ4
