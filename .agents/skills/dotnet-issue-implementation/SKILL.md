---
name: dotnet-issue-implementation
description: Use esta skill para transformar uma issue bem definida em uma alteração focada de projeto .NET (API, worker ou biblioteca), com requisitos explícitos, implementação, testes, validação e conferência do Definition of Done. Não use para investigação de bug sem causa conhecida ou revisão de PR existente.
license: MIT
---

# Objetivo

Executar uma issue de ponta a ponta sem ampliar escopo, preservando contratos relevantes e a baseline técnica e mantendo rastreabilidade entre requisito, código, testes e validação.

# Processo

1. Leia `AGENTS.md` e a issue completa.
2. Extraia problema, comportamento esperado, restrições, DoD e ambiguidades.
3. Pesquise antes de abrir arquivos grandes e localize implementação, contratos, testes, configuração e documentação relacionados.
4. Identifique impacto em contratos (HTTP, eventos, APIs públicas), dependências, persistência, segurança, compatibilidade, packaging ou changelog conforme o tipo de projeto.
5. Defina a menor estratégia capaz de satisfazer o DoD.
6. Delegue somente tarefas mecânicas quando workers/subagentes estiverem disponíveis; mantenha decisões de design, comportamento e risco no agente principal.
7. Implemente com diff focado e adicione ou atualize testes que comprovem o comportamento pedido.
8. Atualize changelog, documentação ou ADR quando a mudança afetar consumidores, contratos ou decisões arquiteturais, respeitando a convenção do projeto.
9. Execute a baseline definida em `AGENTS.md`, README ou CI, quando existente, e validações adicionais exigidas pelo tipo de mudança.
10. Revise o diff completo e confira o DoD item a item como comprovado, bloqueado ou não aplicável.

# Combinação com outras skills

Combine quando necessário com:

- a skill local de implementação de serviços ou bibliotecas, se houver, para mudanças funcionais específicas;
- `csharp-refactoring` (oficial) para refatoração que preserve comportamento;
- `test-anti-patterns` (oficial) ou outra skill de qualidade para riscos de teste;
- a skill de CI/release do projeto para mudanças de pipeline ou publicação;
- `dotnet-security-review` (neste catálogo) quando houver superfície de segurança relevante.

Skills complementares são opcionais: não pressuponha que estejam instaladas no repositório consumidor.

# Restrições específicas

- Não invente requisitos ausentes na issue.
- Não faça refatoração oportunista fora da área necessária.
- Não faça breaking change incidental.

Respeite as instruções aplicáveis de `AGENTS.md`, quando existir, e a configuração real de build, testes e CI do repositório consumidor.

# Saída esperada

Informe de forma objetiva o que mudou, arquivos principais, testes/validações executados, estado do DoD e riscos ou bloqueios restantes.

# Critério de qualidade

Uma boa implementação satisfaz o DoD com o menor diff coerente, possui evidência proporcional ao risco e passa pelos gates determinísticos aplicáveis.
