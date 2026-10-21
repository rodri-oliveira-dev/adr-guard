---
title: Verificações SonarQube Cloud do repositório
description: Análise Sonar opcional, cobertura .NET, Quality Gate em PRs e sincronização controlada com GitHub Issues.
sidebar:
  order: 30
---

O repositório ADR Guard dispõe de automação de qualidade SonarQube Cloud **opcional**, independente dos produtos CLI, MCP e VS Code publicados. Ela permanece desabilitada até a configuração de Variables e secret por um mantenedor.

Configure `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION` e `SONAR_CI_ENABLED=true` como Variables do repositório e `SONAR_TOKEN` como secret. O workflow de análise mede a cobertura .NET de produção, mantém a automação do repositório sob análise estática sem aplicar a ela o gate de cobertura, verifica PRs confiáveis e a `main` e apresenta as condições do Quality Gate da análise atual. Evidência ausente ou inválida atualiza o comentário do PR como `UNKNOWN`, sem preservar um resultado desatualizado. Outro workflow, semanal ou manual, pode sincronizar findings HIGH/BLOCKER em issues gerenciadas pelo bot, sem fechamento automático de findings resolvidos.

[Guia completo de configuração, permissões, validação e troubleshooting](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/sonarqube-cloud.pt-BR.md).

Não são exibidos badges ou resultados públicos de qualidade até que o projeto Sonar esteja configurado e seus dados sejam verificados.
