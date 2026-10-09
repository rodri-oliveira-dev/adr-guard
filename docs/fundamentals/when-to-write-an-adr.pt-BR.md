# Quando escrever uma ADR

[English](when-to-write-an-adr.md) · [Início da documentação](../index.pt-BR.md) · [Anterior](why-use-adrs.pt-BR.md) · [Próximo](lifecycle.pt-BR.md)

Escreva uma ADR quando uma escolha importar para pessoas além da implementação imediata ou for cara de redescobrir. O objetivo é registrar justificativas arquiteturalmente relevantes, não toda ação de engenharia.

## Um teste prático

Considere uma ADR quando uma ou mais respostas forem “sim”:

| Sinal | Pergunta |
| --- | --- |
| Impacto | A mudança afeta estrutura, atributo de qualidade, interface pública ou dependência importante? |
| Reversibilidade | Reverter exige migração, indisponibilidade, mudanças de contrato ou muito retrabalho? |
| Alcance | Várias equipes, serviços ou grupos interessados precisam se alinhar? |
| Risco | A escolha afeta segurança, privacidade, conformidade, resiliência ou integridade de dados? |
| Custo | Ela cria obrigações relevantes de operação, licença, pessoas ou suporte? |
| Longevidade | Mantenedores futuros perguntarão por que isso foi escolhido? |
| Debate | Existem alternativas viáveis com trade-offs significativamente diferentes? |

Exemplos incluem escolher o sistema de registro, definir limites de serviços, mudar o modelo de autenticação, adotar um schema de eventos, escolher uma topologia de implantação ou definir uma estratégia de resiliência.

## Quando não escrever

Uma ADR geralmente não é necessária para:

- uma refatoração local sem trade-off relevante fora daquele código;
- um detalhe reversível coberto pela revisão de código normal;
- status de tarefa, ata de reunião ou procedimento passo a passo;
- uma decisão já regida por um padrão organizacional vigente, salvo se a aplicação local ou exceção for relevante;
- uma hipótese que ainda não chegou ao ponto de decisão — registre primeiro o experimento e depois a escolha e suas evidências.

Não use ADRs para contornar as pessoas autorizadas a decidir, justificar uma conclusão após implementá-la sem revelar esse histórico ou congelar uma opção para sempre.

## Escolha a profundidade depois de decidir registrar

Uma decisão pequena, mas relevante, pode usar o [template Minimal](../decision-design/choosing-a-template.pt-BR.md). Uma escolha de alto risco ou entre equipes se beneficia do Extended ou MADR. Categoria e formato são escolhas separadas: uma ADR de segurança pode ser Minimal e uma ADR de dados pode usar MADR.
