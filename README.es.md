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

Un gestor de archivos pak moderno y de código abierto para la comunidad de servidores privados de Metin2. Reemplaza el formato legacy de archivos EIX/EPK con el nuevo formato **VPK**, con cifrado AES-256-GCM, compresión LZ4/Zstandard/Brotli, hashing BLAKE3 y detección de manipulación HMAC-SHA256.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

> **Capturas de pantalla:** Ver [PREVIEW.md](PREVIEW.md) para la galería completa de la GUI en temas oscuro y claro.

---

## Características

- **Crear archivos VPK** — Empaquetar directorios en archivos `.vpk` únicos con cifrado y compresión opcionales
- **Gestionar archivos existentes** — Explorar, buscar, extraer y validar archivos VPK
- **Convertir EIX/EPK a VPK** — Migración con un clic desde el formato legacy EterPack (soporta variantes 40250 y FliegeV3)
- **Cifrado AES-256-GCM** — Cifrado autenticado por archivo con nonces únicos
- **Compresión LZ4 / Zstandard / Brotli** — Elige el algoritmo que se adapte a tus necesidades
- **Hashing de contenido BLAKE3** — Verificación criptográfica de integridad para cada archivo
- **HMAC-SHA256** — Detección de manipulación a nivel de archivo
- **Ofuscación de nombres de archivo** — Ocultación opcional de rutas dentro de los archivos
- **CLI completa** — Herramienta independiente `42pak-cli` con comandos pack, unpack, list, info, verify, diff, migrate, search, check-duplicates y watch
- **Integración C++ con Metin2** — Código loader drop-in para cliente y servidor para los árboles de código fuente 40250 y FliegeV3

## Comparación VPK vs EIX/EPK

| Característica | EIX/EPK (Legacy) | VPK (42pak) |
|----------------|:-:|:-:|
| Cifrado | TEA / Panama / HybridCrypt | AES-256-GCM |
| Compresión | LZO | LZ4 / Zstandard / Brotli |
| Integridad | CRC32 | BLAKE3 + HMAC-SHA256 |
| Formato de archivo | Dos archivos (.eix + .epk) | Archivo único (.vpk) |
| Cantidad de entradas | Máx. 512 | Ilimitado |
| Longitud nombre archivo | 160 bytes | 512 bytes (UTF-8) |
| Derivación de claves | Claves hardcodeadas | PBKDF2-SHA512 (200k iteraciones) |
| Detección de manipulación | Ninguna | HMAC-SHA256 archivo completo |

---

## Inicio Rápido

### Requisitos previos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 versión 1809+ (para WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (generalmente preinstalado)

### Compilar

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### Ejecutar la GUI

```bash
dotnet run --project src/FortyTwoPak.UI
```

### Ejecutar la CLI

```bash
dotnet run --project src/FortyTwoPak.CLI -- <comando> [opciones]
```

### Ejecutar pruebas

```bash
dotnet test
```

### Publicar (Portable / Instalador)

```powershell
# CLI ejecutable único (~65 MB)
.\publish.ps1 -Target CLI

# GUI carpeta portable (~163 MB)
.\publish.ps1 -Target GUI

# Ambos + instalador Inno Setup
.\publish.ps1 -Target All
```

---

## Uso de la CLI

La CLI independiente (`42pak-cli`) soporta todas las operaciones:

```
42pak-cli pack <DIR_ORIGEN> [--output <ARCHIVO>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <CONTRASEÑA>] [--threads <N>] [--dry-run]
42pak-cli unpack <ARCHIVO> <DIR_SALIDA> [--passphrase <CONTRASEÑA>] [--filter <PATRÓN>]
42pak-cli list <ARCHIVO> [--passphrase <CONTRASEÑA>] [--filter <PATRÓN>] [--json]
42pak-cli info <ARCHIVO> [--passphrase <CONTRASEÑA>] [--json]
42pak-cli verify <ARCHIVO> [--passphrase <CONTRASEÑA>] [--filter <PATRÓN>] [--json]
42pak-cli diff <ARCHIVO_A> <ARCHIVO_B> [--passphrase <CONTRASEÑA>] [--json]
42pak-cli migrate <ARCHIVO_LEGACY> [--output <ARCHIVO>] [--compression <TIPO>] [--passphrase <CONTRASEÑA>]
42pak-cli search <DIR_TRABAJO> <NOMBRE_O_PATRÓN>
42pak-cli check-duplicates <DIR_TRABAJO> [--read-index]
42pak-cli watch <DIR_ORIGEN> [--output <ARCHIVO>] [--debounce <MS>]
```

**Flags:** `-q` / `--quiet` suprime toda la salida excepto errores. `--json` produce JSON estructurado para pipelines scriptables.

**Códigos de salida:** 0 = éxito, 1 = error, 2 = fallo de integridad, 3 = contraseña incorrecta.

---

