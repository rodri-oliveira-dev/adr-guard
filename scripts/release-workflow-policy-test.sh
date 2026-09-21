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

container_block="$(job_block publish-container)"
action_block="$(job_block publish-action-tags)"
release_block="$(job_block github-release)"

grep -Fq 'image-digest: ${{ steps.build-container.outputs.digest }}' <<<"${container_block}"

for dependency in build-and-pack publish-nuget publish-github-packages publish-container; do
  grep -Fq -- "- ${dependency}" <<<"${action_block}" || {
    echo "publish-action-tags must depend on ${dependency}." >&2
    exit 1
  }
done

grep -Fq 'EXPECTED_IMAGE_DIGEST: ${{ needs.publish-container.outputs.image-digest }}' <<<"${action_block}"
grep -Fq 'scripts/publish-action-tags.sh' <<<"${action_block}"
grep -Fq -- '- publish-action-tags' <<<"${release_block}"

# Exact and major image refs must be verified before Git tags become visible.
verify_line="$(grep -n 'Verify exact and major runtime images' "${WORKFLOW}" | cut -d: -f1)"
publish_line="$(grep -n 'Publish immutable and compatibility Action tags' "${WORKFLOW}" | cut -d: -f1)"
if [[ -z "${verify_line}" || -z "${publish_line}" || "${verify_line}" -ge "${publish_line}" ]]; then
  echo "Runtime verification must happen before Action tags are published." >&2
  exit 1
fi

echo "Release workflow Action publication policy checks passed."
