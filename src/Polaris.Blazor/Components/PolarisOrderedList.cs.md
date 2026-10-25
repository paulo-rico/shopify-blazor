<!-- summary-of: PolarisOrderedList.cs -->
<!-- source-sha256: 120b3a18a0f8 -->

# PolarisOrderedList.cs

A numbered list, rendered as `<s-ordered-list>`, whose numbering is always decimal starting at one and
cannot be restyled — so it is for a genuine sequence (steps, ranked results), not for decoration.

`Polaris.Blazor.Components` · renders `<s-ordered-list>` · base `PolarisContentComponentBase`

## Parameters

It declares **no parameters of its own** — the whole class is `TagName => "s-ordered-list"` and
nothing else. Everything comes from the base classes:

- `ChildContent` (`RenderFragment?`, from `PolarisContentComponentBase`) — the items. Polaris accepts
  only `PolarisListItem` children; a bare `<li>`, a `<div>` or plain text is not a list item to this
  element.
- `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` — the shared element
  parameters, described in [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

There is no `Start`, `Type`, `Reversed`, marker or indent parameter: the numbering and spacing belong
to the element and live in Polaris's shadow root, so a list that must begin at another number cannot
be expressed with this wrapper.

## Behaviour

- **Rendering is exactly the child fragment.** `BuildChildContent` returns `ChildContent`, so the
  markup is `<s-ordered-list>` with the item components inside it and nothing added — no heading, no
  wrapper, no counter.
- **Each item is its own component.** `PolarisListItem` also declares nothing but its tag name, so a
  list is one wrapper per level: a nested list is another `PolarisOrderedList` (or an unordered one)
  inside the item's own `ChildContent`.
- **The number is the item's position in the DOM.** Nothing on the item carries an index, and the
  wrapper exposes nothing to read it back, so a page that needs "Step 3" as a value takes it from its
  own loop.
- **It is a block with no styling of its own beyond the list.** The wrapper writes no attributes at
  all, so whatever spacing, indent and marker the page shows belongs to the element inside Polaris's
  shadow root and cannot be tuned from here.

## Gotchas

- **`ChildContent` is untyped, so the child rule is documentation, not a constraint.** Nothing stops a
  `<div>`, a link or plain text being placed inside; Polaris renders only list items as items, so the
  mistake surfaces in the page, never as a compile error or an exception here.
- **Numbering follows DOM order, and removal renumbers.** Delete the second of five items and the
  fourth becomes the third; there is no stable "item 4" the way a table row key would give one. A
  sequence whose numbers are cited elsewhere (in a message, a URL, a saved draft) needs its number in
  the content instead of relying on the marker.
- **Reordering needs `@key`.** Without it the renderer reuses children by position, so the visible text
  and the number can disagree for a frame when the list is sorted in place.
- **It is not an `<ol>`.** The browser's own `list-style`, `::marker`, `start` attribute and
  `display: list-item` rules do not apply, so CSS written against native list markup silently misses
  it, and a stylesheet that hides native markers has no effect here.
- **No marker or indent can be chosen.** The class contributes no attributes, so `AdditionalAttributes`
  is the only way to attempt an attribute this wrapper does not model — one Polaris may ignore.

## See also

- [PolarisListItem.cs.md](PolarisListItem.cs.md) — the only valid child.
- [PolarisUnorderedList.cs.md](PolarisUnorderedList.cs.md) — the bulleted counterpart, for a set where
  order does not carry meaning.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the shared parameters and
  how the element is rendered.
- [PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md) — where `ChildContent`
  comes from.
