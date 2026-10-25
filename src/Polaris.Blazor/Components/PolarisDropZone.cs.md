<!-- summary-of: PolarisDropZone.cs -->
<!-- source-sha256: 44a317fc1ffe -->

# PolarisDropZone.cs

A drag-and-drop target for uploading files, wrapped around `s-drop-zone`. Polaris checks the file
*type* against `Accept` and nothing else: size, count and content are the application's problem, and
the change event does not upload anything by itself.

`Polaris.Blazor.Components` · renders `<s-drop-zone>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible label for the zone. |
| `Error` | `string?` | `null` | Validation message; setting it also puts the zone in its error state. |
| `Accept` | `string?` | `null` | Comma-separated file types the zone accepts, for example `.jpg,.png,.gif`. Anything else is rejected and raises `OnDropRejected`. |
| `Multiple` | `bool` | `false` | Allows more than one file. Without it a second drop replaces the first, silently discarding the earlier file. |
| `Value` | `string?` | `null` | The zone's submitted value. In practice it can only *reset* the zone: see the gotchas. |
| `Required` | `bool` | `false` | Marks the zone as required. A hint to the browser and to assistive technology, not a check this wrapper performs. |
| `Disabled` | `bool` | `false` | Prevents interaction. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the files. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | Hides the label from sighted users while keeping it for screen readers (`Exclusive`), or from both (`Hidden`). |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnChange` | `EventCallback<PolarisEventArgs>` | Files are chosen or dropped. It does **not** mean they were uploaded, and it fires before any of your code has read a byte. |
| `OnInput` | `EventCallback<PolarisEventArgs>` | The file list changes, including removals. |
| `OnDropRejected` | `EventCallback<PolarisEventArgs>` | Files were rejected, for example because their type is not in `Accept`. |

## Behaviour that is not visible in the markup

- **The element has a `files` property, and this wrapper deliberately does not model it.** It is an
  array of browser `File` objects, which cannot cross the .NET interop boundary. So the upload path
  belongs in the browser (JS), and C# receives `Value` and the events rather than the bytes.
- **There is no `defaultValue` attribute here**, unlike the text-like fields. `Value` is only ever
  written as the `value` DOM property, and only when it is non-null.
- **Selection and upload are separate steps.** A file being present in the zone says nothing about it
  having reached the server, so a form that submits on `OnChange` submits before anything has been
  read.

## Gotchas

- **`Value` cannot put files into the zone.** Once the element holds files its `value` is read-only, so
  the only useful write is a reset: set `Value` to the empty string to clear the zone. Setting a
  filename or a URL does nothing visible.
- **Setting `Value` back to `null` does not clear the zone either** — the property is skipped when the
  value is null and no cleared property is declared. `""` is the value that clears.
- **`Accept` is the only validation Polaris performs.** There is no size limit, no file-count limit and
  no content sniffing, and a rejected type only surfaces as `OnDropRejected`; a caller that ignores
  that callback sees a drop do nothing at all.
- **Without `Multiple`, a second drop replaces the first** rather than being refused, so a merchant who
  drags two files loses one with no error.
- **`Required` is a client-side hint, not a guarantee.** Validate the uploaded set server-side.

## See also

- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — how `value` is applied as a property.
- [PolarisImage.cs.md](PolarisImage.cs.md) — the display side of an uploaded file.
- [PolarisDateField.cs.md](PolarisDateField.cs.md) — a field whose constraints, like `Accept`, do not validate anything by themselves.

`PolarisAccessibilityVisibility` (used by `LabelAccessibilityVisibility`) is defined in
`src/Polaris.Blazor/PolarisEnums.cs`.