## Estructura del Proyecto

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # Biblioteca principal: lectura/escritura VPK, cripto, compresión, importación legacy
│   │   ├── VpkFormat/               # Cabecera, entrada, clases de archivo VPK
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # Compresores LZ4 / Zstandard / Brotli
│   │   ├── Legacy/                  # Lector y conversor EIX/EPK (40250 + FliegeV3)
│   │   ├── Cli/                     # Handler CLI compartido (12 comandos)
│   │   └── Utils/                   # Ofuscación de nombres, informe de progreso
│   ├── FortyTwoPak.CLI/             # Herramienta CLI independiente (42pak-cli)
│   ├── FortyTwoPak.UI/              # Aplicación de escritorio WebView2
│   │   ├── MainWindow.cs            # Host WinForms con control WebView2
│   │   ├── BridgeService.cs         # Puente JavaScript <-> C#
│   │   └── wwwroot/                 # Frontend HTML/CSS/JS (6 pestañas, tema oscuro/claro)
│   └── FortyTwoPak.Tests/           # Suite de pruebas xUnit (22 pruebas)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # Integración C++ para 40250/ClientVS22 (HybridCrypt)
│   │   └── FliegeV3/                # Integración C++ para FliegeV3 (XTEA/LZ4)
│   └── Server/                      # Handler VPK compartido del lado servidor
├── docs/
│   └── FORMAT_SPEC.md               # Especificación del formato binario VPK
├── publish.ps1                      # Script de publicación (CLI/GUI/Instalador/Todos)
├── installer.iss                    # Script del instalador Inno Setup 6
├── assets/                          # Capturas de pantalla e imágenes de banner
└── build/                           # Salida de compilación
```

---

## Formato de Archivo VPK

Archivo único con el siguiente diseño binario:

```
+-------------------------------------+
| VpkHeader (512 bytes, fijo)         |  Magic "42PK", versión, cantidad de entradas,
|                                     |  flag de cifrado, salt, autor, etc.
+-------------------------------------+
| Bloque de datos 0 (alineado a 4096) |  Contenido del archivo (comprimido + cifrado)
+-------------------------------------+
| Bloque de datos 1 (alineado a 4096) |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| Tabla de entradas (tamaño variable) |  Array de registros VpkEntry. Si está cifrado,
|                                     |  envuelto en AES-GCM (nonce + tag + datos).
+-------------------------------------+
| HMAC-SHA256 (32 bytes)              |  Cubre todo lo anterior. Cero si no está firmado.
+-------------------------------------+
```

### Pipeline de Cifrado

Para cada archivo: `Original -> Compresión LZ4 -> Cifrado AES-256-GCM -> Almacenamiento`

Al extraer: `Lectura -> Descifrado AES-256-GCM -> Descompresión LZ4 -> Verificación BLAKE3`

Derivación de clave: `PBKDF2-SHA512("42PK-v1:" + contraseña, salt, 100000 iteraciones) -> 64 bytes`
- Primeros 32 bytes: clave AES-256
- Últimos 32 bytes: clave HMAC-SHA256

Para la especificación binaria completa, ver [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md).

---

## Uso

### Crear un Archivo VPK (GUI)

1. Abre 42pak-generator
2. Ve a la pestaña **Create Pak**
3. Selecciona el directorio de origen con los archivos a empaquetar
4. Elige la ruta del archivo `.vpk` de salida
5. Configura las opciones:
   - **Cifrado**: Actívalo e introduce una contraseña
   - **Nivel de Compresión**: 0 (ninguno) a 12 (máximo)
   - **Ofuscación de Nombres**: Ocultar rutas dentro del archivo
   - **Autor / Comentario**: Metadatos opcionales
6. Haz clic en **Build**

### Convertir EIX/EPK a VPK

1. Ve a la pestaña **Convert**
2. Selecciona el archivo `.eix` (el `.epk` debe estar en el mismo directorio)
3. Elige la ruta `.vpk` de salida
4. Opcionalmente establece la contraseña de cifrado
5. Haz clic en **Convert**

### Integración con Cliente Metin2

Se proporcionan dos perfiles de integración para diferentes árboles de código fuente:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### Integración con Servidor Metin2

Ver [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) para lectura VPK del lado servidor.

---

## Tecnologías

| Componente | Tecnología |
|-----------|------------|
| Runtime | .NET 8.0 (C#) |
| UI | WebView2 (host WinForms) |
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Cifrado | AES-256-GCM vía `System.Security.Cryptography` |
| Derivación de claves | PBKDF2-SHA512 (200.000 iteraciones) |
| Hashing | BLAKE3 vía [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| Compresión | LZ4 vía [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard vía [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (integrado) |
| Detección de manipulación | HMAC-SHA256 |
| C++ Crypto | OpenSSL 1.1+ |
| Pruebas | xUnit |

---

## Licencia

Este proyecto está licenciado bajo la Licencia MIT — ver el archivo [LICENSE](LICENSE) para más detalles.

## Contribuir

¡Las contribuciones son bienvenidas! Ver [CONTRIBUTING.md](CONTRIBUTING.md) para las directrices.

## Agradecimientos

- La comunidad de servidores privados de Metin2 por mantener el juego vivo
- El formato original EterPack de YMIR Entertainment por proporcionar la base
- [Equipo BLAKE3](https://github.com/BLAKE3-team/BLAKE3) por la función hash rápida
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) por el binding .NET LZ4
