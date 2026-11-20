---
name: product-version-documentation
description: Use esta skill sempre que uma versão, candidata de release, tag, manifesto, pacote ou estado de publicação do CLI, MCP, extensão VS Code ou GitHub Action mudar. Sincroniza documentação, exemplos, changelogs e portal em inglês e pt-BR sem confundir código-fonte com publicação real.
license: MIT
---

# Sincronização de versões e documentação dos produtos

## Quando ativar

Ative antes de preparar ou revisar qualquer mudança de `VersionPrefix`, `extensions/vscode/package.json`, `.mcp/server.json`, tags `v*` ou `mcp/v*`, referência `@v1`, release notes, VSIX, NuGet, MCP Registry ou Marketplace. Também ative quando um artefato for **publicado posteriormente** e a documentação continuar marcando sua versão como candidata. Acione esta skill mesmo para atualizações somente documentais. Aplique junto com `repository-governance-sdd` apenas quando a tarefa também modificar instruções de agente.

Esta é uma skill de **contribuição ao repositório** em `.agents/skills/`. Não é uma das skills públicas `skills/adr-guard-*` distribuídas aos consumidores.

## Fontes de verdade e estados

| Produto | Versão do código-fonte | Prova de publicação |
| --- | --- | --- |
| CLI / .NET Tool | `src/AdrGuard/AdrGuard.csproj` (`VersionPrefix`) | Versão exata `RodriOliveira.AdrGuard` no NuGet.org e `vX.Y.Z` no GitHub Releases; verificar contêineres separadamente quando afirmados |
| GitHub Action | Coordenada com a release da CLI, `action.yml` e a imagem | Tag exata `vX.Y.Z`, tag móvel `v1` e imagem efetivamente correspondente; não inferir da versão do projeto |
| MCP / .NET Tool | `src/AdrGuard.Mcp/AdrGuard.Mcp.csproj` (`VersionPrefix`) | Versão exata do `RodriOliveira.AdrGuard.Mcp` no NuGet.org, `mcp/vX.Y.Z` e registro MCP; o manifesto `.mcp/server.json` descreve o estado do Registry |
| Extensão VS Code | `extensions/vscode/package.json` (`version`) | Item `rodrioliveira.adr-guard` no Visual Studio Marketplace; um VSIX produzido pela CI **não** comprova publicação |
| Architecture Agent | `src/AdrGuard.Agent` sem versão pública | Somente código-fonte até existir publicação independente verificada; não herda a versão da CLI |

Diferencie três situações: **candidata** (versão preparada no código, distribuição pública anterior), **publicada** (artefato comprovado no canal específico) e **somente código-fonte**. Quando a versão do projeto aumentar, **não** anuncie uma publicação nem atualize tags/Registry antecipadamente. Quando o artefato for publicado, execute uma segunda sincronização dos documentos de disponibilidade. GitHub Packages é feed secundário autenticado; não declare backfill publicado sem verificação própria.

## Matriz obrigatória de documentos por mudança

| Componente | Documentação a examinar e atualizar quando aplicável |
| --- | --- |
| CLI | `README.md`, `README.pt-BR.md`, `CHANGELOG.md`, `docs/cli-reference.md` e `.pt-BR.md`, `docs/compatibility.md` e `.pt-BR.md`, `docs/releases/vX.Y.Z.md` e `.pt-BR.md`, `website/src/content/docs/product/index.md` e `website/src/content/docs/pt-br/product/index.md` |
| Action e contêiner | `docs/github-action.md` e `.pt-BR.md`, `docs/github-action-release.md` e `.pt-BR.md` quando pertinente, `docs/examples/github-action-*.yml`, imagens/tags citadas, `Dockerfile` e workflows de publicação **somente se o contrato mudar** |
| MCP | `src/AdrGuard.Mcp/README.md`, `src/AdrGuard.Mcp/CHANGELOG.md`, `docs/mcp.md` e `.pt-BR.md`, `docs/mcp-configuration.md` e `.pt-BR.md`, `docs/mcp-release.md` e `.pt-BR.md`, `docs/compatibility.*`, `docs/releases/mcp-vX.Y.Z.*`, `website/src/content/docs/{product,pt-br/product}/index.md` e os comandos de instalação nos READMEs do VS Code |
| VS Code | `extensions/vscode/package.json` e `package-lock.json` quando o lock acompanhar o bump, `extensions/vscode/README.md` e `README.pt-BR.md`, `extensions/vscode/CHANGELOG.md`, `extensions/vscode/PUBLISHING.md`, `docs/releases/vscode-vX.Y.Z.md` e `.pt-BR.md`, `docs/compatibility.*`, portal bilíngue, exemplos de VSIX e testes de empacotamento |
| Agent | Manter `docs/architecture-agent.md` e `.pt-BR.md`, o README e a matriz corretos quanto ao estado **development-only**; não introduzir SemVer artificialmente |

