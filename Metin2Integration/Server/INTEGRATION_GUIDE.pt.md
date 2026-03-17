<p align="center">
  <img src="../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Guia de Integração do Servidor

Baseado na análise do código-fonte real do servidor 40250 em
`Server/metin2_server+src/metin2/src/server/`.

## Descoberta Chave: O Servidor Não Usa EterPack

O servidor 40250 tem **zero** referências a EterPack, ficheiros `.eix` ou `.epk`.
Todas as operações I/O do servidor usam `fopen()`/`fread()` simples ou `std::ifstream`:

| Loader | Usado Por | Ficheiro Fonte |
|--------|----------|----------------|
| `CTextFileLoader` | mob_manager, item_manager, DragonSoul, skill, refine | `game/src/text_file_loader.cpp` |
| `CGroupTextParseTreeLoader` | item special groups, tabelas DragonSoul | `game/src/group_text_parse_tree.cpp` |
| `cCsvTable` / `cCsvFile` | mob_proto.txt, item_proto.txt, *_names.txt | `db/src/CsvReader.cpp` |
| `fopen()` direto | CONFIG, ficheiros de mapa, regen, locale, fishing, cube | Vários |

O servidor referencia packs apenas num local: **`ClientPackageCryptInfo.cpp`**,
que carrega chaves de encriptação de `package.dir/` e envia-as ao **cliente**
para desencriptar ficheiros `.epk`. O servidor nunca abre ficheiros de pack.

## Opções de Integração

### Opção A: Manter Ficheiros do Servidor Como Ficheiros Raw (Recomendado)

Como o servidor já lê ficheiros raw do disco, a abordagem mais simples é:
- Manter todos os ficheiros de dados do servidor (protos, configs, mapas) como ficheiros raw
- Usar VPK apenas no lado do cliente
- Remover ou atualizar `ClientPackageCryptInfo` para não enviar chaves EPK

Esta é a abordagem recomendada para a maioria das configurações.

### Opção B: Empacotar Dados do Servidor em Arquivos VPK

Se quiser ficheiros de dados do servidor resistentes a manipulação e encriptados, use `CVpkHandler`
para ler de arquivos VPK em vez de ficheiros raw.

## Ficheiros

| Ficheiro | Propósito |
|----------|-----------|
| `VpkHandler.h` | Leitor VPK autónomo (sem dependência de EterPack) |
| `VpkHandler.cpp` | Implementação completa com criptografia integrada |

O handler do servidor é completamente autónomo – inclui as suas próprias implementações
de AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4, Zstandard e Brotli
integradas. Sem dependência de qualquer código EterPack do lado do cliente.

## Bibliotecas Necessárias

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

### Adições ao Makefile

```makefile
CXXFLAGS += -I/usr/include/openssl
LDFLAGS  += -lssl -lcrypto -llz4 -lzstd -lbrotlidec -lbrotlicommon
SOURCES  += VpkHandler.cpp blake3.c blake3_dispatch.c blake3_portable.c
```

## Exemplos de Integração

### Exemplo 1: Carregar Dados Proto de VPK

O servidor DB carrega protos de ficheiros CSV/binários em `db/src/ClientManagerBoot.cpp`:

**Antes (ficheiro raw):**
```cpp
FILE* fp = fopen("locale/en/item_proto", "rb");
fread(itemData, sizeof(TItemTable), count, fp);
fclose(fp);
```

**Depois (VPK):**
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
    // processar itens...
    return true;
}
```

### Exemplo 2: Ler Ficheiros de Texto de VPK

Para ficheiros no estilo `CTextFileLoader` (mob_group.txt, etc.):

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

### Exemplo 3: Atualizar ClientPackageCryptInfo

O servidor 40250 envia chaves de encriptação EPK ao cliente através de
`game/src/ClientPackageCryptInfo.cpp` e `game/src/panama.cpp`.

Ao usar VPK, tem duas opções:

**Opção A: Remover completamente o envio de chaves de pack**
Se todos os packs foram convertidos para VPK, o cliente já não precisa de
chaves EPK enviadas pelo servidor. Remova ou desative `LoadClientPackageCryptInfo()` e `PanamaLoad()`
da sequência de arranque em `game/src/main.cpp`.

**Opção B: Manter suporte híbrido**
Se alguns packs permanecem como EPK enquanto outros são VPK, mantenha o
código de envio de chaves existente para packs EPK. Packs VPK usam o seu próprio
mecanismo de passphrase e não precisam de chaves enviadas pelo servidor.

### Exemplo 4: Comandos de Admin

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

## Gestão de Passphrase

No servidor, armazene a passphrase VPK de forma segura:

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

## Suporte de Compressão

O handler do servidor suporta os três algoritmos de compressão:

| Algoritmo | ID | Biblioteca | Velocidade | Rácio |
|-----------|----|-----------|------------|-------|
| LZ4 | 1 | liblz4 | Mais rápido | Bom |
| Zstandard | 2 | libzstd | Rápido | Melhor |
| Brotli | 3 | libbrotli | Mais lento | O melhor |

O algoritmo é armazenado no cabeçalho VPK e detetado automaticamente.
Não é necessária configuração no lado da leitura.

## Estrutura de Diretórios

```
/usr/metin2/
├── conf/
│   └── vpk.conf             ← passphrase (chmod 600)
├── pack/
│   ├── locale_en.vpk        ← dados de localização
│   ├── locale_de.vpk
│   ├── proto.vpk             ← tabelas item/mob proto
│   └── scripts.vpk           ← scripts de quest, configurações
└── game/
    └── src/
        ├── VpkHandler.h
        ├── VpkHandler.cpp
        └── blake3.h / blake3.c ...
```

## Notas de Desempenho

- Leituras de ficheiros VPK são I/O sequencial (uma busca + uma leitura por ficheiro)
- A tabela de entradas é parseada uma vez em `Open()` e guardada em cache na memória
- Para ficheiros acedidos frequentemente, considere guardar em cache o resultado de `ReadFile()`
- A verificação de hash BLAKE3 adiciona ~0,1ms por MB de dados

## Deteção de Manipulação

O HMAC-SHA256 no final de cada ficheiro VPK garante:
- Modificações a qualquer conteúdo de ficheiro são detetadas
- Entradas adicionadas ou removidas são detetadas
- Manipulação do cabeçalho é detetada

Se `Open()` devolver false para um VPK encriptado, o arquivo pode ter sido
manipulado ou a passphrase está errada.
- A descompressão LZ4 adiciona ~0,5ms por MB de dados comprimidos
