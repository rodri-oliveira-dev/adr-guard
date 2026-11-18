# Contrato v1 de exportação de relacionamentos ADR

[English](adr-relationships-export-v1.md)

Este documento congela o contrato de intercâmbio JSON preparado para integração com o Align CLI. O Core produz esses dados determinísticos, e tanto a ferramenta de relacionamentos do MCP quanto o adaptador CLI em estágio de código-fonte reutilizam essa implementação sem alterar o contrato MCP. O comando CLI está implementado no repositório, mas ainda não faz parte de uma release publicada.

A definição legível por máquina autoritativa é [`adr-relationships-export-v1.schema.json`](schemas/adr-relationships-export-v1.schema.json). As fixtures representativas de integração estão em [`docs/examples/adr-relationships-export-v1`](examples/adr-relationships-export-v1/).

## Exportação pela CLI

```text
adr-guard export relationships docs/adr
adr-guard export relationships docs/adr --output artifacts/relationships.json
```

O destino padrão é `<raiz-do-repositório>/.adr-guard/relationships.json`. Um destino personalizado é relativo à raiz detectada do repositório, deve usar a extensão `.json`, permanecer dentro dessa raiz e não pode atravessar links simbólicos nem reparse points. Diretórios de destino ausentes são criados. O documento UTF-8 completo é gravado em um arquivo temporário no diretório de destino e depois promovido atomicamente sobre qualquer artefato existente; uma falha de escrita preserva o artefato anterior.

O comando retorna `0` sempre que grava o artefato com segurança, inclusive quando `issues` não está vazio. Sintaxe ou configuração inválida retorna `2`; falhas de entrada, caminho, segurança, cancelamento ou escrita retornam `3`. Ele não edita ADRs ou índices nem modifica configuração ou estado do Git, e não exige servidor MCP, provedor de modelo, credenciais ou conexão de rede.

O SHA de commit opcional `generatedFrom` foi deliberadamente omitido do schema `1.0`. Ler estado Git implícito acrescentaria um valor mutável e uma dependência de Git a uma exportação que, sem isso, depende apenas do formato do repositório. Um contrato compatível futuro pode adicionar proveniência explícita se um consumidor demonstrar essa necessidade sem enfraquecer a saída determinística.

## Envelope e metadados de ADR

Todo documento contém, nesta ordem, `schemaVersion: "1.0"`, `adrs`, `relationships` e `issues`. Não há timestamps, corpos Markdown das ADRs nem conjuntos calculados de decisões efetivas.

Cada item de `adrs` contém `identifier`, `path`, `title` anulável e `status` anulável. Os valores de status reconhecidos são exatamente `Accepted`, `Proposed`, `Deprecated` e `Superseded`. Um status de origem ausente, desconhecido ou inválido é serializado como `null` e continua atribuível por uma issue; ele nunca é promovido a `Accepted`.

Todos os caminhos são portáveis, relativos à raiz do repositório e usam `/`. Eles nunca são relativos ao diretório de ADR configurado nem contêm caminhos absolutos da máquina.

## Relacionamentos e issues

Cada relacionamento preserva `sourceIdentifier`, `sourcePath`, `type`, `declaredTarget`, `targetIdentifier` anulável, `targetPath` anulável e `resolution`. Os tipos suportados são `supersedes`, `superseded-by` e `depends-on`.

Os valores de resolução correspondem intencionalmente à representação da ferramenta MCP somente leitura `get_adr_relationships` existente:

- `valid`: a declaração resolve para exatamente uma ADR;
- `ambiguous`: a declaração resolve para mais de uma candidata, portanto identidade e caminho do destino são `null`;
- `unresolved`: a declaração não resolve para uma ADR, portanto identidade e caminho do destino são `null`.

`valid` descreve apenas a resolução do destino. Achados de validação do relacionamento, como ciclos ou substituições inconsistentes, permanecem em `issues`; eles não são ocultados nem convertidos em conformidade bem-sucedida. Cada issue preserva `code`, `message`, `path` relativo ao repositório e `severity` (`warning` ou `error`) da validação do Core.

A exportação não infere relacionamentos não declarados. Declarações recíprocas permanecem como registros tipados separados para que consumidores apliquem sua própria política de desduplicação.

## Ordenação e compatibilidade de versão

Produtores devem emitir campos de objeto e arrays determinísticos para entradas idênticas. ADRs são ordenadas por identificador e caminho; relacionamentos por identificador de origem, caminho de origem, tipo e destino declarado; issues seguem a ordem determinística da validação do Core. As fixtures seguem essa política.

`schemaVersion` usa `major.minor`:

- incremente a versão major para remoções, campos renomeados, mudança de anulabilidade, mudança de conjuntos de valores ou qualquer alteração semântica incompatível;
- incremente a versão minor somente para extensões compatíveis que preservem o significado de todos os campos existentes;
- nunca altere silenciosamente a semântica de um campo existente.

Consumidores podem aceitar uma versão minor mais nova dentro de uma major suportada, mas devem rejeitar uma major desconhecida. O schema `1.0` é conceitualmente alinhado a `get_adr_relationships` e adiciona a lista de metadados `adrs`; ele não altera o contrato de requisição ou resposta do MCP.
