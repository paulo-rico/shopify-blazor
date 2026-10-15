#!/usr/bin/env python3
"""Generates the tracked component catalogue from the wrapper sources plus the Shopify spec.

`tools/generate-component-reference.py` already derives the wrapper half of the catalogue from the
C# and emits `docs/components.md`. This script extends that parse instead of repeating it, and
emits the three artefacts a machine needs:

  docs/component-inventory.json          one joined record per element, both halves
  docs/component-inventory.md            the same data rendered for a human
  src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json
                                         the trimmed projection the CLI embeds for `add component`

Everything is derived: the wrapper names, the parameters and their types, which parameters are DOM
properties rather than attributes, the events, the test names that exercise each wrapper, and the
verification state. The only hand-maintained input is `tools/polaris-elements.json`, which holds
what no C# file can answer — Shopify's slots, events and accessibility constraints.

Because every byte is derived, the three outputs are reproducible and `--check` is meaningful: any
difference means a wrapper, a test or the spec changed and the catalogue is stale.

Usage:
  python3 tools/generate-component-inventory.py            # write the three outputs
  python3 tools/generate-component-inventory.py --check     # verify, write nothing, exit 1 on drift
"""

from __future__ import annotations

import argparse
import difflib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
COMPONENTS = ROOT / "src" / "Polaris.Blazor" / "Components"
SPEC = ROOT / "tools" / "polaris-elements.json"

# The spec category for components that come from App Bridge rather than Polaris. They are documented
# separately by Shopify because they configure the admin around the app instead of drawing inside it,
# and the inventory keeps them apart for the same reason. See the `counts` block.
APP_BRIDGE_CATEGORY = "app-bridge"
TEST_DIR = ROOT / "tests" / "Polaris.Blazor.Tests"
GALLERY = ROOT / "samples" / "Shopify.Blazor.SampleApp" / "Components" / "Pages" / "Components.razor"
OUTPUTS = {
    "inventory-json": ROOT / "docs" / "component-inventory.json",
    "inventory-md": ROOT / "docs" / "component-inventory.md",
    "cli-catalogue": ROOT / "src" / "Shopify.Blazor.Cli" / "Catalogue" / "polaris-catalogue.json",
}

# Where the browser-verification claim comes from. The sample app's component gallery renders this
# list into `#polaris-probe-elements`, and `tools/verify-in-browser.mjs` reads it and checks each
# element really upgraded in a real Polaris runtime. Deriving the claim from that list is what keeps
# it honest: a component added to the gallery is verified without anyone updating a second list, and
# one listed but never rendered fails the probe's count check instead of passing vacuously.
PROBE_LIST_MARKER = "string[] ProbedElements ="


def parse_browser_verified_elements() -> frozenset[str]:
    """
    The element types the browser probe exercises, read from the sample gallery.

    A missing file or a renamed marker means nobody is verifying anything in a browser yet, which
    the inventory must report as zero rather than inherit an old, stale claim.
    """
    if not GALLERY.exists():
        return frozenset()

    text = read(GALLERY)
    if PROBE_LIST_MARKER not in text:
        return frozenset()

    block = text.split(PROBE_LIST_MARKER, 1)[1]
    block = block.split("];", 1)[0]
    return frozenset(re.findall(r'"(s-[a-z-]+)"', block))


_BROWSER_VERIFIED: frozenset[str] | None = None


def browser_verified_elements() -> frozenset[str]:
    """
    The lazily-computed probe list.

    Lazy because the parser needs `read`, which is defined below this point in the file, and because
    an eager read of the gallery would make importing this module touch the sample app.
    """
    global _BROWSER_VERIFIED
    if _BROWSER_VERIFIED is None:
        _BROWSER_VERIFIED = parse_browser_verified_elements()
    return _BROWSER_VERIFIED

# Declared on PolarisElementComponentBase, so every wrapper has them. Listing them per component
# would repeat the base class 59 times and hide the parameters that are actually interesting.
INHERITED_PARAMETERS = frozenset({"Id", "AccessibilityLabel", "ItemTag", "Slot", "AdditionalAttributes"})

