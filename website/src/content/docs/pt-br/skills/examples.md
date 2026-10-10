---
title: Exemplos práticos de Agent Skills
description: Siga exemplos e comandos do ADR Guard para propor, revisar, substituir, auditar e institucionalizar decisões arquiteturais.
sidebar:
  order: 4
---

Estes são **exemplos de tarefas** e não comandos executados ao visitar a página. Utilize-os em um agente compatível após revisar a skill e autorizar eventuais alterações.

## Cenário 1 — Precisamos de um novo ADR?

**Prompt para o agente:**

> Avalie se migrar chamadas síncronas de checkout para eventos assíncronos precisa de um ADR. Considere limites do sistema, consistência, reversibilidade, operação e riscos. Se a escolha ainda não estiver madura, liste as evidências necessárias. Não altere arquivos.

**Skills:** [`adr-guard-when-to-record`](../catalog/#adr-guard-when-to-record) e [`adr-guard-tradeoff-analysis`](../catalog/#adr-guard-tradeoff-analysis).

**Resultado esperado:** avaliação da relevância, matriz de alternativas, restrições conhecidas, medições ausentes e responsáveis pela revisão. O agente não deve presumir que mensageria é sempre superior.

Caso o ADR faça sentido, utilize o [guia de criação canônica](../../product/creation/).

## Cenário 2 — Criar e validar uma proposta

**Prompt para o agente:**

> Ajude a criar um ADR Extended sobre nossa estratégia de cache. Examine decisões existentes, registre alternativas reais e custos operacionais, e visualize o arquivo. Só escreva após minha aprovação. Nunca marque a decisão como Accepted.

**Skill:** [`adr-guard-create`](../catalog/#adr-guard-create).

**Fluxo da CLI** (com `docs/adr` já existente):

```bash
adr-guard new docs/adr --title "Usar Redis gerenciado como cache" --template extended --culture pt-BR --preview
# Após confirmação humana:
adr-guard new docs/adr --title "Usar Redis gerenciado como cache" --template extended --culture pt-BR
# Edite a proposta com contexto, alternativas e consequências reais.
adr-guard check docs/adr
adr-guard index docs/adr
```

**Resultado esperado:** um ADR `Proposed` e índice gerado apenas depois da validação. O preview **não reserva** o identificador; confira o arquivo efetivamente criado.

Veja os [exemplos de formatos com Redis](/adr-guard/pt-br/examples/redis-cache/).

## Cenário 3 — Revisar sem aprovar automaticamente

**Prompt para o agente:**

> Revise o ADR 0007 quanto a justificativas, alternativas, requisitos não funcionais, segurança, operação, migração e critérios verificáveis. Cite evidências e diferencie lacunas de violações. Não altere status nem envie arquivos a provedores de IA sem meu consentimento.

**Skills:** [`adr-guard-validate`](../catalog/#adr-guard-validate) e [`adr-guard-technical-review`](../catalog/#adr-guard-technical-review).

Validação estrutural offline:

```bash
adr-guard check docs/adr --format json
```

Revisão com provedor, apenas quando autorizada:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai --model MODELO_ESCOLHIDO \
  --policy advisory --format json
```

O arquivo do exemplo precisa existir, e o modelo deve ser definido pelo usuário. O relatório é consultivo; exit code `0` **não** significa aprovação. Consulte [revisão e privacidade](../../product/ai-review/).

## Cenário 4 — Substituir uma decisão aceita

**Prompt para o agente:**

> Nosso ADR 0007 aceito deixou de atender ao contexto. Estruture uma nova decisão, crie uma proposta de substituição com links explícitos e mantenha o ADR antigo como Accepted até a aprovação humana. Preserve o raciocínio histórico.

**Skill:** [`adr-guard-supersede`](../catalog/#adr-guard-supersede).

A nova proposta pode declarar:

```md
## Supersedes
[ADR 0007](0007-old-approach.md)
```

**Somente depois da aprovação humana**, altere o registro anterior para `Superseded`, com `## Superseded by` apontando para a nova decisão aceita. Utilize os **nomes reais**, execute `adr-guard check docs/adr` e então `adr-guard index docs/adr`. Veja o [exemplo completo de substituição](/adr-guard/pt-br/examples/supersession/).

## Cenário 5 — Auditar e apoiar a adoção

**Prompt para o agente:**

> Faça uma auditoria somente leitura do diretório de ADRs. Separe falhas determinísticas de possíveis premissas obsoletas ou falta de responsáveis. Cite evidências e proponha prioridades. Não altere arquivos nem alegue divergência da implementação sem provas.

**Skill:** [`adr-guard-audit`](../catalog/#adr-guard-audit).

```bash
adr-guard check docs/adr --format json
```

Em seguida, [`adr-guard-team-adoption`](../catalog/#adr-guard-team-adoption) pode propor revisores, autoridade decisória, templates, piloto e política de preservação do histórico.

## Próximos passos

[Instalação](../installation/) · [Todas as 11 skills](../catalog/) · [Segurança e governança](../security-and-governance/)
