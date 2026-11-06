# Compatibilidade de formato e produtos

[English](compatibility.md)

O formato do documento e o rigor de validação são independentes. Os comandos existentes usam `canonical` com `legacy` por padrão, preservando comportamento publicado e códigos de saída.

| Perfil | Ciclo de vida | Texto de substituição | Placeholders | Metadados |
| --- | --- | --- | --- | --- |
| `legacy` | Quatro status históricos | Desligado | Desligado | Opcionais, sem validação |
| `advisory` | Status históricos | Desligado | Aviso | Opcionais, sem validação |
| `standard` | Adiciona `Rejected` | Ligado | Aviso | Validados |
| `strict` | Adiciona `Rejected` | Ligado | Erro | Validados |

Use `adr-format: canonical` ou `madr-4` independentemente de `validation-profile: legacy|advisory|standard|strict`. Configurações explícitas de ciclo de vida, nomes, placeholders e metadados refinam o perfil. A geração MADR nativa está publicada na CLI `1.11.0`; a geração canônica continua sendo o padrão.

## Matriz de compatibilidade dos produtos

Os produtos usam linhas SemVer independentes. Números iguais não são obrigatórios, e disponibilidade no código-fonte não comprova publicação. O estado foi verificado em GitHub Releases, NuGet.org, Visual Studio Marketplace, MCP Registry e tags Git em 06/11/2026.

| Produto / capacidade | Versão no repositório | Dependência mínima | Estado de distribuição |
| --- | --- | --- | --- |
| CLI / .NET Tool | candidata `1.12.0` | .NET 10 | Publicada: `1.11.0` no NuGet.org e GitHub Releases. Preparada, não publicada: `1.12.0`. |
| Geração MADR nativa | Incluída na CLI `1.11.0+` | CLI `1.11.0`; schema de formulário permanece `1.0` | Publicada na `1.11.0`; o canônico continua padrão. |
| Grafo de relacionamentos somente leitura | candidata CLI `1.12.0` | Uso direto da CLI; schema `1.0` | Preparado, não publicado. Os contratos JSON/SARIF existentes do check permanecem inalterados. |
| Núcleo, formulários, análises e provider MCP do VS Code | candidata `1.2.0` | VS Code `1.101.0+`; CLI separada adequada a cada comando | Marketplace `1.1.1` está publicado. `1.2.0` está preparada, mas não publicada; artifacts VSIX validados ficam retidos por 21 dias e nunca são publicados automaticamente. |
| Grafo de relacionamentos do VS Code | candidata `1.2.0` | CLI `1.12.0` ou build compatível que exponha `relationships` schema `1.0` | Preparado, não publicado. CLIs antigas ou malformadas produzem estado incompatível explícito. |
| Servidor MCP / .NET Tool | candidata `1.0.2` | .NET 10; cliente `stdio` local | `1.0.1` está publicado no NuGet.org, GitHub Releases, GitHub Packages e MCP Registry. `1.0.2` está preparado, mas não publicado. |
| GitHub Action | candidata coordenada `v1.12.0` | Runner Linux com Docker; container correspondente da CLI | `v1.11.0` exata e `v1` móvel estão publicadas. A Action mantém `check`, `index`, `review`, `impact` e `drift`; ela não expõe `relationships` nem `new`. |

A extensão VS Code consulta capacidades de comando em vez de confiar na versão textual. O grafo exige `relationships --help` e resposta estrita no schema `1.0`; nunca usa parsing alternativo de Markdown em TypeScript. MCP continua opcional e instalado separadamente; a extensão preserva seu comportamento apoiado pela CLI quando MCP está desabilitado, ausente ou incompatível.

## Limites internos de assemblies e pacotes

```text
VS Code / GitHub Action --processo ou container--> CLI AdrGuard --> AdrGuard.Core
cliente MCP compatível --------------stdio------> AdrGuard.Mcp --> AdrGuard.Core
```

`AdrGuard.Core` é interno e sem pacote. Ele não possui versão, pacote, tag ou release independente. Pacotes da CLI contêm `adr-guard` e `AdrGuard.Core.dll` e excluem MCP. Pacotes MCP contêm `adr-guard-mcp` e `AdrGuard.Core.dll` e excluem `adr-guard.dll`. CLI e MCP mantêm IDs de pacote, versões, tags, manifestos e workflows manuais de release separados.

Confirme uma CLI instalada com `adr-guard --version` e o `--help` do comando relevante. Confirme versões da extensão no Marketplace e versões MCP no NuGet.org e MCP Registry. Branch, pacote local, artifact de workflow, manifesto ou nota de release isolados não comprovam publicação.
