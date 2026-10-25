<!-- summary-of: PolarisTable.cs -->
<!-- source-sha256: f9757113a73f -->

# PolarisTable.cs

A table shell: the header row and body are children, with an optional filters area in the header, the
table's own loading state, a stacked list presentation, and previous/next paging controls that raise
events instead of holding data.

`Polaris.Blazor.Components` · renders `<s-table>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Loading` | `bool` | `false` | Shows the table's own loading state. A reflected boolean, so it has to be cleared as both property and attribute — see Gotchas. |
| `Variant` | `string?` | `null` | `"list"` renders stacked key/value pairs instead of columns; `"auto"` is the element's default. A raw string, not an enum. |
| `Paginate` | `bool` | `false` | Renders the previous/next controls and wires the page events. |
| `HasNextPage` | `bool` | `false` | Whether a next page exists. **Only emitted when `Paginate` is true**; otherwise dropped silently. |
| `HasPreviousPage` | `bool` | `false` | Whether a previous page exists. Same condition. |
| `Filters` | `RenderFragment?` | `null` | Content for the table's header filter area, wrapped in a `div` carrying `slot="filters"`. Omitted entirely when null. |
| `ChildContent` | `RenderFragment?` | `null` | The header row and the body — the table's actual cells. Omitted when null. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnNextPage` | `EventCallback<PolarisEventArgs>` | The merchant presses next. `args.Type` is `nextpage` and there is no payload — the page number is the caller's own model. |
| `OnPreviousPage` | `EventCallback<PolarisEventArgs>` | The merchant presses previous, `previouspage`. |

## Behaviour

- **Paging is server-driven and stateless.** The element renders the controls and raises an event; it
  never advances a page itself, has no page number and no total. The page re-queries, then sets
  `HasNextPage` / `HasPreviousPage` for the new result set — that pair is the only feedback the
  controls get.
- `Filters` is not a slot parameter on a child but a fragment the wrapper wraps: `BuildChildContent`
  emits `<div slot="filters">` around it, then appends `ChildContent` after. The wrapper element is
  unavoidable here because a `RenderFragment` cannot carry an attribute, and this slot is not
  type-checked by Polaris.
- `Variant` is passed through untouched — no enum, no validation — so `"list"` is a magic string and
  any other value is whatever the element makes of it.
- `Loading` is handled by the interop layer, which clears both the reflected property and the
  attribute; this wrapper only states the boolean.

## Gotchas

- **`HasNextPage` and `HasPreviousPage` do nothing without `Paginate`.** The attributes are only
  emitted when `Paginate` is true, so setting them on an unpaginated table is a silent no-op rather
  than an error.
- **`Loading` is a reflected boolean, so `Loading="false"` is not a plain attribute removal.** The
  interop layer clears the property as well as the attribute; writing the attribute by hand elsewhere
  would leave the table stuck in its loading skin.
- Prefer `Loading` over stacking a [PolarisSpinner.cs.md](PolarisSpinner.cs.md) on the table: the
  element's own loading state is applied to the cells and keeps the layout, while an overlay hides
  the column widths the merchant was reading.
- Interactivity under Blazor's renderer: the wrapper inserts `s-table` before its header and body
  reach the DOM, so Polaris may log a missing-child warning on an interactive render. The data is
  still correct — it is the element's upgrade-time check, not a fault in the markup.
- The table holds no rows. Everything visible comes from child components inside `ChildContent`, so
  "the table is empty" is almost always an empty child collection rather than a table parameter.

## See also

- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — this table plus sorting, filtering, selection and editing.
- [PolarisDataPager.cs.md](PolarisDataPager.cs.md) — a standalone pager for a model that is not the table's.
- [PolarisSpinner.cs.md](PolarisSpinner.cs.md), [PolarisSection.cs.md](PolarisSection.cs.md)
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)
