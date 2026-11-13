# Especificação: Architecture Agent

## Status e escopo

- **Status:** Implementado no código-fonte para desenvolvimento; critérios residuais e distribuição permanecem abertos.
- **Escopo desta especificação:** definir e verificar um agente .NET opcional que usa o MCP somente leitura existente para reunir evidências e usa a CLI existente, após aprovação humana explícita, para a única escrita autorizada de uma nova ADR `Proposed`.
- **Fora de escopo nesta entrega:** novos contratos CLI/MCP/Core, edição de ADR existente, mudança de lifecycle, aprovação autônoma, execução autônoma de escrita, pacote/release do Agent e alegações de disponibilidade pública.

## Objetivo

Oferecer um fluxo assistido e auditável para investigar o estado arquitetural de um repositório, relacionar evidências a ADRs e preparar ações propostas sem transferir a responsabilidade da decisão para o modelo. O agente deve reutilizar o MCP local e somente leitura como fonte estruturada e a CLI como única fronteira de escrita, mantendo validação determinística e autoridade humana.

## Personas

### Arquiteta ou arquiteto responsável

Explora decisões, relações, impacto e drift; avalia alternativas; aprova ou rejeita propostas; continua sendo responsável pelo conteúdo e pelo ciclo de vida das ADRs.

### Pessoa desenvolvedora

Investiga quais decisões podem ser afetadas por uma mudança, pede explicações baseadas em evidência e prepara um novo registro `Proposed` para revisão.

### Mantenedora ou mantenedor de plataforma

Configura os executáveis locais, limites, provider de modelo e política de aprovação; precisa de isolamento por repositório, telemetria segura e comportamento fail-closed.

### Revisora ou revisor de segurança/compliance

Verifica proveniência, limites de confiança, comandos aprovados e resultados determinísticos sem aceitar interpretação de IA como evidência ou conformidade.

## Casos de uso

1. **Explorar ADRs:** listar, localizar, ler e resumir ADRs, preservando a indicação de que Markdown é conteúdo não confiável.
2. **Explicar relações:** apresentar relações declaradas e diagnósticos sem inferir arestas ausentes.
3. **Validar o conjunto:** solicitar validação ao MCP e explicar achados sem reimplementar regras.
4. **Avaliar impacto:** correlacionar mudanças Git locais a mapeamentos explícitos e recomendar revisão humana, sem declarar violação.
5. **Avaliar drift:** apresentar `pass`, `violation`, `unknown` e `not-applicable` conforme o resultado MCP, sem promover incerteza a sucesso.
6. **Preparar uma nova ADR:** produzir uma proposta revisável e solicitar autorização humana antes de invocar um fluxo de criação existente da CLI; o status inicial permanece `Proposed`.
7. **Preparar uma edição futura:** edição de ADR existente permanece fora da primeira entrega e não possui comando autorizado no Agent atual.
8. **Cancelar ou recusar:** permitir que a pessoa recuse, edite ou cancele uma proposta sem efeitos no repositório.

## Requisitos funcionais

### Leitura e evidência

- **RF-001:** o agente deve iniciar um processo MCP local por repositório, por `stdio`, com raiz e diretório de ADRs explicitamente delimitados.
- **RF-002:** o agente deve consumir apenas contratos MCP suportados e versionados; versão desconhecida ou resposta inesperada deve interromper o fluxo afetado.
- **RF-003:** o agente deve expor as sete capacidades MCP existentes (`list_adrs`, `get_adr`, `search_adrs`, `get_adr_relationships`, `validate_adrs`, `analyze_impact` e `verify_architecture`) sem duplicar suas regras de domínio.
- **RF-004:** toda afirmação factual sobre o repositório deve referenciar a ferramenta, os parâmetros relevantes e a evidência retornada, ou ser marcada como hipótese/opinião.
- **RF-005:** resultados `unknown`, inconclusivos, incompletos ou com falha operacional devem permanecer explícitos e nunca ser convertidos em aprovação, conformidade ou ausência de impacto.

### Modelo e orquestração

- **RF-006:** a integração com modelo deve ficar atrás de uma abstração de provider opcional e receber apenas o contexto que a pessoa autorizou para o fluxo.
- **RF-007:** o orquestrador deve manter estado explícito de sessão, evidências, proposta, aprovação e execução, sem permitir que texto gerado altere diretamente o estado do repositório.
- **RF-008:** conteúdo de ADR, Markdown, configuração, evidência e resposta do modelo deve ser tratado como não confiável e nunca como instrução de ferramenta.
- **RF-009:** o agente deve separar, na apresentação, fatos vindos das ferramentas, interpretação do modelo, lacunas e ações propostas.
- **RF-010:** o agente deve suportar cancelamento e encerrar processos filhos de forma limitada e previsível.

### Aprovação e escrita

