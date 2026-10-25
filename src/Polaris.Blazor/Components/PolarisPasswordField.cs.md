<!-- summary-of: PolarisPasswordField.cs -->
<!-- source-sha256: 02e886df0078 -->

# PolarisPasswordField.cs

A masked text field for a secret, rendered as `<s-password-field>`, with a visible label, help text and
error state, and four forwarded events. It has no built-in reveal control, so a "show password"
affordance has to be built from a button that toggles between this field and a text field.

`Polaris.Blazor.Components` · renders `<s-password-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible label drawn inside the field's own frame. Fields are self-labelling, so do not add a sibling `<label>`. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message; a non-null value puts the field in its error presentation, so clearing it clears both. |
| `Placeholder` | `string?` | `null` | Placeholder shown while the field is empty. |
| `Value` | `string?` | `null` | The current secret. Also rendered as `defaultValue` for first paint, and written as the `value` DOM property after the element upgrades. Null writes no property at all. |
| `DefaultValue` | `string?` | `null` | First-paint value only, used when `Value` is null. Changing it after the first render has no effect. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Required` | `bool` | `false` | Marks the field required. Presence attribute: `false` removes it rather than writing `"false"`. |
| `Disabled` | `bool` | `false` | Prevents editing. |
| `ReadOnly` | `bool` | `false` | Shows the value but prevents editing. Polaris offers no further distinction here, so choosing between `ReadOnly` and `Disabled` is a design decision about how unavailable the field should look. |
| `MaxLength` | `int?` | `null` | Maximum accepted length. Omitted when null, formatted with the invariant culture when set. |
| `MinLength` | `int?` | `null` | Minimum accepted length — a hint to the browser, **not** a rule the server can rely on. Same formatting. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed straight through to the browser. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (announced, not drawn) or `Hidden`. Null drops the attribute and leaves Polaris's own default. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)). There is **no
`ChildContent`** — the field takes its value from the parameter, never from child text, which is the
opposite of the native `<input>` habit.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` is the field's text as it now stands — the secret, in a string. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The value is committed: blur or Enter. This is the "the merchant has finished" signal. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour

- **The value is split across two mechanisms.** `BuildAttributes` always writes
  `defaultValue = Value ?? DefaultValue`, so a server-rendered or not-yet-upgraded field shows the right
  placeholder text; the live value is then written as the `value` DOM property in `OnAfterRenderAsync`,
  because Polaris does not reflect `value` and the attribute would freeze at first paint. The attribute
  is rebuilt on every parameter pass, so `Value` wins over `DefaultValue` for first paint whenever both
  are set.
- **A null `Value` writes no property.** The shared empty property set is used, so the merchant's typing
  survives a re-render that carries null. That is what makes the field usable uncontrolled — omit the
  `Value` binding entirely.
- **`Error` does double duty**: it is the message *and* the switch that puts the field into its error
  presentation. There is no separate `Invalid` flag.
- **Booleans are presence attributes** through `PolarisAttributeValue.Format`, and the one nullable enum
  goes through `EnumValue` to Shopify's lower-case spelling. Numbers are formatted with the invariant
  culture, so `MaxLength = 12` renders `"12"` on any machine locale.
- **Events are mapped by DOM name** — `input`, `change`, `focus`, `blur` — so a callback is true to the
  event it claims to be, unlike the native `onchange` which fires on blur only.
- **No reveal control, no strength meter, no icon, prefix or suffix.** The mask is the element's own and
  cannot be toggled from here: there is no `Type`, `Reveal` or `ShowPassword` parameter, so the
  affordance is a button that swaps this component for a text field and back.

## Gotchas

- **`MinLength` and `MaxLength` are browser hints, not validation.** A client that bypasses the page
  submits whatever it likes; the rule has to be repeated server-side. `Required` is in the same
  category.
- **`DefaultValue` is first paint only.** Changing it after the first render does nothing — no error —
  so a value that arrives late (a fetched secret or a form reset) belongs in `Value`.
- **`OnInput` fires per keystroke, with the secret in the args.** Binding it to a request sends the
  password once per character; read `OnChange` for the committed value, and be careful what is logged,
  since `args.Value` is the plaintext.
- **The plaintext lives in the DOM property.** Masking hides the characters on screen, not the value:
  the `value` property, the args and any logging carry the secret in full.
- **`Autocomplete` is not a cosmetic hint.** Omit it and password managers guess; a login form wants
  `"current-password"` and a change-password form wants `"new-password"`, or the browser will offer to
  fill the wrong kind of field.
- **A reveal built by re-rendering loses the cursor.** Swapping components replaces the input, so the
  caret position and selection go with it unless the page owns and restores them; a public "show
  password" toggle is usually better placed beside the field as a checkbox that the page honours itself.
- **Nothing is validated as a password.** There is no confirmation field, no matching check and no
  strength feedback — those are page-level concerns.

## See also

- [PolarisEmailField.cs.md](PolarisEmailField.cs.md) — the same field family, typed for email.
- [PolarisUrlField.cs.md](PolarisUrlField.cs.md) — the same `Value` / `DefaultValue` contract.
- [PolarisSearchField.cs.md](PolarisSearchField.cs.md) — a text field with an `input` event and no
  accessory slots.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes versus DOM
  properties, and the inherited parameters.
