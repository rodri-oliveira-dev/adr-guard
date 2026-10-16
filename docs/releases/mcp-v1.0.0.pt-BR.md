# Candidata a release do ADR Guard MCP v1.0.0

[English](mcp-v1.0.0.md)

Esta é a primeira release estável preparada do servidor ADR Guard MCP, versionado de forma independente. Ela não comprova que o pacote NuGet, a GitHub Release ou a entrada no Registry foram publicados. Estabilidade significa aqui que os sete nomes públicos de ferramentas, argumentos de inicialização, resultados estruturados no schema `1.0`, identidade do pacote, comando e transporte `stdio` foram revisados e cobertos por testes de contrato; não promete compatibilidade com futuras mudanças incompatíveis do protocolo ou do Registry.

## Incluído

- Sete ferramentas limitadas e somente leitura: `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact` e `verify_architecture`.
- Uma .NET Tool dependente do framework .NET 10 com Package ID `RodriOliveira.AdrGuard.Mcp` e comando `adr-guard-mcp`.
- Nome oficial no Registry `io.github.rodri-oliveira-dev/adr-guard` e README do pacote com o marcador de propriedade obrigatório.
- Transporte `stdio` local, um repositório fixo por processo, contenção canônica de caminhos, proteção contra escapes por symlink/reparse point, limites de arquivos/busca/evidências/respostas/tempo, cancelamento e erros sanitizados.
- Resultados estruturados de validação, impacto e drift que preservam estados inválidos, desconhecidos, inconclusivos e de falha de política sem afirmar aprovação arquitetural ou conformidade.
- Testes de instalação do pacote, handshake MCP real, inicialização, descoberta das sete ferramentas, invocação, entrada inválida, impacto, drift, encerramento, manifesto e contrato do arquivo `.nupkg`.

## Instalação após a publicação

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.0
adr-guard-mcp --repository /caminho/absoluto/do/repositorio --adr-directory docs/adr
```

Para um manifesto local de ferramentas:

```shell
dotnet new tool-manifest
dotnet tool install --local RodriOliveira.AdrGuard.Mcp --version 1.0.0
dotnet tool run adr-guard-mcp -- --repository /caminho/absoluto/do/repositorio --adr-directory docs/adr
```

O .NET 10 é obrigatório. Git é necessário somente para análise de impacto e metadados de drift associados a commit. O servidor não oferece transporte de rede, ferramentas de escrita, integração com providers, download automático, aprovação de ADRs nem produção de evidências.

## Pré-requisitos de publicação

Propriedade/trusted publishing no NuGet, ambiente protegido `mcp-release`, CI bem-sucedida no commit selecionado da `main`, tags imutáveis de reserva/release, verificação pública do pacote e validação/publicação no Registry continuam sendo pré-requisitos externos. Não descreva o MCP 1.0.0 como operacionalmente publicado antes que todas as etapas terminem e os identificadores públicos sejam verificados de forma independente.

Consulte a [visão geral](../mcp.pt-BR.md), a [configuração](../mcp-configuration.pt-BR.md), os [contratos das ferramentas](../mcp-tools.pt-BR.md), o [modelo de segurança](../mcp-security.pt-BR.md) e os [pré-requisitos de release](../mcp-release.pt-BR.md).
