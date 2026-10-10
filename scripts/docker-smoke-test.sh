#!/usr/bin/env bash
set -euo pipefail

IMAGE="${1:-adr-guard:ci}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TEMP_DIR}"' EXIT

assert_exit_code() {
  local expected="$1"
  shift

  set +e
  "$@"
  local actual=$?
  set -e

  if [[ "${actual}" -ne "${expected}" ]]; then
    echo "Expected exit code ${expected}, got ${actual}: $*" >&2
    exit 1
  fi
}

configured_user="$(docker image inspect --format '{{.Config.User}}' "${IMAGE}")"
if [[ -z "${configured_user}" || "${configured_user}" == "0" || "${configured_user}" == "root" ]]; then
  echo "Container image must configure a non-root default user." >&2
  exit 1
fi

echo "Container default user: ${configured_user}"

docker run --rm --read-only "${IMAGE}" --help >"${TEMP_DIR}/help.txt"
grep -q "ADR Guard" "${TEMP_DIR}/help.txt"
grep -q "Exit codes:" "${TEMP_DIR}/help.txt"

docker run --rm --entrypoint /usr/bin/git "${IMAGE}" --version >"${TEMP_DIR}/git-version.txt"
grep -q '^git version ' "${TEMP_DIR}/git-version.txt"

assert_exit_code 0 \
  docker run --rm --read-only \
    --mount "type=bind,src=${ROOT_DIR}/docs/adr,dst=/workspace/docs/adr,readonly" \
    "${IMAGE}" check docs/adr

mkdir -p "${TEMP_DIR}/invalid"
cat >"${TEMP_DIR}/invalid/0001-invalid.md" <<'EOF'
# Invalid ADR

## Status

Accepted

## Context

This fixture intentionally omits a required section.

## Consequences

Validation must fail.
EOF

assert_exit_code 1 \
  docker run --rm --read-only \
    --mount "type=bind,src=${TEMP_DIR}/invalid,dst=/workspace/adrs,readonly" \
    "${IMAGE}" check adrs

assert_exit_code 2 \
  docker run --rm --read-only \
    "${IMAGE}" unsupported-command

assert_exit_code 3 \
  docker run --rm --read-only \
    "${IMAGE}" check /workspace/does-not-exist

mkdir -p "${TEMP_DIR}/writable"
cp -R "${ROOT_DIR}/docs/adr/." "${TEMP_DIR}/writable/"
rm -f "${TEMP_DIR}/writable/README.md"

assert_exit_code 0 \
  docker run --rm \
    --user "$(id -u):$(id -g)" \
    --mount "type=bind,src=${TEMP_DIR}/writable,dst=/workspace/adrs" \
    "${IMAGE}" index adrs

test -s "${TEMP_DIR}/writable/README.md"
grep -q "# Architecture Decision Records" "${TEMP_DIR}/writable/README.md"

# The release image contains the local Git executable required by read-only
# architecture impact analysis.
IMPACT_REPOSITORY="${TEMP_DIR}/impact-repository"
mkdir -p "${IMPACT_REPOSITORY}/docs/adr" "${IMPACT_REPOSITORY}/src"
git -C "${IMPACT_REPOSITORY}" init -b main >/dev/null
git -C "${IMPACT_REPOSITORY}" config user.email tests@example.com
git -C "${IMPACT_REPOSITORY}" config user.name "ADR Guard Tests"
cat >"${IMPACT_REPOSITORY}/docs/adr/0001-service.md" <<'EOF'
# Service boundary

## Status
Accepted

## Context
Context.

## Decision
Decision.

## Consequences
Consequences.
EOF
cat >"${IMPACT_REPOSITORY}/.adrguard-impact.json" <<'EOF'
{"schemaVersion":"1.0","mappings":[{"decision":{"stableId":"ADR-1","path":"docs/adr/0001-service.md"},"patterns":["src/**"],"relationship":"governs","reason":"Explicit mapping."}]}
EOF
printf 'initial\n' >"${IMPACT_REPOSITORY}/src/service.cs"
git -C "${IMPACT_REPOSITORY}" add .
git -C "${IMPACT_REPOSITORY}" commit -m initial >/dev/null
printf 'changed\n' >>"${IMPACT_REPOSITORY}/src/service.cs"
assert_exit_code 0 \
  docker run --rm --read-only --network=none \
    --env GIT_CONFIG_COUNT=1 \
    --env GIT_CONFIG_KEY_0=safe.directory \
    --env GIT_CONFIG_VALUE_0=/workspace \
    --mount "type=bind,src=${IMPACT_REPOSITORY},dst=/workspace,readonly" \
    "${IMAGE}" impact /workspace --base-ref HEAD \
    --map .adrguard-impact.json --format json >"${TEMP_DIR}/impact.json"
grep -Fq '"schemaVersion": "1.0"' "${TEMP_DIR}/impact.json"
grep -Fq '"status": "affected"' "${TEMP_DIR}/impact.json"

# Release images also expose offline new (not an Action input). Exercise both
# built-in and custom modes against writable bind mounts without AI secrets.
mkdir -p "${TEMP_DIR}/generated"
assert_exit_code 0 \
  docker run --rm --read-only \
    --tmpfs /tmp:rw,nosuid,nodev,mode=1777,size=16m \
    --user "$(id -u):$(id -g)" \
    --mount "type=bind,src=${TEMP_DIR}/generated,dst=/workspace/adrs" \
    "${IMAGE}" new adrs --title "Adopt Redis" --template minimal
assert_exit_code 0 \
  docker run --rm --read-only \
    --tmpfs /tmp:rw,nosuid,nodev,mode=1777,size=16m \
    --user "$(id -u):$(id -g)" \
    --mount "type=bind,src=${TEMP_DIR}/generated,dst=/workspace/adrs" \
    --mount "type=bind,src=${ROOT_DIR}/docs/examples/templates/team.en-US.md,dst=/workspace/team.md,readonly" \
    "${IMAGE}" new adrs --title "Adopt Cache" --template-file team.md
test -s "${TEMP_DIR}/generated/0001-adopt-redis.md"
grep -Fq 'ADR 0002' "${TEMP_DIR}/generated/0002-adopt-cache.md"
assert_exit_code 0 \
  docker run --rm --read-only \
    --mount "type=bind,src=${TEMP_DIR}/generated,dst=/workspace/adrs,readonly" \
    "${IMAGE}" check adrs

echo "Docker smoke tests passed."
