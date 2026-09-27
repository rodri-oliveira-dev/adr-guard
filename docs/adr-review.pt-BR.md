# Revisão técnica de ADR assistida por IA

[English](adr-review.md)

`adr-guard review` realiza uma revisão técnica somente leitura, orientada a evidências, de um ADR existente e estruturalmente válido por meio de um provider de IA escolhido explicitamente. É assistência arquitetural para um revisor humano, não prova de que a decisão está correta, não é certificação formal de segurança/compliance e não é aprovação ou rejeição automatizada.

## Disponibilidade por versão

CLI e GitHub Action têm históricos de release separados.

| Capacidade | Primeira release publicada do CLI | Status |
| --- | --- | --- |
| `adr-guard review` base, oito dimensões, contexto limitado e relatórios versionados | `v1.1.2` | Publicado |
| Policy v1 determinística com advisory/enforce | `v1.1.3` | Publicado |
| Hardening dos limites de confiança do review e matriz determinística com mock provider | `v1.1.4` | Publicado |
| `command: review` na Action reutilizável | `v1.1.6` | Publicado na linha móvel de compatibilidade `@v1` |
| Patch exato mais recente de CLI/pacote/imagem | Consulte GitHub Releases / NuGet | Não é fixado na documentação porque cada release bem-sucedida pode avançar o patch |

A Action `@v1` publicada suporta `check`, `index` e `review` opt-in desde a `v1.1.6`. A listagem no Marketplace é acompanhada separadamente e não é apresentada como disponível até que uma URL pública canônica seja verificada.

## Uso mínimo do CLI

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai \
  --model <modelo-openai>
```

O ADR selecionado passa por validação estrutural antes da chamada ao provider. O comando é somente leitura: não edita o ADR, não muda status, não regenera índice, não altera estado do git e não aprova/rejeita a decisão.

### Providers, modelos e autenticação

O ADR Guard nunca escolhe modelo automaticamente. `--provider` e `--model` são obrigatórios.

| Provider | Valor no CLI | Variável de credencial | Endpoint |
| --- | --- | --- | --- |
| OpenAI | `openai` | `OPENAI_API_KEY` | Endpoint oficial; `--endpoint` customizado é rejeitado |
| Anthropic | `anthropic` | `ANTHROPIC_API_KEY` | Endpoint oficial; `--endpoint` customizado é rejeitado |
| Gemini | `gemini` | `GEMINI_API_KEY` | Endpoint oficial; `--endpoint` customizado é rejeitado |
| OpenAI-compatible | `openai-compatible` | `ADR_GUARD_OPENAI_COMPATIBLE_API_KEY` (opcional) | `--endpoint <uri>` obrigatório |

A autenticação vem de variáveis de ambiente, não de argumentos do CLI. Endpoints OpenAI-compatible remotos exigem HTTPS. HTTP sem TLS só é permitido em loopback sem autenticação; com API key configurada, HTTPS é obrigatório inclusive no loopback.

Disponibilidade de modelos, cobrança, quotas e rate limits pertencem ao provider. O ADR Guard não estima custo preciso de tokens.

## Entradas do review

```text
adr-guard review <adr-file>
  --provider <provider>
  --model <model>
  [--endpoint <uri>]
  [--context-file <path>]...
  [--include-existing-adrs]
  [--policy advisory|enforce]
  [--policy-file <path>]
  [--format text|json]
  [--output <path> [--overwrite]]
