<!-- summary-of: PolarisGridExpression.cs -->
<!-- source-sha256: 7e3e3dfe6fbc -->

# PolarisGridExpression.cs

Reads a column's declaring lambda once, so that rendering a page of rows never has to. It derives the
member path, the value type, a compiled getter and a reflective setter — everything a grid later needs
to know about `item => item.Metal.Name` is decided here, at declaration time, rather than per cell.

`Polaris.Blazor.DataGrid` · `internal static class` · one public method, `Analyse<TItem>`

## Members

| Member | Signature | What it does |
|---|---|---|
| `Analyse<TItem>` | `static PolarisGridMemberAccess Analyse<TItem>(Expression<Func<TItem, object?>> field)` | Compiles the getter, derives the member path and the value type, builds a setter when the member is writable, and returns all four as a `PolarisGridMemberAccess`. Throws `ArgumentNullException` when `field` is null. |

## Behaviour

- **The value type and the member name both come from the expression**, and that is deliberate: the
  type is what filter values are coerced against, and the name is the column's default id — so a typo
  in a hand-written id cannot make a filter silently point at nothing.
- **The getter is compiled; the setter is not.** `field.Compile()` produces the `Func<TItem, object?>`
  because the getter runs once per cell per render; the setter uses `PropertyInfo`/`FieldInfo`
  reflection because it runs once per commit, where the cost is invisible.
- **The boxing conversion is stripped first.** An `object`-typed lambda body is a `Convert` node over
  the member access, so `Unwrap` peels `Convert`/`ConvertChecked` until it reaches the real body.
- **A member path is only built when the chain is rooted at the lambda's own parameter.**
  `item => item.Name` and `item => item.Metal.Name` both qualify; the path is dotted
  (`Metal.Name`), and the values are the CLR member names, not the column ids.
- **The setter is `null` when the member cannot be written**, and that single fact is what
  distinguishes a column that can be edited from one that can only be shown. Three cases produce it:
  a body that is not a member access at all (a computed value), a get-only property, and a `readonly`
  field.
- `init` accessors **count as writable**: a positional record declares them, and a grid over records
  would otherwise have no editable column at all. A caller who would rather produce a new record than
  mutate one says so with the column's `ValueSetter`.
- **The setter is built from the outermost member alone.** For `item => item.Metal.Name` the member is
  `Name` — declaring type `Metal` — while `Setter` is invoked with the row itself as its target. Only
  a direct member of the row can therefore be written through the default setter; a nested column
  needs the column's `ValueSetter`.

## Gotchas

- **A computed expression silently produces a read-only, unnamed column.** `item => item.First + " " +
  item.Last` has no `MemberName` and no `Setter`; the column renders, cannot be edited, and has no
  default id, so a filter declared against its id finds no column and is ignored. The failure is
  invisible: nothing logs, and the column simply never filters or edits.
- **Only a bare member access at the top of the chain is recognised.** The path walk stops at the
  first non-`MemberExpression` node, so `item => item.Name.ToUpper()` yields neither a name nor a
  setter — the method call sits between the member and the lambda.
- **A private setter counts as writable.** `GetSetMethod(nonPublic: true)` is used, so a property
  that looks immutable from the outside is still mutated in place on commit.
- **A `readonly` field gets no setter, but a mutable public field does** — and that write is a plain
  field assignment with no validation hook, unlike a property.
- The getter is typed `Func<TItem, object?>`, so a value-type column is boxed on every cell read. That
  is fine at page sizes the grid allows, but it is not free, and it is why the type is also carried
  separately in `ValueType` for the engine to compare with.
- Descriptors and sorts name columns by **id**, not by member name; the member name is only the
  default id. Renaming a property therefore renames an id that a saved view or a hand-written
  descriptor may still be using.
- The result is consumed by `PolarisGridColumnDefinition<TItem>`, whose declared `ValueSetter`
  overrides the reflective one this file builds.

## See also

- [PolarisGridMemberAccess.cs.md](PolarisGridMemberAccess.cs.md) — the four values this returns.
- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — uses `ValueType` to coerce filter
  values.
