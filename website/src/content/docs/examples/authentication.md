---
title: Authentication and authorization strategy
description: Record a security decision with requirements, risks, trade-offs, and responsibility boundaries.
sidebar:
  order: 5
---

## Context and requirements

A multi-tenant SaaS product needs workforce single sign-on, short-lived user sessions, service-to-service identity, auditable role changes, and immediate removal of access for terminated users. The team must not implement password storage or cryptographic protocols itself.

## Alternatives

1. Build identity and token issuance in the product.
2. Use a managed OpenID Connect provider and validate tokens in each trusted backend.
3. Put all authorization only at the API gateway.

## Decision

Use a managed OpenID Connect provider for authentication. Backends validate issuer, audience, signature, lifetime, and required claims using supported libraries. Coarse tenant and route checks occur at the gateway; resource-level authorization remains inside the owning service. Service identities use dedicated workload credentials, never user tokens.

## Consequences

The product avoids password custody and benefits from established federation and revocation controls. It becomes dependent on provider availability, tenant configuration, key rotation, and incident response. Authorization policy exists in more than one layer and requires contract tests and audit events.

## Risks and responsibilities

- Platform owns provider configuration, key rotation, emergency revocation, and clock-skew policy.
- Service teams own resource authorization and deny-by-default tests.
- Security reviews claim design, privilege boundaries, audit retention, and threat models.
- Clients must not make authoritative decisions from decoded tokens alone.

Misconfigured audiences, overly broad roles, leaked refresh tokens, and stale group claims remain risks. The ADR does not replace a threat model, data classification, or incident playbook.

## When Extended fits

Security choices distribute responsibility across provider, gateway, services, and clients. Explicit boundaries are more valuable than a short declaration of the chosen vendor.
