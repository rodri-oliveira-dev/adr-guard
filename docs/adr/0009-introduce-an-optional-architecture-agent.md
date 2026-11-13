# Introduce an optional Architecture Agent over MCP and CLI boundaries

## Status

Proposed

## Context

ADR Guard already has two executable products with intentionally different boundaries. The CLI owns commands, presentation, provider-backed advisory workflows, exit-code adaptation, and controlled writes. The MCP server exposes seven versioned, read-only tools over local `stdio`. Both use one internal Core for deterministic parsing, validation, configuration, Git inventory, impact, and drift behavior.

People could benefit from a guided workflow that gathers those structured results, compares alternatives, maintains conversational context, and prepares a proposed action. Embedding that workflow into Core would mix probabilistic orchestration with deterministic rules. Embedding it into MCP would weaken the server's read-only contract. Letting a model write files or invoke arbitrary commands would also bypass the CLI's validation, atomicity, path controls, and stable exit semantics.

The architecture must keep ADR decisions and approvals human-owned. Model output and repository content are untrusted. Impact remains advisory, missing or contradictory evidence remains `unknown` or inconclusive, and deterministic policy results do not approve or reject an ADR.

## Decision

Introduce a separately versioned, optional .NET Architecture Agent as a future product surface. The agent will be a process-level consumer of existing products rather than a new consumer of their implementation assemblies:

```text
Human
  | authorizes context and approves one immutable proposal
  v
Architecture Agent
  |-- model adapter (optional, advisory, untrusted)
  |-- orchestrator and approval gate
  |-- MCP client ----stdio----> adr-guard-mcp ----> AdrGuard.Core (read-only)
  `-- CLI adapter ---process---> adr-guard ----> AdrGuard.Core (authorized writes)
```

The agent will not reference `AdrGuard.Core`, `AdrGuard`, or `AdrGuard.Mcp` as assemblies. It will consume only supported MCP and CLI process contracts. This preserves the graph `AdrGuard -> AdrGuard.Core` and `AdrGuard.Mcp -> AdrGuard.Core` without adding an executable-to-executable or Core-to-agent dependency.

Read workflows will use only the seven existing MCP schema `1.0` tools. The agent will not duplicate parsing, relationship, validation, impact, drift, lifecycle, fingerprint, or policy rules. Unknown contract versions, malformed results, timeouts, missing evidence, and inconclusive outcomes will fail closed or remain explicitly uncertain.

The agent will separate five concerns:

1. A session model records intent, tool evidence, hypotheses, proposals, approval state, and actual results.
2. An orchestrator selects from a closed tool catalog, limits context, preserves provenance, and enforces the workflow state machine.
3. An optional model adapter produces advisory interpretation only. It cannot invoke tools directly, establish evidence, approve a decision, or mutate session authorization.
4. A human approval gate binds authorization to one immutable proposal, including target, content, CLI executable and argument list, and a fingerprint when an existing contract supplies one. Any material change invalidates approval.
5. A CLI process adapter performs the only writes, after approval, by invoking a small allowlist of existing commands with shell execution disabled, discrete arguments, bounded output/time, cancellation, and unchanged exit-code semantics.

Initial delivery will be incremental. It starts with contracts, a threat model, and a provider-free read-only client. Optional model advice follows only after isolation and untrusted-content tests. Proposal and approval are implemented against a fake CLI before one minimal existing write flow is enabled. The preferred first write is creation of a `Proposed` ADR through an existing structured CLI contract; the exact allowlist remains a human decision before implementation. Lifecycle approval, rejection, autonomous supersession, baseline changes, and arbitrary command execution are outside the first release.

This decision does not change CLI commands, exit codes, JSON/SARIF schemas, MCP tools or schemas, Core visibility, or current package contents. It does not claim that an agent package or release exists. Distribution, Package ID, supported provider, interaction surface, and persistent audit format must be decided and verified before public availability is documented.

## Alternatives considered

- **Embed agent orchestration and a model in the MCP server.** This reduces the visible process count but combines untrusted network/model behavior and writes with a deliberately local, read-only evidence boundary.
- **Reference Core directly from the agent.** This avoids protocol overhead but creates another coupled implementation consumer and lets orchestration bypass the published adapter contracts.
- **Reimplement ADR rules in the agent.** This may simplify a prototype but risks different findings, ordering, fingerprints, limits, and security semantics.
- **Add MCP write tools.** This gives the model a uniform interface but breaks the read-only guarantee, expands the blast radius, and requires new versioned public contracts.
- **Use only packaged Agent Skills.** Skills remain useful workflow guidance, but they do not provide a stateful .NET orchestrator, process isolation, contract validation, or an enforceable approval gate.

## Consequences

The selected boundary keeps deterministic evidence in MCP/Core and controlled mutations in the CLI. The agent can operate read-only without provider credentials or write capability, and teams can omit it entirely. Human approval is a technical gate as well as a product statement.

The design adds a third independently versioned executable, process lifecycle management, compatibility negotiation, provider privacy work, and cross-platform integration tests. Users must configure compatible CLI and MCP binaries separately. The agent must clearly distinguish component availability from connection state and from published distribution.

Every enabled write command requires threat modeling, an immutable proposal shape, stale-approval protection, adversarial tests, and documentation in English and Brazilian Portuguese. Logs must be sanitized and bounded. Repository content and model output must never become executable instructions.

If this proposal is accepted later, implementation must follow the incremental plan in `specs/001-architecture-agent/` and satisfy its verifiable acceptance criteria. Acceptance of this ADR would authorize the architecture direction, not autonomous architectural approval and not publication of an agent.
