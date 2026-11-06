# syntax=docker/dockerfile:1

# .NET SDK 10.0.403, Alpine 3.24.
FROM mcr.microsoft.com/dotnet/sdk@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
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
FROM mcr.microsoft.com/dotnet/runtime@sha256:54d48f739ebffdef82388056d420af37ed39c722b8d5ee5dedca6878537c2737 AS final
WORKDIR /workspace

# Impact analysis invokes the bounded local Git CLI. The runtime remains
# non-root and the Action mounts consumer repositories read-only.
RUN apk add --no-cache git=2.54.0-r0

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
