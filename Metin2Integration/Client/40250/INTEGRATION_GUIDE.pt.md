<p align="center">
  <img src="../../../assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# 42pak VPK - Guia de Integração do Cliente (40250 / ClientVS22)

> **Perfil:** 40250 - destinado à arquitetura multi-cifra HybridCrypt.
> Para integração FliegeV3 (XTEA/LZ4), consulte `../FliegeV3/INTEGRATION_GUIDE.md`.

Substituto direto para o sistema EterPack (EIX/EPK). Baseado no código-fonte
real do cliente 40250. Todos os caminhos de ficheiros referem-se à árvore de código real.

## O Que Obtém

| Funcionalidade | EterPack (antigo) | VPK (novo) |
|---------------|------------------|------------|
| Encriptação | TEA / Panama / HybridCrypt | AES-256-GCM (acelerado por hardware) |
| Compressão | LZO | LZ4 / Zstandard / Brotli |
| Integridade | CRC32 por ficheiro | BLAKE3 por ficheiro + HMAC-SHA256 arquivo |
| Formato de ficheiro | par .eix + .epk | Ficheiro único .vpk |
| Gestão de chaves | Panama IV enviado pelo servidor + HybridCrypt SDB | Frase-passe PBKDF2-SHA512 |

## Arquitetura

O VPK integra-se através do `CEterFileDict` - a mesma pesquisa hash que o
EterPack original utiliza. Quando `CEterPackManager::RegisterPackAuto()`
encontra um ficheiro `.vpk`, cria um `CVpkPack` em vez de `CEterPack`. O
`CVpkPack` regista as suas entradas no `m_FileDict` partilhado com um
marcador sentinel. Quando `GetFromPack()` resolve um nome de ficheiro,
verifica o marcador e redireciona transparentemente para `CEterPack::Get2()`
ou `CVpkPack::Get2()`.

```
CEterPackManager::Get()
  └─ GetFromPack()
       └─ m_FileDict.GetItem(hash, filename)
            ├─ compressed_type == -1  →  CVpkPack::Get2()   [VPK]
            └─ compressed_type >= 0   →  CEterPack::Get2()  [EPK]
```

## Ficheiros

### Novos ficheiros a adicionar a `source/EterPack/`

| Ficheiro | Finalidade |
|----------|-----------|
| `VpkLoader.h` | Classe `CVpkPack` - substituto direto para `CEterPack` |
| `VpkLoader.cpp` | Implementação completa: análise de cabeçalho, tabela de entradas, desencriptar+descomprimir, verificação BLAKE3 |
| `VpkCrypto.h` | Utilitários criptográficos: AES-GCM, PBKDF2, HMAC-SHA256, BLAKE3, LZ4/Zstd/Brotli |
| `VpkCrypto.cpp` | Implementações usando OpenSSL + BLAKE3 + LZ4 + Zstd + Brotli |
| `EterPackManager_Vpk.h` | Cabeçalho `CEterPackManager` modificado com suporte VPK |
| `EterPackManager_Vpk.cpp` | `CEterPackManager` modificado com `RegisterVpkPack`, `RegisterPackAuto`, `SetVpkPassphrase` |

### Ficheiros a modificar

| Ficheiro | Alteração |
|----------|----------|
| `source/EterPack/EterPackManager.h` | Substituir por `EterPackManager_Vpk.h` (ou fundir as adições) |
| `source/EterPack/EterPackManager.cpp` | Substituir por `EterPackManager_Vpk.cpp` (ou fundir as adições) |
| `source/UserInterface/UserInterface.cpp` | Alteração de 2 linhas (ver abaixo) |

## Bibliotecas Necessárias

### OpenSSL 1.1+
- Windows: Descarregar de https://slproweb.com/products/Win32OpenSSL.html
- Cabeçalhos: `<openssl/evp.h>`, `<openssl/hmac.h>`
- Ligação: `libssl.lib`, `libcrypto.lib`

### LZ4
- https://github.com/lz4/lz4/releases
- Cabeçalho: `<lz4.h>`
- Ligação: `lz4.lib`

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

## Integração Passo a Passo

### Passo 1: Adicionar ficheiros ao projeto EterPack no VS

