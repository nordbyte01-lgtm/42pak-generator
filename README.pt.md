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

Um gerenciador de arquivos pak moderno e open-source para a comunidade de servidores privados de Metin2. Substitui o formato legado de arquivos EIX/EPK pelo novo formato **VPK**, com criptografia AES-256-GCM, compressão LZ4/Zstandard/Brotli, hashing BLAKE3 e detecção de adulteração HMAC-SHA256.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)

> **Capturas de tela:** Veja [PREVIEW.md](PREVIEW.md) para a galeria completa da GUI nos temas escuro e claro.

---

## Funcionalidades

- **Criar arquivos VPK** — Empacotar diretórios em arquivos `.vpk` únicos com criptografia e compressão opcionais
- **Gerenciar arquivos existentes** — Navegar, pesquisar, extrair e validar arquivos VPK
- **Converter EIX/EPK para VPK** — Migração com um clique do formato legado EterPack (suporta variantes 40250, FliegeV3 e MartySama 5.8)
- **Criptografia AES-256-GCM** — Criptografia autenticada por arquivo com nonces únicos
- **Compressão LZ4 / Zstandard / Brotli** — Escolha o algoritmo que melhor se adapta às suas necessidades
- **Hash de conteúdo BLAKE3** — Verificação criptográfica de integridade para cada arquivo
- **HMAC-SHA256** — Detecção de adulteração em nível de arquivo
- **Ofuscação de nomes de arquivo** — Ocultação opcional dos caminhos de arquivo dentro dos arquivos
- **CLI completo** — Ferramenta independente `42pak-cli` com comandos pack, unpack, list, info, verify, diff, migrate, search, check-duplicates e watch
- **Integração C++ com Metin2** — Código loader drop-in para cliente e servidor para as árvores de código-fonte 40250, FliegeV3 e MartySama 5.8

## Comparação VPK vs EIX/EPK

| Recurso | EIX/EPK (Legado) | VPK (42pak) |
|---------|:-:|:-:|
| Criptografia | TEA / Panama / HybridCrypt | AES-256-GCM |
| Compressão | LZO | LZ4 / Zstandard / Brotli |
| Integridade | CRC32 | BLAKE3 + HMAC-SHA256 |
| Formato de arquivo | Dois arquivos (.eix + .epk) | Arquivo único (.vpk) |
| Contagem de entradas | Máx. 512 | Ilimitado |
| Tamanho do nome | 160 bytes | 512 bytes (UTF-8) |
| Derivação de chave | Chaves hardcoded | PBKDF2-SHA512 (200k iterações) |
| Detecção de adulteração | Nenhuma | HMAC-SHA256 arquivo inteiro |

---

## Início Rápido

### Pré-requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 versão 1809+ (para WebView2)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (geralmente pré-instalado)

### Compilar

```bash
cd 42pak-generator
dotnet restore
dotnet build --configuration Release
```

### Executar a GUI

```bash
dotnet run --project src/FortyTwoPak.UI
```

### Executar o CLI

```bash
dotnet run --project src/FortyTwoPak.CLI -- <comando> [opções]
```

### Executar Testes

```bash
dotnet test
```

### Publicar (Portátil / Instalador)

```powershell
# CLI executável único (~65 MB)
.\publish.ps1 -Target CLI

# GUI pasta portátil (~163 MB)
.\publish.ps1 -Target GUI

# Ambos + instalador Inno Setup
.\publish.ps1 -Target All
```

---

## Uso do CLI

O CLI independente (`42pak-cli`) suporta todas as operações:

