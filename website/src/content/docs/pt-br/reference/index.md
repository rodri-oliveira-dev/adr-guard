---
title: Contratos e referência
description: Esquemas versionados de relatório, limites de segurança, comportamento da Action e contratos de manutenção.
sidebar:
  order: 1
---

Páginas de referência descrevem contratos e limites operacionais, não o percurso de aprendizagem.

- [Rastreabilidade entre implementação e documentação](./generated/documentation-audit/)
- [Política de revisão por IA v1](./generated/review-policy-v1/)
- [Segurança da GitHub Action](./generated/github-action-security/)
- [Modo de revisão da GitHub Action](./generated/github-action-review/)
- [Processo de release da GitHub Action](./generated/github-action-release/)
- [Verificação externa](./generated/github-action-external-verification/)
- [Verificações SonarQube Cloud](./sonarqube-cloud/)

O [contrato do relatório de revisão por IA v1](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/adr-review-report-v1.md) ainda não possui tradução de origem; por isso não há fallback silencioso no portal. Os esquemas JSON continuam versionados no repositório. Trate a fonte e a versão instalada da CLI como autoritativas ao integrar saídas estruturadas.

## Contratos de impacto arquitetural

- [Schema v1 do manifesto de mapeamento ADR-código](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-impact-map-v1.schema.json)
- [Schema v1 do relatório de impacto arquitetural](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-impact-report-v1.schema.json)
- [Guia de Análise de Impacto Arquitetural](../product/architecture-impact/)

## Contratos de drift arquitetural

- [Contrato de regras de fitness v1](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/fitness-rules-v1.md) e [schema](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-fitness-rules-v1.schema.json)
- [Schema v1 de evidências externas de fitness](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-fitness-evidence-v1.schema.json)
- [Schema v1 de snapshot/baseline de drift](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-drift-snapshot-v1.schema.json)
- [Schema v1 do relatório de drift](https://github.com/rodri-oliveira-dev/adr-guard/blob/main/docs/schemas/adr-drift-report-v1.schema.json)
- [Guia de Drift Arquitetural](../product/architectural-drift/)

Esses contratos são distintos. Relatórios de impacto indicam candidatos à revisão a partir de mapeamentos explícitos de arquivos; relatórios de drift avaliam regras de fitness declaradas com evidências limitadas. Nenhum dos contratos registra aprovação humana.