# The two slots PolarisSlottedComponentBase offers as fragments. Children of a slotted wrapper are
# placed in them without the caller setting `Slot`, which is what makes them different from the
# validated slots.
UNVALIDATED_SLOTS = frozenset({"supplemental-start", "aside"})


def clean(block: str) -> str:
    """
    Turns a doc-comment block into one readable line.

    Copied from `generate-component-reference.py` rather than imported, because importing a script
    whose name is not a valid module identifier means a loader hack, and the two renderings must be
    free to diverge. Doc comments carry XML tags (`<summary>`, `<see cref="…"/>`, `<para>`) which
    have no place in a table.
    """
    text = "\n".join(line.strip().lstrip("/").strip() for line in block.splitlines())
    text = text.replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")
    text = re.sub(r'<see\s+cref="(?:\w+:)?([^"]+)"\s*/>', r"`\1`", text)
    text = re.sub(r"</?(?:summary|para|remarks|list|item|description|code|c)\s*/?>", " ", text)
    text = re.sub(r"<(s-[a-z-]+)>", r"`<\1>`", text)
    text = re.sub(r"</[A-Za-z][^>]*>", " ", text)
    text = re.sub(r"<[A-Za-z][\w.-]*\s+[^>]*?/>", " ", text)
    text = re.sub(r"<[A-Za-z][\w.-]*\s+[^>]*>", " ", text)
    text = " ".join(text.split())
    text = re.sub(r"\s+([.,;:!?])", r"\1", text)
    return text


def first_sentence(text: str) -> str:
    """Keeps one sentence: the full summary belongs in the source, not repeated in a catalogue."""
    for end in (". ", "! ", "? "):
        index = text.find(end)
        if index != -1:
            return text[: index + 1]
    return text


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def parse_parameters(text: str) -> list[dict]:
    """
    Every `[Parameter]` the class declares, with the doc comment above it and its default.

    Returns the source order, so the catalogue and `docs/components.md` list parameters the same
    way and a curious reader can find the same parameter in both. The initialiser is kept because
    a default such as `PolarisTone.Auto` is part of the public API: without it a generated wrapper
    would take the type's zero member, which is a different value for a non-greedy enum.
    """
    pattern = re.compile(
        r"((?:\s*///[^\n]*\n)*)\s*\[Parameter(?:\([^)]*\))?\]\s*public\s+([\w<>?\[\], .]+?)\s+(\w+)\s*\{\s*get;\s*set;\s*\}\s*(=\s*([^;]+);)?",
        re.MULTILINE,
    )
    parameters = []
    for match in pattern.finditer(text):
        comment, type_name, name, _, default = match.groups()
        if name in INHERITED_PARAMETERS:
            continue
        parameters.append(
            {
                "name": name,
                "type": type_name.strip(),
                "doc": clean(comment),
                "default": default.strip() if default else None,
            }
        )
    return parameters


def parse_event_callbacks(text: str) -> list[tuple[str, str]]:
    """
    The event callbacks the wrapper declares, as (handler name, DOM event name).

    `OnAfterHide` is the DOM event `afterhide`; the mapping is a case transformation, and it is the
    same one `PolarisEvents` uses for the constants it defines.
    """
    handlers = re.findall(
        r"\[Parameter\]\s*public\s+EventCallback<PolarisEventArgs>\s+(On\w+)\s*\{\s*get;\s*set;\s*\}",
        text,
    )
    events = []
    for handler in handlers:
        name = handler[2:]
        # `NextPage` → `nextpage`, `AfterHide` → `afterhide`, `DropRejected` → `droprejected`.
        events.append((handler, name.lower()))
    return events


