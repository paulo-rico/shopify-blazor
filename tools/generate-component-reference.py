#!/usr/bin/env python3
"""Generates docs/components.md from the component sources.

The table is derived, not maintained by hand: an attribute that exists in code but not in the
docs is a documentation bug, and one that exists in the docs but not in code is worse. Run this
whenever a wrapper changes.

Usage: python3 tools/generate-component-reference.py
"""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
COMPONENTS = ROOT / "src" / "Polaris.Blazor" / "Components"
OUTPUT = ROOT / "docs" / "components.md"


def clean(block: str) -> str:
    """
    Turns a doc-comment block into one readable line.

    Doc comments contain XML tags (`<summary>`, `<see cref="…"/>`, `<para>`) which have no place
    in a Markdown table. `<see cref="X"/>` is rendered as `X`, because that is what a reader
    needs, and every other tag is dropped.
    """
    text = "\n".join(line.strip().lstrip("/").strip() for line in block.splitlines())

    # Doc comments escape angle brackets so the compiler does not read them as XML, so a tag name
    # arrives as `&lt;s-button&gt;`. Unescape first, or the Markdown shows the escape instead of
    # the element name.
    text = text.replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")

    text = re.sub(r'<see\s+cref="(?:\w+:)?([^"]+)"\s*/>', r"`\1`", text)
    text = re.sub(r"</?(?:summary|para|remarks|list|item|description|code|c)\s*/?>", " ", text)
    # Backticks around a bare tag name read as a code span, which is what a component name is.
    text = re.sub(r"<(s-[a-z-]+)>", r"`<\1>`", text)
    # A bare tag name such as `<s-button>` is just text and is kept; only self-closing and
    # tag-with-attribute forms count as markup here.
    text = re.sub(r"</[A-Za-z][^>]*>", " ", text)
    text = re.sub(r"<[A-Za-z][\w.-]*\s+[^>]*?/>", " ", text)
    text = re.sub(r"<[A-Za-z][\w.-]*\s+[^>]*>", " ", text)
    text = " ".join(text.split())
    # Tidy the space the tag stripping leaves before punctuation.
    text = re.sub(r"\s+([.,;:!?])", r"\1", text)

    # Keep the first sentence: the full summary belongs in the source, not repeated in a table.
    for end in (". ", "! ", "? "):
        index = text.find(end)
        if index != -1:
            return text[: index + 1]

    return text


