<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Guía de integración del cliente (MartySama 5.8)

> **Perfil:** MartySama 5.8 - dirigido a la arquitectura multi-cifrado HybridCrypt basada en Boost.
> Para la integración de 40250/ClientVS22, consulte `../40250/INTEGRATION_GUIDE.md`.
> Para la integración de FliegeV3, consulte `../FliegeV3/INTEGRATION_GUIDE.md`.

Reemplazo directo para el sistema EterPack (EIX/EPK). Basado en el código
fuente real del cliente MartySama 5.8 (`Binary & S-Source/Binary/Client/EterPack/`).
Todas las rutas de archivos hacen referencia al árbol de código fuente real de MartySama.

## Qué se obtiene

| Característica | EterPack (antiguo) | VPK (nuevo) |
|---------------|-------------------|-------------|
| Cifrado | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (acelerado por hardware) |
| Compresión | LZO | LZ4 / Zstandard / Brotli |
| Integridad | CRC32 por archivo | BLAKE3 por archivo + HMAC-SHA256 archivo |
| Formato de archivo | par .eix + .epk | Un solo archivo .vpk |
| Gestión de claves | Panama IV enviado por servidor + HybridCrypt SDB | Frase de contraseña PBKDF2-SHA512 |

## MartySama 5.8 vs 40250 vs FliegeV3

| Aspecto | 40250 | MartySama 5.8 | FliegeV3 |
|---------|-------|---------------|----------|
| Sistema criptográfico | Camellia/Twofish/XTEA (HybridCrypt) | Igual que 40250 | Solo XTEA |
| Entrega de claves | Panama IV enviado por servidor + SDB | Igual que 40250 | Claves estáticas compiladas |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| Tipos de contenedor | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| Estilo de encabezado | Include guards | `#pragma once` | Include guards |
| Anti-manipulación | Ninguno | `#ifdef __THEMIDA__` VM_START/VM_END | Ninguno |
| Métodos del manager | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | Igual que 40250 | Ninguno |
| Compresión | LZO | LZO | LZ4 |
| Estructura de índice | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` |

El código de integración VPK se adapta a los contenedores Boost de MartySama y a los
marcadores Themida manteniendo la misma interfaz API (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`).

## Arquitectura

