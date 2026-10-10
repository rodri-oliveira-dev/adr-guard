---
title: Instalar Agent Skills do ADR Guard
description: Aprenda a listar e instalar skills com o Skills CLI, configurar a CLI .NET independente e verificar um fluxo seguro.
sidebar:
  order: 2
---

As Agent Skills e a CLI .NET do ADR Guard são **instalações independentes**. A skill orienta o agente; a CLI executa operações determinísticas e valida a documentação. É possível discutir decisões sem a ferramenta, mas o agente não pode afirmar que validou arquivos sem executá-la.

## Pré-requisitos

- Um agente compatível com Agent Skills, como o Codex.
- Node.js e `npx` para executar o [Skills CLI](https://github.com/vercel-labs/skills). Verifique as políticas da sua organização antes de executar pacotes com `npx`.
- A [.NET Tool do ADR Guard](../../product/cli/) para tarefas que dependem de comandos. Revisão ou escrita com provedores de IA são opcionais e exigem provedor, modelo e credenciais específicos.

## 1. Descubra as skills

Execute no **repositório que vai consumir** as skills:

```bash
npx skills add rodri-oliveira-dev/adr-guard --list
```

Leia as instruções antes de instalar: uma skill contém orientações executáveis pelo agente, e seu conteúdo deve ser tratado com o mesmo cuidado de outras dependências.

**Atenção à branch:** o comando acima descobre skills da branch padrão. Ao revisar alterações ainda não incorporadas, consulte diretamente a branch da proposta, sem presumir que já estão disponíveis na listagem padrão.

## 2. Instale somente o necessário

Para criar ADRs usando o Codex:

```bash
npx skills add rodri-oliveira-dev/adr-guard --skill adr-guard-create --agent codex
```

Para comparar alternativas:

```bash
npx skills add rodri-oliveira-dev/adr-guard --skill adr-guard-tradeoff-analysis --agent codex
```

O [catálogo](../catalog/) descreve as 11 skills. Para outros agentes, confirme no help atual do Skills CLI os identificadores e modos suportados. Evite instalar todas sem necessidade.

## 3. Instale o ADR Guard separadamente

Se a tarefa utilizar `init`, `new`, `check`, `index` ou `review`, instale a .NET Tool e confira sua versão:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard --version
```

Atualização de uma instalação global existente:

```bash
dotnet tool update --global RodriOliveira.AdrGuard
```

Não instale ferramentas ou altere o repositório sem autorização. Equipes podem adotar ferramentas locais com versões fixadas.

## 4. Experimente uma tarefa segura

Peça ao agente:

> Analise este repositório e explique se a escolha do Redis como cache distribuído precisa de um ADR. Liste restrições conhecidas, dúvidas, alternativas e pessoas que devem revisar. Ainda não altere arquivos.

Se decidir documentar, autorize o agente a visualizar uma proposta antes de escrever:

```bash
adr-guard new docs/adr --title "Usar Redis para cache" --template extended --culture pt-BR --preview
```

O diretório de ADRs deve existir. Com autorização, o agente pode executar `new`, `check` e `index`; o registro começa como **Proposed**.

## Solução de problemas

| Problema | O que verificar |
| --- | --- |
| `--list` não encontra as skills | Elas já foram incorporadas à branch padrão? |
| A skill é reconhecida, mas o comando `adr-guard` não existe | Instale ou restaure a .NET Tool independente e execute `adr-guard --version`. |
| A CLI rejeita um ADR | Verifique `.adrguard.yml`, seções canônicas, formato e diagnósticos reais; veja [validação](../catalog/#adr-guard-validate). |
| A revisão com IA pede credenciais | Defina provedor e modelo e autorize o envio de contexto. Não há escolha automática de provedor. |
| MADR 4.0 não corresponde ao template gerado | MADR é escrito separadamente e validado com `--adr-format madr-4`; `new` não gera arquivos MADR. |

Próximos passos: [catálogo](../catalog/), [exemplos](../examples/) e [limites de segurança](../security-and-governance/).
