<!-- summary-of: PolarisDateEnums.cs -->
<!-- source-sha256: fb1220c71381 -->

# PolarisDateEnums.cs

The two `s-date-picker` options: whether the merchant picks one date or a range, and how many months
the calendar shows at once. Both are written as attributes on the picker by `PolarisDatePicker`.

`Polaris.Blazor` · two enums for `s-date-picker` · wire spellings pinned with `[PolarisValue]`

## Members

**`PolarisDatePickerType`** — selection behaviour (`type` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `Single` | `single` | One date. The default on `PolarisDatePicker`. |
| `Range` | `range` | Two dates, held together in the value as `start--end`. |

**`PolarisDatePickerVisibleMonths`** — how many months are shown (`visibleMonths` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `Auto` | `auto` | Chosen from the available width. The default. |
| `One` | `1` | One month, whatever the width. |
| `Two` | `2` | Two months side by side. |

## Behaviour

- `PolarisDatePicker.Type` defaults to `Single` and `PolarisDatePicker.VisibleMonths` to `Auto`; both
  are written on every render, so the attributes are always present.
- **`Range` changes the shape of the value, not the type of the property.** The picker's `Value` stays
  a `string?` and holds two ISO dates separated by `--`, for example `2025-05-28--2025-05-31`. Code
  that parses `Value` as a single `DateOnly` works until someone switches the picker to `Range`.
- The same `start--end` syntax appears in the picker's `Allow` and `Disallow` restrictions, where an
  omitted end means "and later" (`2025-01-01--`). The separators are the same two hyphens.
- `VisibleMonths` only changes how much calendar is painted. It does not make any date unselectable —
  that is `Allow`/`Disallow`.

## Gotchas

- **The numeric wire values must stay pinned.** A C# member cannot be named `1`, hence `One`/`Two`
  with `[PolarisValue("1")]` / `[PolarisValue("2")]`; the naming-convention fallback would emit
  `one`/`two`, which the picker ignores without an error.
- **The value is a plain `string?` in either mode, so nothing in the type says whether to expect one
  date or two.** Anything that stores it (a column, a query string, a filter) has to know which mode
  produced it, so keep `Type` alongside the value rather than re-deriving the shape from the text.
- **The picker is a two-dimensional, pointer-driven control**, awkward by keyboard and on a touch
  screen; the source's own remark points at `PolarisDateField` as the better form control. These
  enums belong to the calendar surface, not to the field.
- There is no `Three` or `Auto`-with-override; adding a member means adding a wire value Shopify
  documents, which today is only `auto`, `1` and `2`.

## See also

- [PolarisDatePicker.cs.md](Components/PolarisDatePicker.cs.md) — where both enums are parameters,
  and where the `--` range value and `Allow`/`Disallow` syntax are documented.
- [PolarisDateField.cs.md](Components/PolarisDateField.cs.md) — the text-field alternative that does
  not use these enums at all.
- [PolarisAttributeValue.cs.md](PolarisAttributeValue.cs.md) — how a member becomes its wire string.
