<!-- summary-of: PolarisSlottedComponentBase.cs -->
<!-- source-sha256: e2b7716b05be -->

# PolarisSlottedComponentBase.cs

Base class for a wrapper whose element accepts children in Shopify's named slots. It exposes the two
slots Polaris does not type-check — `supplemental-start` and `aside` — as fragments around the ordinary
`ChildContent`, and leaves every validated slot to the child component's `Slot` parameter.

`Polaris.Blazor` · abstract base · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `SupplementalStart` | `RenderFragment?` | `null` | Content for the `supplemental-start` slot, rendered before the main content. Not validated by Polaris, which is why a banner or an informational block belongs here. `null` renders no slot element at all. |
| `Aside` | `RenderFragment?` | `null` | Content for the `aside` column, where the element has one. Rendered after the main content. Also not validated. |

Inherited: `ChildContent` (rendered between the two slots) plus `Id`, `AccessibilityLabel`, `ItemTag`,
`Slot` and `AdditionalAttributes` — see
[PolarisContentComponentBase.cs.md](PolarisContentComponentBase.cs.md) and
[PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md).

## Members

| Member | Signature | What it does |
|---|---|---|
| `BuildChildContent` | `protected override RenderFragment? BuildChildContent()` | Emits `supplemental-start` at sequence 0, `ChildContent` at 100, `aside` at 200. The element base calls it after the element-reference capture. |
| `AddUnvalidatedSlot` | `protected static void AddUnvalidatedSlot(RenderTreeBuilder builder, int openSequence, string slot, RenderFragment? fragment)` | Wraps a fragment in a plain `<div slot="…">` and closes it, or returns without emitting anything when the fragment is null. For `supplemental-start` and `aside` only. |

## Behaviour

- **The order in the element is fixed**: `supplemental-start`, then `ChildContent`, then `aside`. The
  three regions have their own sequence numbers (0–2, 100, 200–202), and the numbers have to keep
  increasing within the render scope — the renderer throws
  `NotImplementedException: Unexpected frame type during RemoveOldFrame` when they go backwards.
- **A null fragment produces no wrapper element at all**, not an empty `<div>` — so a slot nobody set
  leaves the element's markup exactly as it would have been.
- **The slot attribute lives on a `div`, not on the content.** A `RenderFragment` cannot carry an
  attribute, so the class supplies the carrier; the slotted content is therefore a child of that `div`,
  not a direct child of the Polaris element.
- **Five of the seven subclasses get the two slots**: `PolarisModal`, `PolarisSection`, `PolarisPage`,
  `PolarisMenu` and `PolarisButtonGroup` do not override `BuildChildContent`. `PolarisBanner` overrides
  it to return `ChildContent` alone, and `PolarisEmptyState` overrides it to render a `subheading` slot
  plus `ChildContent` — both therefore accept `SupplementalStart` and `Aside` and render neither.
- `AddUnvalidatedSlot` has no call site in the library today: `PolarisEmptyState` writes the same three
  frames inline, wrapping its subheading in an `s-text` that carries the slot attribute. The helper is
  that shape generalised, for a wrapper that needs an unvalidated slot of its own.

## Gotchas

- **A validated slot cannot come through this class.** Polaris checks the element type of a slot's
  children, and `primary-action` rejects a wrapping `div` — so a modal's OK and Cancel are
  `PolarisButton Slot="primary-action"` placed as **direct children of the modal**, beside its body, not
  inside the body's stack. Nested there, the slot attribute is ignored and the dialog renders with no
  buttons at all, with nothing logged. The same rule is why this class exposes these two slots as
  fragments and no others.
- **Only these two slots are unvalidated.** `supplemental-start` and `aside` accept any element, which
  is the stated reason they can be fragments; anything else has to be set from the child. Adding a third
  parameter here for a validated slot would build markup Polaris silently discards.
- **Nothing checks that the element has the slot.** Both parameters are inherited by all seven
  subclasses, and whether the element paints an `aside` column at all is Polaris's business — the page's
  is the one this library mentions, and it renders only at the page's `base` inline size — so an `Aside`
  on a button group is content that never appears, with no error.
- **A subclass that overrides `BuildChildContent` silently loses both parameters**, as `PolarisBanner`
  does. That is the trap to check before passing a fragment to a component of this family: look at
  whether the component has its own child-content override.
- **`ASP0006` is suppressed inside `AddUnvalidatedSlot` on purpose.** The analyzer wants literal sequence
  numbers at each call site; a helper cannot use literals, so the block disables the rule rather than
  duplicating itself at two call sites. Only the open and content frames need numbers that increase
  together, which is why the helper takes one starting number and derives the other two.
- **A `.razor` component cannot usefully derive from this class.** The Razor compiler injects its own
  `BuildRenderTree`, which replaces the base's, so `BuildChildContent` is never called and the component
  renders nothing. Wrappers are plain C# classes.

## See also

- [PolarisContentComponentBase.cs.md](PolarisContentComponentBase.cs.md) — the parent that supplies
  `ChildContent`, and the class to derive from when an element has no slots at all.
- [PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md) — the render lifecycle this
  class plugs its child content into, and the `Slot` parameter that validated slots use.
- [PolarisModal.cs.md](Components/PolarisModal.cs.md) — the component whose `primary-action` slot is the
  expensive version of the nesting mistake.
- [PolarisBanner.cs.md](Components/PolarisBanner.cs.md) — a subclass that overrides
  `BuildChildContent`, and so drops both slot parameters.
