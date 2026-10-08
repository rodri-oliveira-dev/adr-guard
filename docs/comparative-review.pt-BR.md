# Review comparativo explícito por IA

> Status de preparação: este modo opt-in está implementado na branch encadeada da v1.4 e não é publicado até a conclusão da release manual v1.4.0.

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --compare-ref main \
  --provider openai \
  --model <modelo-explícito>
```

O ADR Guard verifica a referência Git, lê o arquivo atual do ADR selecionado e o mesmo path relativo no único commit informado, adicionando a versão anterior como `[comparison-base]`. Não transmite histórico, diff, mensagens de commit, arquivos vizinhos, variáveis de ambiente nem contexto do repositório. ADRs existentes e context files continuam opt-ins separados.

O contrato do provider solicita mudanças apoiadas em evidência no contexto/problema, decisão, justificativa e consequências. Findings devem distinguir evidência direta, possível impacto arquitetural, incerteza e `not enough information`; mudança textual isolada não é violação, aprovação, rejeição nem regressão.

As duas versões são dados não confiáveis, recebem a mesma redação de credenciais e limites existentes e são divulgadas localmente antes da chamada. A saída do provider continua não confiável e consultiva. O relatório preserva schema `1.0`; `[comparison-base]` aparece em `inputScope.explicitContext`, mantendo compatibilidade para consumidores de fontes explícitas.

O comando falha antes do provider quando ref/path não existe, é inseguro, está fora do Git, é cancelado ou não pode ser lido. Testes usam apenas providers mock.

Em pull requests, execute comparação com provider apenas em eventos confiáveis do mesmo repositório e com secret explicitamente selecionado. Nunca exponha credenciais a código de forks e nunca use `pull_request_target` para fazer checkout/review de mudanças não confiáveis. Forks devem executar `check` determinístico; um maintainer pode disparar o review depois a partir de commit/workflow confiável.
