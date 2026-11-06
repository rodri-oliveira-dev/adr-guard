# ADR Guard para VS Code 1.2.0 — candidata a release

[English](vscode-v1.2.0.md)

ADR Guard para VS Code `1.2.0` está preparada, mas **não foi publicada**. A versão pública atual no Marketplace continua sendo `1.1.1`, verificada pela Gallery API do Marketplace em 06/11/2026.

## Destaques

- Adiciona um grafo visual somente leitura dos relacionamentos declarados entre ADRs, com seleção de nó, informações essenciais da decisão e abertura segura do arquivo Markdown original.
- Adiciona busca, filtros por status e tipo de relacionamento, além de foco em uma ADR e suas arestas diretas de entrada e saída.
- Distingue direção de dependência e supersessão e destaca diagnósticos existentes de ciclos, referências quebradas e ambíguas sem inferir relacionamentos.
- Adiciona recarga explícita, estados de carregamento/vazio/erro/incompatibilidade, navegação por teclado, foco móvel, indicadores visíveis de foco, rótulos acessíveis e localização em inglês e pt-BR.
- Aceita relatórios limitados a 5.000 ADRs e 10.000 relacionamentos, renderizando no máximo 100 nós correspondentes. Relatórios maiores precisam ser filtrados ou focados antes da renderização.

## Compatibilidade e segurança

- Exige uma CLI instalada separadamente que exponha o schema `1.0` de `adr-guard relationships`; a CLI `1.12.0` preparada fornece esse contrato.
- Antes de carregar um relatório, a extensão consulta `relationships --help`. Comando ausente, versão de schema incompatível, saída malformada e limites excedidos produzem estados fail closed explícitos, sem parsing local alternativo.
- A navegação para Markdown aceita somente um caminho exato do relatório carregado que resolva para um arquivo regular `.md` existente e fisicamente contido no workspace `file:` confiável selecionado.
- A extensão nunca cria, edita, aprova, substitui ou altera o status de ciclo de vida de uma ADR pelo grafo. O VSIX não inclui CLI nem MCP.

## Empacotamento e publicação

Depois do merge, execute **VS Code VSIX Package** na `main` para produzir e validar `adr-guard-1.2.0.vsix`. O workflow instala pelo lockfile, audita, aplica lint, verifica tipos, compila, executa testes unitários/de localização/do host da extensão, empacota o VSIX, instala e executa smoke test de ativação.

O workflow envia um artifact com retenção de 21 dias e **não** publica no Visual Studio Marketplace. A publicação no Marketplace exige uma ação separada e explicitamente autorizada da pessoa proprietária, com autenticação do Marketplace, seguida da verificação de que `rodrioliveira.adr-guard` informa `1.2.0`.
