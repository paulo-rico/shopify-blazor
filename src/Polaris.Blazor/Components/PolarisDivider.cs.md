<!-- summary-of: PolarisDivider.cs -->
<!-- source-sha256: 650a3c887a5c -->

# PolarisDivider.cs

A rule between blocks or between items — horizontal by default, vertical when asked. Shopify's names
describe the axis the divider *spans* rather than the axis it separates, so `Inline` is the horizontal
rule and is both the element's default and this wrapper's.

`Polaris.Blazor.Components` · renders `<s-divider>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Direction` | `PolarisDividerDirection` | `Inline` | Which axis the rule spans. `Inline` is a horizontal rule between stacked blocks; `Block` is a vertical rule between items laid out horizontally. |
| `Color` | `PolarisColor` | `Base` | Rule contrast: `Base` for standard, `Subdued` for a quieter separation, `Strong` for a heavier one. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

No events: a divider reports nothing back.

## Behaviour that is not visible in the markup

- **Both attributes are always written**, even at their defaults, so the element receives
  `direction="inline"` and `color="base"` rather than relying on its own defaults. Enum values are
  lower-cased through `PolarisAttributeValue.EnumValue`, which is why the markup reads `inline`/`block`
  and `base`/`subdued`/`strong`.
- **The direction names are the trap, not the styling.** `Inline` runs along the inline axis, so it is
  the horizontal line under a heading. A vertical rule between two buttons in an inline stack is
  `Block` — the opposite of what "block" suggests at first reading.

## Gotchas

- The wrapper adds no layout of its own: it renders one element with two attributes, so whether the
  rule is wide, tall or visible at all is decided entirely by the stack or grid it sits in. A `Block`
  divider in a container that stacks vertically is the case that looks broken and is not.
- It carries no text, so it must not be the only thing conveying a grouping to a screen reader; the
  accessibility name, if any, belongs on the section or list around it.
- There is no thickness or spacing parameter — `Color` is the only styling knob, and any border or
  margin beyond it has to come from a class passed through `AdditionalAttributes`.

## See also

- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — how the enum attributes are formatted.
- [PolarisButton.cs.md](PolarisButton.cs.md) — another single-element wrapper, for the attribute and event conventions.

`PolarisDividerDirection` and `PolarisColor` are defined in `src/Polaris.Blazor/PolarisEnums.cs`; the
container that decides a divider's axis in practice is `PolarisStack`.
