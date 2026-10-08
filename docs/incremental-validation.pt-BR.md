# Validação incremental e baseline de diagnósticos

> Status de preparação: estes contratos opt-in estão implementados na branch encadeada da v1.4 e não são publicados até a conclusão da release manual v1.4.0.

## ADRs alterados

```bash
adr-guard check docs/adr --changed --base-ref main
```

O ADR Guard verifica a base como commit, calcula o merge-base com `HEAD` e combina mudanças commitadas da branch, worktree, renames, exclusões e ADRs não rastreados. Git é iniciado diretamente com lista de argumentos; refs e conteúdo nunca são interpolados em shell.

Dívida estrutural local de arquivos inalterados é omitida no modo changed. A integridade global continua avaliando todo o conjunto atual: IDs duplicados, referências quebradas, target/ciclos/consistência de substituição e dependências explícitas inativas não podem ser ignorados. Excluir ou renomear um ADR referenciado ainda afeta os ADRs que o referenciam.

O `check` completo permanece como default. O modo changed retorna exit code operacional `3` quando Git não existe, o diretório não pertence a repositório, a base é inválida/indisponível, não há merge-base, o histórico shallow é insuficiente ou ocorre cancelamento. O ADR Guard nunca faz fetch automático.

No GitHub Actions, use `actions/checkout` com `fetch-depth: 0` (em produção, fixe um SHA revisado). PRs de forks podem executar este modo determinístico com `contents: read`, sem secrets. Se a base não estiver disponível localmente, prefira check completo a criar um fluxo privilegiado de fetch.

## Baselines

Gere ou atualize explicitamente o [schema v1 do baseline](schemas/adr-diagnostic-baseline-v1.schema.json):

```bash
adr-guard baseline docs/adr --output .adrguard-baseline.json
adr-guard baseline docs/adr --output .adrguard-baseline.json --update
adr-guard check docs/adr --baseline .adrguard-baseline.json
```

Fingerprints usam SHA-256 sobre código, path relativo normalizado e mensagem. Rename ou mudança de mensagem aparece como um finding resolvido e outro novo. Exclusão gera resolvidos; reaparecimento exato volta a coincidir com a entrada preservada.

Relatórios mostram quantidades `new`, `existing` e `resolved` em texto, JSON e SARIF. Somente findings locais exatos podem ser existentes. **ADR006–ADR008 e ADR010–ADR014** são diagnósticos de integridade global e continuam obrigatórios/novos, mesmo se inseridos no baseline. **ADR009** (seção canônica duplicada) é local ao documento e pode ser reconhecido como existente. Com `--changed --baseline`, os contadores de `new` e `existing` refletem o conjunto selecionado, mas `resolved` sempre considera a validação completa, evitando classificar diagnósticos de arquivos inalterados como resolvidos. JSON inválido/incompatível, falhas de IO/Git/provider e cancelamento continuam sendo erros. O baseline muda apenas pelo comando explícito `baseline`; a gravação é atômica e nunca substitui symlink.

Para adoção gradual, revise e versione o JSON gerado. A CI deve consumi-lo somente leitura. Atualização deve ser mudança deliberada e revisável, nunca etapa automática da validação.
