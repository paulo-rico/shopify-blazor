"""Shared helpers for the generator tests.

The two tools under test have hyphens in their filenames (`generate-component.py`), so they are not
importable by name. They are loaded from their path instead, which also means the tests exercise the
same module the command line does rather than a copy of the logic.

Standard library only, like the tools themselves: these tests have to run on a machine with nothing
but Python 3.
"""

from __future__ import annotations

import contextlib
import importlib.util
import io
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TESTS_DIR = Path(__file__).resolve().parent
TOOLS_DIR = TESTS_DIR.parent
ROOT = TOOLS_DIR.parent

FIXTURES = TESTS_DIR / "fixtures"
GOLDEN = TESTS_DIR / "golden"

# Plain aliases: an f-string in Python 3.11 cannot contain a nested same-type quote, and these paths
# appear inside generated f-strings in the assertions below.
_GENERATE = "generate-component.py"
_INVENTORY = "generate-component-inventory.py"


def load_tool(filename: str):
    """Imports a tool module from its file, registering it under a name tests can re-find."""
    name = filename.replace(".py", "").replace("-", "_")
    if name in sys.modules:
        return sys.modules[name]

    spec = importlib.util.spec_from_file_location(name, TOOLS_DIR / filename)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def run_tool(filename: str, *args: str, cwd: Path | None = None) -> subprocess.CompletedProcess:
    """Runs a tool exactly as the documented invocation does, so exit codes are the real ones."""
    return subprocess.run(
        [sys.executable, str(TOOLS_DIR / filename), *args],
        cwd=str(cwd or ROOT),
        capture_output=True,
        text=True,
        check=False,
    )


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


@contextlib.contextmanager
def captured():
    """Captures stdout and stderr, so a test can assert on what a human would have seen."""
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        yield out, err


class TempRepoCase(unittest.TestCase):
    """
    A throwaway copy of the repository's shape, for the tests that must mutate an input.

    A drift check can only be tested by making the inputs disagree with the outputs, and doing that
    in the real tree would leave the checkout modified if a test failed halfway. The copy holds the
    same directory names so the tools' own path arithmetic — which resolves from their file — works
    unchanged.
    """

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory(prefix="polaris-inventory-")
        self.repo = Path(self._tmp.name) / "repo"
        (self.repo / "src" / "Polaris.Blazor" / "Components").mkdir(parents=True)
        (self.repo / "tests" / "Polaris.Blazor.Tests").mkdir(parents=True)
        (self.repo / "src" / "Shopify.Blazor.Cli" / "Catalogue").mkdir(parents=True)
        (self.repo / "tools").mkdir(parents=True)
        # The tools are copied, not symlinked: a test may rewrite them, and the real checkout must
        # not be affected.
        shutil.copy(TOOLS_DIR / _INVENTORY, self.repo / "tools" / _INVENTORY)
        shutil.copy(TOOLS_DIR / _GENERATE, self.repo / "tools" / _GENERATE)
        self.spec = self.repo / "tools" / "polaris-elements.json"
        self.spec.write_text(STUB_SPEC, encoding="utf-8")
        self.component = self.repo / "src" / "Polaris.Blazor" / "Components" / "PolarisAccordion.cs"
        self.component.write_text(STUB_WRAPPER, encoding="utf-8")

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def run_repo_tool(self, *args: str) -> subprocess.CompletedProcess:
        return subprocess.run(
            [sys.executable, str(self.repo / "tools" / _INVENTORY), *args],
            cwd=str(self.repo),
            capture_output=True,
            text=True,
            check=False,
        )

    def write_outputs(self) -> None:
        result = self.run_repo_tool()
        self.assertEqual(result.returncode, 0, result.stderr)


STUB_WRAPPER = """using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps the drift fixture.</summary>
public sealed class PolarisAccordion : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-accordion";

    /// <summary>The panel heading.</summary>
    [Parameter] public string? Heading { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["heading"] = Heading;
    }
}
"""

STUB_SPEC = """{
  "minPolarisVersion": "1.1",
  "elements": [
    {
      "element": "s-accordion",
      "officialName": "Accordion",
      "category": "layout-and-structure",
      "slug": "accordion",
      "docsUrl": "https://shopify.dev/docs/api/app-home/latest/web-components/layout-and-structure/accordion",
      "slots": [],
      "events": [],
      "accessibility": ["The fixture has nothing to say about accessibility."],
      "minPolarisVersion": "1.1",
      "verification": "unverified",
      "notes": "A drift fixture."
    }
  ]
}
"""


def first_difference(expected: str, actual: str):
    """The first line number and its two versions, for a readable assertion message."""
    expected_lines = expected.splitlines()
    actual_lines = actual.splitlines()
    for index in range(max(len(expected_lines), len(actual_lines))):
        left = expected_lines[index] if index < len(expected_lines) else "<missing>"
        right = actual_lines[index] if index < len(actual_lines) else "<missing>"
        if left != right:
            return index + 1, left, right
    return None


def marker_count(text: str) -> int:
    return len(re.findall(r"TODO\(polaris\)", text))
