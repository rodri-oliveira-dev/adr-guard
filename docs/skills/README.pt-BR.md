# Agent Skills do ADR Guard — Fases P0 + P1

[English](README.md) · [Especificação Agent Skills](https://agentskills.io/specification)

As skills são **fluxos opcionais para agentes de IA** que ajudam a usar o ADR Guard com consistência. Elas não incluem o executável, não substituem a validação determinística e não aprovam decisões.

## Skills disponíveis

| Skill | Quando utilizar |
| --- | --- |
| [adr-guard-init](../../skills/adr-guard-init/SKILL.md) | Adotar ADRs em um repositório |
| [adr-guard-create](../../skills/adr-guard-create/SKILL.md) | Criar uma decisão no estado Proposed |
| [adr-guard-validate](../../skills/adr-guard-validate/SKILL.md) | Validar estrutura, links, IDs e relacionamentos |
| [adr-guard-technical-review](../../skills/adr-guard-technical-review/SKILL.md) | Revisar justificativas, alternativas, riscos e evidências |
| [adr-guard-lifecycle](../../skills/adr-guard-lifecycle/SKILL.md) | Registrar mudanças de status autorizadas e substituições |
| [adr-guard-ci-setup](../../skills/adr-guard-ci-setup/SKILL.md) | Configurar validação no GitHub Actions |

### P1 — Análise e governança de decisões

| Skill | Quando utilizar |
| --- | --- |
| [adr-guard-when-to-record](../../skills/adr-guard-when-to-record/SKILL.md) | Decidir se uma escolha merece um ADR |
| [adr-guard-tradeoff-analysis](../../skills/adr-guard-tradeoff-analysis/SKILL.md) | Comparar alternativas, critérios, riscos e incertezas |
| [adr-guard-supersede](../../skills/adr-guard-supersede/SKILL.md) | Substituir uma decisão preservando seu histórico |
| [adr-guard-audit](../../skills/adr-guard-audit/SKILL.md) | Auditar documentação e governança sem alterar arquivos |
| [adr-guard-team-adoption](../../skills/adr-guard-team-adoption/SKILL.md) | Adotar ADRs com papéis, revisão e responsabilidade humana |

## Qual skill utilizar?

- **Preciso mesmo de um ADR?** `adr-guard-when-to-record`. **Quais opções e consequências considerar?** `adr-guard-tradeoff-analysis`. **Documentar a proposta:** `adr-guard-create`.
- **Preciso substituir uma decisão?** `adr-guard-supersede`, para relações entre registros; `adr-guard-lifecycle` cobre as demais transições autorizadas.
- **Tenho um acervo de ADRs?** `adr-guard-validate` examina regras verificáveis; `adr-guard-audit` avalia qualitativamente documentação e governança sem escrever.
- **Quero implantar a prática no time?** `adr-guard-team-adoption` define a proposta de política, `adr-guard-init` faz a configuração e `adr-guard-ci-setup` integra à CI.

As P1 não pressupõem comandos futuros de análise de impacto ou architectural drift já implementados.

## Descoberta e instalação

```bash
npx skills add rodri-oliveira-dev/adr-guard --list
npx skills add rodri-oliveira-dev/adr-guard --skill adr-guard-create --agent codex
```

Antes do merge, o comando apontado para o repositório padrão não encontrará as skills nesta branch. O teste de descoberta deverá ser repetido após a publicação na `main`. [Skills CLI](https://github.com/vercel-labs/skills).

A instalação das skills **não** instala a ferramenta CLI. Para executar seus comandos:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

## Limites e segurança

- As decisões são humanas: `check` só verifica regras estruturais; `review` orienta, não aprova.
- `new` e `draft` começam em `Proposed`. Não substituir decisões aceitas silenciosamente.
- `draft` e `review` com provider exigem autorização explícita para transmitir arquivos e contexto.
- As skills podem ser instaladas individualmente; não dependem de caminhos relativos para documentação do repositório.
- Revisar o destino antes de escrever, alterar configuração ou substituir um workflow.

## Validação

```bash
python3 scripts/validate-agent-skills.py
python3 -m unittest discover -s tests/agent_skills -p 'test_*.py'
```

O workflow dedicado executa essas verificações. Elas checam estrutura, frontmatter básico e referências locais; não substituem testes de integração com os agentes.