VPK se integra a través de `CEterFileDict` - la misma búsqueda hash
`boost::unordered_multimap` que utiliza el EterPack original de MartySama.
Cuando `CEterPackManager::RegisterPackAuto()` encuentra un archivo `.vpk`,
crea un `CVpkPack` en lugar de `CEterPack`. `CVpkPack` registra sus entradas
en el `m_FileDict` compartido con un marcador sentinel. Cuando `GetFromPack()`
resuelve un nombre de archivo, verifica el marcador y redirige de forma
transparente a `CEterPack::Get2()` o `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Archivos

### Nuevos archivos a agregar a `Client/EterPack/`

| Archivo | Propósito |
|---------|----------|
| `VpkLoader.h` | Clase `CVpkPack` - reemplazo directo para `CEterPack` |
| `VpkLoader.cpp` | Implementación completa: análisis de encabezado, tabla de entradas, descifrado+descompresión, verificación BLAKE3 |
| `VpkCrypto.h` | Utilidades criptográficas: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementaciones usando OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Encabezado `CEterPackManager` modificado con VPK + HybridCrypt + Boost |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificado con despacho VPK, HybridCrypt, iteradores Boost, Themida |

### Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `Client/EterPack/EterPackManager.h` | Reemplazar con `EterPackManager_Vpk.h` (o fusionar) |
| `Client/EterPack/EterPackManager.cpp` | Reemplazar con `EterPackManager_Vpk.cpp` (o fusionar) |
| `Client/UserInterface/UserInterface.cpp` | Cambio de 2 líneas (ver abajo) |

## Bibliotecas requeridas

### OpenSSL 1.1+
- Windows: Descargar desde https://slproweb.com/products/Win32OpenSSL.html
- Encabezados: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Enlace: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Encabezado: `<lz4.h>`
- Enlace: `lz4.lib`
- **Nota:** MartySama 5.8 NO incluye LZ4 por defecto. Esta es una nueva dependencia.

### Zstandard
- https://github.com/facebook/zstd/releases
- Encabezado: `<zstd.h>`
- Enlace: `zstd.lib` (o `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Encabezados: `<brotli/decode.h>`
- Enlace: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copiar en su proyecto: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: agregar también `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

### Dependencias existentes (ya incluidas en MartySama 5.8)

MartySama 5.8 ya incluye estas bibliotecas que **no** son necesarias para VPK
pero permanecen en su proyecto para la ruta de código EPK heredada:

| Biblioteca | Utilizada por |
|-----------|-------------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), cifrado Panama, funciones hash |
| Boost | `boost::unordered_map`/`boost::unordered_multimap` en EterPack, EterPackManager, CEterFileDict |
| LZO | Descompresión EPK (tipos de pack 1, 2) |

Estas siguen funcionando junto con VPK. No se necesitan cambios.

## Integración paso a paso

### Paso 1: Agregar archivos al proyecto EterPack en VS

1. Copiar `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` en `Client/EterPack/`
2. Copiar los archivos C de BLAKE3 en `Client/EterPack/` (o una ubicación compartida)
3. Agregar todos los archivos nuevos al proyecto EterPack en Visual Studio
4. Agregar rutas de inclusión para OpenSSL, LZ4, Zstd, Brotli en **Additional Include Directories**
5. Agregar rutas de bibliotecas en **Additional Library Directories**
6. Agregar `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` en **Additional Dependencies**

### Paso 2: Reemplazar EterPackManager

**Opción A (reemplazo limpio):**
- Reemplazar `Client/EterPack/EterPackManager.h` con `EterPackManager_Vpk.h`
- Reemplazar `Client/EterPack/EterPackManager.cpp` con `EterPackManager_Vpk.cpp`
- Renombrar ambos a `EterPackManager.h` / `EterPackManager.cpp`

**Opción B (fusionar):**
Agregar esto al `EterPackManager.h` existente:

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

**Importante:** Observe el uso de `boost::unordered_map` en lugar de `std::unordered_map`
para coincidir con la convención de MartySama. El `EterPackManager_Vpk.h` proporcionado
ya utiliza contenedores Boost en todo el código.

Luego fusionar las implementaciones de `EterPackManager_Vpk.cpp` en su archivo `.cpp` existente.

### Paso 3: Modificar UserInterface.cpp

En `Client/UserInterface/UserInterface.cpp`, el bucle de registro de packs
(alrededor de la línea 557–579):

**Antes:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**Después:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

Y agregar esta línea **antes** del bucle de registro:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Eso es todo. Dos cambios en total:
1. `SetVpkPassphrase()` antes del bucle
2. `RegisterPack()` → `RegisterPackAuto()` (2 ocurrencias)

Consulte `UserInterface_VpkPatch.cpp` para el parche completo anotado con antes/después.

### Paso 4: Compilar

Compilar primero el proyecto EterPack, luego la solución completa. El código VPK se compila
junto con el código EterPack existente - no se elimina nada.

**Notas de compilación específicas de MartySama 5.8:**
- MartySama usa Boost en todo el proyecto. El `boost::unordered_multimap` en
  `CEterFileDict` es totalmente compatible con VPK - `InsertItem()` funciona de forma idéntica.
- Asegúrese de que `StdAfx.h` en EterPack incluya `<boost/unordered_map.hpp>` (debería
  hacerlo si el proyecto MartySama compila).
- CryptoPP ya está en la solución para HybridCrypt - no hay conflicto con OpenSSL.
  Sirven para propósitos completamente diferentes (CryptoPP para EPK heredado, OpenSSL para VPK).
- Si usa Themida, los marcadores `#ifdef __THEMIDA__` en `EterPackManager_Vpk.cpp`
  ya están configurados. Defina `__THEMIDA__` en su configuración de compilación si desea
  que los marcadores VM_START/VM_END estén activos.

## Cómo funciona

### Flujo de registro

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

### Flujo de acceso a archivos

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

### Coexistencia con HybridCrypt

MartySama 5.8 recibe las claves de cifrado del servidor al iniciar sesión:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

