#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: $0 <source> <output>" >&2
  exit 2
fi

source_file="$1"
output_file="$2"
start_marker='<!-- release-candidate-only:start -->'
end_marker='<!-- release-candidate-only:end -->'

[[ -s "$source_file" ]] || {
  echo "Release notes source is missing or empty: $source_file" >&2
  exit 1
}

[[ "$source_file" != "$output_file" ]] || {
  echo "Release notes output must differ from the source." >&2
  exit 1
}

output_directory="$(dirname "$output_file")"
[[ -d "$output_directory" ]] || {
  echo "Release notes output directory does not exist: $output_directory" >&2
  exit 1
}

start_count="$(grep -Fc "$start_marker" "$source_file" || true)"
end_count="$(grep -Fc "$end_marker" "$source_file" || true)"
if [[ "$start_count" -eq 0 || "$start_count" -ne "$end_count" ]]; then
  echo "Release notes must contain balanced candidate-only markers." >&2
  exit 1
fi

temporary_file="$(mktemp "${output_file}.tmp.XXXXXX")"
trap 'rm -f "$temporary_file"' EXIT

awk -v start_marker="$start_marker" -v end_marker="$end_marker" '
  {
    line = $0
    sub(/\r$/, "", line)
  }
  line == start_marker {
    if (candidate_only) invalid = 1
    candidate_only = 1
    next
  }
  line == end_marker {
    if (!candidate_only) invalid = 1
    candidate_only = 0
    next
  }
  !candidate_only { print }
  END {
    if (candidate_only || invalid) exit 1
  }
' "$source_file" > "$temporary_file" || {
  echo "Release notes contain nested or unbalanced candidate-only markers." >&2
  exit 1
}

[[ -s "$temporary_file" ]] || {
  echo "Rendered release notes are empty." >&2
  exit 1
}

if grep -Fq '<!-- release-candidate-only:' "$temporary_file"; then
  echo "Rendered release notes still contain candidate-only markers." >&2
  exit 1
fi

mv "$temporary_file" "$output_file"
trap - EXIT