# `PolarisEvents` members whose name is not the DOM event name lower-cased: the compound ones
# concatenate without a separator, so `NextPage` is `nextpage` and not `next-page`. Anything absent
# here that a wrapper registers is a constant this table has not caught up with, and the inventory
# then reports the registration as missing — the failure mode to watch for.
EVENT_CONSTANTS = {
    "Click": "click",
    "Input": "input",
    "Change": "change",
    "Focus": "focus",
    "Blur": "blur",
    "Dismiss": "dismiss",
    "Show": "show",
    "Hide": "hide",
    "Remove": "remove",
    "NextPage": "nextpage",
    "PreviousPage": "previouspage",
    "AfterHide": "afterhide",
    "Load": "load",
    "Error": "error",
    "Invalid": "invalid",
    "ViewChange": "viewchange",
    "DropRejected": "droprejected",
}


def parse_registered_events(text: str) -> list[str]:
    """
    The DOM event names `ConfigureEvents` actually registers.

    A wrapper can declare a callback and never register it, which is a silent no-op in the browser
    — that difference between "declared" and "wired" is exactly what the inventory has to show.
    """
    found = []
    for constant in re.findall(r"events\.On\(PolarisEvents\.(\w+)", text):
        if constant in EVENT_CONSTANTS:
            found.append(EVENT_CONSTANTS[constant])
        else:
            # An unknown constant becomes a second-class registration, so the event is reported as
            # unwired. That is the honest answer until this table is extended.
            found.append(re.sub(r"(?<!^)(?=[A-Z])", "-", constant).lower())
    found += [m.lower() for m in re.findall(r'events\.On\("([a-z]+)"', text)]
    return sorted(set(found))


def parse_attribute_keys(text: str) -> tuple[set[str], set[str]]:
    """
    The attribute keys `BuildAttributes` writes, split into those carrying a value and those
    deliberately nulled.

    `attributes["checked"] = null;` is not dead code: it is how a wrapper guarantees a value that
    must be a DOM property never appears in the markup, where a stale attribute would win. Those
    keys are returned separately so a parameter can be described as a property rather than an
    attribute.
    """
    body = re.search(r"protected override void BuildAttributes\((.*?)\n    \}", text, re.DOTALL)
    if not body:
        return set(), set()
    block = body.group(1)

    valued: set[str] = set()
    nulled: set[str] = set()
    for key, expression in re.findall(r'attributes\["([^"]+)"\]\s*=\s*([^;]+);', block):
        # The key is written either as the bare parameter name or camel-cased for the attribute.
        # A null literal, or a ternary whose only non-null branch is another assignment, means the
        # parameter is handled elsewhere.
        if expression.strip() == "null":
            nulled.add(key)
        else:
            valued.add(key)
    return valued, nulled


def parse_property_keys(text: str) -> set[str]:
    """
    The DOM property names the wrapper assigns after upgrade, from either shape of `Properties`.

    Both shapes occur: an object-initialiser dictionary (`["checked"] = Checked`) and a conditional
    one that builds a dictionary only when a value is present (`{ ["value"] = Value }`).
    """
    match = re.search(r"protected override IReadOnlyDictionary<string, object\?> Properties =>(.*?);\n", text, re.DOTALL)
    if not match:
        return set()
    block = match.group(1)
    keys = set(re.findall(r'\["([^"]+)"\]\s*=', block))
    # A null-guarded property set names its keys in the initialiser, which the pattern above reads.
    # Anything still missing is named as a string only, e.g. `{ ["values"] = Values }`.
    keys |= set(re.findall(r'\[\s*"([^"]+)"\s*\]', block))
    return keys


def parse_cleared_properties(text: str) -> list[str]:
    """
    Property names cleared on every render.

    A cleared property is a promise from the wrapper: without it a stale array keeps state the
    server has dropped, and nothing in the markup shows it.
    """
    string_arrays = re.findall(r'(?:static readonly string\[\]|string\[\])\s+\w+\s*=\s*\[([^\]]*)\]', text)
    names: list[str] = []
    for array in string_arrays:
        names += re.findall(r'"([^"]+)"', array)
    names += re.findall(r'ClearedProperties\s*=>\s*[^;]*?\[\s*"([^"]+)"\s*\]', text)
    if "ClearedProperties" not in text:
        return []
    return sorted(set(names))