1. Copiar `VpkLoader.h`, `VpkLoader.cpp`, `VpkCrypto.h`, `VpkCrypto.cpp` para `source/EterPack/`
2. Copiar os ficheiros C do BLAKE3 para `source/EterPack/` (ou uma localização partilhada)
3. Adicionar todos os novos ficheiros ao projeto EterPack no Visual Studio
4. Adicionar caminhos de inclusão para OpenSSL, LZ4, Zstd, Brotli em **Additional Include Directories**
5. Adicionar caminhos de bibliotecas em **Additional Library Directories**
6. Adicionar `libssl.lib`, `libcrypto.lib`, `lz4.lib`, `zstd.lib`, `brotlidec.lib`, `brotlicommon.lib` em **Additional Dependencies**

### Passo 2: Substituir o EterPackManager

**Opção A (substituição limpa):**
- Substituir `source/EterPack/EterPackManager.h` por `EterPackManager_Vpk.h`
- Substituir `source/EterPack/EterPackManager.cpp` por `EterPackManager_Vpk.cpp`
- Renomear ambos para `EterPackManager.h` / `EterPackManager.cpp`

**Opção B (fusão):**
Adicionar o seguinte ao `EterPackManager.h` existente:

```cpp
#include "VpkLoader.h"

// Dentro da declaração da classe, adicionar a public:
    bool RegisterVpkPack(const char* c_szName, const char* c_szDirectory);
    bool RegisterPackAuto(const char* c_szName, const char* c_szDirectory, const BYTE* c_pbIV = NULL);
    void SetVpkPassphrase(const char* passphrase);

// Dentro da declaração da classe, adicionar a protected:
    typedef std::list<CVpkPack*> TVpkPackList;
    typedef std::unordered_map<std::string, CVpkPack*, stringhash> TVpkPackMap;
    TVpkPackList    m_VpkPackList;
    TVpkPackMap     m_VpkPackMap;
    std::string     m_strVpkPassphrase;
```

Depois fundir as implementações de `EterPackManager_Vpk.cpp` no ficheiro `.cpp` existente.

### Passo 3: Modificar UserInterface.cpp

Em `source/UserInterface/UserInterface.cpp`, o ciclo de registo de pacotes (cerca da linha 220):

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

### Passo 4: Compilar

Compilar primeiro o projeto EterPack, depois a solução completa. O código VPK
compila ao lado do código EterPack existente - nada é removido.

## Como Funciona

### Fluxo de Registo

```
UserInterface.cpp                    EterPackManager_Vpk.cpp
─────────────────                    ───────────────────────
SetVpkPassphrase("secret")    →     armazena m_strVpkPassphrase

RegisterPackAuto("pack/xyz",  →     _access("pack/xyz.vpk") existe?
                 "xyz/")              ├─ SIM → RegisterVpkPack()
                                      │         └─ CVpkPack::Create()
                                      │              ├─ ReadHeader()
                                      │              ├─ DeriveKeys(passphrase)
                                      │              ├─ VerifyHmac()
                                      │              ├─ ReadEntryTable()
                                      │              └─ RegisterEntries(m_FileDict)
                                      │                   └─ InsertItem() para cada ficheiro
                                      │                      (compressed_type = -1 sentinel)
                                      │
                                      └─ NÃO → RegisterPack() [fluxo EPK original]
```

### Fluxo de Acesso a Ficheiros

```
Qualquer código chama:
  CEterPackManager::Instance().Get(mappedFile, "d:/ymir work/item/weapon.gr2", &pData)

GetFromPack()
  ├─ ConvertFileName → "d:/ymir work/item/weapon.gr2"
  ├─ GetCRC32(filename)
  ├─ m_FileDict.GetItem(hash, filename)
  │
  ├─ se compressed_type == -1 (sentinel VPK):
  │     CVpkPack::Get2(mappedFile, filename, index, &pData)
  │       ├─ DecryptAndDecompress(entry)
  │       │    ├─ Desencriptação AES-GCM (se encriptado)
  │       │    ├─ Descompressão LZ4/Zstd/Brotli (se comprimido)
  │       │    └─ Verificação hash BLAKE3
  │       └─ mappedFile.AppendDataBlock(data, size)
  │            └─ CMappedFile é dono da memória (libertada no destrutor)
  │
  └─ senão (EPK):
        CEterPack::Get2(mappedFile, filename, index, &pData)
          └─ [descompressão original: LZO / Panama / HybridCrypt]
```

