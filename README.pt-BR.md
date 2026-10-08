# ADR Guard

[![CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/RodriOliveira.AdrGuard.svg)](https://www.nuget.org/packages/RodriOliveira.AdrGuard)
[![GitHub Release](https://img.shields.io/github/v/release/rodri-oliveira-dev/adr-guard)](https://github.com/rodri-oliveira-dev/adr-guard/releases)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
[![License](https://img.shields.io/github/license/rodri-oliveira-dev/adr-guard)](LICENSE)

[English](README.md)

**Consumidores da GitHub Action:** consulte o [guia de consumo](docs/github-action.pt-BR.md), o [guia de review por IA](docs/github-action-review.pt-BR.md), a [política de release](docs/github-action-release.pt-BR.md), o [modelo de segurança](docs/github-action-security.pt-BR.md), a [auditoria da release pública](docs/public-release-audit.pt-BR.md), as [evidências de verificação externa](docs/github-action-external-verification.pt-BR.md) e o [checklist de publicação no Marketplace](docs/github-marketplace.pt-BR.md). A tag móvel `@v1` está publicada e é exercitada pelo CI no contrato público de `check`/`index`; o `review` opt-in está publicado na linha `v1` desde a `v1.1.6`. Listagem no Marketplace: [ADR Guard - Architecture Decision Validator](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator). A URL foi fornecida pelo proprietário em 07/10/2026; a verificação independente em sessão deslogada ainda está pendente. O suporte está em [SUPPORT.md](SUPPORT.md) e relatos de segurança seguem [SECURITY.md](SECURITY.md).

> **Disponibilidade por versão:** `new` offline e `draft` com templates estão publicados desde a **v1.1.0**. O CLI `review` está publicado desde a **v1.1.2**, a policy determinística de review desde a **v1.1.3**, o hardening de segurança/regressão desde a **v1.1.4** e o `command: review` opt-in da GitHub Action desde a **v1.1.6**. Use o badge do NuGet ou [GitHub Releases](https://github.com/rodri-oliveira-dev/adr-guard/releases) como fonte de verdade para o patch exato mais recente; a documentação evita fixar um patch "atual" que muda a cada release.

ADR Guard é uma ferramenta de linha de comando para .NET focada em validar e indexar Architecture Decision Records (ADRs).

A proposta é permitir que convenções de ADR sejam explícitas, revisáveis e verificáveis tanto no desenvolvimento local quanto no CI, sem adicionar dependências pesadas em runtime.

## Recursos

- valida nomes de arquivos, títulos, status e seções obrigatórias;
- detecta IDs de ADR duplicados;
- detecta links relativos quebrados entre ADRs;
- exige um link válido em `Superseded by` para decisões substituídas;
- gera um índice Markdown determinístico;
- evita reescrever um índice que já está atualizado;
- fornece códigos de validação estáveis (`ADR001` até `ADR009`);
- fornece exit codes previsíveis para CI/CD;
- cria ADRs `Proposed` editáveis offline usando templates Markdown internos ou personalizados;
- oferece criação assistida por IA de ADRs `Proposed`, com revisão humana, providers e contexto explícitos;
- revisa ADRs existentes em oito dimensões arquiteturais com evidências, incerteza, relatórios versionados e enforcement determinístico opcional;
- é distribuído como .NET Tool sem dependências externas em runtime.

## Instalação

As releases são publicadas tanto no NuGet.org quanto no [GitHub Packages](https://github.com/rodri-oliveira-dev?tab=packages).

A instalação mais simples usa o NuGet.org:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
```

Para atualizar uma instalação existente:

```bash
dotnet tool update --global RodriOliveira.AdrGuard
```

O GitHub Packages também fica disponível como registry secundário. Clientes NuGet precisam de autenticação no GitHub para consumir pacotes dessa fonte.

O comando instalado é:

```bash
adr-guard
```

## Imagens de container

O ADR Guard também é distribuído, na mesma versão da release, pelo GHCR e pelo Docker Hub:

```text
ghcr.io/rodri-oliveira-dev/adr-guard
docker.io/rodrigodotnet/adr-guard
```

As imagens suportam `linux/amd64` e `linux/arm64` e publicam tags SemVer exata, minor, major e `latest`. Exemplo:

```bash
docker run --rm \
  -v "$PWD:/workspace:ro" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  check docs/adr
```

A imagem executa como usuário não-root. Releases incluem metadados OCI, attestations de SBOM e provenance, e o caminho de CI é protegido por Hadolint, smoke tests, atualizações de imagem-base pelo Dependabot e análise de vulnerabilidades com Trivy.

Consulte o [guia de container e supply chain](docs/container.pt-BR.md) para volumes graváveis, credenciais de providers de IA, pin por digest imutável, tags e detalhes de verificação.

## GitHub Action

O ADR Guard fornece uma composite GitHub Action que executa diretamente a imagem publicada no GHCR, portanto o repositório consumidor não precisa instalar o .NET SDK. O checkout deve acontecer antes da Action, que suporta runners Linux com Docker funcional, como `ubuntu-latest`.

> A tag móvel de compatibilidade `@v1` está publicada. Consumidores podem usar `uses: rodri-oliveira-dev/adr-guard@v1`; a [listagem no GitHub Marketplace](https://github.com/marketplace/actions/adr-guard-architecture-decision-validator) tem URL informada pelo proprietário; a verificação pública independente ainda está pendente.

Para workflows completos de pull request/main, inputs, annotations, configuração de checks obrigatórios, troubleshooting e status de release/Marketplace, consulte o [guia de consumo da GitHub Action](docs/github-action.pt-BR.md).

A operação padrão valida `docs/adr`:

```yaml
name: Validação de ADRs

on:
  pull_request:
  push:

permissions:
  contents: read

jobs:
  adr-guard:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          persist-credentials: false

      - name: Validar ADRs
        uses: rodri-oliveira-dev/adr-guard@vX.Y.Z
        with:
          path: docs/adr
          command: check
```

As entradas são pequenas e correspondem diretamente ao comportamento suportado pelo CLI:

| Entrada | Padrão | Valores permitidos / política |
| --- | --- | --- |
| `path` | `docs/adr` | Diretório de ADRs relativo ao repositório. Caminhos absolutos, travessia com `..`, diretórios inexistentes e caminhos que resolvam para fora de `GITHUB_WORKSPACE` são rejeitados. |
| `command` | `check` | `check`, `index` ou `review` explícito. `review` está disponível na linha `v1` publicada desde a `v1.1.6`; `new` e `draft` não são comandos da Action. |
| `version` | vazio | Versão exata opcional da imagem, no formato `X.Y.Z` ou `vX.Y.Z`. Quando omitida, `@vX.Y.Z` seleciona a imagem exata e `@vX` seleciona a tag major móvel correspondente. Pins por SHA/branch exigem versão exata explícita. |

A seleção de versão nunca faz fallback para `latest`. Com `uses: rodri-oliveira-dev/adr-guard@v1.2.3`, a Action executa `ghcr.io/rodri-oliveira-dev/adr-guard:1.2.3`. Com `uses: rodri-oliveira-dev/adr-guard@v1`, ela usa a tag major móvel correspondente da imagem, `:1`. Tags exatas da Action são imutáveis; tags major avançam apenas para releases bem-sucedidas mais novas daquela major. Se a Action estiver fixada por SHA de commit ou por uma branch, informe a versão da imagem explicitamente:

```yaml
- name: Validar ADRs com a Action fixada por commit
  uses: rodri-oliveira-dev/adr-guard@<commit-sha>
  with:
    path: architecture/adr
    command: check
    version: 1.2.3
```

Consulte a [política de release da GitHub Action](docs/github-action-release.pt-BR.md) para semântica de tags exatas e major, garantias de idempotência, ordem de publicação e procedimento de verificação.

O comando `check` monta o checkout como somente leitura, impedindo que a validação altere arquivos do repositório. No `index`, o workspace continua somente leitura e apenas o diretório de ADRs selecionado é sobreposto como gravável, pois é nele que o CLI gera ou atualiza o `README.md`:

```yaml
- name: Gerar índice de ADRs
  uses: rodri-oliveira-dev/adr-guard@vX.Y.Z
  with:
    path: docs/adr
    command: index

- name: Falhar se o índice gerado não estiver commitado
  run: git diff --exit-code -- docs/adr/README.md
```

Todas as entradas do usuário são passadas como argumentos separados de processo, e não como fragmentos executáveis de shell. Os caminhos são resolvidos em relação ao checkout antes da execução do Docker, incluindo resolução de symlinks, e o contrato de exit codes do CLI permanece inalterado: `0` para sucesso, `1` para falha de validação, `2` para erro de uso/entrada e `3` para erro operacional.

Quando um comando `check` ou `index` termina com exit code `1`, a Action converte os diagnósticos reconhecidos `ADR001`–`ADR009` do CLI em **anotações de erro associadas ao arquivo** no GitHub, apenas para ADRs existentes dentro do diretório validado. Como o CLI não fornece números de linha confiáveis, as anotações não informam uma linha inventada. Caminhos e mensagens são validados e escapados antes da emissão de workflow commands; saídas inesperadas e erros operacionais não geram anotações de regras ADR.

A Action grava um `GITHUB_STEP_SUMMARY` compacto com resultado, exit code e, nas falhas de validação reconhecidas, quantidade total e contagem por regra. São emitidas **no máximo 50 anotações por execução**; todos os diagnósticos continuam disponíveis no log bruto do CLI. A interpretação de workflow commands fica temporariamente suspensa durante a exibição desse log, evitando que conteúdo não confiável dos ADRs injete anotações ou outros comandos. Uma falha no relatório não altera o exit code original do CLI.

O caminho padrão `check`/`index` da Action não precisa de credenciais de provider nem de rede e mantém `--network=none`. O `review` opt-in publicado usa rede de saída somente para o provider selecionado, mantém o checkout somente leitura, encaminha apenas a variável de credencial do provider selecionado, nunca encaminha `GITHUB_TOKEN`/`GH_TOKEN`, aceita somente eventos `push`, `workflow_dispatch`, `schedule` e `pull_request` do mesmo repositório, rejeita todos os demais tipos de evento antes da execução do provider e continua exigindo apenas `permissions: contents: read`. O `draft` assistido por IA permanece fora do contrato da Action. Consulte o [guia de review por IA na GitHub Action](docs/github-action-review.pt-BR.md) e o [modelo de segurança](docs/github-action-security.pt-BR.md).

Windows, macOS, runners Linux sem Docker funcional e execução como root para `index` gravável não são suportados.

A Action publicada `rodri-oliveira-dev/adr-guard@v1` suporta `check`, `index` e `review` opt-in. `new` e `draft` com IA continuam sendo fluxos de CLI/.NET Tool ou container direto, não comandos da Action.

## Formato dos ADRs

O ADR Guard espera arquivos Markdown com um ID de quatro dígitos seguido por um slug em lowercase kebab-case:

```text
0001-use-postgresql.md
```

Um ADR mínimo válido:

```markdown
# Use PostgreSQL

## Status

Accepted

## Context

We need a relational database.

## Decision

Use PostgreSQL.

## Consequences

The service depends on PostgreSQL operational knowledge.
```

Status aceitos:

- `Proposed`
- `Accepted`
- `Deprecated`
- `Superseded`

As seções `Context`, `Decision` e `Consequences` são obrigatórias. Um ADR com status `Superseded` também precisa de uma seção `Superseded by` apontando para um ADR existente.

## Criar ADRs Proposed offline

O comando `adr-guard new` publicado cria uma ADR editável **sem IA, credenciais ou acesso à rede**. O diretório de destino precisa existir. Minimal e `en-US` são os padrões; Extended e Custom são opcionais.

```bash
mkdir -p docs/adr
adr-guard new docs/adr --title "Adotar Redis" --culture pt-BR
adr-guard new docs/adr --title "Adotar Mensageria" --template extended --culture pt-BR
adr-guard new docs/adr --title "Adotar Cache" --template-file docs/examples/templates/team.pt-BR.md --culture pt-BR
adr-guard new docs/adr --title "Somente prévia" --template minimal --preview
adr-guard check docs/adr
adr-guard index docs/adr
```

`--dry-run` equivale a `--preview`; nenhum dos dois grava ADR, atualiza o índice ou reserva ID. `new` não atualiza o índice automaticamente: execute `check` e `index` explicitamente. `--template minimal|extended` e `--template-file <caminho>` são mutuamente exclusivos. `--culture en-US|pt-BR` traduz instruções editoriais, mas **não** os títulos canônicos `Status`, `Context`, `Decision`, `Consequences` nem o status inicial `Proposed`. O arquivo personalizado deve ser um `.md` UTF-8 de até 65.536 bytes, selecionado individualmente e resolvido a partir do diretório de invocação; mantenha templates fora da pasta de ADRs. O novo ID é calculado após o maior ID existente; criadores cooperantes no mesmo host compartilham o bloqueio e a gravação atômica sem sobrescrita; `{{id}}` recebe o ID alocado definitivo. **Validação estrutural não é aprovação arquitetural:** um responsável deve revisar o conteúdo e substituir as instruções.

**Exemplos válidos:** [Minimal EN](docs/examples/generated/minimal/0001-adopt-redis.md), [Extended pt-BR](docs/examples/generated/extended/0001-adotar-redis.md), [Custom EN](docs/examples/generated/custom/0001-adopt-cache.md), [Custom pt-BR](docs/examples/generated/custom-pt-BR/0001-adotar-cache.md). Consulte o [guia completo de criação offline e códigos de saída](docs/creation.pt-BR.md) e as [regras de placeholders](docs/custom-templates.pt-BR.md).

> **Preparação da v1.3.0:** builds do código-fonte na branch encadeada da v1.3 adicionam suporte explícito a MADR 4.0 com `--adr-format madr-4` e governança determinística de relacionamentos. O formato canônico permanece como default; estes recursos não são publicados até a conclusão da release independente v1.3.0. Consulte o [guia MADR](docs/madr-4.pt-BR.md) e o [guia de governança](docs/relationship-governance.pt-BR.md).

## Validar ADRs

> **Preparação da v1.2.0:** `init`, `.adrguard.yml` e `check --format json|sarif` estão disponíveis via build do código-fonte na branch de desenvolvimento da v1.2. Eles não fazem parte de uma release publicada até a conclusão do processo manual e independente da v1.2.0.

Para validar recursivamente uma pasta:

```bash
adr-guard check docs/adr
```

Quando a pasta não é informada, o diretório atual é utilizado:

```bash
adr-guard check
```

Uma validação bem-sucedida retorna exit code `0`. Falhas exibem caminho do arquivo, código estável da regra e mensagem.

Exemplo:

```text
docs/adr/0002-use-cache.md: ADR004 Status 'Approved' is invalid. Allowed values: Proposed, Accepted, Deprecated, Superseded.
Validation failed with 1 issue(s).
```

Builds do código-fonte preparados para a v1.2.0 podem inicializar um repositório e reutilizar configuração opcional:

```bash
adr-guard init --adr-directory docs/adr --template minimal --github-actions
adr-guard check
```

A precedência é: argumentos explícitos da CLI, `.adrguard.yml` e, por fim, os defaults legados. Sem configuração, todos os comandos existentes preservam seus defaults anteriores. O arquivo aceita somente o esquema escalar documentado; tags, aliases, substituições e comandos nunca são executados. Consulte a [referência da CLI e configuração da v1.2](docs/cli-reference.pt-BR.md).

O `check` mantém texto como saída padrão e pode emitir JSON determinístico ou SARIF 2.1.0 no stdout:

```bash
adr-guard check docs/adr --format json
adr-guard check docs/adr --format sarif > adr-guard.sarif
```

A saída estruturada continua sendo um documento válido quando a validação retorna exit code `1`; mensagens operacionais usam stderr. O [schema JSON](docs/schemas/adr-check-report-v1.schema.json), o [guia de relatórios](docs/check-reports.pt-BR.md) e o exemplo de Code Scanning com privilégio mínimo definem os contratos.

## Gerar o índice de ADRs

Para validar os ADRs e gerar `README.md` dentro da pasta:

```bash
adr-guard index docs/adr
```

O arquivo gerado é determinístico:

```markdown
# Architecture Decision Records

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-use-postgresql.md) | Use PostgreSQL | Accepted |
| [0002](0002-adopt-opentelemetry.md) | Adopt OpenTelemetry | Proposed |
```

O índice só é escrito depois que a validação passa. Se o conteúdo existente já estiver atualizado, o arquivo não é reescrito.

Também é possível gerar um índice customizado fora da pasta de ADRs:

```bash
adr-guard index docs/adr --output adr-index.md
```

Dentro da própria pasta de ADRs, Markdown gerado precisa se chamar `README.md`; caso contrário, ele seria interpretado como candidato a ADR na validação seguinte.

## Criação assistida por IA de ADRs

O ADR Guard pode solicitar a um provider externo de IA configurado que gere um rascunho de ADR, mantendo persistência, seleção de contexto e aceitação arquitetural sob controle humano. A saída da IA é sempre tratada como uma **proposta**: o ADR Guard força o status gerado para `Proposed`, valida a estrutura do documento e nunca aceita uma decisão arquitetural em nome do time.

Um rascunho persistido mínimo usa somente o contexto arquitetural inline informado na linha de comando:

```bash
adr-guard draft docs/adr \
  --title "Adotar um message broker" \
  --context "Precisamos de integração assíncrona." \
  --provider openai \
  --model <modelo-openai>
```

O fluxo normal de persistência aloca deterministicamente o próximo ID de ADR, cria um filename compatível e valida o candidato gerado com o parser/validator normal de ADRs. Na persistência, o candidato completo é escrito em um arquivo temporário dentro do diretório de ADRs, passa por flush e somente então é promovido atomicamente para o filename final sem sobrescrita. Se houver cancelamento, falha do provider, falha de validação, erro de I/O ou corrida concorrente pelo mesmo filename, o ADR Guard não deixa um ADR final parcial e remove seu arquivo temporário.

A CLI de produção propaga cancelamento por todo o fluxo de draft. Pressionar `Ctrl+C` solicita cancelamento gracioso durante carregamento de contexto, chamadas HTTP ao provider, pontos de validação e persistência.

### Templates opcionais no draft com IA

Sem `--template` ou `--template-file`, o `draft` mantém renderização anterior, culturas .NET, contrato de provedor e seleção explícita do contexto. Com template selecionado, somente a renderização final muda localmente: o conteúdo, a orientação e o caminho do template **nunca são enviados ao provedor de IA**. Use `--template minimal|extended` ou `--template-file docs/examples/templates/team.pt-BR.md` junto com os parâmetros existentes `--provider`, `--model`, `--title` e `--context`. ADRs existentes são compartilhadas apenas com `--include-existing-adrs` explícito; arquivos de contexto, apenas com `--context-file` explícito. Diferentemente do `new --preview` offline, `draft --preview` ainda chama o provedor, mas não grava. Consulte o [guia de templates para IA e privacidade](docs/draft-templates.pt-BR.md).

### Providers, modelos e autenticação

O ADR Guard não escolhe um modelo automaticamente. `--provider` e `--model` são obrigatórios em runtime.

| Provider | Valor na CLI | Autenticação | Endpoint |
| --- | --- | --- | --- |
| OpenAI | `openai` | `OPENAI_API_KEY` | endpoint oficial; `--endpoint` customizado é rejeitado |
| Anthropic | `anthropic` | `ANTHROPIC_API_KEY` | endpoint oficial; `--endpoint` customizado é rejeitado |
| Gemini | `gemini` | `GEMINI_API_KEY` | endpoint oficial; `--endpoint` customizado é rejeitado |
| OpenAI-compatible | `openai-compatible` | `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY` (opcional) | `--endpoint <uri>` obrigatório |

Exemplos:

```bash
adr-guard draft docs/adr --title "Decisão" --context "Contexto" \
  --provider anthropic --model <modelo-anthropic>

adr-guard draft docs/adr --title "Decisão" --context "Contexto" \
  --provider gemini --model <modelo-gemini>

adr-guard draft docs/adr --title "Decisão" --context "Contexto" \
  --provider openai-compatible --model <modelo> \
  --endpoint https://example.internal/v1
```

A autenticação é lida de variáveis de ambiente, e não de argumentos da CLI, evitando expor credenciais no histórico do comando ou no conteúdo dos ADRs. A CLI informa provider e modelo selecionados, mas não exibe valores de autenticação.

Para `openai-compatible`, endpoints remotos devem usar HTTPS mesmo quando nenhuma API key estiver configurada, porque o próprio contexto arquitetural pode ser sensível. HTTP sem TLS é permitido somente para endpoints de loopback, como `localhost`, `127.0.0.1` ou `::1`, preservando fluxos locais com Ollama/LM Studio e similares. Se `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY` estiver definida, HTTPS é obrigatório inclusive em loopback para que a credencial Bearer nunca seja enviada em texto puro. As requisições oficiais da OpenAI definem explicitamente `store: false`.

### Idioma e contexto inline

`--context` fornece diretamente o problema arquitetural ou suas restrições e continua obrigatório. O texto gerado usa `en-US` por padrão; para outro idioma, informe um culture name do padrão de globalization do .NET, como `pt-BR`:

```bash
adr-guard draft docs/adr \
  --title "Adotar cache distribuído" \
  --context "Precisamos reduzir a latência de leitura." \
  --culture pt-BR \
  --provider openai \
  --model <modelo-openai>
```

Os headings canônicos do ADR e o status `Proposed` permanecem inalterados independentemente da culture selecionada. A prose gerada pelo provider é rejeitada se tentar introduzir outro título de nível um ou duplicar as seções canônicas de nível dois `Status`, `Context`, `Decision` ou `Consequences`. Headings dentro de blocos de código cercados por fences continuam sendo tratados como conteúdo da seção.

### Limites de tamanho do contexto

O ADR Guard limita deterministicamente o conteúdo enviado antes de invocar o provider de IA configurado:

| Fonte de contexto | Máximo |
| --- | ---: |
| `--context` inline | 20.000 caracteres |
| Cada `--context-file` | 50.000 caracteres |
| Todos os context files explícitos somados | 100.000 caracteres |
| Contexto parseado dos ADRs existentes | 12.000 caracteres |
| Contexto final composto para geração | 120.000 caracteres |

Arquivos explícitos são lidos somente até o limite individual mais um caractere, evitando carregar arquivos arbitrariamente grandes por inteiro apenas para detectar excesso. Contexto inline, arquivo individual, soma dos arquivos ou contexto final acima do limite é rejeitado com erro acionável antes da chamada ao provider. Arquivos explícitos nunca são truncados silenciosamente.

### Contexto de ADRs existentes

Os ADRs existentes **não** são enviados a um provider de IA por padrão. Use `--include-existing-adrs` para habilitar explicitamente esse contexto:

```bash
adr-guard draft docs/adr \
  --title "Adotar um message broker" \
  --context "Precisamos de integração assíncrona." \
  --include-existing-adrs \
  --provider openai \
  --model <modelo-openai>
```

O ADR Guard constrói esse contexto a partir dos dados parseados dos ADRs, em vez de concatenar arquivos do repositório. Cada ADR selecionado contribui com ID, título, status, decisão e relacionamentos Markdown locais. A ordenação é determinística pelo ID numérico e, em seguida, pelo filename.

O contexto dos ADRs existentes é limitado a **12.000 caracteres**. Representações completas dos ADRs são adicionadas em ordem determinística enquanto couberem no limite; quando a próxima representação completa ultrapassaria o limite, esse ADR e todos os seguintes são omitidos. Essa estratégia não trunca parcialmente os campos de um ADR. A CLI avisa explicitamente quando conteúdo de ADRs existentes será enviado ao provider.

### Arquivos de contexto explícitos

Use opções repetíveis `--context-file <path>` para adicionar arquivos Markdown ou texto selecionados explicitamente:

```bash
adr-guard draft docs/adr \
  --title "Adotar um message broker" \
  --context "Precisamos de integração assíncrona." \
  --context-file ./architecture/constraints.md \
  --context-file ./notes/runtime.txt \
  --provider openai \
  --model <modelo-openai>
```

Somente os paths `.md` e `.txt` exatos informados pelo usuário são lidos. O ADR Guard não faz varredura recursiva do repositório, da árvore de código-fonte, de arquivos vizinhos nem de diretórios pai. Quando há vários arquivos, eles são compostos na mesma ordem em que aparecem na linha de comando.

Antes da geração, a CLI exibe os paths locais resolvidos que serão utilizados. O request enviado ao provider contém o nome e o conteúdo de cada arquivo selecionado, mas não o path local completo do filesystem.

A composição do contexto é determinística:

1. `--context` inline;
2. conteúdo dos `--context-file` explícitos na ordem da linha de comando;
3. contexto parseado dos ADRs existentes quando `--include-existing-adrs` está habilitado.

### Preview sem persistência

Use `--dry-run` ou o alias `--preview` para executar o caminho normal de geração e validação sem criar arquivo:

```bash
adr-guard draft docs/adr \
  --title "Adotar um message broker" \
  --context "Precisamos de integração assíncrona." \
  --provider openai \
  --model <modelo-openai> \
  --dry-run
```

O preview calcula o mesmo ID e filename candidatos de forma determinística, gera o ADR, força `Proposed`, faz parse e validação e então exibe o path candidato e o Markdown completo gerado. Apenas a etapa final de escrita é pulada: o diretório de ADRs e o índice gerado permanecem inalterados.

### Privacidade, limitações e revisão humana

Todo contexto inline, conteúdo dos context files explicitamente selecionados e contexto de ADRs existentes habilitado via opt-in é enviado ao provider externo configurado. Revise o material selecionado para identificar credenciais, dados pessoais, informações confidenciais de negócio e outros conteúdos sensíveis antes da geração. Armazenamento, retenção, treinamento e processamento realizados pelo provider seguem as políticas do provider configurado.

A criação assistida por IA permanece deliberadamente human-in-the-loop. Um ADR gerado pode ser estruturalmente válido e ainda conter premissas incorretas, trade-offs fracos, problemas de segurança ou informações inventadas. **Um arquiteto ou revisor responsável deve revisar o raciocínio arquitetural antes de alterar um ADR de `Proposed` para qualquer outro status.**

Esse fluxo não realiza varredura de código-fonte, ingestão automática do repositório inteiro, análise de Git diff, detecção automática de necessidade de ADR, alteração automática dos status de ADRs existentes, commits ou pull requests, RAG/busca vetorial/embeddings, fallback entre providers nem roteamento automático de modelos.

## Revisão técnica assistida por IA de ADRs existentes

`adr-guard review` é um fluxo separado e somente leitura para revisar um ADR existente. Ele **não** reescreve o ADR, não muda status, não atualiza o índice, não aprova/rejeita a decisão e não trata saída do modelo como gate objetivo de CI.

Uso mínimo:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai \
  --model <modelo-openai>
```

O mesmo contrato de provider/model/autenticação usado por `draft` vale para `review`: OpenAI/`OPENAI_API_KEY`, Anthropic/`ANTHROPIC_API_KEY`, Gemini/`GEMINI_API_KEY` ou `openai-compatible` com endpoint explícito e `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY` opcional. O ADR Guard nunca escolhe o modelo automaticamente.

O review aceita `--context-file <path>` explícito e repetível, `--include-existing-adrs` opcional, `--policy advisory|enforce`, `--policy-file <path>`, `--format text|json` e persistência opcional do relatório por `--output <path> [--overwrite]`.

O contexto continua opt-in e limitado:

| Fonte do review | Máximo |
| --- | ---: |
| Cada `--context-file` explícito | 50.000 caracteres / 150.000 bytes |
| Todos os context files explícitos | 100.000 caracteres / 300.000 bytes |
| Contexto parseado de ADRs existentes | 12.000 caracteres |
| Contexto final composto do review | 120.000 caracteres |

Por padrão, somente o ADR selecionado é enviado ao provider externo configurado. Context files explícitos precisam ser `.md`/`.txt` UTF-8 válidos; descoberta de ADRs existentes só ocorre com `--include-existing-adrs`. O material selecionado pode sair da máquina/organização e fica sujeito às políticas do provider sobre retenção, logging, residência, treinamento, cobrança, quota e rate limit. Para optar por não usar processamento de terceiros, não execute `review`; `check`, `index` e `new` continuam fluxos determinísticos/offline.

Toda resposta do provider precisa cobrir oito dimensões: clareza/racional, alternativas, requisitos não funcionais, riscos/consequências, consistência arquitetural, segurança/compliance, viabilidade de implementação/operação e critérios mensuráveis de verificação. Findings usam `observed-evidence`, `potential-risk`, `missing-context`, `recommendation-for-human-investigation` ou `not-applicable`. Contexto ausente, riscos potenciais, linguagem severa e recomendações do modelo continuam advisory.

`--policy advisory` é o padrão. `--policy enforce --policy-file <path>` só pode falhar por regras locais determinísticas (`required-section-content` e `required-context-file` no schema `1.0`) e retorna exit code `4` antes da chamada ao provider quando uma regra nomeada falha. Policy passando ainda não significa aprovação arquitetural.

Relatórios em text são compatíveis com Markdown e legíveis por humanos. `--format json` emite schema versionado `1.0` com campos camelCase estáveis, escopo dos inputs selecionados, as oito dimensões, findings de follow-up, incerteza, limitações e aviso de custo. Falhas de provider/transporte/timeout/rate limit/resposta malformada retornam `3`, nunca “sem problemas”.

Para o contrato público completo, exemplo de relatório, semântica de evidência, matriz de policy, workflow da Action, orientação para forks/comunidade, disponibilidade por release e troubleshooting, consulte o [guia de review por IA](docs/adr-review.pt-BR.md), a [policy v1](docs/adr-review-policy-v1.pt-BR.md) e os [limites de segurança](docs/adr-review-security.pt-BR.md).

**A responsabilidade humana é obrigatória:** a saída do review é assistência arquitetural, não prova de correção, certificação formal de segurança/compliance nem aceitação automática.

## Regras de validação

| Código | Validação |
| --- | --- |
| `ADR001` | Filename deve seguir `NNNN-lowercase-kebab-case.md` |
| `ADR002` | Título de nível um é obrigatório |
| `ADR003` | Status é obrigatório |
| `ADR004` | Status precisa ser suportado |
| `ADR005` | Seção obrigatória ausente ou vazia |
| `ADR006` | ID de ADR duplicado |
| `ADR007` | Referência relativa para ADR quebrada |
| `ADR008` | ADR substituído sem link válido em `Superseded by` |
| `ADR009` | Seção canônica de nível dois do ADR está duplicada |
| `ADR010` | Grafo de substituição contém ciclo |
| `ADR011` | ADR declara a si próprio como decisão substituta |
| `ADR012` | ADR declara múltiplas decisões substitutas |
| `ADR013` | Declarações explícitas de substituição conflitam com status/target |
| `ADR014` | `Depends on`/`Dependencies` explícito aponta para decisão inativa |

A numeração não precisa ser contínua. Lacunas são aceitas porque ADRs podem ser arquivados, migrados ou removidos sem renumerar decisões históricas.

## Exit codes

| Código | Significado |
| ---: | --- |
| `0` | Sucesso |
| `1` | Validação dos ADRs falhou |
| `2` | Uso inválido da CLI |
| `3` | Erro operacional/provider/cancelamento |
| `4` | Regra determinística de `review --policy enforce` falhou |

Isso permite integração direta em CI:

```yaml
- name: Validate ADRs
  run: adr-guard check docs/adr
```

## Build a partir do código-fonte

Requisito:

- .NET SDK 10.0.400 ou patch compatível da mesma feature band.

Build e testes:

```bash
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
dotnet test AdrGuard.slnx --configuration Release --no-build
```

Gerar o pacote da ferramenta:

```bash
dotnet pack src/AdrGuard/AdrGuard.csproj --configuration Release --no-build --output artifacts/package
```

Instale localmente o pacote baseline gerado (o `VersionPrefix` do repositório é a base da série de releases, não o patch público mais recente):

```bash
dotnet tool install --tool-path ./.tools RodriOliveira.AdrGuard --version 1.1.0 --add-source ./artifacts/package
./.tools/adr-guard check docs/adr
```

## Decisões de arquitetura

O ADR Guard valida os próprios ADRs do projeto. Consulte [docs/adr](docs/adr/README.md).

O CI do repositório compila e testa a solução, empacota a .NET Tool, instala o pacote localmente, executa o `adr-guard` empacotado contra `docs/adr`, regenera o índice e verifica se houve drift na documentação. O caminho de container também valida o `Dockerfile` com Hadolint, faz build e smoke tests da imagem e bloqueia vulnerabilidades corrigíveis `HIGH` ou `CRITICAL` detectadas pelo Trivy.

Para os principais recursos da release, consulte as [notas da v1.1.0](docs/releases/v1.1.0.pt-BR.md) ([EN](docs/releases/v1.1.0.md)) e a [matriz exata de disponibilidade do review por IA](docs/releases/ai-review.pt-BR.md) ([EN](docs/releases/ai-review.md)).

## Recursos adicionais

Para quem quiser se aprofundar em Architecture Decision Records, há uma coleção de documentos, modelos e exemplos em português brasileiro:

- [Architecture Decision Record — documentação em português brasileiro](https://github.com/rodri-oliveira-dev/architecture-decision-record/blob/translation/pt-br/locales/pt-br/index.md)

A tradução para português brasileiro foi uma contribuição minha ao projeto `architecture-decision-record`.

## Releases

Depois que um pull request é integrado à `main`, o workflow de release aguarda o workflow `CI` desse commit em `main` terminar com sucesso. Em seguida ele:

1. resolve uma versão SemVer estável, começando pelo `VersionPrefix` e incrementando o patch nas releases seguintes;
2. empacota `RodriOliveira.AdrGuard` com essa versão;
3. publica o pacote no NuGet.org via Trusted Publishing (OIDC) e no GitHub Packages;
4. publica a imagem OCI multi-plataforma no GHCR e Docker Hub com tags exata/minor/major/`latest`, metadados OCI, SBOM e attestations de provenance;
5. verifica arquiteturas, attestations, digest da imagem exata e digest da imagem major móvel;
6. executa smoke test da resolução de runtime da Action pelas referências exata `vMAJOR.MINOR.PATCH` e major móvel `vMAJOR`;
7. cria ou verifica a tag exata imutável da Action e avança a tag major móvel somente depois que todos os jobs de publicação de runtime/pacotes terminarem com sucesso;
8. cria a GitHub Release e anexa o `.nupkg` sem sobrescrever um asset imutável já existente.

Uma falha na publicação do container não consegue expor uma nova tag da Action. Reexecuções preservam tags SemVer imutáveis e nunca movem uma tag major de compatibilidade para trás. Consulte a [política de release da GitHub Action](docs/github-action-release.pt-BR.md) para as regras completas de verificação e idempotência.

## Licença

Licenciado sob a [MIT License](LICENSE).
