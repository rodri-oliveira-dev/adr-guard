#!/usr/bin/env bash
set -euo pipefail

reservation_tag="${1:?Reservation tag is required (release-reservation/vMAJOR.MINOR.PATCH).}"
release_tag="${2:?Exact release tag is required (vMAJOR.MINOR.PATCH).}"
validated_sha="${3:?Validated commit SHA is required.}"
remote="${4:-origin}"

if [[ ! "${release_tag}" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Release tag must use vMAJOR.MINOR.PATCH; got '${release_tag}'." >&2
  exit 2
fi

if [[ "${reservation_tag}" != "release-reservation/${release_tag}" ]]; then
  echo "Reservation tag must be 'release-reservation/${release_tag}'; got '${reservation_tag}'." >&2
  exit 2
fi

git cat-file -e "${validated_sha}^{commit}" 2>/dev/null || {
  echo "Validated SHA '${validated_sha}' is not a commit in the checkout." >&2
  exit 3
}

git fetch "${remote}" --tags --force >/dev/null

remote_tag_sha() {
  git ls-remote --tags "${remote}" "refs/tags/$1" | awk 'NR == 1 { print $1 }'
}

exact_remote_sha="$(remote_tag_sha "${release_tag}")"
if [[ -n "${exact_remote_sha}" ]]; then
  exact_commit="$(git rev-list -n 1 "${release_tag}")"
  if [[ "${exact_commit}" != "${validated_sha}" ]]; then
    echo "Immutable release tag '${release_tag}' already belongs to ${exact_commit}, not ${validated_sha}." >&2
    exit 1
  fi

  echo "Exact release tag '${release_tag}' already reserves this successful release."
  exit 0
fi

reservation_remote_sha="$(remote_tag_sha "${reservation_tag}")"
if [[ -n "${reservation_remote_sha}" ]]; then
  reservation_commit="$(git rev-list -n 1 "${reservation_tag}")"
  if [[ "${reservation_commit}" != "${validated_sha}" ]]; then
    echo "Release version ${release_tag} is reserved by ${reservation_commit}, not ${validated_sha}." >&2
    exit 1
  fi

  echo "Release version ${release_tag} is already reserved for the validated commit."
  exit 0
fi

git tag "${reservation_tag}" "${validated_sha}"
git push "${remote}" "refs/tags/${reservation_tag}"

echo "Reserved release version ${release_tag} for ${validated_sha} without publishing an Action-compatible tag."
