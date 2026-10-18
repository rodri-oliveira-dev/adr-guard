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