```

| Opção | Comportamento |
| --- | --- |
| `<adr-file>` | ADR alvo obrigatório. Apenas o filename, e não o path local absoluto, é exposto ao modelo. |
| `--context-file <path>` | Arquivo explícito UTF-8 `.md` ou `.txt`; repetível e nunca descoberto automaticamente. |
| `--include-existing-adrs` | Autoriza explicitamente descoberta limitada de ADRs parseados abaixo do diretório do ADR alvo para contexto cross-ADR. |
| `--policy advisory\|enforce` | Padrão `advisory`. Somente regras locais determinísticas podem fazer enforcement. |
| `--policy-file <path>` | Policy JSON local estrita, schema `1.0`. Não é enviada ao provider. Obrigatória em `enforce`. |
| `--format text\|json` | Padrão text. JSON emite um único objeto versionado no stdout. |
| `--output <path>` | Persistência opcional do relatório. Text exige `.md`/`.txt`; JSON exige `.json`. |
| `--overwrite` | Permite substituir atomicamente apenas relatório existente; nunca permite substituir o ADR revisado nem o índice. |

## Seleção de contexto, limites e privacidade

Por padrão, **somente o ADR selecionado** é enviado ao provider externo configurado. Nada mais é descoberto silenciosamente.

Material adicional é opt-in:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider anthropic \
  --model <modelo-anthropic> \
  --context-file docs/architecture/security.md \
  --context-file docs/architecture/availability.txt \
  --include-existing-adrs
```

Os limites do contexto de review são determinísticos:

| Fonte | Máximo |
| --- | ---: |
| Cada `--context-file` explícito | 50.000 caracteres e 150.000 bytes |
| Todos os context files explícitos somados | 100.000 caracteres e 300.000 bytes |
| Contexto parseado de ADRs existentes | 12.000 caracteres |
| Contexto final composto do review | 120.000 caracteres |

Contexto explícito aceita somente `.md`/`.txt` UTF-8 válido. UTF-16, UTF-32, UTF-8 inválido e conteúdo binário/NUL são rejeitados. Arquivos acima do limite são rejeitados em vez de truncados silenciosamente.

Com `--include-existing-adrs`, o ADR Guard pode inspecionar ADRs Markdown abaixo do diretório do ADR alvo para montar contexto parseado limitado e evidência cross-ADR. Não há varredura de código-fonte, histórico git, URLs arbitrárias, arquivos não relacionados ou variáveis de ambiente como contexto arquitetural.

Antes da chamada ao provider, as fontes selecionadas são divulgadas localmente. O contexto do provider usa IDs de fonte e filenames, não paths absolutos do filesystem local.

**Processamento por terceiros:** o ADR selecionado, context files opt-in e material de ADRs existentes opt-in são transmitidos ao provider configurado e podem sair da sua máquina, organização ou região. Valem as regras do provider sobre retenção, logging, residência de dados, treinamento e processamento. Se isso não for aceitável, não execute `review`; continue usando os fluxos offline/determinísticos `check`, `index` e `new`.

A redação de credenciais é defesa em profundidade, não um sistema DLP. Valores literais exatos e não vazios de variáveis conhecidas de provider/GitHub são substituídos quando encontrados, mas valores codificados, correspondências parciais e secrets vindos de outras variáveis não têm detecção garantida.

## Oito dimensões de revisão

Toda resposta válida do provider deve cobrir exatamente:

1. `clarity-and-rationale`
2. `considered-alternatives`
3. `nonfunctional-requirements`
4. `risks-and-consequences`
5. `architectural-consistency`
6. `security-and-compliance`
7. `implementation-and-operational-feasibility`
8. `measurable-verification-criteria`

O modelo não deve inventar workloads, SLAs, medições, infraestrutura, orçamento, obrigações legais ou documentos fonte.

## Classificações de evidência e incerteza

Os findings usam cinco classificações:

| Classificação | Significado | Efeito no CI |
| --- | --- | --- |
| `observed-evidence` | O material selecionado sustenta diretamente a observação. | Apenas advisory |
| `potential-risk` | A evidência sugere risco, mas exige confirmação humana. | Apenas advisory |
| `missing-context` | Faltam fatos necessários; a explicação precisa declarar “not enough information”. | Apenas advisory |
| `recommendation-for-human-investigation` | O material selecionado justifica investigação humana adicional. | Apenas advisory |
| `not-applicable` | A evidência selecionada sustenta que a dimensão não se aplica. | Apenas advisory |

