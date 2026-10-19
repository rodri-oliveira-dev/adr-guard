# Baseline de qualidade

Esta página registra evidências voltadas a contribuidores para o roadmap P1 de qualidade. Ela descreve verificações do repositório-fonte, não capacidades garantidas por uma versão publicada do ADR Guard.

## Cobertura multiplataforma da CLI principal

O workflow `Core CLI cross-platform smoke` executa uma jornada focada de comandos públicos no Ubuntu, Windows e macOS. Ele compila em Release somente a CLI autoritativa e invoca o `adr-guard.dll` resultante por meio de `dotnet`; não repete toda a suíte .NET nem a matriz de pacotes MCP.

O caminho do repositório descartável contém espaços e Unicode. A jornada verifica:

- `init → new → check → index` por argumentos reais da linha de comando;
- reexecuções idempotentes de `init` e de `index` com conteúdo idêntico em bytes;
- schema JSON `1.0`, exit `0` para sucesso, exit `1` para validação e um achado `ADR004` estável;
- um repositório Git e `check --changed --base-ref main`;
- saída de máquina idêntica em verificações inválidas repetidas; e
- limpeza em um bloco `finally`.

Execute com o runner MTP nativo fixado pelo repositório:

```text
dotnet test --project tests/AdrGuard.Tests/AdrGuard.Tests.csproj --configuration Release --filter-class AdrGuard.Tests.Cli.CoreCliCrossPlatformSmokeTests
```

A suíte existente de testes unitários e de integração continua responsável por traversal, caminho físico e contenção de symlinks. O smoke não cria symlinks porque os runners Windows compartilhados não concedem esse privilégio de forma consistente. Separadores nativos e o comportamento de case do filesystem são exercitados por cada runner; o teste não exige que variantes distintas apenas por maiúsculas/minúsculas coexistam.

## Auditoria de diagnósticos

Todos os códigos atuais de validação foram rastreados do parser e validador até texto, JSON/SARIF e o adaptador Problems do VS Code. `--diagnostic-locations` continua opt-in e disponível apenas no código-fonte até a publicação de uma versão da CLI que a contenha.

| Código / família | Gatilho e ação | Evidência de localização exata | Cobertura / resultado da auditoria |
| --- | --- | --- | --- |
| `ADR001` | Nome de arquivo viola a convenção selecionada; renomeie-o. | Somente arquivo; a violação é o path. | Cobertura do validador; intencionalmente sem posição. |
| `ADR002` | Título H1 ausente; adicione-o. | Somente arquivo; não existe heading. | Cobertura do validador; intencionalmente sem posição. |
| `ADR003` | Status canônico ausente/vazio ou metadata MADR vazia; adicione valor suportado. | Heading canônico existente apenas. Heading ausente e front matter continuam limitados ao arquivo. | Cobertura do validador e fallback do adaptador. |
| `ADR004` | Status não suportado; escolha um valor do ciclo de vida configurado. | Heading canônico de status interpretado. | Cobertura JSON, SARIF, VS Code e smoke multiplataforma. |
| `ADR005` | Seção obrigatória ausente/vazia; adicione conteúdo. | Heading interpretado quando vazio; seção ausente fica limitada ao arquivo. | Cobertura JSON/SARIF e VS Code. |
| `ADR006` | Identidade numérica/estável duplicada; torne as identidades únicas. | Conflito entre arquivos, portanto limitado ao arquivo. | Cobertura incremental/global. |
| `ADR007` | Target Markdown/relacionamento inseguro ou não resolvido; corrija o target reportado. | Link interpretado quando presente; metadata de status MADR não resolvida fica limitada ao arquivo. | Cobertura de relacionamento baseada no parser. |
| `ADR008` | ADR substituída não possui sucessora utilizável; adicione link para ADR existente. | Heading `Superseded by` interpretado quando existe; seção ausente fica limitada ao arquivo. | **Lacuna corrigida:** validador, JSON, SARIF e fixture de contrato do VS Code. |
| `ADR009` | Seção canônica/MADR duplicada; remova ou una duplicatas. | Segundo heading interpretado. | Cobertura de localização do validador. |
| `ADR010` | Grafo de substituição contém ciclo; interrompa o ciclo. | Conflito global com várias arestas, portanto limitado ao arquivo. | Cobertura de determinismo do grafo. |
| `ADR011`–`ADR012` | Auto-substituição ou múltiplas sucessoras; corrija o conjunto de relações. | Resultado do grafo atualmente limitado ao arquivo. | Coberto; posição por aresta adiada porque o validador normaliza targets antes desses checks. |
| `ADR013` | Direção de substituição conflita com status/target; alinhe ambos os lados. | Referência `Supersedes` interpretada quando retida; conflitos normalizados de sucessora/status ficam limitados ao arquivo. | Caminhos com posição e fallback cobertos. |
| `ADR014` | Dependência aponta para ADR inativa; revise a dependência. | Referência de dependência interpretada. | Cobertura de localização do validador. |
| `ADR015` | Placeholder de autoria não resolvido; substitua-o. | Heading da seção interpretada que o contém. Severidade depende da policy. | Cobertura da policy de placeholders. |
| `ADR016` | Metadata limitada inválida; corrija chave/valor nomeado. | Somente arquivo porque posições de chaves do front matter não são retidas pelo parser atual. | Cobertura de metadata; expansão do parser adiada para evitar coordenadas inferidas. |

