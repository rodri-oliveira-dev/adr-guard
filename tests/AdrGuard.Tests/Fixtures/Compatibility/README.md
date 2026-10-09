# ADR practice compatibility fixtures

This directory inventories the regression fixtures introduced by ADR #0005. Tests add focused documents under `canonical`, `madr-4`, `links`, `relationships`, `identity`, `metadata`, and `security` as each policy is implemented.

Every fixture must state whether it exercises unchanged default behavior or an explicit opt-in policy. Hostile fixtures are data only and must never be executed or resolved outside their temporary repository root.

