# Tarefas: Architecture Agent

As tarefas abaixo registram o backlog e a implementação incremental. Itens marcados comprovam somente o comportamento presente no código-fonte; nenhum deles autoriza alteração de contratos CLI/MCP/Core, publicação ou aprovação do ADR.

## Dependências e marcos

- `M0` deve terminar antes de código de produto.
- `M1` é somente leitura e precede qualquer integração real com modelo.
- `M2` precede proposta/aprovação.
- `M3` deve passar com CLI fake antes de `M4` habilitar escrita real.
- `M5` somente começa quando os critérios de segurança e compatibilidade anteriores estiverem atendidos.

## M0 — Decisão detalhada e segurança

- [x] **T001** Resolver e registrar as cinco dúvidas abertas da especificação com responsáveis e data.
- [ ] **T002** Submeter o ADR `0009` à revisão humana; manter `Proposed` até decisão explícita.
- [x] **T003** Definir máquina de estados de sessão, evidência, proposta, aprovação, execução, falha e cancelamento.
- [x] **T004** Produzir diagramas de sequência dos fluxos de leitura, proposta, aprovação, escrita e releitura.
- [x] **T005** Elaborar threat model para prompt injection, exfiltração, confused deputy, TOCTOU, processo adulterado, isolamento multi-repositório e logs.
- [x] **T006** Definir política de dados por provider: classes transmitidas, retenção, residência, redação e consentimento.
- [x] **T007** Escolher a allowlist mínima de comandos CLI e justificar cada entrada; não incluir mudança de lifecycle na primeira entrega.
- [x] **T008** Congelar a matriz inicial de compatibilidade MCP schema `1.0` e versões CLI publicadas.

## M1 — Agente local somente leitura

- [x] **T009** Criar o projeto executável .NET opcional sem referência de assembly a Core, CLI ou MCP.
- [x] **T010** Implementar configuração confiável de raiz, executável MCP, timeout e diretório de ADRs.
- [x] **T011** Implementar lifecycle de um processo MCP `stdio` por repositório, com cancelamento e limites.
- [x] **T012** Implementar validação estrita dos envelopes e schema `1.0` consumidos.
- [x] **T013** Implementar adapters para as sete ferramentas MCP existentes, preservando proveniência e conteúdo não confiável.
- [x] **T014** Implementar apresentação separada de evidência, incerteza e sugestão; o primeiro fluxo funcional também identifica explicitamente a interpretação remota como não confiável.
- [x] **T015** Adicionar testes de contrato, timeout, cancelamento, resposta excessiva, dados malformados e isolamento.
- [x] **T016** Adicionar teste de arquitetura que rejeite referências de assembly proibidas.

## M2 — Integração opcional com modelo

- [x] **T017** Definir porta de provider com orçamento de tokens, cancelamento e metadados de proveniência; adaptar OpenAI por `Microsoft.Extensions.AI` sem fechar a porta para providers futuros.
- [x] **T018** Implementar seleção explícita do contexto MCP e confirmação `--allow-remote-context` para transmitir pergunta e evidências selecionadas.
- [x] **T019** Impedir tool calling direto pelo modelo; somente o orquestrador determinístico pode selecionar operações do catálogo fechado.
- [x] **T020** Implementar distinção visual/estrutural entre evidência MCP, interpretação consultiva, referências, trade-offs, incertezas e lacunas; propostas permanecem fora deste fluxo somente leitura.
- [x] **T021** Adicionar testes offline com provider fake para prompt injection, tentativa de adicionar tool call, referência inventada, segredo sentinela e indisponibilidade, sem rede nem consumo externo.
- [x] **T022** Realizar revisão de privacidade e segurança do primeiro provider real, incluindo audit NuGet, credencial por ambiente, contexto autorizado, prompt injection, allowlist e sanitização da apresentação.

## M3 — Proposta e aprovação sem escrita

- [ ] **T023** Definir modelo imutável e serialização local limitada de proposta, sem registrar payloads sensíveis por padrão.
- [x] **T024** Implementar preview/diff e exibição do executável, argumentos, alvo, efeitos e riscos.
- [x] **T025** Implementar gate de aprovação explícita vinculado a ID, conteúdo, alvo, argumentos e fingerprint.
- [x] **T026** Invalidar aprovação em qualquer mudança material, timeout configurado ou alteração do estado observado.
- [ ] **T027** Implementar recusa, edição e cancelamento sem efeitos.
- [ ] **T028** Implementar trilha sanitizada de proposta, decisão humana e resultado.
- [x] **T029** Provar com adaptador CLI fake que `describe` e `preview` não executam `operation=create` sem aprovação válida.

## M4 — Escrita mínima via CLI

