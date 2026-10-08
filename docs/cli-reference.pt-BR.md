# Referência de CLI e configuração do ADR Guard v1.2

> Status de preparação: estes contratos estão implementados na branch de desenvolvimento da v1.2, mas não são publicados até a conclusão da release manual v1.2.0.

## Inicializar um repositório

```text
adr-guard init [repositório] [--adr-directory <caminho>]
  [--template minimal|extended | --template-file <caminho>]
  [--github-actions] [--dry-run] [--overwrite]
```

`repositório` usa o diretório de invocação por padrão. Caminhos gerenciados devem ser relativos, permanecer dentro do repositório e não podem atravessar links simbólicos/reparse points. `--dry-run` relata exatamente os arquivos planejados sem gravar. Repetir o mesmo comando não regrava arquivos inalterados. Arquivos existentes causam erro operacional, salvo quando `--overwrite` autoriza explicitamente a substituição.

`--github-actions` grava `.github/workflows/adr-guard.yml` com `contents: read`, sem credenciais de provider e sem permissão de escrita.

## Configuração

O ADR Guard procura `.adrguard.yml` no diretório de invocação. O schema v1 é intencionalmente limitado a escalares top-level inertes:

```yaml
schema-version: 1
adr-directory: "docs/adr"
template: minimal
# template-file: "docs/templates/team.md" # exclusivo com template
# adr-format: canonical                   # canonical ou madr-4; madr-4 fica reservado para v1.3
```

Propriedades desconhecidas/duplicadas, versões incompatíveis, YAML aninhado, coleções, tags, anchors, aliases, block scalars, UTF-8 inválido, arquivos grandes demais, caminhos inseguros e uso simultâneo de `template`/`template-file` são rejeitados. Caminhos relativos partem do diretório da configuração. A configuração nunca é interpretada como comando e não expande variáveis de ambiente.

A precedência é:

1. Argumento explícito da CLI.
2. Valor de `.adrguard.yml`.
3. Default legado do comando.

O diretório configurado é aplicado quando o argumento posicional é omitido em `check`, `index`, `new` e `draft`. O template configurado é aplicado somente quando `new`/`draft` não recebem opção explícita. `review` continua exigindo target explícito.

## Saída do check

```text
adr-guard check [diretório] [--format text|json|sarif]
```

`text` permanece como default. JSON usa schema `1.0`; SARIF usa `2.1.0`. Ambos são gravados no stdout em sucesso e falha de validação; diagnósticos operacionais usam stderr. Os exit codes continuam `0` para válido, `1` para diagnósticos ADR, `2` para uso/configuração inválida e `3` para falha operacional.

## Solução de problemas

- **Configuração ignorada:** invoque o ADR Guard no diretório de `.adrguard.yml` ou informe diretório/template explicitamente.
- **Recusa de sobrescrita:** inspecione o arquivo existente e use `init --overwrite` somente quando a substituição for desejada.
- **Caminho inseguro:** remova paths absolutos, escapes com `..` ou componentes de link simbólico/reparse point.
- **Relatório estruturado vazio:** JSON/SARIF usa stdout; não redirecione stderr para o mesmo arquivo.
- **CI precisa de Code Scanning:** gere SARIF e use uma etapa separada com `security-events: write`; a Action padrão permanece intencionalmente somente leitura.

## Migração

Nenhuma migração é necessária. Repositórios sem `.adrguard.yml` preservam o comportamento v1.1. Adicione configuração apenas para eliminar argumentos repetidos. Não altere versões de pacote nem tags de release para testar esta preparação via código-fonte.
