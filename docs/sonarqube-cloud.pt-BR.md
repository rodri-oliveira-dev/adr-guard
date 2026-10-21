# SonarQube Cloud — qualidade opcional do repositório

[English](sonarqube-cloud.md) · [Contribuição](../AGENTS.md)

O SonarQube Cloud complementa os testes .NET, verificações de dependências e segurança, testes multiplataforma e políticas de publicação existentes no ADR Guard. **A integração é opcional e permanece desabilitada até sua configuração administrativa.** Resultados do Sonar não aprovam decisões arquiteturais, não alteram o status de ADRs e não substituem o validador determinístico.

## Habilitar a integração

1. Crie ou vincule um projeto no SonarQube Cloud. Consulte **a chave real do projeto e da organização** nas configurações do Sonar; não as deduza do nome do repositório GitHub.
2. Configure estas **Variables** em Settings → Secrets and variables → Actions:
   - `SONAR_CI_ENABLED`: `true` para habilitar análise e sincronização de findings; deixe ausente ou `false` durante a preparação.
   - `SONAR_PROJECT_KEY`: chave exata do projeto no SonarQube Cloud.
   - `SONAR_ORGANIZATION`: chave exata da organização no SonarQube Cloud.
3. Configure o **secret** `SONAR_TOKEN` com apenas as permissões necessárias para analisar o projeto e consultar Quality Gate/findings. Nunca versionar tokens.
4. Confirme a integração GitHub/SonarQube e as permissões. Habilite `SONAR_CI_ENABLED` **apenas após** preparar as demais configurações e confirmar que o projeto aceita análises. Verifique uma execução real antes de tornar o check obrigatório na proteção da branch.

O workflow de análise é [`.github/workflows/sonar.yml`](../.github/workflows/sonar.yml). Analisa PRs **internos** para `main`, pushes na `main` e execuções manuais na `main`. **PRs de forks não executam análise privilegiada nem recebem secrets.** Testes unitários da automação são executados por um job sem privilégios mesmo quando o Sonar está desabilitado. Os workflows de release permanecem intactos.

## Cobertura e Quality Gate

- `.config/dotnet-tools.json` fixa versões de `dotnet-sonarscanner` e `dotnet-coverage`; `dotnet tool restore` instala as ferramentas locais.
- `eng/sonar-coverage.settings.xml` seleciona os assemblies de produção `AdrGuard.Core.dll`, `adr-guard.dll` e `adr-guard-mcp.dll`, excluindo testes, benchmarks, código gerado, assemblies não relacionados e a automação em `eng/**` do gate de cobertura, sem retirar essa automação da análise estática.
- `eng/run-sonar-tests.ps1` executa a suíte da solução pelo **Microsoft.Testing.Platform nativo** (xUnit v3), sem filtros VSTest ou chamadas reais a provedores de IA.
- O XML de cobertura Visual Studio é salvo em `artifacts/coverage/sonar/coverage.xml` e verificado quanto aos três assemblies antes do envio ao Sonar.
- Nos PRs, o scanner aguarda o Quality Gate. O workflow resolve a tarefa do Compute Engine da análise atual e consulta o gate pelo respectivo `analysisId`; assim, um gate reprovado é apresentado com suas condições mesmo quando o scanner termina com falha. Um único comentário do bot é sempre atualizado; ausência de evidência da tarefa, falhas de API/rede ou respostas malformadas são indicadas como `UNKNOWN`, nunca como sucesso ou como resultado desatualizado.
- A análise Sonar é independente da CI regular e da publicação. Após ativação, falhas aparecerão como checks separados. Só os torne obrigatórios depois da estabilização.

O workflow **não** envia cobertura ao Codecov nem publica badges de cobertura/Quality Gate. Adicione-os somente após verificar o serviço e o projeto correspondente.

## Findings HIGH/BLOCKER → GitHub Issues

O workflow [`sonar-quality-issues.yml`](../.github/workflows/sonar-quality-issues.yml) executa às segundas-feiras ou manualmente na `main`, **somente com `SONAR_CI_ENABLED=true`**. Consulta findings OPEN com impacto HIGH/BLOCKER e sincroniza issues com a label `Sonar Quality Issues`.

- Cada finding possui chave estável do Sonar e bloco Markdown gerenciado pela automação. Uma issue só é gerenciada quando **foi criada pelo bot e contém marcadores válidos**.
- Atualizações são idempotentes e preservam o texto fora dos marcadores; findings resolvidos/desaparecidos **não são fechados automaticamente**.
- Erros de autenticação/API interrompem a automação em vez de informar falsamente ausência de findings. Nunca faça testes com credenciais de exemplo no tracker de produção.
- O workflow desabilitado não modifica issues. Anotações e issues criadas manualmente são preservadas.

Teste offline: `node --test scripts/tests/sonar-quality-sync.test.cjs scripts/tests/sonar-quality-gate.test.cjs`. Verifica criação, repetição, preservação de notas, autoria, relato de reprovação do Quality Gate e falhas da API sem acessar serviços externos.

## Solução de problemas

| Sintoma | Verificar |
| --- | --- |
| Análise ignorada | `SONAR_CI_ENABLED=true`; PR interno; evento e branch permitidos |
| Configuração ausente | Variables `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION` e secret `SONAR_TOKEN` |
| Assembly ausente na cobertura | Build Release, comando de testes nativo, XML e `ModulePaths` |
| Quality Gate desconhecido | Resultado do scanner, processamento do Sonar e permissões de API |
| Nenhuma issue sincronizada | Findings OPEN com impacto HIGH/BLOCKER e workflow habilitado |

Não inventar chave do projeto, percentual de cobertura, resultado de Quality Gate, findings ou badges. Essa configuração automatiza o repositório; ela **não altera versões de release nem o comportamento público da CLI, MCP ou VS Code**.
