"""Tests for tools/generate-component.py.

Run either of:
    python3 -m unittest discover -s tools/tests -v
    python3 tools/tests/run.py

The tests come in three kinds, and the difference matters:

  * golden-file: the generated C# for a mechanical spec must equal the file in `golden/` byte for
    byte. A change in the template is then a visible diff in review rather than a surprise.
  * refusal: a spec that needs a human must not produce a file silently, and must not overwrite one
    that already exists. Both of those are the failures that would ship broken generated code.
  * round trip: generating from the committed catalogue's own format has to produce the same file,
    which is what keeps the inventory and the generator from drifting apart.
"""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import modules

FIXTURE = modules.FIXTURES


class MechanicalGoldenTest(unittest.TestCase):
    """A mechanical spec generates a wrapper identical to the committed golden file."""

    def test_mechanical_fixture_matches_the_golden_file_byte_for_byte(self):
        # The point of a golden file: any change to the template, the attribute rendering or the
        # ordering shows up as a diff here instead of quietly altering every future wrapper.
        with tempfile.TemporaryDirectory() as out:
            result = modules.run_tool(
                "generate-component.py", "--spec", str(FIXTURE / "mechanical.json"), "--element", "s-accordion", "--out", out
            )
            self.assertEqual(result.returncode, 0, result.stderr)

            generated = Path(out) / "PolarisAccordion.cs"
            self.assertTrue(generated.exists(), result.stdout)

            expected = modules.read(modules.GOLDEN / "PolarisAccordion.golden.cs")
            actual = modules.read(generated)
            difference = modules.first_difference(expected, actual)
            self.assertIsNone(
                difference,
                f"generated output differs at line {difference[0] if difference else 0}: "
                f"golden={difference[1]!r} generated={difference[2]!r}" if difference else "",
            )

    def test_the_mechanical_fixture_reports_nothing_left_to_do(self):
        with tempfile.TemporaryDirectory() as out:
            result = modules.run_tool(
                "generate-component.py", "--spec", str(FIXTURE / "mechanical.json"), "--element", "s-accordion", "--out", out
            )

            self.assertIn("nothing left to do", result.stdout)
            self.assertNotIn("TODO(polaris)", modules.read(Path(out) / "PolarisAccordion.cs"))

    def test_boolean_attributes_render_as_presence_not_as_false(self):
        # `disabled="false"` is still true to a custom element, so a bool must go through
        # PolarisAttributeValue.Format. A golden file catches this, but the assertion names the
        # failure for the reader who has never met the trap.
        with tempfile.TemporaryDirectory() as out:
            modules.run_tool(
                "generate-component.py", "--spec", str(FIXTURE / "mechanical.json"), "--element", "s-accordion", "--out", out
            )

            body = modules.read(Path(out) / "PolarisAccordion.cs")
            self.assertIn('attributes["open"] = PolarisAttributeValue.Format(Open);', body)


class RefusalTest(unittest.TestCase):
    """The generator refuses to do the parts the plan assigns to a human."""

    def run_fixture(self, fixture: str, *extra: str, out: str):
        return modules.run_tool(
            "generate-component.py",
            "--spec",
            str(FIXTURE / fixture),
            "--element",
            "s-accordion",
            "--out",
            out,
            *extra,
        )

    def test_array_property_exits_non_zero_and_writes_nothing(self):
        with tempfile.TemporaryDirectory() as out:
            result = self.run_fixture("array-property.json", out=out)

            self.assertEqual(result.returncode, 1)
            self.assertIn("array DOM property", result.stderr)
            self.assertIn("nothing was written", result.stderr)
            self.assertEqual(list(Path(out).glob("*.cs")), [])

    def test_non_reflected_value_exits_non_zero_and_names_the_property_override(self):
        with tempfile.TemporaryDirectory() as out:
            result = self.run_fixture("non-reflected-value.json", out=out)

            self.assertEqual(result.returncode, 1)
            self.assertIn("`Value` is a DOM property, not an attribute", result.stderr)
            self.assertIn("Properties", result.stderr)

    def test_slot_wiring_exits_non_zero_and_explains_the_child_slot_attribute(self):
        with tempfile.TemporaryDirectory() as out:
            result = self.run_fixture("slot-wiring.json", out=out)

            self.assertEqual(result.returncode, 1)
            self.assertIn("slot `primary-action`", result.stderr)
            self.assertIn("child component", result.stderr)

    def test_unmapped_event_exits_non_zero_and_asks_for_a_constant(self):
        with tempfile.TemporaryDirectory() as out:
            result = self.run_fixture("unmapped-event.json", out=out)

            self.assertEqual(result.returncode, 1)
            self.assertIn("event `toggle` has no `PolarisEvents` constant", result.stderr)

    def test_allow_todos_writes_the_file_with_an_impossible_to_miss_block(self):
        with tempfile.TemporaryDirectory() as out:
            result = self.run_fixture("slot-wiring.json", "--allow-todos", out=out)

            self.assertEqual(result.returncode, 0, result.stderr)
            body = modules.read(Path(out) / "PolarisAccordion.cs")
            self.assertTrue(body.startswith("// ==="), body[:80])
            self.assertIn("// TODO(polaris):", body)
            self.assertIn("is NOT finished", body)
            # The summary a human sees on stdout is the same list that is in the file.
            self.assertIn("slot `primary-action`", result.stdout)

    def test_refuses_to_overwrite_an_existing_file_without_force(self):
        with tempfile.TemporaryDirectory() as out:
            first = self.run_fixture("mechanical.json", out=out)
            self.assertEqual(first.returncode, 0, first.stderr)

            target = Path(out) / "PolarisAccordion.cs"
            # A wrapper is hand-finished after generation, so overwriting would discard exactly the
            # work the TODOs asked for.
            target.write_text("// hand-finished\n", encoding="utf-8")

            second = self.run_fixture("mechanical.json", out=out)
            self.assertEqual(second.returncode, 1)
            self.assertIn("refusing to overwrite", second.stderr)
            self.assertEqual(target.read_text(encoding="utf-8"), "// hand-finished\n")

    def test_force_overwrites(self):
        with tempfile.TemporaryDirectory() as out:
            self.run_fixture("mechanical.json", out=out)
            target = Path(out) / "PolarisAccordion.cs"
            target.write_text("// stale\n", encoding="utf-8")

            result = self.run_fixture("mechanical.json", "--force", out=out)

            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("public sealed class PolarisAccordion", target.read_text(encoding="utf-8"))

    def test_an_unknown_element_is_a_usage_error_not_a_crash(self):
        with tempfile.TemporaryDirectory() as out:
            result = modules.run_tool(
                "generate-component.py", "--spec", str(FIXTURE / "mechanical.json"), "--element", "s-nope", "--out", out
            )

            self.assertEqual(result.returncode, 2)
            self.assertIn("no spec entry", result.stderr)
            self.assertIn("s-accordion", result.stderr)


