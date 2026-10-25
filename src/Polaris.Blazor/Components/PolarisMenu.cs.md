<!-- summary-of: PolarisMenu.cs -->
<!-- source-sha256: ef7c8d5d5eca -->

# PolarisMenu.cs

A list of actions that apply to one resource, rendered as `<s-menu>`. It has no trigger of its own:
you give it an `Id` and point a `PolarisButton.CommandFor` at it, and Polaris opens it with no
JavaScript.

`Polaris.Blazor.Components` · renders `<s-menu>` · base `PolarisSlottedComponentBase`

## Parameters

None of its own — the element is otherwise configured entirely by its children, and the id that makes
it openable is the inherited one:

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Id` | `string?` (inherited) | `null` | The name a trigger addresses with `CommandFor`. Without it the menu cannot be opened at all. |

Inherited from `PolarisSlottedComponentBase` / `PolarisContentComponentBase`: `ChildContent` (the menu
items), plus `SupplementalStart` and `Aside`, which render into the `supplemental-start` and `aside`
slots of elements that have them. The menu's own children are meant to be buttons and sections, so
only `ChildContent` is normally used. `AccessibilityLabel`, `ItemTag`, `Slot` and
`AdditionalAttributes` come from the element base — see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **The trigger is external and declarative.** A menu is opened by a sibling button, not by itself: a
  `PolarisButton` with `CommandFor` set to the menu's `Id`. With the default `Command` (`Auto`, the
  `--auto` value) the button toggles the menu. No interop, no handler and no `Open` parameter are
  involved.
- **Only two child types are accepted**: `PolarisButton` for an action and `PolarisSection` to group
  actions under a heading. Everything else — a `div`, a custom component, a paragraph of help text —
  is not part of the menu.
- **There are no submenus.** A menu is one flat level of actions; anything hierarchical has to become
  a page or a modal.
- **A menu is for actions, not navigation.** The element itself holds no state: which action was
  pressed is reported by each child button's own `OnClick`.

## Gotchas

- **A menu with no `Id` is dead markup.** Nothing generates an id for it (`RequiresId` is not
  overridden here), so the only way to open it is a trigger that names an id you supplied. It renders
  correctly and simply never appears, with no warning.
- **The `Id` must be unique on the page.** Two menus sharing one id means the trigger opens whichever
  the browser resolves first — usually the earlier one.
- **A child's `Slot` is not the way in.** Menu items are ordinary children, not slotted content, so a
  `Slot` on a button inside a menu is not how the item is placed.
- **A long menu stops being scannable.** Beyond roughly a dozen items the list is hard to read; the
  source's own advice is a page of its own instead.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — the trigger (`CommandFor`, `Command`) and the only child
  type besides a section.
- [PolarisSection.cs.md](PolarisSection.cs.md) — groups menu actions under a heading.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — `Id`, slots and the
  inherited parameters.
