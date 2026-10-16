#!/usr/bin/env python3
"""Emits a C# wrapper for one element from a spec entry.

The wrapper pattern is uniform enough that most of a new component is a template fill, and this
script does exactly that much and no more. It writes the class, `TagName`, the `[Parameter]`
declarations, the `BuildAttributes` assignments, and the `events.On(...)` registrations for events
that map to a `PolarisEvents` constant.

Everything the pattern does *not* answer becomes an explicit `// TODO(polaris):` block in the
generated file, and the tool exits non-zero until `--allow-todos` is passed. Those are the parts the
plan says need a human:

  * a DOM property — a value Polaris does not reflect, which has to be assigned after upgrade
  * an array property — the `s-choice-list.values` class of bug, where an attribute silently
    selects nothing and a stale value keeps state the server has dropped
  * slot wiring — Polaris validates slot children, so the slot belongs on the child component and
    cannot be a `RenderFragment`
  * an event with no `PolarisEvents` constant — the spelling would be a guess, and a wrong event
    name is a listener that never fires

Generating those silently would produce plausible code that looks right and does the wrong thing,
which is worse than not generating at all. The stdout summary says what is left to do.

Usage:
  python3 tools/generate-component.py --element s-banner
  python3 tools/generate-component.py --element s-banner --out /tmp/wrappers --allow-todos
  python3 tools/generate-component.py --spec path/to/spec.json --element My-Thing --out . --force
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SPEC = ROOT / "tools" / "polaris-elements.json"
DEFAULT_OUT = ROOT / "src" / "Polaris.Blazor" / "Components"
COMPONENTS = ROOT / "src" / "Polaris.Blazor" / "Components"
EVENTS_FILE = ROOT / "src" / "Polaris.Blazor" / "Events" / "PolarisEventArgs.cs"

TODO_MARKER = "// TODO(polaris):"

# The `s-` prefix every Polaris custom element shares; a wrapper is named after the element without
# it, so `s-button-group` becomes `PolarisButtonGroup`.
ELEMENT_PREFIX = "s-"

# Types whose attribute value comes from `PolarisAttributeValue.Format`, which is what makes a
# number culture-invariant and a boolean a presence attribute.
FORMATTED_TYPES = frozenset(
    {"bool", "bool?", "int", "int?", "long", "long?", "double", "double?", "float", "float?", "decimal", "decimal?"}
)

# Types that are plainly an array or list and therefore cannot be an attribute.
ARRAY_TYPES = ("[]", "IReadOnlyList<", "IList<", "List<", "IEnumerable<", "IReadOnlyCollection<")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def load_spec(path: Path) -> dict:
    return json.loads(read(path))


def pascal_case(slug: str) -> str:
    """`button-group` → `ButtonGroup`. The wrapper class name is mechanical from the element."""
    words = re.split(r"[^A-Za-z0-9]+", slug)
    return "".join(word[:1].upper() + word[1:] for word in words if word)


def xml_escape(text: str) -> str:
    """
    Makes a summary safe inside a doc comment.

    A doc comment is compiled as XML, so a bare `<s-button>` fails the build with a confusing
    "XML comment has badly formed XML" error. The reference generator's `clean` unescapes exactly
    these entities on the way back out, so the round trip is stable.
    """
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def wrapper_name(element: str) -> str:
    slug = element[len(ELEMENT_PREFIX):] if element.startswith(ELEMENT_PREFIX) else element
    return "Polaris" + pascal_case(slug)


def event_constants() -> dict[str, str]:
    """
    The DOM event name → `PolarisEvents` member mapping, read from the constants file.

    Read rather than duplicated so the generator cannot emit a constant that does not exist. A name
    that has no constant is a TODO, not a guess.
    """
    text = read(EVENTS_FILE)
    constants = re.findall(r'public const string (\w+)\s*=\s*"([^"]+)"', text)
    return {value: name for name, value in constants}


def known_enum_types() -> set[str]:
    """
    The `Polaris*` enums declared in the library.

    A parameter whose type is one of these renders through `PolarisAttributeValue.EnumValue`, which
    is what keeps Shopify's spelling rather than the .NET member name out of the markup. The set is
    read from source so a new enum is picked up without editing this script.
    """
    types: set[str] = set()
    for path in sorted((ROOT / "src" / "Polaris.Blazor").glob("Polaris*.cs")):
        for name in re.findall(r"public enum (\w+)", read(path)):
            types.add(name)
    return types


def attribute_expression(parameter: dict, enums: set[str]) -> str:
    """
    The right-hand side of one `attributes[...] = ...;` line.

    The three shapes are the whole reason a wrapper is more than a passthrough: an enum has to lose
    its .NET member name, a boolean has to be presence rather than `"false"`, and everything else is
    the value as written.
    """
    name = parameter["name"]
    type_name = parameter["type"]
    stripped = type_name.rstrip("?")

    if stripped in enums:
        if type_name.endswith("?"):
            # A nullable enum needs the null check; otherwise "not set" collapses into the enum's
            # zero member, which is usually `auto` and therefore a different instruction.
            return (
                f"{name} is null ? null : PolarisAttributeValue.EnumValue({name}.Value)"
            )
        return f"PolarisAttributeValue.EnumValue({name})"

    if type_name in FORMATTED_TYPES:
        return f"PolarisAttributeValue.Format({name})"

    # A string, or an opaque value the browser can read as-is.
    return name


def problems_for(entry: dict, enums: set[str], constants: dict[str, str]) -> list[str]:
    """
    Everything about this entry that is not mechanical, as one line each.

    The list is what the TODO block in the generated file and the stdout summary both render, so a
    human reads the same statement in the file they are editing and in the terminal. One line per
    distinct problem: an array that is also a DOM property is one thing to fix, not two.
    """
    problems: list[str] = []

    for parameter in entry["parameters"]:
        kind = parameter["kind"]
        is_array = any(marker in parameter["type"] for marker in ARRAY_TYPES)
        if kind == "dom property" and is_array:
            problems.append(
                f"`{parameter['name']}` is an array DOM property ({parameter['type']}): no attribute "
                f"can carry it, and Polaris does not reflect it, so set it after upgrade through the "
                f"`Properties` override, null the attribute in `BuildAttributes`, and clear it when it "
                f"goes back to null or a stale value keeps state the server has dropped."
            )
        elif kind == "dom property":
            problems.append(
                f"`{parameter['name']}` is a DOM property, not an attribute: Polaris does not reflect it, "
                f"so assign it after upgrade through the `Properties` override (and null it in "
                f"`BuildAttributes` so no stale attribute survives a re-render)."
            )
        elif is_array:
            problems.append(
                f"`{parameter['name']}` is an array ({parameter['type']}) but is listed as an "
                f"attribute, which cannot carry it. Set it as a property and clear it when it goes "
                f"back to null."
            )

    for slot in entry["slots"]:
        if slot["name"] == "children":
            continue
        problems.append(
            f"slot `{slot['name']}` takes {slot['accepts']} and has to be wired by hand: Polaris "
            f"validates slot children, so the `slot` attribute belongs on the child component, not "
            f"on a wrapping element."
        )

    for event in entry["events"]:
        if event not in constants:
            problems.append(
                f"event `{event}` has no `PolarisEvents` constant, so its spelling would be a guess. "
                f"Add a constant to `Events/PolarisEventArgs.cs` and register it."
            )

    return problems


# The spec vocabulary and this tool's vocabulary differ on three field names, because the inventory
# uses the plan's names (`supportedProperties`, `eventDefinitions`, `slotDefinitions`) while a
# from-scratch spec reads better with the plain ones. Both are accepted.
FIELD_ALIASES = {
    "parameters": ("parameters", "supportedProperties"),
    "events": ("events", "eventDefinitions"),
    "slots": ("slots", "slotDefinitions"),
    "minVersion": ("minVersion", "minimumSupportedVersion", "minPolarisVersion"),
}


def normalise_entry(entry: dict) -> dict:
    """
    One spec entry with this tool's field names.

    Event definitions may be plain event names or the inventory's objects, so both are flattened to
    the names: the generator only needs to know which events exist and whether a constant maps them.
    """
    normalised = dict(entry)
    # The inventory calls the base class `wrapperBase`; this tool only needs to inherit from it.
    if "base" not in normalised and entry.get("wrapperBase"):
        normalised["base"] = entry["wrapperBase"]
    for canonical, aliases in FIELD_ALIASES.items():
        for alias in aliases:
            if alias in entry:
                normalised[canonical] = entry[alias]
                break
    normalised["parameters"] = [
        {
            "name": parameter["name"],
            "type": parameter["type"],
            "kind": parameter.get("kind", "attribute"),
            # The inventory and the reference generator both call the prose `doc`; a hand-written
            # spec entry tends to call it `docs`. Accept either.
            "doc": parameter.get("doc") or parameter.get("docs") or "",
            "default": parameter.get("default"),
        }
        for parameter in normalised.get("parameters", [])
    ]
    normalised["events"] = [
        event["event"] if isinstance(event, dict) else event for event in normalised.get("events", [])
    ]
    normalised["slots"] = [
        {"name": slot, "accepts": "HTMLElement", "notes": ""} if isinstance(slot, str) else slot
        for slot in normalised.get("slots", [])
    ]
    return normalised


def render(entry: dict) -> tuple[str, list[str]]:
    """The C# file body, plus the list of things a human still has to do."""
    entry = normalise_entry(entry)
    enums = known_enum_types()
    constants = event_constants()
    problems = problems_for(entry, enums, constants)

    element = entry["element"]
    name = entry.get("wrapper") or wrapper_name(element)
    base = entry.get("base") or "PolarisElementComponentBase"
    namespace = entry.get("namespace") or "Polaris.Blazor.Components"
    summary = entry.get("summary") or f"Wraps <c>&lt;{element}&gt;</c>."

    parameters = entry["parameters"]
    callbacks = [p for p in parameters if "EventCallback" in p["type"]]
    plain = [p for p in parameters if "EventCallback" not in p["type"]]
    registered = [e for e in entry["events"] if e in constants]
    unwired = [e for e in entry["events"] if e not in constants]
    # A callback the spec declares is a subscription the caller can make; if no event of that name
    # is listed, the wiring would be invented rather than derived. Compared case-insensitively
    # because a spec may spell the event `nextPage` where the DOM spells it `nextpage`.
    declared_events = {_handler_event(p["name"]) for p in callbacks}
    listed_events = {event.lower() for event in entry["events"]}
    for handler in sorted(declared_events - listed_events):
        problems.append(
            f"callback `On{pascal_case(handler)}` has no matching entry in the spec's `events`, so "
            f"it would never be registered."
        )

    lines: list[str] = []
    lines.append("using Microsoft.AspNetCore.Components;")
    if callbacks:
        lines.append("using Polaris.Blazor.Events;")
    lines.append("")
    lines.append(f"namespace {namespace};")
    lines.append("")
    lines.append(f"/// <summary>{summary}</summary>")
    lines.append(f"public sealed class {name} : {base}")
    lines.append("{")

    # TagName is not a parameter, so it is always emitted: a generated class wraps exactly one
    # element. A component whose tag changed on a parameter could not be catalogued either — the
    # inventory indexes a wrapper by its tag — so such a component is two components, not one.
    lines.append("    /// <inheritdoc />")
    lines.append(f'    protected override string TagName => "{element}";')
    lines.append("")

    for parameter in parameters:
        doc = parameter.get("doc") or f"TODO: document `{parameter['name']}`."
        if not parameter.get("doc"):
            problems.append(f"`{parameter['name']}` has no doc comment in the spec; the reference generator reads it.")
        # The doc comment is XML, so a generic type or an angle-bracketed element name would break
        # the build. Shopify's own element names arrive already escaped in the source.
        lines.append(f"    /// <summary>{xml_escape(doc)}</summary>")
        default = f" = {parameter['default']};" if parameter.get("default") else ""
        lines.append(
            f"    [Parameter] public {parameter['type']} {parameter['name']} {{ get; set; }}{default}"
        )
        lines.append("")

    if callbacks or registered:
        lines.append("    /// <inheritdoc />")
        lines.append("    protected override void ConfigureEvents(PolarisEventRegistration events)")
        lines.append("    {")
        for event in registered:
            handler = next((p["name"] for p in callbacks if _handler_event(p["name"]) == event), None)
            if handler is None:
                problems.append(
                    f"event `{event}` is registrable but has no callback parameter in the spec, so "
                    f"nothing can subscribe to it."
                )
                continue
            lines.append(f"        events.On(PolarisEvents.{constants[event]}, {handler});")
        for event in unwired:
            lines.append(f"        // {TODO_MARKER} register `{event}` once a PolarisEvents constant exists.")
        lines.append("    }")
        lines.append("")

    lines.append("    /// <inheritdoc />")
    lines.append("    protected override void BuildAttributes(Dictionary<string, string?> attributes)")
    lines.append("    {")
    lines.append("        base.BuildAttributes(attributes);")
    lines.append("")
    for parameter in plain:
        key = parameter["name"][0].lower() + parameter["name"][1:]
        if parameter["kind"] == "dom property":
            # The attribute must be nulled, not written: a stale `value` or `checked` attribute
            # outlives the property assignment on the next render.
            lines.append(f'        attributes["{key}"] = null;')
        else:
            lines.append(f'        attributes["{key}"] = {attribute_expression(parameter, enums)};')
    if not plain:
        # An empty override is otherwise a compile warning about an unused parameter, and an
        # override that writes nothing still belongs in the file so a human has somewhere to add
        # the first attribute.
        lines.append("        _ = attributes;")
    lines.append("    }")
    lines.append("}")

    body = "\n".join(lines) + "\n"
    if problems:
        body = render_todo_block(problems, element, name) + body
    return body, problems


