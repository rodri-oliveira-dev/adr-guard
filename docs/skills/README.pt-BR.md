# Agent Skills do ADR Guard — Fase P0

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
