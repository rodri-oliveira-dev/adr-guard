# Templates opcionais no `draft` assistido por IA

O fluxo `draft` com seleção de templates está implementado na branch `feature/issues-59` para a entrega coordenada da v1.1.0. O comando **sem** `--template` ou `--template-file` mantém a renderização histórica exata e aceita os mesmos nomes de culturas .NET anteriormente suportados.

```bash
adr-guard draft ./docs/adr --title "Adotar Redis" --context "Precisamos de cache limitado." \
  --provider openai --model SEU_MODELO --template minimal --culture pt-BR --preview

adr-guard draft ./docs/adr --title "Adotar Redis" --context "Precisamos de cache limitado." \
  --provider openai --model SEU_MODELO --template extended --culture pt-BR

adr-guard draft ./docs/adr --title "Adotar Redis" --context "Precisamos de cache limitado." \
  --provider openai --model SEU_MODELO --template-file ./templates/equipe.md
```

**Seleção.** `--template minimal|extended` e `--template-file <caminho>` são opcionais, mutuamente exclusivos e não podem se repetir. A orientação do template suporta apenas `en-US` e `pt-BR`; quando `--culture` é omitido, usa-se `en-US`. Caminhos relativos são resolvidos a partir do diretório de trabalho da invocação, **não** do diretório de saída das ADRs. Arquivo personalizado ausente, inacessível, malformado, sem UTF-8 válido ou grande demais é rejeitado antes de construir ou chamar o provedor. O arquivo tem limite de 65.536 bytes e placeholders estritos descritos em [custom-templates.pt-BR.md](custom-templates.pt-BR.md). Use `--preview` / `--dry-run` para inspecionar o Markdown proposto sem gravar arquivos.

## Dados exatos enviados ao provedor de IA

A seleção do template altera **somente a renderização local, após a resposta do provedor**. O ADR Guard não acrescenta ao pedido o nome do template, o Markdown de origem, os placeholders, as instruções localizadas, as seções suplementares, o caminho do arquivo nem qualquer outro byte do template personalizado. O provedor recebe o `AdrGenerationRequest` existente: título da ADR sem espaços externos, contexto arquitetural composto como antes e cultura .NET solicitada. O contexto arquitetural contém:

- O texto obrigatório `--context`, normalizado e sujeito aos limites de tamanho existentes.
- Apenas os arquivos selecionados individualmente via `--context-file`, com limites e leitura pelo carregador já existente; arquivos não selecionados não são varridos ou anexados.
- Conteúdo das ADRs existentes já interpretadas **somente** quando `--include-existing-adrs` for explicitamente informado, com o limite determinístico já existente de 12.000 caracteres. Sem a opção, nenhum texto de ADR anterior é enviado.

Os três campos de resposta do provedor são validados e inseridos **uma única vez** em `{{context}}`, `{{decision}}` e `{{consequences}}` pelo renderizador compartilhado. A orientação do template e as seções opcionais continuam como **instruções editoriais locais**, não como contexto ou instruções enviados à IA. Se um template omitir o placeholder de determinado campo, esse campo não entra no Markdown salvo. O comando não pede à IA para preencher outras seções específicas do template. As credenciais continuam configuradas por variáveis de ambiente, sem serem registradas em logs pela seleção do template.

## Estrutura, segurança e concorrência

As ADRs geradas mantêm `Proposed`, exatamente um H1, seções obrigatórias invariáveis e as regras atuais do parser/validador. O caminho do template nunca determina o nome ou o destino do arquivo criado. Títulos H1/H2 gerados pelo provedor (exceto em blocos de código cercados por delimitadores Markdown) são rejeitados em drafts **com template**, em vez de inseridos como estrutura adicional. O draft padrão, sem template, preserva suas regras de validação existentes.

Quando outro criador cooperante grava uma ADR após a alocação para a prévia, a persistência com template renderiza `{{id}}` novamente com o **ID final**, sob o mutex de criação compartilhado, valida o mesmo conteúdo e grava de maneira atômica, sem sobrescrita. Não há chamada ao provedor dentro do mutex. O Markdown retornado corresponde aos bytes efetivamente persistidos. A prévia usa um ID provisório e não reserva nada; a gravação posterior pode receber outro ID.

Os templates criam documentos editáveis com status **Proposed**, não aprovações arquiteturais automáticas. A revisão humana continua necessária. O contrato público da GitHub Action `@v1` permanece inalterado e não expõe `new` nem `draft` assistido por IA.
