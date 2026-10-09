# Categorias de decisão

[English](decision-categories.md) · [Início da documentação](../index.pt-BR.md) · [Próximo](formats-and-templates.pt-BR.md)

Uma categoria descreve **o que a escolha afeta**. Um template ou formato descreve **como o registro é organizado**. Não confunda os conceitos: qualquer categoria pode usar Minimal, Extended, Custom ou uma ADR MADR escrita separadamente quando seu modo de validação estiver selecionado.

| Categoria | Perguntas de exemplo |
| --- | --- |
| Arquitetura do sistema | Onde ficam os limites dos componentes? Quais responsabilidades pertencem juntas? |
| Dados | Qual é o sistema de registro? Como tratar propriedade, consistência, retenção e migração? |
| Integração | Qual contrato de API, evento, mensageria ou compatibilidade conecta sistemas? |
| Segurança e privacidade | Como tratar identidade, autorização, segredos, ameaças e dados sensíveis? |
| Infraestrutura e implantação | Qual topologia, plataforma, região ou abordagem de entrega será usada? |
| Resiliência | Qual meta de disponibilidade, isolamento de falhas, retry, recuperação ou continuidade se aplica? |
| Observabilidade | Quais sinais, correlação, retenção e responsabilidades tornam o comportamento diagnosticável? |
| Desenvolvimento e entrega | Qual convenção de build, teste, branches, dependências ou release tem impacto arquitetural? |
| Governança | Quem é responsável por um contrato, qual padrão se aplica ou como uma decisão entre equipes é controlada? |

Categorias ajudam a descobrir decisões, mas não são labels obrigatórias no ADR Guard. Uma decisão pode atravessar vários domínios; mantenha título e escopo em torno do único resultado que precisa ser decidido. Se duas escolhas podem mudar de forma independente, normalmente merecem ADRs separadas.

A categoria também ajuda a encontrar revisores. Retenção de dados pode exigir segurança e jurídico; resiliência pode exigir operações e produto. Ela não deve determinar automaticamente status, autoridade de aprovação ou profundidade do template — isso depende da governança e do risco.
