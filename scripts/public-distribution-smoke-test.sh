#!/usr/bin/env bash
set -euo pipefail

REPOSITORY="rodri-oliveira-dev/adr-guard"
PACKAGE_ID="RodriOliveira.AdrGuard"
NUGET_SOURCE="https://api.nuget.org/v3/index.json"
GHCR_IMAGE="ghcr.io/rodri-oliveira-dev/adr-guard"
DOCKERHUB_IMAGE="docker.io/rodrigodotnet/adr-guard"

for tool in curl jq dotnet docker sha256sum; do
  command -v "${tool}" >/dev/null 2>&1 || {
    echo "Required public-distribution audit tool is unavailable: ${tool}" >&2
    exit 1
  }
done

if ! docker info >/dev/null 2>&1; then
  echo "Docker daemon is required for public container smoke verification." >&2
  exit 1
fi

release_json="$(curl --fail --silent --show-error --location   -H 'Accept: application/vnd.github+json'   "https://api.github.com/repos/${REPOSITORY}/releases/latest")"

release_tag="$(jq -r '.tag_name // empty' <<<"${release_json}")"
release_url="$(jq -r '.html_url // empty' <<<"${release_json}")"
release_commitish="$(jq -r '.target_commitish // empty' <<<"${release_json}")"

if [[ ! "${release_tag}" =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  echo "Latest GitHub Release tag is not a stable SemVer tag: '${release_tag:-<empty>}'." >&2
  exit 1
fi

version="${release_tag#v}"
major="${BASH_REMATCH[1]}"

ref_json="$(curl --fail --silent --show-error --location   -H 'Accept: application/vnd.github+json'   "https://api.github.com/repos/${REPOSITORY}/git/ref/tags/v${major}")"
exact_ref_json="$(curl --fail --silent --show-error --location   -H 'Accept: application/vnd.github+json'   "https://api.github.com/repos/${REPOSITORY}/git/ref/tags/${release_tag}")"

major_sha="$(jq -r '.object.sha // empty' <<<"${ref_json}")"
exact_sha="$(jq -r '.object.sha // empty' <<<"${exact_ref_json}")"

if [[ -z "${major_sha}" || -z "${exact_sha}" || "${major_sha}" != "${exact_sha}" ]]; then
  echo "Moving Action tag v${major} does not resolve to the latest exact release ${release_tag}." >&2
  echo "v${major}: ${major_sha:-<missing>}; ${release_tag}: ${exact_sha:-<missing>}" >&2
  exit 1
fi

TEMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "${TEMP_DIR}"' EXIT

TOOLS_DIR="${TEMP_DIR}/tools"
ADR_DIR="${TEMP_DIR}/adrs"
mkdir -p "${TOOLS_DIR}" "${ADR_DIR}"

echo "Installing public NuGet package ${PACKAGE_ID} ${version}."
dotnet tool install   --tool-path "${TOOLS_DIR}"   "${PACKAGE_ID}"   --version "${version}"   --source "${NUGET_SOURCE}"   --no-cache

ADR_GUARD="${TOOLS_DIR}/adr-guard"
"${ADR_GUARD}" --help >"${TEMP_DIR}/help.txt"
grep -Fq "ADR Guard" "${TEMP_DIR}/help.txt"

"${ADR_GUARD}" new "${ADR_DIR}"   --title "Verify Public Release"   --template minimal

test -s "${ADR_DIR}/0001-verify-public-release.md"
"${ADR_GUARD}" check "${ADR_DIR}"
"${ADR_GUARD}" index "${ADR_DIR}"
test -s "${ADR_DIR}/README.md"

smoke_image() {
  local image="$1"
  echo "Pulling public image ${image}."
  docker pull "${image}" >/dev/null
  docker run --rm --read-only "${image}" --help >"${TEMP_DIR}/container-help.txt"
  grep -Fq "ADR Guard" "${TEMP_DIR}/container-help.txt"
  docker run --rm --read-only     --mount "type=bind,src=${ADR_DIR},dst=/workspace/adrs,readonly"     "${image}" check adrs
}

smoke_image "${GHCR_IMAGE}:${version}"
exact_ghcr_id="$(docker image inspect --format '{{.Id}}' "${GHCR_IMAGE}:${version}")"
smoke_image "${GHCR_IMAGE}:${major}"
major_ghcr_id="$(docker image inspect --format '{{.Id}}' "${GHCR_IMAGE}:${major}")"

if [[ "${exact_ghcr_id}" != "${major_ghcr_id}" ]]; then
  echo "GHCR moving major image :${major} does not match exact :${version} for this runner platform." >&2
  exit 1
fi

smoke_image "${DOCKERHUB_IMAGE}:${version}"
exact_dockerhub_id="$(docker image inspect --format '{{.Id}}' "${DOCKERHUB_IMAGE}:${version}")"
smoke_image "${DOCKERHUB_IMAGE}:${major}"
major_dockerhub_id="$(docker image inspect --format '{{.Id}}' "${DOCKERHUB_IMAGE}:${major}")"

if [[ "${exact_dockerhub_id}" != "${major_dockerhub_id}" ]]; then
  echo "Docker Hub moving major image :${major} does not match exact :${version} for this runner platform." >&2
  exit 1
fi

echo "Public distribution audit passed."
echo "GitHub Release: ${release_tag} (${release_url})"
echo "Release target: ${release_commitish}"
echo "Action refs: v${major} and ${release_tag} -> ${exact_sha}"
echo "NuGet.org: ${PACKAGE_ID} ${version}"
echo "GHCR: ${GHCR_IMAGE}:${version} and :${major}"
echo "Docker Hub: ${DOCKERHUB_IMAGE}:${version} and :${major}"
