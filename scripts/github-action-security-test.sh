#!/usr/bin/env bash
set -euo pipefail

IMAGE_VERSION="${1:-0.1.12}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE="ghcr.io/rodri-oliveira-dev/adr-guard:${IMAGE_VERSION}"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "${TEMP_DIR}"' EXIT

# Prove the published GHCR image can be consumed without Docker credentials or
# provider/GitHub tokens. DOCKER_CONFIG points at a fresh directory so runner
# credentials cannot accidentally satisfy the pull.
mkdir -p "${TEMP_DIR}/docker-config"
env \
  -u GITHUB_TOKEN \
  -u GH_TOKEN \
  -u OPENAI_API_KEY \
  -u ANTHROPIC_API_KEY \
  -u GEMINI_API_KEY \
  -u ADR_GUARD_OPENAI_COMPATIBLE_API_KEY \
  DOCKER_CONFIG="${TEMP_DIR}/docker-config" \
  docker pull "${IMAGE}" >/dev/null

if [[ -e "${TEMP_DIR}/docker-config/config.json" ]] &&
   grep -Eq '"auths"[[:space:]]*:[[:space:]]*\{[^}]*ghcr\.io' "${TEMP_DIR}/docker-config/config.json"; then
  echo "Anonymous pull test unexpectedly created GHCR credentials." >&2
  exit 1
fi

configured_user="$(docker image inspect --format '{{.Config.User}}' "${IMAGE}")"
if [[ -z "${configured_user}" || "${configured_user}" == "0" || "${configured_user}" == "root" || "${configured_user}" == "0:0" ]]; then
  echo "Published image must declare a non-root runtime user; got '${configured_user:-<empty>}'." >&2
  exit 1
fi

repo_digest="$(docker image inspect --format '{{index .RepoDigests 0}}' "${IMAGE}")"
if [[ ! "${repo_digest}" =~ ^ghcr\.io/rodri-oliveira-dev/adr-guard@sha256:[0-9a-f]{64}$ ]]; then
  echo "Published image did not resolve to an immutable GHCR digest: ${repo_digest}" >&2
  exit 1
fi

image_env="$(docker image inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "${IMAGE}")"
for secret_name in OPENAI_API_KEY ANTHROPIC_API_KEY GEMINI_API_KEY ADR_GUARD_OPENAI_COMPATIBLE_API_KEY GITHUB_TOKEN GH_TOKEN; do
  if grep -Eq "^(${secret_name})=" <<<"${image_env}"; then
    echo "Published image must not bake ${secret_name} into its environment." >&2
    exit 1
  fi
done

# The reusable Action exposes provider/model selection only for explicit review.
# Credentials and GitHub tokens must never become Action inputs, and draft stays unavailable.
if grep -Eq 'GITHUB_TOKEN|secrets\.|OPENAI_API_KEY|ANTHROPIC_API_KEY|GEMINI_API_KEY|ADR_GUARD_OPENAI_COMPATIBLE_API_KEY' "${ROOT_DIR}/action.yml"; then
  echo "action.yml must not request or forward repository/provider credentials." >&2
  exit 1
fi

if grep -Eq '^[[:space:]]+(token|api-key|credential|openai-api-key|anthropic-api-key|gemini-api-key):' "${ROOT_DIR}/action.yml"; then
  echo "Provider credentials must not be exposed as Action inputs." >&2
  exit 1
fi

grep -Eq '^  review-target:' "${ROOT_DIR}/action.yml"
grep -Eq '^  provider:' "${ROOT_DIR}/action.yml"
grep -Eq '^  model:' "${ROOT_DIR}/action.yml"
grep -Eq '^  base-ref:' "${ROOT_DIR}/action.yml"
grep -Eq '^  impact-map:' "${ROOT_DIR}/action.yml"
grep -Eq '^  drift-rules:' "${ROOT_DIR}/action.yml"
grep -Eq '^  drift-policy:' "${ROOT_DIR}/action.yml"
grep -Eq '^  drift-baseline:' "${ROOT_DIR}/action.yml"
grep -Eq '^  drift-evidence:' "${ROOT_DIR}/action.yml"

if grep -Eq '^[[:space:]]+(drift-write-baseline|drift-update-baseline|script|command-line):' "${ROOT_DIR}/action.yml"; then
  echo "The Action must not expose drift writes or arbitrary command execution." >&2
  exit 1
fi

# Both stages must stay on explicit Alpine variants; a generic .NET digest can
# silently select a non-Alpine distribution where apk is unavailable.
grep -Eq '^FROM mcr\.microsoft\.com/dotnet/sdk:[0-9.]+-alpine[0-9.]+(@sha256:[a-f0-9]{64})? AS build
grep -Fq 'USER $APP_UID' "${ROOT_DIR}/Dockerfile"

if grep -Eq '(^|[[:space:]])draft([[:space:]]|$)' "${ROOT_DIR}/action.yml"; then
  echo "The default GitHub Action must not expose the AI draft command." >&2
  exit 1
fi

echo "GitHub Action security checks passed for ${IMAGE} (${repo_digest})."
 "${ROOT_DIR}/Dockerfile"
grep -Eq '^FROM mcr\.microsoft\.com/dotnet/runtime:[0-9.]+-alpine[0-9.]+(@sha256:[a-f0-9]{64})? AS final
grep -Fq 'USER $APP_UID' "${ROOT_DIR}/Dockerfile"

if grep -Eq '(^|[[:space:]])draft([[:space:]]|$)' "${ROOT_DIR}/action.yml"; then
  echo "The default GitHub Action must not expose the AI draft command." >&2
  exit 1
fi

echo "GitHub Action security checks passed for ${IMAGE} (${repo_digest})."
 "${ROOT_DIR}/Dockerfile"
# Install the latest security-fixed Git and zlib Alpine package revisions.
grep -Fxq 'RUN apk add --no-cache --upgrade git zlib' "${ROOT_DIR}/Dockerfile"
grep -Fq 'USER $APP_UID' "${ROOT_DIR}/Dockerfile"

if grep -Eq '(^|[[:space:]])draft([[:space:]]|$)' "${ROOT_DIR}/action.yml"; then
  echo "The default GitHub Action must not expose the AI draft command." >&2
  exit 1
fi

echo "GitHub Action security checks passed for ${IMAGE} (${repo_digest})."
