# ADR Guard

[![CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/RodriOliveira.AdrGuard.svg)](https://www.nuget.org/packages/RodriOliveira.AdrGuard)
[![VS Code Marketplace](https://img.shields.io/visual-studio-marketplace/v/rodrioliveira.adr-guard?label=VS%20Code%20Marketplace)](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard)
[![GitHub Release](https://img.shields.io/github/v/release/rodri-oliveira-dev/adr-guard)](https://github.com/rodri-oliveira-dev/adr-guard/releases)
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
- oferece escrita e revisão técnica opcionais com IA, revisão humana, providers e contexto explícitos;
- integra-se a GitHub Actions, VS Code e imagens versionadas de container.

A validação confirma regras determinísticas da documentação. Ela **não** aprova mérito técnico, autoriza uma decisão nem transforma saída de IA em arquitetura aceita.

## Instalação

Instale a .NET Tool publicada no NuGet.org:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

Atualize uma instalação com `dotnet tool update --global RodriOliveira.AdrGuard`. GitHub Packages também está disponível como registro secundário autenticado. Consulte as [Releases do GitHub](https://github.com/rodri-oliveira-dev/adr-guard/releases) ou o badge do NuGet para a versão publicada atual.

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
| GitHub Action | Validar, indexar ou executar review explícito com IA em runners Linux compatíveis | [Guia de consumo](docs/github-action.pt-BR.md) |
| VS Code | Comandos, diagnósticos em Problems e ADR Explorer por uma CLI compatível instalada | [Guia da extensão](extensions/vscode/README.pt-BR.md) |
| Containers | Executar a mesma CLI versionada por GHCR ou Docker Hub | [Guia de container e cadeia de suprimentos](docs/container.pt-BR.md) |
| Relatórios de CI | Produzir validação em texto, JSON ou SARIF | [Relatórios de check](docs/check-reports.pt-BR.md) |

A [Action no GitHub Marketplace](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) e a [extensão no Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=rodrioliveira.adr-guard) estão publicadas. A tag móvel `@v1` está publicada e suporta `check`, `index` e `review` opcional desde a v1.1.6; `new` e `draft` continuam fluxos da CLI/container direto. A extensão não inclui a CLI.

## Documentação

- **Aprenda:** [fundamentos](docs/index.pt-BR.md#aprenda-a-prática), [categorias](docs/decision-design/decision-categories.pt-BR.md), [ciclo de vida](docs/fundamentals/lifecycle.pt-BR.md) e [anti-patterns](docs/decision-design/anti-patterns.pt-BR.md).
- **Use:** [primeira ADR](docs/guides/getting-started.pt-BR.md), [adoção em equipe](docs/guides/team-adoption.pt-BR.md) e [criação offline](docs/creation.pt-BR.md).
- **Consulte:** [CLI e configuração](docs/cli-reference.pt-BR.md), [validação e governança](docs/relationship-governance.pt-BR.md) e [índice técnico completo](docs/index.pt-BR.md#referência-técnica).
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