- **RF-011:** nenhuma ferramenta MCP deve ser usada para escrita e nenhum mecanismo de escrita paralela deve ser introduzido no agente.
- **RF-012:** toda escrita deve ser representada primeiro como uma proposta imutável com identificador, alvo, comando CLI permitido, argumentos discretos, resumo do efeito, riscos e fingerprint do contexto relevante quando o contrato existente o oferecer.
- **RF-013:** a aprovação humana deve ser explícita, específica para uma única proposta e invalidada quando alvo, conteúdo, argumentos ou fingerprint mudarem.
- **RF-014:** após aprovação, o agente deve invocar somente comandos de escrita já publicados pela CLI, sem shell, com argumentos discretos, diretório de trabalho delimitado, timeout, cancelamento e captura limitada de stdout/stderr.
- **RF-015:** o agente deve apresentar o resultado real da CLI e seus exit codes sem reinterpretar falha como sucesso. Os códigos `0`, `1`, `2`, `3` e `4` mantêm a semântica existente.
- **RF-016:** criação assistida deve resultar no máximo em uma ADR `Proposed`; alteração de status, aceitação, rejeição, supersessão e mudanças equivalentes exigem um fluxo humano específico e não fazem parte da primeira entrega.
- **RF-017:** o agente deve registrar trilha local e sanitizada contendo proposta, aprovação/recusa, ferramenta ou comando executado, horário e resultado, sem segredos nem payloads completos por padrão.

### Configuração e experiência

- **RF-018:** provider, modelo, compartilhamento de contexto e capacidades de escrita devem ser opt-in e configurados fora de conteúdo não confiável do workspace.
- **RF-019:** o modo somente leitura deve funcionar sem CLI de escrita configurada; indisponibilidade do modelo deve preservar acesso determinístico às ferramentas MCP quando a experiência permitir.
- **RF-020:** o agente deve informar separadamente disponibilidade do binário MCP, da CLI, do provider e das capacidades habilitadas, sem confundir configuração com processo conectado ou produto publicado.

## Requisitos não funcionais

- **RNF-001 — Determinismo:** o agente não deve recalcular validação, relações, impacto ou drift. Resultados determinísticos vêm do MCP/CLI e preservam ordenação, fingerprints e semântica originais.
- **RNF-002 — Menor privilégio:** um processo MCP por repositório; nenhuma rede aberta pelo agente além do endpoint explicitamente escolhido para o provider; escrita desabilitada por padrão.
- **RNF-003 — Segurança de processo:** shell desabilitado, executáveis configurados por caminho confiável, argumentos separados, saída e duração limitadas, e encerramento dos filhos em cancelamento.
- **RNF-004 — Contenção:** caminhos canônicos devem permanecer na raiz selecionada; symlinks/reparse points e escapes devem falhar de forma fechada conforme a fronteira invocada.
- **RNF-005 — Privacidade:** antes de qualquer chamada ao modelo, a experiência deve mostrar a classe de dados que pode sair da máquina e exigir opt-in compatível com a política do provider.
- **RNF-006 — Compatibilidade:** a primeira implementação deve consumir MCP schema `1.0` e contratos CLI publicados sem modificá-los; versões não suportadas são rejeitadas.
- **RNF-007 — Resiliência:** timeout, cancelamento, resposta grande, processo encerrado e dados malformados geram estados explícitos, recuperáveis quando possível e nunca sucesso parcial silencioso.
- **RNF-008 — Observabilidade segura:** logs usam identificadores, tipos de operação e resultados sanitizados; não registram credenciais, variáveis de ambiente, conteúdo integral de ADRs ou respostas integrais do provider.
- **RNF-009 — Testabilidade:** modelo, MCP, aprovação, relógio e processo CLI devem ser portas substituíveis por fakes; testes não dependem de provider vivo, credenciais ou rede.
- **RNF-010 — Acessibilidade e localização:** prompts de aprovação devem ser inequívocos e a experiência deve permitir conteúdo em inglês e pt-BR sem mudar contratos ou resultados determinísticos.

## Limites de confiança

```text
Pessoa
  | autoriza contexto e aprova uma proposta específica
  v
Architecture Agent (.NET opcional)
  |-- Orquestrador: estado, política de aprovação e proveniência
  |-- Adaptador de modelo: interpretação não confiável e opt-in
  |-- Cliente MCP: leitura estruturada por stdio
  |-- Adaptador CLI: escrita autorizada por processo, nunca por shell
  |
  +--> adr-guard-mcp --> AdrGuard.Core --> arquivos/Git locais (somente leitura)
  +--> adr-guard CLI --> AdrGuard.Core --> escrita já governada pela CLI
```

- A pessoa é a única autoridade para aprovar decisões e mutações.
- O modelo é externo ao domínio confiável: sua saída é conselho não confiável.
- O orquestrador pode decidir se um passo é permitido, mas não decide arquitetura nem conformidade.
- O MCP é a fronteira autoritativa de leitura estruturada e permanece sem ferramentas de escrita.
- A CLI é a única fronteira de mutação; suas validações, atomicidade e exit codes continuam autoritativos.
- O repositório, seus ADRs, manifests, evidências e configuração são entradas não confiáveis.
- Core permanece interno aos executáveis existentes; o agente não o referencia nem cria uma segunda implementação de regras.

