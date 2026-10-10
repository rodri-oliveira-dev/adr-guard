# Metadados opcionais de ADR

O ADR Guard pode interpretar front matter opcional para `date`, `last-reviewed`, `owner`, `decision-makers`, `stakeholders`, `consulted`, `informed`, `requirements`, `follow-ups`, `review-trigger` e `category`. Listas usam escalares separados por vírgula ou ponto e vírgula. O `## Status` canônico continua soberano; MADR 4.0 mantém a autoridade de metadados documentada.

Defina `metadata-policy: validate` para reportar chaves duplicadas, recursos YAML não suportados, valores grandes demais, datas ISO inválidas e links de acompanhamento malformados. O ADR Guard nunca acessa links, avalia tags/âncoras YAML, infere aprovação ou coleta identidades. Publique apenas nomes ou contatos fornecidos intencionalmente e adequados à visibilidade do repositório.

