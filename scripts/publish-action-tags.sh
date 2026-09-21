#!/usr/bin/env bash
set -euo pipefail

release_tag="${1:?Exact release tag is required (vMAJOR.MINOR.PATCH).}"
validated_sha="${2:?Validated commit SHA is required.}"
remote="${3:-origin}"

if [[ ! "${release_tag}" =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  echo "Release tag must use vMAJOR.MINOR.PATCH; got '${release_tag}'." >&2
  exit 2
fi

major="${BASH_REMATCH[1]}"
major_tag="v${major}"
release_version="${release_tag#v}"

git cat-file -e "${validated_sha}^{commit}" 2>/dev/null || {
  echo "Validated SHA '${validated_sha}' is not a commit in the checkout." >&2
  exit 3
}

git fetch "${remote}" --tags --force >/dev/null

remote_tag_sha() {
  local tag="$1"
  git ls-remote --tags "${remote}" "refs/tags/${tag}" \
    | awk 'NR == 1 { print $1 }'
}

tag_commit() {
  local tag="$1"
  git rev-list -n 1 "${tag}"
}

exact_remote_sha="$(remote_tag_sha "${release_tag}")"

if [[ -n "${exact_remote_sha}" ]]; then
  exact_commit="$(tag_commit "${release_tag}")"

  if [[ "${exact_commit}" != "${validated_sha}" ]]; then
    echo "Immutable Action tag '${release_tag}' already points to ${exact_commit}, not ${validated_sha}." >&2
    exit 1
  fi

  echo "Immutable Action tag '${release_tag}' already points to the validated commit."
else
  if git rev-parse -q --verify "refs/tags/${release_tag}" >/dev/null; then
    local_exact_commit="$(tag_commit "${release_tag}")"
    if [[ "${local_exact_commit}" != "${validated_sha}" ]]; then
      echo "Local immutable tag '${release_tag}' conflicts with validated commit ${validated_sha}." >&2
      exit 1
    fi
  else
    git tag "${release_tag}" "${validated_sha}"
  fi

  git push "${remote}" "refs/tags/${release_tag}"
  echo "Published immutable Action tag '${release_tag}' at ${validated_sha}."
fi

major_remote_sha="$(remote_tag_sha "${major_tag}")"

if [[ -z "${major_remote_sha}" ]]; then
  git tag -f "${major_tag}" "${validated_sha}" >/dev/null
  git push "${remote}" "refs/tags/${major_tag}"
  echo "Published compatibility Action tag '${major_tag}' at ${release_tag}."
  exit 0
fi

major_commit="$(tag_commit "${major_tag}")"

if [[ "${major_commit}" == "${validated_sha}" ]]; then
  echo "Compatibility Action tag '${major_tag}' already points to ${release_tag}."
  exit 0
fi

major_release_tag="$(
  git tag --points-at "${major_commit}" \
    | grep -E "^v${major}\.[0-9]+\.[0-9]+$" \
    | sort -V \
    | tail -n 1 \
    || true
)"

if [[ -z "${major_release_tag}" ]]; then
  echo "Compatibility tag '${major_tag}' points to ${major_commit}, which has no immutable v${major}.MINOR.PATCH tag; refusing to overwrite it." >&2
  exit 1
fi

major_version="${major_release_tag#v}"
newest="$(printf '%s\n%s\n' "${major_version}" "${release_version}" | sort -V | tail -n 1)"

if [[ "${newest}" != "${release_version}" ]]; then
  echo "Compatibility tag '${major_tag}' already points to newer ${major_release_tag}; leaving it unchanged."
  exit 0
fi

git tag -f "${major_tag}" "${validated_sha}" >/dev/null
git push \
  --force-with-lease="refs/tags/${major_tag}:${major_remote_sha}" \
  "${remote}" \
  "refs/tags/${major_tag}"

echo "Moved compatibility Action tag '${major_tag}' from ${major_release_tag} to ${release_tag}."
