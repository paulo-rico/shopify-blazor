<!-- summary-of: PolarisButtonInlineSize.cs -->
<!-- source-sha256: 631478b2691a -->

# PolarisButtonInlineSize.cs

How an `s-button` occupies the line it sits on: sized by its content, stretched across the available
inline space, or fitted to its content exactly. It is the `inlineSize` attribute on the button.

`Polaris.Blazor` · enum for `PolarisButton.InlineSize` · wire spellings pinned with `[PolarisValue]`

## Members

| Member | Wire value | What it does |
|---|---|---|
| `Auto` | `auto` | Shopify decides from the button's context. The default on `PolarisButton`. |
| `Fill` | `fill` | Stretches to fill the available inline space — the full-width button in a stack or dialog. |
| `FitContent` | `fit-content` | Exactly as wide as its content requires. |

## Behaviour

- Exposed as `PolarisButton.InlineSize`, defaulting to `Auto`; the wrapper always writes the
  attribute, so the markup says `inlineSize="auto"` rather than omitting it.
- Used inside the library itself by `PolarisSuggestFieldInline`, which sets `Fill` on its two
  row-level buttons so each spans the suggestion row.
- The width is a property of the button, not of its container: a page that wants a full-width action
  sets `InlineSize="PolarisButtonInlineSize.Fill"` on the button itself, rather than relying on the
  stack or dialog around it to stretch it.

## Gotchas

- **`FitContent` must stay pinned.** `PolarisAttributeValue.ToCamelCase` only lower-cases the first
  letter, so without `[PolarisValue("fit-content")]` the member would emit `fitContent`, which Polaris
  does not recognise and silently ignores — the button would just look auto-sized.
- **There is no `Block`, `Full` or `Stretch` member.** `Fill` is the whole-width value; the names in
  this enum are exactly the three Shopify documents.
- **`Auto` is not "omit the attribute".** It is a real value (`auto`) that is always written, which
  matters if you were expecting the element to inherit a width from CSS: the attribute is present on
  every button.

## See also

- [PolarisButton.cs.md](Components/PolarisButton.cs.md) — the only component that exposes this as a
  parameter, alongside `Variant`, `Tone` and `Command`.
- [PolarisEnums.cs.md](PolarisEnums.cs.md) — the shared enums, including the sizes and variants a
  button also takes.
- [PolarisAttributeValue.cs.md](PolarisAttributeValue.cs.md) — why a pinned wire value matters.
