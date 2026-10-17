# Distribuição MCP e pré-requisitos de release

[English](mcp-release.md) · [Visão geral do MCP](mcp.pt-BR.md)

O workflow `MCP Release` (`.github/workflows/mcp-release.yml`) é independente da release da CLI. Ele recebe um único SemVer estável, executa apenas por acionamento manual na `main`, exige CI bem-sucedido para o commit escolhido e deriva desse valor as versões do pacote, manifesto, tag, GitHub Release e Registry. Não o acione antes de concluir a configuração abaixo.

O MCP `1.0.0` foi publicado no [NuGet.org](https://www.nuget.org/packages/RodriOliveira.AdrGuard.Mcp/1.0.0), em uma [GitHub Release própria](https://github.com/rodri-oliveira-dev/adr-guard/releases/tag/mcp/v1.0.0) e no MCP Registry. O GitHub Packages é um feed NuGet secundário e autenticado; a disponibilidade da versão `1.0.0` nesse canal depende da execução do workflow de backfill após o merge desta mudança.

## Administração única obrigatória

1. No NuGet.org, confirme que `rodri-oliveira-dev` possui ou pode criar `RodriOliveira.AdrGuard.Mcp`.
2. Em **Trusted Publishing** desse pacote, adicione uma policy do GitHub Actions para owner `rodri-oliveira-dev`, repositório `adr-guard`, workflow `mcp-release.yml` e environment `mcp-release`. Um pacote novo pode exigir a ativação de policy pendente do NuGet.org; a policy da CLI não cobre automaticamente este Package ID.
3. No GitHub, crie o environment protegido `mcp-release`, restrinja-o à `main` e configure revisores obrigatórios para os jobs irreversíveis.
4. Confirme que a proteção da branch exige o workflow `CI` completo na `main` e permita que Actions crie tags `mcp/v*`, `mcp-release-reservation/v*` e Releases.

Nenhum PAT ou token persistente MCP/NuGet é necessário. NuGet e MCP Registry recebem `id-token: write` somente nos respectivos jobs protegidos de publicação.

## Ordem, artefatos e recuperação

O workflow compila um `.nupkg` dependente do framework, valida o marcador do README e dependências de runtime, gera checksums SHA-256 e o instala de fonte isolada em Windows, Linux e macOS. Ele reserva a versão, publica com OIDC do NuGet, verifica e instala o pacote público, além de publicar o mesmo candidato validado por checksum no GitHub Packages usando o `GITHUB_TOKEN` com permissões restritas. O fluxo principal também cria `mcp/v<versão>` e a GitHub Release e, por fim, autentica no Registry oficial com OIDC do GitHub. Uma falha no canal secundário não contorna as verificações do NuGet.org nem os controles da GitHub Release/Registry; o resumo da release é executado mesmo com falha em algum job e indica o status real de cada canal.

Reexecuções preservam versão e SHA originais. A etapa de reserva distingue HTTP `404` (criar uma nova tag) de HTTP `200` (verificar o commit exato da tag existente) e interrompe a publicação em erros de autorização, transporte ou servidor. As respostas da API do GitHub são limitadas a 64 KiB, e o token temporário é transmitido ao curl pela configuração na entrada padrão, nunca como argumento do processo. Tags de reserva/release existentes devem apontar para esse SHA. Versões NuGet e Registry existentes devem comprovar metadados equivalentes ou o workflow falha de forma fechada. Etapas posteriores ausentes podem ser retomadas; artefatos imutáveis e tags nunca são sobrescritos ou movidos.

Os assets são o `.nupkg` MCP, `SHA256SUMS`, `server.json` validado e notas específicas do MCP. O pacote não é binário nativo autossuficiente. Disponibilidade preview e mudanças de schema do Registry continuam riscos externos, e workflows de PR/push nunca publicam.

## GitHub Packages: publicação retroativa da release existente

Como o MCP `1.0.0` foi lançado antes da integração com o GitHub Packages, **não execute novamente `MCP Release` a partir de um commit mais recente para publicar retroativamente a versão `1.0.0`**: as tags imutáveis de release e de reserva pertencem ao commit original. Depois do merge deste PR e de CI aprovado na `main`, execute manualmente **MCP GitHub Packages Backfill** (`.github/workflows/mcp-github-packages-backfill.yml`) na branch `main` com `version=1.0.0`, aprovando o ambiente protegido `mcp-release` se solicitado.

O backfill verifica a tag e o SHA originais, baixa o `.nupkg` e o `SHA256SUMS` da GitHub Release existente, confere checksum e metadados do pacote com o commit original e verifica ID/versão/commit do pacote publicado no NuGet.org. Somente depois publica o pacote original no feed `https://nuget.pkg.github.com/rodri-oliveira-dev/index.json` com `packages: write` e `NUGET_API_KEY` no ambiente (nunca na linha de comando). O workflow consulta primeiro o feed autenticado: uma versão existente só é aceita quando seu `.nupkg` baixado possui o **mesmo SHA-256 do artefato original**; versões ausentes são publicadas sem `--skip-duplicate` e verificadas pelo download e checksum do registro. Conflitos, divergências, falhas de autorização ou disponibilidade inconclusiva bloqueiam a publicação. O NuGet.org pode assinar o pacote, por isso não se exige igualdade byte a byte entre registries. Não há nova compilação, movimentação de tags, publicação de outra versão no NuGet.org nem alteração no MCP Registry. As próximas versões do MCP usarão automaticamente o workflow principal para publicar no GitHub Packages.

GitHub Packages é um **feed secundário autenticado**. O consumidor precisa de acesso/credenciais apropriadas; o NuGet.org continua sendo a fonte de instalação recomendada. Após a primeira publicação, confirme a visibilidade do pacote e seu vínculo ao repositório no GitHub Packages. A extensão VS Code é distribuída separadamente pelo Visual Studio Marketplace e não aparece no GitHub Packages.