class InventoryRoundTripTest(unittest.TestCase):
    """The generator accepts the joined catalogue's own field names."""

    def test_inventory_shaped_input_produces_the_same_file_as_the_plain_spec(self):
        # The inventory is the single source of the code-derivable half, so generating from it has
        # to work; otherwise the catalogue and the generator would need two hand-kept vocabularies.
        with tempfile.TemporaryDirectory() as inventory_out, tempfile.TemporaryDirectory() as plain_out:
            inventory = modules.run_tool(
                "generate-component.py",
                "--spec",
                str(FIXTURE / "inventory-shaped.json"),
                "--element",
                "s-accordion",
                "--out",
                inventory_out,
            )
            plain = modules.run_tool(
                "generate-component.py",
                "--spec",
                str(FIXTURE / "mechanical.json"),
                "--element",
                "s-accordion",
                "--out",
                plain_out,
            )

            self.assertEqual(inventory.returncode, 0, inventory.stderr)
            self.assertEqual(plain.returncode, 0, plain.stderr)
            self.assertEqual(
                modules.read(Path(inventory_out) / "PolarisAccordion.cs"),
                modules.read(Path(plain_out) / "PolarisAccordion.cs"),
            )

    def test_generating_from_the_committed_catalogue_reproduces_the_hand_written_badge(self):
        # PolarisBadge is mechanical end to end, so the generator has to reproduce its parameter
        # declarations, defaults, and BuildAttributes exactly. This is the real-data check: the
        # golden files use a fixture, and a fixture cannot catch a parser that only handles it.
        catalogue = modules.ROOT / "docs" / "component-inventory.json"
        self.assertTrue(catalogue.exists(), "run tools/generate-component-inventory.py first")

        with tempfile.TemporaryDirectory() as out:
            result = modules.run_tool(
                "generate-component.py", "--from-inventory", "--element", "s-badge", "--out", out
            )
            self.assertEqual(result.returncode, 0, result.stderr)

            generated = modules.read(Path(out) / "PolarisBadge.cs")
            real = modules.read(modules.ROOT / "src" / "Polaris.Blazor" / "Components" / "PolarisBadge.cs")

            for line in (
                "[Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;",
                "[Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;",
                "[Parameter] public PolarisSize Size { get; set; } = PolarisSize.Base;",
                "[Parameter] public string? Icon { get; set; }",
                'attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);',
                'attributes["icon"] = Icon;',
            ):
                self.assertIn(line, generated)
                self.assertIn(line, real)

    def test_list_names_every_element_in_the_spec(self):
        result = modules.run_tool("generate-component.py", "--list")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("s-button", result.stdout)
        self.assertIn("PolarisBadge", result.stdout)
        self.assertEqual(len(result.stdout.strip().splitlines()), 62)


class LibraryTypeTest(unittest.TestCase):
    """The generator reads the library's enums and event constants rather than inventing them."""

    def test_every_generated_event_uses_a_real_constant(self):
        tool = modules.load_tool("generate-component.py")
        constants = tool.event_constants()

        # A constant the wrapper file defines must be found; otherwise every event would be a TODO.
        self.assertEqual(constants["click"], "Click")
        self.assertEqual(constants["nextpage"], "NextPage")
        self.assertEqual(constants["droprejected"], "DropRejected")

    def test_the_wrapper_name_is_derived_from_the_element(self):
        tool = modules.load_tool("generate-component.py")

        self.assertEqual(tool.wrapper_name("s-button-group"), "PolarisButtonGroup")
        self.assertEqual(tool.wrapper_name("s-money-field"), "PolarisMoneyField")
        self.assertEqual(tool.wrapper_name("s-empty-state"), "PolarisEmptyState")


if __name__ == "__main__":
    unittest.main()
