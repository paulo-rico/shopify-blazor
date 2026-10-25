<!-- summary-of: PolarisContentComponentBase.cs -->
<!-- source-sha256: 89b19a83696c -->

# PolarisContentComponentBase.cs

Base class for a wrapper whose children are ordinary markup: it adds `ChildContent` and returns it
from `BuildChildContent`, leaving a subclass only the tag name and attributes.

`Polaris.Blazor` · abstract base · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` | The element's children, rendered between the open and close tags by the base class. `null` renders an empty element — correct for an icon-only button or an empty container — with no error and no placeholder. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md)).

## Members

| Member | Signature | What it does |
|---|---|---|
| `BuildChildContent` | `protected override RenderFragment? BuildChildContent()` | Returns `ChildContent`. It is the only thing this class adds; the base's `BuildRenderTree` calls it after the element reference capture. |

## Behaviour

- A subclass is still a plain C# class: it overrides `TagName`, optionally `BuildAttributes`,
  `ConfigureEvents`, `Properties` and `ClearedProperties`, and never writes `BuildRenderTree`.
  `PolarisButton` is the canonical example — everything it adds beyond this class is attributes, one
  DOM property and three event mappings.
- The children are **light-DOM content**, appended in order to the element the base opens. The base
  class emits `AddContent` last, after all attributes and the element-reference capture, which is the
  ordering the renderer requires.
- Because `ChildContent` is a `RenderFragment?`, a caller can pass markup that this library does not
  wrap at all — plain `<div>`s, text, or other Polaris components. There is no validation here; what
  Polaris accepts as a child is Polaris's business.

## Gotchas

- **A `.razor` component cannot usefully derive from this class.** The Razor compiler injects its own
  `BuildRenderTree`, silently replacing the base implementation: the build succeeds and the page is
  empty. Wrappers are plain C# classes, without exception.
- **`ChildContent` cannot carry a slot.** Polaris validates the children of named slots by element
  type (`primary-action` rejects a wrapping `div`), and a fragment cannot carry an attribute — so
  anything that belongs in a slot is placed with a child component's `Slot` parameter, not through
  this class.
- **Do not default `ChildContent` to something.** A default fragment would render on every instance,
  including the icon-only controls where an empty element is the intent.
- Components whose children *are* slots derive from `PolarisSlottedComponentBase` instead; this class
  is only for elements that take ordinary content.

## See also

- [PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md) — the render lifecycle this
  class plugs `ChildContent` into.
- [PolarisButton.cs.md](Components/PolarisButton.cs.md) — a content-based wrapper, where the label is
  `ChildContent`.
- [PolarisStack.cs.md](Components/PolarisStack.cs.md) — a layout wrapper that exists to hold content
  children.
