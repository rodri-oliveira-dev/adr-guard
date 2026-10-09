# Governança determinística de relacionamentos entre ADRs

Links Markdown locais comuns precisam apenas resolver para um arquivo existente dentro dos limites do repositório; eles não precisam ser ADRs carregados. URLs e âncoras nunca são acessadas, exemplos em blocos de código são ignorados, e travessias ou escapes por links simbólicos/reparse points são reportados como `ADR007`.

O modo de compatibilidade pode reconhecer a forma canônica exata `Superseded by [ADR 0002](0002-successor.md)`. Um sucessor `Proposed` pode declarar `Supersedes` enquanto o predecessor permanece `Accepted`; isso é uma proposta pendente e não desativa o histórico nem implica aprovação. A substituição efetiva ainda exige um sucessor ativo e mantém as verificações de ciclo, autorreferência, múltiplos alvos e contradições.

O ADR Guard valida somente relacionamentos declarados explicitamente pelos autores. Isso mantém a governança auditável e evita transformar julgamento arquitetural em regra bloqueante.

As declarações reconhecidas são links Markdown sob headings de nível dois `Superseded by`, `Supersedes`, `Depends on` ou `Dependencies`, além do status MADR `status: "superseded by ADR-NNNN"`. Links narrativos em outras seções continuam sujeitos à validação de referência quebrada, mas não ganham semântica de dependência/substituição.

| Código | Invariante objetivo |
| --- | --- |
| `ADR010` | A decisão participa de ciclo direcionado de substituição. |
| `ADR011` | A decisão declara a si própria como substituta. |
| `ADR012` | A decisão declara mais de um target substituto distinto. |
| `ADR013` | A direção explícita de substituição conflita com status ou target nomeado. |
| `ADR014` | Uma dependência explícita aponta para decisão `Deprecated` ou `Superseded`. |

`ADR001`–`ADR009` preservam os significados publicados. Targets Markdown/MADR quebrados continuam como `ADR007`; um registro canônico `Superseded` sem target utilizável continua como `ADR008`.

Cadeias históricas longas são válidas: uma decisão que substituiu outra pode ser substituída depois. Referências narrativas circulares também são válidas quando não são arestas explícitas de substituição. O grafo é avaliado em ordem determinística por path e não usa IA nem interpretação semântica.
