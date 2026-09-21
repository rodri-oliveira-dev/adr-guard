#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORKFLOW="${ROOT_DIR}/.github/workflows/release.yml"

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

echo "Release workflow Action publication policy checks passed."