## Critérios de aceite verificáveis

- **CA-001:** uma análise de dependências comprova que o projeto do agente não referencia `AdrGuard.Core`, `AdrGuard` ou `AdrGuard.Mcp` como assemblies; a comunicação ocorre por processos e contratos publicados.
- **CA-002:** testes de contrato exercitam as sete ferramentas MCP schema `1.0` e rejeitam versão, campo ou forma não suportada conforme a política estrita definida para o cliente.
- **CA-003:** testes demonstram que Markdown com instruções maliciosas não dispara ferramenta, CLI ou mudança de estado.
- **CA-004:** testes demonstram que `unknown`, resposta incompleta, timeout, cancelamento e erro MCP não são apresentados como conformidade ou sucesso.
- **CA-005:** sem aprovação, nenhuma tentativa de criação ou edição inicia a CLI e nenhum arquivo do repositório muda.
- **CA-006:** aprovação de uma proposta não autoriza proposta alterada; mudança de alvo, conteúdo, argumentos ou fingerprint exige nova aprovação.
- **CA-007:** a invocação da CLI usa executável e argumentos discretos, shell desabilitado, timeout e cancelamento; testes cobrem exit codes `0` a `4` sem remapeamento indevido.
- **CA-008:** um fluxo de criação autorizado usa um comando existente da CLI e produz somente `Proposed`; o agente não oferece ação de aprovação automática.
- **CA-009:** logs e diagnósticos de teste não contêm segredo sentinela, conteúdo integral sentinela, caminho absoluto sanitizado nem resposta integral do provider.
- **CA-010:** o modo somente leitura funciona com MCP configurado e sem credenciais de provider ou CLI de escrita.
- **CA-011:** testes de isolamento comprovam que uma sessão não acessa nem reutiliza processos, evidências ou aprovações de outro repositório.
- **CA-012:** documentação em inglês e pt-BR descreve disponibilidade real, instalação, privacidade, aprovação, segurança e limitações antes de qualquer publicação.
- **CA-013:** os contratos e testes existentes de CLI, MCP e Core permanecem inalterados e aprovados; nenhuma alteração de schema, comando, exit code ou visibilidade pública é necessária.

## Definition of Done da evolução

- O ADR desta proposta foi revisado por pessoas e, se aceito no futuro, seu status foi alterado por uma ação humana separada.
- A implementação foi entregue incrementalmente, com fronteiras de modelo, orquestração, ferramentas, aprovação e CLI testáveis de forma independente.
- Todos os critérios de aceite estão cobertos por testes automatizados ou por uma verificação documental reproduzível identificada no plano.
- Testes unitários, de contrato, integração de processos, segurança e empacotamento passam nos ambientes suportados.
- As baselines existentes de CLI/MCP/Core passam sem mudança de contrato e os testes de arquitetura impedem referências proibidas.
- Threat model e revisão de privacidade cobrem prompt injection, confusão entre repositórios, exfiltração, aprovação obsoleta, adulteração de executável e vazamento em logs.
- Documentação pública equivalente em inglês e pt-BR, portal e notas de release refletem apenas capacidades realmente implementadas e publicadas.
- Empacotamento, nome, versão e canal de distribuição independentes são verificados antes de qualquer alegação pública.
- Nenhuma ação do agente aprova/rejeita ADR, dispensa política determinística, fabrica evidência ou transforma incerteza em conformidade.

## Não objetivos

- Substituir a CLI, o MCP ou `AdrGuard.Core`.
- Adicionar ferramentas de escrita ao MCP.
- Embutir um modelo no servidor MCP ou no Core.
- Executar builds, testes, scripts ou produtores de evidência descobertos no workspace.
- Fazer semantic search, inferir relações ou gerar compliance a partir de prosa.
- Hospedar serviço remoto multi-tenant na primeira versão.
- Alterar automaticamente status, baselines, mapeamentos, regras, índices ou estado Git.

## Decisões implementadas e pendências explícitas

Decisões registradas em 13/11/2026, sob responsabilidade das pessoas mantenedoras do repositório:

1. **Superfície inicial:** executável de console `AdrGuard.Agent`, executado somente a partir do código-fonte.
2. **Allowlist de escrita:** somente `adr-guard new <diretório> --form` schema `1.0`, operação `create`, após `describe`, `preview` e confirmação interativa do fingerprint; `edit --form save` não é permitido.
3. **Aprovação:** efêmera por execução e vinculada à proposta exata. Uma trilha persistente, versionada e sanitizada continua pendente em `T028`.
4. **Provider inicial:** OpenAI por `Microsoft.Extensions.AI`, com credencial somente em ambiente e opt-in explícito para contexto remoto. Retenção, residência, preço e política organizacional continuam sendo responsabilidade externa; o modo local funciona sem provider.
5. **Distribuição:** capacidade somente no código-fonte, sem Package ID, pacote, instalador, contêiner, manifesto, tag ou release do Agent. Uma distribuição independente só poderá ser definida depois dos gates de `T037`–`T043`.
