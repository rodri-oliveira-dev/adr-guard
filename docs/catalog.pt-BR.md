# Catálogo enriquecido de governança

`adr-guard index --catalog enriched` produz um catálogo Markdown opt-in com data da decisão, responsável, categoria, sucessor e data de revisão. A saída normal de `index` permanece inalterada. Valores ausentes aparecem como `unknown`; o ADR Guard não infere responsáveis nem consulta sistemas externos. As linhas têm ordenação determinística e o conteúdo e os links da tabela são escapados.