Estos métodos iteran `m_PackMap` (que contiene solo objetos `CEterPack*`).
Los packs VPK se almacenan en `m_VpkPackMap` de forma separada, por lo que la entrega
de claves HybridCrypt es completamente transparente - ambos sistemas coexisten
sin cambios en el código de red.

### Gestión de memoria

CVpkPack utiliza `CMappedFile::AppendDataBlock()` - el mismo mecanismo que
CEterPack usa para los datos HybridCrypt. Los datos descomprimidos se copian en
un búfer propiedad de CMappedFile que se libera automáticamente cuando el
CMappedFile sale del ámbito. No se necesita limpieza manual.

## Conversión de packs

Use la herramienta 42pak-generator para convertir sus archivos pack MartySama existentes:

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

O use la vista Crear de la interfaz gráfica para construir archivos VPK desde carpetas.

## Estrategia de migración

1. **Configurar bibliotecas** - agregar OpenSSL, LZ4, Zstd, Brotli, BLAKE3 a su proyecto
2. **Agregar archivos VPK** - copiar los 6 nuevos archivos fuente en `Client/EterPack/`
3. **Modificar EterPackManager** - fusionar o reemplazar el encabezado e implementación
4. **Modificar UserInterface.cpp** - el cambio de 2 líneas
5. **Compilar** - verificar que todo compile
6. **Convertir un pack** - ej. `metin2_patch_etc` → probar que cargue correctamente
7. **Convertir los packs restantes** - uno por uno o todos a la vez
8. **Eliminar archivos EPK antiguos** - una vez convertidos todos los packs

Como `RegisterPackAuto` retrocede a EPK cuando no existe un VPK, puede
convertir packs de forma incremental sin romper nada.

## Configuración de frase de contraseña

| Método | Cuándo usar |
|--------|-------------|
| Codificada en el código fuente | Servidores privados, enfoque más simple |
| Archivo de configuración (`metin2.cfg`) | Fácil de cambiar sin recompilar |
| Enviada por servidor al iniciar sesión | Máxima seguridad - la frase cambia por sesión |

Para frase de contraseña enviada por servidor, modifique `CAccountConnector` para recibirla
en la respuesta de autenticación y llame a `CEterPackManager::Instance().SetVpkPassphrase()`
antes de cualquier acceso a packs. El `AccountConnector.cpp` de MartySama ya maneja
paquetes personalizados del servidor - agregue un nuevo handler junto a la llamada
existente `RetrieveHybridCryptPackKeys`.

## Solución de problemas

| Síntoma | Causa | Solución |
|---------|-------|----------|
| Archivos pack no encontrados | Falta la extensión `.vpk` | Asegúrese de que el nombre del pack no incluya extensión - `RegisterPackAuto` agrega `.vpk` |
| "HMAC verification failed" | Frase de contraseña incorrecta | Verifique que `SetVpkPassphrase` se llame antes de `RegisterPackAuto` |
| Archivos no encontrados en VPK | Diferencia de mayúsculas/minúsculas en la ruta | VPK normaliza a minúsculas con separadores `/` |
| Crash en `Get2()` | Colisión del sentinel `compressed_type` | Asegúrese de que ningún archivo EPK use `compressed_type == -1` (ninguno lo hace en Metin2 estándar) |
| Error de enlace LZ4/Zstd/Brotli | Biblioteca faltante | Agregue la biblioteca de descompresión a Additional Dependencies |
| Error de compilación BLAKE3 | Archivos fuente faltantes | Asegúrese de que todos los archivos `blake3_*.c` estén en el proyecto |
| Errores del enlazador Boost | Faltan includes de Boost | Verifique las rutas de includes de Boost en las propiedades del proyecto EterPack |
| Conflicto CryptoPP + OpenSSL | Nombres de símbolos duplicados | No debería ocurrir - definen símbolos separados. Si ocurre, verifique contaminación por `using namespace` |
| Errores de Themida | `__THEMIDA__` no definido | Defínalo en su configuración de compilación, o elimine los bloques `#ifdef __THEMIDA__` de `EterPackManager_Vpk.cpp` |