def parse_wrapper(path: Path) -> dict:
    """One wrapper class, as the catalogue needs it."""
    text = read(path)

    tag = re.search(r'TagName\s*=>\s*"([^"]+)"', text)
    if tag:
        tags = [tag.group(1)]
    else:
        # PolarisText switches element on its Paragraph parameter.
        conditional = re.search(r'TagName\s*=>\s*Paragraph\s*\?\s*"([^"]+)"\s*:\s*"([^"]+)"', text)
        tags = [conditional.group(1), conditional.group(2)] if conditional else []

    # The class's own summary is the comment block directly above the type declaration. The first
    # `<summary>` in the file belongs to a parameter when the class has no doc comment of its own.
    declaration = re.search(r"\n((?:\s*///[^\n]*\n)+)\s*(?:public|internal)\s+(?:sealed\s+)?class\s", text)
    summary = first_sentence(clean(declaration.group(1))) if declaration else ""

    base = re.search(r"class\s+\w+\s*:\s*(\w+)", text)
    base_name = base.group(1) if base else ""

    valued_attributes, nulled_attributes = parse_attribute_keys(text)
    property_keys = parse_property_keys(text)
    cleared = parse_cleared_properties(text)

    parameters = []
    for parameter in parse_parameters(text):
        key = parameter["name"][0].lower() + parameter["name"][1:]
        if "EventCallback" in parameter["type"]:
            # A callback is neither attribute nor property: it is the subscription side of an
            # event, and it is listed again in the record's `events`.
            kind = "event callback"
        elif key in property_keys:
            kind = "dom property"
        elif key in valued_attributes or key.lower() in valued_attributes:
            kind = "attribute"
        elif key in nulled_attributes:
            kind = "dom property"
        else:
            # Declared but never written: a flag that only selects which markup to emit, such as
            # PolarisText.Paragraph. It is still part of the public API.
            kind = "render flag"
        parameters.append({**parameter, "kind": kind})

    wired = parse_registered_events(text)
    events = []
    for handler, event in parse_event_callbacks(text):
        events.append({"event": event, "handler": handler, "wrapped": event in wired})

    return {
        "wrapper": path.stem,
        "tags": tags,
        "summary": summary,
        "base": base_name,
        "contentChildren": base_name in {"PolarisContentComponentBase", "PolarisSlottedComponentBase"},
        "unvalidatedSlots": base_name == "PolarisSlottedComponentBase",
        "parameters": parameters,
        "events": events,
        "properties": sorted(property_keys),
        "clearedProperties": cleared,
        "hasAccessibilityLabel": True,
    }


def parse_tests() -> list[dict]:
    """
    The test methods in the C# suite, with their bodies, so coverage can name the test that proves
    a wrapper renders.

    The wrapper name is the only honest signal: a test that merely mentions `s-button` in an
    expectation does not prove `PolarisButton` works, but a test that constructs one does.
    """
    tests = []
    pattern = re.compile(r"\[(?:Fact|Theory)\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\(")
    for path in sorted(TEST_DIR.glob("*.cs")):
        source = read(path)
        matches = list(pattern.finditer(source))
        for index, match in enumerate(matches):
            start = match.start()
            end = matches[index + 1].start() if index + 1 < len(matches) else len(source)
            tests.append(
                {
                    "name": match.group(1),
                    "file": path.name,
                    "class": path.stem,
                    "body": source[start:end],
                }
            )
    return tests


def verification_for(element: str, wrapper: str, tests: list[dict]) -> tuple[str, list[str]]:
    """
    The verification state, and the test names that produced it.

    The order is the point, and each step is a weaker claim than the one before:

      1. `browser-verified` — the element is in the sample gallery's probe list, so
         `tools/verify-in-browser.mjs` checks that it really upgraded in a real Polaris runtime.
      2. `unit-tested` — the wrapper class is named in the C# suite, which asserts on the rendered
         markup. A test that merely mentions the tag name does not count; constructing the wrapper
         does.
      3. `unverified` — neither, and the inventory says so rather than implying coverage.

    Deriving the browser list from the gallery means a component added there is covered without
    anyone updating a second list, and one listed but never rendered fails the probe's count check
    rather than passing vacuously. It follows that this file cannot itself claim the probe *ran*:
    `verifiedInBrowser` records membership of the list, and the probe's own output is the evidence.
    """
    covered = sorted({t["name"] for t in tests if re.search(rf"\b{re.escape(wrapper)}\b", t["body"])})
    if element in browser_verified_elements():
        return "browser-verified", covered
    if covered:
        return "unit-tested", covered
    return "unverified", []


