#!/usr/bin/env python3
"""Fails when a source file has no summary, or has one that describes an older version of it.

Every source file under `src/` carries a sibling `<name>.md` — `PolarisButton.cs.md` next to
`PolarisButton.cs` — holding its API and behaviour in prose, so that an agent or a person can learn
what a file does without reading the file. A summary that has silently drifted from its source is
worse than no summary, because it is believed.

Each summary therefore records the SHA-256 of the source it was written against:

    <!-- source-sha256: 3f1c9a2b7d4e -->

This compares that marker with the file as it is now. A mismatch does not prove the prose is wrong —
it only proves nobody has looked since the source changed — so the message asks for a re-read, and
`--sha` prints the line to paste once the summary has been brought up to date.

Line endings are normalised before hashing, so a CRLF checkout does not report every file as stale.

Usage:
    python3 tools/check-summaries.py                 # check src/, exit 1 on a missing or stale summary
    python3 tools/check-summaries.py --list          # every source and the state of its summary
    python3 tools/check-summaries.py --pending       # machine-readable list of what needs writing
    python3 tools/check-summaries.py --sha FILE      # the marker line for FILE, after editing its summary
"""

from __future__ import annotations

import argparse
import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# The code that gets a summary. Markdown is excluded because it cannot have a summary of a summary,
# and project files because nobody needs prose about a .csproj.
SOURCE_SUFFIXES = {".cs", ".razor", ".js", ".css"}

SKIPPED_DIRECTORIES = {"bin", "obj", "node_modules", ".git", ".vs", ".nuget", "artifacts"}

# The default boundary. `src` is where the two libraries, the CLI and the template live; tests and
# samples are read as examples of use rather than as an API to consult.
DEFAULT_ROOTS = ["src"]

MARKER = re.compile(r"<!--\s*source-sha256:\s*([0-9a-fA-F]+)\s*-->")

# The name a summary claims to describe. It is written by hand, so a copy-pasted header is the one
# way a summary can end up describing a different file while its hash still matches something.
NAMES = re.compile(r"<!--\s*summary-of:\s*(\S+?)\s*-->")


def digest(path: Path) -> str:
    """The short hash of a file's content, with line endings normalised to LF."""
    data = path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    return hashlib.sha256(data).hexdigest()[:12]


def is_skipped(path: Path) -> bool:
    parts = set(path.parts)
    return bool(parts & SKIPPED_DIRECTORIES)


def sources(roots: list[Path]) -> list[Path]:
    found: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        for candidate in root.rglob("*"):
            if candidate.is_file() and candidate.suffix.lower() in SOURCE_SUFFIXES and not is_skipped(candidate):
                found.append(candidate)
    return sorted(found, key=lambda p: str(p).lower())


def summary_path(source: Path) -> Path:
    """`PolarisButton.cs` -> `PolarisButton.cs.md`, beside it."""
    return source.with_name(source.name + ".md")


def display(path: Path) -> str:
    """A path relative to the repository when it is inside it, and in full when it is not.

    The tests point this tool at a throwaway directory, and `relative_to` raises rather than printing
    an ugly path, which would turn a test failure into a crash inside the message.
    """
    resolved = path.resolve()
    return str(resolved.relative_to(ROOT)) if resolved.is_relative_to(ROOT) else str(path)


def recorded_hash(summary: Path) -> str | None:
    if not summary.exists():
        return None
    match = MARKER.search(summary.read_text(encoding="utf-8", errors="replace"))
    return match.group(1).lower() if match else None


def recorded_name(summary: Path) -> str | None:
    """The file a summary claims to describe, as written in its first line."""
    if not summary.exists():
        return None
    match = NAMES.search(summary.read_text(encoding="utf-8", errors="replace"))
    return match.group(1) if match else None


LINK = re.compile(r"\[[^\]]*\]\(([^)\s]+)")
EXTERNAL = ("http://", "https://", "mailto:")


