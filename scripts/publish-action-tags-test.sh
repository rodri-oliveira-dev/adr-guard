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
sha_101="$(git -C "${WORK}" rev-parse HEAD)"
git -C "${WORK}" push -u origin main >/dev/null

(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/reserve-release-version.sh" \
    release-reservation/v1.0.1 v1.0.1 "${sha_101}"
)

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/release-reservation/v1.0.1)" = "${sha_101}"

(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" \
    v1.0.1 "${sha_101}" origin release-reservation/v1.0.1
)

if git --git-dir="${REMOTE}" rev-parse --verify refs/tags/release-reservation/v1.0.1 >/dev/null 2>&1; then
  echo "Completed release reservation v1.0.1 was not removed." >&2
  exit 1
fi

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.0.1)" = "${sha_101}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_101}"

# Idempotent rerun must not change either tag.
(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.0.1 "${sha_101}"
)

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.0.1)" = "${sha_101}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_101}"

printf 'second\n' >>"${WORK}/file.txt"
git -C "${WORK}" add file.txt
git -C "${WORK}" commit -m "second" >/dev/null
sha_110="$(git -C "${WORK}" rev-parse HEAD)"
git -C "${WORK}" push origin main >/dev/null

(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/reserve-release-version.sh" \
    release-reservation/v1.1.0 v1.1.0 "${sha_110}"
)

(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" \
    v1.1.0 "${sha_110}" origin release-reservation/v1.1.0
)

if git --git-dir="${REMOTE}" rev-parse --verify refs/tags/release-reservation/v1.1.0 >/dev/null 2>&1; then
  echo "Completed release reservation v1.1.0 was not removed." >&2
  exit 1
fi

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.0.1)" = "${sha_101}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.1.0)" = "${sha_110}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_110}"

# Re-running an older release must never roll the moving major tag backwards.
(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.0.1 "${sha_101}"
)

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_110}"

# An immutable tag conflict must fail instead of moving v1.1.0.
set +e
(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/publish-action-tags.sh" v1.1.0 "${sha_101}"
)
conflict_status=$?
set -e

if [[ "${conflict_status}" -eq 0 ]]; then
  echo "Immutable Action tag conflict unexpectedly succeeded." >&2
  exit 1
fi

test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1.1.0)" = "${sha_110}"
test "$(git --git-dir="${REMOTE}" rev-parse refs/tags/v1)" = "${sha_110}"

# A reservation owned by another commit must fail before artifacts can be reused.
git -C "${WORK}" tag release-reservation/v1.1.1 "${sha_101}"
git -C "${WORK}" push origin refs/tags/release-reservation/v1.1.1 >/dev/null
set +e
(
  cd "${WORK}"
  bash "${ROOT_DIR}/scripts/reserve-release-version.sh" \
    release-reservation/v1.1.1 v1.1.1 "${sha_110}"
)
reservation_conflict_status=$?
set -e

if [[ "${reservation_conflict_status}" -eq 0 ]]; then
  echo "Conflicting release reservation unexpectedly succeeded." >&2
  exit 1
fi

echo "Action v1.0.1 -> v1.1.0 exact-tag and moving @v1 policy tests passed."