- [x] **T030** Implementar adapter de processo CLI sem shell, com caminho confiável, argumentos discretos, timeout, cancelamento e limites de saída.
- [x] **T031** Implementar somente o primeiro comando aprovado em T007, preferindo criação estruturada de ADR `Proposed`.
- [ ] **T032** Preservar e apresentar literalmente a semântica dos exit codes `0` a `4`.
- [x] **T033** Revalidar proposta e fingerprint imediatamente antes da execução; falhar fechado em divergência.
- [ ] **T034** Proibir retry automático de escrita e oferecer releitura MCP explícita após resultado ambíguo.
- [x] **T035** Adicionar integração offline que comprova escrita apenas via CLI e ausência de escrita direta.
- [x] **T036** Adicionar regressões para concorrência, cancelamento, falha parcial, caminhos hostis e sanitização.

## M5 — Endurecimento, documentação e distribuição

- [ ] **T037** Executar revisão de segurança dedicada e resolver achados antes de empacotar.
- [ ] **T038** Validar comportamento e encerramento de processos em Windows, Linux e macOS.
- [ ] **T039** Definir nome, Package ID, versionamento, conteúdo do pacote, SBOM e canal de distribuição independente.
- [x] **T040** Adicionar documentação pública equivalente em inglês e pt-BR para instalação, configuração, privacidade, segurança, aprovação e troubleshooting.
- [x] **T041** Atualizar portal, referências e notas de release somente quando a capacidade estiver realmente pronta e o canal for verificável.
- [x] **T042** Executar testes e validações de documentação proporcionais usando os workflows oficiais do repositório.
- [ ] **T043** Verificar pacote publicado e instalação limpa antes de declarar disponibilidade.

## Verificação do Definition of Done

- [x] **T044** Mapear cada critério `CA-001` a `CA-013` para evidência automatizada ou revisão reproduzível.
- [x] **T045** Confirmar que não houve mudança de schema, comando, exit code ou API pública de CLI/MCP/Core.
- [x] **T046** Confirmar que nenhuma ação autônoma aprova/rejeita ADRs, muda lifecycle, cria compliance ou oculta `unknown`.
- [x] **T047** Revisar diff completo, documentação bilíngue, publicação real e riscos residuais antes do merge.

### Matriz reproduzível dos critérios de aceite

| Critério | Evidência atual | Estado |
| --- | --- | --- |
| `CA-001` | `AgentProjectArchitectureTests.AgentProjectHasNoAssemblyReferencesToExistingProducts` | Comprovado. |
| `CA-002` | `AdrGuardMcpClientTests.SevenAdaptersPreserveToolArgumentsEvidenceAndUncertainty`, rejeições estritas de versão/forma/propriedade duplicada e `AdrGuardMcpProcessIntegrationTests.LocalStdioClientUsesAllSevenRealReadOnlyTools` | Comprovado para schema `1.0`. |
| `CA-003` | Cenário versionado `malicious-adr-instructions` e `ArchitectureQuestionRunnerTests.UntrustedEvidenceCannotRequestAnotherTool` | Comprovado offline. |
| `CA-004` | Cenários `insufficient-impact-context`, `inconclusive-drift`, `mcp-timeout` e `caller-cancellation`, além dos testes de erro/limite do cliente MCP | Comprovado offline. |
| `CA-005` | Cenário `proposal-remains-preview-only`, `AdrProposalWorkflowTests.CreationDecisionWithoutApprovalDoesNotInvokeCreate` e integração real de preview sem escrita | Comprovado. |
| `CA-006` | `AdrProposalAuthorizationTests`, `AdrNewFormPreviewClientTests.ChangedAuthorizedContentIsRejectedBeforeCreate` e `ChangedAuthorizedInvocationIsRejectedBeforeCreate` | Comprovado. |
| `CA-007` | Processo real usa argumentos discretos, `UseShellExecute=false`, timeout/cancelamento e saída limitada; integrações observam literalmente códigos `0` e `2` | Parcial: falta stub de processo multiplataforma que force e confira os códigos `1`, `3` e `4`; `T032` permanece aberto. |
| `CA-008` | Integração real `AuthorizedCreateUsesRealCliAndWritesOnlyProposedContent` e cenário preview-only | Comprovado para o único comando permitido. |
| `CA-009` | `ProcessEnvironmentPolicyTests`, cenário `provider-credential-not-in-child-process` e regressões de configuração/provider | Parcial: a trilha persistente sanitizada de `T028` ainda não existe. |
| `CA-010` | `ArchitectureQuestionRunnerTests.LocalReadOnlyModeReturnsMcpEvidenceWithoutProviderOrModel` e `AdrGuardMcpProcessIntegrationTests.ConfiguredRunnerReadsRealMcpWithoutProviderCredentialOrCli` comprovam leitura MCP local sem credencial nem CLI | Comprovado. |
| `CA-011` | `AdrGuardMcpProcessIntegrationTests.SeparateClientsKeepRepositoryEvidenceIsolated` e cenário de traversal `repository-path-traversal` | Comprovado para processos/evidências; aprovação é efêmera por instância. |
| `CA-012` | `docs/architecture-agent.md`, equivalente pt-BR, matriz de compatibilidade, README, CHANGELOG e portal sincronizado/validado | Comprovado para o estado real source-only; publicação continua explicitamente inexistente. |
| `CA-013` | Testes do projeto Agent mais baseline Release da solução, sem alteração de contratos CLI/MCP/Core | Comprovado nesta revisão; requer repetição após mudanças futuras. |

