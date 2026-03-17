<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Guía de Integración del Servidor

Basado en el análisis del código fuente real del servidor 40250 en
`Server/metin2_server+src/metin2/src/server/`.

## Hallazgo Clave: El Servidor No Usa EterPack

El servidor 40250 tiene **cero** referencias a EterPack, archivos `.eix` o `.epk`.
Todas las operaciones I/O del servidor usan `fopen()`/`fread()` simple o `std::ifstream`:

| Loader | Usado Por | Archivo Fuente |
|--------|----------|----------------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, tablas DragonSoul | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| `fopen()` directo | CONFIG, archivos de mapa, regen, locale, fishing, cube | Varios |

El servidor hace referencia a paquetes solo en un lugar: **`ClientPackageCryptInfo.cpp`**,
que carga claves de encriptación desde `package.dir/` y las envía al **cliente**
para desencriptar archivos `.epk`. El servidor nunca abre archivos de paquete.

## Opciones de Integración

### Opción A: Mantener Archivos del Servidor Como Archivos Raw (Recomendado)

Como el servidor ya lee archivos raw del disco, el enfoque más simple es:
- Mantener todos los archivos de datos del servidor (protos, configs, mapas) como archivos raw
- Usar VPK solo en el lado del cliente
- Eliminar o actualizar `ClientPackageCryptInfo` para no enviar claves EPK

Este es el enfoque recomendado para la mayoría de las configuraciones.

### Opción B: Empaquetar Datos del Servidor en Archivos VPK

Si desea archivos de datos del servidor resistentes a manipulación y encriptados, use `CVpkHandler`
para leer desde archivos VPK en lugar de archivos raw.

## Archivos

| Archivo | Propósito |
|---------|-----------|
| `VpkHandler.h` | Lector VPK autónomo (sin dependencia de EterPack) |
| `VpkHandler.cpp` | Implementación completa con criptografía integrada |

El handler del servidor es completamente autónomo – incluye sus propias implementaciones
de AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard y Brotli
integradas. Sin dependencia de ningún código EterPack del lado del cliente.

## Bibliotecas Necesarias

### Linux (servidor típico)

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

### Adiciones al Makefile

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## Ejemplos de Integración

### Ejemplo 1: Cargar Datos Proto desde VPK

El servidor DB carga protos desde archivos CSV/binarios en `db/src/ClientManagerBoot.cpp`:

**Antes (archivo raw):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**Después (VPK):**
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
    // procesar elementos...
    return true;
}
```

### Ejemplo 2: Leer Archivos de Texto desde VPK

Para archivos estilo `CTextFileLoader` (mob_group.txt, etc.):

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
    // parsear como antes...
    return true;
}
```

### Ejemplo 3: Actualizar ClientPackageCryptInfo

El servidor 40250 envía claves de encriptación EPK al cliente a través de
`game/src/ClientPackageCryptInfo.cpp` y `game/src/panama.cpp`.

Al usar VPK, tiene dos opciones:

**Opción A: Eliminar completamente el envío de claves de paquete**
Si todos los paquetes han sido convertidos a VPK, el cliente ya no necesita
claves EPK enviadas por el servidor. Elimine o deshabilite `LoadClientPackageCryptInfo()` y `PanamaLoad()`
de la secuencia de arranque en `game/src/main.cpp`.

**Opción B: Mantener soporte híbrido**
Si algunos paquetes permanecen como EPK mientras otros son VPK, mantenga el
código de envío de claves existente para paquetes EPK. Los paquetes VPK usan su propio
mecanismo de passphrase y no necesitan claves enviadas por el servidor.

### Ejemplo 4: Comandos de Admin

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

## Gestión de Passphrase

En el servidor, almacene la passphrase VPK de forma segura:

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

## Soporte de Compresión

El handler del servidor soporta los tres algoritmos de compresión:

| Algoritmo | ID | Biblioteca | Velocidad | Ratio |
|-----------|----|-----------|-----------|-------|
| LZ4 | 1 | liblz4 | El más rápido | Bueno |
| Zstandard | 2 | libzstd | Rápido | Mejor |
| Brotli | 3 | libbrotli | Más lento | El mejor |

El algoritmo se almacena en el header VPK y se detecta automáticamente.
No se necesita configuración en el lado de lectura.

## Estructura de Directorios

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← datos de localización
│   ├── locale_de.vpk
│   ├── proto.vpk             ← tablas item/mob proto
│   └── scripts.vpk           ← scripts de quests, configuraciones
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## Notas de Rendimiento

- Las lecturas de archivos VPK son I/O secuencial (una búsqueda + una lectura por archivo)
- La tabla de entradas se parsea una vez en `Open()` y se almacena en caché en memoria
- Para archivos accedidos frecuentemente, considere almacenar en caché la salida de `ReadFile()`
- La verificación de hash BLAKE3 añade ~0,1ms por MB de datos

## Detección de Manipulación

El HMAC-SHA256 al final de cada archivo VPK asegura:
- Las modificaciones a cualquier contenido de archivo son detectadas
- Las entradas añadidas o eliminadas son detectadas
- La manipulación del header es detectada

Si `Open()` devuelve false para un VPK encriptado, el archivo puede haber sido
manipulado o la passphrase es incorrecta.
- La descompresión LZ4 añade ~0,5ms por MB de datos comprimidos
