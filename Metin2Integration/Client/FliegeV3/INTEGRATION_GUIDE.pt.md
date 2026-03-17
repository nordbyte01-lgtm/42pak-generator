<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK – Guia de Integração do Cliente FliegeV3

Substituição direta do sistema EterPack (EIX/EPK). Referenciado a partir do código-fonte real
do cliente FliegeV3 binary-src. Todos os caminhos referem-se à árvore de código FliegeV3 real.

## O Que Você Recebe

| Funcionalidade | EterPack (FliegeV3) | VPK (Novo) |
|----------------|---------------------|------------|
| Encriptação | XTEA (chave 128-bit, 32 rounds) | AES-256-GCM (acelerado por hardware) |
| Compressão | LZ4 (migrado de LZO) | LZ4 / Zstandard / Brotli |
| Integridade | CRC32 por ficheiro | BLAKE3 por ficheiro + HMAC-SHA256 do arquivo |
| Formato de Ficheiro | Par .eix + .epk | Ficheiro .vpk único |
| Gestão de Chaves | Chaves XTEA estáticas codificadas no código | Passphrase PBKDF2-SHA512 |
| Entradas de Índice | `TEterPackIndex` de 192 bytes | Entradas VPK de comprimento variável |

## FliegeV3 vs 40250 – Porquê Perfis Separados?

O FliegeV3 difere significativamente do setup 40250:

