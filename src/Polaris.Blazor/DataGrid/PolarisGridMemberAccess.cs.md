<!-- summary-of: PolarisGridMemberAccess.cs -->
<!-- source-sha256: 28727bfc3ce2 -->

# PolarisGridMemberAccess.cs

What a column's `Expression` says about the property behind it. It carries the dotted member path, the
type of value the expression produces, a compiled getter that never reflects per cell, and a setter
that exists only when the member can actually be written — the four-value answer
`PolarisGridExpression.Analyse` returns, and what tells the grid whether a column can be edited or only
shown.

`Polaris.Blazor.DataGrid` · `internal readonly record struct` · not a component, not constructible outside the assembly

## Members

| Member | Signature | What it does |
|---|---|---|
| Constructor | `PolarisGridMemberAccess(string? MemberName, Type ValueType, Func<object?, object?> Getter, Action<object?, object?>? Setter)` | Positional record struct; the four members below are the parameters. |
| `MemberName` | `string? MemberName` | The dotted member path, such as `Name` or `Metal.Name`, or **null** when the expression is not rooted at the row — a computed value rather than a property. |
| `ValueType` | `Type ValueType` | The type of the value the expression produces, read from the expression body **without** the object boxing. This is what filter values are coerced against. |
| `Getter` | `Func<object?, object?> Getter` | The compiled accessor. Reflection never runs per cell, so a page of rows pays for a delegate call only. |
| `Setter` | `Action<object?, object?>? Setter` | Writes a value back onto the record, or null when the member cannot be written. This is the difference between a column that can be edited and one that can only be shown. |

## Behaviour

- **`MemberName` doubles as the column's default id.** A column declared without an explicit id takes
  this name, which is why a typo in a hand-written id cannot make a filter silently point at nothing —
  the filter and the column are derived from the same expression.
- **`Setter` is the only signal of writability at this level.** It is null for a computed expression, a
  get-only property and a `readonly` field, and non-null for a settable property — including one with
  a private setter and including a positional record's `init` accessor.
- `ValueType` is the *unboxed* body type, so a column over `int` reports `int`, not `object`; the
  engine relies on that when it coerces a filter's text into the column's own type.
- The struct is internal: it is the engine's plumbing, not an extension point. Callers see the effect
  through the column, never through this type.

## Gotchas

- **A non-null `Setter` does not make a column editable.** The column's own `Editable` flag has to
  allow it as well, and the grid has to be in editing mode; the setter is a requirement, not a
  permission.
- **A null `MemberName` is silent.** It does not raise, warn or render differently — the column simply
  has no default id, so an explicitly declared id becomes required, and a descriptor naming the member
  path finds no column at all.
- **A null `Setter` is equally silent.** An `Editable` column over a computed expression renders its
  editor, accepts typing and then discards the commit unless the column supplies a `ValueSetter`.
- The `Getter` takes and returns `object?`, so value types are boxed on every read. That is the price
  of one delegate serving every column type, and it is why `ValueType` is carried alongside rather than
  inferred from the returned value.
- Being `internal`, it cannot be constructed, mocked or compared by a consumer — there is no seam here
  to fake a column's getter. A test that needs a different value arranges the underlying object
  instead, or supplies the column's `ValueSetter`.
- `PolarisGridColumnDefinition<TItem>` is what consumes these values: its `Id` defaults to
  `MemberName`, and its `Editable` and `ValueSetter` decide what is actually done with `Setter`.

## See also

- [PolarisGridExpression.cs.md](PolarisGridExpression.cs.md) — the only producer of this value.
- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — reads `ValueGetter` and `ValueType`
  through the column.
- [PolarisGridState.cs.md](PolarisGridState.cs.md) — the state that owns the registered columns.

