using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-text-area&gt;</c>.</summary>
/// <remarks>
/// The value is a property, never the element's child text. A native
/// <c>&lt;textarea&gt;content&lt;/textarea&gt;</c> takes its initial value from its children, so
/// copying that habit here submits an empty string and nothing warns you.
/// </remarks>
public sealed class PolarisTextArea : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-text-area";

    /// <summary>Visible field label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Placeholder shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>The field's value.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Initial value only.</summary>
    [Parameter] public string? DefaultValue { get; set; }

    /// <summary>Form field name.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Visible height, in text rows.</summary>
    [Parameter] public int? Rows { get; set; }

    /// <summary>Maximum accepted length.</summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary>Minimum accepted length.</summary>
    [Parameter] public int? MinLength { get; set; }

    /// <summary>Marks the field as required.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Prevents editing.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the value but prevents editing.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised on every keystroke.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <summary>Raised when the value is committed.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised when the field receives focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the field loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Input, OnInput);
        events.On(PolarisEvents.Change, OnChange);
        events.On(PolarisEvents.Focus, OnFocus);
        events.On(PolarisEvents.Blur, OnBlur);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["label"] = Label;
        attributes["details"] = Details;
        attributes["error"] = Error;
        attributes["placeholder"] = Placeholder;
        attributes["name"] = Name;
        attributes["rows"] = PolarisAttributeValue.Format(Rows);
        attributes["maxLength"] = PolarisAttributeValue.Format(MaxLength);
        attributes["minLength"] = PolarisAttributeValue.Format(MinLength);
        attributes["required"] = PolarisAttributeValue.Format(Required);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["readOnly"] = PolarisAttributeValue.Format(ReadOnly);
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);

        // The initial value belongs in markup so the first render is correct even before the
        // element upgrades; afterwards the property is authoritative.
        attributes["defaultValue"] = Value ?? DefaultValue;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Value is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = Value };
}
