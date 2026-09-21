#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "${TEMP_DIR}"' EXIT

REMOTE="${TEMP_DIR}/remote.git"
WORK="${TEMP_DIR}/work"

git init --bare "${REMOTE}" >/dev/null
git init -b main "${WORK}" >/dev/null
git -C "${WORK}" config user.name "ADR Guard CI"
git -C "${WORK}" config user.email "adr-guard@example.invalid"
git -C "${WORK}" remote add origin "${REMOTE}"

printf 'first\n' >"${WORK}/file.txt"
git -C "${WORK}" add file.txt
git -C "${WORK}" commit -m "first" >/dev/null
sha_123="$(git -C "${WORK}" rev-parse HEAD)"
git -C "${WORK}" push -u origin main >/dev/null

(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.2.3 "${sha_123}"
)

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.2.3)" = "${sha_123}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_123}"

# Idempotent rerun must not change either tag.
(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.2.3 "${sha_123}"
)

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.2.3)" = "${sha_123}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_123}"

printf 'second\n' >>"${WORK}/file.txt"
git -C "${WORK}" add file.txt
git -C "${WORK}" commit -m "second" >/dev/null
sha_124="$(git -C "${WORK}" rev-parse HEAD)"
git -C "${WORK}" push origin main >/dev/null

(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.2.4 "${sha_124}"
)

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.2.3)" = "${sha_123}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.2.4)" = "${sha_124}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_124}"

# Re-running an older release must never roll the moving major tag backwards.
(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.2.3 "${sha_123}"
)

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_124}"

# An immutable tag conflict must fail instead of moving v1.2.4.
set +e
(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.2.4 "${sha_123}"
)
conflict_status=$?
set -e

if [[ "${conflict_status}" -eq 0 ]]; then
  echo "Immutable Action tag conflict unexpectedly succeeded." >&2
  exit 1
fi

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.2.4)" = "${sha_124}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_124}"

echo "Action release tag policy tests passed."
