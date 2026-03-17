<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Guía de integración del cliente (40250 / ClientVS22)

> **Perfil:** 40250 - dirigido a la arquitectura multi-cifrado HybridCrypt.
> Para la integración de FliegeV3 (XTEA/LZ4), consulte `../FliegeV3/INTEGRATION_GUIDE.md`.

Reemplazo directo para el sistema EterPack (EIX/EPK). Basado en el código
fuente real del cliente 40250. Todas las rutas de archivos hacen referencia
al árbol de código fuente real.

## Qué se obtiene

| Característica | EterPack (antiguo) | VPK (nuevo) |
|---------------|-------------------|-------------|
| Cifrado | TEA / Panama / HybridCrypt | AES-256-GCM (acelerado por hardware) |
| Compresión | LZO | LZ4 / Zstandard / Brotli |
| Integridad | CRC32 por archivo | BLAKE3 por archivo + HMAC-SHA256 archivo |
| Formato de archivo | par .eix + .epk | Un solo archivo .vpk |
| Gestión de claves | Panama IV enviado por servidor + HybridCrypt SDB | Frase de contraseña PBKDF2-SHA512 |

## Arquitectura

VPK se integra a través de `CEterFileDict` - la misma búsqueda hash que
utiliza el EterPack original. Cuando `CEterPackManager::RegisterPackAuto()`
encuentra un archivo `.vpk`, crea un `CVpkPack` en lugar de `CEterPack`.
`CVpkPack` registra sus entradas en el `m_FileDict` compartido con un
marcador sentinel. Cuando `GetFromPack()` resuelve un nombre de archivo,
verifica el marcador y redirige de forma transparente a `CEterPack::Get2()`
o `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Archivos

### Nuevos archivos a agregar a `source/EterPack/`

| Archivo | Propósito |
|---------|----------|
| `VpkLoader.h` | Clase `CVpkPack` - reemplazo directo para `CEterPack` |
| `VpkLoader.cpp` | Implementación completa: análisis de encabezado, tabla de entradas, descifrado+descompresión, verificación BLAKE3 |
| `VpkCrypto.h` | Utilidades criptográficas: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementaciones usando OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Encabezado `CEterPackManager` modificado con soporte VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificado con `RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase` |

### Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `source/EterPack/EterPackManager.h` | Reemplazar con `EterPackManager_Vpk.h` (o fusionar las adiciones) |
| `source/EterPack/EterPackManager.cpp` | Reemplazar con `EterPackManager_Vpk.cpp` (o fusionar las adiciones) |
| `source/UserInterface/UserInterface.cpp` | Cambio de 2 líneas (ver abajo) |

## Bibliotecas requeridas

### OpenSSL 1.1+
- Windows: Descargar desde https://slproweb.com/products/Win32OpenSSL.html
- Encabezados: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Enlace: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Encabezado: `<lz4.h>`
- Enlace: `lz4.lib`

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

## Integración paso a paso

### Paso 1: Agregar archivos al proyecto EterPack en VS

1. Copiar `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` en `source/EterPack/`
2. Copiar los archivos C de BLAKE3 en `source/EterPack/` (o una ubicación compartida)
3. Agregar todos los archivos nuevos al proyecto EterPack en Visual Studio
4. Agregar rutas de inclusión para OpenSSL, LZ4, Zstd, Brotli en **Additional Include Directories**
5. Agregar rutas de bibliotecas en **Additional Library Directories**
6. Agregar `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` en **Additional Dependencies**

### Paso 2: Reemplazar EterPackManager

**Opción A (reemplazo limpio):**
- Reemplazar `source/EterPack/EterPackManager.h` con `EterPackManager_Vpk.h`
- Reemplazar `source/EterPack/EterPackManager.cpp` con `EterPackManager_Vpk.cpp`
- Renombrar ambos a `EterPackManager.h` / `EterPackManager.cpp`

**Opción B (fusión):**
Agregar lo siguiente al `EterPackManager.h` existente:

```cpp
#include "VpkLoader.h"

// Dentro de la declaración de la clase, agregar a public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Dentro de la declaración de la clase, agregar a protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Luego fusionar las implementaciones de `EterPackManager_Vpk.cpp` en su archivo `.cpp` existente.

### Paso 3: Modificar UserInterface.cpp

En `source/UserInterface/UserInterface.cpp`, el bucle de registro de paquetes (alrededor de la línea 220):

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

### Paso 4: Compilar

