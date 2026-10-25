<!-- summary-of: PolarisUnorderedList.cs -->
<!-- source-sha256: 0bf538303d9f -->

# PolarisUnorderedList.cs

A bulleted list, rendered as `<s-unordered-list>`, whose children must all be `PolarisListItem`
components. Use `PolarisOrderedList` instead when the order of the items carries meaning.

`Polaris.Blazor.Components` · renders `<s-unordered-list>` · base `PolarisContentComponentBase`

## Parameters

It declares **no parameters of its own** — the class is `TagName => "s-unordered-list"` and nothing
else. Everything comes from the base classes:

- `ChildContent` (`RenderFragment?`, from `PolarisContentComponentBase`) — the items. Polaris validates
  them, so they have to be `PolarisListItem` components; a bare `<li>`, a `<div>` or plain text is not a
  list item to this element.
- `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` — the shared element parameters,
  described in [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

There is no `Type`, marker, indent or start-number parameter: the list style is the element's own and
cannot be restyled, because the styles live in Polaris's shadow root.

## Behaviour

- **Rendering is exactly the child fragment.** `BuildChildContent` returns `ChildContent`, so the markup
  is `<s-unordered-list>` with the item components inside it and nothing added.
- **Each item is its own component.** `PolarisListItem` also declares nothing but its tag name, so a
  list is one wrapper per level: a nested list is another `PolarisUnorderedList` inside the item's own
  `ChildContent`.

## Gotchas

- **`ChildContent` is untyped, so the child rule is documentation, not a constraint.** Nothing stops a
  `<div>`, a link or plain text being placed inside; Polaris accepts only `PolarisListItem` children, so
  the mistake surfaces only in the rendered page, never as a compile error or an exception here.
- **A marker or indent cannot be chosen.** The class contributes no attributes at all, so there is no
  parameter for the bullet style, and `AdditionalAttributes` is the only way to attempt an attribute this
  wrapper does not know about — one that Polaris may ignore.
- **The list is a visual grouping, not a navigation or step indicator.** Nothing here numbers, counts or
  orders anything; if the sequence matters, the ordered wrapper is the element that expresses it.

## See also

- [PolarisListItem.cs](PolarisListItem.cs) — the only valid child.
- [PolarisOrderedList.cs](PolarisOrderedList.cs) — the ordered counterpart, for a genuine sequence.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the shared parameters and
  how the element is rendered.
- [PolarisContentComponentBase.cs](../PolarisContentComponentBase.cs) — where `ChildContent` comes from.
