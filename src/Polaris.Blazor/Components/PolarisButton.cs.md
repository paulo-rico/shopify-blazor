<!-- summary-of: PolarisButton.cs -->
<!-- source-sha256: c450f85342f0 -->

# PolarisButton.cs

A button: an ordinary action, a destructive action, a loading action, or a link. The label is
`ChildContent`. `CommandFor` + `Command` open a modal or popover with no JavaScript, and `Href`
renders the button as a link.

`Polaris.Blazor.Components` · renders `<s-button>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Variant` | `PolarisButtonVariant` | `Auto` | Emphasis: primary, secondary, tertiary, plain. |
| `Tone` | `PolarisTone` | `Auto` | Semantic colour. `PolarisTone.Critical` for a destructive action. |
| `Icon` | `string?` | `null` | Name of a predefined admin icon, rendered before the label. |
| `Disabled` | `bool` | `false` | Blocks interaction; a disabled button also leaves the tab order. |
| `Loading` | `bool` | `false` | Shows the button's own spinner. Does **not** debounce or block repeat clicks — guard in C# too. |
| `Type` | `PolarisButtonType` | `Button` | Form behaviour: `Button`, `Submit`, `Reset`. |
| `Href` | `string?` | `null` | Renders as a link. Shopify may block external URLs inside the Admin. |
| `Target` | `PolarisLinkTarget` | `Auto` | Browsing context for `Href`. Dropped when `Href` is null. |
| `InlineSize` | `PolarisButtonInlineSize` | `Auto` | Width behaviour. |
| `CommandFor` | `string?` | `null` | Id of the element this button controls — the declarative way to open an `s-modal` or `s-popover`. |
| `Command` | `PolarisCommand` | `Auto` | The command sent to `CommandFor`. Dropped when `CommandFor` is null. |
| `InterestFor` | `string?` | `null` | Id of a tooltip or popover shown on hover/focus. |
| `AccessibilityExpanded` | `PolarisAccessibilityExpanded?` | `null` | Announces open/closed state through `aria-expanded` on the shadow `<button>`. Leave null for a button that controls nothing. |
| `ChildContent` | `RenderFragment?` | `null` | The label. Omit for an icon-only button, and set `AccessibilityLabel`. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnClick` | `EventCallback<PolarisEventArgs>` | Clicked. Modifier keys are on the args (`AltKey`, `CtrlKey`, `MetaKey`, `ShiftKey`). |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The button loses focus. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The button receives focus. |
| `OnKeyDown` | `EventCallback<PolarisEventArgs>` | Every key press while the button has focus. `args.Key` is the key name — `ArrowDown`, `Escape`. Enter and Space click the button natively and need no handling. |

`OnKeyDown` is a DOM event forwarded from the element, not a Polaris one; it costs a listener per
button and is attached only when a handler is supplied. It exists so a row in a list of buttons can
follow the arrow keys — see [PolarisAutocomplete.cs.md](PolarisAutocomplete.cs.md).

## Behaviour that is not visible in the markup

- **`AccessibilityExpanded` is a DOM property, not an attribute.** Polaris does not reflect it, so an
  attribute would freeze at first paint; the semantics live on the `<button>` inside the shadow root,
  which a light-DOM attribute cannot reach. It is also not reactive — the interop layer calls the
  element's `queueRender()` after writing it, which is why a change is visible at all.
- **It is ignored when `CommandFor` is set.** Then `aria-expanded` follows the target's open state and
  the value here is dropped with a console warning.
- The valid values are the strings `"expanded"` / `"collapsed"`, exposed as
  `PolarisAccessibilityExpanded`. A `bool` here renders the opposite of what is meant.

## See also

- [PolarisButtonGroup.cs.md](PolarisButtonGroup.cs.md), [PolarisPopover.cs.md](PolarisPopover.cs.md),
  [PolarisModal.cs.md](PolarisModal.cs.md)
- `docs/components.md` — generated reference, same parameters.
