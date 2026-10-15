#!/usr/bin/env python3
"""Fails when a generated file in the repository no longer matches its generator.

Several files here are derived rather than written: the component reference, the component
inventory, the CLI's component catalogue, and the toolkit version pinned in the template. Each is
committed so it can be read without running a tool, which means each can rot the moment somebody
edits a component and forgets to regenerate.

The check is deliberately non-destructive. It snapshots each generated file, runs the generator, and
compares. On drift it restores the committed content and reports the difference, so running this in
the middle of other work cannot silently rewrite what you were about to commit. `git diff` would be
simpler and would also flag unrelated edits, which is a different question from "is this stale?".

Usage:
    python3 tools/check-drift.py                 # all checks
    python3 tools/check-drift.py --list          # show what is checked
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# Each entry runs one generator and names every file that generator owns. All outputs of a command
# are snapshotted before it runs and compared after, so a generator that writes three files is one
# check rather than three invocations.
CHECKS: list[dict] = [
    {
        "name": "component reference (docs/components.md)",
        "command": ["tools/generate-component-reference.py"],
        "outputs": ["docs/components.md"],
    },
    {
        "name": "component inventory (JSON, Markdown and the CLI catalogue)",
        "command": ["tools/generate-component-inventory.py"],
        "outputs": [
            "docs/component-inventory.json",
            "docs/component-inventory.md",
            "src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json",
        ],
    },
    {
        # The template pins the toolkit version in a template parameter, where it cannot be derived
        # from Directory.Build.props. A tool that knows both values checks them; keeping the
        # comparison in one place is why this shells out instead of re-reading the two files here.
        "name": "template toolkit version",
        "command": ["tools/verify-template.py", "--metadata-only"],
        "outputs": [],
    },
]


def snapshot(paths: list[Path]) -> dict[Path, bytes | None]:
    """Reads the current content of each path, or None when it does not exist."""
    return {path: (path.read_bytes() if path.exists() else None) for path in paths}


def restore(before: dict[Path, bytes | None]) -> None:
    for path, content in before.items():
        if content is None:
            path.unlink(missing_ok=True)
        else:
            path.write_bytes(content)


def first_difference(before: bytes | None, after: bytes | None) -> str:
    """A one-line explanation of how two versions differ, for the failure message."""
    if before is None:
        return "the file is not committed at all"
    if after is None:
        return "the generator no longer produces this file"

    before_lines = before.decode("utf-8", errors="replace").splitlines()
    after_lines = after.decode("utf-8", errors="replace").splitlines()

    for index, (old, new) in enumerate(zip(before_lines, after_lines), start=1):
        if old != new:
            return f"first difference on line {index}:\n      committed: {old.strip()[:120]}\n      generated: {new.strip()[:120]}"

    return f"line counts differ: committed {len(before_lines)}, generated {len(after_lines)}"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list", action="store_true", help="list the checks and exit")
    args = parser.parse_args()

    if args.list:
        for check in CHECKS:
            outputs = ", ".join(check["outputs"]) or "(no files; compares two values)"
            print(f"{check['name']}\n    command: {' '.join(check['command'])}\n    outputs: {outputs}")
        return 0

    failures: list[str] = []

    for check in CHECKS:
        script = ROOT / check["command"][0]
        if not script.exists():
            failures.append(f"{check['name']}: {check['command'][0]} is missing, so this cannot be checked")
            continue

        outputs = [ROOT / name for name in check["outputs"]]
        before = snapshot(outputs)

        print(f"checking {check['name']} …", flush=True)
        completed = subprocess.run(
            [sys.executable, *check["command"]],
            cwd=ROOT,
            capture_output=True,
            text=True,
        )

        if completed.returncode != 0:
            failures.append(
                f"{check['name']}: the generator failed with exit code {completed.returncode}\n"
                f"{(completed.stdout + completed.stderr)[-1500:]}"
            )
            continue

        for path in outputs:
            after = path.read_bytes() if path.exists() else None
            if before[path] != after:
                failures.append(
                    f"{check['name']}: {path.relative_to(ROOT)} is out of date — {first_difference(before[path], after)}"
                )

        # Put the working tree back exactly as it was found, whatever the outcome.
        restore(before)

    if failures:
        print("\ngenerated files have drifted:\n", file=sys.stderr)
        for failure in failures:
            print(f"  - {failure}", file=sys.stderr)
        print("\nRun the generator named above and commit the result.", file=sys.stderr)
        return 1

    print(f"\nall {len(CHECKS)} generated-file checks are in sync")
    return 0


if __name__ == "__main__":
    sys.exit(main())
