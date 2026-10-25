<!-- summary-of: PolarisAttributeValue.cs -->
<!-- source-sha256: c92e2c832af7 -->

# PolarisAttributeValue.cs

The one place a .NET parameter value becomes the string a Polaris custom element expects: enums to
wire spelling, booleans to presence attributes, numbers to invariant text. Every wrapper's
`BuildAttributes` and the caller's `AdditionalAttributes` go through it.

`Polaris.Blazor` · `static class` of conversions · also declares `PolarisValueAttribute`

## Members

| Member | Signature | What it does |
|---|---|---|
| `Format` | `static string? Format(object? value)` | The single conversion the wrappers use. `null` → `null` (attribute omitted); `bool true` → `""` (bare attribute); `bool false` → `null` (attribute removed); `string` → unchanged; `Enum` → `EnumValue`; `IFormattable` → `ToString(null, InvariantCulture)`; anything else → `ToString()`. |
| `IsBare` | `static bool IsBare(object? value)` | True only for a boxed boolean `true`. A string `"true"` is not bare. |
| `EnumValue` | `static string EnumValue(Enum value)` | The member's `[PolarisValue]` string when it has one, otherwise `ToCamelCase` of the member name. |
| `ToCamelCase` | `static string ToCamelCase(string name)` | Lower-cases the **first character only**; a name that is null, empty or already lower-case is returned untouched. |
| `PolarisValueAttribute` | `sealed class`, ctor `(string value)`, read-only `string Value` | Attribute applied to an enum field to pin the exact string Shopify documents. `[AttributeUsage(AttributeTargets.Field)]`. |

## What a value becomes in the markup

| Parameter value | `Format` returns | Rendered as |
|---|---|---|
| `true` | `""` | a bare attribute (`disabled`) — the only correct spelling of a Polaris boolean |
| `false` | `null` | nothing at all; the attribute is absent |
| `null` | `null` | nothing at all |
| `"critical"` (string) | `"critical"` | `tone="critical"` |
| `PolarisTone.Critical` | `"critical"` | `tone="critical"` |
| `1.5m` | `"1.5"` | `value="1.5"`, even on a machine whose decimal separator is a comma |
| `(PolarisTone)99` | `"99"` | `tone="99"` — a value Shopify does not know |

## Behaviour

- **Attribute names are never mapped.** Polaris attribute names are camelCase
  (`labelAccessibilityVisibility`, `borderRadius`), so a wrapper's parameter name is already the
  attribute name and there is no table to fall out of date. Only *values* need converting, which is
  all this class does.
- `Format` is the funnel for two different paths: `PolarisElementComponentBase.BuildAttributes` runs
  every entry of the caller's `AdditionalAttributes` through it, and each wrapper runs its own
  parameters through `Format` or `EnumValue` explicitly.
- The empty-string-for-`true` convention is resolved later, in `BuildRenderTree`: a value of length
  zero is emitted as `AddAttribute(key, true)`, which Blazor renders as a valueless attribute. `Format`
  itself never returns markup.
- The two enum helpers are part of the public API because a wrapper may need the wire string without
  an attribute — `PolarisButton.Properties` writes `accessibilityExpanded` as a DOM property and
  still calls `EnumValue`.

## Gotchas

- **`EnumValue`'s XML doc claims it throws `ArgumentException` when a member cannot be converted;
  the code never throws.** It falls back to `ToCamelCase`, so a multi-word member without a
  `[PolarisValue]` becomes silently wrong markup — `FitContent` would come out as `fitContent`, not
  the documented `fit-content`. This is why every enum in the library pins each member.
- **`ToCamelCase` is a first-letter operation, not a hyphenation rule.** `Auto` → `auto` is correct by
  luck; `SemiBold` → `semiBold`, `Large100` → `large100`, `FitContent` → `fitContent` are all wrong
  for Polaris. No convention can derive `large-100` or `semibold`, hence the attribute.
- **`false` never reaches the markup as text.** `Format(false)` removes the attribute; emitting
  `disabled="false"` would still be true to a custom element. A hand-written string `"false"` in
  `AdditionalAttributes` is therefore a live bug, not a no-op.
- **`IsBare` is public but nothing in the library calls it** — the base class uses the
  empty-string convention instead. It exists for callers building attributes themselves, and it answers
  true only for the actual boolean `true`; the string `"true"` and every other truthy value answer
  false.
- **Enum lookup is reflection on every call** (`GetMember(value.ToString())`), so it runs once per
  enum attribute per render. Cheap, but it is not cached.
- A combined `[Flags]` value or a cast-to-number enum has no member to find, so it stringifies as
  its numeric or comma-joined form and flows straight into a Shopify attribute.
- Invariant culture is not cosmetic: without it a comma-decimal machine emits `value="1,5"` and
  Shopify rejects the value with nothing visible in the log.

## See also

- [PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md) — where `Format`'s `null`,
  empty and non-empty results become omitted, bare and valued attributes.
- [PolarisEnums.cs.md](PolarisEnums.cs.md) — the enums this class converts, all pinned.
- [PolarisTextDirection.cs.md](PolarisTextDirection.cs.md) — the same conversion applied to the HTML
  `dir` values.
- [PolarisAccessibilityExpanded.cs.md](PolarisAccessibilityExpanded.cs.md) — a two-member enum whose
  strings Polaris compares rather than merely records.
