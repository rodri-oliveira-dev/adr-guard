# ADR Guard MCP v1.0.1

[English](mcp-v1.0.1.md)

<!-- release-candidate-only:start -->
O ADR Guard MCP `1.0.1` está preparado no código-fonte, mas, em 20/10/2026, **não está publicado** no NuGet.org, em GitHub Releases, no GitHub Packages nem no MCP Registry. A release MCP pública atual continua sendo a `1.0.0`. Esta candidata patch altera a composição interna e corrige o tratamento de plataforma, preservando todos os contratos públicos do MCP.
<!-- release-candidate-only:end -->

## Principais mudanças

- Referencia diretamente o projeto interno `AdrGuard.Core` para parsing, validação, configuração, inventário Git, impacto e drift compartilhados.
- Remove qualquer dependência de build ou runtime no executável da CLI ADR Guard. O pacote inclui `adr-guard-mcp` e `AdrGuard.Core.dll` e exclui `adr-guard.dll`.
- Mantém as sete ferramentas somente leitura no schema `1.0` por `stdio` local: `list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact` e `verify_architecture`.

## Correções e melhorias internas

- Resolve identidades físicas de diretórios Git antes da contenção para que aliases `/var` e `/private/var` do macOS não causem falsas violações dos limites do repositório.
- Mantém ordenação, fingerprints, validação, impacto e drift determinísticos compartilhados com a CLI por meio do Core, sem publicar o Core como pacote separado.
- Reforça verificações do grafo e do arquivo de pacote para impedir referência do MCP à CLI ou inclusão do executável oposto.
- Preserva o fluxo de release manual e fail closed, com reserva de versão independente, smoke tests do pacote local, verificação de proveniência e validação do Registry.

## Compatibilidade e requisitos

- Exige .NET 10 e um cliente MCP local compatível com `stdio`.
- Nomes das ferramentas, argumentos de inicialização, schema `1.0`, campos estruturados, limites, semântica de erros, transporte e anotações somente leitura permanecem iguais à versão `1.0.0`.
- Git continua opcional e é usado somente por `analyze_impact` e pelos metadados associados a commit em `verify_architecture`.
- O servidor independe da CLI e da extensão VS Code. O VS Code `1.1.0` pode registrá-lo quando a integração MCP é habilitada explicitamente e um caminho separado para o executável é configurado.

## Mudanças de comportamento

Não há mudança pública intencional. A correção do macOS evita rejeitar um repositório válido apenas porque o sistema operacional expõe aliases equivalentes do caminho físico. Evidência ausente, malformada, fora do escopo ou não verificável continua falhando de forma fechada ou permanecendo `unknown`/inconclusiva.

## Limitações conhecidas

- O servidor não possui transporte de rede, ferramentas de escrita, serviço hospedado, integração com provider, aprovação autônoma nem produção de evidências.
- Ele não executa builds, testes, scripts ou `git fetch` e não pode criar, editar, aprovar nem alterar o status de ciclo de vida de uma ADR.
- O `.mcp/server.json` versionado registra a última entrada do Registry verificada de forma independente. O workflow protegido deriva a versão selecionada do commit validado da release e valida o manifesto gerado antes da publicação no Registry.

## Instalar ou atualizar

<!-- release-candidate-only:start -->
Antes da publicação, instale somente o pacote local validado:

```shell
dotnet tool install --tool-path ./.mcp-tools RodriOliveira.AdrGuard.Mcp \
  --version 1.0.1 --add-source ./artifacts/mcp
./.mcp-tools/adr-guard-mcp --repository /caminho/absoluto/do/repo --adr-directory docs/adr
```

Continue usando `1.0.0` nas instalações pelo NuGet público até que `1.0.1` esteja visível de forma independente no NuGet.org e no MCP Registry.
<!-- release-candidate-only:end -->

Quando o NuGet.org listar a versão exata, instale-a com:

```shell
dotnet tool install --global RodriOliveira.AdrGuard.Mcp --version 1.0.1
adr-guard-mcp --repository /caminho/absoluto/do/repo --adr-directory docs/adr
```

<!-- release-candidate-only:start -->
## Checklist pós-publicação

Depois que a publicação for explicitamente autorizada e concluída:

- verifique `1.0.1` no NuGet.org, a proveniência e o conteúdo do pacote e uma instalação limpa;
- verifique a GitHub Release imutável `mcp/v1.0.1` e seus assets;
- verifique se a entrada oficial do Registry identifica `1.0.1`, aponta para o mesmo pacote NuGet e continua declarando `stdio`;
- verifique o GitHub Packages separadamente; não execute o backfill histórico de `1.0.0` como parte desta release;
- somente então atualize `.mcp/server.json`, defaults de instalação, estado no changelog, READMEs, matriz de compatibilidade e fontes do portal de candidata para publicada.
<!-- release-candidate-only:end -->
