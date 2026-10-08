#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORKFLOW="${ROOT_DIR}/.github/workflows/release.yml"
PROJECT="${ROOT_DIR}/src/AdrGuard/AdrGuard.csproj"

grep -Fq '  workflow_dispatch:' "${WORKFLOW}" || {
  echo "Release workflow must be started explicitly with workflow_dispatch." >&2
  exit 1
}

grep -Fq '      version:' "${WORKFLOW}" && grep -Fq '        required: true' "${WORKFLOW}" || {
  echo "Manual release must require an explicit version input." >&2
  exit 1
}
grep -Fq 'RELEASE_VERSION: ${{ inputs.version }}' "${WORKFLOW}" || {
  echo "Release resolver must read the explicit dispatch version." >&2
  exit 1
}

if grep -Fq 'workflow_run:' "${WORKFLOW}" || grep -Fq 'github.event.workflow_run' "${WORKFLOW}"; then
  echo "Release workflow must not publish automatically after CI." >&2
  exit 1
fi

grep -Fq "if: github.event_name == 'workflow_dispatch' && github.ref == 'refs/heads/main'" "${WORKFLOW}" || {
  echo "Manual release must be restricted to an explicit dispatch of the main branch." >&2
  exit 1
}

grep -Fq 'ref: ${{ github.sha }}' "${WORKFLOW}" || {
  echo "Manual release must checkout the commit selected by workflow_dispatch." >&2
  exit 1
}

grep -Fq 'VALIDATED_SHA: ${{ github.sha }}' "${WORKFLOW}" || {
  echo "Manual release must bind versioning and artifacts to the dispatched commit." >&2
  exit 1
}

grep -Fq 'actions: read' "${WORKFLOW}" || {
  echo "Manual release must be able to verify CI status for the dispatched commit." >&2
  exit 1
}

grep -Fq 'actions/workflows/ci.yml/runs?head_sha=${VALIDATED_SHA}&event=push&status=completed' "${WORKFLOW}" || {
  echo "Manual release must query completed CI push runs for the exact dispatched commit." >&2
  exit 1
}

grep -Fq 'select(.conclusion == "success")' "${WORKFLOW}" || {
  echo "Manual release must require a successful CI run before publication." >&2
  exit 1
}

