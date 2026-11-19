# Contrato v1 de exportação de relacionamentos ADR

[English](adr-relationships-export-v1.md)

Este documento congela o contrato de intercâmbio JSON preparado para integração com o Align CLI. O Core produz esses dados determinísticos, e tanto a ferramenta de relacionamentos do MCP quanto o adaptador CLI em estágio de código-fonte reutilizam essa implementação sem alterar o contrato MCP. O comando CLI está implementado no repositório, mas ainda não faz parte de uma release publicada.

A definição legível por máquina autoritativa é [`adr-relationships-export-v1.schema.json`](schemas/adr-relationships-export-v1.schema.json). As fixtures representativas de integração estão em [`docs/examples/adr-relationships-export-v1`](examples/adr-relationships-export-v1/).

## Exportação pela CLI

```text
adr-guard export relationships docs/adr
adr-guard export relationships docs/adr --output artifacts/relationships.json
```

O destino padrão é `<raiz-do-repositório>/.adr-guard/relationships.json`. Um destino personalizado é relativo à raiz detectada do repositório, usa separadores portáveis `/`, deve usar a extensão `.json`, permanecer dentro dessa raiz e não pode atravessar links simbólicos nem reparse points. Diretórios de destino ausentes são criados. O documento UTF-8 completo é gravado em um arquivo temporário no diretório de destino e depois promovido atomicamente sobre qualquer artefato existente; uma falha de escrita preserva o artefato anterior.

O comando retorna `0` sempre que grava o artefato com segurança, inclusive quando `issues` não está vazio. Sintaxe ou configuração inválida retorna `2`; falhas de entrada, caminho, segurança, cancelamento ou escrita retornam `3`. Ele não edita ADRs ou índices nem modifica configuração ou estado do Git, e não exige servidor MCP, provedor de modelo, credenciais ou conexão de rede.

O SHA de commit opcional `generatedFrom` foi deliberadamente omitido do schema `1.0`. Ler estado Git implícito acrescentaria um valor mutável e uma dependência de Git a uma exportação que, sem isso, depende apenas do formato do repositório. Um contrato compatível futuro pode adicionar proveniência explícita se um consumidor demonstrar essa necessidade sem enfraquecer a saída determinística.

O exportador lê no máximo 1.000 arquivos Markdown de ADR com até 524.288 bytes cada, exige UTF-8 válido e ignora links simbólicos e reparse points antes de ler o conteúdo. Exceder um limite ou encontrar entrada malformada retorna o exit operacional `3` sem produzir um artefato substituto.

## GitHub Actions e Align

[`github-action-relationships-export.yml`](examples/github-action-relationships-export.yml) mostra um job de privilégio mínimo que restaura uma ferramenta ADR Guard fixada por manifesto, grava `.adr-guard/relationships.json` e faz upload para um consumidor externo como o Align. Faça commit de um `.config/dotnet-tools.json` fixado em uma versão publicada que contenha este comando antes de habilitar o exemplo; a CLI publicada atualmente ainda não o contém.

O Align pode baixar ou receber o artefato e combinar cada `path` relativo ao repositório com sua árvore de documentação importada. Ele deve aceitar somente `resolution: "resolved"` como destino identificado de forma única e ainda deve inspecionar `issues` antes de tratar a aresta como semanticamente válida.

A etapa opcional `check` do exemplo é deliberadamente separada e consultiva. Exit `0` da exportação significa que o artefato JSON foi gravado; isso **não** significa que todas as ADRs ou relacionamentos passaram na validação. Equipes que exigem validação podem remover `continue-on-error`, mas essa é uma política de CI independente e não faz parte do contrato de exportação.

## Envelope e metadados de ADR

Todo documento contém, nesta ordem, `schemaVersion: "1.0"`, `adrs`, `relationships` e `issues`. Não há timestamps, corpos Markdown das ADRs nem conjuntos calculados de decisões efetivas.

Cada item de `adrs` contém `identifier`, `path`, `title` anulável e `status` anulável. Os valores de status reconhecidos são exatamente `Accepted`, `Proposed`, `Deprecated` e `Superseded`. Um status de origem ausente, desconhecido ou inválido é serializado como `null` e continua atribuível por uma issue; ele nunca é promovido a `Accepted`.

Todos os caminhos são portáveis, relativos à raiz do repositório e usam `/`. Eles nunca são relativos ao diretório de ADR configurado nem contêm caminhos absolutos da máquina.

## Relacionamentos e issues

Cada relacionamento preserva `sourceIdentifier`, `sourcePath`, `type`, `declaredTarget`, `targetIdentifier` anulável, `targetPath` anulável e `resolution`. Os tipos suportados são `supersedes`, `superseded-by` e `depends-on`.

Os valores de resolução da exportação distinguem resolução referencial de validação semântica:

- `resolved`: a declaração resolve para exatamente uma ADR;
- `ambiguous`: a declaração resolve para mais de uma candidata, portanto identidade e caminho do destino são `null`;
- `unresolved`: a declaração não resolve para uma ADR, portanto identidade e caminho do destino são `null`.

`resolved` descreve apenas a resolução do destino. Ele não confirma que o relacionamento seja semanticamente válido. Achados de validação, como ciclos, substituições inconsistentes ou dependências inativas, permanecem em `issues`; eles não são ocultados nem convertidos em conformidade bem-sucedida. Do mesmo modo, uma exportação bem-sucedida significa apenas que o artefato foi gravado, não que todos os relacionamentos sejam válidos. Cada issue preserva `code`, `message`, `path` relativo ao repositório e `severity` (`warning` ou `error`) da validação do Core.

A exportação não infere relacionamentos não declarados. Declarações recíprocas permanecem como registros tipados separados para que consumidores apliquem sua própria política de desduplicação.

## Ordenação e compatibilidade de versão

Produtores devem emitir campos de objeto e arrays determinísticos para entradas idênticas. ADRs são ordenadas por identificador e caminho; relacionamentos por identificador de origem, caminho de origem, tipo e destino declarado; issues seguem a ordem determinística da validação do Core. As fixtures seguem essa política.

`schemaVersion` usa `major.minor`:

- incremente a versão major para remoções, campos renomeados, mudança de anulabilidade, mudança de conjuntos de valores ou qualquer alteração semântica incompatível;
- incremente a versão minor somente para extensões compatíveis que preservem o significado de todos os campos existentes;
- nunca altere silenciosamente a semântica de um campo existente.

Consumidores podem aceitar uma versão minor mais nova dentro de uma major suportada, mas devem rejeitar uma major desconhecida. O schema `1.0` usa `resolved` para impedir que consumidores confundam resolução referencial com validação. A resposta MCP existente permanece inalterada e continua usando `valid` para seu valor histórico de resolução referencial.