Para qualquer caminho com `X.Y.Z`, use a versão efetiva daquela distribuição. Revise também referências de instalação, exemplos, requisitos **mínimos** de compatibilidade, changelogs e links. **Não** substitua indiscriminadamente versões históricas ou mínimas por versões atuais. Mantenha separadas as famílias SemVer de CLI, MCP e VS Code. Atualize ambas as línguas no **mesmo PR**.

## Procedimento

1. Leia `AGENTS.md` e identifique todos os arquivos de versão alterados, o tipo de release e a última versão comprovadamente pública de cada canal. Compare a branch com a base para distinguir alteração de versão de mudança apenas de status.
2. Confirme o estado externo antes de usar palavras como "publicado": NuGet.org, GitHub Releases, tag da Action, MCP Registry e Marketplace. Registre a data e o link verificado no PR. Um manifesto local, VSIX ou tag preparada **não** basta.
3. Escolha **candidata** ou **publicada** por componente e gere o mapa de impacto com a matriz acima. Inclua `Architecture Agent` como fonte-only salvo prova independente. Preserve o funcionamento de `@v1` e sua distinção da tag exata.
4. Atualize documentação de referência, README, changelog, notas de release, exemplos executáveis e páginas **fonte** do portal em inglês e pt-BR. Em `website`, execute `npm run sync:docs` em vez de editar páginas geradas manualmente. Corrija instruções de instalação que fixam versões antigas, mas preserve exemplos intencionalmente históricos.
5. Mantenha os contratos editoriais: marcadores `release-candidate-only:start/end` balanceados conforme `scripts/render-release-notes.sh`; blocos `vsix-package-status:start/end` e linhas esperadas por `extensions/vscode/scripts/package-vsix.mjs`. Ao trocar o estado candidata/publicada, atualize o teste e o empacotador **juntos**, sem afrouxar a verificação de versão.
6. Para release da CLI, confira `docs/examples/github-action-drift.yml` e `scripts/github-action-docs-test.sh`; a Action `@v1` só pode ser descrita como avançada depois de verificar a referência pública. Para release do MCP, sincronize o manifesto **somente com a versão efetivamente registrada no Registry**.
7. Execute a validação de documentação e contratos proporcional ao diff: `python3 scripts/validate-development-skills.py`, `python3 scripts/validate-version-documentation.py`, `npm run validate` em `website`, `bash scripts/github-action-docs-test.sh` quando a Action mudar, e os testes de VSIX quando a extensão mudar. Revise o PR e só indique testes como aprovados após execução concluída.
8. Registre no PR a matriz código-fonte / publicação / documentação, links de evidência, checks realizados e canais **não verificados**. Não faça release, push de pacote, merge ou movimento de tag sem pedido explícito.

## Critérios de aceite

- Mudanças em `VersionPrefix`, `package.json` ou estado de publicação são refletidas nos guias correspondentes **EN + pt-BR**, notas e matriz do mesmo PR.
- A versão candidata nunca é apresentada como instalada/publicada sem confirmação externa; distribuição pública anterior continua indicada até a promoção.
- Os testes de consistência impedem que versões de código-fonte desapareçam das páginas de referência, e a CI verifica as mudanças mesmo quando o PR alterar apenas metadados de versão.
- O README incluído em um pacote NuGet já publicado é imutável: a correção no repositório não altera retroativamente o pacote existente.
