# Keep the CLI dependency-free

## Status

Accepted

## Context

The initial CLI exposes only two commands and a small set of options. Adding a command-line framework now would introduce another runtime dependency and more abstractions than the current command surface requires.

## Decision

Parse command-line arguments with the .NET base class library for the initial release.

Keep command execution separated from parsing so a dedicated CLI framework can be introduced later without changing the validation or indexing layers.

## Consequences

The executable stays small and has no third-party runtime dependencies. Argument parsing is more manual, so new command shapes must be added carefully and covered by integration tests. If the CLI grows substantially, this decision should be revisited.