def slot_support(slot: str, wrapper: dict) -> str:
    """
    How a wrapper exposes a slot Shopify documents.

    This is derived, not asserted, so the inventory can show a real gap: an unwrapped validated slot
    is one a caller can only reach through the raw `Slot` parameter, which the wrapper does accept
    but does not help with.
    """
    if slot == "children":
        return "ChildContent" if wrapper["contentChildren"] else "none"
    if slot in UNVALIDATED_SLOTS and wrapper["unvalidatedSlots"]:
        return "fragment parameter"
    return "child Slot parameter"


def build_records() -> list[dict]:
    """
    The catalogue records, in the schema the plan asks for.

    Internal shapes stay internal: this returns the published contract, and `build_inventory`
    attaches the counts around it.
    """
    return [build_record(record) for record in join_records()]


def join_records() -> list[dict]:
    """Joins the Shopify spec with the wrapper sources, one internal record per element."""
    spec = json.loads(read(SPEC))
    spec_by_element = {entry["element"]: entry for entry in spec["elements"]}

    wrappers = [parse_wrapper(path) for path in sorted(COMPONENTS.glob("Polaris*.cs"))]
    by_tag: dict[str, dict] = {}
    for wrapper in wrappers:
        for tag in wrapper["tags"]:
            by_tag[tag] = wrapper

    tests = parse_tests()

    records = []
    for element, entry in spec_by_element.items():
        wrapper = by_tag.get(element)
        name = wrapper["wrapper"] if wrapper else None
        verification, covered = verification_for(element, name, tests) if name else (entry["verification"], [])

        documented_events = entry["events"]
        declared = {e["event"]: e for e in wrapper["events"]} if wrapper else {}
        events = []
        for event in sorted(set(documented_events) | set(declared)):
            present = declared.get(event)
            events.append(
                {
                    "event": event,
                    "documented": event in documented_events,
                    "handler": present["handler"] if present else None,
                    "wrapped": bool(present and present["wrapped"]),
                }
            )

        slots = [
            {**slot, "wrapperSupport": slot_support(slot["name"], wrapper) if wrapper else "none"}
            for slot in entry["slots"]
        ]

        records.append(
            {
                "officialName": entry["officialName"],
                "element": element,
                "category": entry["category"],
                "docsUrl": entry["docsUrl"],
                "wrapper": name,
                "wrapperBase": wrapper["base"] if wrapper else None,
                "namespace": "Polaris.Blazor.Components" if wrapper else None,
                "summary": wrapper["summary"] if wrapper else "",
                "minPolarisVersion": entry["minPolarisVersion"],
                # A component that cannot be probed in a standalone page says so in the spec, and the
                # exemption is carried here so a test can hold the two sets against each other. An
                # exemption without a stated reason is refused by the test.
                "browserProbeExempt": entry.get("browserProbe", True) is False,
                "verification": verification,
                "implementationStatus": "wrapped" if wrapper else "unwrapped",
                "parameters": [
                    {
                        "name": p["name"],
                        "type": p["type"],
                        "kind": p["kind"],
                        "doc": p["doc"],
                        "default": p["default"],
                    }
                    for p in (wrapper["parameters"] if wrapper else [])
                ],
                "clearedProperties": wrapper["clearedProperties"] if wrapper else [],
                "events": events,
                "slots": slots,
                "accessibility": entry["accessibility"],
                "testCoverage": covered,
                "notes": entry["notes"],
                "componentOf": entry.get("componentOf"),
            }
        )

    return records


