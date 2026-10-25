<!-- summary-of: PolarisListItem.cs -->
<!-- source-sha256: 2a249fa47729 -->

# PolarisListItem.cs

One entry in an ordered or unordered list: it renders `<s-list-item>` around whatever content it is
given and declares no parameters of its own. It is only valid inside a `PolarisOrderedList` or a
`PolarisUnorderedList`, which supply the marker, the indentation and the spacing between items.

`Polaris.Blazor.Components` · renders `<s-list-item>` · base `PolarisContentComponentBase`

## Parameters

None of its own. The entire API is the inherited `ChildContent` (`RenderFragment?`, default `null`) —
whatever sits between the tags is the item's text. Everything else is inherited from
`PolarisElementComponentBase`: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and
`AdditionalAttributes`, described in
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md). A plain list item normally
leaves all of them unset.

## Behaviour

- **It renders one element and nothing else.** There is no wrapper, no marker, no bullet and no
  styling of its own: the parent `<s-ordered-list>` / `<s-unordered-list>` owns all of that, which is
  why an item used anywhere else looks like bare text.
- **The content is ordinary content, not a fragment list.** Inline components (`PolarisText`,
  `PolarisBadge`, a link) can be mixed inside one item, because the children are only passed through.
- **There is no key, value or index.** The component cannot tell you where it is in the list; an item
  that needs its position gets it from the page's loop.

## Gotchas

- **Nothing validates the parent.** Polaris only styles and marks list items inside a list element, so
  a stray `PolarisListItem` renders its content with no marker and no indent, and nothing reports the
  mistake — it looks like unstyled text rather than like a broken component.
- **It is not an `<li>`.** The browser's own `list-style`, `::marker` and `display: list-item` rules
  do not apply to it, so CSS written against native list markup silently misses it.
- **Ordering comes from DOM order.** Markup built in a loop shows items in the order Blazor emits them;
  reordering needs `@key` on each item, or the renderer reuses an item's children by position and the
  visible order can differ from the model.

## See also

- [PolarisUnorderedList.cs.md](PolarisUnorderedList.cs.md) — the bulleted list this belongs in; its
  sibling `PolarisOrderedList` is the numbered one.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the inherited parameters
  and the lifecycle every wrapper follows.
