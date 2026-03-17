<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Guía de Integración del Cliente FliegeV3

Reemplazo directo del sistema EterPack (EIX/EPK). Basado en el código fuente real del cliente
FliegeV3 binary-src. Todas las rutas se refieren al árbol de código fuente FliegeV3 real.

## Lo Que Obtienes

| Característica | EterPack (FliegeV3) | VPK (Nuevo) |
|---------------|---------------------|-------------|
| Encriptación | XTEA (clave 128-bit, 32 rondas) | AES-256-GCM (acelerado por hardware) |
| Compresión | LZ4 (migrado desde LZO) | LZ4 / Zstandard / Brotli |
| Integridad | CRC32 por archivo | BLAKE3 por archivo + HMAC-SHA256 del archivo |
| Formato de Archivo | Par .eix + .epk | Archivo .vpk único |
| Gestión de Claves | Claves XTEA estáticas codificadas en el código | Passphrase PBKDF2-SHA512 |
| Entradas de Índice | `TEterPackIndex` de 192 bytes | Entradas VPK de longitud variable |

## FliegeV3 vs 40250 – ¿Por Qué Perfiles Separados?

FliegeV3 difiere significativamente de la configuración 40250:

| Aspecto | 40250 | FliegeV3 |
|---------|-------|----------|
| Sistema de Encriptación | Híbrido Camellia/Twofish/XTEA (HybridCrypt) | Solo XTEA |
| Entrega de Claves | Panama IV del servidor + bloques SDB | Claves estáticas compiladas |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` |
| Métodos del Manager | `DecryptPackIV`, `WriteHybridCryptPackInfo`, `RetrieveHybridCryptPackKeys/SDB` | Ninguno de estos métodos |
| Búsqueda de Archivos | Siempre permite archivos locales | Condicional `ENABLE_LOCAL_FILE_LOADING` |
| Estructura del Índice | 169 bytes (#pragma pack(push,4) nombre 161 chars) | 192 bytes (misma struct, alineamiento/padding diferente) |

El código de integración VPK se adapta a estas diferencias manteniendo la misma API
(`RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase`).

## Arquitectura

VPK se integra a través de `CEterFileDict` – el mismo `boost::unordered_multimap`
que usa el EterPack original de FliegeV3. Cuando `RegisterPackAuto()` encuentra un archivo
`.vpk`, crea un `CVpkPack` en lugar de `CEterPack`. `CVpkPack` registra sus entradas
en el `m_FileDict` compartido con un marcador sentinel (`compressed_type == -1`).

Cuando `GetFromPack()` resuelve un nombre de archivo, verifica el marcador y despacha
de forma transparente a `CEterPack::Get2()` o `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Archivos

### Archivos Nuevos para Agregar a `source/EterPack/`

| Archivo | Propósito |
|---------|-----------|
| `VpkLoader.h` | Clase `CVpkPack` – reemplazo directo de `CEterPack` |
| `VpkLoader.cpp` | Implementación completa: parsing de header, tabla de entradas, desencriptación+descompresión, verificación BLAKE3 |
| `VpkCrypto.h` | Utilidades de encriptación: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementación con OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Header `CEterPackManager` modificado con soporte VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificado – sin HybridCrypt, lógica de búsqueda FliegeV3 |

### Archivos para Modificar

| Archivo | Cambio |
|---------|--------|
| `source/EterPack/EterPackManager.h` | Reemplazar con `EterPackManager_Vpk.h` (o fusionar) |
| `source/EterPack/EterPackManager.cpp` | Reemplazar con `EterPackManager_Vpk.cpp` (o fusionar) |
| `source/UserInterface/UserInterface.cpp` | Cambiar 2 líneas (ver abajo) |

## Bibliotecas Necesarias

