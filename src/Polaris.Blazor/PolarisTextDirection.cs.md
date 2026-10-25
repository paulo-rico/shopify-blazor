<!-- summary-of: PolarisTextDirection.cs -->
<!-- source-sha256: 8f2ff1dc0d24 -->

# PolarisTextDirection.cs

The three values of the HTML `dir` attribute — browser-detected, left-to-right and right-to-left — with "unset" deliberately absent, so call sites use a nullable.

`Polaris.Blazor` · enum for a `dir` attribute · serialized through `PolarisAttributeValue.EnumValue`

## Members

| Member | Wire value | What it means |
|---|---|---|
| `Auto` | `auto` | The browser decides from the content's first strong character. |
| `Ltr` | `ltr` | Force left-to-right. |
| `Rtl` | `rtl` | Force right-to-left. |

## Behaviour

- Appears as `PolarisTextDirection? Dir` on `PolarisText`, `PolarisParagraph` and `PolarisNumber`. A `null` value drops the attribute entirely, so the element inherits its direction; a non-null value always emits `dir="…"`.
- Values are pinned with `[PolarisValue]`, which survives the generic enum conversion even if a member is renamed. The wire spellings are the HTML ones, lower-case, and are what `PolarisAttributeValue.EnumValue` returns.

## Gotchas

- **There is no `Unset`/`Inherit` member; `null` is the only way to say it, and `Auto` is not it.** `Auto` writes `dir="auto"`, which asks the browser to inspect the element's own content — a different outcome from doing nothing and inheriting the page's direction, especially on an element whose text is not the page's text.
- Because the type is nullable everywhere it is used, a wrapper must not default its `Dir` property to `Auto`: that would put `dir="auto"` on every instance and quietly override inheritance for all of them.
- Only `PolarisText`, `PolarisParagraph` and `PolarisNumber` expose `Dir`; every other component takes its direction from an ancestor, because there is no parameter to set on it.

## See also

- [PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md) — where a `null` parameter becomes an absent attribute.
- [PolarisIcon.cs.md](Components/PolarisIcon.cs.md) — the same enum-to-attribute conversion applied to `Tone`, `Color` and `Size`.

`PolarisText`, `PolarisParagraph` and `PolarisNumber` are the components that expose `Dir`; their summaries are written alongside their own source files.
