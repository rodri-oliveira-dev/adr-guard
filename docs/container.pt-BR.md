# Imagem de container do ADR Guard

O ADR Guard é publicado como imagem OCI multi-plataforma em dois registries:

- GitHub Container Registry (GHCR): `ghcr.io/rodri-oliveira-dev/adr-guard`
- Docker Hub: `docker.io/rodrigodotnet/adr-guard`

Os dois registries recebem o mesmo build de release para `linux/amd64` e `linux/arm64`.

## Tags

Releases estáveis publicam quatro níveis de tag:

```text
1.2.3
1.2
1
latest
```

Para automações reproduzíveis, prefira a tag SemVer exata ou, quando for necessária imutabilidade estrita, fixe a imagem pelo digest.

## Baixar a imagem

GHCR:

```bash
docker pull ghcr.io/rodri-oliveira-dev/adr-guard:latest
```

Docker Hub:

```bash
docker pull rodrigodotnet/adr-guard:latest
```

## Validar ADRs

Monte o repositório em `/workspace`, que é o diretório de trabalho da imagem:

```bash
docker run --rm \
  -v "$PWD:/workspace:ro" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  check docs/adr
```

A validação pode usar um volume somente leitura porque `check` não altera arquivos do repositório.

## Gerar o índice de ADRs

`index` escreve no repositório montado. Em Linux e macOS, usar o UID/GID do host evita diferenças de ownership nos arquivos gerados:

```bash
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -v "$PWD:/workspace" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  index docs/adr
```

A imagem já executa como usuário não-root por padrão. O `--user` explícito acima serve apenas para alinhar o ownership de volumes graváveis do host quando necessário.

## Criação assistida por IA

As credenciais dos providers devem ser fornecidas em runtime por variáveis de ambiente. Elas nunca são incorporadas à imagem.

Exemplo com OpenAI:

```bash
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e OPENAI_API_KEY \
  -v "$PWD:/workspace" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  draft docs/adr \
  --title "Adotar um message broker" \
  --context "Precisamos de integração assíncrona." \
  --provider openai \
  --model <modelo-openai>
```

As mesmas regras de variáveis de ambiente documentadas para a .NET Tool valem para Anthropic, Gemini e providers OpenAI-compatible.

## Controles de supply chain

O caminho do container é protegido por controles independentes:

- Hadolint é um quality gate obrigatório do `Dockerfile` no CI;
- Dependabot monitora as imagens-base .NET referenciadas pelo `Dockerfile`;
- o CI faz build e smoke tests da imagem antes que uma release possa executar;
- o Trivy analisa a imagem gerada no CI em busca de vulnerabilidades corrigíveis `HIGH` e `CRITICAL` em pacotes do sistema operacional e bibliotecas, falhando o workflow quando encontra alguma;
- a imagem de runtime usa a variante chiseled do .NET e executa como usuário não-root;
- imagens de release recebem metadados OCI de origem, documentação, autor, licença, versão e revisão;
- o BuildKit publica uma attestation de SBOM e uma attestation de provenance em `mode=max` para cada release multi-plataforma;
- o workflow de release verifica a presença dos manifests `linux/amd64`, `linux/arm64` e dos manifests de attestation tanto no GHCR quanto no Docker Hub.

O SBOM e a provenance são attestations OCI associadas ao índice da imagem publicada, e não arquivos embutidos no filesystem de runtime.

## Inspecionar uma release

Para inspecionar o manifest multi-plataforma e as attestations associadas:

```bash
docker buildx imagetools inspect \
  ghcr.io/rodri-oliveira-dev/adr-guard:1.2.3
```

Para usar uma referência imutável, resolva o digest e fixe a imagem:

```text
ghcr.io/rodri-oliveira-dev/adr-guard@sha256:<digest>
```

## Fluxo de release

As imagens de container só são publicadas depois que o workflow de CI do commit validado na `main` termina com sucesso. Em seguida, o workflow de release resolve a mesma versão SemVer usada pelo pacote NuGet, cria/verifica a Git tag, publica a imagem multi-plataforma no GHCR e no Docker Hub, valida os manifests e as attestations publicados e somente então conclui a GitHub Release.
