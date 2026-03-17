<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Guia de integração do cliente (MartySama 5.8)

> **Perfil:** MartySama 5.8 - direcionado à arquitetura multi-cifra HybridCrypt baseada em Boost.
> Para integração 40250/ClientVS22, veja `../40250/INTEGRATION_GUIDE.md`.
> Para integração FliegeV3, veja `../FliegeV3/INTEGRATION_GUIDE.md`.

Substituição direta para o sistema EterPack (EIX/EPK). Baseado no código-fonte
real do cliente MartySama 5.8 (`Binary & S-Source/Binary/Client/EterPack/`).
Todos os caminhos de ficheiros referem-se à árvore de código-fonte real do MartySama.

## O que se obtém

| Funcionalidade | EterPack (antigo) | VPK (novo) |
|---------------|------------------|------------|
| Encriptação | TEA / Panama / HybridCrypt (Camellia + Twofish + XTEA CTR) | AES-256-GCM (acelerado por hardware) |
| Compressão | LZO | LZ4 / Zstandard / Brotli |
| Integridade | CRC32 por ficheiro | BLAKE3 por ficheiro + HMAC-SHA256 arquivo |
| Formato de ficheiro | par .eix + .epk | Ficheiro único .vpk |
| Gestão de chaves | Panama IV enviado pelo servidor + HybridCrypt SDB | Palavra-passe PBKDF2-SHA512 |

## MartySama 5.8 vs 40250 vs FliegeV3

| Aspeto | 40250 | MartySama 5.8 | FliegeV3 |
|--------|-------|---------------|----------|
| Sistema criptográfico | Camellia/Twofish/XTEA (HybridCrypt) | Igual ao 40250 | Apenas XTEA |
| Entrega de chaves | Panama IV enviado pelo servidor + SDB | Igual ao 40250 | Chaves estáticas compiladas |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` | `boost::unordered_multimap` |
| Tipos de contentor | `std::unordered_map` | `boost::unordered_map` | `std::unordered_map` |
| Estilo de cabeçalho | Include guards | `#pragma once` | Include guards |
| Anti-adulteração | Nenhum | `#ifdef __THEMIDA__` VM_START/VM_END | Nenhum |
| Métodos do manager | `DecryptPackIV`, `RetrieveHybridCryptPackKeys/SDB` | Igual ao 40250 | Nenhum |
| Compressão | LZO | LZO | LZ4 |
| Estrutura de índice | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` | 192 bytes `#pragma pack(push, 4)` |

O código de integração VPK adapta-se aos contentores Boost do MartySama e aos
marcadores Themida, mantendo a mesma interface API (`RegisterVpkPack`,
`RegisterPackAuto`, `SetVpkPassphrase`).

## Arquitetura

