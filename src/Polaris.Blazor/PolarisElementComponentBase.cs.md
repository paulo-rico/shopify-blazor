<!-- summary-of: PolarisElementComponentBase.cs -->
<!-- source-sha256: 2386dcc7d810 -->

# PolarisElementComponentBase.cs

Owns the whole life of one Polaris custom element: builds its attributes, renders it, applies DOM
properties that cannot be attributes, attaches and detaches event listeners, and disposes. A wrapper
subclass supplies `TagName`, adds attributes in `BuildAttributes`, maps events in `ConfigureEvents`,
and declares properties in `Properties` / `ClearedProperties`. Subclasses never write markup.

`Polaris.Blazor` · abstract base · the class every single-element wrapper derives from

## Members a subclass overrides or calls

| Member | Kind | What it does |
|---|---|---|
| `TagName` | `abstract string` | The custom element, e.g. `s-button`. |
| `IdAttributeName` | `virtual string` | Attribute carrying the id; default `id`. |
| `BuildAttributes(Dictionary<string,string?>)` | `virtual void` | Adds attributes. Call `base` first so shared parameters and `AdditionalAttributes` keep precedence. A `null` value is dropped; an **empty string** renders a bare attribute, which is how a Polaris boolean is spelled. |
| `ConfigureEvents(PolarisEventRegistration)` | `virtual void` | Maps DOM event names to callbacks. Called on every parameter pass; the map is rebuilt from scratch. |
| `BuildChildContent()` | `virtual RenderFragment?` | The element's children. |
| `Properties` | `virtual IReadOnlyDictionary<string,object?>` | DOM properties written after upgrade, for values attributes cannot express (arrays). |
| `ClearedProperties` | `virtual IEnumerable<string>` | Properties reset before they are written, so a stale array cannot keep state the server dropped. Cleared to `[]`, never `undefined`. |
| `RequiresId` | `virtual bool` | True when the element must get an id even if the caller passed none, because another element addresses it. |
| `Interop` | `[Inject] IPolarisInterop` | The interop service, supplied by the app. |
| `Element` | `ElementReference` | The element; valid only after the first render. |
| `ElementId` | `string?` | The id in force, supplied or generated. |
| `EmptyPropertySet` | `static readonly` | Shared empty property dictionary, for components whose properties are all optional. |

## Parameters (inherited by every wrapper)

| Parameter | Type | What it does |
|---|---|---|
| `Id` | `string?` | Stable id, so a trigger can address the element with `commandFor`. |
| `AccessibilityLabel` | `string?` | Accessible name when the visible content is not descriptive. Required for icon-only controls. |
| `ItemTag` | `string?` | Opaque token echoed on every event, so one handler can serve a list of rows. |
| `Slot` | `string?` | Puts the element in one of its parent's named slots. Slots are validated by the child's own element type, which is why they are a parameter here and not a `RenderFragment`. |
| `AdditionalAttributes` | `IReadOnlyDictionary<string,object>?` | Unmodelled attributes, forwarded as-is. |

## Lifecycle

1. `OnParametersSet` rebuilds the event map.
2. `BuildRenderTree` writes the element: attributes, element-reference capture, children. Attributes
   with an empty value are emitted as `true` so they render bare.
3. `OnAfterRenderAsync` clears `ClearedProperties`, sets `Properties`, re-attaches events when any are
   registered, and generates an id when `RequiresId`.
4. `DisposeAsync` detaches listeners once.

## Gotchas

- Sequence numbers in `BuildRenderTree` must **not** go backwards inside a scope; the renderer throws
  `NotImplementedException: Unexpected frame type during RemoveOldFrame` when they do. Attributes first,
  then the capture, then children.
- Attribute order is the caller's: `AdditionalAttributes` wins over a modelled parameter of the same name.
- A `.razor` component cannot derive from this class usefully — the Razor compiler injects its own
  `BuildRenderTree`, which silently renders nothing. Wrappers are plain C# classes.

## See also

- [PolarisContentComponentBase.cs.md](PolarisContentComponentBase.cs.md) — adds `ChildContent`.
- [PolarisSlottedComponentBase.cs.md](PolarisSlottedComponentBase.cs.md) — for elements whose children are slots.
- [PolarisAttributeValue.cs.md](PolarisAttributeValue.cs.md) — the formatting rules used inside `BuildAttributes`.
