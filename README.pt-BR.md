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

## Contexto de ADRs existentes para rascunhos com IA

Os ADRs existentes **não** são enviados a um provedor de IA por padrão. Para incluir explicitamente dados parseados dos ADRs como contexto de geração, use:

```bash
adr-guard draft docs/adr \
  --title "Adotar um message broker" \
  --context "Precisamos de integração assíncrona." \
  --provider openai \
  --model <modelo> \
  --include-existing-adrs
```

Quando habilitado, o ADR Guard monta um contexto compacto a partir dos dados parseados dos ADRs, em vez de concatenar arquivos do repositório. Cada ADR selecionado contribui com ID, título, status, decisão e relacionamentos Markdown locais. Os ADRs são ordenados pelo ID numérico e, em seguida, pelo nome do arquivo.

O contexto dos ADRs existentes é limitado de forma determinística a **12.000 caracteres**. O ADR Guard adiciona representações completas dos ADRs nessa ordem enquanto elas couberem no limite; quando o próximo ADR completo ultrapassaria o limite, ele e todos os ADRs seguintes são omitidos. Essa estratégia nunca trunca parcialmente os campos de um ADR.

A opção é propositalmente opt-in porque o conteúdo selecionado dos ADRs é enviado ao provedor externo de IA configurado.

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
