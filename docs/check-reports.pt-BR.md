# JSON, SARIF e GitHub Code Scanning

> `--diagnostic-locations` está disponível atualmente apenas em builds do código-fonte e não está incluída no pacote publicado da CLI 1.8.0. A saída JSON e SARIF existente continua sendo o padrão publicado.

`adr-guard check --format json` emite o [schema v1 do relatório do ADR Guard](schemas/adr-check-report-v1.schema.json). Diagnósticos são ordenados por arquivo, código e mensagem. `valid` representa a validação estrutural; resumo e lista de arquivos tornam explícitos os relatórios vazios/de sucesso. Adicione `--diagnostic-locations` para habilitar as propriedades opcionais `line` e `column`, baseadas em um, quando o validador consegue identificar uma localização a partir do Markdown interpretado. As duas propriedades são omitidas quando a evidência identifica somente o arquivo. Sem a flag, a saída permanece compatível com clientes estritos do schema `1.0` anteriores a essas propriedades opcionais.

`adr-guard check --format sarif` emite SARIF 2.1.0. Cada diagnóstico vira um resultado com `ruleId` estável, mensagem, severidade e URI do artefato adequada ao workspace. Com `--diagnostic-locations`, posições conhecidas viram uma `region`; diagnósticos limitados ao arquivo a omitem. O ADR Guard nunca inventa linha ou coluna quando o validador não possui evidência de localização.

Para `ADR008`, uma seção `Superseded by` existente, porém vazia ou inutilizável, é localizada no heading interpretado. Quando a própria seção está ausente, o achado continua limitado ao arquivo porque não é possível comprovar um ponto exato de inserção.

Mantenha a validação normal somente leitura. Para upload no Code Scanning, use um job/etapa separado e conceda `security-events: write` apenas a ele:

```yaml
permissions:
  contents: read

jobs:
  adr-sarif:
    permissions:
      contents: read
      security-events: write
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
      - run: dotnet tool restore
      - name: Produzir SARIF mesmo com falha de validação
        shell: bash
        run: |
          set +e
          dotnet tool run adr-guard check docs/adr --format sarif > adr-guard.sarif
          status=$?
          test "$status" -eq 0 || test "$status" -eq 1
      - uses: github/codeql-action/upload-sarif@v4
        with:
          sarif_file: adr-guard.sarif
```

Em produção, fixe actions de terceiros em SHAs revisados. PRs de forks podem não ter permissão para upload de security events; execute o check somente leitura em forks e faça upload apenas em workflow confiável que nunca faça checkout nem execute código não confiável com credenciais elevadas.
