# Compatibilidade de formato e validação de ADR

O formato do documento e o rigor de validação são independentes. Os comandos existentes usam `canonical` com `legacy` por padrão, preservando comportamento publicado e códigos de saída.

| Perfil | Ciclo de vida | Texto de substituição | Placeholders | Metadados |
| --- | --- | --- | --- | --- |
| `legacy` | Quatro status históricos | Desligado | Desligado | Opcionais, sem validação |
| `advisory` | Status históricos | Desligado | Aviso | Opcionais, sem validação |
| `standard` | Adiciona `Rejected` | Ligado | Aviso | Validados |
| `strict` | Adiciona `Rejected` | Ligado | Erro | Validados |

Use `adr-format: canonical` ou `madr-4` independentemente de `validation-profile: legacy|advisory|standard|strict`. Configurações explícitas de ciclo de vida, nomes, placeholders e metadados refinam o perfil. Fingerprints de baseline mantêm a identidade de código, caminho e mensagem; avisos não alteram o código de saída. JSON adiciona `severity` apenas para avisos e SARIF usa o nível padrão `warning`.

## Matriz de compatibilidade dos produtos

Os produtos usam linhas SemVer independentes. Números iguais não são obrigatórios, e disponibilidade no código-fonte não comprova publicação. Na tabela, **publicado** significa disponível no registro público indicado, **candidato** significa um baseline versionado de código/pacote preparado e validado para release, mas não promovido a um registro, e **somente código-fonte** significa implementação presente sem baseline de release para consumidores.

| Produto / capacidade | Versão no repositório | Dependência mínima | Runtime / host | Dependências opcionais | Funciona sem MCP? | Estado de distribuição verificado em 20/10/2026 |
| --- | --- | --- | --- | --- | --- | --- |
| CLI / .NET Tool | candidata `1.9.0` | Nenhuma | .NET 10 | Git somente para validação incremental, review comparativo, impacto e metadados de drift associados a commit; provider de IA configurado somente para `draft`/`review` | Sim | **Publicado:** `1.8.0` no NuGet.org e em GitHub Releases. **Não publicado:** `1.9.0`; pacotes locais adicionam localizações opcionais de diagnóstico e incluem o Core interno. |
| Comandos básicos, Problems e Explorer no VS Code | `1.1.0` | CLI `1.3.0+` compatível para o conjunto básico documentado | VS Code `1.101.0+`; host Node de workspace; workspace local `file:` confiável | Requisitos de Git/provider vêm do comando da CLI escolhido | Sim | **Publicado:** Marketplace `1.1.0`. Artifacts VSIX dos workflows são um canal separado de instalação por 21 dias e nunca são publicados automaticamente. |
| Criação/edição visual no VS Code | `1.1.0` | CLI `1.7.0+`, schema de formulário `1.0` | Mesmo limite do VS Code | Nenhuma além da CLI | Sim | **Publicado:** Marketplace `1.1.0`. |
| Impacto, drift, baseline, catálogo enriquecido, draft e review/comparação no VS Code | `1.1.0` | Build da CLI cujo `<command> --help` exponha o contrato pedido; a CLI pública `1.8.0` suporta os fluxos documentados | Mesmo limite do VS Code | Git/mapeamentos/evidências locais ou provider de IA somente quando o fluxo escolhido exigir | Sim | **Publicado:** Marketplace `1.1.0`; a extensão ainda exige uma CLI instalada separadamente. |
| Provider MCP do VS Code | `1.1.0` | MCP `1.0.0+` instalado separadamente com contratos schema `1.0` inalterados | VS Code `1.101.0+`, host de workspace local/remoto confiável | `dotnet` no `PATH` do host da extensão quando configurado por DLL | Não aplicável; esta linha é a integração opcional | **Publicado:** incluído no Marketplace `1.1.0`, desabilitado por padrão. MCP `1.0.1` permanece uma candidata não publicada. |
| Servidor MCP / .NET Tool | candidata `1.0.1` | Nenhuma; a extensão não é obrigatória | .NET 10, cliente `stdio` local | Git somente para `analyze_impact` e metadados de commit | Não aplicável; executa de forma independente | **Publicado:** `1.0.0` no NuGet.org, em GitHub Releases e no MCP Registry. **Não publicado:** `1.0.1`; o manifesto versionado do Registry permanece intencionalmente em `1.0.0`. |

A CLI executa independentemente da extensão e do MCP. A extensão preserva todo o comportamento apoiado pela CLI quando MCP está desabilitado, ausente ou incompatível. O servidor MCP funciona com qualquer cliente `stdio` compatível e não exige a extensão.

## Limites internos de assemblies e pacotes

As builds atuais do código-fonte usam este grafo de dependências:

```text
VS Code / GitHub Action ──processo ou container──> CLI AdrGuard ──> AdrGuard.Core
cliente MCP compatível ───────────────stdio──────> AdrGuard.Mcp ──> AdrGuard.Core
```

`AdrGuard.Core` é uma biblioteca de classes interna, sem pacote próprio. Ela contém parsing, validação, configuração, inventário Git limitado, impacto e drift compartilhados, mas não expõe um produto para consumidores com versionamento independente. Pacotes candidatos da CLI incluem `adr-guard` e `AdrGuard.Core.dll`; pacotes candidatos MCP incluem `adr-guard-mcp` e `AdrGuard.Core.dll` e excluem `adr-guard.dll`. CLI e MCP compartilham assim regras determinísticas, mas preservam executáveis, IDs de pacote, versões, tags de release e workflows de publicação separados. A extração no código-fonte não altera retroativamente os artefatos publicados CLI 1.8.0, MCP 1.0.0, Marketplace 1.1.0 ou Action `@v1`.

Confirme uma CLI instalada com `adr-guard --version` e o `--help` do comando relevante. Confirme versões da extensão no Visual Studio Marketplace e versões MCP tanto no NuGet.org quanto no MCP Registry. Branch, pacote local, VSIX de workflow, manifesto ou nota de release isolados não comprovam publicação em registro.

