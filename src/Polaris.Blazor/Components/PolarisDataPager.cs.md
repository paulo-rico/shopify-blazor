<!-- summary-of: PolarisDataPager.cs -->
<!-- source-sha256: cbc26a32565e -->

# PolarisDataPager.cs

Page navigation for a data grid, driven entirely by the grid's shared `PolarisGridState` rather than by
any count of its own. Every number on screen, and every button's enabled state, is read from the state
after its query ran. Polaris has no pagination element, so the pager is composed from `s-stack`,
`s-button`, `s-select` and `s-text`.

`Polaris.Blazor.Components` · composed, renders `<nav aria-label="Pagination">` around an `<s-stack>` · base `PolarisGridComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `State` | `PolarisGridState` | none — `[EditorRequired]` | The shared state. Must be the same instance the grid was given; null renders an empty nav with nothing in it. |
| `PageSizeOptions` | `IReadOnlyList<int>` | `[10, 25, 50, 100]` | Sizes offered in the rows-per-page select. The state's own page size is added when missing, and the union is sorted ascending, so the caller's ordering is not preserved. |
| `ShowFirstLast` | `bool` | `true` | Adds first-page and last-page buttons outside previous and next. |
| `ShowPageNumbers` | `bool` | `true` | Adds the numbered page buttons. Setting it false is what reveals the "Page N of M" text — the counter is a substitute for the numbers, never an addition. |
| `ShowRecordRange` | `bool` | `true` | Shows the record range, such as `26–50 of 148`, or `No records` when nothing matched. |
| `ShowPageSize` | `bool` | `true` | Shows the rows-per-page select. |
| `MaxPageNumbers` | `int` | `5` | Upper bound on numbered buttons, current page included. Silently clamped to 3–9 when the window is built: below three is raised, above nine lowered. |

This is not an element wrapper, so the inherited `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and
`AdditionalAttributes` parameters do **not** exist here.

## Behaviour

- **Layout.** The root is a `<nav>` with `aria-label="Pagination"` — a landmark a screen reader can
  jump to — holding one inline `s-stack` (gap `base`, `justify-content: space-between`, centred). Its
  two children are a summary stack on the left and a controls stack on the right.
- **Summary.** The left stack holds the record range as subdued text when `ShowRecordRange` is true,
  and `Page {n} of {count}` as subdued text only when `ShowPageNumbers` is false and the state has at
  least one page.
- **Controls, left to right.** Rows-per-page select (when `ShowPageSize`), first page (when
  `ShowFirstLast`), previous, numbered pages (when `ShowPageNumbers`), next, last page (when
  `ShowFirstLast`).
- **The page-size select** is an `s-select` labelled `Rows per page` with
  `LabelAccessibilityVisibility="Exclusive"`, so that label exists for screen readers and is not
  painted. Each option is an `s-option` whose `Selected` marks the size in force.
- **Numbered buttons.** The current page is a `Secondary` button labelled `Page {n}, current page`;
  every other page is `Tertiary` and labelled `Page {n}`. Clicking calls `State.SetPage(index)`.
- **The window.** The first and last pages always keep a button, because those are the two jumps a long
  list needs; the pages between them follow the current page in `MaxPageNumbers` minus two slots. A
  skipped gap becomes a subdued `…`, which is text and not a button — the pages it stands for are
  reachable with first, last and next, and a clickable ellipsis would offer a jump its label does not
  describe.
- **Step buttons** move with `State.SetPage`: first to `0`, previous to `PageIndex - 1`, next to
  `PageIndex + 1`, last to `PageCount - 1`, and are disabled from `HasPreviousPage` / `HasNextPage`, so
  the state's own clamping is never what stops a click.
- **Nothing is recomputed here.** `SetPage` and `SetPageSize` clamp, re-run the query and raise
  `Changed` once; `PolarisGridComponentBase` re-subscribes only when the state instance really changed
  and dispatches `StateHasChanged`, so the pager redraws together with the grid and filter that share
  the state. The pager never touches the state's lifetime — the page owns and disposes it.

## Gotchas

- **A second state instance silently describes a different grid.** The pager trusts `State`
  completely, so giving it a new `PolarisGridState` (rather than the grid's) produces plausible numbers
  that belong to nothing on screen.
- **The rows-per-page label is intentionally invisible.** Nothing on the page reads "Rows per page";
  that is the `Exclusive` visibility, not a missing label.
- **The page-size list is a sorted set, not your list.** Duplicates collapse, ordering is ascending,
  and the state's current size is always present — which is deliberate: without it the select would
  have no matching option and would display the first one, naming a page size that is not in force.
- **A non-positive option is inert.** Entries of `0` or below render as options, but choosing one is
  ignored (only a positive, invariant-culture parse reaches `SetPageSize`), so the select snaps back to
  the state's size on the next render.
- **Changing the page size keeps the record, not the page number.** `SetPageSize` preserves the first
  visible record and computes a new page index from it, so going from page 3 of 10 to 100 rows lands on
  a smaller index — expected, and visible as the range text and the re-centred numbers.
- **`MaxPageNumbers` below three does not mean three buttons on a short list.** The clamp governs the
  window; a result with fewer pages than the window shows a button per page and no ellipsis.
- **First and last reuse the same chevron icons as previous and next** (`chevron-left` /
  `chevron-right`). Only the `AccessibilityLabel` distinguishes them, so an icon-only reading of the
  markup makes two pairs look identical.
- **Every interpolated number uses `InvariantCulture`**, including the em-dash range, so no machine
  locale inserts a group separator into a page count.

## See also

- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — the grid that owns the rows this pager pages through.
- [PolarisGridState.cs.md](../DataGrid/PolarisGridState.cs.md) — `SetPage`, `SetPageSize`, `PageCount` and the clamp rules.
- [PolarisButton.cs.md](PolarisButton.cs.md) — the step and number buttons, including `Disabled` leaving the tab order.

The redraw subscription comes from `PolarisGridComponentBase` (in `src/Polaris.Blazor/DataGrid/`), which
re-subscribes only when the state instance changes and never disposes a state the page owns.
