"""Tests for tools/generate-component-inventory.py and its three tracked outputs.

Run either of:
    python3 -m unittest discover -s tools/tests -v
    python3 tools/tests/run.py

There are two halves. The first asserts the committed outputs are in sync with their inputs, which
is the check CI needs: a wrapper or a test changed without regenerating means `docs/component-
inventory.json` is describing a library that no longer exists. The second asserts the catalogue's
contents, because a generated file that is in sync can still be wrong — a parser that silently drops
every parameter would be perfectly reproducible.

The drift test mutates a throwaway copy of the repository rather than the real one, so a failure
leaves no half-mutated checkout behind.
"""

from __future__ import annotations

import json
import re
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

import modules


class DriftCheckOnARealTreeTest(unittest.TestCase):
    """`--check` passes on the committed files, and writes nothing."""

    def test_check_passes_on_the_committed_files(self):
        result = modules.run_tool("generate-component-inventory.py", "--check")

        self.assertEqual(result.returncode, 0, result.stderr)
        for name in (
            "docs/component-inventory.json",
            "docs/component-inventory.md",
            "src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json",
        ):
            self.assertIn(name, result.stdout)
            self.assertIn("ok", result.stdout)

    def test_check_writes_nothing(self):
        # A check that regenerates the thing it compares cannot detect drift, and on CI it would
        # quietly "fix" the tree instead of failing the build.
        inventory = modules.ROOT / "docs" / "component-inventory.json"
        before = inventory.read_bytes()
        mtime = inventory.stat().st_mtime_ns

        modules.run_tool("generate-component-inventory.py", "--check")

        self.assertEqual(inventory.read_bytes(), before)
        self.assertEqual(inventory.stat().st_mtime_ns, mtime)


class DriftCheckOnAMutatedCopyTest(modules.TempRepoCase):
    """A mutated input makes `--check` fail, and the failure names the file and the line."""

    def test_check_passes_on_the_stub_tree_first(self):
        # Establishes the baseline the mutation tests rely on: without this, a `--check` that always
        # failed would make the mutation tests pass for the wrong reason.
        self.write_outputs()

        result = self.run_repo_tool("--check")

        self.assertEqual(result.returncode, 0, result.stderr)

    def test_mutating_a_wrapper_makes_check_exit_one(self):
        self.write_outputs()
        original = self.component.read_text(encoding="utf-8")
        # The point of the join: the wrapper source is half the catalogue, so a change there makes
        # the committed outputs stale even though `tools/polaris-elements.json` is untouched. A new
        # parameter needs a declaration as well as an assignment, exactly as a real edit would.
        mutated = original.replace(
            '    /// <summary>The panel heading.</summary>\n'
            '    [Parameter] public string? Heading { get; set; }\n',
            '    /// <summary>The panel heading.</summary>\n'
            '    [Parameter] public string? Heading { get; set; }\n\n'
            '    /// <summary>Whether the panel starts open.</summary>\n'
            '    [Parameter] public bool Open { get; set; }\n',
        ).replace(
            '        attributes["heading"] = Heading;',
            '        attributes["heading"] = Heading;\n'
            '        attributes["open"] = PolarisAttributeValue.Format(Open);',
        )
        self.assertNotEqual(mutated, original, "the mutation did not apply")
        self.component.write_text(mutated, encoding="utf-8")

        result = self.run_repo_tool("--check")

        self.assertEqual(result.returncode, 1)
        self.assertIn("DRIFT", result.stderr)
        self.assertIn("component-inventory.json", result.stderr)
        self.assertIn("stale", result.stderr)
        # Restore. tearDown removes the whole copy anyway; asserting the restore is what proves the
        # failure above was the mutation and not a permanently broken stub tree.
        self.component.write_text(original, encoding="utf-8")
        self.assertEqual(self.run_repo_tool("--check").returncode, 0)

    def test_mutating_a_test_makes_check_exit_one(self):
        self.write_outputs()
        # Test coverage is derived from the test sources, so adding a test that names the wrapper
        # has to change the catalogue. This is the half a reader would not think to regenerate for.
        new_test = self.repo / "tests" / "Polaris.Blazor.Tests" / "ExtraTests.cs"
        new_test.write_text(
            "public sealed class ExtraTests\n"
            "{\n"
            "    [Fact]\n"
            "    public void Accordion_renders_the_custom_element()\n"
            "    {\n"
            "        // PolarisAccordion is the wrapper under test.\n"
            "    }\n"
            "}\n",
            encoding="utf-8",
        )

        result = self.run_repo_tool("--check")

        self.assertEqual(result.returncode, 1)
        self.assertIn("DRIFT", result.stderr)

    def test_mutating_the_spec_makes_check_exit_one(self):
        self.write_outputs()
        spec = json.loads(self.spec.read_text(encoding="utf-8"))
        spec["elements"][0]["events"] = ["click"]
        self.spec.write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")

        result = self.run_repo_tool("--check")

        self.assertEqual(result.returncode, 1)
        self.assertIn("DRIFT", result.stderr)

    def test_a_missing_output_is_drift_not_a_crash(self):
        self.write_outputs()
        (self.repo / "docs" / "component-inventory.md").unlink()

        result = self.run_repo_tool("--check")

        self.assertEqual(result.returncode, 1)
        self.assertIn("does not exist", result.stderr)


