# Referência de CLI e configuração do ADR Guard

> Disponibilidade: `init`, `.adrguard.yml` e a saída estruturada de `check` foram publicados no ADR Guard v1.2.0. A seleção do formato MADR 4.0 foi publicada na v1.3.0. Consulte as [Releases do GitHub](https://github.com/rodri-oliveira-dev/adr-guard/releases) para os artefatos exatos.

> A análise de impacto arquitetural foi introduzida na v1.5.0, o drift arquitetural na v1.6.0 e os contratos de formulário visual na v1.7.0. A CLI 1.8.0 está publicada no [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard/1.8.0) e em sua [GitHub Release](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/v1.8.0).

> Os contratos de formulário visual no schema `1.0` exigem CLI 1.7.0 ou superior e, no uso visual, uma extensão VS Code compatível e versionada separadamente. Versões da CLI, extensão e MCP são independentes.

## Inventário de comandos

Esta tabela espelha o dispatcher e o `--help` embutido. Os guias especializados detalham contratos e limites de segurança.

| Comando | Entradas obrigatórias e opções principais | Escritas / efeitos externos | Contrato de saída |
| --- | --- | --- | --- |
| `init [repositório]` | Diretório de ADR, template, geração de workflow, `--dry-run` e `--overwrite` opcionais | Grava configuração/template/workflow somente quando solicitado; dry run não grava | `0`, `2`, `3` |
| `check [diretório]` | Texto/JSON/SARIF, formato, lifecycle/supersession, políticas de nome/placeholder/metadados/perfil, changed/base ref e baseline opcionais | Somente leitura; lê Git local com `--changed` | `0` válido, `1` achados, `2`, `3` |
| `baseline [diretório] --output <arquivo>` | Opções de política; `--update` obrigatório para substituir | Cria ou substitui explicitamente um baseline versionado | `0`, `2`, `3` |
| `index [diretório]` | Saída e política opcionais; `--catalog enriched` | Grava índice padrão determinístico ou catálogo enriquecido somente após validação | `0`, `1`, `2`, `3` |
| `new [diretório]` | Fluxo humano exige `--title`; fluxo estruturado exige `--form`; opções de template/cultura/nome | Prévia não grava; criação persiste uma ADR `Proposed` atomicamente | `0`, `1`, `2`, `3` |
| `edit <arquivo-adr> --form` | Requisição stdin schema `1.0`, opções de formato/política e diretório ADR opcional | `load` é somente leitura; `save` com fingerprint atualiza atomicamente apenas a ADR selecionada | `0`, `1`, `2`, `3` |
| `draft [diretório]` | Título, contexto, provider e modelo; endpoint/arquivos/ADRs/template/cultura/prévia opcionais | Chama o provider; prévia não grava; criação persiste uma ADR `Proposed` | `0`, `1`, `2`, `3` |
| `review <arquivo-adr>` | Provider/modelo; ref comparativa, contexto, política determinística, texto/JSON/saída opcionais | Chama o provider; ADR permanece somente leitura; relatório em arquivo apenas com `--output` | `0`, `1`, `2`, `3`, `4` |
| `impact [repositório] --base-ref <ref> --map <arquivo>` | Texto ou JSON | Análise somente leitura do Git local; nunca faz fetch | `0`, `2`, `3` |
| `drift [repositório] --rules <arquivo>` | Evidências, baseline, escrita/atualização de baseline, formato e política opcionais | Lê evidência limitada; grava baseline somente quando explícito; nunca executa produtores | `0`, `2`, `3`, `4` |

`-h`/`--help` global mostra a ajuda, e `--version` mostra a versão do assembly. Exit `2` significa uso inválido, `3` falha operacional ou resultado inconclusivo e `4` falha determinística de política; achados de IA isolados nunca representam aprovação nem conformidade.

## Inicializar um repositório

```text
adr-guard init [repositório] [--adr-directory <caminho>]
  [--template minimal|extended | --template-file <caminho>]
  [--github-actions] [--dry-run] [--overwrite]
```

`repositório` usa o diretório de invocação por padrão. Caminhos gerenciados devem ser relativos, permanecer dentro do repositório e não podem atravessar links simbólicos/reparse points. `--dry-run` relata exatamente os arquivos planejados sem gravar. Repetir o mesmo comando não regrava arquivos inalterados. Arquivos existentes causam erro operacional, salvo quando `--overwrite` autoriza explicitamente a substituição.

`--github-actions` grava `.github/workflows/adr-guard.yml` com `contents: read`, sem credenciais de provider e sem permissão de escrita.

## Configuração

O ADR Guard procura `.adrguard.yml` no diretório de invocação. O schema v1 é intencionalmente limitado a escalares top-level inertes:

```yaml
schema-version: 1
adr-directory: "docs/adr"
template: minimal
# template-file: "docs/templates/team.md" # exclusivo com template
# adr-format: canonical                   # canonical ou madr-4
# lifecycle-statuses: Rejected=rejected,Under Review=proposed
# conventional-supersession: true          # habilita relações no texto de status
# filename-policy: canonical               # canonical, unnumbered, adr-prefix, numeric:1..9
# placeholder-policy: warn                 # off (padrão), warn ou error
# metadata-policy: validate                # validação opcional e limitada de metadados
# validation-profile: legacy              # legacy, advisory, standard, strict
```

Propriedades desconhecidas/duplicadas, versões incompatíveis, YAML aninhado, coleções, tags, anchors, aliases, block scalars, UTF-8 inválido, arquivos grandes demais, caminhos inseguros e uso simultâneo de `template`/`template-file` são rejeitados. Caminhos relativos partem do diretório da configuração. A configuração nunca é interpretada como comando e não expande variáveis de ambiente.

A precedência é:

1. Argumento explícito da CLI.
2. Valor de `.adrguard.yml`.
3. Default legado do comando.

O diretório configurado é aplicado quando o argumento posicional é omitido em `check`, `index`, `new` e `draft`. O template configurado é aplicado somente quando `new`/`draft` não recebem opção explícita. `review` continua exigindo target explícito.

## Criar uma ADR por um cliente estruturado

```text
adr-guard new <diretório> --form
  [--template minimal|extended | --template-file <caminho>]
  [--culture en-US|pt-BR] [--filename-policy <política>]
```

`new --form` é um contrato JSON limitado para clientes confiáveis, como a extensão VS Code, e não um formulário interativo do terminal. Ele lê uma requisição no schema `1.0` pelo stdin e grava uma resposta no schema `1.0` no stdout. O comando tradicional `new --title` continua sendo o fluxo direto voltado a pessoas e aceita templates nativos ou personalizados com restrições.

As operações aceitas são:

- `describe`: retorna nome, compatibilidade e IDs ordenados dos campos do template, indicando os obrigatórios. O template nativo `minimal` expõe `context`, `decision` e `consequences` obrigatórios. `extended` acrescenta `decisionDrivers`, `optionsConsidered`, `rationale`, `positiveConsequences`, `negativeConsequences`, `risks` e `references` opcionais.
- `preview`: exige `title` e os `fields` de strings descritos; valida e retorna `path` provável, `markdown` exato e `written: false`, sem gravar, atualizar o índice ou reservar ID.
- `create`: recebe o mesmo conteúdo, realoca o ID definitivo sob o lock de criação, valida e persiste atomicamente. A resposta retorna o `path` final e `written: true`, sem devolver o corpo Markdown.

Somente os templates canônicos nativos `minimal` e `extended` podem ser mapeados para campos estruturados. Com `--template-file`, `describe` retorna `compatible: false` e o motivo `custom-template-field-mapping-unavailable`; `preview` e `create` rejeitam essa combinação. Use a criação tradicional para templates personalizados. `--culture` aceita instruções `en-US` ou `pt-BR`, e as políticas de nome permanecem iguais às de `new` tradicional.

```json
{"schemaVersion":"1.0","operation":"describe"}
```

```json
{"schemaVersion":"1.0","operation":"preview","title":"Adotar uma fila","fields":{"context":"Pedidos precisam de processamento assíncrono.","decision":"Usar a fila gerenciada.","consequences":"Precisamos operar retentativas e mensagens mortas."}}
```

As respostas contêm `schemaVersion`, `operation`, `template` (`name`, `compatible`, `reason` opcional e `fields` ordenados), `written` e `path`/`markdown` conforme a operação. Propriedades JSON desconhecidas ou duplicadas, IDs de campo não suportados, valores obrigatórios ausentes ou inválidos, versões incompatíveis e excesso de tamanho/profundidade falham de forma fechada. Erros de contrato/uso retornam `2`, achados de validação da ADR retornam `1`, e falhas de filesystem, cancelamento ou outras operações retornam `3`. Consulte o [guia de criação](creation.pt-BR.md) para detalhes do renderer, templates, prévia e concorrência.

## Editar uma ADR por um cliente estruturado

```text
adr-guard edit <arquivo-adr> --form [--adr-directory <diretório>]
  [--adr-format canonical|madr-4]
  [--lifecycle-statuses <mapeamento>] [--conventional-supersession]
  [--filename-policy <política>] [--placeholder-policy off|warn|error]
  [--validate-metadata]
  [--validation-profile <perfil>]
```

`edit --form` usa o mesmo limite stdin/stdout com schema `1.0` e também não é um editor interativo do terminal. Ele aceita `canonical` e `madr-4` opcional; `--adr-directory` informa o conjunto de validação e deve conter o alvo.

`load` aceita exatamente `schemaVersion` e `operation`. Ele valida a ADR atual e retorna `format`, `path` normalizado, `fingerprint` SHA-256 de 64 caracteres, `title`, `written: false` e um array `fields` ordenado. Cada campo contém `id`, `label`, `kind`, `required` e `value`; campos de status também incluem as `options` permitidas. Conforme o formato selecionado, os campos editáveis cobrem front matter escalar suportado, status canônico e seções de nível dois interpretadas.

```json
{"schemaVersion":"1.0","operation":"load"}
```

`save` aceita exatamente `schemaVersion`, `operation`, `fingerprint`, `title` e `fields`. `fields` é um objeto cujas chaves são os IDs retornados por `load`, com todos os campos carregados presentes uma única vez. Seu acknowledgement de sucesso é deliberadamente limitado: retorna apenas a versão do schema, a operação, o fingerprint dos bytes atuais e `written`, que é `true` somente quando o conteúdo mudou.

```json
{"schemaVersion":"1.0","operation":"save","fingerprint":"<SHA-256 de 64 caracteres retornado por load>","title":"Adotar uma fila durável","fields":{"section-0":"Proposed","section-1":"Contexto atualizado.","section-2":"Decisão atualizada.","section-3":"Consequências atualizadas."}}
```

```json
{"schemaVersion":"1.0","operation":"save","fingerprint":"<SHA-256 de 64 caracteres dos bytes atuais>","written":true}
```

IDs de campo são identificadores opacos de cada carregamento; clientes não devem inferir nem fixar os IDs do exemplo. O salvamento altera somente os intervalos do título ou dos campos modificados. Ele preserva nome e identificador da ADR, BOM UTF-8 quando presente, estilo de quebra de linha das seções substituídas, conteúdo aninhado, seções desconhecidas, comentários, metadados e todo o restante do Markdown não alterado. Antes de substituir atomicamente apenas esse arquivo, valida o alvo atualizado no formato e no contexto do repositório selecionados. Sem mudanças, retorna `written: false` e mantém os bytes intactos.

`save` exige o fingerprint devolvido por `load`. Se o arquivo foi alterado externamente, retorna erro operacional `3`, preserva esses bytes externos e exige novo carregamento antes da tentativa seguinte. Propriedades JSON desconhecidas/duplicadas, conjunto de propriedades incorreto, fingerprint inválido, IDs de campos ausentes/extras, valores inseguros, versões incompatíveis e excesso de tamanho/profundidade retornam `2`. Achados de validação da ADR retornam `1`; caminhos inválidos, alvos que não sejam Markdown ou sejam links simbólicos, UTF-8 inválido, arquivos grandes demais, I/O e cancelamento retornam `3`. A verificação do fingerprint impede gravações obsoletas comuns, embora um escritor não cooperante ainda possa alterar o alvo no intervalo inevitável entre a conferência e o rename atômico.

## Saída do check

> Não lançado: `--diagnostic-locations` está disponível no código-fonte atual, mas não faz parte do pacote publicado da CLI 1.8.0. Use essa opção somente com um build do código-fonte até que uma versão da CLI que a contenha seja lançada.

```text
adr-guard check [diretório] [--format text|json|sarif] [--diagnostic-locations]
```

`text` permanece como default. JSON usa schema `1.0`; SARIF usa `2.1.0`. `--diagnostic-locations` habilita linha/coluna baseadas em um ou regiões SARIF somente quando há evidência; posições não comprovadas são omitidas. Sem a flag, o formato existente do relatório é preservado para clientes estritos. Ambos são gravados no stdout em sucesso e falha de validação; diagnósticos operacionais usam stderr. Os exit codes continuam `0` para válido, `1` para diagnósticos ADR, `2` para uso/configuração inválida e `3` para falha operacional.

## Impacto arquitetural

```text
adr-guard impact [repositório] --base-ref <ref> --map <arquivo> [--format text|json]
```

`impact` é explícito, consultivo e somente leitura. `repositório` usa o diretório atual por padrão; `--map` é relativo ao repositório. Texto é o formato padrão, e JSON usa o schema independente de relatório `1.0`. Achados afetados/não mapeados retornam `0`, uso inválido retorna `2`, e falhas de Git/histórico/manifesto/limites retornam `3`. Consulte o [guia completo](guides/architecture-impact-analysis.pt-BR.md).

## Drift arquitetural

```text
adr-guard drift [repositório] --rules <arquivo> [--evidence <arquivo>]... [--baseline <arquivo>] [--write-baseline <arquivo> [--update-baseline]] [--format text|json] [--policy advisory|enforce]
```

`drift` verifica regras explícitas ligadas a ADRs sem avaliar MSBuild nem executar testes externos. Advisory é o padrão. Análise conclusiva retorna `0`; uso inválido retorna `2`; evidência desconhecida/inconclusiva ou falhas de entrada retornam `3`; enforcement explícito retorna `4` somente para uma nova violação estática determinística com gate habilitado. Consulte o [guia completo](guides/architectural-drift.pt-BR.md).

## Solução de problemas

- **Configuração ignorada:** invoque o ADR Guard no diretório de `.adrguard.yml` ou informe diretório/template explicitamente.
- **Recusa de sobrescrita:** inspecione o arquivo existente e use `init --overwrite` somente quando a substituição for desejada.
- **Caminho inseguro:** remova paths absolutos, escapes com `..` ou componentes de link simbólico/reparse point.
- **Relatório estruturado vazio:** JSON/SARIF usa stdout; não redirecione stderr para o mesmo arquivo.
- **CI precisa de Code Scanning:** gere SARIF e use uma etapa separada com `security-events: write`; a Action padrão permanece intencionalmente somente leitura.

## Migração

Nenhuma migração é necessária. Repositórios sem `.adrguard.yml` preservam os padrões legados. Adicione configuração quando ela ajudar a centralizar argumentos repetidos de diretório, template ou formato.
