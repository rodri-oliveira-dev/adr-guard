#!/usr/bin/env bash
set -uo pipefail

adr_directory="${1:-}"
action_ref="${ADR_GUARD_ACTION_REF_OVERRIDE:-${2:-}}"

if [[ "${RUNNER_OS:-Linux}" != "Linux" ]]; then
  echo "::error::ADR Guard GitHub Action supports Linux runners with Docker only." >&2
  exit 3
fi

if [[ -z "${adr_directory}" ]]; then
  echo "::error::The 'adr-directory' input must not be empty." >&2
  exit 2
fi

if [[ "${adr_directory}" = /* ]]; then
  echo "::error::The 'adr-directory' input must be relative to GITHUB_WORKSPACE." >&2
  exit 2
fi

if [[ -z "${GITHUB_WORKSPACE:-}" || ! -d "${GITHUB_WORKSPACE}" ]]; then
  echo "::error::GITHUB_WORKSPACE must point to the checked-out repository. Run actions/checkout before ADR Guard." >&2
  exit 3
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "::error::Docker is required. Use a Linux runner with Docker available, such as ubuntu-latest." >&2
  exit 3
fi

if ! docker info >/dev/null 2>&1; then
  echo "::error::Docker is installed but the daemon is not available." >&2
  exit 3
fi

if [[ ! "${action_ref}" =~ ^v([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  echo "::error::ADR Guard must be referenced by an exact release tag such as @v1.2.3." >&2
  exit 2
fi

image="ghcr.io/rodri-oliveira-dev/adr-guard:${BASH_REMATCH[1]}"

echo "Running ADR Guard ${BASH_REMATCH[1]} against '${adr_directory}' using ${image}."

docker run --rm --read-only \
  --mount "type=bind,src=${GITHUB_WORKSPACE},dst=/workspace,readonly" \
  --workdir /workspace \
  "${image}" \
  check "${adr_directory}"
status=$?

# Docker reserves 125-127 for engine/invocation failures. Normalize those to
# ADR Guard's operational-error contract while preserving CLI exit codes 0-3.
if (( status >= 125 )); then
  echo "::error::ADR Guard container could not be executed (Docker exit code ${status})." >&2
  exit 3
fi

exit "${status}"