O VPK integra-se através de `CEterFileDict` - a mesma pesquisa por hash
`boost::unordered_multimap` que o EterPack original do MartySama utiliza.
Quando `CEterPackManager::RegisterPackAuto()` encontra um ficheiro `.vpk`,
cria um `CVpkPack` em vez de `CEterPack`. O `CVpkPack` regista as suas
entradas no `m_FileDict` partilhado com um marcador sentinel. Quando
`GetFromPack()` resolve um nome de ficheiro, verifica o marcador e
redireciona de forma transparente para `CEterPack::Get2()` ou `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Ficheiros

### Novos ficheiros a adicionar a `Client/EterPack/`

| Ficheiro | Propósito |
|----------|----------|
| `VpkLoader.h` | Classe `CVpkPack` - substituição direta para `CEterPack` |
| `VpkLoader.cpp` | Implementação completa: análise de cabeçalho, tabela de entradas, desencriptação+descompressão, verificação BLAKE3 |
| `VpkCrypto.h` | Utilitários criptográficos: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementações com OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Cabeçalho `CEterPackManager` modificado com VPK + HybridCrypt + Boost |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificado com despacho VPK, HybridCrypt, iteradores Boost, Themida |

### Ficheiros a modificar

| Ficheiro | Alteração |
|----------|----------|
| `Client/EterPack/EterPackManager.h` | Substituir por `EterPackManager_Vpk.h` (ou fundir) |
| `Client/EterPack/EterPackManager.cpp` | Substituir por `EterPackManager_Vpk.cpp` (ou fundir) |
| `Client/UserInterface/UserInterface.cpp` | Alteração de 2 linhas (ver abaixo) |

## Bibliotecas necessárias

### OpenSSL 1.1+
- Windows: Descarregar de https://slproweb.com/products/Win32OpenSSL.html
- Cabeçalhos: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Ligação: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Cabeçalho: `<lz4.h>`
- Ligação: `lz4.lib`
- **Nota:** MartySama 5.8 NÃO inclui LZ4 por predefinição. Esta é uma nova dependência.

### Zstandard
- https://github.com/facebook/zstd/releases
- Cabeçalho: `<zstd.h>`
- Ligação: `zstd.lib` (ou `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Cabeçalhos: `<brotli/decode.h>`
- Ligação: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copiar para o projeto: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: adicionar também `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

### Dependências existentes (já incluídas no MartySama 5.8)

O MartySama 5.8 já inclui estas bibliotecas que **não** são necessárias para o VPK
mas permanecem no projeto para o caminho de código EPK legado:

| Biblioteca | Utilizada por |
|-----------|-------------|
| CryptoPP | HybridCrypt (Camellia, Twofish, XTEA CTR), cifra Panama, funções de hash |
| Boost | `boost::unordered_map`/`boost::unordered_multimap` em EterPack, EterPackManager, CEterFileDict |
| LZO | Descompressão EPK (tipos de pack 1, 2) |

Estas continuam a funcionar ao lado do VPK. Não são necessárias alterações.

## Integração passo a passo

### Passo 1: Adicionar ficheiros ao projeto EterPack no VS

1. Copiar `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` para `Client/EterPack/`
2. Copiar os ficheiros C do BLAKE3 para `Client/EterPack/` (ou uma localização partilhada)
3. Adicionar todos os novos ficheiros ao projeto EterPack no Visual Studio
4. Adicionar caminhos de inclusão para OpenSSL, LZ4, Zstd, Brotli em **Additional Include Directories**
5. Adicionar caminhos de bibliotecas em **Additional Library Directories**
6. Adicionar `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` em **Additional Dependencies**

### Passo 2: Substituir EterPackManager

**Opção A (substituição limpa):**
- Substituir `Client/EterPack/EterPackManager.h` por `EterPackManager_Vpk.h`
- Substituir `Client/EterPack/EterPackManager.cpp` por `EterPackManager_Vpk.cpp`
- Renomear ambos para `EterPackManager.h` / `EterPackManager.cpp`

**Opção B (fundir):**
Adicionar isto ao `EterPackManager.h` existente:

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

**Importante:** Note o uso de `boost::unordered_map` em vez de `std::unordered_map`
para corresponder à convenção do MartySama. O `EterPackManager_Vpk.h` fornecido
já utiliza contentores Boost em todo o código.

Em seguida, fundir as implementações de `EterPackManager_Vpk.cpp` no ficheiro `.cpp` existente.

### Passo 3: Modificar UserInterface.cpp

Em `Client/UserInterface/UserInterface.cpp`, o ciclo de registo de packs
(cerca da linha 557–579):

**Antes:**
```cpp
CEterPackManager::Instance().RegisterPack(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPack(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

**Depois:**
```cpp
CEterPackManager::Instance().RegisterPackAuto(strPackName.c_str(), c_rstFolder.c_str());
CEterPackManager::Instance().RegisterPackAuto(strTexCachePackName.c_str(), c_rstFolder.c_str());
```

E adicionar esta linha **antes** do ciclo de registo:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

É tudo. Duas alterações no total:
1. `SetVpkPassphrase()` antes do ciclo
2. `RegisterPack()` → `RegisterPackAuto()` (2 ocorrências)

Consulte `UserInterface_VpkPatch.cpp` para o patch completo anotado com antes/depois.

### Passo 4: Compilar

Compilar primeiro o projeto EterPack, depois a solução completa. O código VPK compila
ao lado do código EterPack existente - nada é removido.

**Notas de compilação específicas do MartySama 5.8:**
- O MartySama usa Boost em todo o projeto. O `boost::unordered_multimap` em
  `CEterFileDict` é totalmente compatível com VPK - `InsertItem()` funciona de forma idêntica.
- Certifique-se de que `StdAfx.h` no EterPack inclui `<boost/unordered_map.hpp>` (deve
  já incluir se o projeto MartySama compila).
- CryptoPP já está na solução para HybridCrypt - sem conflito com OpenSSL.
  Servem propósitos completamente diferentes (CryptoPP para EPK legado, OpenSSL para VPK).
- Se usar Themida, os marcadores `#ifdef __THEMIDA__` em `EterPackManager_Vpk.cpp`
  já estão configurados. Defina `__THEMIDA__` na sua configuração de compilação se quiser
  que os marcadores VM_START/VM_END estejam ativos.

## Como funciona

### Fluxo de registo

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

### Fluxo de acesso a ficheiros

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

### Coexistência com HybridCrypt

O MartySama 5.8 recebe as chaves de encriptação do servidor ao fazer login:

```
AccountConnector.cpp / PythonNetworkStreamPhaseHandShake.cpp
  ├─ RetrieveHybridCryptPackKeys(stream)   →  applies to CEterPack only
  ├─ RetrieveHybridCryptPackSDB(stream)    →  applies to CEterPack only
  └─ DecryptPackIV(panamaKey)              →  applies to CEterPack only
```

Estes métodos iteram `m_PackMap` (que contém apenas objetos `CEterPack*`).
Os packs VPK são armazenados em `m_VpkPackMap` separadamente, portanto a entrega
de chaves HybridCrypt é completamente transparente - ambos os sistemas coexistem
sem alterações no código de rede.

### Gestão de memória

CVpkPack utiliza `CMappedFile::AppendDataBlock()` - o mesmo mecanismo que
CEterPack usa para os dados HybridCrypt. Os dados descomprimidos são copiados para
um buffer pertencente ao CMappedFile que é libertado automaticamente quando o
CMappedFile sai do âmbito. Não é necessária limpeza manual.

## Conversão de packs

Use a ferramenta 42pak-generator para converter os ficheiros pack MartySama existentes:

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

Ou use a vista Criar da interface gráfica para construir arquivos VPK a partir de pastas.

## Estratégia de migração

1. **Configurar bibliotecas** - adicionar OpenSSL, LZ4, Zstd, Brotli, BLAKE3 ao projeto
2. **Adicionar ficheiros VPK** - copiar os 6 novos ficheiros-fonte para `Client/EterPack/`
3. **Modificar EterPackManager** - fundir ou substituir o cabeçalho e implementação
4. **Modificar UserInterface.cpp** - a alteração de 2 linhas
5. **Compilar** - verificar que tudo compila
6. **Converter um pack** - ex. `metin2_patch_etc` → testar que carrega corretamente
7. **Converter os packs restantes** - um de cada vez ou todos de uma vez
8. **Remover ficheiros EPK antigos** - depois de convertidos todos os packs

Como `RegisterPackAuto` recai para EPK quando não existe um VPK, pode
converter packs de forma incremental sem quebrar nada.

## Configuração da palavra-passe

| Método | Quando usar |
|--------|-------------|
| Codificada no código-fonte | Servidores privados, abordagem mais simples |
| Ficheiro de configuração (`metin2.cfg`) | Fácil de alterar sem recompilar |
| Enviada pelo servidor ao fazer login | Segurança máxima - a palavra-passe muda por sessão |

Para palavra-passe enviada pelo servidor, modifique `CAccountConnector` para recebê-la
na resposta de autenticação e chame `CEterPackManager::Instance().SetVpkPassphrase()`
antes de qualquer acesso a packs. O `AccountConnector.cpp` do MartySama já trata
pacotes personalizados do servidor - adicione um novo handler junto à chamada
existente `RetrieveHybridCryptPackKeys`.

## Resolução de problemas

| Sintoma | Causa | Solução |
|---------|-------|---------|
| Ficheiros pack não encontrados | Extensão `.vpk` em falta | Certifique-se de que o nome do pack não inclui extensão - `RegisterPackAuto` adiciona `.vpk` |
| "HMAC verification failed" | Palavra-passe incorreta | Verifique que `SetVpkPassphrase` é chamado antes de `RegisterPackAuto` |
| Ficheiros não encontrados no VPK | Diferença de maiúsculas/minúsculas no caminho | VPK normaliza para minúsculas com separadores `/` |
| Crash em `Get2()` | Colisão do sentinel `compressed_type` | Certifique-se de que nenhum ficheiro EPK usa `compressed_type == -1` (nenhum usa no Metin2 padrão) |
| Erro de ligação LZ4/Zstd/Brotli | Biblioteca em falta | Adicione a biblioteca de descompressão a Additional Dependencies |
| Erro de compilação BLAKE3 | Ficheiros-fonte em falta | Certifique-se de que todos os ficheiros `blake3_*.c` estão no projeto |
| Erros do linker Boost | Includes Boost em falta | Verifique os caminhos de includes Boost nas propriedades do projeto EterPack |
| Conflito CryptoPP + OpenSSL | Nomes de símbolos duplicados | Não deveria acontecer - definem símbolos separados. Se acontecer, verifique poluição por `using namespace` |
| Erros Themida | `__THEMIDA__` não definido | Defina na sua configuração de compilação, ou remova os blocos `#ifdef __THEMIDA__` de `EterPackManager_Vpk.cpp` |
