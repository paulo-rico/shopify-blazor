<!-- summary-of: PolarisGridValues.cs -->
<!-- source-sha256: 7c5e63f367e9 -->

# PolarisGridValues.cs

The comparison and conversion rules the query engine applies to values of any type, plus the
`IComparer<object?>` built on them — centralised because every one of these rules fails silently when it
is wrong.

`Polaris.Blazor.DataGrid` · `internal static class PolarisGridValues` + `internal sealed class PolarisGridValueComparer` · not a component

## Members

**`PolarisGridValues`** (internal static).

| Member | Signature | What it does |
|---|---|---|
| `TryCoerce` | `static bool TryCoerce(object? value, Type targetType, out object? result)` | Converts to `targetType` when the value is not already of it. Unwraps `Nullable<T>` first. Returns **false** when a non-empty value could not be converted — the caller then drops the filter rather than applying one that matches nothing. A **null input succeeds** with a null result. |
| `Compare` | `static int Compare(object? left, object? right, Type? targetType = null)` | Orders two values: text `OrdinalIgnoreCase`, numbers numerically, dates chronologically, and **empty values after everything else**. When `targetType` is given, the *right* operand is coerced into it first. |
| `IsNumeric` | `static bool IsNumeric(object value)` | True for `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`. |
| `TryToDecimal` | `static bool TryToDecimal(object? value, out decimal result)` | Reads a value as a decimal for a range control's bounds. Text is parsed with `NumberStyles.Number \| NumberStyles.Float` and the invariant culture. |
| `TryToDateOnly` | `static bool TryToDateOnly(object? value, out DateOnly result)` | Reads a value as a date without a time. A `DateTime` is **truncated** to its date; text is parsed invariantly. |

**`PolarisGridValueComparer`** (internal sealed, `IComparer<object?>`).

| Member | Signature | What it does |
|---|---|---|
| `Instance` | `static readonly PolarisGridValueComparer Instance` | The single instance; the comparer is stateless and its constructor is private. |
| `Compare` | `int Compare(object? x, object? y)` | Delegates to `PolarisGridValues.Compare(x, y)` with no target type, so the runtime types of the two values decide. |

## Behaviour

- **The invariants, stated because they are not visible in the code:** comparison is case-insensitive for
  text, numeric for numbers, chronological for dates, and an **empty value is the greatest value**. So
  ascending order puts blanks last and descending order puts them first — the spreadsheet convention.
- **`Compare` handles null before anything else.** Two nulls (or the same reference) are equal, a null left
  operand sorts after a non-null right one, and a null right operand sorts before.
- **Text and numbers are compared across types.** A filter value always arrives as a string, because every
  Polaris form control reports one; when the left value is numeric and the right is a parseable numeric
  string, the two are compared numerically — without that, `"9"` and `"18"` order as text and 9 lands above
  18.
- **Everything else has a typed case**: `DateTime`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `Enum`, `bool`.
  Only when none matches does it fall back to comparing the invariant `ToString` of both sides.
- **`TryCoerce` has an explicit case per target** — `string`, `bool`, `DateOnly`, `DateTime`, `TimeOnly`,
  `Guid`, enum — and everything else goes through `Convert.ChangeType` with the invariant culture, catching
  `FormatException`, `InvalidCastException`, `OverflowException` and `ArgumentException` into `false`.
- **Culture is fixed everywhere.** A filter typed `"1.5"` means the same thing on a server whose locale
  writes `"1,5"`, and the same dataset yields the same bounds on every machine.

## Gotchas

- **`TryCoerce` returning true does not mean the result has a value.** A null input returns true with
  `result = null`; a caller must check both.
- **A boolean column does not read anything as false.** `"true"`/`"false"` parse; any other text returns
  false and the filter is dropped rather than interpreted as `false`. A real `bool` passes through, and a
  non-string value goes through `Convert.ToBoolean`.
- **An enum filter is parsed by name, case-insensitively.** A non-string value goes through `Enum.ToObject`,
  and an `ArgumentException` there becomes a `false` return rather than a crash.
- **A `DateTime` bound is truncated, never rounded.** A filter bound of "the 5th" must not exclude the 5th
  because its time-of-day was 00:00 versus 00:00:01.
- **`Compare` coerces only the right operand.** A left value that is a string always takes the text branch,
  so a text date against a `DateOnly` column compares as text unless the caller passed a `targetType` that
  makes the right side a string as well.
- **The fallback is `ToString`.** Two values of a type with no typed case and no sensible invariant string
  (an object with the default `ToString`) compare as their type names — equal to each other, in no order.
- Both types are `internal`: this is the engine's plumbing, not an extension point. A caller that needs the
  ordering asks the state, not these.

## See also

- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — applies these rules to filters, sorts and
  paging.
- [PolarisGridStateOfT.cs.md](PolarisGridStateOfT.cs.md) — the cached bounds and distinct values built from
  them.
- [PolarisGridFilterDescriptor.cs.md](PolarisGridFilterDescriptor.cs.md) — the descriptors whose value these
  conversions turn into a typed comparison.
