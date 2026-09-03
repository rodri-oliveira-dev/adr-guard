# ADR Guard

[![CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/RodriOliveira.AdrGuard.svg)](https://www.nuget.org/packages/RodriOliveira.AdrGuard)
[![GitHub Release](https://img.shields.io/github/v/release/rodri-oliveira-dev/adr-guard)](https://github.com/rodri-oliveira-dev/adr-guard/releases)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
[![License](https://img.shields.io/github/license/rodri-oliveira-dev/adr-guard)](LICENSE)

[English](README.md)

ADR Guard é uma ferramenta de linha de comando para .NET focada em validar e indexar Architecture Decision Records (ADRs).

A proposta é permitir que convenções de ADR sejam explícitas, revisáveis e verificáveis tanto no desenvolvimento local quanto no CI, sem adicionar dependências pesadas em runtime.

## Recursos

- valida nomes de arquivos, títulos, status e seções obrigatórias;
- detecta IDs de ADR duplicados;
- detecta links relativos quebrados entre ADRs;
- exige um link válido em `Superseded by` para decisões substituídas;
- gera um índice Markdown determinístico;
- evita reescrever um índice que já está atualizado;
- fornece códigos de validação estáveis (`ADR001` até `ADR008`);
- fornece exit codes previsíveis para CI/CD;
- oferece criação assistida por IA de ADRs `Proposed`, com revisão humana, providers e contexto explícitos;
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

## Validar ADRs

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

O fluxo normal de persistência aloca deterministicamente o próximo ID de ADR, cria um filename compatível, valida o candidato gerado com o parser/validator normal de ADRs e grava usando semântica de criação de arquivo novo, impedindo a sobrescrita de um arquivo existente.

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

Os headings canônicos do ADR e o status `Proposed` permanecem inalterados independentemente da culture selecionada.

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

A numeração não precisa ser contínua. Lacunas são aceitas porque ADRs podem ser arquivados, migrados ou removidos sem renumerar decisões históricas.

## Exit codes

| Código | Significado |
| ---: | --- |
| `0` | Sucesso |
| `1` | Validação dos ADRs falhou |
| `2` | Uso inválido da CLI |
| `3` | Erro operacional |

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

Instalar localmente o pacote gerado:

```bash
dotnet tool install --tool-path ./.tools RodriOliveira.AdrGuard --version 0.1.0 --add-source ./artifacts/package
./.tools/adr-guard check docs/adr
```

## Decisões de arquitetura

O ADR Guard valida os próprios ADRs do projeto. Consulte [docs/adr](docs/adr/README.md).

O CI do repositório compila e testa a solução, empacota a .NET Tool, instala o pacote localmente, executa o `adr-guard` empacotado contra `docs/adr`, regenera o índice e verifica se houve drift na documentação.

## Recursos adicionais

Para quem quiser se aprofundar em Architecture Decision Records, há uma coleção de documentos, modelos e exemplos em português brasileiro:

- [Architecture Decision Record — documentação em português brasileiro](https://github.com/rodri-oliveira-dev/architecture-decision-record/blob/translation/pt-br/locales/pt-br/index.md)

A tradução para português brasileiro foi uma contribuição minha ao projeto `architecture-decision-record`.

## Releases

Depois que um pull request é integrado à `main`, o workflow de release aguarda o workflow `CI` desse commit em `main` terminar com sucesso. Em seguida ele:

1. resolve uma versão SemVer estável, começando pelo `VersionPrefix` e incrementando o patch nas releases seguintes;
2. empacota `RodriOliveira.AdrGuard` com essa versão;
3. autentica no NuGet.org via Trusted Publishing (OIDC) e publica o pacote;
4. publica o mesmo pacote no GitHub Packages;
5. cria a tag correspondente `vMAJOR.MINOR.PATCH` e a GitHub Release;
6. anexa o `.nupkg` à GitHub Release.

O workflow é idempotente para um commit que já possua uma tag de release.

## Licença

Licenciado sob a [MIT License](LICENSE).
