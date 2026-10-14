using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-select&gt;</c>. Single-select only; use a choice list for multi-select.</summary>
public sealed class PolarisSelect : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-select";

    /// <summary>Visible field label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Text shown when nothing is selected.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>The selected option's value. Set as a DOM property after upgrade.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Name of a predefined admin icon shown inside the field.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Form field name.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Marks the field as required.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Prevents interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised when the selection is committed. Read <c>args.Value</c>.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised on every change to the selection.</summary>
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
        attributes["placeholder"] = Placeholder;
        attributes["icon"] = Icon;
        attributes["name"] = Name;
        attributes["required"] = PolarisAttributeValue.Format(Required);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Value is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = Value };
}
