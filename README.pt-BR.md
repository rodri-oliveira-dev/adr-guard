# ADR Guard

[![CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml)
[![CLI NuGet](https://img.shields.io/nuget/v/RodriOliveira.AdrGuard.svg?label=CLI%20NuGet)](https://www.nuget.org/packages/RodriOliveira.AdrGuard)
[![MCP NuGet](https://img.shields.io/nuget/v/RodriOliveira.AdrGuard.Mcp.svg?label=MCP%20NuGet)](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp)
[![VS Code Marketplace](https://img.shields.io/visual-studio-marketplace/v/rodrioliveira.adr-guard?label=VS%20Code%20Marketplace)](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard)
[![Release da CLI no GitHub](https://img.shields.io/github/v/release/rodri-oliveira-dev/adr-guard?filter=v*&sort=semver&label=release%20da%20CLI)](https://github.com/rodri-oliveira-dev/adr-guard/releases?q=v1.8.0)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
[![License](https://img.shields.io/github/license/rodri-oliveira-dev/adr-guard)](LICENSE)

[English](README.md) · [Portal de documentação](https://rodri-oliveira-dev.github.io/adr-guard/pt-br/) · [Documentação em Markdown](docs/index.pt-BR.md) · [Changelog](CHANGELOG.md)

**Entenda decisões arquiteturais. Registre a justificativa. Preserve um histórico confiável.**

ADR Guard é uma ferramenta de linha de comando .NET leve para criar, validar, revisar e indexar Architecture Decision Records (ADRs). Ela mantém a documentação de decisões consistente no desenvolvimento local e em CI sem tomar decisões arquiteturais pela equipe.

## O que é uma ADR?

Uma ADR é um documento curto que explica uma decisão relevante de software: o contexto que a exigiu, a opção escolhida e os benefícios e custos resultantes. ADRs ajudam futuros colaboradores a entender **por que** um sistema ganhou determinada forma, não apenas o que o código faz.

Está começando? Siga o guia progressivo:

1. [O que é uma ADR?](docs/fundamentals/what-is-an-adr.pt-BR.md)
2. [Por que e quando uma equipe deve usar ADRs?](docs/fundamentals/why-use-adrs.pt-BR.md)
3. [Escreva e valide sua primeira ADR](docs/guides/getting-started.pt-BR.md)

## O que o ADR Guard faz

- cria ADRs `Proposed` offline a partir de templates Minimal, Extended ou Custom com restrições;
- valida estrutura canônica, nomes, status, seções, links, IDs e relacionamentos de substituição;
- suporta validação MADR 4.0 separada e opcional;
- gera um índice Markdown determinístico somente depois da validação;
- oferece checks incrementais com Git, baselines de diagnóstico e relatórios JSON/SARIF;
- oferece [análise consultiva de impacto arquitetural](docs/guides/architecture-impact-analysis.pt-BR.md), opcional e baseada em mapeamentos ADR-código explícitos;
- verifica [drift arquitetural e evidências de fitness](docs/guides/architectural-drift.pt-BR.md) de forma opcional, sem avaliar MSBuild nem tratar evidência ausente como conformidade;
- oferece escrita e revisão técnica opcionais com IA, revisão humana, providers e contexto explícitos;
- integra-se a GitHub Actions, VS Code, imagens versionadas de container e um servidor MCP opcional empacotado de forma independente para agentes compatíveis.

A validação confirma regras determinísticas da documentação. Ela **não** aprova mérito técnico, autoriza uma decisão nem transforma saída de IA em arquitetura aceita.

## Instalação

Instale a .NET Tool publicada no NuGet.org:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

Atualize uma instalação com `dotnet tool update --global RodriOliveira.AdrGuard`. O GitHub Packages também está disponível como registro secundário autenticado para a CLI. O [pacote MCP 1.0.0](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.0) independente está publicado no NuGet.org; consulte a [distribuição do MCP](docs/mcp-release.pt-BR.md) para saber sobre o GitHub Packages opcional e a publicação retroativa segura da versão existente. Consulte as [Releases do GitHub](https://github.com/rodri-oliveira-dev/adr-guard/releases) ou o badge do NuGet para a versão atual da CLI.

## Início rápido

Na raiz de um repositório existente:

```bash
adr-guard init . --adr-directory docs/adr --template minimal
adr-guard new docs/adr --title "Usar PostgreSQL como sistema de registro de pedidos" --culture pt-BR
```

Edite a ADR `Proposed` gerada e substitua as instruções por contexto, decisão e consequências equilibradas reais. Revise-a com as pessoas afetadas e execute:

```bash
adr-guard check docs/adr
adr-guard index docs/adr
```

`check` valida o conjunto. `index` valida novamente e cria ou atualiza `docs/adr/README.md`; `new` não atualiza o índice automaticamente. Leia o [tutorial da primeira ADR](docs/guides/getting-started.pt-BR.md) para o fluxo completo.

## Escolha a estrutura certa

| Opção | Quando usar | Limite do produto |
| --- | --- | --- |
| Minimal | Contexto, escolha, justificativa e consequências centrais bastam | Gerado por `new`/`draft`; validação canônica |
| Extended | Opções, direcionadores, riscos ou impacto entre equipes precisam estar explícitos | Gerado por `new`/`draft`; validação canônica; **não é MADR** |
| Custom | A equipe precisa de perguntas ou seções canônicas adicionais | Um arquivo local estrito via `--template-file`; não aceita Markdown arbitrário |
| MADR 4.0 | A equipe escolhe deliberadamente a estrutura externa MADR | Escrito separadamente; `--adr-format madr-4` opcional; sem gerador MADR no `new` |

Consulte a [matriz de seleção](docs/decision-design/choosing-a-template.pt-BR.md), os [exemplos completos](docs/examples/README.pt-BR.md), o [contrato Custom](docs/custom-templates.pt-BR.md) e o [guia MADR](docs/madr-4.pt-BR.md).

## Integrações

| Integração | Uso | Guia |
| --- | --- | --- |
| GitHub Action | Validar, indexar ou executar review/análise de impacto arquitetural explícitos em runners Linux compatíveis | [Guia de consumo](docs/github-action.pt-BR.md) |
| VS Code | Gestão visual de ADRs, fluxos de arquitetura/governança, comandos, diagnósticos em Problems e ADR Explorer por uma CLI compatível instalada | [Guia da extensão](extensions/vscode/README.pt-BR.md) |
| Servidor MCP | Oferecer a agentes compatíveis acesso estruturado e somente leitura a descoberta, validação, impacto e drift | [Visão geral do MCP](docs/mcp.pt-BR.md) |
| Containers | Executar a mesma CLI versionada por GHCR ou Docker Hub | [Guia de container e cadeia de suprimentos](docs/container.pt-BR.md) |
| Relatórios de CI | Produzir validação em texto, JSON ou SARIF | [Relatórios de check](docs/check-reports.pt-BR.md) |
| Agent Skills | Oferecer instruções portáveis a agentes para fluxos de ADR e CLI controlados por pessoas | [Guia de Skills](docs/skills/README.pt-BR.md) |

### Arquitetura interna de runtime

A CLI e o servidor MCP são executáveis separados que referenciam a mesma biblioteca de classes interna `AdrGuard.Core`. O Core contém o comportamento determinístico compartilhado de parsing, validação, configuração, inventário Git, impacto e drift; ele não depende de nenhum executável e não é um produto ou pacote NuGet independente. Pacotes e containers gerados deste código-fonte após a extração levam a CLI e o Core, enquanto pacotes MCP versionados independentemente e gerados deste código-fonte levam MCP e Core sem incluir o executável da CLI. Essa mudança no código-fonte não altera os artefatos já publicados da CLI `1.8.0` ou do MCP `1.0.0`. VS Code e GitHub Action continuam sendo adaptadores do contrato da CLI e não reimplementam regras do Core. Consulte a [ADR 0008](docs/adr/0008-extract-an-internal-shared-core.md) e a [matriz de compatibilidade](docs/compatibility.pt-BR.md).

Situação das releases (verificada em 18/10/2026): a CLI `1.8.0` está publicada no [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard/1.8.0) e em sua [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.8.0). A extensão VS Code `1.0.1` está publicada no [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard); a versão `1.1.0` continua sendo uma candidata em código-fonte/VSIX. O MCP `1.0.0` está publicado de forma independente no [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.0), em sua [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/mcp/v1.0.0) e no MCP Registry. Consulte a [matriz de compatibilidade](docs/compatibility.pt-BR.md) para os limites exatos entre componentes.

A [extensão ADR Guard para VS Code publicada](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard) inclui listagem visual de ADRs, criação orientada, edição e configurações. Essas telas exigem os contratos de formulário da CLI 1.7.0. As versões da extensão e da CLI permanecem independentes.

![Listagem visual do ADR Guard no Extension Development Host do VS Code](website/public/images/vscode/adr-list.png)

Consulte o [código-fonte e guia de instalação da extensão](extensions/vscode/README.pt-BR.md) e o [Guia Visual do VS Code](https://rodri-oliveira-dev.github.io/adr-guard/pt-br/product/vscode-visual-guide/) no portal para o passo a passo completo da interface.

A criação e a edição estruturadas de ADRs estão implementadas para clientes visuais confiáveis, como a extensão VS Code. Uma CLI compatível da linha 1.7.0 expõe `new <diretório> --form` e `edit <arquivo> --form` como contratos JSON versionados por stdin/stdout; eles não são formulários interativos do terminal. O fluxo tradicional `new --title` continua sendo o caminho voltado a pessoas na linha de comando, inclusive para templates personalizados com restrições. Consulte o [guia de criação](docs/creation.pt-BR.md), a [referência dos contratos da CLI](docs/cli-reference.pt-BR.md) e o [guia da extensão](extensions/vscode/README.pt-BR.md). Confirme que as versões da CLI e da extensão que oferecem os formulários visuais foram publicadas antes de depender delas.

A [Action no GitHub Marketplace](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) e a [extensão no Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard) estão publicadas. A tag móvel `@v1` está publicada e suporta `check`, `index`, `review` opcional desde a v1.1.6 e `impact` opcional desde a v1.5.0. Na v1.6.0, `drift` é disponibilizado por meio da CLI/container direto, assim como `new` e `draft`; não é um modo da Action composta. A extensão não inclui a CLI.

A release independente MCP `1.0.0` pode ser instalada com `dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0`. Seu backfill autenticado para GitHub Packages continua sendo uma etapa manual de distribuição; NuGet.org é a fonte recomendada. Consulte a [configuração do MCP](docs/mcp-configuration.pt-BR.md), as [ferramentas disponíveis](docs/mcp-tools.pt-BR.md), a [integração com VS Code](extensions/vscode/README.pt-BR.md#integração-mcp) e [segurança e permissões](docs/mcp-security.pt-BR.md).

## Agent Skills (P0 + P1)

Onze [Agent Skills](docs/skills/README.pt-BR.md) portáveis orientam a inicialização, criação, validação, revisão técnica, manutenção do ciclo de vida, configuração de CI, decisão sobre quando registrar, análise de trade-offs, substituição de ADRs, auditoria e adoção em equipe. Elas não instalam a CLI nem aprovam decisões arquiteturais.

```bash
npx skills add rodri-oliveira-dev/adr-guard --list
npx skills add rodri-oliveira-dev/adr-guard --skill adr-guard-create --agent codex
```

As onze Agent Skills já estão disponíveis na branch padrão. Consulte [instalação, segurança e escopo](docs/skills/README.pt-BR.md) para escolher e instalar apenas os fluxos necessários.

## Documentação

- **Aprenda:** [fundamentos](docs/index.pt-BR.md#aprenda-a-prática), [categorias](docs/decision-design/decision-categories.pt-BR.md), [ciclo de vida](docs/fundamentals/lifecycle.pt-BR.md) e [anti-patterns](docs/decision-design/anti-patterns.pt-BR.md).
- **Use:** [primeira ADR](docs/guides/getting-started.pt-BR.md), [adoção em equipe](docs/guides/team-adoption.pt-BR.md) e [criação offline](docs/creation.pt-BR.md).
- **Consulte:** [CLI e configuração](docs/cli-reference.pt-BR.md), [matriz de compatibilidade](docs/compatibility.pt-BR.md), [validação e governança](docs/relationship-governance.pt-BR.md) e [índice técnico completo](docs/index.pt-BR.md#referência-técnica).
- **Análise de arquitetura:** [análise de impacto](docs/guides/architecture-impact-analysis.pt-BR.md), [drift arquitetural](docs/guides/architectural-drift.pt-BR.md), [baselines de diagnóstico](docs/incremental-validation.pt-BR.md) e [review por IA](docs/adr-review.pt-BR.md).
- **MCP:** [visão geral](docs/mcp.pt-BR.md), [configuração](docs/mcp-configuration.pt-BR.md), [contratos das ferramentas](docs/mcp-tools.pt-BR.md) e [segurança](docs/mcp-security.pt-BR.md).
- **Limites de IA:** o review da CLI está publicado desde a v1.1.2 e é executado explicitamente como `adr-guard review`; o enforcement determinístico pode retornar exit code `4`. Consulte o [guia de review por IA](docs/adr-review.pt-BR.md), a [privacidade do draft](docs/draft-templates.pt-BR.md) e a [segurança do review](docs/adr-review-security.pt-BR.md).
- **Arquitetura do projeto:** [índice gerado das ADRs do ADR Guard](docs/adr/README.md).

Inglês é o idioma padrão. Páginas em português brasileiro usam `.pt-BR.md` e cada tradução oferece link recíproco.

## Build e contribuição

O projeto usa .NET 10. Para compilar e testar a partir do código:

```bash
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --no-restore
dotnet test AdrGuard.slnx --no-build
```

Issues e pull requests focados são bem-vindos. Use o [issue tracker](https://github.com/rodri-oliveira-dev/adr-guard/issues) para propostas e bugs reproduzíveis, [SUPPORT.md](SUPPORT.md) para suporte e [SECURITY.md](SECURITY.md) para relatar vulnerabilidades de forma privada.

## Releases e licença

Artefatos publicados e notas exatas estão nas [Releases do GitHub](https://github.com/rodri-oliveira-dev/adr-guard/releases). As notas versionadas do repositório permanecem em [`docs/releases`](docs/releases/).

ADR Guard é distribuído sob a [Licença MIT](LICENSE).
