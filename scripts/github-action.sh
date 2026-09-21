#!/usr/bin/env bash
set -uo pipefail

adr_path="${ADR_GUARD_PATH:-}"
command="${ADR_GUARD_COMMAND:-}"
requested_version="${ADR_GUARD_VERSION:-}"
action_ref="${ADR_GUARD_ACTION_REF:-}"

usage_error() {
  echo "::error::$1" >&2
  exit 2
}

operational_error() {
  echo "::error::$1" >&2
  exit 3
}

if [[ "${RUNNER_OS:-Linux}" != "Linux" ]]; then
  operational_error "ADR Guard GitHub Action supports Linux runners with Docker only."
fi

if [[ -z "${GITHUB_WORKSPACE:-}" || ! -d "${GITHUB_WORKSPACE}" ]]; then
  operational_error "GITHUB_WORKSPACE must point to the checked-out repository. Run actions/checkout before ADR Guard."
fi

if [[ -z "${adr_path}" ]]; then
  usage_error "The 'path' input must not be empty."
fi

if [[ -z "${command}" ]]; then
  usage_error "The 'command' input must not be empty. Allowed values: check, index."
fi

case "${command}" in
  check|index)
    ;;
  *)
    usage_error "Unsupported command '${command}'. Allowed values: check, index."
    ;;
esac

if [[ "${adr_path}" = /* ]]; then
  usage_error "The 'path' input must be relative to GITHUB_WORKSPACE."
fi

IFS='/' read -r -a path_segments <<< "${adr_path}"
for segment in "${path_segments[@]}"; do
  if [[ "${segment}" == ".." ]]; then
    usage_error "The 'path' input must not contain '..' traversal segments."
  fi
done

workspace="$(realpath -e "${GITHUB_WORKSPACE}")" || operational_error "Unable to resolve GITHUB_WORKSPACE."
resolved_path="$(realpath -e "${workspace}/${adr_path}" 2>/dev/null)" ||
  usage_error "ADR directory '${adr_path}' does not exist inside GITHUB_WORKSPACE."

if [[ ! -d "${resolved_path}" ]]; then
  usage_error "ADR path '${adr_path}' must resolve to a directory."
fi

case "${resolved_path}" in
  "${workspace}"|"${workspace}/"*)
    ;;
  *)
    usage_error "ADR path '${adr_path}' resolves outside GITHUB_WORKSPACE."
    ;;
esac

if ! command -v docker >/dev/null 2>&1; then
  operational_error "Docker is required. Use a Linux runner with Docker available, such as ubuntu-latest."
fi

if ! docker info >/dev/null 2>&1; then
  operational_error "Docker is installed but the daemon is not available."
fi

version=""
if [[ -n "${requested_version}" ]]; then
  if [[ "${requested_version}" =~ ^v?([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
    version="${BASH_REMATCH[1]}"
  else
    usage_error "Invalid 'version' input '${requested_version}'. Use an exact version such as 1.2.3 or v1.2.3."
  fi
elif [[ "${action_ref}" =~ ^v([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  version="${BASH_REMATCH[1]}"
else
  usage_error "Unable to select an image version from action ref '${action_ref:-<empty>}'. Reference ADR Guard with @vX.Y.Z or set the exact 'version' input."
fi

image="ghcr.io/rodri-oliveira-dev/adr-guard:${version}"
container_path="/workspace${resolved_path#"${workspace}"}"

echo "Running ADR Guard ${version}: ${command} '${adr_path}' using ${image}."

docker_args=(
  run
  --rm
  --read-only
  --workdir /workspace
)

if [[ "${command}" == "check" ]]; then
  docker_args+=(--mount "type=bind,src=${workspace},dst=/workspace,readonly")
else
  docker_args+=(
    --user "$(id -u):$(id -g)"
    --mount "type=bind,src=${workspace},dst=/workspace"
  )
fi

docker_args+=(
  "${image}"
  "${command}"
  "${container_path}"
)

# Capture both streams before replaying them so a hostile ADR diagnostic cannot
# issue arbitrary GitHub workflow commands through the raw CLI log.
log_directory="$(mktemp -d)" || operational_error "Unable to create temporary log directory."
trap 'rm -rf -- "${log_directory}"' EXIT
stdout_log="${log_directory}/stdout"
stderr_log="${log_directory}/stderr"

docker "${docker_args[@]}" >"${stdout_log}" 2>"${stderr_log}"
status=$?

# The stop token is unpredictable and printed only outside the untrusted CLI
# output. Replay the original output for troubleshooting without interpreting
# any embedded workflow commands.
stop_token="$(od -An -N16 -tx1 /dev/urandom | tr -d '[:space:]')"
if [[ ! "${stop_token}" =~ ^[0-9a-f]{32}$ ]]; then
  operational_error "Unable to generate a safe workflow-command suspension token."
fi
printf '::stop-commands::%s\n' "${stop_token}"
cat -- "${stdout_log}" "${stderr_log}"
printf '::%s::\n' "${stop_token}"

# Docker reserves 125-127 for engine/invocation failures. Normalize those to
# ADR Guard's operational-error contract while preserving CLI exit codes 0-3.
if (( status >= 125 )); then
  echo "::error::ADR Guard image '${image}' could not be executed (Docker exit code ${status}). The requested version is not replaced with 'latest'." >&2
  status=3
fi

# Reporting is best-effort: never replace the original validator exit code.
if ! bash "$(dirname "${BASH_SOURCE[0]}")/github-action-report.sh" \
  "${status}" "${workspace}" "${resolved_path}" "${stderr_log}" "${command}"; then
  echo "::warning::ADR Guard annotation reporting failed; inspect the raw CLI log." >&2
fi

exit "${status}"