# The release baseline is owned by the project, never pinned in CI policy.
project_baseline="$(sed -n 's:.*<VersionPrefix>\([^<]*\)</VersionPrefix>.*:\1:p' "${PROJECT}" | head -n 1)"
if [[ ! "${project_baseline}" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Project VersionPrefix must be stable MAJOR.MINOR.PATCH." >&2
  exit 1
fi
grep -Fq 'below VersionPrefix ${base_version}' "${WORKFLOW}" || {
  echo "Release resolver must reject versions below the project's VersionPrefix." >&2
  exit 1
}

grep -Fq 'AI-assisted drafting, and evidence-oriented advisory technical review' "${PROJECT}" || {
  echo "NuGet metadata must describe the published ADR review capability." >&2
  exit 1
}

grep -Fq 'org.opencontainers.image.description=A lightweight .NET CLI for validating, creating, indexing, drafting, and reviewing Architecture Decision Records (ADRs).' "${WORKFLOW}" || {
  echo "OCI metadata must describe the published ADR review capability." >&2
  exit 1
}

job_block() {
  local job="$1"
  awk -v header="  ${job}:" '
    $0 == header { capture=1 }
    capture && $0 ~ /^  [A-Za-z0-9_-]+:$/ && $0 != header { exit }
    capture { print }
  ' "${WORKFLOW}"
}

if grep -Fq '  ensure-release-tag:' "${WORKFLOW}"; then
  echo "Release workflow must not publish the immutable Action tag before runtime artifacts." >&2
  exit 1
fi

grep -Fq "git tag --list 'release-reservation/v*'" "${WORKFLOW}" || {
  echo "Release version resolution must account for unfinished version reservations." >&2
  exit 1
}

grep -Fq 'resume_existing=true' "${WORKFLOW}" || {
  echo "Release resolution must support safe same-commit retries." >&2
  exit 1
}

grep -Fq 'reservation-tag: ${{ steps.version.outputs.reservation_tag }}' "${WORKFLOW}" || {
  echo "Resolved release reservation must be exposed to downstream jobs." >&2
  exit 1
}

reservation_block="$(job_block reserve-release-version)"
nuget_block="$(job_block publish-nuget)"
packages_block="$(job_block publish-github-packages)"
container_block="$(job_block publish-container)"
action_block="$(job_block publish-action-tags)"
release_block="$(job_block github-release)"

grep -Fq 'image-digest: ${{ steps.build-container.outputs.digest }}' <<<"${container_block}"

grep -Fq 'scripts/reserve-release-version.sh' <<<"${reservation_block}"

for publication_block in "${nuget_block}" "${packages_block}" "${container_block}"; do
  grep -Fq -- '- reserve-release-version' <<<"${publication_block}" || {
    echo "Artifact publication must wait for the release version reservation." >&2
    exit 1
  }
done

for dependency in build-and-pack publish-nuget publish-github-packages publish-container; do
  grep -Fq -- "- ${dependency}" <<<"${action_block}" || {
    echo "publish-action-tags must depend on ${dependency}." >&2
    exit 1
  }
done

grep -Fq 'EXPECTED_IMAGE_DIGEST: ${{ needs.publish-container.outputs.image-digest }}' <<<"${action_block}"
grep -Fq 'scripts/publish-action-tags.sh' <<<"${action_block}"
grep -Fq 'RESERVATION_TAG: ${{ needs.build-and-pack.outputs.reservation-tag }}' <<<"${action_block}"
grep -Fq -- '- publish-action-tags' <<<"${release_block}"

# Exact and major image refs must be verified before Git tags become visible.
verify_line="$(grep -n 'Verify exact and major runtime images' "${WORKFLOW}" | cut -d: -f1)"
publish_line="$(grep -n 'Publish immutable and compatibility Action tags' "${WORKFLOW}" | cut -d: -f1)"
if [[ -z "${verify_line}" || -z "${publish_line}" || "${verify_line}" -ge "${publish_line}" ]]; then
  echo "Runtime verification must happen before Action tags are published." >&2
  exit 1
fi

grep -Fq 'ADR_GUARD_BIN="${PWD}/.release-tools/adr-guard" bash scripts/template-regression-test.sh' "${WORKFLOW}" || {
  echo "The release must smoke-test the installed versioned .NET Tool and its template modes." >&2
  exit 1
}

# Feature releases 1.1.0 and 1.3.0 use curated notes; other releases
# retain generated notes, and all tags remain immutable.
grep -Fq 'ref: ${{ needs.build-and-pack.outputs.validated-sha }}' <<<"${release_block}"
grep -Fq 'release_notes=(--generate-notes)' <<<"${release_block}"
grep -Fq 'if [[ "${RELEASE_TAG}" == v1.1.0 ]]; then' <<<"${release_block}"
grep -Fq 'release_notes=(--notes-file docs/releases/v1.1.0.md)' <<<"${release_block}"
grep -Fq 'elif [[ "${RELEASE_TAG}" == v1.3.0 ]]; then' <<<"${release_block}"
grep -Fq 'release_notes=(--notes-file docs/releases/v1.3.0.md)' <<<"${release_block}"
test -s "${ROOT_DIR}/docs/releases/v1.1.0.md"
test -s "${ROOT_DIR}/docs/releases/v1.1.0.pt-BR.md"
test -s "${ROOT_DIR}/docs/releases/v1.3.0.md"
test -s "${ROOT_DIR}/docs/releases/v1.3.0.pt-BR.md"

echo "Release workflow Action publication policy checks passed."
