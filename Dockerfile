# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/AdrGuard/AdrGuard.csproj src/AdrGuard/

RUN dotnet restore src/AdrGuard/AdrGuard.csproj

COPY src/AdrGuard/ src/AdrGuard/

RUN dotnet publish src/AdrGuard/AdrGuard.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --no-self-contained \
    /p:UseAppHost=true

FROM mcr.microsoft.com/dotnet/runtime:10.0.13-azurelinux3.0@sha256:26d37c58334e91321cfc6b69f287b72f644ae9105d23238fde65c5a59733783e AS final
WORKDIR /workspace

# Impact analysis invokes the bounded local Git CLI. The runtime remains
# non-root and the Action mounts consumer repositories read-only.
# hadolint ignore=DL3041
RUN tdnf install -y git \
    && tdnf clean all \
    && rm -rf /var/cache/tdnf

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
