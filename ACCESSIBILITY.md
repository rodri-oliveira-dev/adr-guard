# Accessibility statement / Declaração de acessibilidade

[English](#english) · [Português (Brasil)](#português-brasil)

## English

ADR Guard aims to make architecture decision workflows usable by people with disabilities. Accessibility requirements differ across its command-line interface (CLI), VS Code extension, documentation portal and machine-facing integrations.

### Scope and objectives

| Surface | Accessibility objectives |
| --- | --- |
| CLI | Meaningful text diagnostics and exit codes; no essential information conveyed by color or symbols alone; readable output when redirected or consumed by assistive technology. |
| VS Code extension | Keyboard-operable commands and visual forms, meaningful accessible names, predictable focus, accessible validation messages, and compatibility with high-contrast themes and screen readers. |
| Documentation portal and Markdown | Semantic structure, descriptive links, text alternatives for meaningful diagrams and screenshots, keyboard navigation, contrast and responsive layouts. The portal targets [WCAG 2.2 Level AA](https://www.w3.org/TR/WCAG22/). |
| MCP server, JSON/SARIF and CI integrations | Preserve structured, meaningful diagnostics and explicit error information. Presentation and assistive technology support also depend on the consuming client or host. |

The development-only Architecture Agent is not a released product; its terminal interaction is subject to similar CLI objectives if evaluated. Container distribution inherits the CLI interface; the GitHub Action also depends on GitHub's presentation of logs and reports. Agent Skills are textual guidance whose presentation depends on the consuming agent.

### Assessment and limitations

These are objectives, not a claim of conformance. A comprehensive accessibility assessment across supported terminals, VS Code hosts, browsers, screen readers and product versions has not been documented. Compatibility with assistive technologies and the accessibility of third-party clients cannot be guaranteed. Automated checks alone do not establish WCAG conformance.

Future assessments should record component and version, environment, applicable requirements, test method, results and known barriers. Relevant regressions should be covered by focused tests where feasible, without changing versioned public contracts unintentionally.

### Reporting barriers

Report accessibility problems through [GitHub Issues](https://github.com/rodri-oliveira-dev/adr-guard/issues/new/choose). Include the affected component and version, operating system, terminal/editor/browser, assistive technology if relevant, steps to reproduce, and expected versus observed behavior. Do not include private ADRs, credentials or sensitive information. See [SUPPORT.md](SUPPORT.md) for other support channels and [SECURITY.md](SECURITY.md) for private vulnerability reporting.

## Português (Brasil)

O ADR Guard busca tornar os fluxos de decisões arquiteturais utilizáveis por pessoas com deficiência. Os requisitos de acessibilidade variam entre a interface de linha de comando (CLI), a extensão VS Code, o portal de documentação e as integrações voltadas a outras ferramentas.

### Escopo e objetivos

| Superfície | Objetivos de acessibilidade |
| --- | --- |
| CLI | Diagnósticos textuais e códigos de saída significativos; nenhuma informação essencial transmitida apenas por cores ou símbolos; saída legível quando redirecionada ou consumida por tecnologia assistiva. |
| Extensão VS Code | Comandos e formulários visuais operáveis por teclado, nomes acessíveis significativos, foco previsível, mensagens de validação acessíveis e compatibilidade com temas de alto contraste e leitores de tela. |
| Portal de documentação e Markdown | Estrutura semântica, links descritivos, alternativas textuais para diagramas e capturas relevantes, navegação por teclado, contraste e leiaute responsivo. O portal tem como objetivo as [WCAG 2.2 nível AA](https://www.w3.org/TR/WCAG22/). |
| Servidor MCP, JSON/SARIF e integrações de CI | Preservar diagnósticos estruturados e significativos e informações explícitas de erro. A apresentação e o suporte a tecnologias assistivas também dependem do cliente ou ambiente consumidor. |

O Architecture Agent, disponível apenas em desenvolvimento, não é um produto publicado; sua interação por terminal está sujeita a objetivos semelhantes aos da CLI quando avaliada. A distribuição por contêiner herda a interface da CLI; a GitHub Action também depende da apresentação de logs e relatórios pelo GitHub. Agent Skills são orientações textuais cuja apresentação depende do agente consumidor.

### Avaliação e limitações

Esses objetivos não constituem uma declaração de conformidade. Ainda não há avaliação abrangente documentada de acessibilidade entre os terminais, ambientes VS Code, navegadores, leitores de tela e versões dos produtos suportados. Não é possível garantir compatibilidade com tecnologias assistivas ou acessibilidade de clientes de terceiros. Verificações automatizadas isoladas não comprovam conformidade com WCAG.

Avaliações futuras devem registrar componente e versão, ambiente, requisitos aplicáveis, método de teste, resultados e barreiras conhecidas. Sempre que viável, regressões relevantes devem receber testes específicos, sem alterar inadvertidamente contratos públicos versionados.

### Relato de barreiras

Relate problemas de acessibilidade pelo [GitHub Issues](https://github.com/rodri-oliveira-dev/adr-guard/issues/new/choose). Informe componente e versão afetados, sistema operacional, terminal/editor/navegador, tecnologia assistiva quando pertinente, passos para reproduzir e comportamento esperado e observado. Não inclua ADRs privadas, credenciais ou informações sensíveis. Consulte [SUPPORT.md](SUPPORT.md) para outros canais de suporte e [SECURITY.md](SECURITY.md) para comunicação privada de vulnerabilidades.

---

Last reviewed / Última revisão: 2026-11-23.