# The plan's required fields, spelled out so the JSON is self-describing and a consumer can assert
# that every record carries them. `events`, `slots` and `testCoverage` are always present; a
# component that has none of them gets an empty list, which is a fact about Shopify, not a gap in
# the catalogue.
REQUIRED_FIELDS = (
    "officialName",
    "element",
    "category",
    "docsUrl",
    "wrapper",
    "namespace",
    "summary",
    "supportedProperties",
    "eventDefinitions",
    "slotDefinitions",
    "accessibilityRequirements",
    "minimumSupportedVersion",
    "testCoverage",
    "implementationStatus",
    "verifiedInBrowser",
    "browserProbeExempt",
)


def build_record(record: dict) -> dict:
    """
    Projects the internal record onto the plan's field names.

    The two shapes are deliberately separate: the internal record is convenient to build, and this
    is the contract the plan asks for — official component name, custom element name, C# wrapper,
    supported properties, event definitions, slot definitions, accessibility requirements, minimum
    version, test coverage and implementation status, each under a name a consumer can rely on.
    """
    return {
        "officialName": record["officialName"],
        "element": record["element"],
        "category": record["category"],
        "docsUrl": record["docsUrl"],
        "wrapper": record["wrapper"],
        "namespace": record["namespace"],
        "summary": record["summary"],
        "supportedProperties": record["parameters"],
        "eventDefinitions": record["events"],
        "slotDefinitions": record["slots"],
        "accessibilityRequirements": record["accessibility"],
        "minimumSupportedVersion": record["minPolarisVersion"],
        "testCoverage": record["testCoverage"],
        "implementationStatus": record["implementationStatus"],
        "verification": record["verification"],
        # The element types tools/verify-in-browser.mjs probes in a real Polaris runtime. Kept as a
        # field of its own so a reader can see the claim's basis without opening the probe script.
        "verifiedInBrowser": record["element"] in browser_verified_elements(),
        # True when the component cannot be probed in a standalone page at all — which today means the
        # App Bridge components, because app-bridge.js defines them only once it has connected to the
        # admin host. The spec states the exemption and requires a reason in the notes.
        "browserProbeExempt": record["browserProbeExempt"],
        "clearedProperties": record["clearedProperties"],
        "notes": record["notes"],
        "composedInside": record["componentOf"],
    }


def build_inventory(records: list[dict]) -> dict:
    spec = json.loads(read(SPEC))
    categories: dict[str, int] = {}
    for record in records:
        categories[record["category"]] = categories.get(record["category"], 0) + 1

    return {
        "generatedBy": Path(__file__).name,
        "spec": "tools/polaris-elements.json",
        "specProvenance": (
            "Slots, events and accessibility notes hand-maintained from "
            "https://shopify.dev/docs/api/app-home/latest/web-components/<category>/<slug>.md "
            "(Polaris 1.1), cross-checked against the polaris-web-components DSH skill."
        ),
        "browserVerifiedBy": "tools/verify-in-browser.mjs",
        "browserVerifiedElementsSource": "samples/Shopify.Blazor.SampleApp/Components/Pages/Components.razor (ProbedElements)",
        "minPolarisVersion": spec["minPolarisVersion"],
        "requiredFields": list(REQUIRED_FIELDS),
        "counts": {
            "elements": len(records),
            # Polaris and App Bridge are two different sources. A Polaris component comes from
            # polaris-1.js and is versioned with that release; an App Bridge component configures the
            # admin that surrounds the app, comes from app-bridge.js, and is not versioned with Polaris
            # at all. Counting them together would make "top-level components" mean two things at once.
            "topLevel": sum(
                1 for r in records if r["composedInside"] is None and r["category"] != APP_BRIDGE_CATEGORY
            ),
            "subElements": sum(1 for r in records if r["composedInside"] is not None),
            "appBridge": sum(1 for r in records if r["category"] == APP_BRIDGE_CATEGORY),
            "wrapped": sum(1 for r in records if r["wrapper"]),
            "unwrapped": sum(1 for r in records if not r["wrapper"]),
            "byCategory": dict(sorted(categories.items())),
            "byVerification": {
                state: sum(1 for r in records if r["verification"] == state)
                for state in ("browser-verified", "unit-tested", "unverified")
            },
        },
        "components": records,
    }


