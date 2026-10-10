---
title: Crie seu primeiro ADR
description: Tutorial progressivo e verificado desde a identificação da decisão até sua validação e indexação.
sidebar:
  order: 2
---

Um registro de decisão arquitetural é mais útil quando captura uma escolha relevante enquanto o contexto ainda está fresco. Este tutorial usa o formato **Minimal canônico** do ADR Guard e mantém todos os comandos locais.

## 1. Delimite a decisão

Escreva uma frase que nomeie a tensão, não a ferramenta: “Como nossa API deve reduzir leituras repetidas de produtos sem manter preços obsoletos por muito tempo?” Uma decisão merece ADR quando tem consequências técnicas duradouras, trade-offs relevantes ou afeta mais de uma pessoa.

## 2. Escolha um template

Use [o seletor de template](/adr-guard/pt-br/templates/) como heurística. Minimal serve para este piloto focado e reversível. Extended é melhor quando alternativas, motivadores ou riscos pedem mais detalhe. MADR 4.0 é um modo de validação separado, não outro template de geração canônico.

## 3. Inicialize e crie

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard init . --adr-directory docs/adr --template minimal
adr-guard new docs/adr --title "Adotar Redis para cache distribuído" --template minimal --culture pt-BR
```

`init` prepara o diretório configurado de ADRs. `new` reserva um identificador e escreve um registro canônico. Confira o caminho gerado antes de editar.

## 4. Escreva contexto, decisão e consequências

Descreva a pressão atual e as restrições em **Context**. Em **Decision**, declare a escolha com precisão suficiente para orientar a implementação. Em **Consequences**, registre benefícios e custos — inclusive operação, modos de falha e incertezas.

```md
## Context

Leituras repetidas de produtos aumentam a carga no banco. Preços podem ficar obsoletos por no máximo 30 segundos.

## Decision

Usar Redis gerenciado com cache-aside e TTL de 30 segundos para preços.

## Consequences

A latência e a carga devem cair. Precisamos operar Redis, observar a taxa de acerto
e tratar falhas do cache sem bloquear a fonte da verdade.
```

## 5. Revise o raciocínio

Peça às pessoas afetadas que verifiquem premissas, alternativas, segurança, operação e reversibilidade. O ADR Guard verifica estrutura e pode apoiar uma revisão; ele **não** aceita a decisão arquitetural pela equipe.

## 6. Valide

```bash
adr-guard check docs/adr
```

Uma estrutura válida é necessária, mas não prova que a decisão é boa. Corrija os diagnósticos e obtenha a aprovação humana exigida pelo processo da equipe.

## 7. Gere o índice

```bash
adr-guard index docs/adr
```

Revise o índice gerado na mesma alteração. O ADR Guard valida antes de substituí-lo, protegendo o índice anterior contra entradas inválidas.

Confira os novos arquivos antes do commit: `git diff` não exibe arquivos não rastreados. Revise a configuração, a ADR e o índice preparados no staging.

```bash
git status --short -- .adrguard.yml docs/adr
git add -- .adrguard.yml docs/adr
git diff --cached -- .adrguard.yml docs/adr
```

## 8. Integre ao desenvolvimento

Adicione [validação incremental](/adr-guard/pt-br/product/incremental-validation/) localmente ou use a [GitHub Action](/adr-guard/pt-br/product/github-action/) em pull requests. Comece com feedback de validação; adicione políticas ou IA apenas quando houver necessidade clara.

## Próximos passos

- Compare [Minimal, Extended e MADR 4.0](/adr-guard/pt-br/templates/).
- Leia o [exemplo de Redis](/adr-guard/pt-br/examples/redis-cache/).
- Planeje um piloto leve de [adoção em equipe](/adr-guard/pt-br/adoption/).
- Utilize [Agent Skills](/adr-guard/pt-br/skills/) como orientação para o agente; instale a skill e a CLI separadamente.
