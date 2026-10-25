<!-- summary-of: PolarisGridFormatting.cs -->
<!-- source-sha256: f411bee0456b -->

# PolarisGridFormatting.cs

Turns any value into the text a cell shows, invariant by default and honouring a custom
`ICustomFormatter` when one is supplied. It is one small function, but it is the single place where a
cell's displayed text is decided, so a currency format, an enum name and a bool all go through the
same three-line path.

`Polaris.Blazor.DataGrid` · `internal static class` · one public method, `Format`

## Members

| Member | Signature | What it does |
|---|---|---|
| `Format` | `static string Format(object? value, string? format, IFormatProvider? provider)` | Returns the display text of `value`. Null becomes the empty string; a custom formatter on the provider is asked first; a string is returned unchanged; an `IFormattable` is formatted with `format` and the provider, falling back to the invariant culture; anything else uses its own `ToString()`. |

## Behaviour

- **Null renders as the empty string**, never as "null" and never as a dash — a placeholder is the
  caller's decision, not this function's.
- **Invariant by default**, for the same reason every attribute in this library is formatted
  invariantly: a comma decimal separator produces text that other code — a test, an exported CSV, a
  comparison — reads differently from what was rendered. A caller who wants the merchant's own
  conventions passes the column's `FormatProvider`.
- **A custom formatter is consulted first**, before the string and `IFormattable` branches, in the same
  order `string.Format(IFormatProvider, string, object)` uses. That is what lets a column print "Yes"
  and "No" for a bool, or "In Shopify" for a flag.
- A `string` value ignores `format` entirely and is returned as-is; only non-text values reach the
  `IFormattable` branch.
- The fallback is `ToString()`, with a null result turned into the empty string, so a type that
  overrides nothing renders its type name rather than throwing.

## Gotchas

- **Without an `ICustomFormatter`, a bool renders "True" and "False".** This is the failure the custom
  formatter branch exists to prevent: measured on a hidden "In Shopify" column whose chip read
  "is True" beside a filter offering Yes and No, because `bool.ToString(string, IFormatProvider)` — and
  its siblings for every number and date — never consult a provider. The custom formatter is the only
  hook that works.
- **A `format` string on a text column does nothing.** Strings short-circuit before `IFormattable`, so
  a column formatting `Name` with `"C2"` or a truncation pattern renders the raw text with no warning.
- **A provider that implements `ICustomFormatter` but returns null** would break the contract this
  method relies on — it returns whatever the formatter returns, without a null check on the result.
  Implementations must return a string.
- The invariant fallback applies whenever `provider` is null, including the `IFormattable` path, so
  passing `CultureInfo.CurrentCulture` is a deliberate opt-in rather than an accident of the machine's
  locale.

- `PolarisGridColumnDefinition<TItem>` supplies `Format` and `FormatProvider` per column; this is the
  function it calls.

## See also

- [PolarisGridValues.cs.md](PolarisGridValues.cs.md) — the comparison rules, which are related but
  deliberately separate from display formatting.
- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — global search uses the invariant
  `ToString`, not this formatter, so a cell can display one text and search on another.
