# Escreva sua primeira ADR

[English](getting-started.md) · [Início da documentação](../index.pt-BR.md) · [Exemplos completos](../examples/README.pt-BR.md)

Este tutorial começa pela decisão, não pela ferramenta. Você formulará uma escolha realista, criará um registro canônico Minimal, substituirá as instruções por justificativa real, fará a revisão, validará e gerará um índice.

## 1. Identifique uma decisão que merece registro

Imagine que um serviço de pedidos armazene dados críticos em um banco de documentos. Novos requisitos de auditoria e consistência exigem atualizações atômicas entre cabeçalho, itens e pagamentos. Reverter a escolha de armazenamento exigiria migração e operação, portanto ela merece uma ADR.

Antes de escolher tecnologia, anote:

- o problema: mudanças atômicas e auditabilidade;
- as restrições: dados existentes, janela limitada de migração e responsabilidade pelo serviço;
- as opções viáveis: manter o banco atual com lógica compensatória ou adotar um banco relacional;
- os direcionadores: consistência, auditoria, risco de migração e suporte operacional.

## 2. Escolha um modelo

Use **Minimal** quando comparação e consequências couberem com clareza nas seções centrais. Use [Extended](../decision-design/choosing-a-template.pt-BR.md) se revisores precisarem de análise explícita por opção. Este tutorial usa Minimal.

## 3. Instale e inicialize

Instale a .NET Tool publicada e execute na raiz de um repositório existente:

```bash
dotnet tool install --global RodriOliveira.AdrGuard
adr-guard init . --adr-directory docs/adr --template minimal
adr-guard new docs/adr --title "Usar PostgreSQL como sistema de registro de pedidos" --culture pt-BR
```

`init` cria o diretório e `.adrguard.yml`; não substitui configuração existente diferente sem `--overwrite` explícito. `new` cria o próximo arquivo `NNNN-lowercase-kebab-case.md` como `Proposed`. Ele funciona offline e não atualiza o índice.

## 4. Substitua as instruções

Edite o arquivo gerado. Remova cada instrução `[EDITAR: ...]` e escreva conteúdo completo:

- **Context:** fatos, restrições, participantes e por que a decisão é necessária agora.
- **Decision:** opção escolhida, escopo e motivo de atender aos direcionadores.
- **Consequences:** benefícios e desvantagens, incluindo migração e responsabilidade operacional.

Use o [exemplo Minimal completo](../examples/canonical-minimal/pt-BR/0001-usar-postgresql-para-pedidos.md) como referência, não como texto para copiar sem verificar seu contexto.

## 5. Revise e decida

Abra a ADR no fluxo normal de pull request. Inclua responsáveis pelo serviço e plataforma e participantes de segurança, dados ou produto afetados. Revise evidências, alternativas, custos, modos de falha e premissas.

Mantenha `Proposed` durante a discussão. Apenas um processo humano autorizado muda o status para `Accepted`. Validação aprovada ou sugestão de IA não é aprovação.

## 6. Valide e gere o índice

```bash
adr-guard check docs/adr
adr-guard index docs/adr
git status --short -- .adrguard.yml docs/adr
git add -- .adrguard.yml docs/adr
git diff --cached -- .adrguard.yml docs/adr
```

`check` retorna `0` quando o conjunto passa na validação estrutural e `1` quando há achados. `index` valida primeiro e depois cria ou atualiza `docs/adr/README.md` de forma determinística. Confira o `git status` antes de adicionar arquivos ao staging, para evitar incluir arquivos não relacionados. `git add` também inclui arquivos novos, e `git diff --cached` mostra a nova ADR, o índice gerado e a configuração, além das mudanças nos arquivos já rastreados. Revise o diff preparado antes de fazer o commit da ADR e do índice, quando o projeto versionar esse arquivo.

Você criou a primeira decisão de um histórico durável. Continue com [adoção em equipe](team-adoption.pt-BR.md) ou consulte [opções de criação e exit codes](../creation.pt-BR.md).
