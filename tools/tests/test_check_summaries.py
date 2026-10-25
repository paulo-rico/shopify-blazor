#!/usr/bin/env python3
"""Tests for check-summaries.py and generate-summary-index.py.

The summaries are load-bearing: an agent reads one instead of the source, so a summary that silently
describes last month's behaviour is worse than no summary. What is worth testing is therefore the
detection — a missing file, a stale hash, a hash that CRLF fakes — plus the one thing the index
generator must get right, which is which sentence becomes the map entry.
"""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from modules import TOOLS_DIR, load_tool, read, run_tool

check = load_tool("check-summaries.py")


def summary_for(source: Path, text: str = "A file that does one thing.") -> str:
    return (
        f"<!-- summary-of: {source.name} -->\n"
        f"<!-- source-sha256: {check.digest(source)} -->\n"
        f"\n# {source.name}\n\n{text}\n"
    )


class SummaryCheckCase(unittest.TestCase):
    """Each test gets its own directory, because every one of them is about a file's absence."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory(prefix="polaris-summaries-")
        self.root = Path(self._tmp.name)
        (self.root / "src").mkdir()
        self.source = self.root / "src" / "Widget.cs"
        self.source.write_text("public class Widget { }\n", encoding="utf-8", newline="\n")
        self.summary = self.source.with_name("Widget.cs.md")
        self.result = None

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def run_check(self, *args: str):
        self.result = run_tool("check-summaries.py", *(args or (str(self.root),)))
        return self.result

    def test_a_source_with_no_summary_fails_and_names_the_file(self):
        # Without this the whole mechanism is optional in practice: nothing else notices that a file
        # has no summary, and an agent silently falls back to reading the source after all.
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stdout)
        self.assertIn("no summary (1)", result.stderr)
        self.assertIn("Widget.cs.md", result.stderr)

    def test_a_summary_whose_hash_matches_passes(self):
        self.summary.write_text(summary_for(self.source), encoding="utf-8")
        result = self.run_check()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("all 1 source summaries are present and current", result.stdout)

    def test_a_summary_from_before_the_last_edit_fails_as_stale(self):
        # The failure this exists to catch: the source changed, the prose was not touched, and the
        # summary now describes a component that no longer exists in that shape.
        self.summary.write_text(summary_for(self.source), encoding="utf-8")
        self.source.write_text("public class Widget { public int Added { get; set; } }\n", encoding="utf-8")
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("older than its source", result.stderr)
        # It has to say what the new hash is, or the fix is a guess at a hash function.
        self.assertIn(check.digest(self.source), result.stderr)

    def test_a_summary_without_a_marker_is_reported_separately_from_a_stale_one(self):
        # "unmarked" and "stale" need different fixes — add a line, versus re-read the source — so
        # collapsing them into one message sends the reader to the wrong task.
        self.summary.write_text("# Widget.cs\n\nNo marker here.\n", encoding="utf-8")
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("no source-sha256 marker", result.stderr)

    def test_line_endings_do_not_make_every_summary_stale(self):
        # A CRLF checkout on Windows and an LF checkout in CI hash the same text differently. The
        # marker is a drift signal, not a byte-for-byte one, so the digest normalises first. Without
        # that, every summary in the repository reads as stale on a Windows machine.
        lf = self.root / "src" / "lf.cs"
        lf.write_bytes(b"first\nsecond\n")
        crlf = self.root / "src" / "crlf.cs"
        crlf.write_bytes(b"first\r\nsecond\r\n")

        self.assertEqual(check.digest(lf), check.digest(crlf))
        for source in (self.source, lf, crlf):
            source.with_name(source.name + ".md").write_text(summary_for(source), encoding="utf-8")

        result = self.run_check()
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_sha_prints_the_marker_the_checker_accepts(self):
        # The documented follow-up step. If the printed line is not the line the checker accepts,
        # every update becomes a round trip of guessing.
        result = self.run_check("--sha", str(self.source))
        self.assertEqual(result.returncode, 0, result.stderr)
        marker = result.stdout.strip()
        self.assertEqual(marker, f"<!-- source-sha256: {check.digest(self.source)} -->")

        self.summary.write_text(f"<!-- summary-of: Widget.cs -->\n{marker}\n\n# Widget.cs\n\nText.\n", encoding="utf-8")
        self.assertEqual(self.run_check().returncode, 0, self.result.stderr)

    def test_a_summary_that_names_another_file_is_reported(self):
        # The header is written by hand, so a copy-pasted one is the one way a summary can describe a
        # file it does not sit beside while its hash still matches — which reads as current.
        self.summary.write_text(summary_for(self.source).replace("Widget.cs -->", "Gadget.cs -->"), encoding="utf-8")
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("names a different source (1)", result.stderr)
        self.assertIn("Gadget.cs, not Widget.cs", result.stderr)

    def test_a_link_to_a_file_that_does_not_exist_is_reported(self):
        # Every "see also" entry is a promise that the next summary exists. A dangling one is
        # invisible until somebody clicks it, which is exactly when it is least useful.
        self.summary.write_text(
            summary_for(self.source) + "\n## See also\n\n- [Nowhere.cs.md](Nowhere.cs.md)\n",
            encoding="utf-8",
        )
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("broken cross-reference (1)", result.stderr)
        self.assertIn("Nowhere.cs.md", result.stderr)

    def test_only_sources_are_expected_to_have_a_summary(self):
        # A README, a .csproj or a summary itself must not produce a demand for a summary of a
        # summary, or the checker reports a repository-sized list of impossible work.
        self.summary.write_text(summary_for(self.source), encoding="utf-8")
        (self.root / "src" / "README.md").write_text("# Notes\n", encoding="utf-8")
        (self.root / "src" / "Lib.csproj").write_text("<Project />\n", encoding="utf-8")
        self.assertEqual(self.run_check().returncode, 0, self.result.stderr)


class IndexCase(unittest.TestCase):
    """The generated map: the first sentence of the first paragraph is the entry."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory(prefix="polaris-index-")
        self.repo = Path(self._tmp.name) / "repo"
        (self.repo / "tools").mkdir(parents=True)
        (self.repo / "src" / "Lib").mkdir(parents=True)
        # Copied, not run from the checkout: the tool resolves its output from its own path, so a
        # copy is what makes it write into this throwaway repository.
        (self.repo / "tools" / "generate-summary-index.py").write_bytes(
            (TOOLS_DIR / "generate-summary-index.py").read_bytes()
        )
        self.source = self.repo / "src" / "Lib" / "Widget.cs"
        self.source.write_text("public class Widget { }\n", encoding="utf-8")

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def run_index(self):
        return subprocess.run(
            [sys.executable, str(self.repo / "tools" / "generate-summary-index.py")],
            cwd=str(self.repo),
            capture_output=True,
            text=True,
            check=False,
        )

    def index(self) -> str:
        return read(self.repo / "docs" / "source-summaries.md")

    def test_the_first_sentence_becomes_the_map_entry(self):
        # The index is the first thing an agent reads, so a purpose line that rambles on, or is the
        # metadata breadcrumb, makes the map useless at a glance.
        (self.repo / "src" / "Lib" / "Widget.cs.md").write_text(
            f"<!-- summary-of: Widget.cs -->\n<!-- source-sha256: {check.digest(self.source)} -->\n\n"
            "# Widget.cs\n\nA widget that does one thing. It also does a second thing, at length.\n\n"
            "`Polaris.Blazor.Components` · renders `<s-widget>`\n",
            encoding="utf-8",
        )

        result = self.run_index()
        self.assertEqual(result.returncode, 0, result.stderr)

        self.assertIn("| [`Widget.cs.md`](../src/Lib/Widget.cs.md) | A widget that does one thing. |", self.index())
        self.assertNotIn("second thing", self.index())
        self.assertIn("## src/Lib", self.index())

    def test_a_source_with_no_summary_still_gets_a_row(self):
        # The index doubles as the list of what is missing, so it must not omit an unsummarised file.
        result = self.run_index()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("| [`Widget.cs.md`](../src/Lib/Widget.cs.md) | *no summary yet* |", self.index())

    def test_the_index_is_byte_stable_when_nothing_changed(self):
        # check-drift.py compares the committed file against a fresh render, so byte-stability is what
        # makes this a usable drift check rather than one that fails on every run.
        self.assertEqual(self.run_index().returncode, 0)
        first = self.index()
        self.assertEqual(self.run_index().returncode, 0)
        self.assertEqual(first, self.index())


if __name__ == "__main__":
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    unittest.main(verbosity=2)