def _handler_event(handler: str) -> str:
    """`OnAfterHide` → `afterhide`; the same mapping the inventory uses, kept in one place."""
    return handler[2:].lower()


def render_todo_block(problems: list[str], element: str, name: str) -> str:
    """
    The impossible-to-miss header block.

    Deliberately a block rather than scattered comments: it has to survive being pasted into a file
    and read at the top, and `grep -c 'TODO(polaris)'` has to be a usable check.
    """
    lines = [
        "// =====================================================================================",
        f"{TODO_MARKER} {name} ({element}) is NOT finished.",
        "// The generated parts are the mechanical ones only. A human must resolve each of the",
        "// following, and delete this block when done:",
        "//",
    ]
    for problem in problems:
        lines.append(f"//   * {problem}")
    lines += [
        "//",
        "// The plan (docs/templates-plan.md, Phase 6) is explicit that these parts are not generated",
        "// because plausible-looking code here silently does the wrong thing.",
        "// =====================================================================================",
        "",
    ]
    return "\n".join(lines)


def find_entry(spec: dict, element: str) -> dict | None:
    wanted = element if element.startswith(ELEMENT_PREFIX) else ELEMENT_PREFIX + element
    for entry in spec["elements"]:
        # Match on the element, or on the wrapper a reader is more likely to name.
        if entry["element"] == wanted or wrapper_name(entry["element"]).lower() == element.lower():
            return entry
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--element", help="element to generate, e.g. s-banner or PolarisBanner")
    parser.add_argument(
        "--spec",
        type=Path,
        default=SPEC,
        help=(
            "spec file to read. The default, tools/polaris-elements.json, holds only the Shopify "
            "facts, so it describes no wrapper — pass docs/component-inventory.json to generate "
            f"from the joined catalogue, or a hand-written entry file. (default: {SPEC.relative_to(ROOT)})"
        ),
    )
    parser.add_argument(
        "--from-inventory",
        action="store_true",
        help="shortcut for --spec docs/component-inventory.json, generating from the joined catalogue",
    )
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="directory to write into")
    parser.add_argument("--force", action="store_true", help="overwrite an existing wrapper file")
    parser.add_argument(
        "--allow-todos",
        action="store_true",
        help="write the file even though a human still has work to do; the summary is still printed",
    )
    parser.add_argument("--list", action="store_true", help="list the elements in the spec and exit")
    args = parser.parse_args()

    if args.from_inventory:
        args.spec = ROOT / "docs" / "component-inventory.json"

    if not args.spec.exists():
        print(f"spec not found: {args.spec}", file=sys.stderr)
        return 2

    spec = load_spec(args.spec)
    if "elements" not in spec:
        # The inventory nests its records under `components`; both shapes are a list of entries.
        spec = {**spec, "elements": spec.get("components", [])}

    if args.list:
        for entry in spec["elements"]:
            print(f"{entry['element']:<20} {entry.get('wrapper') or wrapper_name(entry['element'])}")
        return 0

    if not args.element:
        parser.error("--element is required (or use --list)")

    entry = find_entry(spec, args.element)
    if entry is None:
        print(f"no spec entry for '{args.element}'. Known elements:", file=sys.stderr)
        for known in spec["elements"]:
            print(f"  {known['element']}", file=sys.stderr)
        return 2

    body, problems = render(entry)
    name = entry.get("wrapper") or wrapper_name(entry["element"])
    target = args.out / f"{name}.cs"

    if target.exists() and not args.force:
        # Refusing is the point: a wrapper is hand-finished after generation, and overwriting it
        # would discard exactly the work the TODOs were asking for.
        print(f"refusing to overwrite {target} without --force", file=sys.stderr)
        return 1

    if problems and not args.allow_todos:
        print(f"{name}: {len(problems)} thing(s) a human must do before this wrapper is finished:", file=sys.stderr)
        for problem in problems:
            print(f"  - {problem}", file=sys.stderr)
        print(
            "\nnothing was written. Resolve them in the spec, or pass --allow-todos to write the file "
            "with the TODO block in place.",
            file=sys.stderr,
        )
        return 1

    args.out.mkdir(parents=True, exist_ok=True)
    target.write_text(body, encoding="utf-8")
    print(f"wrote {target}")

    if problems:
        print(f"{name}: {len(problems)} thing(s) a human must still do (marked {TODO_MARKER} in the file):")
        for problem in problems:
            print(f"  - {problem}")
    else:
        print(f"{name}: generated mechanically end to end; nothing left to do.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