def broken_links(summary: Path) -> list[str]:
    """Targets in a summary's markdown links that do not exist on disk.

    The "see also" links are how a reader gets from one file to the next, and a summary is written
    before its neighbours sometimes exist. A link that points at nothing is invisible until someone
    follows it, so it is checked here rather than discovered later.
    """
    if not summary.exists():
        return []

    broken: list[str] = []
    for target in LINK.findall(summary.read_text(encoding="utf-8", errors="replace")):
        if target.startswith(EXTERNAL) or target.startswith("#"):
            continue
        path = target.split("#", 1)[0]
        if path and not (summary.parent / path).exists():
            broken.append(target)
    return broken


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="*", help="files or directories to check (default: src/)")
    parser.add_argument("--list", action="store_true", help="show every source and whether its summary is current")
    parser.add_argument("--pending", action="store_true", help="print 'hash<TAB>source' for each summary to write")
    parser.add_argument("--sha", metavar="FILE", help="print the source-sha256 marker line for FILE")
    args = parser.parse_args()

    if args.sha:
        target = (ROOT / args.sha) if not Path(args.sha).is_absolute() else Path(args.sha)
        if not target.exists():
            print(f"no such file: {args.sha}", file=sys.stderr)
            return 2
        print(f"<!-- source-sha256: {digest(target)} -->")
        return 0

    roots = [Path(p) if Path(p).is_absolute() else ROOT / p for p in (args.paths or DEFAULT_ROOTS)]
    files = []
    for root in roots:
        files.extend([root] if root.is_file() else sources([root]))

    missing: list[Path] = []
    stale: list[Path] = []
    unmarked: list[Path] = []
    mislabelled: list[Path] = []

    for source in files:
        summary = summary_path(source)
        if not summary.exists():
            missing.append(source)
            continue
        recorded = recorded_hash(summary)
        if recorded is None:
            unmarked.append(source)
        elif recorded != digest(source):
            stale.append(source)
        if recorded_name(summary) not in (None, source.name):
            mislabelled.append(source)

    if args.pending:
        for source in files:
            summary = summary_path(source)
            if not summary.exists() or recorded_hash(summary) != digest(source):
                print(f"{digest(source)}\t{display(source)}")
        return 0

    if args.list:
        for source in files:
            summary = summary_path(source)
            if not summary.exists():
                state = "MISSING"
            elif recorded_hash(summary) is None:
                state = "UNMARKED"
            elif recorded_hash(summary) != digest(source):
                state = "STALE"
            else:
                state = "ok"
            print(f"{state:8}  {display(source)}")
        return 0

    links = [(summary_path(source), target) for source in files for target in broken_links(summary_path(source))]

    if not (missing or stale or unmarked or mislabelled or links):
        print(f"all {len(files)} source summaries are present and current")
        return 0

    for label, group, detail in (
        ("no summary", missing, "write {summary} from {source}"),
        ("summary has no source-sha256 marker", unmarked, "add the marker line to {summary}"),
        ("summary is older than its source", stale, "re-read {source} and update {summary}"),
    ):
        if not group:
            continue
        print(f"\n{label} ({len(group)}):", file=sys.stderr)
        for source in group:
            print(f"  - {detail.format(source=display(source), summary=display(summary_path(source)))}", file=sys.stderr)
            if detail.startswith("add") or detail.startswith("re-read"):
                print(f"        {display(source)} is now {digest(source)}", file=sys.stderr)

    if mislabelled:
        print(f"\nsummary names a different source ({len(mislabelled)}):", file=sys.stderr)
        for source in mislabelled:
            print(
                f"  - {display(summary_path(source))} says it describes {recorded_name(summary_path(source))}, "
                f"not {source.name}",
                file=sys.stderr,
            )

    if links:
        print(f"\nbroken cross-reference ({len(links)}):", file=sys.stderr)
        for summary, target in links:
            print(f"  - {display(summary)} links to {target}, which does not exist", file=sys.stderr)

    print(
        "\nA summary records the source it was written against. Update the prose, then run:\n"
        "  python3 tools/check-summaries.py --sha <source>   # prints the marker line to paste\n"
        "  python3 tools/generate-summary-index.py           # refreshes docs/source-summaries.md",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
