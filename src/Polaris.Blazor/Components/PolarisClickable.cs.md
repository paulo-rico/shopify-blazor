<!-- summary-of: PolarisClickable.cs -->
<!-- source-sha256: d6f4d34bc96f -->

# PolarisClickable.cs

Wraps `<s-clickable>`, a custom interactive surface — usually a whole card that is the click target.
It combines the button-like behaviour set (disabled, loading, href, declarative command) with the box
styling set (padding, border, size, overflow), and it is deliberately *not* a button: it has no
keyboard behaviour or accessible role of its own.

`Polaris.Blazor.Components` · renders `<s-clickable>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Disabled` | `bool` | `false` | Prevents activation. Bare presence attribute when true; false removes it. |
| `Loading` | `bool` | `false` | Shows a loading state. It does **not** debounce or block repeat clicks — guard in C# as well. |
| `Href` | `string?` | `null` | Renders the surface as a link instead of a click target. |
| `Target` | `PolarisLinkTarget` | `PolarisLinkTarget.Auto` | Browsing context for `Href`: `Auto`, `Blank`, `Self`, `Parent`, `Top`. Dropped entirely when `Href` is null. |
| `Download` | `string?` | `null` | Marks the link target as a download. Unreliable cross-origin, per the XML. |
| `Type` | `PolarisButtonType` | `PolarisButtonType.Button` | Form behaviour inside a form: `Button`, `Submit`, `Reset`. Ignored by Polaris once `Href` or `CommandFor` is set, but still emitted. |
| `AccessibilityRole` | `string?` | `null` | ARIA role for the surface, for example `button`. Without one assistive technology sees nothing interactive. |
| `AccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (hidden from sight, still announced) or `Hidden`. Null leaves Polaris's default. |
| `CommandFor` | `string?` | `null` | Id of the element this surface controls — the declarative way to open an `s-modal`, `s-popover` or menu with no JavaScript. |
| `Command` | `PolarisCommand` | `PolarisCommand.Auto` | Command sent to `CommandFor`: `Auto` (usually toggle), `Show`, `Hide`, `Toggle`, `Copy`. Dropped when `CommandFor` is null. |
| `InterestFor` | `string?` | `null` | Id of a tooltip or popover shown when the surface is hovered or focused. |
| `AccessibilityExpanded` | `PolarisAccessibilityExpanded?` | `null` | `Expanded`/`Collapsed`, announcing whether what this surface controls is open. Leave null for a surface that controls nothing. |
| `Background` | `string?` | `null` | Background treatment, e.g. `subdued` or `transparent`. |
| `Padding` | `string?` | `null` | Padding on all edges, in the one-to-four-value shorthand. |
| `PaddingBlock` | `string?` | `null` | Block-axis padding, overriding `Padding`. |
| `PaddingBlockStart` | `string?` | `null` | Block-start padding, overriding `PaddingBlock`. |
| `PaddingBlockEnd` | `string?` | `null` | Block-end padding, overriding `PaddingBlock`. |
| `PaddingInline` | `string?` | `null` | Inline-axis padding, overriding `Padding`. |
| `PaddingInlineStart` | `string?` | `null` | Inline-start padding, overriding `PaddingInline`. |
| `PaddingInlineEnd` | `string?` | `null` | Inline-end padding, overriding `PaddingInline`. |
| `Border` | `string?` | `null` | Border shorthand, stated as width, then colour, then style. |
| `BorderWidth` | `string?` | `null` | Border width, overriding `Border`. The XML warns it is drawn from the small-to-large scale, not the full size scale. |
| `BorderStyle` | `string?` | `null` | Border style, overriding `Border`, e.g. `dashed`. |
| `BorderColor` | `string?` | `null` | Border colour, overriding `Border`. |
| `BorderRadius` | `string?` | `null` | Corner radius, in the one-to-four-value shorthand. |
| `BlockSize` | `string?` | `null` | Block size. |
| `MinBlockSize` | `string?` | `null` | Minimum block size. |
| `MaxBlockSize` | `string?` | `null` | Maximum block size. |
| `InlineSize` | `string?` | `null` | Inline size. |
| `MinInlineSize` | `string?` | `null` | Minimum inline size. |
| `MaxInlineSize` | `string?` | `null` | Maximum inline size. |
| `Overflow` | `string?` | `null` | `visible` or `hidden`. |
| `Display` | `string?` | `null` | `auto` or `none` — `none` takes the surface and its children out of layout. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes`, `ChildContent` — see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnClick` | `EventCallback<PolarisEventArgs>` | The surface is clicked. Modifier keys are on the args (`AltKey`, `CtrlKey`, `MetaKey`, `ShiftKey`). |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The surface receives focus — only reachable when it has been made focusable. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The surface loses focus. |

## Behaviour

- **`AccessibilityExpanded` is written through the interop layer, not as a property or an attribute.**
  The XML records why, measured from Polaris's render function: `s-clickable` reads only its
  accessibility label and its command/interest props, so the state is applied to the `<button>` Polaris
  renders inside the shadow root via `IPolarisInterop.SetAriaExpandedAsync`. An attribute on the host
  lands on an element with no role and is ignored.
- The write happens in `OnAfterRenderAsync` on **every** render, because Polaris re-creates that button
  when it renders and anything written onto the previous node goes with it. A `false` return from the
  interop call means there was no button to write to, which costs the announcement and nothing else.
- **The surface has no accessible name of its own**, because a heading set as an attribute lives in
  Polaris's shadow root rather than in this element's text. Set `AccessibilityLabel` alongside
  `AccessibilityExpanded`.
- Child content is rendered inside the surface. The XML notes this was once a bare element wrapper, so
  content written inside it had nowhere to go — a surface with no content and nothing to say so.

## Gotchas

- The XML's first instruction: prefer [PolarisButton.cs.md](PolarisButton.cs.md) for an action and
  [PolarisLink.cs.md](PolarisLink.cs.md) for navigation. Both bring the keyboard and screen-reader
  behaviour a bare clickable surface does not.
- It is **not focusable by default**, and without `AccessibilityRole` assistive technology sees nothing
  interactive at all. Anything a merchant must reach by keyboard should not be built on this.
- `Target` is dropped when `Href` is null and `Command` is dropped when `CommandFor` is null — neither
  is an error, both attributes simply never appear.
- `Type` does nothing once `Href` or `CommandFor` is set: the surface is a link or a command trigger,
  not a submit button, even though `type="button"` is still in the markup.
- `Loading` is cosmetic. A double click produces two `OnClick` callbacks; there is no built-in guard.
- `Border` can be partly overridden: each of `BorderWidth`, `BorderStyle` and `BorderColor` takes
  precedence over that part of the shorthand. `BorderWidth` is drawn from the small-to-large scale, not
  the full size scale, so a size token from elsewhere will not fit it.

## See also

- [PolarisClickableChip.cs.md](PolarisClickableChip.cs.md) — the same idea for a small chip.
- [PolarisBox.cs.md](PolarisBox.cs.md) — the same styling set with no interaction.
- [PolarisButton.cs.md](PolarisButton.cs.md) — what to use instead for a real action.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the interop hooks this
  component extends.
