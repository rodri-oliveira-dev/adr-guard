# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
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

FROM mcr.microsoft.com/dotnet/runtime:10.0.13-azurelinux3.0@sha256:0f82ccc5e4affe9f538d8fc5e14cc246e11459a72ebc5cef1ea2287625f6f2a3 AS final
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
