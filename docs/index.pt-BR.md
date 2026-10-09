# Documentação do ADR Guard

[English](index.md) · [README do projeto](../README.pt-BR.md)

Architecture Decision Records (ADRs), ou Registros de Decisão de Arquitetura, preservam o contexto, a escolha e as consequências de decisões que moldam um sistema de software. O ADR Guard ajuda equipes a manter esses registros estruturalmente consistentes, conectados e revisáveis. Ele valida a documentação; as pessoas continuam responsáveis pela decisão arquitetural.

## Escolha seu caminho

| Quero... | Comece aqui |
| --- | --- |
| Entender ADRs pela primeira vez | [O que é uma ADR?](fundamentals/what-is-an-adr.pt-BR.md), depois [por que equipes usam ADRs](fundamentals/why-use-adrs.pt-BR.md) |
| Decidir se um assunto merece uma ADR | [Quando escrever uma ADR](fundamentals/when-to-write-an-adr.pt-BR.md) |
| Criar e validar meu primeiro registro | [Escreva sua primeira ADR](guides/getting-started.pt-BR.md) |
| Escolher Minimal, Extended, Custom ou MADR 4.0 | [Escolha um template ou formato](decision-design/choosing-a-template.pt-BR.md) |
| Estabelecer a prática em uma equipe | [Adote ADRs em equipe](guides/team-adoption.pt-BR.md) |
| Encontrar contratos de comandos e integrações | [Referência técnica](#referência-técnica) |

## Aprenda a prática

### Fundamentos

- [O que é uma ADR?](fundamentals/what-is-an-adr.pt-BR.md) — o problema, o registro e um pequeno exemplo.
- [Por que usar ADRs?](fundamentals/why-use-adrs.pt-BR.md) — benefícios, impacto organizacional e limitações.
- [Quando escrever uma ADR](fundamentals/when-to-write-an-adr.pt-BR.md) — critérios práticos e quando não criar uma.
- [Ciclo de vida](fundamentals/lifecycle.pt-BR.md) — propor, revisar, aceitar, descontinuar e substituir.
- [Glossário e referências](fundamentals/glossary.pt-BR.md) — vocabulário comum e fontes reconhecidas.

### Projete um registro útil

- [Categorias de decisão](decision-design/decision-categories.pt-BR.md) — arquitetura, dados, segurança, operações e outros domínios.
- [Formatos e templates](decision-design/formats-and-templates.pt-BR.md) — diferença entre o assunto da decisão e a forma do documento.
- [Guia de seleção](decision-design/choosing-a-template.pt-BR.md) — comparação entre Minimal, Extended, Custom e MADR 4.0.
- [Como escrever boas ADRs](decision-design/writing-effective-adrs.pt-BR.md) — evidências, opções, trade-offs e resultados claros.
- [Anti-patterns](decision-design/anti-patterns.pt-BR.md) — justificativa fraca, custos ocultos e reescrita silenciosa do histórico.

### Coloque em prática

- [Escreva sua primeira ADR](guides/getting-started.pt-BR.md) — tutorial completo para iniciantes.
- [Adote ADRs em equipe](guides/team-adoption.pt-BR.md) — responsabilidade, revisão, governança e adoção gradual.
- [Exemplos completos](examples/README.pt-BR.md) — uma decisão realista nos formatos Minimal, Extended e MADR 4.0.

## Referência técnica

| Área | Documentação |
| --- | --- |
| CLI e configuração | [Referência da CLI](cli-reference.pt-BR.md), [criação offline](creation.pt-BR.md), [templates personalizados](custom-templates.pt-BR.md) |
| Modos de validação | [MADR 4.0](madr-4.pt-BR.md), [validação incremental e baselines](incremental-validation.pt-BR.md), [governança de relacionamentos](relationship-governance.pt-BR.md) |
| Relatórios | [relatórios de check](check-reports.pt-BR.md), [schema do relatório de review](adr-review-report-v1.md), [política de review](adr-review-policy-v1.pt-BR.md) |
| Fluxos assistidos por IA | [templates e privacidade do draft](draft-templates.pt-BR.md), [review de ADRs](adr-review.pt-BR.md), [review comparativo](comparative-review.pt-BR.md), [segurança do review](adr-review-security.pt-BR.md) |
| Integrações | [GitHub Action](github-action.pt-BR.md), [VS Code](../extensions/vscode/README.pt-BR.md), [imagens de container](container.pt-BR.md) |
| Release e cadeia de suprimentos | [política de release da Action](github-action-release.pt-BR.md), [segurança da Action](github-action-security.pt-BR.md), [auditoria da release pública](public-release-audit.pt-BR.md) |
| Arquitetura do projeto | [índice das ADRs do próprio ADR Guard](adr/README.md) |

Notas específicas de versões permanecem em [`docs/releases`](releases/). Consulte as [Releases do GitHub](https://github.com/rodri-oliveira-dev/adr-guard/releases) para a versão publicada mais recente.
