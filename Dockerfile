# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
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

FROM mcr.microsoft.com/dotnet/runtime:10.0.11-azurelinux3.0-distroless-extra AS final
WORKDIR /workspace

LABEL org.opencontainers.image.title="ADR Guard" \
      org.opencontainers.image.description="A lightweight .NET CLI for validating, maintaining, indexing, and drafting Architecture Decision Records (ADRs)." \
      org.opencontainers.image.url="https://github.com/rodri-oliveira-dev/adr-guard" \
      org.opencontainers.image.source="https://github.com/rodri-oliveira-dev/adr-guard" \
      org.opencontainers.image.documentation="https://github.com/rodri-oliveira-dev/adr-guard#readme" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.authors="Rodrigo de Oliveira"

COPY --from=build /app/publish /app

USER $APP_UID

ENTRYPOINT ["/app/adr-guard"]
