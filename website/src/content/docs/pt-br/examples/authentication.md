---
title: Estratégia de autenticação e autorização
description: Registre uma decisão de segurança com requisitos, riscos, trade-offs e limites de responsabilidade.
sidebar:
  order: 5
---

## Contexto e requisitos

Um SaaS multi-tenant precisa de SSO corporativo, sessões curtas, identidade serviço-a-serviço, mudanças de papéis auditáveis e remoção imediata de acesso. A equipe não deve implementar armazenamento de senhas ou protocolos criptográficos.

## Alternativas

1. Construir identidade e emissão de tokens no produto.
2. Usar provedor OpenID Connect gerenciado e validar tokens em cada backend confiável.
3. Colocar toda autorização apenas no gateway.

## Decisão

Usar provedor OpenID Connect gerenciado para autenticação. Backends validam emissor, audiência, assinatura, validade e claims exigidas com bibliotecas mantidas. Verificações gerais de tenant e rota ocorrem no gateway; autorização por recurso permanece no serviço dono. Identidades de serviço usam credenciais próprias, nunca tokens de usuário.

## Consequências

O produto evita custodiar senhas e ganha federação e revogação estabelecidas. Torna-se dependente da disponibilidade do provedor, configuração, rotação de chaves e resposta a incidentes. Políticas em mais de uma camada exigem testes de contrato e eventos de auditoria.

## Riscos e responsabilidades

- Plataforma responde por configuração, rotação, revogação emergencial e clock skew.
- Equipes de serviço respondem pela autorização do recurso e testes deny-by-default.
- Segurança revisa claims, privilégios, retenção de auditoria e threat models.
- Clientes não tomam decisões autoritativas apenas decodificando tokens.

Audiências incorretas, papéis amplos, refresh tokens vazados e grupos obsoletos permanecem riscos. O ADR não substitui threat model, classificação de dados ou plano de incidentes.

## Quando Extended é adequado

Escolhas de segurança distribuem responsabilidade entre provedor, gateway, serviços e clientes. Limites explícitos valem mais que uma declaração curta do fornecedor.
