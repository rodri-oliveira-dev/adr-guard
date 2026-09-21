# Contrato de templates ADR personalizados

Este documento especifica o formato Markdown offline, tratado exclusivamente como dados, implementado na issue #54. A infraestrutura de seleção está disponível internamente na branch de desenvolvimento. A opção pública `adr-guard new --template-file` será entregue na #51, e a seleção opcional em `draft` na #57; **essas opções ainda não existem na CLI v1**.

## Origem e seleção

Os modelos internos possuem identificadores estáveis `minimal` (padrão, quando nenhuma origem é informada) e `extended`. Um template personalizado é um único arquivo Markdown `.md` local, selecionado explicitamente. `--template <nome>` e `--template-file <caminho>` são mutuamente exclusivos, inclusive quando o nome é `minimal`. Nomes desconhecidos e opções conflitantes serão erros de uso nos comandos consumidores; arquivos ausentes, inacessíveis, grandes demais ou malformados são rejeitados antes da persistência da ADR.

Um caminho relativo é resolvido a partir do diretório de trabalho da invocação, **não** do diretório de saída das ADRs. Mantenha os templates fora do diretório de ADRs selecionado: `check` e a criação validam os arquivos Markdown presentes nesse diretório, não os arquivos de template. Nenhum outro arquivo é descoberto ou lido recursivamente.

O arquivo precisa conter UTF-8 válido, com no máximo **65.536 bytes**, incluindo um BOM UTF-8 opcional. As quebras de linha são normalizadas para LF. Não há acesso a provedores, rede nem avaliação de código.

## Estrutura Markdown exata

```markdown
# {{title}}

## Status

{{status}}

## Context

{{guidance-context}}

[EDITAR: Descreva o problema da ADR {{id}}.]

{{context}}

## Decision

{{guidance-decision}}

[EDITAR: Descreva a abordagem proposta.]

{{decision}}

## Consequences

{{guidance-consequences}}

[EDITAR: Registre o impacto esperado.]

{{consequences}}
```

A primeira linha não vazia deve ser **exatamente** `# {{title}}`. A primeira seção de nível dois deve ser **exatamente** `## Status`, com conteúdo `{{status}}` ou `Proposed`; o renderizador sempre produz o status canônico `Proposed`. Devem existir as seções não vazias `## Context`, `## Decision` e `## Consequences`. São permitidas seções adicionais com títulos literais, desde que não dupliquem títulos canônicos ou o Status. Não insira títulos H1/H2 nos corpos das seções; o renderizador controla a estrutura final. A saída precisa passar pelo parser e validador existentes sem mudar as regras de validação.

O documento gerado é um ponto de partida editável pelo autor, **não** uma decisão arquitetural aprovada. Substitua as instruções e revise a ADR antes de alterar manualmente seu status `Proposed`.

## Regras dos placeholders

Os identificadores são exatos, distinguem maiúsculas de minúsculas e usam `{{nome}}`:

- `{{title}}`: título de uma linha, com caracteres especiais Markdown escapados. O renderizador controla o H1.
- `{{id}}`: ID numérico alocado, com quatro dígitos (por exemplo, `0042`); o consumidor deve fornecer um ID válido de 1 a 9999.
- `{{status}}`: `Proposed` invariável; não é permitido sobrescrevê-lo.
- `{{context}}`, `{{decision}}`, `{{consequences}}`: substituições opcionais inseridas uma única vez. O conteúdo do usuário não pode adicionar H1/H2 fora de blocos de código Markdown, e o validador verifica a ADR final.
- `{{guidance-context}}`, `{{guidance-decision}}`, `{{guidance-consequences}}`: orientação interna localizada para `en-US` e `pt-BR`; esses campos são reservados.

Tokens desconhecidos, delimitadores malformados/aninhados e formas alternativas como `{title}`, `${title}` ou `{% expression %}` são rejeitados. Os valores são inseridos **uma única vez**, sem nova interpretação como template ou execução de shell/código. Texto semelhante a comandos permanece texto Markdown. O template não escolhe o caminho de saída nem altera alocação de IDs, nomes de arquivo, diretório de destino, proibição de sobrescrita ou persistência atômica.

**Concorrência:** se outro processo criar uma ADR e o ID alocado mudar, o comando consumidor deverá renderizar novamente `{{id}}` com o ID efetivamente persistido, sob o bloqueio compartilhado de criação. Essa integração pertence à #51; persistir o conteúdo da prévia com ID antigo seria incorreto.

## Exemplos e testes

Veja `tests/AdrGuard.Tests/Fixtures/Templates/custom-template.en-US.md` e `custom-template.pt-BR.md`. Os testes verificam também UTF-8, tamanho, resolução do caminho, validação estrutural, segurança dos placeholders e integração com `check`.