### OpenSSL 1.1+
- Windows: Descargar de https://slproweb.com/products/Win32OpenSSL.html
- Headers: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Enlazar: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Headers: `<lz4.h>`
- Enlazar: `lz4.lib`
- **Nota:** FliegeV3 ya usa LZ4 para la compresión de packs. Si tu proyecto
  ya enlaza `lz4.lib`, no se necesita configuración adicional.

### Zstandard
- https://github.com/facebook/zstd/releases
- Headers: `<zstd.h>`
- Enlazar: `zstd.lib` (o `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Headers: `<brotli/decode.h>`
- Enlazar: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copiar al proyecto: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: Agregar también `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

## Integración Paso a Paso

### Paso 1: Agregar Archivos al Proyecto EterPack en VS

1. Copiar `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` a `source/EterPack/`
2. Copiar archivos C de BLAKE3 a `source/EterPack/` (o ubicación compartida)
3. Agregar todos los archivos nuevos al proyecto EterPack en Visual Studio
4. Agregar rutas de include para OpenSSL, Zstd, Brotli en **Additional Include Directories**
   - LZ4 ya debería estar configurado si tu build FliegeV3 lo usa
5. Agregar rutas de biblioteca en **Additional Library Directories**
6. Agregar `libssl.lib`, `libcrypto.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` en **Additional Dependencies**
   - `lz4.lib` ya debería estar enlazado

### Paso 2: Reemplazar EterPackManager

**Opción A (Reemplazo limpio):**
- Reemplazar `source/EterPack/EterPackManager.h` con `EterPackManager_Vpk.h`
- Reemplazar `source/EterPack/EterPackManager.cpp` con `EterPackManager_Vpk.cpp`
- Renombrar ambos a `EterPackManager.h` / `EterPackManager.cpp`

**Opción B (Fusión):**
Agregar esto al `EterPackManager.h` existente:

```cpp
#include "VpkLoader.h"

// Dentro de la declaración de clase, agregar en public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Dentro de la declaración de clase, agregar en protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Luego fusionar la implementación de `EterPackManager_Vpk.cpp` en tu archivo `.cpp` existente.

**Importante:** No agregues `DecryptPackIV`, `WriteHybridCryptPackInfo`,
`RetrieveHybridCryptPackKeys` o `RetrieveHybridCryptPackSDB` – FliegeV3
no tiene estos métodos.

### Paso 3: Modificar UserInterface.cpp

En `source/UserInterface/UserInterface.cpp`, en el loop de registro de packs:

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

Y agregar esta línea **antes** del loop de registro:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

Eso es todo. Dos cambios en total:
1. `SetVpkPassphrase()` antes del loop
2. `RegisterPack()` → `RegisterPackAuto()` (2 ubicaciones)

**Nota:** A diferencia de la integración 40250, FliegeV3 no requiere parches de código de red.
No hay llamadas `RetrieveHybridCryptPackKeys` o `RetrieveHybridCryptPackSDB`
de las que preocuparse.

### Paso 4: Compilar

Compila primero el proyecto EterPack, luego compila toda la solución. El código VPK compila
junto con el código EterPack existente – nada se elimina.

**Notas de compilación específicas de FliegeV3:**
- FliegeV3 usa Boost para `boost::unordered_multimap` en `CEterFileDict`. Esto es compatible
  con VPK – `InsertItem()` funciona de la misma manera.
- Asegúrate de que `StdAfx.h` en EterPack incluya `<boost/unordered_map.hpp>`
  (ya debería estar presente si tu proyecto FliegeV3 compila)
- Si ves errores de linker sobre `boost::unordered_multimap`, verifica las
  rutas de include de Boost en las propiedades del proyecto EterPack

## Cómo Funciona

### Flujo de Registro

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     Almacena m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") ¿existe?
                 "xyz/")              ├─ Sí → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() por cada archivo
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ No → RegisterPack() [ruta EPK original]
```

### Flujo de Acceso a Archivos

```
Cualquier código llama:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → minúsculas, normaliza separadores
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ Si compressed_type == -1 (VPK sentinel):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Desencripta AES-GCM (si está encriptado)
  │       │    ├─ Descomprime LZ4/Zstd/Brotli (si está comprimido)
  │       │    └─ Verifica hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │
  └─ En caso contrario (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [descompresión original: LZ4 + XTEA / header MCOZ]
```

### Gestión de Memoria

CVpkPack usa `CMappedFile::AppendDataBlock()` – el mismo mecanismo que usa CEterPack.
Los datos descomprimidos se copian a un buffer gestionado por CMappedFile
y se liberan automáticamente cuando CMappedFile sale del ámbito.

## Convertir Packs

Usa la herramienta 42pak-generator para convertir packs FliegeV3 existentes:

```
# CLI - convertir par EPK individual a VPK
42pak-generator.exe convert --source metin2_patch_etc.eix --passphrase "tu-frase-secreta"

# CLI - crear VPK desde carpeta
42pak-generator.exe build --source ./ymir_work/item/ --output pack/item.vpk \
    --passphrase "tu-frase-secreta" --algorithm lz4 --compression 6
```

O usa la vista Create en la GUI para crear archivos VPK desde carpetas.

## Estrategia de Migración

1. **Configurar bibliotecas** – Agregar OpenSSL, Zstd, Brotli, BLAKE3 al proyecto (LZ4 probablemente ya existe)
2. **Agregar archivos VPK** – Copiar los 6 nuevos archivos fuente a `source/EterPack/`
3. **Modificar EterPackManager** – Fusionar o reemplazar header e implementación
4. **Editar UserInterface.cpp** – Cambiar 2 líneas
5. **Compilar** – Verificar que todo compile
6. **Convertir un pack** – Por ejemplo `metin2_patch_etc` → probar que se carga correctamente
7. **Convertir el resto** – Uno por uno o todos a la vez
8. **Eliminar EPKs antiguos** – Después de que todos los packs estén convertidos

Como `RegisterPackAuto` vuelve a EPK cuando no existe VPK, puedes convertir
packs incrementalmente sin romper nada.

## Configuración de la Passphrase

| Método | Cuándo Usarlo |
|--------|---------------|
| Codificada en el código fuente | Servidor privado, lo más fácil |
| Archivo de configuración (`metin2.cfg`) | Fácil de cambiar sin recompilar |
| Enviada por el servidor al iniciar sesión | Máxima seguridad – la passphrase cambia cada sesión |

Para passphrase enviada por el servidor, modifica `CAccountConnector` para recibir la passphrase
en la respuesta de autenticación y llama a `CEterPackManager::Instance().SetVpkPassphrase()`
antes de cualquier acceso a packs.

## Solución de Problemas

| Síntoma | Causa | Solución |
|---------|-------|----------|
| Pack no encontrado | Falta extensión `.vpk` | Verificar que el nombre del pack no tenga extensión – `RegisterPackAuto` agrega `.vpk` |
| "HMAC verification failed" | Passphrase incorrecta | Verificar que `SetVpkPassphrase` se llame antes de `RegisterPackAuto` |
| Archivo no encontrado en VPK | Diferencia de mayúsculas/minúsculas | VPK normaliza a minúsculas con separadores `/` |
| Crash en `Get2()` | Colisión de sentinel `compressed_type` | Verificar que ningún archivo EPK use `compressed_type == -1` (ninguno en Metin2 estándar) |
| Errores de linker LZ4/Zstd/Brotli | Biblioteca faltante | Agregar bibliotecas de descompresión en Additional Dependencies |
| Errores de compilación BLAKE3 | Archivos fuente faltantes | Verificar que todos los archivos `blake3_*.c` estén en el proyecto |
| Errores de linker Boost | Faltan Boost includes | Verificar rutas de include de Boost en las propiedades del proyecto EterPack |
| Problema `ENABLE_LOCAL_FILE_LOADING` | Archivos locales no encontrados en release | Definir la macro solo en builds debug – convención FliegeV3 |
