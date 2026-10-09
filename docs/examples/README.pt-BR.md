# Exemplos completos de ADR

[English](README.md) · [Início da documentação](../index.pt-BR.md) · [Seleção de template](../decision-design/choosing-a-template.pt-BR.md)

Estes exemplos expressam a mesma decisão — usar PostgreSQL como sistema de registro de pedidos — em três níveis de estrutura. Eles contêm justificativas completas, não instruções de template. Cada idioma e formato fica em diretório separado porque toda amostra usa intencionalmente o ID `0001` e a seleção de formato vale para o conjunto validado inteiro.

| Exemplo | Por que esta forma serve |
| --- | --- |
| [Minimal em português](canonical-minimal/pt-BR/0001-usar-postgresql-para-pedidos.md) · [inglês](canonical-minimal/en/0001-use-postgresql-for-order-data.md) | A decisão cabe em contexto, escolha e consequências equilibradas. |
| [Extended em português](canonical-extended/pt-BR/0001-usar-postgresql-para-pedidos.md) · [inglês](canonical-extended/en/0001-use-postgresql-for-order-data.md) | Revisores precisam de direcionadores, alternativas, justificativa, riscos e responsáveis explícitos. |
| [MADR 4.0 em português](madr-4/pt-BR/0001-usar-postgresql-para-pedidos.md) · [inglês](madr-4/en/0001-use-postgresql-for-order-data.md) | A equipe usa a estrutura de opções e resultado do MADR. O arquivo é escrito separadamente, não gerado por `new`. |

Os exemplos em português mantêm títulos estruturais invariantes em inglês para que o validador correspondente os reconheça. Seus nomes não podem usar o sufixo documental `.pt-BR.md`, pois ADRs válidas devem obedecer a `NNNN-lowercase-kebab-case.md`; diretórios `pt-BR` separados permitem localização sem enfraquecer o contrato.

## Valide as amostras

Faça o build uma vez e execute cada conjunto isolado no modo declarado:

```bash
dotnet build AdrGuard.slnx
dotnet run --project src/AdrGuard -- check docs/examples/canonical-minimal/en
dotnet run --project src/AdrGuard -- check docs/examples/canonical-minimal/pt-BR
dotnet run --project src/AdrGuard -- check docs/examples/canonical-extended/en
dotnet run --project src/AdrGuard -- check docs/examples/canonical-extended/pt-BR
dotnet run --project src/AdrGuard -- check docs/examples/madr-4/en --adr-format madr-4
dotnet run --project src/AdrGuard -- check docs/examples/madr-4/pt-BR --adr-format madr-4
```

Passar nessas verificações confirma estrutura, nome, status e referências — não o mérito técnico nem a aprovação organizacional da decisão de exemplo.
