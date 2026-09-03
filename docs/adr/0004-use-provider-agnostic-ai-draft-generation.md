# Use provider-agnostic AI draft generation

## Status

Accepted

## Context

ADR Guard is introducing AI-assisted drafting for Architecture Decision Records. Tying the core workflow directly to one vendor SDK would couple the CLI to a specific provider, add runtime dependencies, and make future support for other providers harder.

AI-generated content must also remain subject to the same structural rules as manually authored ADRs, and an AI provider must not be able to mark an architectural decision as accepted on behalf of a human reviewer.

## Decision

Introduce an internal provider abstraction for ADR generation. Provider implementations return only the generated Context, Decision, and Consequences content.

ADR Guard owns the final Markdown structure, assigns the ADR ID and compliant filename, forces the generated status to Proposed, parses the candidate with the existing Markdown parser, and validates the complete ADR set before writing the new file.

Keep provider-specific contracts behind the abstraction and use .NET base class library capabilities for future HTTP integrations rather than adding provider SDK dependencies to the core CLI.

## Consequences

The generation workflow can be tested deterministically without network access and can support multiple AI providers without changing the core ADR creation logic.

AI-assisted drafts cannot bypass ADR Guard validation or automatically become accepted decisions. Real provider adapters still need to be implemented separately, and provider-specific authentication and HTTP error handling remain outside this decision.
