#!/usr/bin/env python3
"""Fail closed when source component versions are missing from active bilingual docs.

This validator is offline. It checks documentation coverage and source manifests,
not public-registry publication status (which requires independent verification).
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
VERSION = re.compile(r"\d+\.\d+\.\d+\Z")


def read_text(root: Path, relative: str, errors: list[str]) -> str:
    try:
        return (root / relative).read_text(encoding="utf-8")
    except (OSError, UnicodeError) as exc:
        errors.append(f"{relative}: cannot read source or documentation: {exc}")
        return ""


def xml_version(root: Path, relative: str, errors: list[str]) -> str:
    content = read_text(root, relative, errors)
    matches = re.findall(r"<VersionPrefix>([^<]+)</VersionPrefix>", content)
    if len(matches) != 1 or not VERSION.fullmatch(matches[0]):
        errors.append(f"{relative}: expected one stable VersionPrefix")
        return ""
    return matches[0]


def package_version(root: Path, relative: str, errors: list[str]) -> str:
    content = read_text(root, relative, errors)
    try:
        version = json.loads(content)["version"]
    except (ValueError, KeyError, TypeError) as exc:
        errors.append(f"{relative}: missing or invalid JSON version: {exc}")
        return ""
    if not isinstance(version, str) or not VERSION.fullmatch(version):
        errors.append(f"{relative}: expected a stable SemVer version")
        return ""
    return version


def contains_version(content: str, version: str) -> bool:
    """Match a whole SemVer token, never a substring of a newer patch number."""
    return bool(version and re.search(r"(?<![\d.])" + re.escape(version) + r"(?![\d.])", content))


def check_docs(root: Path, component: str, version: str, paths: list[str], errors: list[str]) -> None:
    for relative in paths:
        content = read_text(root, relative, errors)
        if content and not contains_version(content, version):
            errors.append(f"{relative}: {component} source version {version!r} is not documented")


def check_matrix_row(root: Path, prefixes: tuple[str, str], version: str, errors: list[str]) -> None:
    for relative, prefix in zip(("docs/compatibility.md", "docs/compatibility.pt-BR.md"), prefixes):
        content = read_text(root, relative, errors)
        row = next((line for line in content.splitlines() if line.startswith(prefix)), "")
        if not row or not contains_version(row, version):
            errors.append(f"{relative}: missing {prefix!r} row for source version {version}")


def validate_version_documentation(root: Path = ROOT) -> list[str]:
    errors: list[str] = []
    cli = xml_version(root, "src/AdrGuard/AdrGuard.csproj", errors)
    mcp = xml_version(root, "src/AdrGuard.Mcp/AdrGuard.Mcp.csproj", errors)
    vscode = package_version(root, "extensions/vscode/package.json", errors)
    vscode_lock = package_version(root, "extensions/vscode/package-lock.json", errors)
    registry_file = ".mcp/server.json"
    registry = package_version(root, registry_file, errors)

    if vscode and vscode_lock and vscode != vscode_lock:
        errors.append("extensions/vscode/package-lock.json: version differs from package.json")

    manifest = read_text(root, registry_file, errors)
    if manifest:
        try:
            packages = json.loads(manifest)["packages"]
            package_versions = [p["version"] for p in packages]
            if not package_versions or any(v != registry for v in package_versions):
                errors.append(f"{registry_file}: package versions must equal manifest version")
        except (ValueError, TypeError, KeyError) as exc:
            errors.append(f"{registry_file}: malformed Registry package metadata: {exc}")

    portal = [
        "website/src/content/docs/product/index.md",
        "website/src/content/docs/pt-br/product/index.md",
    ]
    matrix = ["docs/compatibility.md", "docs/compatibility.pt-BR.md"]

    if cli:
        check_docs(root, "CLI", cli, [
            "README.md", "README.pt-BR.md", "CHANGELOG.md",
            "docs/cli-reference.md", "docs/cli-reference.pt-BR.md",
            "docs/github-action.md", "docs/github-action.pt-BR.md",
            f"docs/releases/v{cli}.md", f"docs/releases/v{cli}.pt-BR.md",
            *matrix, *portal,
        ], errors)
        check_matrix_row(root, ("| CLI / .NET Tool |", "| CLI / .NET Tool |"), cli, errors)

    if mcp:
        check_docs(root, "MCP", mcp, [
            "src/AdrGuard.Mcp/README.md", "src/AdrGuard.Mcp/CHANGELOG.md",
            "docs/mcp.md", "docs/mcp.pt-BR.md",
            "docs/mcp-configuration.md", "docs/mcp-configuration.pt-BR.md",
            "docs/mcp-release.md", "docs/mcp-release.pt-BR.md",
            f"docs/releases/mcp-v{mcp}.md",
            f"docs/releases/mcp-v{mcp}.pt-BR.md",
            *matrix, *portal,
        ], errors)
        check_matrix_row(root, ("| MCP server / .NET Tool |", "| Servidor MCP / .NET Tool |"), mcp, errors)

    if vscode:
        check_docs(root, "VS Code", vscode, [
            "extensions/vscode/README.md", "extensions/vscode/README.pt-BR.md",
            "extensions/vscode/CHANGELOG.md", "extensions/vscode/PUBLISHING.md",
            f"docs/releases/vscode-v{vscode}.md",
            f"docs/releases/vscode-v{vscode}.pt-BR.md",
            *matrix, *portal,
        ], errors)
        check_matrix_row(root, (
            "| VS Code core commands, Problems, Explorer |",
            "| Comandos básicos, Problems e Explorer no VS Code |",
        ), vscode, errors)

    # Registry may intentionally lag behind a newer MCP source release candidate.
    if registry:
        check_docs(root, "MCP Registry manifest", registry, matrix + portal, errors)

    return errors


def main() -> int:
    errors = validate_version_documentation()
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print("PASS: CLI, MCP/Registry and VS Code source versions have bilingual documentation coverage")
    print("NOTE: publication status must be verified against external registries separately")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