def parse(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")

    # One class wraps one element, which is what lets a wrapper be joined to the spec by tag. A class
    # whose TagName switched on a parameter would read as "?" here, which is deliberate: it has no
    # single element to be indexed under, and silently reporting one of the two would hide the other.
    tag = re.search(r'TagName\s*=>\s*"([^"]+)"', text)
    tag_name = tag.group(1) if tag else "?"

    # Everything up to the first closing tag: a per-parameter summary later in the file must not
    # be mistaken for the type's own summary.
    # Only the first block: every parameter has its own `<summary>`, and a non-greedy match still
    # ends at the first closing tag, so the type's own summary is the one that is read.
    summary = re.search(r"/// <summary>\n(.*?)\n\s*/// </summary>", text, re.DOTALL)
    doc = clean(summary.group(1)) if summary else ""

    # If that picked up a parameter block, fall back to the comment block directly above the type.
    type_declaration = re.search(r"\n((?:\s*///[^\n]*\n)+)\s*(?:public|internal)\s+(?:sealed\s+)?class\s", text)
    if type_declaration:
        doc = clean(type_declaration.group(1))

    # Parameters, with the preceding doc comment when there is one.
    #
    # The attribute list is matched loosely: `[Parameter]`, `[Parameter(CaptureUnmatchedValues = true)]`
    # and `[Parameter, EditorRequired]` are all parameters, and a further attribute line between the
    # list and the property must not hide it — an `[EditorRequired]` written that way made a
    # documented parameter an undocumented one, silently, because the table is generated.
    parameters = []
    pattern = re.compile(
        r"((?:\s*///[^\n]*\n)*)\s*\[Parameter\b[^\]]*\]\s*(?:\[[^\]\n]*\]\s*)*public\s+([\w<>?\[\], .]+?)\s+(\w+)\s*\{\s*get;\s*set;\s*\}",
        re.MULTILINE,
    )
    for match in pattern.finditer(text):
        comment, type_name, name = match.groups()
        parameters.append((name, type_name.strip(), clean(comment)))

    events = sorted(set(re.findall(r"events\.On\(PolarisEvents\.(\w+)", text)))
    events += sorted(set(re.findall(r'events\.On\("([a-z]+)"', text)))

    # Status is derived, not asserted: a component counts as covered when a test names it.
    tests = (ROOT / "tests" / "Polaris.Blazor.Tests").glob("*.cs")
    covered = any(path.stem in test.read_text(encoding="utf-8") for test in tests)

    # A static class is a helper, not a component, and it has none of what this reference documents: no
    # element, no parameters, no events. The library keeps its shared number formatting beside the
    # controls that use it (`PolarisNumberText`), so the glob picks it up — and documenting it would
    # claim a control that renders nothing. Skipped here rather than by renaming the file, because a
    # name that dodges the glob is a trap for the next helper.
    if re.search(r"\bstatic\s+class\s+" + re.escape(path.stem) + r"\b", text):
        return None

    return {
        "name": path.stem,
        "tag": tag_name,
        "doc": doc,
        "parameters": parameters,
        "events": sorted(set(events)),
        "covered": covered,
    }


def main() -> None:
    components = sorted(
        (parsed for path in COMPONENTS.glob("Polaris*.cs") if (parsed := parse(path)) is not None),
        key=lambda c: c["name"],
    )

    # A class with no single literal tag is composed rather than a wrapper: Polaris has no element
    # behind it, and it is built from components that do. Only the accordion is like this today, and
    # the two kinds are counted and printed separately so that neither claim ("wraps an element" /
    # "is made of them") is quietly stretched over the other.
    wrappers = [c for c in components if c["tag"] != "?"]
    composed = [c for c in components if c["tag"] == "?"]

    lines = [
        "# Polaris.Blazor component reference",
        "",
        "<!-- Generated by tools/generate-component-reference.py. Do not edit by hand. -->",
        "",
        f"{len(wrappers)} components wrap Shopify's Polaris web components. Each one renders the",
        "element Shopify defined; none of them reimplements appearance, because Polaris components",
        "live in a shadow root and cannot be restyled.",
        "",
    ]

    if composed:
        lines += [
            f"{len(composed)} more are composed from those wrappers, for behaviour Polaris does not",
            "provide in an element of its own. They add structure and interaction; they do not add",
            "appearance either, because everything visible in them is still a Polaris element.",
            "",
        ]

    lines += [
        "## Inventory",
        "",
        "| Component | Element | Events forwarded | Named in tests |",
        "|---|---|---|---|",
    ]

    for component in components:
        events = ", ".join(f"`{e}`" for e in component["events"]) or "—"
        covered = "yes" if component["covered"] else "**no**"
        element = f"`<{component['tag']}>`" if component["tag"] != "?" else "— composed"
        lines.append(f"| `{component['name']}` | {element} | {events} | {covered} |")

    lines += [
        "",
        "Every component also accepts the shared parameters in the section below, plus the `Slot`",
        "parameter described under composition.",
        "",
        "## Shared parameters",
        "",
        "| Parameter | Type | Purpose |",
        "|---|---|---|",
        "| `Id` | `string?` | Stable element id. Required for `CommandFor`/`InterestFor` targeting. |",
        "| `Slot` | `string?` | Places the element in a parent's named slot. See composition below. |",
        "| `AccessibilityLabel` | `string?` | Replaces the accessible name. Required for icon-only controls. |",
        "| `ItemTag` | `string?` | Opaque value carried on every event, so one handler can serve many instances. |",
        "| `AdditionalAttributes` | `IReadOnlyDictionary<string, object>?` | Forwards any Polaris attribute not modelled here. |",
        "",
        "## Composition and slots",
        "",
        "Polaris reads slots from the light DOM and **validates the children**: a `primary-action`",
        "slot accepts a button and rejects anything else, including a `div` that merely wraps one.",
        "A `RenderFragment` cannot carry an attribute, so slots are set on the child component:",
        "",
        "```razor",
        "<PolarisModal Id=\"save-modal\" Heading=\"Save changes\">",
        "    <PolarisText>This cannot be undone.</PolarisText>",
        "",
        "    <PolarisButton Slot=\"primary-action\"",
        "                   CommandFor=\"save-modal\"",
        "                   Command=\"PolarisCommand.Hide\"",
        "                   Variant=\"PolarisButtonVariant.Primary\">",
        "        Save",
        "    </PolarisButton>",
        "</PolarisModal>",
        "```",
        "",
        "Two slots are exempt because Shopify does not type-check them — `supplemental-start` and",
        "`aside` on `PolarisPage`, `PolarisEmptyState` and `PolarisBanner` — and those accept a",
        "fragment parameter.",
        "",
        "## Boolean attributes",
        "",
        "Polaris booleans are *presence* attributes: `disabled=\"false\"` is still `true` to a custom",
        "element. Every wrapper therefore renders `true` as a bare attribute and omits `false`",
        "entirely.",
        "",
        "## Values that are properties, not attributes",
        "",
        "Three things cannot be expressed reliably as attributes, so the wrappers set them as DOM",
        "properties after the element upgrades:",
        "",
        "| Property | Why |",
        "|---|---|",
        "| `value` | Not reflected by Polaris, so a server-side change needs the property. |",
        "| `checked` | Not reflected, and attribute presence cannot express `false`. |",
        "| `selected`, `indeterminate` | Not reflected. |",
        "",
        "`value` is also rendered as `defaultValue` so the first paint is correct before the element",
        "upgrades — important in a server-rendered app, where markup arrives before JavaScript.",
        "",
        "## Events",
        "",
        "Events are DOM events forwarded to .NET through a small interop module",
        "(`/_content/Polaris.Blazor/polaris-interop.js`). A listener is attached only for callbacks",
        "you actually supply, because each one costs a JavaScript listener and, on Blazor Server, a",
        "circuit round trip per occurrence.",
        "",
        "```razor",
        "<PolarisButton OnClick=\"SaveAsync\">Save</PolarisButton>",
        "",
        "@code {",
        "    private async Task SaveAsync(PolarisEventArgs args)",
        "    {",
        "        // args.Value, args.Checked, args.Values, args.ShiftKey, args.Detail …",
        "    }",
        "}",
        "```",
        "",
        "The handler runs on the server for a Blazor Server app, so it can call services directly.",
        "Values arrive as strings, because that is what the form components emit.",
        "",
        "## Per-component parameters",
        "",
    ]

    for component in components:
        heading = (
            f"### `{component['name']}` → `<{component['tag']}>`"
            if component["tag"] != "?"
            else f"### `{component['name']}` (composed)"
        )
        lines.append(heading)
        lines.append("")
        if component["doc"]:
            lines.append(component["doc"])
            lines.append("")

        if component["parameters"]:
            lines.append("| Parameter | Type | Notes |")
            lines.append("|---|---|---|")
            for name, type_name, comment in component["parameters"]:
                lines.append(f"| `{name}` | `{type_name}` | {comment} |")
        else:
            lines.append("No parameters of its own; the element is a plain wrapper.")

        lines.append("")

    OUTPUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"wrote {OUTPUT.relative_to(ROOT)} with {len(components)} components")


if __name__ == "__main__":
    main()
