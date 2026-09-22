#!/usr/bin/env bash
set -euo pipefail

# Execute the actual version-resolution step extracted from release.yml against
# isolated Git history. This guards the release baseline without publishing tags
# or making network requests to GitHub.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORKFLOW="${ROOT}/.github/workflows/release.yml"
PROJECT="${ROOT}/src/AdrGuard/AdrGuard.csproj"
TMP="$(mktemp -d)"
trap 'rm -rf -- "${TMP}"' EXIT

grep -Fq '<VersionPrefix>1.1.0</VersionPrefix>' "${PROJECT}" || {
  echo 'The v1.1.0 feature release requires VersionPrefix 1.1.0.' >&2
  exit 1
}

# Extract just the shell body of the deployed Resolve release version workflow
# step, with the ten YAML-indentation spaces removed.
sed -n '/^      - name: Resolve release version$/,/^      - name: Restore$/p' "${WORKFLOW}" \
  | sed '1,/^        run: |$/d; /^      - name: Restore$/,$d; s/^          //' \
  >"${TMP}/resolve-version.sh"

grep -Fq 'base_version=' "${TMP}/resolve-version.sh"
grep -Fq 'existing_reservation=' "${TMP}/resolve-version.sh"
grep -Fq 'release_tag=' "${TMP}/resolve-version.sh"

git init --bare "${TMP}/remote.git" >/dev/null
git init -b main "${TMP}/work" >/dev/null
git -C "${TMP}/work" config user.name 'ADR Guard CI'
git -C "${TMP}/work" config user.email 'adr-guard@example.invalid'
git -C "${TMP}/work" remote add origin "${TMP}/remote.git"
mkdir -p "${TMP}/work/src/AdrGuard"
cp "${PROJECT}" "${TMP}/work/src/AdrGuard/AdrGuard.csproj"

printf 'previous release\n' >"${TMP}/work/release-state.txt"
git -C "${TMP}/work" add .
git -C "${TMP}/work" commit -m 'published 1.0.1 baseline' >/dev/null
prior_sha="$(git -C "${TMP}/work" rev-parse HEAD)"
git -C "${TMP}/work" tag v1.0.0 "${prior_sha}"
git -C "${TMP}/work" tag v1.0.1 "${prior_sha}"
git -C "${TMP}/work" tag v1 "${prior_sha}"
git -C "${TMP}/work" push -u origin main --tags >/dev/null

printf 'feature release\n' >>"${TMP}/work/release-state.txt"
git -C "${TMP}/work" add release-state.txt
git -C "${TMP}/work" commit -m 'feature branch merged' >/dev/null
feature_sha="$(git -C "${TMP}/work" rev-parse HEAD)"

assert_resolution() {
  local sha="$1"
  local expected="$2"
  : >"${TMP}/output"
  (
    cd "${TMP}/work"
    VALIDATED_SHA="${sha}" GITHUB_OUTPUT="${TMP}/output" \
      bash "${TMP}/resolve-version.sh" >"${TMP}/resolver.log"
  )
  for expected_line in \
    "package_version=${expected}" \
    "release_tag=v${expected}" \
    "reservation_tag=release-reservation/v${expected}" \
    "validated_sha=${sha}"; do
    grep -Fxq "${expected_line}" "${TMP}/output" || {
      echo "Unexpected version resolution; expected ${expected_line}" >&2
      cat "${TMP}/output" "${TMP}/resolver.log" >&2
      exit 1
    }
  done
}

# A latest published 1.0.1 must not automatically select 1.0.2.
assert_resolution "${feature_sha}" 1.1.0

# Same validated commit must reuse its immutable tag/reservation on rerun.
git -C "${TMP}/work" tag release-reservation/v1.1.0 "${feature_sha}"
assert_resolution "${feature_sha}" 1.1.0
git -C "${TMP}/work" tag -d release-reservation/v1.1.0 >/dev/null
git -C "${TMP}/work" tag v1.1.0 "${feature_sha}"
assert_resolution "${feature_sha}" 1.1.0

# Later commits on the 1.1 line may resume patch semantics, without
# duplicating the immutable v1.1.0 tag.
printf 'future patch\n' >>"${TMP}/work/release-state.txt"
git -C "${TMP}/work" add release-state.txt
git -C "${TMP}/work" commit -m 'future patch' >/dev/null
future_sha="$(git -C "${TMP}/work" rev-parse HEAD)"
assert_resolution "${future_sha}" 1.1.1

echo 'Release version resolver tests passed: 1.0.1 -> 1.1.0, reservation/exact reruns and next patch.'
