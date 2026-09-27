# Policy de review de ADR v1

[English](adr-review-policy-v1.md)

A policy de review do ADR Guard separa enforcement determinístico em CI de orientação falível produzida pelo revisor de IA.

## Padrão seguro

`adr-guard review` usa o modo de policy `advisory` por padrão. Findings da IA, inclusive linguagem como `critical` ou `high`, nunca alteram o exit code por si só e nunca aceitam, rejeitam nem mudam o status de um ADR.

Enforcement opt-in exige ambos:

- `--policy enforce`
- `--policy-file <path>`

O arquivo de policy é JSON local, não é transmitido ao provider de review e usa schema version `1.0`.

## Schema da policy

```json
{
  "schemaVersion": "1.0",
  "rules": [
    {
      "name": "decision-rationale-present",
      "type": "required-section-content",
      "section": "Decision"
    },
    {
      "name": "security-context-selected",
      "type": "required-context-file",
      "path": "security-context.md"
    }
  ]
}
```

Os nomes das regras precisam ser únicos e conter somente letras, números, `-`, `_` ou `.`.

Regras determinísticas suportadas:

| Tipo | Evidência determinística |
| --- | --- |
| `required-section-content` | Uma seção Markdown de nível dois com o nome indicado existe e contém conteúdo não vazio. |
| `required-context-file` | O arquivo relativo `.md` ou `.txt` existe em relação ao arquivo da policy e o mesmo arquivo foi informado explicitamente com `--context-file`. |

O JSON da policy rejeita propriedades desconhecidas, tipos de regra não suportados, nomes duplicados, paths absolutos de required-context, extensões não suportadas e versões de schema não suportadas.

## Modos

| Modo | Violações determinísticas de policy | Findings da IA |
| --- | --- | --- |
| `advisory` | São relatadas como diagnósticos; o review continua e elas não falham o CI. | Apenas relatados; nunca falham o CI. |
| `enforce` | O review para antes da chamada ao provider e retorna código 4, listando regra nomeada e evidência. | Nunca são avaliados como gate de policy e nunca falham o CI. |

Uma policy de enforcement aprovada não significa que o ADR está aprovado. A responsabilidade humana pela decisão não muda.

## Matriz de resultado e exit code

| Situação | Resultado / diagnóstico | Exit code |
| --- | --- | ---: |
| Review advisory concluído com ou sem findings de follow-up do modelo | O resultado do relatório é `no-follow-up-findings`, `follow-up-suggested` ou `needs-context` | 0 |
| Modo enforce com todas as regras determinísticas satisfeitas | Relatório normal; linguagem do modelo não altera o resultado da policy | 0 |
| ADR estruturalmente inválido | Falha de validação estrutural; provider não é chamado | 1 |
| CLI ou configuração de policy inválida | Erro de uso/configuração de policy | 2 |
| Target/contexto ausente, provider indisponível, cancelamento ou outra falha operacional/I/O | Diagnóstico operacional explícito; nunca tratado como “sem problemas” | 3 |
| Saída de review do provider malformada ou incompleta | Falha operacional explícita de provider/relatório; nunca tratada como “sem problemas” | 3 |
| Modo enforce com uma ou mais regras determinísticas violadas | Diagnóstico `policy-failed` com nome/tipo/evidência da regra; provider não é chamado | 4 |

Um finding `missing-context` produzido pelo modelo gera resultado `needs-context`, mas continua advisory inclusive em `enforce`. Se contexto ausente precisa ser gate de CI, represente essa exigência como regra determinística, por exemplo `required-context-file`.
