# Phase 3: geração nativa de ADRs MADR 4.0

## Objetivo e limites

Preparar a geração offline de ADRs no subconjunto MADR 4.0 já aceito pelo
ADR Guard, sem implementar o gerador nesta etapa. A implementação futura deve
preservar integralmente o fluxo canônico, manter a aprovação da decisão sob
responsabilidade humana e não iniciar trabalho do grafo.

Esta fase de planejamento não altera código, schemas, argumentos publicados,
dependências ou comportamento de runtime.

## Evidência consultada

- `docs/madr-4.md` e `docs/madr-4.pt-BR.md`: o formato é opt-in, a opção
  explícita prevalece sobre `adr-format: madr-4` e a ausência de ambos mantém
  `canonical`.
- [Templates oficiais MADR 4.0.0](https://github.com/adr/madr/tree/4.0.0/template):
  estrutura obrigatória, metadata opcional e hierarquia das seções.
- Fluxos atuais de `NewCommand`, `NewFormCommand`, `AdrMarkdownRenderer`,
  `AdrNewService`, `AdrCreationService`, catálogo/contratos de formulário e seus
  testes correspondentes.

## Decisões

### Seleção do formato

O comando `new` aceitará `--adr-format canonical|madr-4`. A resolução será
determinística e seguirá esta precedência:

1. `--adr-format` informado explicitamente;
2. `adr-format` da configuração efetiva;
3. `canonical`.

`ConfigurationArguments` deverá aplicar `AddAdrFormatIfMissing` também a
`new`. Assim, o mecanismo existente preserva a opção explícita e injeta a
configuração somente quando a opção não existe. `NewCommand` e
`NewFormCommand` validarão apenas os dois valores conhecidos e transportarão
um `AdrFormat`, não uma string livre, para a geração.

Sem opção nem configuração, help, preview, criação, templates, conteúdo,
validação e códigos de saída canônicos devem permanecer inalterados.

### Compatibilidade entre formato e template

| Formato | `minimal` | `extended` | `--template-file` |
| --- | --- | --- | --- |
| `canonical` | comportamento atual | comportamento atual | comportamento atual |
| `madr-4` | template MADR mínimo | template MADR completo | erro de uso explícito |

Templates customizados atuais possuem contrato estrutural canônico. O sistema
não deve inferir, converter ou reinterpretar esses arquivos como MADR. Suporte
a template MADR customizado exige uma proposta contratual separada.

### Saída MADR 4.0

A saída usará LF e terminará com newline, como o renderer de templates atual.
O título continuará escapado como texto Markdown inline e o nome do arquivo
continuará sob a política existente.

O front matter inicial será mínimo e inteiramente controlado pelo produto:

```yaml
---
status: "proposed"
---
```

Nenhum texto do usuário será interpolado no YAML. `date` não será gerado a
partir do relógio, pois isso quebraria a determinação para entradas idênticas.
`decision-makers`, `consulted` e `informed` também serão omitidos nesta entrega;
o MADR os define como opcionais. Inclusão futura de metadata exigirá campos
explícitos, limites e serialização segura, sem tratar YAML como superfície de
execução.

A ordem e a hierarquia serão:

```text
# <título>
## Context and Problem Statement       obrigatória, não vazia
## Decision Drivers                    opcional
## Considered Options                  obrigatória, não vazia
## Decision Outcome                    obrigatória, não vazia
### Consequences                       opcional, filha de Decision Outcome
### Confirmation                       opcional, filha de Decision Outcome
## Pros and Cons of the Options        opcional
## More Information                    opcional
```

Os headings estruturais permanecerão em inglês nas culturas `en-US` e
`pt-BR`, porque são parte do contrato reconhecido pelo validador. Somente o
texto de orientação será localizado.

O template `minimal` conterá apenas as três seções obrigatórias. O template
`extended` seguirá a ordem completa acima. No fluxo tradicional, cada seção
emitida conterá orientação editável e não representará aprovação. No fluxo
de formulário, seções opcionais vazias serão omitidas.

### Renderer e serviços

`AdrTemplateDefinition` e `AdrMarkdownRenderer` hoje codificam invariantes do
formato canônico, inclusive `Status`, headings obrigatórios e seções somente em
nível dois. A implementação não deve enfraquecer esses guards para acomodar
MADR. Deve introduzir uma definição estrutural discriminada por `AdrFormat` e
uma renderização MADR dedicada, compartilhando apenas normalização, limites,
escape e substituição segura que sejam realmente comuns.

`AdrNewService` transportará o formato resolvido durante carregamento,
preview, renderização sob mutex e persistência. `AdrCreationService.Prepare` e
a revalidação dentro do lock usarão o mesmo formato, removendo o uso fixo de
`AdrFormat.Canonical`. O conteúdo final realocado continuará sendo renderizado,
validado e persistido atomicamente sob o mesmo lock. Nenhuma relação ou regra
de grafo nova faz parte desta fase.

### Integração com `new --form`

O envelope JSON continuará exatamente no schema `1.0`: mesmas propriedades,
operações, regras estritas de parsing, limites, rejeição de membros
desconhecidos e formato de resposta. O formato será selecionado somente por
`--adr-format`/configuração, fora do payload; não será adicionada uma
propriedade `format` ao JSON.

Para `canonical`, `describe` e os campos atuais permanecem idênticos. Para
`madr-4`, `describe` usará os mesmos nomes de template (`minimal` ou
`extended`) e anunciará os campos:

| Campo | `minimal` | `extended` |
| --- | --- | --- |
| `contextAndProblemStatement` | obrigatório | obrigatório |
| `decisionDrivers` | ausente | opcional |
| `consideredOptions` | obrigatório | obrigatório |
| `decisionOutcome` | obrigatório | obrigatório |
| `consequences` | ausente | opcional |
| `confirmation` | ausente | opcional |
| `prosAndConsOfOptions` | ausente | opcional |
| `moreInformation` | ausente | opcional |

Isso preserva a forma do schema `1.0` e permite que consumidores usem o fluxo
existente `describe -> preview/create`. Um consumidor que não passa nem recebe
configuração MADR continua observando exatamente o catálogo canônico. Preview
e create devem usar a mesma definição e produzir conteúdo idêntico, exceto
pelos campos de resposta já condicionais. `--template-file` continuará sendo
descrito como incompatível com o formulário.

## Plano de alteração

1. Estender o parsing/help de `NewCommand` e `NewFormCommand` com
   `--adr-format canonical|madr-4`, preservando o default.
2. Aplicar `adr-format` configurado a `new` pelo resolvedor existente, com
   cobertura da precedência explícita.
3. Separar definição/renderização estrutural MADR dos invariantes canônicos e
   adicionar os templates MADR `minimal` e `extended` nas duas culturas.
4. Tornar `AdrNewService` e `AdrCreationService` conscientes do formato em todas
   as validações, inclusive a executada sob o mutex.
5. Tornar `AdrFormTemplateCatalog` consciente do formato e mapear os campos
   MADR sem alterar os records JSON privados do schema `1.0`.
6. Atualizar documentação pública equivalente em inglês e pt-BR, portal,
   exemplos e changelog somente junto da implementação, sem alegar publicação
   antes do canal correspondente existir.

## Plano de testes

- Regressão byte a byte do default canônico, de seus templates, help, preview,
  criação e formulário schema `1.0`.
- Matriz de resolução: default canônico, configuração MADR, argumento MADR e
  argumento canônico sobrescrevendo configuração MADR.
- Rejeição determinística de formato desconhecido e de
  `madr-4 + --template-file`, sem escrita.
- Snapshots dos templates MADR por cultura e tamanho, ordem, heading level,
  newline, front matter fixo, título escapado e seções obrigatórias não vazias.
- Guards contra heading estrutural duplicado/injetado, placeholder desconhecido,
  ampliação acima do limite, NUL e metadata controlada pelo usuário.
- Validação do conjunto existente e do candidato com `AdrFormat.Madr4`, tanto
  no preview quanto na realocação concorrente sob mutex.
- `new --form` MADR para `describe`, `preview` e `create`, incluindo campos
  obrigatórios, omissão de opcionais vazios, igualdade preview/arquivo e
  rejeição de campos de outro formato.
- Confirmação de que nenhum teste depende de data corrente, cultura do host,
  ordem de execução, rede ou arquivos do usuário.

Na implementação, executar primeiro os testes focados de `NewCommand`,
`NewFormCommand`, renderer e serviços de criação; depois executar o baseline da
solução conforme a skill `run-tests` e o overlay do repositório.

## Riscos e mitigações

- **Regressão canônica:** manter caminhos discriminados e snapshots do default;
  não generalizar guards canônicos.
- **Validação com formato divergente:** transportar um único `AdrFormat` do
  boundary até a validação final sob lock.
- **YAML inseguro ou não determinístico:** emitir somente o literal fixo de
  status; não interpolar entrada nem usar relógio.
- **Quebra silenciosa do formulário:** manter schema `1.0`, preservar o catálogo
  canônico e exigir seleção MADR fora do payload.
- **Mistura de formatos no diretório:** validar todo o conjunto com o formato
  selecionado e falhar antes de persistir, sem inferência por arquivo.
- **Escopo crescer para grafo ou templates customizados:** rejeitar combinações
  não suportadas e tratar essas capacidades em propostas separadas.

## Critérios de conclusão da implementação futura

- O comportamento canônico permanece o default e passa nas regressões atuais.
- MADR é gerado apenas por seleção explícita ou configuração efetiva.
- A saída MADR passa no validador MADR 4 existente antes de qualquer escrita.
- Preview, create e form compartilham seleção, renderer, validação e limites.
- O schema de formulário continua `1.0` e contratos publicados não são
  alterados inadvertidamente.
- Nenhuma dependência nova ou comportamento de grafo é introduzido.
