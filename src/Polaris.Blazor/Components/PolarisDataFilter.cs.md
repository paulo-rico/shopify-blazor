<!-- summary-of: PolarisDataFilter.cs -->
<!-- source-sha256: a459980ed066 -->

# PolarisDataFilter.cs

The filter bar for a data grid: a search box, a popover of per-column controls generated from the
grid's own column metadata, and a removable chip for every filter that is applied. It never touches
the collection — it writes a `PolarisGridFilterDescriptor` into the shared `PolarisGridState`, which
re-runs the query — so it can be moved, replaced or left out.

`Polaris.Blazor.Components` · composed component, not one `<s-*>` element: an `s-stack` bar holding an
`s-search-field`, an `s-button`, an `s-clickable-chip` row and an `s-popover` panel · base
`PolarisGridComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `State` | `PolarisGridState` | required (`EditorRequired`, `default!`) | The shared state, and it must be the same instance the grid was given. Columns, `Query`, the applied filters and `Changed` all live there. Null renders nothing at all: the render method returns before the first element. |
| `ApplyMode` | `PolarisGridFilterApplyMode` | `Debounced` | When a change reaches the query: `Immediate` on every change, `Debounced` after the quiet period, `Manual` only when Apply is pressed. Applies to **every** control, not just text. |
| `DebounceMilliseconds` | `int` | `300` | Quiet period a debounced change waits. Negative values are clamped to 0 (`Math.Max`), so a bad number means "apply at once" rather than an exception. Ignored by the other two modes. |
| `SearchPlaceholder` | `string?` | `"Search"` | Placeholder in the search box. `null` is passed through as null — there is no render-time fallback to the default, the default is only the initialiser. |
| `ShowActiveFilters` | `bool` | `true` | Draws the chip row. Off hides the chips only: filters still apply, and the filter button still counts them. |
| `ShowClearAll` | `bool` | `true` | Draws the "Clear all" button — and only when the state has a column filter or a non-blank search (`Query.HasAnyFilter`). With nothing applied the button is absent rather than disabled. |
| `ShowSearch` | `bool` | `true` | Draws the search box. Off removes the control but leaves a search already in the state applying. |
| `Actions` | `RenderFragment?` | `null` | Content at the end of the bar, in its own inline stack — an export button, a column picker. Rendered before the popover, which is the last child of the outer stack. |

Inherited: none. `PolarisGridComponentBase` adds no parameters; it only subscribes to `State.Changed`
and redraws.

## Members (called from the page)

| Member | Signature | What it does |
|---|---|---|
| `SearchAsync` | `Task SearchAsync(string? term)` | Applies a term as if the merchant had typed it. `Manual` records a draft under the internal `__search` key (the box shows it straight away); `Debounced` schedules `State.SetGlobalSearch`; otherwise applies now. Null or whitespace clears the search, because the state normalises it to null. |
| `SetFilterAsync` | `Task SetFilterAsync(string field, PolarisGridFilterDescriptor? filter)` | Applies or clears one column's filter: `Manual` drafts it, `Debounced` schedules it, otherwise applies it now. A null descriptor clears the column through `State.ClearFilter`. Throws `ArgumentException` when `field` is null or empty. |
| `ClearAllAsync` | `Task ClearAllAsync()` | Drops drafts, echoes and pending flags, then calls `State.ClearAllFilters()` — every column filter and the search, in any apply mode. That state call is a no-op when nothing is applied, so in that case it is only a local reset. |
| `Dispose` | `void Dispose()` | Cancels and disposes the debounce timer before the base unsubscribes, so a change still inside its interval is dropped. |

## Behaviour

- **The bar.** An outer inline `s-stack` (small gap, centred, `space-between`) whose children are, in
  order: a second inline stack holding search, filter button, chips and clear-all; `Actions` in its
  own inline stack when supplied; and the `s-popover` panel. The panel is a sibling of the group that
  triggers it, not nested inside it.
- **Opening the panel is declarative.** The button carries `CommandFor` = the bar's generated id
  (`polaris-data-filter-N`, from a static counter) with `Command=Show`; Apply and Cancel reuse the id
  with `Command=Hide`. No JavaScript of this library's own is involved.
- **Search.** An `s-search-field` labelled "Search" whose label is hidden
  (`LabelAccessibilityVisibility.Exclusive`). Both `OnInput` and `OnChange` apply, so a keystroke and
  a blur each count — under `Debounced` each one reschedules the same timer.
- **Filter button.** Absent when no column declares a filter type; otherwise reads `Filter` or
  `Filters (n)`, where n counts column filters only, and flips `Auto` → `Secondary` once n > 0. A
  search term alone leaves it reading `Filter`.
- **Chips.** One removable, strong-coloured chip per column filter, labelled by operator:
  `Title contains “x”`, `Title is 3`, `Title is A, B`, `Title is 1 to 5`, `Title is above 3`,
  `Title is empty`. A bound is written the way the slider's own Min/Max fields write it, so a chip
  reading `4.17` sits beside fields reading `4.17` rather than `4.1666667`.
- **One control per filter type.** `Text` → `s-text-field`; `MultiSelect` → a multiple
  `s-choice-list` over `FilterOptions`, else `State.GetDistinctValues`, each label rendered with
  `FormatValue`; `Boolean` → an `s-select` of All/Yes/No; `Range` → `PolarisRangeSlider` over
  `State.GetNumericBounds`; `DateRange` → two `s-date-field`s; `Custom` → the column's
  `FilterTemplate`, and nothing at all when that is null; `None` → excluded from the panel (and, if no
  other column declares a type, from the filter button too).
- **Range.** A null `Value` means the whole domain. The domain is the unfiltered dataset's bounds —
  a missing lower bound becomes 0 and a missing upper one becomes the lower bound plus 1 — the step is
  about a hundredth of the span, and the stored filter is re-read as decimals falling back to the
  domain ends. A range reaching both ends is reported as
  `null` — the absence of a filter — rather than as a criterion that matches everything.
- **Date range.** Each field's change rebuilds a `Between` descriptor from the **applied** filter's
  other bound, treating a whitespace-only string as absent; both empty clears the filter.
- **Boolean.** The select reads the applied value as a bool or as the string a page seeded it with,
  and shows `All` for null. Choosing All clears the filter; Yes/No writes `Equals(field, true/false)`
  with a real boolean.
- **Every path ends in the state.** `SetFilter`, `ClearFilter`, `SetGlobalSearch` or
  `ClearAllFilters` raise `Changed` after recomputing, and all three grid components redraw from that
  one event. The bar itself never re-queries anything.
- **Mirrors.** `_textEchoes` (per column) and `_searchEcho` are written back into the controls so a
  change made elsewhere shows up in the panel; while a field's change is pending they are left alone,
  or a stale value would be written back mid-keystroke and the caret would jump.

## Gotchas

- **The panel's size is stated because the measured default is unusable.** With no `InlineSize` the
  slotted content lays out at zero width — a slider's track is 0px and cannot be dragged, a text field
  is a sliver. With a block-size cap Polaris **clips** the content and does not scroll it, so with
  several controls the lower ones were painted past the panel's edge, in space it does not occupy and
  where clicks find nothing. Hence 320px inline, a 70vh cap, and a 300px scroll box of our own that
  must stay *shorter* than the cap.
- **An `s-popover` paints no padding of its own.** Its shadow `div.popover` computes to 0px padding
  and has the same rect as the slotted content, so controls sat flush against the frame. The panel
  states `padding:var(--p-space-400,16px)`; Polaris defines those tokens inside its shadow roots and
  not on `:root`, so the fallback is the value in force today.
- **One timer for the whole bar, and the loser is dropped, not merged.** Changing a second control
  inside the interval cancels the first change outright: only the last applies. A search scheduled and
  then superseded never reaches the state, and the next state change re-syncs the box from the state,
  so the typed term disappears.
- **`Debounced` is the default for every control**, so a select, a slider or a date also waits
  `DebounceMilliseconds` — not just typing.
- **The mirrors start empty, not from the state.** `_searchEcho` and `_textEchoes` are filled only by
  `OnStateChanged`; neither is seeded when the state arrives or in `OnParametersSet`. A view restored
  with a search or a text filter already applied therefore shows an empty box beside a filtered grid
  until the state changes again.
- **`FilterableColumns` ignores `Visible`, deliberately.** A filter type says "this value can narrow
  the result"; visibility says "this is drawn in the table", and a screen often needs the first
  without the second. Tying them made a hidden facet unfilterable and left a hidden column's filter
  applying with its control gone (measured on the products list). A column that should not be
  filterable says `FilterType="None"`.
- **A chip needs a matching column.** Active filters are matched to `State.Columns` by `Id`; a filter
  whose field matches no column still narrows the result but has no chip and no control here, so this
  bar cannot remove it. A global search is never chipped: both the chip row and the filter button's
  count ignore it, while `Clear all` includes it in `HasAnyFilter`.
- **`Manual` mode's Cancel is destructive.** Every change is a draft until Apply, but Cancel calls
  `ClearAllAsync`, so it discards the drafts *and* clears every filter already applied and the search.
  There is no "revert to what was applied".
- **Manual drafts are rebuilt from the applied filters, not preserved.** When the applied signature
  changes, `OnParametersSet` repopulates `_drafts` from `Query.Filters`; the signature covers
  field, operator, value, second bound and value count only — the global search is not part of it.
  `ClearAllAsync` empties the drafts as well.
- **The date-range fields cannot both be drafted under `Manual`.** Each reads the applied filter, not
  the draft, for its other bound, so the second field's edit builds its descriptor from the state and
  loses the first field's unapplied half.
- **The choice list is given the column id as its `Name`.** Polaris warns in the console on every
  render when a choice list has no name, and an unnamed group is not a form control either.
- **A boolean filter must carry a boolean.** The descriptor is built with `Equals(field, true/false)`;
  a string made every chip read `In Shopify is false` beside a control offering Yes and No, because
  the chip and the column formatter speak about a bool. Reading back accepts either form, so a
  page-seeded `Equals(…, true)` selects Yes.
- **Filter bounds never follow the filter.** `GetNumericBounds` is the whole dataset on purpose:
  bounds taken from the filtered data move inward with the handles, so a narrow range could never be
  widened again.
- **`SetFilterAsync` validates its field** and throws `ArgumentException` for null or empty, unlike
  the UI paths, which never produce one.

## See also

- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — the table this bar narrows, and the other owner of the same state.
- [PolarisDataPager.cs.md](PolarisDataPager.cs.md) — the third component on the shared state.
- [PolarisGridState.cs.md](../DataGrid/PolarisGridState.cs.md) — `SetFilter`, `ClearFilter`, `ClearAllFilters`, `SetGlobalSearch` and the `Changed` event.
