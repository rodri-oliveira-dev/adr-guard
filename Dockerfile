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

# .NET Runtime 10.0.15, Alpine 3.24.
FROM mcr.microsoft.com/dotnet/runtime:10.0.15-alpine3.24@sha256:7518aaad9e4328001518006a7e8b3fcf6ce8f8816355574f7264c1fa0a0ce188 AS final
WORKDIR /workspace

# Impact analysis invokes the bounded local Git CLI. The runtime remains
# non-root and the Action mounts consumer repositories read-only.
# Always upgrade inherited zlib for CVE-2026-4738 (fixed in 1.4-r0).
# Alpine security updates replace exact -rN package revisions. Upgrade at build
# time to avoid stale constraints while retaining explicit Alpine base versions.
# hadolint ignore=DL3018
RUN apk add --no-cache --upgrade git zlib

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