```
42pak-cli pack <DIR_ORIGEM> [--output <ARQUIVO>] [--compression <lz4|zstd|brotli|none>] [--level <N>] [--passphrase <SENHA>] [--threads <N>] [--dry-run]
42pak-cli unpack <ARQUIVO> <DIR_SAÍDA> [--passphrase <SENHA>] [--filter <PADRÃO>]
42pak-cli list <ARQUIVO> [--passphrase <SENHA>] [--filter <PADRÃO>] [--json]
42pak-cli info <ARQUIVO> [--passphrase <SENHA>] [--json]
42pak-cli verify <ARQUIVO> [--passphrase <SENHA>] [--filter <PADRÃO>] [--json]
42pak-cli diff <ARQUIVO_A> <ARQUIVO_B> [--passphrase <SENHA>] [--json]
42pak-cli migrate <ARQUIVO_LEGADO> [--output <ARQUIVO>] [--compression <TIPO>] [--passphrase <SENHA>]
42pak-cli search <DIR_TRABALHO> <NOME_OU_PADRÃO>
42pak-cli check-duplicates <DIR_TRABALHO> [--read-index]
42pak-cli watch <DIR_ORIGEM> [--output <ARQUIVO>] [--debounce <MS>]
```

**Flags:** `-q` / `--quiet` suprime toda saída exceto erros. `--json` produz JSON estruturado para pipelines scriptáveis.

**Códigos de saída:** 0 = sucesso, 1 = erro, 2 = falha de integridade, 3 = senha incorreta.

---

## Estrutura do Projeto

```
42pak-generator/
├── 42pak-generator.sln
├── src/
│   ├── FortyTwoPak.Core/            # Biblioteca principal: leitura/escrita VPK, cripto, compressão, importação legado
│   │   ├── VpkFormat/               # Cabeçalho, entrada, classes de arquivo VPK
│   │   ├── Crypto/                  # AES-GCM, PBKDF2, BLAKE3, HMAC
│   │   ├── Compression/             # Compressores LZ4 / Zstandard / Brotli
│   │   ├── Legacy/                  # Leitor e conversor EIX/EPK (40250 + FliegeV3 + MartySama 5.8)
│   │   ├── Cli/                     # Handler CLI compartilhado (12 comandos)
│   │   └── Utils/                   # Ofuscação de nomes, relatório de progresso
│   ├── FortyTwoPak.CLI/             # Ferramenta CLI independente (42pak-cli)
│   ├── FortyTwoPak.UI/              # Aplicativo desktop WebView2
│   │   ├── MainWindow.cs            # Host WinForms com controle WebView2
│   │   ├── BridgeService.cs         # Ponte JavaScript <-> C#
│   │   └── wwwroot/                 # Frontend HTML/CSS/JS (6 abas, tema escuro/claro)
│   └── FortyTwoPak.Tests/           # Suíte de testes xUnit (22 testes)
├── Metin2Integration/
│   ├── Client/
│   │   ├── 40250/                   # Integração C++ para 40250/ClientVS22 (HybridCrypt)
│   │   ├── MartySama58/             # Integração C++ para MartySama 5.8 (Boost + HybridCrypt)
│   │   └── FliegeV3/                # Integração C++ para FliegeV3 (XTEA/LZ4)
│   └── Server/                      # Handler VPK compartilhado do lado servidor
├── docs/
│   └── FORMAT_SPEC.md               # Especificação do formato binário VPK
├── publish.ps1                      # Script de publicação (CLI/GUI/Instalador/Todos)
├── installer.iss                    # Script do instalador Inno Setup 6
├── assets/                          # Capturas de tela e imagens de banner
└── build/                           # Saída da compilação
```

---

## Formato de Arquivo VPK

Arquivo único com o seguinte layout binário:

```
+-------------------------------------+
| VpkHeader (512 bytes, fixo)         |  Magic "42PK", versão, contagem de entradas,
|                                     |  flag de criptografia, salt, autor, etc.
+-------------------------------------+
| Bloco de dados 0 (alinhado a 4096)  |  Conteúdo do arquivo (comprimido + criptografado)
+-------------------------------------+
| Bloco de dados 1 (alinhado a 4096)  |
+-------------------------------------+
| ...                                 |
+-------------------------------------+
| Tabela de entradas (tamanho variável)|  Array de registros VpkEntry. Se criptografado,
|                                     |  envolvido em AES-GCM (nonce + tag + dados).
+-------------------------------------+
| HMAC-SHA256 (32 bytes)              |  Cobre tudo acima. Zero se não assinado.
+-------------------------------------+
```

