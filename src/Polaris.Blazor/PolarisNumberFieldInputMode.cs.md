<!-- summary-of: PolarisNumberFieldInputMode.cs -->
<!-- source-sha256: 6a4c1574a8fc -->

# PolarisNumberFieldInputMode.cs

The on-screen keyboard an `s-number-field` asks for: one with a decimal separator, or a digits-only one.
It is a hint to the keyboard and nothing else — the field's value stays a string, no input is rejected,
and the server still has to parse it.

`Polaris.Blazor` · enum for `PolarisNumberField.InputMode` · wire spellings pinned with `[PolarisValue]`

## Members

| Member | Wire value | What it does |
|---|---|---|
| `Decimal` | `decimal` | Requests a keyboard that offers a decimal separator. The default on `PolarisNumberField`. |
| `Numeric` | `numeric` | Requests a digits-only keyboard. |

## Behaviour

- `PolarisNumberField.InputMode` defaults to `Decimal` and is written on every render as
  `inputMode="decimal"`, so the attribute is always present rather than omitted for the platform to
  guess.
- The enum's own remark is the whole contract: it is a keyboard hint only. It does not restrict what
  can be typed, so a server-side parse must still handle junk. A hardware keyboard, a paste or a
  barcode scanner delivers whatever it likes to either member.
- It does not change the type of the value. `PolarisNumberField.Value` is a `string?` in both modes,
  because that is what the element emits and what a partially typed number looks like — `"-"`, `"1."`
  and `""` are all legitimate states of a field on its way to a decimal.
- It has no bearing on formatting. The caller decides what text goes into `Value`; the enum only asks
  for a keyboard, and an unparseable string is just as unparseable on either one.

## Gotchas

- **`Numeric` takes away the decimal separator that `Decimal` exists to offer.** A field bound to a
  price, a percentage or a weight should stay `Decimal`; a digits-only keyboard makes a fractional value
  untypeable, which reads to the merchant as a field that rejects valid input.
- **Neither member validates anything.** The name promises a number field, the keyboard is a hint, and
  the value is a string — `decimal.TryParse` with the invariant culture is still the caller's job, and a
  parse that trusts the keyboard will meet a pasted `12abc` or an empty string.
- **A number field is the wrong control for an identifier.** A SKU, a barcode or an order name may
  contain letters, and this enum cannot make the field accept them; the remark points at a text field
  instead, and `InputMode` does not exist on those components at all.
- **Only two members exist** — there is no `text`, `tel` or `search`, because this type is the number
  field's own `inputMode` and not a general-purpose HTML input-mode enum.
- **The wire strings would survive the camel-case fallback**, so the `[PolarisValue]` pins here are
  convention rather than a correction; adding a member with more than one word would need one.

## See also

- [PolarisNumberField.cs.md](Components/PolarisNumberField.cs.md) — the component, including `Step`,
  `Min`, `Max`, the `value` DOM property and the `OnInput`/`OnChange` split.
- [PolarisNumberRange.cs.md](PolarisNumberRange.cs.md) — the interval the range controls bind, which a
  number field is usually one end of.
- [PolarisRangeSlider.cs.md](Components/PolarisRangeSlider.cs.md) — where the two numeric fields live,
  and where the "a field's value is a plain number" rule is written down.
