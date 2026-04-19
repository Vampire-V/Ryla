#!/usr/bin/env python3
"""
Compile changelog fragments into Markdown release notes.

Usage:
  python3 scripts/compile-changelog.py                    # แสดง release notes จาก fragments ทั้งหมด
  python3 scripts/compile-changelog.py --version v0.2.0   # ระบุ version ที่จะแสดง
  python3 scripts/compile-changelog.py --archive v0.2.0   # compile + ย้าย fragments ไป released/
"""

from __future__ import annotations

import argparse
import os
import shutil
import sys
from datetime import date
from pathlib import Path

# ใช้ safe yaml loading — ไม่ต้อง pip install เพิ่ม
try:
    import yaml
except ImportError:
    # Fallback: parse YAML แบบง่ายถ้าไม่มี PyYAML
    yaml = None

FRAGMENTS_DIR = Path("changelog/fragments")
RELEASED_DIR = Path("changelog/released")

# จัดกลุ่มตาม type → section heading
TYPE_SECTIONS = {
    "feat": "Features",
    "fix": "Bug Fixes",
    "perf": "Performance",
    "refactor": "Refactoring",
    "security": "Security",
    "docs": "Documentation",
    "chore": "Chores",
}


def parse_yaml_simple(text: str) -> dict:
    """Parse YAML แบบง่าย — รองรับ key: value, multiline |, boolean"""
    result = {}
    current_key = None
    multiline_value = []
    in_multiline = False

    for line in text.splitlines():
        if in_multiline:
            if line and not line[0].isspace():
                result[current_key] = "\n".join(multiline_value).strip()
                in_multiline = False
            else:
                multiline_value.append(line.strip())
                continue

        if ":" in line and not line.startswith(" "):
            key, _, value = line.partition(":")
            key = key.strip()
            value = value.strip()

            if value == "|":
                current_key = key
                multiline_value = []
                in_multiline = True
            elif value.lower() in ("true", "false"):
                result[key] = value.lower() == "true"
            elif value.isdigit():
                result[key] = int(value)
            else:
                result[key] = value.strip('"').strip("'")

    if in_multiline and current_key:
        result[current_key] = "\n".join(multiline_value).strip()

    return result


def load_fragment(path: Path) -> dict:
    """Load a changelog fragment YAML file."""
    text = path.read_text(encoding="utf-8")
    if yaml:
        return yaml.safe_load(text)
    return parse_yaml_simple(text)


def compile_notes(version: str | None = None) -> str:
    """Compile all fragments into Markdown release notes."""
    if not FRAGMENTS_DIR.exists():
        return "⚠️  No changelog/fragments/ directory found."

    fragments = sorted(FRAGMENTS_DIR.glob("*.yml"))
    fragments = [f for f in fragments if f.name != ".gitkeep"]

    if not fragments:
        return "⚠️  No changelog fragments found."

    # Load all fragments
    entries = []
    for f in fragments:
        try:
            data = load_fragment(f)
            data["_file"] = f
            entries.append(data)
        except Exception as e:
            print(f"⚠️  Error parsing {f}: {e}", file=sys.stderr)

    # Group by type
    sections: dict[str, list[dict]] = {}
    for entry in entries:
        t = entry.get("type", "chore")
        sections.setdefault(t, []).append(entry)

    # Build Markdown
    version_str = version or "UNRELEASED"
    today = date.today().isoformat()
    lines = [f"## {version_str} ({today})", ""]

    # Breaking changes first
    breaking = [e for e in entries if e.get("breaking")]
    if breaking:
        lines.append("### ⚠️ Breaking Changes")
        for e in breaking:
            lines.append(f"- **{e.get('scope', '?')}:** {e.get('title', '?')} (#{e.get('pr', '?')})")
        lines.append("")

    # Regular sections
    for type_key, heading in TYPE_SECTIONS.items():
        group = sections.get(type_key, [])
        if not group:
            continue
        lines.append(f"### {heading}")
        for e in group:
            pr = e.get("pr", "?")
            scope = e.get("scope", "")
            title = e.get("title", "")
            desc = e.get("description", "").strip()

            lines.append(f"- **{scope}:** {title} (#{pr})")
            if desc:
                for desc_line in desc.splitlines():
                    lines.append(f"  {desc_line.strip()}")
        lines.append("")

    # Migrations
    migrations = [e for e in entries if e.get("migration")]
    if migrations:
        lines.append("### Migrations")
        for e in migrations:
            lines.append(f"- ⚠️ {e.get('title', '?')} (#{e.get('pr', '?')}) — requires `supabase db push`")
        lines.append("")

    return "\n".join(lines)


def archive_fragments(version: str) -> None:
    """ย้าย fragments ไป changelog/released/vX.Y.Z/"""
    dest = RELEASED_DIR / version
    dest.mkdir(parents=True, exist_ok=True)

    fragments = sorted(FRAGMENTS_DIR.glob("*.yml"))
    moved = 0
    for f in fragments:
        if f.name == ".gitkeep":
            continue
        shutil.move(str(f), str(dest / f.name))
        moved += 1

    print(f"✅ Archived {moved} fragment(s) to {dest}/", file=sys.stderr)


def main() -> None:
    parser = argparse.ArgumentParser(description="Compile changelog fragments into release notes")
    parser.add_argument("--version", "-v", help="Version string (e.g., v0.2.0)")
    parser.add_argument("--archive", "-a", metavar="VERSION",
                        help="Compile + archive fragments to changelog/released/VERSION/")
    args = parser.parse_args()

    version = args.archive or args.version
    notes = compile_notes(version)
    print(notes)

    if args.archive:
        archive_fragments(args.archive)


if __name__ == "__main__":
    main()
