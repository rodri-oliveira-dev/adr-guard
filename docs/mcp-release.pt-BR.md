# Distribuição MCP e pré-requisitos da primeira release

[English](mcp-release.md) · [Visão geral do MCP](mcp.pt-BR.md)

O workflow `MCP Release` (`.github/workflows/mcp-release.yml`) é independente da release da CLI. Ele recebe um único SemVer estável, executa apenas por acionamento manual na `main`, exige CI bem-sucedido para o commit escolhido e deriva desse valor as versões do pacote, manifesto, tag, GitHub Release e Registry. Não o acione antes de concluir a configuração abaixo.

O repositório está preparado para a candidata `1.0.0`, mas essa versão não está publicada. Trate o contrato como operacionalmente estável somente após o workflow protegido concluir a verificação do pacote, a instalação pública, a publicação no Registry e as verificações pós-publicação.

## Administração única obrigatória

1. No NuGet.org, confirme que `rodri-oliveira-dev` possui ou pode criar `RodriOliveira.AdrGuard.Mcp`.
2. Em **Trusted Publishing** desse pacote, adicione uma policy do GitHub Actions para owner `rodri-oliveira-dev`, repositório `adr-guard`, workflow `mcp-release.yml` e environment `mcp-release`. Um pacote novo pode exigir a ativação de policy pendente do NuGet.org; a policy da CLI não cobre automaticamente este Package ID.
3. No GitHub, crie o environment protegido `mcp-release`, restrinja-o à `main` e configure revisores obrigatórios para os jobs irreversíveis.
4. Confirme que a proteção da branch exige o workflow `CI` completo na `main` e permita que Actions crie tags `mcp/v*`, `mcp-release-reservation/v*` e Releases.

Nenhum PAT ou token persistente MCP/NuGet é necessário. NuGet e MCP Registry recebem `id-token: write` somente nos respectivos jobs protegidos de publicação.

## Ordem, artefatos e recuperação

O workflow compila um `.nupkg` dependente do framework, valida o marcador do README e dependências de runtime, gera checksums SHA-256 e o instala de fonte isolada em Windows, Linux e macOS. Ele reserva a versão, publica com OIDC do NuGet, verifica e instala o pacote público, cria `mcp/v<versão>` e a GitHub Release e, por fim, autentica no Registry oficial com OIDC do GitHub.

Reexecuções preservam versão e SHA originais. Tags de reserva/release existentes devem apontar para esse SHA. Versões NuGet e Registry existentes devem comprovar metadados equivalentes ou o workflow falha de forma fechada. Etapas posteriores ausentes podem ser retomadas; artefatos imutáveis e tags nunca são sobrescritos ou movidos.

Os assets são o `.nupkg` MCP, `SHA256SUMS`, `server.json` validado e notas específicas do MCP. O pacote não é binário nativo autossuficiente. Disponibilidade preview e mudanças de schema do Registry continuam riscos externos, e workflows de PR/push nunca publicam.