## Validação desta fase documental

- [x] **T048** Criar `spec.md`, `plan.md` e `tasks.md` sem código de produto.
- [x] **T049** Registrar a decisão como ADR `Proposed` conforme a convenção atual.
- [x] **T050** Revisar links, formatação, status Git e `git diff --check` antes da entrega.

## M2.1 — Workflows controlados por Agent Skills

- [x] **T051** Registrar allowlist conceitual das onze `skills/adr-guard-*` sem copiar, reescrever ou alterar seus artefatos.
- [x] **T052** Selecionar deterministicamente um entre cinco workflows: quando registrar, trade-offs, consulta/validação, revisão técnica consultiva e proposta de ADR.
- [x] **T053** Carregar somente o `SKILL.md` autorizado do workflow selecionado, com validação de path, frontmatter e limite de tamanho.
- [x] **T054** Preservar MCP como fonte dos resultados determinísticos, modelo como interpretação consultiva e decisão/aprovação como responsabilidade humana.
- [x] **T055** Cobrir seleção, contexto ausente, resultado MCP inconclusivo e catálogo de ferramentas ausente/incompleto com testes offline.
- [x] **T056** Executar a baseline completa da solução e as validações documentais aplicáveis desta integração.
- [x] **T057** Revisar o diff final e confirmar que Core, CLI e MCP não receberam nova funcionalidade ou contrato.

## M2.2 — Proposta de ADR sem persistência

- [x] **T058** Analisar a solicitação e ADRs relacionados por ferramentas MCP existentes antes de preparar uma proposta.
- [x] **T059** Produzir título, campos do template, alternativas, requisitos, riscos, referências e incertezas em resposta estruturada limitada, mantendo o status `Proposed` sob controle do orquestrador.
- [x] **T060** Consumir o contrato real schema `1.0` de `adr-guard new --form`: executar `describe` antes do modelo e somente `preview` depois dele, sem usar `draft` ou `create`.
- [x] **T061** Exibir Markdown validado pela CLI, evidências MCP, incertezas e destino provisório, deixando explícito que nenhum arquivo, status ou ID foi persistido ou reservado.
- [x] **T062** Falhar fechado para entrada estruturada inválida, resposta/preview rejeitada, schema inesperado, timeout, saída excessiva e destino fora do diretório de ADRs.
- [x] **T063** Cobrir entrada inválida, prévia rejeitada, conflito concorrente e ausência de evidência com testes offline, incluindo integração de processo com o CLI real.
- [x] **T064** Executar baseline completa, validações documentais e revisão final do diff desta fase.

## M3.1 — Criação supervisionada de ADR `Proposed`

- [x] **T065** Exibir a prévia validada antes de qualquer confirmação, incluindo executável, argumentos discretos, diretório-alvo, efeito, risco e fingerprint da proposta exata.
- [x] **T066** Exigir ação humana interativa e independente que repita o fingerprint; recusa, cancelamento, EOF e input não interativo não iniciam escrita.
- [x] **T067** Revalidar o Markdown autorizado imediatamente antes da escrita e rejeitar alteração inesperada sem solicitar nova resposta ao modelo.
- [x] **T068** Invocar exclusivamente `adr-guard new <diretório> --form` com schema `1.0`, `operation=create`, título e campos previamente aprovados, usando argumentos discretos e shell desabilitado.
- [x] **T069** Validar com a CLI real criação somente `Proposed`, verificação do conteúdo gravado, alocação atômica concorrente e preservação de arquivos existentes.
- [x] **T070** Cobrir autorização negada, cancelamento, fingerprint incorreto, execução não interativa, gravação bem-sucedida e falhas fechadas com testes offline.
- [x] **T071** Executar a baseline completa, validações documentais e revisão final do diff desta fase.

## M3.2 — Avaliações offline determinísticas e endurecimento

- [x] **T072** Versionar o catálogo schema `1.0` `architecture-agent-evaluations.v1.json` com entradas, modos de evidência/modelo e invariantes esperados.
- [x] **T073** Avaliar deterministicamente seleção de ferramentas, fundamentação por IDs de evidência, contexto insuficiente, incerteza, prompt injection e tentativa de tool call pelo modelo.
- [x] **T074** Avaliar isolamento de repositório, timeout, cancelamento, limite de chamadas, segredo sentinela, bloqueio de escrita sem autorização e zero chamadas externas com fakes locais.
- [x] **T075** Rejeitar respostas consultivas sem citação no resumo e propriedades JSON duplicadas em respostas do modelo e evidências MCP.
- [x] **T076** Restringir o ambiente do processo CLI a uma allowlist operacional que não herda credenciais do provider nem variáveis arbitrárias.
- [x] **T077** Executar a classe de avaliações e a suíte completa de `AdrGuard.Agent.Tests` em Release.
- [x] **T078** Executar build e testes Release da solução após o endurecimento e revisar o diff final.