Compilar primero el proyecto EterPack, luego la solución completa. El código VPK
se compila junto al código EterPack existente - nada se elimina.

## Cómo funciona

### Flujo de registro

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     almacena m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     ¿_access("pack/xyz.vpk") existe?
                 "xyz/")              ├─ SÍ → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() por cada archivo
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ NO → RegisterPack() [flujo EPK original]
```

### Flujo de acceso a archivos

```
Cualquier código llama:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → "d:/ymir work/item/weapon.gr2"
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ si compressed_type == -1 (sentinel VPK):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Descifrado AES-GCM (si está cifrado)
  │       │    ├─ Descompresión LZ4/Zstd/Brotli (si está comprimido)
  │       │    └─ Verificación hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │            └─ CMappedFile posee la memoria (liberada en el destructor)
  │
  └─ sino (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [descompresión original: LZO / Panama / HybridCrypt]
```

### Gestión de memoria

CVpkPack utiliza `CMappedFile::AppendDataBlock()` - el mismo mecanismo que
CEterPack usa para datos HybridCrypt. Los datos descomprimidos se copian en
un buffer propiedad de CMappedFile que se libera automáticamente cuando
CMappedFile sale del ámbito. No se necesita limpieza manual.

## Conversión de paquetes

Use la herramienta 42pak-generator para convertir sus archivos de paquete existentes:

```bash
# Convertir un solo par EPK a VPK
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "su-secreto"

# Construir un VPK desde una carpeta
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "su-secreto" --algorithm Zstandard --compression 6

# Conversión por lotes de todos los EIX/EPK en un directorio
42pak-cli migrate ./pack/ ./vpk/ --passphrase "su-secreto" --format Standard

# Monitorear un directorio y reconstruir automáticamente
42pak-cli watch ./gamedata --output game.vpk --passphrase "su-secreto"
```

O use la vista Create de la GUI para construir archivos VPK desde carpetas.

## Estrategia de migración

1. **Configurar bibliotecas** - agregar OpenSSL, LZ4, Zstd, Brotli, BLAKE3 al proyecto
2. **Agregar archivos VPK** - copiar los 6 nuevos archivos fuente en `source/EterPack/`
3. **Modificar EterPackManager** - fusionar o reemplazar el encabezado e implementación
4. **Modificar UserInterface.cpp** - el cambio de 2 líneas
5. **Compilar** - verificar que todo se compile
6. **Convertir un paquete** - ej. `metin2_patch_etc` -> probar que se cargue correctamente
7. **Convertir los paquetes restantes** - uno a la vez o todos a la vez
8. **Eliminar los archivos EPK antiguos** - una vez que todos los paquetes estén convertidos

Como `RegisterPackAuto` recurre automáticamente a EPK cuando no existe VPK,
puede convertir los paquetes incrementalmente sin romper nada.

## Configuración de la frase de contraseña

| Método | Cuándo usarlo |
|--------|--------------|
| Codificada en el código fuente | Servidores privados, enfoque más simple |
| Archivo de configuración (`metin2.cfg`) | Fácil de cambiar sin recompilar |
| Enviada por el servidor al iniciar sesión | Máxima seguridad - la frase cambia por sesión |

Para la frase enviada por el servidor, modifique `CAccountConnector` para
recibirla en la respuesta de autenticación y llame a
`CEterPackManager::Instance().SetVpkPassphrase()` antes de cualquier acceso a paquetes.

## Solución de problemas

| Síntoma | Causa | Solución |
|---------|-------|----------|
| Archivos de paquete no encontrados | Extensión `.vpk` faltante | Asegúrese de que el nombre del paquete no incluya extensión - `RegisterPackAuto` agrega `.vpk` |
| "HMAC verification failed" | Frase de contraseña incorrecta | Verifique que `SetVpkPassphrase` se llame antes de `RegisterPackAuto` |
| Archivos no encontrados en VPK | Discrepancia de mayúsculas/minúsculas | VPK normaliza a minúsculas con separadores `/` |
| Crash en `Get2()` | Colisión de sentinel `compressed_type` | Asegúrese de que ningún archivo EPK use `compressed_type == -1` (ninguno en el Metin2 estándar lo hace) |
| Error de enlace LZ4/Zstd/Brotli | Biblioteca faltante | Agregue la biblioteca de descompresión a Additional Dependencies |
| Error de compilación BLAKE3 | Archivos fuente faltantes | Asegúrese de que todos los archivos `blake3_*.c` estén en el proyecto |
