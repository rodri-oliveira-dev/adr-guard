# Adote ADRs em equipe

[English](team-adoption.md) · [Início da documentação](../index.pt-BR.md) · [Tutorial da primeira ADR](getting-started.pt-BR.md)

Adote ADRs como prática de decisão, não como meta de documentos. Comece com poucas escolhas relevantes, combine responsabilidade e revisão e melhore o processo com a experiência.

## Estabeleça uma política leve

Combine:

- quais sinais de relevância exigem uma ADR;
- onde ficam registros canônicos ou MADR e qual formato cada diretório usa;
- qual é o template padrão e quando mais detalhe é esperado;
- quem propõe, quem deve ser consultado e quem pode aceitar;
- como manter registros `Deprecated` e `Superseded`;
- quando executar validação local e em CI;
- como participantes sem acesso ao repositório colaboram.

Evite um único fluxo de aprovação quando os riscos variam. Uma escolha reversível de um serviço pode exigir seu responsável; um limite de segurança ou contrato entre equipes pode exigir revisão mais ampla.

## Fluxo sugerido em pull requests

1. Abra uma ADR `Proposed` antes que a implementação torne a escolha cara de mudar.
2. Solicite revisão aos responsáveis e especialistas afetados.
3. Resolva divergências materiais atualizando contexto, opções ou consequências — não apagando o trade-off divergente.
4. Registre o resultado humano e altere o status conforme a autoridade da equipe.
5. Valide o conjunto e gere novamente o índice.
6. Relacione a implementação quando útil e observe se as premissas continuam verdadeiras.

O ADR Guard pode aplicar a estrutura determinística localmente, na [GitHub Action](../github-action.pt-BR.md) ou pela [extensão VS Code](../../extensions/vscode/README.pt-BR.md). Ele cria propostas offline e oferece escrita ou revisão opcional com IA. Nenhum desses mecanismos decide pela equipe.

## Adote progressivamente

- Comece por novas decisões; não tente reconstruir toda escolha histórica.
- Recupere apenas decisões vigentes cuja justificativa ausente gere risco ou trabalho repetido.
- Revise algumas ADRs iniciais em conjunto para calibrar a profundidade útil.
- Observe descoberta e status desatualizados como sinais qualitativos. Evite métricas de ROI sem fonte ou metas de quantidade.
- Periodicamente, amostre ADRs aceitas: premissas continuam verdadeiras, responsáveis são conhecidos e links de substituição estão corretos?

## Governança sem burocracia

Torne o caminho padrão rápido: Minimal, revisão focada e responsabilidade clara. Acrescente Extended ou MADR quando risco e complexidade justificarem. A validação evita desvio estrutural; a revisão avalia mérito técnico; a autoridade da equipe aceita. Separar essas responsabilidades torna a prática confiável.
