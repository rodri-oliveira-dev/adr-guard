#!/usr/bin/env python3
"""Dependency-free checks for ADR Guard's portable Agent Skills (P0 + P1).

This deliberately validates the small YAML scalar subset authored by this repository,
not every possible YAML document. Agent Skills' full spec is at agentskills.io.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

EXPECTED_P0 = frozenset(
    {
        "adr-guard-init",
        "adr-guard-create",
        "adr-guard-validate",
        "adr-guard-technical-review",
        "adr-guard-lifecycle",
        "adr-guard-ci-setup",
    }
)
EXPECTED_P1 = frozenset(
    {
        "adr-guard-when-to-record",
        "adr-guard-tradeoff-analysis",
        "adr-guard-supersede",
        "adr-guard-audit",
        "adr-guard-team-adoption",
    }
)
EXPECTED_SKILLS = EXPECTED_P0 | EXPECTED_P1
NAME_RE = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*\Z")
KEY_RE = re.compile(r"([a-z][a-z-]*):\s+(.+)\Z")
LINK_RE = re.compile(r"\[[^\]]+\]\(([^)]+)\)")


def iter_prose_links(markdown_lines: list[str]):
    """Find actionable Markdown links, ignoring fenced code examples."""
    fence_char: str | None = None
    fence_size = 0
    for line in markdown_lines:
        marker = re.match(r"^ {0,3}(`{3,}|~{3,})(.*)$", line)
        if marker:
            run = marker.group(1)
            if fence_char is None:
                fence_char = run[0]
                fence_size = len(run)
            elif run[0] == fence_char and len(run) >= fence_size and not marker.group(2).strip():
                fence_char = None
                fence_size = 0
            continue
        if fence_char is None:
            yield from LINK_RE.findall(line)


def validate_skill(skill_file: Path) -> list[str]:
    errors: list[str] = []
    try:
        text = skill_file.read_text(encoding="utf-8")
    except (OSError, UnicodeError) as exc:
        return [f"{skill_file}: unreadable UTF-8: {exc}"]

    lines = text.splitlines()
    if not lines or lines[0] != "---":
        return [f"{skill_file}: missing opening YAML frontmatter delimiter"]
    try:
        end = lines.index("---", 1)
    except ValueError:
        return [f"{skill_file}: missing closing YAML frontmatter delimiter"]

    fields: dict[str, str] = {}
    for line in lines[1:end]:
        match = KEY_RE.fullmatch(line)
        if not match:
            errors.append(f"{skill_file}: unsupported frontmatter scalar: {line!r}")
            continue
        key, value = match.groups()
        if key in fields:
            errors.append(f"{skill_file}: repeated frontmatter key {key}")
        fields[key] = value
    name = fields.get("name", "")
    description = fields.get("description", "")
    if not NAME_RE.fullmatch(name) or len(name) > 64 or name != skill_file.parent.name:
        errors.append(f"{skill_file}: invalid name / mismatched directory: {name!r}")
    if not 1 <= len(description) <= 1024 or description.startswith(('"', "'")):
        errors.append(f"{skill_file}: description must be an unquoted scalar (1..1024 chars)")
    if not description.endswith(".") or "Use when" not in description:
        errors.append(f"{skill_file}: description should state trigger with 'Use when'")
    for key in fields:
        if key not in {"name", "description", "license", "compatibility"}:
            errors.append(f"{skill_file}: unsupported frontmatter key: {key}")
    if len(fields.get("compatibility", "")) > 500:
        errors.append(f"{skill_file}: compatibility exceeds 500 characters")
    body = "\n".join(lines[end + 1 :])
    if not body.strip() or not body.lstrip().startswith("# "):
        errors.append(f"{skill_file}: missing Markdown H1 instruction body")
    if len(lines) > 500:
        errors.append(f"{skill_file}: SKILL.md exceeds 500 lines")
    for target in iter_prose_links(lines[end + 1 :]):
        # Only verify local links in prose. Example code fences aren't skill dependencies.
        if target.startswith(("https://", "http://", "mailto:", "#")):
            continue
        local = target.split("#", 1)[0]
        if not local:
            continue
        skill_root = skill_file.parent.resolve()
        destination = (skill_root / local).resolve()
        if not destination.is_relative_to(skill_root):
            errors.append(f"{skill_file}: non-portable link escaping skill root: {target}")
        elif not destination.is_file():
            errors.append(f"{skill_file}: missing local reference: {target}")
    return errors


def validate_catalog(root: Path) -> list[str]:
    skills_root = root / "skills"
    discovered = {p.name for p in skills_root.iterdir() if p.is_dir()} if skills_root.is_dir() else set()
    errors = [f"missing required Agent Skill: {name}" for name in sorted(EXPECTED_SKILLS - discovered)]
    for skill in sorted(discovered):
        skill_file = skills_root / skill / "SKILL.md"
        if not skill_file.is_file():
            errors.append(f"{skill_file}: missing SKILL.md")
            continue
        errors.extend(validate_skill(skill_file))
    if not (root / "docs/skills/README.md").is_file() or not (root / "docs/skills/README.pt-BR.md").is_file():
        errors.append("missing bilingual Agent Skills catalog documentation")
    return errors


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    errors = validate_catalog(root)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print(f"PASS: all {len(EXPECTED_SKILLS)} P0/P1 Agent Skills and portable references are valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