A auditoria não justificou alterar identificadores, severidade, ordenação, mensagens, exit codes, versões de schema nem fingerprints de baseline. Em particular, estruturas ausentes, conflitos globais de grafo e achados de front matter permanecem sem posição em vez de receber coordenadas inferidas.

## Baseline reproduzível de performance

`benchmarks/AdrGuard.Benchmarks` é um harness informativo e sem dependências. Ele gera corpora sintéticos válidos e deterministicamente inválidos, separados, com 100, 500 e 900 ADRs e finais de linha LF normalizados. Os arquivos usam status variados do ciclo de vida, referências/dependências resolvidas, texto limitado e um status inválido a cada 100 ADRs. Nenhum conteúdo de produção nem chamada de rede/provider é utilizado.

Compile a CLI e o harness, depois registre cinco amostras subsequentes além da primeira execução:

```text
dotnet build AdrGuard.slnx --configuration Release
dotnet run --project benchmarks/AdrGuard.Benchmarks/AdrGuard.Benchmarks.csproj --configuration Release --no-build -- --cli src/AdrGuard/bin/Release/net10.0/adr-guard.dll --output benchmarks/results/local.json --samples 5
```

O harness reporta o tempo de geração separadamente. `check-invalid` espera exit `1`; `index-valid` espera exit `0` e remove somente o `README.md` gerado antes de cada invocação medida, para que toda amostra meça a criação do índice em vez do no-op idempotente. Cada operação inicia um novo processo da CLI. Portanto, `firstRun` é um proxy de cache do SO/filesystem, não uma medição de JIT em processo persistente; `subsequentRuns` contém mediana, mínimo, máximo, variação e mediana do pico de working set. Os resultados também registram hashes dos datasets, SO, arquitetura, runtime .NET, quantidade de processadores lógicos, memória disponível, versão da CLI, SHA do código-fonte e estado de alterações locais. `WorkingTreeDirty` é `null` quando os metadados do Git não podem ser lidos, em vez de tratar um estado desconhecido como uma árvore de trabalho limpa.

Medições commitadas são observações específicas do ambiente, não SLAs nem thresholds de CI. Compare apenas relatórios com hashes de dataset iguais e investigue mudanças relevantes com profiling antes de propor otimização.

### Baseline Windows observada — 18/10/2026

O relatório completo legível por máquina está em [`benchmarks/results/windows-x64-2026-10-18.json`](../benchmarks/results/windows-x64-2026-10-18.json). Ele mediu o código-fonte e a CLI no SHA `f32b81956037b4935fc3da2cd64bd763e8b7491c`, em Windows x64, .NET 10.0.13, 8 processadores lógicos e aproximadamente 31,4 GiB de memória disponível. O marcador de worktree com alterações é verdadeiro porque o checkout continha uma edição preexistente e não relacionada em `.gitignore`; o relatório registra a versão informacional exata da CLI e os hashes dos datasets para explicitar essa limitação.

| ADRs | Operação | Preparação | Primeira execução | Mediana subsequente (mín–máx; variação) | Mediana do pico de working set |
| ---: | --- | ---: | ---: | ---: | ---: |
| 100 | `check` inválido | 65,182 ms | 1068,201 ms | 255,384 ms (243,517–331,316; 87,799 ms) | 30,5 MiB |
| 100 | `index` válido | 65,182 ms | 247,975 ms | 242,322 ms (236,766–274,834; 38,068 ms) | 30,6 MiB |
| 500 | `check` inválido | 288,848 ms | 334,655 ms | 417,161 ms (310,669–451,435; 140,766 ms) | 35,5 MiB |
| 500 | `index` válido | 288,848 ms | 377,254 ms | 304,760 ms (296,728–324,549; 27,821 ms) | 35,6 MiB |
| 900 | `check` inválido | 472,707 ms | 370,342 ms | 368,928 ms (362,144–406,005; 43,860 ms) | 36,5 MiB |
| 900 | `index` válido | 472,707 ms | 482,886 ms | 367,128 ms (358,665–380,474; 21,809 ms) | 36,3 MiB |

Duas gerações locais completas produziram hashes idênticos para todos os seis datasets válidos/inválidos. Diretórios temporários dos cenários estavam ausentes após cada execução. O primeiro `check` com 100 ADRs mostra custo relevante de cold start/ruído, reforçando por que não há threshold nem conclusão de otimização associada a esta baseline. Nenhum número de performance para macOS ou Linux foi medido neste checkpoint.
