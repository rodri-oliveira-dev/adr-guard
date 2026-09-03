# Validate before writing the ADR index

## Status

Accepted

## Context

An index is useful only when it describes a coherent ADR set. Generating it from invalid files could make broken references, duplicate IDs, or incomplete decisions look authoritative.

## Decision

Run the full ADR validation before writing an index.

If validation fails, return the validation exit code and leave the existing index untouched. When validation succeeds, generate the index deterministically and avoid rewriting the file when its content is already current.

## Consequences

Index generation is safe to run in CI and local workflows. Invalid ADRs must be corrected before the index can be refreshed, which is an intentional constraint rather than a partial-success mode.