As prioridades de follow-up são derivadas localmente: `missing-context` é `required`; riscos e recomendações de investigação humana são `recommended`. A incerteza do relatório também é explícita: `not-enough-information`, `potential` ou `requires-human-investigation`.

Um finding do provider nunca vira violação de policy objetiva só porque usa linguagem severa como “critical” ou “high”.

## Advisory versus enforcement determinístico

O padrão é:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai \
  --model <modelo-openai> \
  --policy advisory
```

Em advisory, diagnósticos determinísticos da policy e todos os findings do modelo continuam sem bloquear CI.

Para aplicar regra local objetiva:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider openai \
  --model <modelo-openai> \
  --policy enforce \
  --policy-file docs/adr/review-policy.json
```

O schema `1.0` de policy suporta:

- `required-section-content`: uma seção Markdown de nível dois com nome definido precisa ter conteúdo não vazio;
- `required-context-file`: um arquivo relativo `.md`/`.txt` precisa existir em relação ao arquivo da policy e também ter sido selecionado explicitamente com `--context-file`.

O enforcement roda localmente **antes da construção/chamada do provider**. Violação retorna `4`. Policy passando ainda não significa aprovação arquitetural.

Consulte [policy v1 de review](adr-review-policy-v1.pt-BR.md).

## Relatórios e schema JSON

Text é o padrão:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider gemini \
  --model <modelo-gemini>
```

JSON versionado está disponível para automação:

```bash
adr-guard review docs/adr/0007-cache-strategy.md \
  --provider gemini \
  --model <modelo-gemini> \
  --format json \
  --output artifacts/adr-review.json
```

O schema JSON do relatório é atualmente `1.0` e usa nomes camelCase estáveis. Ele contém metadados de alvo/revisor/provider, escopo de inputs selecionados, as oito dimensões, findings acionáveis, limitações e o aviso de custo do provider. Em modo JSON, a divulgação das fontes antes da chamada ao provider vai para stderr para preservar stdout como JSON válido.

### Exemplo de relatório legível por humanos

Exemplo abreviado:

```markdown
# ADR Technical Review

Schema version: 1.0
Target: 0007-cache-strategy.md (ADR 0007)
Provider: openai / <modelo-openai>
Outcome: needs-context

> Advisory AI-assisted review only. Human review remains authoritative; this report does not approve, reject, certify, or change the ADR.

## Analyzed dimensions

### clarity-and-rationale
- Classification: observed-evidence
  Evidence: [target] 0007-cache-strategy.md
  Explanation: The decision and stated rationale are present.
  Guidance: Verify that the rationale matches the actual workload.

### security-and-compliance
- Classification: missing-context
  Follow-up priority: required
  Uncertainty: not-enough-information
  Evidence: not available from verified selected sources.
  Explanation: not enough information to determine applicable security or compliance constraints.
  Guidance: Provide the relevant security/compliance requirements.

## Follow-up findings
- security-and-compliance [missing-context] — required
  not enough information to determine applicable security or compliance constraints.
  Guidance: Provide the relevant security/compliance requirements.
```

“No follow-up findings” **não** significa aprovação.

## Exit codes

| Código | Significado para `review` |
| ---: | --- |
| `0` | Review concluído. Findings do modelo ainda podem exigir follow-up humano. |
| `1` | ADR selecionado falhou na validação estrutural determinística; provider não é chamado. |
| `2` | Configuração inválida de CLI/provider/policy. |
| `3` | Falha operacional, provider, transporte, cancelamento, resposta malformada ou relatório. Nunca é interpretada como review limpo. |
| `4` | Uma ou mais regras determinísticas nomeadas falharam; provider não é chamado. |

## GitHub Action e governança de CI

A integração publicada da Action mantém `check` como padrão e torna `review` explícito; `review` está disponível na linha `v1` desde a `v1.1.6`.

Um workflow confiável usa apenas:

```yaml
permissions:
  contents: read
```

e pode optar pelo review usando a Action publicada:

```yaml
- name: Revisar ADR selecionado
  uses: rodri-oliveira-dev/adr-guard@v1
  env:
    OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
  with:
    command: review
    review-target: docs/adr/0007-cache-strategy.md
    provider: openai
    model: <modelo-openai>
    policy: advisory