### Gestão de Memória

CVpkPack usa `CMappedFile::AppendDataBlock()` - o mesmo mecanismo que o
CEterPack usa para dados HybridCrypt. Os dados descomprimidos são copiados
para um buffer gerido pelo CMappedFile que é automaticamente libertado quando
o CMappedFile sai do âmbito. Não necessita de limpeza manual.

## Converter Pacotes

Use a ferramenta 42pak-generator para converter os seus ficheiros de pacotes existentes:

```bash
# Converter um único par EPK para VPK
42pak-cli convert metin2_patch_etc.eix metin2_patch_etc.vpk --passphrase "o-seu-segredo"

# Construir um VPK a partir de uma pasta
42pak-cli build ./ymir_work/item/ pack/item.vpk --passphrase "o-seu-segredo" --algorithm Zstandard --compression 6

# Converter em lote todos os EIX/EPK num diretório
42pak-cli migrate ./pack/ ./vpk/ --passphrase "o-seu-segredo" --format Standard

# Monitorizar um diretório e reconstruir automaticamente
42pak-cli watch ./gamedata --output game.vpk --passphrase "o-seu-segredo"
```

Ou use a vista Create do GUI para construir arquivos VPK a partir de pastas.

## Estratégia de Migração

1. **Configurar bibliotecas** - adicionar OpenSSL, LZ4, Zstd, Brotli, BLAKE3 ao projeto
2. **Adicionar ficheiros VPK** - copiar os 6 novos ficheiros-fonte para `source/EterPack/`
3. **Modificar EterPackManager** - fundir ou substituir o cabeçalho e implementação
4. **Modificar UserInterface.cpp** - a alteração de 2 linhas
5. **Compilar** - verificar que tudo compila
6. **Converter um pacote** - ex. `metin2_patch_etc` -> testar que carrega corretamente
7. **Converter os restantes pacotes** - um de cada vez ou todos de uma vez
8. **Remover ficheiros EPK antigos** - depois de todos os pacotes serem convertidos

Como `RegisterPackAuto` recorre automaticamente ao EPK quando não existe VPK,
pode converter pacotes incrementalmente sem quebrar nada.

## Configuração da Frase-Passe

| Método | Quando usar |
|--------|------------|
| Codificada no código-fonte | Servidores privados, abordagem mais simples |
| Ficheiro de configuração (`metin2.cfg`) | Fácil de alterar sem recompilar |
| Enviada pelo servidor no login | Segurança máxima - frase-passe muda por sessão |

Para frase-passe enviada pelo servidor, modifique `CAccountConnector` para
recebê-la na resposta de autenticação e chame
`CEterPackManager::Instance().SetVpkPassphrase()` antes de qualquer acesso a pacotes.

## Resolução de Problemas

| Sintoma | Causa | Solução |
|---------|-------|---------|
| Ficheiros de pacote não encontrados | Extensão `.vpk` em falta | Certifique-se que o nome do pacote não inclui extensão - `RegisterPackAuto` adiciona `.vpk` |
| "HMAC verification failed" | Frase-passe errada | Verifique que `SetVpkPassphrase` é chamado antes de `RegisterPackAuto` |
| Ficheiros não encontrados no VPK | Incompatibilidade de maiúsculas/minúsculas | VPK normaliza para minúsculas com separadores `/` |
| Crash em `Get2()` | Colisão do sentinel `compressed_type` | Certifique-se que nenhum ficheiro EPK usa `compressed_type == -1` (nenhum no Metin2 padrão usa) |
| Erro de ligação LZ4/Zstd/Brotli | Biblioteca em falta | Adicione a biblioteca de descompressão às Additional Dependencies |
| Erro de compilação BLAKE3 | Ficheiros-fonte em falta | Certifique-se que todos os ficheiros `blake3_*.c` estão no projeto |
