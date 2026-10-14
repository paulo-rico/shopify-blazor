using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-switch&gt;</c>, for a setting that applies immediately.</summary>
public sealed class PolarisSwitch : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-switch";

    /// <summary>Label shown beside the switch.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the switch.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Whether the switch is on. Set as a DOM property after upgrade.</summary>
    [Parameter] public bool Checked { get; set; }

    /// <summary>Initial state only.</summary>
    [Parameter] public bool? DefaultChecked { get; set; }

    /// <summary>Value submitted when the switch is on.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Form field name.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Prevents interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised when the switch is toggled. Read <c>args.Checked</c>.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised on every change.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Change, OnChange);
        events.On(PolarisEvents.Input, OnInput);
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
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["defaultChecked"] = DefaultChecked is true ? string.Empty : null;
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);
        attributes["checked"] = null;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["checked"] = Checked,
    };
}
