# Use provider-agnostic AI draft generation

## Status

Accepted

## Context

ADR Guard is introducing AI-assisted drafting for Architecture Decision Records. Tying the core workflow directly to one vendor SDK would couple the CLI to a specific provider, add runtime dependencies, and make future support for other providers harder.

AI-generated content must also remain subject to the same structural rules as manually authored ADRs, and an AI provider must not be able to mark an architectural decision as accepted on behalf of a human reviewer.

Users also need to control the language and regional convention used for generated prose. Deriving that behavior from the machine or process culture would make the same command produce different requests across environments.

## Decision

Introduce an internal provider abstraction for ADR generation. Provider implementations return only the generated Context, Decision, and Consequences content.

The provider-agnostic generation request carries a standard .NET globalization culture name. The `draft` command accepts `--culture <name>`, validates it with `System.Globalization`, normalizes it through `CultureInfo`, and defaults deterministically to `en-US` when the option is omitted.

Real provider adapters are responsible for translating the selected culture into provider-specific AI instructions. Culture controls generated prose only; ADR structural headings, filename rules, ID allocation, and the `Proposed` status remain canonical and validator-compatible.

ADR Guard owns the final Markdown structure, assigns the ADR ID and compliant filename, forces the generated status to Proposed, parses the candidate with the existing Markdown parser, and validates the complete ADR set before writing the new file.

Keep provider-specific contracts behind the abstraction and use .NET base class library capabilities for future HTTP integrations rather than adding provider SDK dependencies to the core CLI.

## Consequences

The generation workflow can be tested deterministically without network access and can support multiple AI providers without changing the core ADR creation logic.

The generation language is explicit and stable across machines, while providers receive enough information to request localized prose without introducing an ADR Guard-specific language enumeration.

AI-assisted drafts cannot bypass ADR Guard validation or automatically become accepted decisions. Real provider adapters still need to be implemented separately, and provider-specific prompt construction, authentication, and HTTP error handling remain outside this decision.
