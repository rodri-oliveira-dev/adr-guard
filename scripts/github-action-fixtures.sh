#!/usr/bin/env bash
set -euo pipefail

root="${1:?Fixture root is required}"

rm -rf -- "${root}"
mkdir -p \
  "${root}/valid ADRs" \
  "${root}/invalid ADRs" \
  "${root}/index ADRs" \
  "${root}/custom path/adr records" \
  "${root}/matrix/ADR001" \
  "${root}/matrix/ADR002" \
  "${root}/matrix/ADR003" \
  "${root}/matrix/ADR004" \
  "${root}/matrix/ADR005" \
  "${root}/matrix/ADR006/first" \
  "${root}/matrix/ADR006/second" \
  "${root}/matrix/ADR007" \
  "${root}/matrix/ADR008" \
  "${root}/matrix/ADR009"

write_valid() {
  local path="$1"
  local title="${2:-Use PostgreSQL}"
  local status="${3:-Accepted}"
  cat >"${path}" <<EOF
# ${title}

## Status
${status}

## Context
Context.

## Decision
Decision.

## Consequences
Consequences.
EOF
}

write_valid "${root}/valid ADRs/0001-use-postgresql.md"
write_valid "${root}/index ADRs/0001-use-postgresql.md"
write_valid "${root}/custom path/adr records/0001-use-postgresql.md"

cat >"${root}/invalid ADRs/0001-missing-decision.md" <<'EOF'
# Missing Decision

## Status
Accepted

## Context
Context.

## Consequences
Consequences.
EOF

# ADR001 - invalid filename.
write_valid "${root}/matrix/ADR001/1-invalid-name.md"

# ADR002 - missing level-one title.
cat >"${root}/matrix/ADR002/0001-missing-title.md" <<'EOF'
## Status
Accepted

## Context
Context.

## Decision
Decision.

## Consequences
Consequences.
EOF

# ADR003 - missing Status.
cat >"${root}/matrix/ADR003/0001-missing-status.md" <<'EOF'
# Missing Status

## Context
Context.

## Decision
Decision.

## Consequences
Consequences.
EOF

# ADR004 - invalid Status value.
write_valid "${root}/matrix/ADR004/0001-invalid-status.md" "Invalid Status" "Approved"

# ADR005 - missing required section.
cp "${root}/invalid ADRs/0001-missing-decision.md" \
  "${root}/matrix/ADR005/0001-missing-decision.md"

# ADR006 - duplicate numeric ID.
write_valid "${root}/matrix/ADR006/first/0001-first.md" "First"
write_valid "${root}/matrix/ADR006/second/0001-second.md" "Second"

# ADR007 - broken local Markdown reference.
cat >"${root}/matrix/ADR007/0001-broken-reference.md" <<'EOF'
# Broken Reference

## Status
Accepted

## Context
See [missing ADR](0009-missing.md).

## Decision
Decision.

## Consequences
Consequences.
EOF

# ADR008 - Superseded without a valid "Superseded by" reference.
write_valid "${root}/matrix/ADR008/0001-superseded.md" "Superseded ADR" "Superseded"

# ADR009 - duplicate canonical level-two section.
cat >"${root}/matrix/ADR009/0001-duplicate-status.md" <<'EOF'
# Duplicate Status

## Status
Accepted

## Status
Proposed

## Context
Context.

## Decision
Decision.

## Consequences
Consequences.
EOF

echo "ADR Guard Action fixtures prepared at ${root}."
