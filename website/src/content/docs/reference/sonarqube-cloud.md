---
title: SonarQube Cloud repository checks
description: Optional Sonar analysis, .NET coverage, PR Quality Gate and controlled GitHub Issue synchronization.
sidebar:
  order: 30
---

The ADR Guard repository includes **opt-in** SonarQube Cloud quality automation, kept separate from the released CLI, MCP and VS Code products. It remains disabled until the GitHub Variables and secret are configured by a maintainer.

Set `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION` and `SONAR_CI_ENABLED=true` as repository Variables, plus `SONAR_TOKEN` as a secret. The main analysis workflow performs .NET test coverage, scans trusted PRs and `main`, and reports Quality Gate conditions. The separate weekly/manual workflow can synchronize HIGH/BLOCKER findings to bot-managed GitHub Issues without closing resolved ones automatically.

[Full configuration, permissions, validation and troubleshooting](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/sonarqube-cloud.md).

No public badges or quality metrics are asserted until the Sonar project is configured and its results independently verified.
