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

grep -Fq '<VersionPrefix>1.2.0</VersionPrefix>' "${PROJECT}" || {
  echo 'The v1.2.0 feature release requires VersionPrefix 1.2.0.' >&2
  exit 1
}

# Extract just the shell body of the deployed Resolve release version workflow
# step, with the ten YAML-indentation spaces removed.
sed -n '/^      - name: Resolve release version$/,/^      - name: Restore$/p' "${WORKFLOW}" \
  | sed '1,/^        run: |$/d; /^      - name: Restore$/,$d; s/^          //' \
  >"${TMP}/resolve-version.sh"

grep -Fq 'requested_version=' "${TMP}/resolve-version.sh"
grep -Fq 'existing_reservation=' "${TMP}/resolve-version.sh"
grep -Fq 'resume_existing=' "${TMP}/resolve-version.sh"
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
git -C "${TMP}/work" tag v1.1.7 "${prior_sha}"
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
    VALIDATED_SHA="${sha}" RELEASE_VERSION="${expected}" GITHUB_OUTPUT="${TMP}/output" \
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

# A new feature release is explicitly selected, not guessed from v1.1.7.
assert_resolution "${feature_sha}" 1.2.0

assert_rejected() {
  local sha="$1"
  local version="$2"
  local reason="$3"
  if (
    cd "${TMP}/work"
    VALIDATED_SHA="${sha}" RELEASE_VERSION="${version}" GITHUB_OUTPUT="${TMP}/output" \
      bash "${TMP}/resolve-version.sh" >"${TMP}/reject.log" 2>&1
  ); then
    echo "Expected release version ${version} to fail (${reason})." >&2
    exit 1
  fi
  grep -Fq "${reason}" "${TMP}/reject.log" || {
    echo "Incorrect rejection for ${version}, expected ${reason}:" >&2
    cat "${TMP}/reject.log" >&2
    exit 1
  }
}

# Invalid SemVer and lower-than-baseline versions must never publish.
for malformed in '' 'v1.2.0' '1.2' '01.2.0' '1.02.0' '1.2.00' '1.2.0-rc.1' '1.2.0+build' '1.2.0;echo'; do
  assert_rejected "${feature_sha}" "${malformed}" 'Release version must use stable SemVer'
done
assert_rejected "${feature_sha}" '1.1.8' 'below VersionPrefix'

# A version reserved by a different commit cannot be claimed.
git -C "${TMP}/work" tag release-reservation/v1.2.0 "${prior_sha}"
assert_rejected "${feature_sha}" 1.2.0 'already belongs'
git -C "${TMP}/work" tag -d release-reservation/v1.2.0 >/dev/null

# A matching reservation may be resumed on its own commit.
git -C "${TMP}/work" tag release-reservation/v1.2.0 "${feature_sha}"
assert_resolution "${feature_sha}" 1.2.0
assert_rejected "${feature_sha}" 1.2.1 'already has reservation'
git -C "${TMP}/work" tag -d release-reservation/v1.2.0 >/dev/null

# A matching immutable release tag permits idempotent repair.
git -C "${TMP}/work" tag v1.2.0 "${feature_sha}"
assert_resolution "${feature_sha}" 1.2.0
assert_rejected "${feature_sha}" 1.2.1 'already published'

# Later changes need an explicitly selected newer release.
printf 'future patch\n' >>"${TMP}/work/release-state.txt"
git -C "${TMP}/work" add release-state.txt
git -C "${TMP}/work" commit -m 'future patch' >/dev/null
future_sha="$(git -C "${TMP}/work" rev-parse HEAD)"
assert_rejected "${future_sha}" 1.2.0 'already belongs'
assert_resolution "${future_sha}" 1.2.1

git -C "${TMP}/work" tag v1.2.1 "${future_sha}"
assert_rejected "${feature_sha}" 1.2.0 'older than latest'
assert_resolution "${future_sha}" 1.2.1

echo 'Release version resolver tests passed: explicit SemVer, baseline, conflicts, reservations, reruns and monotonic releases.'
