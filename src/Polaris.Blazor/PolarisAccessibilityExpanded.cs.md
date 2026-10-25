<!-- summary-of: PolarisAccessibilityExpanded.cs -->
<!-- source-sha256: 8c1deb3b742c -->

# PolarisAccessibilityExpanded.cs

The two values Polaris accepts for `accessibilityExpanded`, the string property it turns into
`aria-expanded` on the real button in its shadow root.

`Polaris.Blazor` · enum for an `aria-expanded` announcement · wire spellings pinned with `[PolarisValue]`

## Members

| Member | Wire value | What it announces |
|---|---|---|
| `Expanded` | `expanded` | The controlled thing is open — `aria-expanded="true"`. |
| `Collapsed` | `collapsed` | The controlled thing is closed — `aria-expanded="false"`. |

## Behaviour

- Consumed as a **nullable** parameter, `PolarisAccessibilityExpanded?`, on
  `PolarisButton.AccessibilityExpanded` and `PolarisClickable.AccessibilityExpanded`. A `null` value
  omits `aria-expanded` altogether, which is the correct state for a control that controls nothing.
  That third state is why this is an enum rather than a `bool`.
- **On `s-button`** the value is written as a DOM *property*, `accessibilityExpanded`, after the
  element has upgraded — never as a light-DOM attribute. The wrapper puts the member's wire string
  (`"expanded"` / `"collapsed"`) into its `Properties` dictionary, and writes it on every render
  while the parameter is non-null.
- **On `s-clickable`** there is no equivalent property at all: the element reads only its
  accessibility label and command/interest props, so `PolarisClickable` calls
  `IPolarisInterop.SetAriaExpandedAsync` with `AccessibilityExpanded == Expanded`, and the interop
  layer writes `aria-expanded` onto the `<button>` inside the shadow root.
- Polaris derives the announcement from the string — `aria-expanded` is true only when the value is
  exactly `expanded`; any other value it recognises is treated as collapsed. `Collapsed` is therefore
  the element's own spelling for `aria-expanded="false"`, not merely "not expanded".

## Gotchas

- **A `bool` is the wrong type and fails silently.** `true` written into `accessibilityExpanded`
  renders the *opposite* of the intent, because Polaris compares the value against the string
  `"expanded"` and a boolean is read as collapsed. Use the enum.
- **It is not a reactive property.** Writing `accessibilityExpanded` alone changes nothing on screen:
  the render function reads it and schedules no render. The interop layer compensates by calling the
  element's own `queueRender()` after writing a name from its render-only list, which is the only
  reason a change becomes visible.
- **It is ignored when `CommandFor` is set** on a `PolarisButton`. There `aria-expanded` follows the
  open state of the target instead, and the value supplied here is dropped with a console warning:
  no exception, and nothing visibly changes.
- **The `s-clickable` write is re-applied on every render**, because Polaris re-creates the shadow
  button when it renders and anything written onto the previous node goes with it. A failed write —
  no button to write to — costs the announcement and nothing else.
- **There is no `Unknown`, `Auto` or `Inherit` member, and none should be added.** `null` is the only
  way to omit `aria-expanded`; any third member would be a wire value Shopify does not document.
- The wire strings are pinned with `[PolarisValue]`, so renaming a member cannot change the markup.

## See also

- [PolarisButton.cs.md](Components/PolarisButton.cs.md) — the `s-button` consumer, including the
  `CommandFor` override that drops this value.
- [PolarisClickable.cs.md](Components/PolarisClickable.cs.md) — the `s-clickable` consumer, written
  through interop onto the shadow button.
- [PolarisAccordionItem.cs.md](Components/PolarisAccordionItem.cs.md) — sets `Expanded`/`Collapsed`
  from its own open state on every render.
- [PolarisAttributeValue.cs.md](PolarisAttributeValue.cs.md) — where a member becomes its wire string.