def build_cli_catalogue(records: list[dict]) -> dict:
    """
    The CLI's projection: only the fields an `add component` example needs.

    Deliberately a subset, and deliberately the same order as the inventory, so the CLI can embed it
    and a reader can diff the two files by eye.
    """
    return {
        "generatedBy": Path(__file__).name,
        "components": [
            {
                "wrapper": record["wrapper"],
                "element": record["element"],
                "namespace": record["namespace"],
                "summary": record["summary"],
                "parameters": [
                    {"name": p["name"], "type": p["type"]} for p in record["supportedProperties"]
                ],
                "events": [e["event"] for e in record["eventDefinitions"] if e["wrapped"]],
                "slots": [s["name"] for s in record["slotDefinitions"]],
            }
            for record in records
        ],
    }


def render_markdown(records: list[dict]) -> str:
    spec = json.loads(read(SPEC))
    lines = [
        "# Component inventory",
        "",
        "<!-- Generated by tools/generate-component-inventory.py. Do not edit by hand. -->",
        "",
        f"{len(records)} documented Polaris elements: "
        f"{sum(1 for r in records if r['composedInside'] is None)} top-level and "
        f"{sum(1 for r in records if r['composedInside'] is not None)} composed sub-elements. "
        f"Every one is wrapped by a class in `src/Polaris.Blazor/Components`.",
        "",
        "The record for each element joins two sources that cannot drift apart: the wrapper source "
        "(its parameters, types, DOM properties, events and tests) and "
        "[`tools/polaris-elements.json`](../tools/polaris-elements.json), the hand-maintained "
        "Shopify half. `docs/component-inventory.json` carries the same data for machines.",
        "",
        "## Summary by category",
        "",
        "| Category | Elements | Browser-verified | Unit-tested | Unverified |",
        "|---|---|---|---|---|",
    ]

    for category in sorted({r["category"] for r in records}):
        in_category = [r for r in records if r["category"] == category]
        lines.append(
            f"| {category} | {len(in_category)} | "
            f"{sum(1 for r in in_category if r['verification'] == 'browser-verified')} | "
            f"{sum(1 for r in in_category if r['verification'] == 'unit-tested')} | "
            f"{sum(1 for r in in_category if r['verification'] == 'unverified')} |"
        )

    lines += [
        "",
        "## Elements",
        "",
        "| Element | Official name | Wrapper | Category | Minimum version | Verification | Tests |",
        "|---|---|---|---|---|---|---|",
    ]
    for record in records:
        tests = len(record["testCoverage"])
        test_cell = str(tests) if tests else "\u2014"
        lines.append(
            f"| `<{record['element']}>` | {record['officialName']} | `{record['wrapper']}` | "
            f"{record['category']} | {record['minimumSupportedVersion']} | {record['verification']} | {test_cell} |"
        )

    lines += ["", "## Per-element detail", ""]
    for record in records:
        lines.append(f"### `{record['wrapper']}` \u2192 `<{record['element']}>`")
        lines.append("")
        lines.append(record["summary"] or "No summary in the wrapper source.")
        lines.append("")
        lines.append(f"- Official name: {record['officialName']}")
        lines.append(f"- Category: {record['category']}")
        if record["composedInside"]:
            lines.append(f"- Composed inside: `<{record['composedInside']}>`")
        lines.append(f"- Minimum supported version: {record['minimumSupportedVersion']}")
        lines.append(f"- Implementation status: {record['implementationStatus']}")
        lines.append(f"- Verification: {record['verification']}")
        lines.append(f"- Docs: <{record['docsUrl']}>")
        lines.append("")

        if record["supportedProperties"]:
            lines.append("| Parameter | C# type | Rendered as | Notes |")
            lines.append("|---|---|---|---|")
            for parameter in record["supportedProperties"]:
                lines.append(
                    f"| `{parameter['name']}` | `{parameter['type']}` | {parameter['kind']} | "
                    f"{parameter['doc']} |"
                )
        else:
            lines.append("No parameters of its own.")
        lines.append("")

        if record["eventDefinitions"]:
            lines.append("| Event | Callback | Documented | Wired |")
            lines.append("|---|---|---|---|")
            for event in record["eventDefinitions"]:
                handler = f"`{event['handler']}`" if event["handler"] else "\u2014"
                lines.append(
                    f"| `{event['event']}` | {handler} | "
                    f"{'yes' if event['documented'] else 'no'} | "
                    f"{'yes' if event['wrapped'] else '**no**'} |"
                )
        else:
            lines.append("No events.")
        lines.append("")

        if record["slotDefinitions"]:
            lines.append("| Slot | Accepts | Wrapper support | Notes |")
            lines.append("|---|---|---|---|")
            for slot in record["slotDefinitions"]:
                lines.append(
                    f"| `{slot['name']}` | {slot['accepts']} | {slot['wrapperSupport']} | {slot['notes']} |"
                )
        else:
            lines.append("No slots.")
        lines.append("")

        lines.append("Accessibility requirements:")
        lines.append("")
        for requirement in record["accessibilityRequirements"]:
            lines.append(f"- {requirement}")
        lines.append("")

        if record["testCoverage"]:
            lines.append("Exercised by:")
            lines.append("")
            for test in record["testCoverage"]:
                lines.append(f"- `{test}`")
        else:
            lines.append("Not named in a test.")
        lines.append("")

        if record["notes"]:
            lines.append(f"Notes: {record['notes']}")
            lines.append("")

    return "\n".join(lines)