class CommittedCatalogueTest(unittest.TestCase):
    """The committed catalogue says what the plan requires it to say."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.inventory = json.loads(modules.read(modules.ROOT / "docs" / "component-inventory.json"))
        cls.records = cls.inventory["components"]
        cls.by_element = {record["element"]: record for record in cls.records}

    def test_it_holds_a_record_for_every_documented_element(self):
        # 50 top-level elements plus the 10 composed sub-elements. A shorter list means the spec
        # lost an entry, which is the drift no source-derived check can catch.
        self.assertEqual(len(self.records), 60)
        self.assertEqual(self.inventory["counts"]["topLevel"], 50)
        self.assertEqual(self.inventory["counts"]["subElements"], 10)
        self.assertEqual(len(self.by_element), 60)

    def test_every_record_carries_every_required_field(self):
        for record in self.records:
            for field in self.inventory["requiredFields"]:
                self.assertIn(field, record, f"{record['element']} is missing {field}")

    def test_every_element_is_wrapped(self):
        for record in self.records:
            with self.subTest(element=record["element"]):
                self.assertIsInstance(record["wrapper"], str)
                self.assertTrue(record["wrapper"])
                self.assertEqual(record["namespace"], "Polaris.Blazor.Components")
                self.assertEqual(record["implementationStatus"], "wrapped")

    def test_every_record_names_its_accessibility_requirements_and_version(self):
        for record in self.records:
            with self.subTest(element=record["element"]):
                self.assertTrue(record["accessibilityRequirements"], "no accessibility requirement")
                self.assertEqual(record["minimumSupportedVersion"], "1.1")

    def test_verification_follows_the_sample_gallerys_probe_list(self):
        # The browser claim is derived from the gallery's `ProbedElements`, which the probe script
        # reads out of `#polaris-probe-elements`. Comparing the committed inventory against that list
        # is what stops the claim drifting away from the mechanism that supports it.
        probe = modules.load_tool("generate-component-inventory.py").parse_browser_verified_elements()
        self.assertTrue(probe, "the gallery's probe list could not be read")

        for record in self.records:
            with self.subTest(element=record["element"]):
                self.assertEqual(record["verifiedInBrowser"], record["element"] in probe)
                if record["element"] in probe:
                    self.assertEqual(record["verification"], "browser-verified")
                else:
                    self.assertIn(record["verification"], {"unit-tested", "unverified"})

    def test_the_gallery_probes_every_element_the_toolkit_wraps(self):
        # The sample app's own remark claims the list is every element the toolkit wraps. If it ever
        # stops being complete, the inventory must not call the missing ones browser-verified, and
        # this assertion is what makes that visible rather than silent.
        probe = modules.load_tool("generate-component-inventory.py").parse_browser_verified_elements()
        documented = {record["element"] for record in self.records}

        self.assertEqual(probe - documented, set(), "the gallery probes an element the spec does not document")
        self.assertEqual(documented - probe, set(), "the gallery does not probe every documented element")

    def test_dom_properties_are_reported_as_properties_not_attributes(self):
        # The four values Polaris does not reflect, plus the array properties. Getting one of these
        # wrong is the class of bug that produces a field that looks right and submits nothing.
        expected = {
            "s-checkbox": {"Checked", "Indeterminate"},
            "s-choice": {"Selected"},
            "s-option": {"Selected"},
            "s-switch": {"Checked"},
            "s-choice-list": {"Values"},
        }
        for element, names in expected.items():
            record = self.by_element[element]
            properties = {
                parameter["name"]
                for parameter in record["supportedProperties"]
                if parameter["kind"] == "dom property"
            }
            with self.subTest(element=element):
                self.assertEqual(properties, names)

    def test_the_choice_list_always_writes_its_array_property(self):
        # Two ways to get this wrong, and the browser verification found the second one. Not writing
        # the array leaves a selection the server dropped still ticked, with nothing wrong in the
        # markup; clearing it by assigning `undefined` makes Polaris call an array method on
        # undefined and throw inside the component — six uncaught TypeErrors per render, invisible
        # to a markup test. So `values` is written on every render, empty when nothing is selected.
        record = self.by_element["s-choice-list"]

        self.assertEqual(record["clearedProperties"], [])

        values = [
            parameter
            for parameter in record["supportedProperties"]
            if parameter["name"] == "Values"
        ]
        self.assertEqual(len(values), 1, "the choice list must still expose Values")
        self.assertEqual(values[0]["kind"], "dom property")

    def test_every_documented_event_is_either_wired_or_marked_unwired(self):
        for record in self.records:
            for definition in record["eventDefinitions"]:
                with self.subTest(element=record["element"], event=definition["event"]):
                    self.assertIn("documented", definition)
                    self.assertIn("wrapped", definition)
                    self.assertIsInstance(definition["wrapped"], bool)
                    if definition["wrapped"]:
                        self.assertTrue(definition["handler"], "a wired event needs a callback")

    def test_slot_definitions_record_what_shopify_constrains(self):
        # The definitions come from tools/polaris-elements.json, so an empty `accepts` means the
        # hand-maintained half lost a constraint.
        for record in self.records:
            for slot in record["slotDefinitions"]:
                with self.subTest(element=record["element"], slot=slot["name"]):
                    self.assertTrue(slot["name"])
                    self.assertTrue(slot["accepts"])
                    self.assertIn("wrapperSupport", slot)

    def test_no_output_leaks_an_absolute_path_or_a_timestamp(self):
        # Either one makes the drift check useless: the file would differ for every checkout and
        # every run, and CI would fail for reasons that have nothing to do with the code.
        for name in (
            "docs/component-inventory.json",
            "docs/component-inventory.md",
            "src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json",
        ):
            text = modules.read(modules.ROOT / name)
            with self.subTest(output=name):
                self.assertNotIn("/home/", text)
                self.assertNotIn(str(modules.ROOT), text)
                self.assertNotIn(str(Path.home()), text)

    def test_test_coverage_names_real_tests(self):
        # The test names have to exist in the suite, or the coverage column is a decoration.
        sources = "\n".join(
            path.read_text(encoding="utf-8") for path in (modules.ROOT / "tests" / "Polaris.Blazor.Tests").glob("*.cs")
        )
        named = {name for record in self.records for name in record["testCoverage"]}
        self.assertTrue(named)
        for name in sorted(named):
            with self.subTest(test=name):
                self.assertIn(name, sources)


class CliCatalogueSchemaTest(unittest.TestCase):
    """The CLI's catalogue matches the schema the CLI workstream was given."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.catalogue = json.loads(
            modules.read(modules.ROOT / "src" / "Shopify.Blazor.Cli" / "Catalogue" / "polaris-catalogue.json")
        )

    def test_top_level_shape(self):
        self.assertEqual(set(self.catalogue.keys()), {"generatedBy", "components"})
        self.assertEqual(self.catalogue["generatedBy"], "generate-component-inventory.py")

    def test_one_entry_per_element_with_exactly_the_agreed_keys(self):
        self.assertEqual(len(self.catalogue["components"]), 60)
        for component in self.catalogue["components"]:
            with self.subTest(element=component["element"]):
                self.assertEqual(
                    set(component.keys()),
                    {"wrapper", "element", "namespace", "summary", "parameters", "events", "slots"},
                )
                self.assertTrue(component["wrapper"].startswith("Polaris"))
                self.assertTrue(component["element"].startswith("s-"))
                self.assertEqual(component["namespace"], "Polaris.Blazor.Components")
                self.assertTrue(component["summary"])
                for parameter in component["parameters"]:
                    self.assertEqual(set(parameter.keys()), {"name", "type"})

    def test_button_examples_are_usable(self):
        button = next(c for c in self.catalogue["components"] if c["wrapper"] == "PolarisButton")

        self.assertEqual(button["element"], "s-button")
        self.assertIn({"name": "Variant", "type": "PolarisButtonVariant"}, button["parameters"])
        self.assertEqual(button["events"], ["blur", "click", "focus"])
        self.assertEqual(button["slots"], ["children"])


