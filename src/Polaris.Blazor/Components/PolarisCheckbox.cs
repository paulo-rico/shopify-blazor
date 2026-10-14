using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-checkbox&gt;</c>.</summary>
/// <remarks>
/// Use a checkbox when the choice is submitted explicitly; use a switch for a setting that applies
/// immediately.
/// </remarks>
public sealed class PolarisCheckbox : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-checkbox";

    /// <summary>Label shown beside the box.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the checkbox.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>
    /// Whether the box is ticked.
    /// </summary>
    /// <remarks>
    /// Written as a DOM property after upgrade. Polaris does not reflect <c>checked</c> as an
    /// attribute, so a server-side change only takes effect through the property.
    /// </remarks>
    [Parameter] public bool Checked { get; set; }

    /// <summary>Initial checked state only.</summary>
    [Parameter] public bool? DefaultChecked { get; set; }

    /// <summary>Renders the mixed state used by a "select all" control.</summary>
    [Parameter] public bool Indeterminate { get; set; }

    /// <summary>Value submitted when the box is ticked.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Form field name.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Marks the checkbox as required.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Prevents interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised when the box is toggled. Read <c>args.Checked</c>.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised on every change to the box.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <summary>Raised when the checkbox loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Change, OnChange);
        events.On(PolarisEvents.Input, OnInput);
        events.On(PolarisEvents.Blur, OnBlur);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["label"] = Label;
        attributes["details"] = Details;
        attributes["error"] = Error;
        attributes["value"] = Value;
        attributes["name"] = Name;
        attributes["required"] = PolarisAttributeValue.Format(Required);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["defaultChecked"] = DefaultChecked is true ? string.Empty : null;
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);

        // `checked` and `indeterminate` are not reflected, so they are only ever properties.
        attributes["checked"] = null;
        attributes["indeterminate"] = null;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["checked"] = Checked,
        ["indeterminate"] = Indeterminate,
    };
}
