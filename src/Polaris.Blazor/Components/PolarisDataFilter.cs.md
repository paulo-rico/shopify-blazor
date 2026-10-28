<!-- summary-of: PolarisDataFilter.cs -->
<!-- source-sha256: 598ef5488304 -->

# PolarisDataFilter.cs

The filter bar for a data grid: a search box, a popover of per-column controls generated from the
grid's own column metadata, and a removable chip for every filter that is applied. It never touches
the collection — it writes a `PolarisGridFilterDescriptor` into the shared `PolarisGridState`, which
re-runs the query — so it can be moved, replaced or left out. Its `Panel` presentation draws those
same controls in place, in a column of their own, with the chips and Clear all above them, and by
default the controls narrow to the current result as the merchant narrows the list.

`Polaris.Blazor.Components` · composed component, not one `<s-*>` element: an `s-stack` bar holding an
`s-search-field`, an `s-button`, an `s-clickable-chip` row and an `s-popover` panel · base
`PolarisGridComponentBase`, implements `IPolarisGridColumnHost`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `State` | `PolarisGridState` | required (`EditorRequired`, `default!`) | The shared state, and it must be the same instance the grid was given. Columns, `Query`, the applied filters and `Changed` all live there. Null renders nothing at all: the render method returns before the first element. |
| `ApplyMode` | `PolarisGridFilterApplyMode` | `Debounced` | When a change reaches the query: `Immediate` on every change, `Debounced` after the quiet period, `Manual` only when Apply is pressed. Applies to **every** control, not just text. |
| `DebounceMilliseconds` | `int` | `300` | Quiet period a debounced change waits. Negative values are clamped to 0 (`Math.Max`), so a bad number means "apply at once" rather than an exception. Ignored by the other two modes. |
| `SearchPlaceholder` | `string?` | `"Search"` | Placeholder in the search box. `null` is passed through as null — there is no render-time fallback to the default, the default is only the initialiser. |
| `ShowActiveFilters` | `bool` | `true` | Draws the chip row. Off hides the chips only: filters still apply, and the filter button still counts them. |
| `ShowClearAll` | `bool` | `true` | Draws the "Clear all" button — and only when the state has a column filter or a non-blank search (`Query.HasAnyFilter`). With nothing applied the button is absent rather than disabled. |
| `ShowSearch` | `bool` | `true` | Draws the search box. Off removes the control but leaves a search already in the state applying. Ignored by `Panel`, which never draws one. |
| `Actions` | `RenderFragment?` | `null` | Content at the end of the bar, in its own inline stack — an export button, a column picker. Rendered before the popover, which is the last child of the outer stack. Ignored by `Panel`. |
| `Presentation` | `PolarisDataFilterPresentation` | `Bar` | Where the controls are drawn. `Bar` is the search box, Filter button, chips and popover described above. `Panel` drops the bar and draws the same controls in place, with the chips and Clear all above them — for a screen that has given the filter a column of its own. |
| `Columns` | `RenderFragment?` | `null` | Column declarations for a screen whose rows are not a table. A grid registers the columns in its own `Columns` fragment; a card or gallery screen has no grid, so it declares them here and this component is the host they register with. Rendered before the controls are read, and never removed. |
| `NarrowToResult` | `bool` | `true` | Whether each control's choices and bounds come from the current result rather than from the whole dataset. On, the panel narrows as the list narrows and a control with nothing left to offer is not drawn; off, every control always describes the whole dataset. |

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
- **The panel is ordered by `FilterIndex`, not by the columns' declaration order.** `FilterableColumns`
  sorts the registered columns on it, which is what lets a page narrow by supplier and owner first
  while the table draws those columns further right. `OrderBy` is stable, so columns that share an
  index — including every column that leaves the default 999 — keep the order they were registered
  in, and a grid that never mentions `FilterIndex` keeps exactly the panel it had.
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
- **The panel presentation.** With `Presentation="Panel"` there is no bar and no popover: one stack
  holds an optional chip row and then the same controls the popover would hold. The chip row is the
  chips in a wrapping grid and, under them, a Clear all button — a wrapping `s-grid` cannot keep one
  item pinned to the end of the row, and an inline stack does not wrap at all. Nothing is drawn for
  the chip row when nothing is applied, so a panel with no filters is its controls and no empty row.
  `ShowSearch`, `Actions` and the filter button's count have no meaning there and are ignored.