| Aspeto | 40250 | FliegeV3 |
|--------|-------|----------|
| Sistema de Encriptação | Híbrido Camellia/Twofish/XTEA (HybridCrypt) | Apenas XTEA |
| Entrega de Chaves | Panama IV do servidor + blocos SDB | Chaves estáticas compiladas |
| `CEterFileDict` | `std::unordered_multimap` | `boost::unordered_multimap` |
| Métodos do Manager | `DecryptPackIV`, `WriteHybridCryptPackInfo`, `RetrieveHybridCryptPackKeys/SDB` | Nenhum destes métodos |
| Pesquisa de Ficheiros | Sempre permite ficheiros locais | Condicional `ENABLE_LOCAL_FILE_LOADING` |
| Estrutura do Índice | 169 bytes (#pragma pack(push,4) nome 161 chars) | 192 bytes (mesma struct, alinhamento/padding diferente) |

O código de integração VPK adapta-se a estas diferenças mantendo a mesma API
(`RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase`).

## Arquitetura

O VPK integra-se através de `CEterFileDict` – o mesmo `boost::unordered_multimap`
que o EterPack original do FliegeV3 utiliza. Quando `RegisterPackAuto()` encontra um ficheiro
`.vpk`, cria um `CVpkPack` em vez de `CEterPack`. O `CVpkPack` regista as suas entradas
no `m_FileDict` partilhado com um marcador sentinel (`compressed_type == -1`).

Quando `GetFromPack()` resolve um nome de ficheiro, verifica o marcador e despacha
transparentemente para `CEterPack::Get2()` ou `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Ficheiros

### Novos Ficheiros para Adicionar a `source/EterPack/`

| Ficheiro | Propósito |
|----------|-----------|
| `VpkLoader.h` | Classe `CVpkPack` – substituição direta de `CEterPack` |
| `VpkLoader.cpp` | Implementação completa: parsing de cabeçalho, tabela de entradas, desencriptação+descompressão, verificação BLAKE3 |
| `VpkCrypto.h` | Utilitários de encriptação: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementação com OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Cabeçalho `CEterPackManager` modificado com suporte VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificado – sem HybridCrypt, lógica de pesquisa FliegeV3 |

### Ficheiros para Modificar

| Ficheiro | Alteração |
|----------|-----------|
| `source/EterPack/EterPackManager.h` | Substituir por `EterPackManager_Vpk.h` (ou fazer merge) |
| `source/EterPack/EterPackManager.cpp` | Substituir por `EterPackManager_Vpk.cpp` (ou fazer merge) |
| `source/UserInterface/UserInterface.cpp` | Alterar 2 linhas (ver abaixo) |

## Bibliotecas Necessárias

### OpenSSL 1.1+
- Windows: Descarregar de https://slproweb.com/products/Win32OpenSSL.html
- Cabeçalhos: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Linkar: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Cabeçalhos: `<lz4.h>`
- Linkar: `lz4.lib`
- **Nota:** O FliegeV3 já usa LZ4 para compressão de packs. Se o seu projeto
  já linka `lz4.lib`, não é necessária configuração adicional.

### Zstandard
- https://github.com/facebook/zstd/releases
- Cabeçalhos: `<zstd.h>`
- Linkar: `zstd.lib` (ou `libzstd.lib`)

### Brotli
- https://github.com/google/brotli/releases
- Cabeçalhos: `<brotli/decode.h>`
- Linkar: `brotlidec.lib`, `brotlicommon.lib`

### BLAKE3
- https://github.com/BLAKE3-team/BLAKE3/tree/master/c
- Copiar para o projeto: `blake3.h`, `blake3.c`, `blake3_dispatch.c`, `blake3_portable.c`
- x86/x64: Adicionar também `blake3_sse2.c`, `blake3_sse41.c`, `blake3_avx2.c`, `blake3_avx512.c`

## Integração Passo a Passo

### Passo 1: Adicionar Ficheiros ao Projeto EterPack no VS

1. Copiar `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` para `source/EterPack/`
2. Copiar ficheiros C do BLAKE3 para `source/EterPack/` (ou localização partilhada)
3. Adicionar todos os novos ficheiros ao projeto EterPack no Visual Studio
4. Adicionar caminhos de include para OpenSSL, Zstd, Brotli em **Additional Include Directories**
   - O LZ4 já deve estar configurado se a sua build FliegeV3 o utiliza
5. Adicionar caminhos de biblioteca em **Additional Library Directories**
6. Adicionar `libssl.lib`, `libcrypto.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` em **Additional Dependencies**
   - `lz4.lib` já deve estar linkado

### Passo 2: Substituir EterPackManager

**Opção A (Substituição limpa):**
- Substituir `source/EterPack/EterPackManager.h` por `EterPackManager_Vpk.h`
- Substituir `source/EterPack/EterPackManager.cpp` por `EterPackManager_Vpk.cpp`
- Renomear ambos para `EterPackManager.h` / `EterPackManager.cpp`

**Opção B (Merge):**
Adicionar isto ao `EterPackManager.h` existente:

```cpp
#include "VpkLoader.h"

// Dentro da declaração da classe, adicionar em public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Dentro da declaração da classe, adicionar em protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Depois fazer merge da implementação de `EterPackManager_Vpk.cpp` no seu ficheiro `.cpp` existente.

**Importante:** Não adicione `DecryptPackIV`, `WriteHybridCryptPackInfo`,
`RetrieveHybridCryptPackKeys` ou `RetrieveHybridCryptPackSDB` – o FliegeV3
não tem estes métodos.

### Passo 3: Modificar UserInterface.cpp

Em `source/UserInterface/UserInterface.cpp`, no loop de registo de packs:

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

E adicionar esta linha **antes** do loop de registo:
```cpp
CEterPackManager::Instance().SetVpkPassphrase("your-server-passphrase");
```

É só isto. Duas alterações no total:
1. `SetVpkPassphrase()` antes do loop
2. `RegisterPack()` → `RegisterPackAuto()` (2 locais)

**Nota:** Ao contrário da integração 40250, o FliegeV3 não requer patches de código de rede.
Não há chamadas `RetrieveHybridCryptPackKeys` ou `RetrieveHybridCryptPackSDB`
para se preocupar.

### Passo 4: Compilar

Compile o projeto EterPack primeiro, depois compile toda a solução. O código VPK compila
juntamente com o código EterPack existente – nada é removido.

**Notas de compilação específicas do FliegeV3:**
- O FliegeV3 usa Boost para `boost::unordered_multimap` em `CEterFileDict`. Isto é compatível
  com o VPK – `InsertItem()` funciona da mesma forma.
- Certifique-se de que `StdAfx.h` no EterPack inclui `<boost/unordered_map.hpp>`
  (já deve estar presente se o seu projeto FliegeV3 compila)
- Se vir erros de linker sobre `boost::unordered_multimap`, verifique os
  caminhos de include do Boost nas propriedades do projeto EterPack

## Como Funciona

### Fluxo de Registo

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     Guarda m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") existe?
                 "xyz/")              ├─ Sim → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() para cada ficheiro
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ Não → RegisterPack() [caminho EPK original]
```

### Fluxo de Acesso a Ficheiros

```
Qualquer código chama:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → minúsculas, normaliza separadores
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ Se compressed_type == -1 (VPK sentinel):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Desencripta AES-GCM (se encriptado)
  │       │    ├─ Descomprime LZ4/Zstd/Brotli (se comprimido)
  │       │    └─ Verifica hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │
  └─ Caso contrário (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [descompressão original: LZ4 + XTEA / cabeçalho MCOZ]
```

### Gestão de Memória

CVpkPack usa `CMappedFile::AppendDataBlock()` – o mesmo mecanismo que CEterPack usa.
Os dados descomprimidos são copiados para um buffer gerido pelo CMappedFile
e libertados automaticamente quando o CMappedFile sai do âmbito.

## Converter Packs

Use a ferramenta 42pak-generator para converter packs FliegeV3 existentes:

```
# CLI - converter par EPK único para VPK
42pak-generator.exe convert --source metin2_patch_etc.eix --passphrase "a-sua-frase-secreta"

# CLI - criar VPK a partir de pasta
42pak-generator.exe build --source ./ymir_work/item/ --output pack/item.vpk \
    --passphrase "a-sua-frase-secreta" --algorithm lz4 --compression 6
```

Ou use a vista Create no GUI para criar arquivos VPK a partir de pastas.

## Estratégia de Migração

1. **Configurar bibliotecas** – Adicionar OpenSSL, Zstd, Brotli, BLAKE3 ao projeto (LZ4 provavelmente já existe)
2. **Adicionar ficheiros VPK** – Copiar os 6 novos ficheiros fonte para `source/EterPack/`
3. **Modificar EterPackManager** – Fazer merge ou substituir cabeçalho e implementação
4. **Editar UserInterface.cpp** – Alterar 2 linhas
5. **Compilar** – Verificar que tudo compila
6. **Converter um pack** – Por exemplo `metin2_patch_etc` → testar que carrega corretamente
7. **Converter o resto** – Um a um ou todos de uma vez
8. **Remover EPKs antigos** – Depois de todos os packs estarem convertidos

Como `RegisterPackAuto` volta ao EPK quando não existe VPK, pode converter
packs incrementalmente sem quebrar nada.

## Configuração de Passphrase

| Método | Quando Usar |
|--------|-------------|
| Codificada no código-fonte | Servidor privado, mais fácil |
| Ficheiro de configuração (`metin2.cfg`) | Fácil de alterar sem recompilar |
| Enviada pelo servidor no login | Máxima segurança – passphrase muda a cada sessão |

Para passphrase enviada pelo servidor, modifique `CAccountConnector` para receber a passphrase
na resposta de autenticação e chame `CEterPackManager::Instance().SetVpkPassphrase()`
antes de qualquer acesso a packs.

## Resolução de Problemas

| Sintoma | Causa | Solução |
|---------|-------|---------|
| Pack não encontrado | Falta extensão `.vpk` | Verificar que o nome do pack não tem extensão – `RegisterPackAuto` adiciona `.vpk` |
| "HMAC verification failed" | Passphrase errada | Verificar que `SetVpkPassphrase` é chamado antes de `RegisterPackAuto` |
| Ficheiro não encontrado no VPK | Diferença de maiúsculas/minúsculas | VPK normaliza para minúsculas com separadores `/` |
| Crash em `Get2()` | Colisão do sentinel `compressed_type` | Verificar que nenhum ficheiro EPK usa `compressed_type == -1` (nenhum no Metin2 padrão) |
| Erros de linker LZ4/Zstd/Brotli | Biblioteca em falta | Adicionar bibliotecas de descompressão em Additional Dependencies |
| Erros de compilação BLAKE3 | Ficheiros fonte em falta | Verificar que todos os ficheiros `blake3_*.c` estão no projeto |
| Erros de linker Boost | Boost includes em falta | Verificar caminhos de include do Boost nas propriedades do projeto EterPack |
| Problema `ENABLE_LOCAL_FILE_LOADING` | Ficheiros locais não encontrados em release | Definir a macro apenas em builds debug – padrão FliegeV3 |
