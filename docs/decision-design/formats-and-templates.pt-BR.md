# Formatos e templates de ADR

[English](formats-and-templates.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](decision-categories.pt-BR.md) · [Próximo](choosing-a-template.pt-BR.md)

Três ideias são fáceis de confundir:

- Uma **categoria de decisão** é o assunto, como dados ou segurança.
- Um **template** é um ponto de partida para escrita, com seções e orientação.
- Um **formato de validação** é o contrato estrutural aplicado a um diretório de ADRs.

## O que o ADR Guard suporta

A validação **canônica**, padrão do ADR Guard, exige título e seções não vazias de nível dois `Status`, `Context`, `Decision` e `Consequences`. Os status canônicos são `Proposed`, `Accepted`, `Deprecated` e `Superseded`.

O comando `new` pode gerar registros canônicos a partir de:

- `minimal` interno, que é o padrão;
- `extended` interno;
- um único arquivo Markdown local selecionado com `--template-file`.

Extended é um template canônico do ADR Guard com direcionadores, opções, justificativa, consequências, riscos e referências adicionais. **Ele não é MADR.** Templates Custom continuam canônicos e precisam preservar títulos obrigatórios, status inicial `Proposed` e contrato estrito de placeholders. Markdown arbitrário não é aceito.

**MADR 4.0** é um formato de validação separado e opcional, selecionado com `--adr-format madr-4`. O ADR Guard valida um subconjunto explícito da estrutura externa. Não existe um gerador equivalente `new --template madr-4`; escreva ou obtenha a ADR MADR separadamente e valide-a nesse modo.

## Abordagens educacionais e contratos do produto

Y-Statements (“No contexto de…, diante de…, decidimos…”) podem ajudar a formular uma decisão concisa. Outros formatos também podem trazer boas ideias. Eles são abordagens educacionais, não formatos do validador ADR Guard, salvo se o arquivo resultante satisfizer de forma independente o contrato canônico ou MADR 4.0 selecionado.

A seleção de formato vale para todo o conjunto validado. Mantenha ADRs canônicas e MADR em diretórios separados e use comandos separados; o ADR Guard não tenta adivinhar o formato de cada arquivo.