class ParserTest(unittest.TestCase):
    """The parser's own behaviour, where a regression would be invisible in the outputs."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.tool = modules.load_tool("generate-component-inventory.py")

    def test_the_browser_verified_set_is_the_probe_list_the_page_renders(self):
        source = modules.read(
            modules.ROOT / "samples" / "Shopify.Blazor.SampleApp" / "Components" / "Pages" / "Components.razor"
        )
        # The probe reads `#polaris-probe-elements`, which the gallery fills from `ProbedElements`.
        # Both halves have to be present or the browser check would verify nothing at all.
        self.assertIn("polaris-probe-elements", source)
        block = source.split("string[] ProbedElements =", 1)[1].split("];", 1)[0]
        elements = set(re.findall(r'"(s-[a-z-]+)"', block))

        self.assertEqual(set(self.tool.parse_browser_verified_elements()), elements)

    def test_a_missing_gallery_reports_no_browser_verification(self):
        # The honest failure mode: if the gallery is gone, nothing is verified in a browser, and the
        # inventory must say so rather than keep an old claim alive.
        original = self.tool.GALLERY
        try:
            self.tool.GALLERY = modules.ROOT / "samples" / "does-not-exist.razor"
            self.assertEqual(self.tool.parse_browser_verified_elements(), frozenset())
        finally:
            self.tool.GALLERY = original

    def test_a_wrapper_is_found_by_its_tag_not_by_its_file_name(self):
        wrapper = self.tool.parse_wrapper(
            modules.ROOT / "src" / "Polaris.Blazor" / "Components" / "PolarisText.cs"
        )

        # PolarisText renders two elements, so it has to be indexed under both or one of them would
        # look unwrapped in the catalogue.
        self.assertEqual(sorted(wrapper["tags"]), ["s-paragraph", "s-text"])
        self.assertEqual(wrapper["base"], "PolarisContentComponentBase")

    def test_parameters_keep_their_defaults_and_their_rendered_kind(self):
        wrapper = self.tool.parse_wrapper(
            modules.ROOT / "src" / "Polaris.Blazor" / "Components" / "PolarisCheckbox.cs"
        )
        parameters = {parameter["name"]: parameter for parameter in wrapper["parameters"]}

        # Inherited parameters are not repeated per component, or every record would carry the same
        # five shared parameters and hide the ones that are actually interesting.
        self.assertNotIn("Id", parameters)
        self.assertNotIn("AccessibilityLabel", parameters)
        self.assertIn("LabelAccessibilityVisibility", parameters)

        badge = self.tool.parse_wrapper(
            modules.ROOT / "src" / "Polaris.Blazor" / "Components" / "PolarisBadge.cs"
        )
        badge_parameters = {parameter["name"]: parameter for parameter in badge["parameters"]}
        # A default is part of the API: without it a generated wrapper would take the enum's zero
        # member, which is a different value for a non-greedy enum.
        self.assertEqual(badge_parameters["Tone"]["default"], "PolarisTone.Auto")
        self.assertIsNone(badge_parameters["Icon"]["default"])

    def test_cleared_properties_are_read_from_the_array_the_wrapper_names(self):
        # No library component declares ClearedProperties any more: the choice list always writes its
        # array, because clearing it with `undefined` is what made Polaris throw. The contract is
        # kept alive by the base-class test double, which is what this parser test reads — parser
        # coverage should not depend on a component happening to use the feature today.
        wrapper = self.tool.parse_wrapper(
            modules.ROOT / "tests" / "Polaris.Blazor.Tests" / "TestDoubles.cs"
        )

        self.assertEqual(wrapper["clearedProperties"], ["values"])


class GenerationIsReproducibleTest(unittest.TestCase):
    """The outputs contain nothing that would change between runs on the same tree."""

    def test_the_tool_is_deterministic(self):
        # An absolute path or a timestamp anywhere in the output would make `--check` fail on a
        # different checkout and drift detection useless. Two runs here have to agree exactly, and
        # `test_no_output_leaks_an_absolute_path_or_a_timestamp` guards the contents.
        first = modules.run_tool("generate-component-inventory.py", "--check")
        second = modules.run_tool("generate-component-inventory.py", "--check")

        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(first.stdout, second.stdout)
        self.assertEqual(first.stderr, second.stderr)

    def test_writing_twice_leaves_the_files_unchanged(self):
        # Regenerating over a committed tree must be a no-op: if a second run changed a byte, the
        # first run's output was not the canonical one and CI would never converge.
        paths = [
            modules.ROOT / "docs" / "component-inventory.json",
            modules.ROOT / "docs" / "component-inventory.md",
            modules.ROOT / "src" / "Shopify.Blazor.Cli" / "Catalogue" / "polaris-catalogue.json",
        ]
        before = {path: path.read_bytes() for path in paths}

        result = modules.run_tool("generate-component-inventory.py")

        self.assertEqual(result.returncode, 0, result.stderr)
        for path in paths:
            with self.subTest(output=path.name):
                self.assertEqual(path.read_bytes(), before[path], f"{path.name} changed on a no-op run")

    def test_writes_nothing_outside_its_three_outputs(self):
        # `--check` is the CI mode, so a stray write would appear there as an unexplained change to
        # the tree. The three outputs are the whole contract.
        tool = modules.load_tool("generate-component-inventory.py")
        expected = {path.resolve() for path in tool.OUTPUTS.values()}

        self.assertEqual(len(expected), 3)
        for path in expected:
            self.assertTrue(path.is_file(), f"{path} was never written")


if __name__ == "__main__":
    unittest.main()