- **`Columns` hosts the declarations for a screen with no table.** They are rendered before the
  controls are read, so the first render already knows them, and they are rendered outside every
  result branch, so a column stays registered while the list is empty. The component cascades itself
  as `IPolarisGridColumnHost` around that fragment and nowhere else.
- **Manual Apply and Cancel are shared with the popover.** One `BuildControls` draws the controls and
  the two buttons for both presentations; the buttons carry `CommandFor`/`Command=Hide` only when
  there is a popover to close, because a `commandFor` naming an element that does not exist is a
  button that quietly sends nothing.
- **Narrowing reads the rows that pass every OTHER filter.** With `NarrowToResult` on (the default), a
  multi-select's choices are `State.GetNarrowedValues(id)` and a range's ends are
  `State.GetNarrowedNumericBounds(id)` — both computed from the rows that pass every applied filter
  except the column's own. A column that states `FilterOptions` keeps them, minus the options no
  remaining row holds. The column's own criterion is left out on purpose: including it would leave a
  control offering only what is already selected, and a range that had to contain its own ends could
  never be widened again.
- **A control with nothing left to offer is not drawn**, and only that: a multi-select with no
  options left, a range with no numbers left, neither of them carrying a filter of its own. Text,
  boolean and date controls are never hidden — a text control has no list to empty, and a boolean's
  All/Yes/No always offers something. A column that already carries a filter keeps its control even
  when its list has emptied, because the chip and the control are the two routes back to the same
  filter.
- **The narrowed bounds also clamp the handles.** A filter set against a wider domain — Price
  100–200 applied, then a vendor whose pieces top out at 150 — would otherwise draw a handle past
  its own track. The interval is clamped into the domain for display; the stored filter is untouched,
  so nothing is silently rewritten by a render.
- **Turning it off is the old shape.** `NarrowToResult="false"` restores the whole-dataset choices and
  bounds, which is what a screen whose filter bar should stay still while the table narrows wants —
  and the escape hatch if narrowing ever hides a control a merchant needs.

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
- **`FilterIndex` orders the panel, not the chips or the table.** The chip row follows
  `State.Query.Filters`, which is the order filters were applied — a changed filter moves to the end —
  and the table draws `State.Columns` in registration order. A page expecting chips beside their
  controls will see them in the order the merchant narrowed the list instead.
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
- **A panel with no registered columns draws nothing at all**, chip row included. On a screen with no
  grid that is what an omitted `Columns` fragment looks like: an empty sidebar, with no error to say
  why. Declare the columns on the filter, or the screen has no filter.
- **Narrowed choices are recomputed once per filter change, not once per render.** The state caches
  them per column and clears the cache in `Refresh`, so a panel of five controls costs five matching
  passes per change — and a panel rendered twice without a change costs none.
- **A selected value that has just been narrowed away keeps its chip but not its list entry.** The
  chip row comes from the applied filters, so the selection stays visible and removable; the control's
  list only offers what is still possible. That is deliberate: a list that grew back to include an
  impossible value would be the panel contradicting the result.
- **Narrowing does not apply to the search box.** The global search is a filter over every searchable
  column, so it narrows the choices like any other criterion, but it is not itself a control and is
  never hidden or chipped.
- **`Panel` still needs a state.** It is the same component and the same query engine; the only thing
  that changed is where the controls are drawn.

## See also

- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — the table this bar narrows, and the other owner of the same state.
- [PolarisDataPager.cs.md](PolarisDataPager.cs.md) — the third component on the shared state.
- [PolarisGridState.cs.md](../DataGrid/PolarisGridState.cs.md) — `SetFilter`, `ClearFilter`, `ClearAllFilters`, `SetGlobalSearch` and the `Changed` event.
- [IPolarisGridColumnHost.cs.md](../DataGrid/IPolarisGridColumnHost.cs.md) — the cascade that lets a
  `Columns` fragment register here.
