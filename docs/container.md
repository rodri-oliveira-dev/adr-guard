# ADR Guard container image

ADR Guard is published as a multi-platform OCI container image in two registries:

- GitHub Container Registry (GHCR): `ghcr.io/rodri-oliveira-dev/adr-guard`
- Docker Hub: `docker.io/rodrigodotnet/adr-guard`

Both registries receive the same release build for `linux/amd64` and `linux/arm64`.

## Tags

Stable releases publish four tag levels:

```text
1.2.3
1.2
1
latest
```

For reproducible automation, prefer the exact SemVer tag or, when strict immutability is required, pin the image by digest.

## Pull the image

GHCR:

```bash
docker pull ghcr.io/rodri-oliveira-dev/adr-guard:latest
```

Docker Hub:

```bash
docker pull rodrigodotnet/adr-guard:latest
```

## Validate ADRs

Mount the repository at `/workspace`, which is the image working directory:

```bash
docker run --rm \
  -v "$PWD:/workspace:ro" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  check docs/adr
```

Validation can use a read-only mount because `check` does not write repository files.

## Generate the ADR index

`index` writes to the mounted repository. On Linux and macOS, using the host UID/GID avoids ownership mismatches on generated files:

```bash
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -v "$PWD:/workspace" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  index docs/adr
```

The image itself defaults to a non-root user. The explicit `--user` option above is only for matching ownership on writable host mounts where required.

## AI-assisted drafting

Provider credentials must be supplied at runtime through environment variables. They are never baked into the image.

Example with OpenAI:

```bash
docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e OPENAI_API_KEY \
  -v "$PWD:/workspace" \
  ghcr.io/rodri-oliveira-dev/adr-guard:latest \
  draft docs/adr \
  --title "Adopt a message broker" \
  --context "We need asynchronous integration." \
  --provider openai \
  --model <openai-model>
```

The same environment-variable rules documented for the .NET Tool apply to Anthropic, Gemini, and OpenAI-compatible providers.

## Supply-chain controls

The container path is protected by multiple independent controls:

- Hadolint is a required CI gate for the `Dockerfile`;
- Dependabot monitors the .NET base images referenced by the `Dockerfile`;
- CI builds and smoke-tests the image before a release can run;
- Trivy scans the built CI image for fixable `HIGH` and `CRITICAL` OS/library vulnerabilities and fails the workflow when any are found;
- the runtime image is based on the .NET chiseled image and runs as a non-root user;
- release images carry OCI source, documentation, author, license, version, and revision metadata;
- BuildKit publishes an SBOM attestation and `mode=max` provenance attestation with each multi-platform release;
- the release workflow verifies that both `linux/amd64` and `linux/arm64` manifests and attestation manifests are present in GHCR and Docker Hub.

The SBOM and provenance are OCI attestations associated with the published image index rather than files embedded inside the runtime filesystem.

## Inspect a release

Inspect the multi-platform manifest and associated attestations:

```bash
docker buildx imagetools inspect \
  ghcr.io/rodri-oliveira-dev/adr-guard:1.2.3
```

To use an immutable image reference, resolve the digest and pin it:

```text
ghcr.io/rodri-oliveira-dev/adr-guard@sha256:<digest>
```

## Release flow

Container images are published only after the CI workflow for the validated `main` commit succeeds. The release workflow then resolves the same SemVer version used by the NuGet package, creates/verifies the Git tag, publishes the multi-platform image to GHCR and Docker Hub, verifies the published manifests and attestations, and only then completes the GitHub Release.