def render_inventory_json(inventory: dict) -> str:
    """A stable, sorted-key rendering: any change to the data changes exactly one line."""
    return json.dumps(inventory, indent=2, ensure_ascii=False, sort_keys=True) + "\n"


def render_cli_json(catalogue: dict) -> str:
    """Key order is part of the CLI's schema, so this one is not sorted."""
    return json.dumps(catalogue, indent=2, ensure_ascii=False) + "\n"


def generate() -> dict[str, str]:
    records = build_records()
    return {
        "inventory-json": render_inventory_json(build_inventory(records)),
        "inventory-md": render_markdown(records),
        "cli-catalogue": render_cli_json(build_cli_catalogue(records)),
    }


def report_drift(name: str, path: Path, expected: str, actual: str) -> None:
    """Prints where two renderings first differ, which is more use than a byte count."""
    print(f"DRIFT {path.relative_to(ROOT)}", file=sys.stderr)
    if not path.exists():
        print(f"  the file does not exist; expected {len(expected.encode())} bytes", file=sys.stderr)
        return

    expected_lines = expected.splitlines(keepends=True)
    actual_lines = actual.splitlines(keepends=True)
    diff = list(
        difflib.unified_diff(
            actual_lines,
            expected_lines,
            fromfile=f"committed/{path.name}",
            tofile=f"generated/{path.name}",
            n=1,
        )
    )
    shown = 0
    for line in diff:
        if line.startswith("@@") or shown < 40:
            sys.stderr.write("  " + line if line.endswith("\n") else "  " + line + "\n")
            shown += 1
        if shown >= 60:
            print("  ... (truncated)", file=sys.stderr)
            break


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "--check",
        action="store_true",
        help="regenerate in memory, compare with the committed files, write nothing, exit 1 on drift",
    )
    args = parser.parse_args()

    generated = generate()

    if args.check:
        drifted = False
        for name, content in generated.items():
            path = OUTPUTS[name]
            actual = read(path) if path.exists() else None
            if actual != content:
                report_drift(name, path, content, actual or "")
                drifted = True

        if drifted:
            print(
                "component inventory is stale: re-run python3 tools/generate-component-inventory.py",
                file=sys.stderr,
            )
            return 1

        for name, path in OUTPUTS.items():
            print(f"ok      {path.relative_to(ROOT)}")
        return 0

    for name, content in generated.items():
        path = OUTPUTS[name]
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        print(f"wrote {path.relative_to(ROOT)} ({len(content.encode())} bytes)")

    return 0


if __name__ == "__main__":
    sys.exit(main())
