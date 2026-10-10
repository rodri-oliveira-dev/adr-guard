---
title: Catálogo das 11 Agent Skills
description: Consulte as seis skills essenciais e cinco avançadas do ADR Guard, seus gatilhos, responsabilidades e limites de aprovação humana.
sidebar:
  order: 3
---

O catálogo contém **seis Agent Skills essenciais (P0)** e **cinco avançadas (P1)**. Instale apenas as necessárias para a tarefa. Cada skill pode funcionar individualmente.

As instruções ficam no [repositório ADR Guard](https://github.com/rodri-oliveira-dev/adr-guard/tree/main/skills). Cada entrada descreve um fluxo para o agente, **não um novo comando da CLI**.

## P0 — Fluxos essenciais

### adr-guard-init

**Quando usar:** iniciar a documentação de ADRs em um repositório, selecionar diretórios, formatos e templates.

**Faz:** examina configuração e registros, visualiza `adr-guard init ... --dry-run` e inicializa com autorização. **Não faz:** sobrescrever políticas ou migrar ADRs sem consentimento.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-init/SKILL.md) · [Referência da CLI](../../product/cli/)

### adr-guard-create

**Quando usar:** documentar uma escolha arquitetural relevante.

**Faz:** reúne restrições e alternativas, seleciona Minimal/Extended/Custom canônico, visualiza `adr-guard new` e cria registro **Proposed** quando autorizado. O `draft` com IA exige provedor, modelo e autorização para transmitir contexto. **Não faz:** aceitar decisões nem gerar MADR com `new`.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-create/SKILL.md) · [Guia de criação](../../product/creation/)

### adr-guard-validate

**Quando usar:** validar estrutura, links, relacionamentos e falhas na CI.

**Faz:** executa `adr-guard check`, interpreta diagnósticos e opcionalmente atualiza o índice após validação bem-sucedida. **Não faz:** confundir validade estrutural com aprovação técnica.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-validate/SKILL.md) · [Relatórios](../../product/reports/)

### adr-guard-technical-review

**Quando usar:** avaliar justificativas, alternativas, requisitos não funcionais, riscos, segurança e viabilidade de uma proposta.

**Faz:** apresenta sugestões fundamentadas ou executa `adr-guard review` com provedor, modelo e contexto explicitamente autorizados. **Não faz:** aprovar arquitetura, certificar conformidade ou enviar arquivos silenciosamente a terceiros.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-technical-review/SKILL.md) · [Revisão com IA](../../product/ai-review/)

### adr-guard-lifecycle

**Quando usar:** manter transições de status autorizadas e preservar o histórico.

**Faz:** verifica `Proposed`, `Accepted`, `Deprecated`, `Superseded` e status adicionais configurados; valida links declarados. **Não faz:** presumir aprovação por merge ou sucesso nos testes.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-lifecycle/SKILL.md) · [Ciclo de vida](../../learn/lifecycle/)

### adr-guard-ci-setup

**Quando usar:** configurar validação de ADRs no GitHub Actions.

**Faz:** orienta workflows de `command: check` com privilégio mínimo, verificações obrigatórias e SARIF opcional. **Não faz:** executar revisões privilegiadas com IA em PRs não confiáveis.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-ci-setup/SKILL.md) · [Guia da Action](../../product/github-action/)

## P1 — Análise e governança

### adr-guard-when-to-record

**Quando usar:** avaliar se impacto, reversibilidade, alcance ou risco justificam um ADR.

**Faz:** recomenda ADR, documentação mais simples ou experimento. **Não faz:** exigir um ADR para cada tarefa.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-when-to-record/SKILL.md) · [Quando registrar](../../learn/when-to-write-an-adr/)

### adr-guard-tradeoff-analysis

**Quando usar:** comparar alternativas viáveis antes de propor uma decisão.

**Faz:** organiza critérios, premissas, evidências, alternativas e riscos para apoiar um ADR Extended. **Não faz:** inventar notas, benchmarks ou uma opção vencedora definitiva.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-tradeoff-analysis/SKILL.md) · [Como escrever boas ADRs](../../learn/writing-effective-adrs/)

### adr-guard-supersede

**Quando usar:** substituir uma decisão previamente aceita sem apagar seu histórico.

**Faz:** cria novo registro Proposed, preserva o anterior e declara `Supersedes` / `Superseded by` após aprovação humana. **Não faz:** desativar a decisão anterior enquanto a substituta não estiver aceita.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-supersede/SKILL.md) · [Exemplo de substituição](../examples/)

### adr-guard-audit

**Quando usar:** examinar qualidade documental e lacunas de governança em um acervo de ADRs.

**Faz:** executa `check --format json` em modo somente leitura, analisa evidências e propõe melhorias. **Não faz:** declarar architectural drift na implementação nem alterar status automaticamente.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-audit/SKILL.md) · [Segurança](../security-and-governance/)

### adr-guard-team-adoption

**Quando usar:** implementar a prática de propor, revisar, aceitar e manter ADRs em equipe.

**Faz:** sugere critérios de relevância, responsáveis, templates, piloto e integração à CI. **Não faz:** impor política organizacional sem aprovação das pessoas responsáveis.

[Ler a skill](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/skills/adr-guard-team-adoption/SKILL.md) · [Guia para equipes](../../adoption/)

## Escolha pelo objetivo

- **Antes de escrever:** `when-to-record` → `tradeoff-analysis` → `create`.
- **Melhorar o ADR:** `validate` verifica regras; `technical-review` orienta sobre arquitetura.
- **Substituir uma escolha aceita:** `supersede` com apoio de `lifecycle`.
- **Manter a prática:** `audit`, `team-adoption`, `init` e `ci-setup`.

[Instalação](../installation/) · [Exemplos práticos](../examples/) · [Limites de governança](../security-and-governance/)