```

A Action:

- aceita review com provider somente em `push`, `workflow_dispatch`, `schedule` e `pull_request` do mesmo repositório;
- rejeita pull requests de fork, `pull_request_target` e todos os demais tipos de evento antes da execução do Docker/provider;
- nunca encaminha `GITHUB_TOKEN` nem `GH_TOKEN` ao container de review;
- monta o checkout como somente leitura;
- usa rede de saída somente no `review` com provider; `check`/`index` determinísticos mantêm `--network=none`;
- emite findings de IA como annotations `warning` apenas quando a evidência aponta para arquivo local selecionado e verificado;
- não inventa números de linha;
- grava `GITHUB_STEP_SUMMARY` estruturado;
- não cria comentários em PR e não precisa de `pull-requests: write`.

Para contribuições de comunidade/fork, execute `check` determinístico no PR não confiável e faça o review com provider apenas depois do merge ou em workflow manual/confiável separado.

Consulte [review por IA na GitHub Action](github-action-review.pt-BR.md) e [modelo de segurança da GitHub Action](github-action-security.pt-BR.md).

## Segurança, custo e comportamento em falhas do provider

Material de ADR/contexto e saída do provider são não confiáveis. Instruções embutidas, URLs, comandos shell ou pedidos de secrets são tratados como dados inertes. O contrato de review não expõe ao modelo capabilities controladas de filesystem, fetch de rede, execução de comandos, escrita de arquivos, mudança de status do ADR ou acesso a secrets.

A saída do provider passa por validação de schema e limites. Respostas malformadas, incompletas, grandes demais, timeout, rate limit ou outras falhas retornam erro operacional; o ADR Guard nunca as converte em “sem problemas”.

Relatórios persistidos e artefatos de CI podem conter trechos de evidência e observações arquiteturais. Proteja-os conforme as mesmas regras de classificação de dados dos ADRs revisados.

Consulte [segurança e limites de confiança do review](adr-review-security.pt-BR.md).

## Troubleshooting

### `needs-context` ou muitos findings `missing-context`

O provider está dizendo que o material selecionado é insuficiente, não que o ADR falhou. Adicione somente o `--context-file` específico que você pode compartilhar ou habilite `--include-existing-adrs` quando contexto cross-ADR fizer sentido. Se a evidência ausente precisa bloquear CI, represente a exigência como regra determinística de policy.

### Contradição potencial com outro ADR

A análise cross-ADR só acontece quando o contexto de ADRs existentes é habilitado explicitamente. Decisões ativas com mismatch observável podem virar risco potencial, mas escopo e cronologia ainda exigem confirmação humana. ADRs Deprecated e Superseded são contexto histórico e não criam conflito atual apenas por serem diferentes.

### Timeout, rate limit, endpoint indisponível ou cancelamento

São falhas operacionais com exit `3`. Repita conforme a política do provider ou investigue endpoint/conectividade/quota. Não trate a execução com falha como review limpo.

### Resposta malformada ou incompleta do provider

A resposta é rejeitada se violar o contrato JSON estrito, omitir uma das oito dimensões, usar classificação não suportada, exceder limites de campos/findings ou não expressar incerteza em `missing-context`. O resultado é exit `3`, nunca aprovação.

### Policy retorna `4` antes da chamada ao provider

É o comportamento esperado em `--policy enforce`: regras locais determinísticas são avaliadas primeiro. Corrija a regra/evidência objetiva indicada ou altere intencionalmente a configuração da policy; não peça ao modelo para sobrescrevê-la.

## Responsabilidade humana

O ADR Guard pode organizar evidências, destacar lacunas e sugerir perguntas de follow-up. Ele não estabelece correção arquitetural, não certifica segurança/compliance, não decide aceitação organizacional e não substitui o revisor humano responsável. O status final do ADR e as decisões de governança continuam sendo das pessoas.
