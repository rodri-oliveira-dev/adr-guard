# syntax=docker/dockerfile:1

# .NET SDK 10.0.403, Alpine 3.24.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24@sha256:5562fd626eec39f6f53218f9b7d7856ab7195d51d846568304fbe85071a3b6e6 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/AdrGuard.Core/AdrGuard.Core.csproj src/AdrGuard.Core/
COPY src/AdrGuard/AdrGuard.csproj src/AdrGuard/

RUN dotnet restore src/AdrGuard/AdrGuard.csproj

COPY src/AdrGuard/ src/AdrGuard/
COPY src/AdrGuard.Core/ src/AdrGuard.Core/

RUN dotnet publish src/AdrGuard/AdrGuard.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --no-self-contained \
    /p:UseAppHost=true

# .NET Runtime 10.0.14, Alpine 3.24.
FROM mcr.microsoft.com/dotnet/runtime:10.0.14-alpine3.24@sha256:8ef0660a5ae5877915bc868a27aabf334c8b14c60aadbdd0fa058d81b87c52b6 AS final
WORKDIR /workspace

# Impact analysis invokes the bounded local Git CLI. The runtime remains
# non-root and the Action mounts consumer repositories read-only.
# Update the inherited zlib package to address CVE-2026-4738 (fixed in 1.4-r0).
RUN apk add --no-cache git=2.54.0-r0 zlib=1.4-r0

LABEL org.opencontainers.image.title="ADR Guard" \
      org.opencontainers.image.description="A .NET CLI for validating, maintaining, reviewing, analyzing impact, and verifying ADR-backed architectural drift." \
      org.opencontainers.image.url="https://github.com/rodri-oliveira-dev/adr-guard" \
      org.opencontainers.image.source="https://github.com/rodri-oliveira-dev/adr-guard" \
      org.opencontainers.image.documentation="https://github.com/rodri-oliveira-dev/adr-guard#readme" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.authors="Rodrigo de Oliveira"

COPY --from=build /app/publish /app
COPY packaging/container.gitconfig /app/gitconfig

ENV GIT_CONFIG_SYSTEM=/app/gitconfig

USER $APP_UID

ENTRYPOINT ["/app/adr-guard"]