### Pipeline de Criptografia

Para cada arquivo: `Original -> Compressão LZ4 -> Criptografia AES-256-GCM -> Armazenamento`

Na extração: `Leitura -> Descriptografia AES-256-GCM -> Descompressão LZ4 -> Verificação BLAKE3`

Derivação de chave: `PBKDF2-SHA512("42PK-v1:" + senha, salt, 100000 iterações) -> 64 bytes`
- Primeiros 32 bytes: chave AES-256
- Últimos 32 bytes: chave HMAC-SHA256

Para a especificação binária completa, veja [docs/FORMAT_SPEC.md](docs/FORMAT_SPEC.md).

---

## Uso

### Criando um Arquivo VPK (GUI)

1. Abra o 42pak-generator
2. Vá para a aba **Create Pak**
3. Selecione o diretório de origem contendo os arquivos a empacotar
4. Escolha o caminho do arquivo `.vpk` de saída
5. Configure as opções:
   - **Criptografia**: Ative e insira uma senha
   - **Nível de Compressão**: 0 (nenhum) a 12 (máximo)
   - **Ofuscação de Nomes**: Ocultar caminhos dentro do arquivo
   - **Autor / Comentário**: Metadados opcionais
6. Clique em **Build**

### Convertendo EIX/EPK para VPK

1. Vá para a aba **Convert**
2. Selecione o arquivo `.eix` (o `.epk` deve estar no mesmo diretório)
3. Escolha o caminho de saída `.vpk`
4. Opcionalmente defina a senha de criptografia
5. Clique em **Convert**

### Integração com Cliente Metin2

Três perfis de integração são fornecidos para diferentes árvores de código-fonte:

- **40250 / ClientVS22** (HybridCrypt, LZO): [Metin2Integration/Client/40250/INTEGRATION_GUIDE.md](Metin2Integration/Client/40250/INTEGRATION_GUIDE.md)
- **MartySama 5.8** (Boost, HybridCrypt, LZO): [Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md](Metin2Integration/Client/MartySama58/INTEGRATION_GUIDE.md)
- **FliegeV3** (XTEA, LZ4): [Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md](Metin2Integration/Client/FliegeV3/INTEGRATION_GUIDE.md)

### Integração com Servidor Metin2

Veja [Metin2Integration/Server/INTEGRATION_GUIDE.md](Metin2Integration/Server/INTEGRATION_GUIDE.md) para leitura VPK no lado do servidor.

---

## Tecnologias

| Componente | Tecnologia |
|-----------|------------|
| Runtime | .NET 8.0 (C#) |
| UI | WebView2 (host WinForms) |
| Frontend | HTML5, CSS3, Vanilla JavaScript |
| Criptografia | AES-256-GCM via `System.Security.Cryptography` |
| Derivação de chave | PBKDF2-SHA512 (200.000 iterações) |
| Hashing | BLAKE3 via [blake3 NuGet](https://www.nuget.org/packages/Blake3/) |
| Compressão | LZ4 via [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/), Zstandard via [ZstdSharp](https://www.nuget.org/packages/ZstdSharp.Port/), Brotli (integrado) |
| Detecção de adulteração | HMAC-SHA256 |
| C++ Crypto | OpenSSL 1.1+ |
| Testes | xUnit |

---

## Licença

Este projeto está licenciado sob a Licença MIT — veja o arquivo [LICENSE](LICENSE) para detalhes.

## Contribuições

Contribuições são bem-vindas! Veja [CONTRIBUTING.md](CONTRIBUTING.md) para diretrizes.

## Agradecimentos

- A comunidade de servidores privados de Metin2 por manter o jogo vivo
- O formato original EterPack da YMIR Entertainment por fornecer a base
- [Equipe BLAKE3](https://github.com/BLAKE3-team/BLAKE3) pela função hash rápida
- [K4os](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) pelo binding .NET LZ4
