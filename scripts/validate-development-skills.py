#!/usr/bin/env python3
"""Offline validation for repository-local development skills under .agents."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SKILLS_ROOT = ROOT / ".agents" / "skills"
SOURCES_FILE = SKILLS_ROOT / "sources.json"
PROVENANCE_FILE = SKILLS_ROOT / "PROVENANCE.md"
OVERLAY_FILE = ROOT / ".agents" / "skill-overlays" / "dotnet-test" / "run-tests.md"
NAME_RE = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*\Z")
TOP_LEVEL_RE = re.compile(r"([A-Za-z][A-Za-z0-9_-]*):(?:\s*(.*))?\Z")
LINK_RE = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
REVISION_RE = re.compile(r"[0-9a-f]{40}\Z")


def parse_frontmatter(path: Path) -> tuple[dict[str, str], list[str], list[str]]:
    errors: list[str] = []
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeError) as exc:
        return {}, [], [f"{path}: unreadable UTF-8: {exc}"]

    if not lines or lines[0] != "---":
        return {}, lines, [f"{path}: missing opening YAML frontmatter delimiter"]
    try:
        end = lines.index("---", 1)
    except ValueError:
        return {}, lines, [f"{path}: missing closing YAML frontmatter delimiter"]

    fields: dict[str, str] = {}
    index = 1
    while index < end:
        line = lines[index]
        if not line or line[0].isspace():
            index += 1
            continue
        match = TOP_LEVEL_RE.fullmatch(line)
        if not match:
            errors.append(f"{path}: invalid top-level frontmatter line: {line!r}")
            index += 1
            continue
        key, raw_value = match.groups()
        if key in fields:
            errors.append(f"{path}: repeated frontmatter key {key}")
        value = (raw_value or "").strip()
        if value in {">", ">-", "|", "|-"}:
            chunks: list[str] = []
            index += 1
            while index < end and (not lines[index] or lines[index][0].isspace()):
                if lines[index].strip():
                    chunks.append(lines[index].strip())
                index += 1
            fields[key] = " ".join(chunks)
            continue
        if len(value) >= 2 and value[0] == value[-1] and value[0] in {'"', "'"}:
            value = value[1:-1]
        fields[key] = value
        index += 1
    return fields, lines[end + 1 :], errors


def iter_prose_links(lines: list[str]):
    fence_char: str | None = None
    fence_size = 0
    for line in lines:
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


def validate_skill(skill_file: Path) -> tuple[str | None, list[str]]:
    fields, body, errors = parse_frontmatter(skill_file)
    name = fields.get("name", "")
    description = fields.get("description", "")
    if not NAME_RE.fullmatch(name) or len(name) > 64 or name != skill_file.parent.name:
        errors.append(f"{skill_file}: invalid name / mismatched directory: {name!r}")
    if not 1 <= len(description) <= 1024:
        errors.append(f"{skill_file}: description must contain 1..1024 characters")
    if not "\n".join(body).strip():
        errors.append(f"{skill_file}: missing Markdown instruction body")

    skill_root = skill_file.parent.resolve()
    for target in iter_prose_links(body):
        if target.startswith(("https://", "http://", "mailto:", "#")):
            continue
        local = target.split("#", 1)[0].strip("<>")
        if not local:
            continue
        destination = (skill_root / local).resolve()
        if not destination.is_relative_to(skill_root):
            errors.append(f"{skill_file}: relative link escapes skill root: {target}")
        elif not destination.is_file():
            errors.append(f"{skill_file}: missing local reference: {target}")
    return name or None, errors


def validate_sources(discovered: set[str]) -> list[str]:
    errors: list[str] = []
    try:
        manifest = json.loads(SOURCES_FILE.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        return [f"{SOURCES_FILE}: invalid source manifest: {exc}"]

    entries = manifest.get("skills")
    if manifest.get("schemaVersion") != 1 or not isinstance(entries, list):
        return [f"{SOURCES_FILE}: expected schemaVersion 1 and a skills array"]

    recorded: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            errors.append(f"{SOURCES_FILE}: each skill source must be an object")
            continue
        name = entry.get("name")
        if not isinstance(name, str) or not NAME_RE.fullmatch(name):
            errors.append(f"{SOURCES_FILE}: invalid source skill name: {name!r}")
            continue
        if name in recorded:
            errors.append(f"{SOURCES_FILE}: duplicate source entry: {name}")
        recorded.add(name)
        for field in ("origin", "repository", "path", "license", "licenseFile"):
            if not isinstance(entry.get(field), str) or not entry[field].strip():
                errors.append(f"{SOURCES_FILE}: {name} is missing {field}")
        revision = entry.get("revision")
        if entry.get("origin") != "repository-local" and (
            not isinstance(revision, str) or not REVISION_RE.fullmatch(revision)
        ):
            errors.append(f"{SOURCES_FILE}: {name} must pin a 40-character revision")
        license_file = entry.get("licenseFile")
        if isinstance(license_file, str):
            resolved = (SOURCES_FILE.parent / license_file).resolve()
            if not resolved.is_relative_to((ROOT / ".agents").resolve()) or not resolved.is_file():
                errors.append(f"{SOURCES_FILE}: {name} has a missing or escaping licenseFile")

    if recorded != discovered:
        errors.append(
            f"{SOURCES_FILE}: source entries differ from skill directories "
            f"(missing={sorted(discovered - recorded)}, extra={sorted(recorded - discovered)})"
        )
    return errors


def validate_overlay() -> list[str]:
    fields, body, errors = parse_frontmatter(OVERLAY_FILE)
    expected = {
        "core": "dotnet-test/run-tests",
        "binding-revision": "1",
        "mode": "extend",
    }
    for key, value in expected.items():
        if fields.get(key) != value:
            errors.append(f"{OVERLAY_FILE}: expected {key}: {value!r}")
    if not "\n".join(body).strip():
        errors.append(f"{OVERLAY_FILE}: missing overlay instructions")
    return errors


def validate_development_skills(root: Path = ROOT) -> list[str]:
    del root  # The repository root is intentionally fixed by this script's location.
    errors: list[str] = []
    if not SKILLS_ROOT.is_dir():
        return [f"{SKILLS_ROOT}: missing development skills directory"]

    skill_files = sorted(SKILLS_ROOT.glob("*/SKILL.md"))
    directories = {path.name for path in SKILLS_ROOT.iterdir() if path.is_dir()}
    discovered: set[str] = set()
    for skill_file in skill_files:
        name, skill_errors = validate_skill(skill_file)
        errors.extend(skill_errors)
        if name:
            if name in discovered:
                errors.append(f"{skill_file}: duplicate skill name: {name}")
            discovered.add(name)
    missing_entrypoints = directories - {path.parent.name for path in skill_files}
    for directory in sorted(missing_entrypoints):
        errors.append(f"{SKILLS_ROOT / directory}: missing SKILL.md")

    public_names = {path.parent.name for path in (ROOT / "skills").glob("*/SKILL.md")}
    for duplicate in sorted(discovered & public_names):
        errors.append(f"duplicate skill name across contributor and public skills: {duplicate}")

    if not PROVENANCE_FILE.is_file():
        errors.append(f"{PROVENANCE_FILE}: missing provenance documentation")
    errors.extend(validate_sources(discovered))
    errors.extend(validate_overlay())
    return errors


def main() -> int:
    errors = validate_development_skills()
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    count = len(list(SKILLS_ROOT.glob("*/SKILL.md")))
    print(f"PASS: all {count} development skills, sources, licenses, references, and overlays are valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
