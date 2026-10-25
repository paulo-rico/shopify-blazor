<!-- summary-of: PolarisGridFilterDescriptor.cs -->
<!-- source-sha256: 325d65ebcc04 -->

# PolarisGridFilterDescriptor.cs

One column filter, as data rather than as a control. It carries a field id, an operator and the
operand(s), plus the `IsEmpty` test that lets a caller drop a filter which cannot restrict anything;
because it is a value, the same filter can come from a component, a test or a saved view, and
`PolarisDataFilter` can be replaced or omitted without the grid changing behaviour.

`Polaris.Blazor.DataGrid` · `public sealed record` with five static factories · not a component

## Members

| Member | Signature | What it does |
|---|---|---|
| Constructor | `PolarisGridFilterDescriptor(string Field, PolarisGridOperator Operator, object? Value = null, object? SecondValue = null, IReadOnlyList<object?>? Values = null, bool IncludeNulls = false)` | Positional record; every parameter is a public init-only property. |
| `Field` | `string Field` | The `Id` of the column being filtered — a column id, not a property name. |
| `Operator` | `PolarisGridOperator Operator` | How the value is compared. |
| `Value` | `object? Value` | The value, or the **lower** bound of `Between`. |
| `SecondValue` | `object? SecondValue` | The **upper** bound of `Between`; unused by every other operator. |
| `Values` | `IReadOnlyList<object?>? Values` | The accepted values of `In`. |
| `IncludeNulls` | `bool IncludeNulls` | Keeps empty values in the result even though they match nothing. |
| `Text` | `static PolarisGridFilterDescriptor Text(string field, string? term)` | A case-insensitive `Contains` filter over a text column. |
| `Equals` | `static PolarisGridFilterDescriptor Equals(string field, object? value)` | An equality filter; text compares case-insensitively. |
| `In` | `static PolarisGridFilterDescriptor In(string field, IEnumerable<object?> values)` | Accepts any of `values`; the sequence is materialised into a list. |
| `Between` | `static PolarisGridFilterDescriptor Between(string field, object? lower, object? upper)` | An inclusive interval. |
| `InRange` | `static PolarisGridFilterDescriptor InRange(string field, PolarisNumberRange range)` | The same interval, taken from a `PolarisNumberRange`'s `Lower`/`Upper` — the shape a range slider reports. |
| `IsEmpty` | `bool IsEmpty { get; }` | True when the filter cannot restrict anything, so the caller can drop it rather than send a redundant criterion. |

## Behaviour

- **`IsEmpty` is per-operator, not "all fields are null"**:

  | Operator | Empty when |
  |---|---|
  | `Contains`, `StartsWith`, `EndsWith` | the value is null, or is null/empty/whitespace text — a whitespace term is not a search |
  | `In` | `Values` is null or holds no entries |
  | `Between` | **both** bounds are null — one bound alone is a real, open-ended filter |
  | `IsNull`, `IsNotNull` | never — they restrict without an operand |
  | everything else | the value is null |

- The rationale is stated in the source: a full-range slider must not become a filter that still
  triggers a refresh.
- **Values are coerced against the column's own type by the engine, not here.** A descriptor holds
  whatever it was given — often a string, because every Polaris form control reports one — and a value
  that cannot be coerced makes the engine drop the filter rather than match nothing.
- Being a record, two descriptors with the same arguments are equal, and a filter can be held in a
  variable, compared and logged like any other value.

## Gotchas

- **`Equals` is a factory, not the record's equality.** `PolarisGridFilterDescriptor.Equals("Name",
  x)` builds a filter; `a.Equals(b)` is the generated value equality. The two live side by side, and
  the factory name is the one that reads ambiguously in a diff.
- **A descriptor whose `Field` names no column is ignored by the engine**, with no error — the same
  outcome as a filter that matched everything. A typo therefore looks like a filter that is not
  applied, not like a mistake.
- **`IncludeNulls` does not make a null value match.** It only decides whether an empty cell survives
  a predicate that would otherwise reject it, and it is ignored entirely by `IsNull`/`IsNotNull`.
- **`Between` with one bound null is not empty and does filter**: the missing end simply does not
  constrain. `IsEmpty` will not drop such a filter, so a caller that wants to discard a one-sided range
  has to test the bounds itself.
- The record does not clamp or order its bounds. A range whose ends arrive the wrong way round is
  swapped by the engine, not rejected here.

## See also

- [PolarisGridEnums.cs.md](PolarisGridEnums.cs.md) — `PolarisGridOperator`, the values `Operator` takes.
- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — the only code that interprets a
  descriptor.
- [PolarisGridQueryState.cs.md](PolarisGridQueryState.cs.md) — the list these live in, one per column.
- [PolarisGridValues.cs.md](PolarisGridValues.cs.md) — the coercion that decides whether a filter can
  be applied at all.
