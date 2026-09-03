# ADR Guard

ADR Guard is a .NET command-line tool for validating and maintaining Architecture Decision Records (ADRs).

> **Status:** foundation phase. ADR parsing, validation rules, and index generation are intentionally outside this first implementation step.

## Current foundation

- .NET 10 CLI application
- unit tests with xUnit.net v3 and Microsoft Testing Platform
- centralized package version management
- shared compiler and analyzer conventions
- deterministic builds with warnings treated as errors
- GitHub Actions build and test workflow

## Requirements

- .NET SDK 10.0.400 or a compatible patch in the same feature band

## Build and test

```bash
dotnet restore AdrGuard.slnx
dotnet build AdrGuard.slnx --configuration Release --no-restore
dotnet test AdrGuard.slnx --configuration Release --no-build
```

## Run

```bash
dotnet run --project src/AdrGuard/AdrGuard.csproj -- --help
dotnet run --project src/AdrGuard/AdrGuard.csproj -- --version
```

Validation commands will be introduced incrementally in the next implementation phases.

## License

Licensed under the MIT License. See [LICENSE](LICENSE).
