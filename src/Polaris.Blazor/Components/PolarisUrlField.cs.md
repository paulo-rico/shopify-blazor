<!-- summary-of: PolarisUrlField.cs -->
<!-- source-sha256: f252788f8ef3 -->

# PolarisUrlField.cs

A URL input with the standard field chrome — label, help text, validation message, placeholder, length
limits — rendered as `<s-url-field>`. Neither Polaris nor the browser checks a URL usefully, so the value
still has to be normalised and validated on the server.

`Polaris.Blazor.Components` · renders `<s-url-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label, as `label`. |
| `Details` | `string?` | `null` | Help text below the field, as `details`. |
| `Error` | `string?` | `null` | Validation message shown under the field, as `error`. This wrapper does not act on it — it neither validates nor blocks submission. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The value. Rendered as `defaultValue` for the first paint **and** written as the `value` DOM property after the element upgrades, which is the authoritative copy. Null writes no property at all, so the field keeps whatever it holds. |
| `DefaultValue` | `string?` | `null` | Initial value only; changing it after the first render has no effect. It is only used when `Value` is null — the markup fallback is `Value ?? DefaultValue`. |
| `Name` | `string?` | `null` | Form field name, so a native form submits the field. |
| `Required` | `bool` | `false` | Marks the field required. A presence attribute: bare when true, absent when false. |
| `Disabled` | `bool` | `false` | Prevents interaction. Presence attribute. |
| `ReadOnly` | `bool` | `false` | Shows the value but refuses edits. Presence attribute, emitted as `readOnly` with a capital O. |
| `MaxLength` | `int?` | `null` | Maximum accepted length, formatted invariantly. Null omits the attribute and leaves the element's own default. |
| `MinLength` | `int?` | `null` | Minimum accepted length. Null omits the attribute. |
| `Autocomplete` | `string?` | `null` | Autofill hint for the browser, e.g. `url`. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (hidden from sighted users, still announced) or `Hidden` (removed from the accessibility tree). Null omits the attribute. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` — see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md). There is no `ChildContent`
and no prefix/suffix parameter here.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` holds the current text. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The value is committed — on blur or Enter. This, not `OnInput`, is the validation signal. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour

- **`Value` reaches the element twice, deliberately.** As `defaultValue` in markup so the first paint
  shows the model's value before the custom element upgrades, then as the `value` property, because
  Polaris does not reflect `value` into an attribute. The property wins once the element is live.
- **The typed text is not the .NET value.** The element reports its value as a string
  (`PolarisEventArgs.Value`), so parse, normalise and validate in `OnChange` rather than trusting the
  markup that was bound.
- **`Required`, `Disabled` and `ReadOnly` go through the presence-attribute rule.** `true` renders the
  bare attribute and `false` removes it, because `disabled="false"` is still disabled to a custom
  element.

## Gotchas

- **Nothing here validates a URL.** The source says the browser's URL check is weak and vendor-specific,
  Polaris prepends no scheme (`example.com` stays `example.com`) and encodes nothing, so `"not a url"` can
  reach your handler. Normalise and validate server-side, and do not treat `s-url-field` as a guard.
- **Setting `Value` back to `null` does not clear the field.** Null means "write no `value` property"
  (the property dictionary is empty), and this wrapper declares no cleared properties, so the element
  keeps its last text. Bind `""` to empty it.
- **`DefaultValue` is silently overridden.** The markup fallback is `Value ?? DefaultValue`, so a
  component that sets both gets one value in the markup and the other as the live property, and the
  default is never seen.
- **`MaxLength` and `MinLength` are attributes, not enforcement.** They are formatted as invariant
  integers, so a comma decimal separator cannot break the attribute, but nothing in this wrapper checks
  either limit; the element and the browser decide what to do with them.
- **`LabelAccessibilityVisibility="Hidden"` removes the label from assistive technology as well**, so the
  field is left without an accessible name unless `AccessibilityLabel` is set. Use `Exclusive` to hide it
  only visually.

## See also

- [PolarisEmailField.cs.md](PolarisEmailField.cs.md) — the same field contract for email addresses.
- [PolarisTextField.cs](PolarisTextField.cs) — the plain single-line field.
- [PolarisLink.cs.md](PolarisLink.cs.md) — where a validated URL usually ends up being rendered.

`PolarisAccessibilityVisibility` is defined in `src/Polaris.Blazor/PolarisEnums.cs`.
