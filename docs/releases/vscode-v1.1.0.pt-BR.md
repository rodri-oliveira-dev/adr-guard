# ADR Guard para VS Code 1.1.0 — Candidata no código-fonte (não publicada)

[English](vscode-v1.1.0.md)

A candidata **ADR Guard para VS Code 1.1.0**, versionada de forma independente, está disponível no código-fonte, mas **não foi publicada como atualização 1.1.0 no Visual Studio Marketplace**. A versão eventualmente já publicada no Marketplace é independente desta candidata. Uma execução bem-sucedida no CI ou um artifact VSIX disponível para download não significam publicação no Marketplace.

## Novidades desta candidata

- Fluxo visual para consultar, criar, editar, configurar e validar ADRs, analisar impacto e drift, gerenciar baselines e catálogos enriquecidos, além de recursos opcionais de rascunho e revisão com IA.
- Provider opcional de registro MCP, desabilitado por padrão, que exige um servidor MCP compatível instalado de forma independente.
- Interface bilíngue e limites explícitos de execução, permissões e confiança do workspace.

Para recursos e requisitos da CLI, consulte o [README da extensão](../../extensions/vscode/README.pt-BR.md) e a [matriz de compatibilidade](../compatibility.pt-BR.md). A CLI 1.8.0 é a versão recomendada para o fluxo visual completo; o MCP 1.0.0 é independente e não deve ser considerado publicado sem verificação.

## Download do VSIX validado (prazo de 21 dias)

1. Depois do merge das alterações de empacotamento na `main`, abra [Actions — VS Code VSIX Package](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension-release.yml).
2. Selecione **Run workflow** na `main`. O workflow lê a versão em `extensions/vscode/package.json`; não exige confirmação de publicação nem credenciais do Marketplace.
3. Aguarde o sucesso das verificações e do empacotamento e abra **Summary → Artifacts** na execução.
4. Baixe `adr-guard-vscode-1.1.0` (ou a versão identificada pelo manifesto em uma execução mais recente), extraia o ZIP e localize `adr-guard-1.1.0.vsix` (ou a versão correspondente).
5. No VS Code, use **Extensions: Install from VSIX...** ou execute `code --install-extension adr-guard-1.1.0.vsix` no diretório extraído.

A retenção configurada é de **21 dias por execução bem-sucedida**, sujeita ao limite máximo permitido pelo repositório. Artifacts de execuções anteriores expiram de forma independente; a candidata empacotada mais recente é identificada pela **última execução bem-sucedida e pela versão do artifact**, não pela página histórica de deployments. O [VS Code Extension CI](https://github.com/rodri-oliveira-dev/adr-guard/actions/workflows/vscode-extension.yml) também gera o artifact com versão dinâmica nas execuções aplicáveis, com retenção de 21 dias.

## Estado da publicação no Marketplace

Os workflows de empacotamento e CI **nunca executam `vsce publish`**, solicitam credenciais de publicação nem fazem deployment para o ambiente `vscode-marketplace`. Para distribuir uma nova versão pelo Marketplace, o projeto precisaria introduzir um **workflow separado e explicitamente autorizado**. Versões anteriormente publicadas no Marketplace não são afetadas pelas alterações no empacotamento.

Consulte o [guia de empacotamento](../../extensions/vscode/PUBLISHING.md) para o procedimento completo.
