using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-number-field&gt;</c>.</summary>
/// <remarks>
/// The value is a string, because that is what the element emits and what a partially typed number
/// looks like. <see cref="InputMode"/> only chooses the on-screen keyboard, so a server-side parse
/// must still handle non-numeric input. For an identifier that may contain letters, such as a SKU,
/// use a text field instead.
/// </remarks>
public sealed class PolarisNumberField : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-number-field";

    /// <summary>Visible field label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Placeholder shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>The field's value, as a string. Set as a DOM property after the element upgrades.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Initial value only. Changing this after the first render has no effect.</summary>
    [Parameter] public string? DefaultValue { get; set; }

    /// <summary>Form field name, used when a native form submits the field.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Increment the spinner buttons apply.</summary>
    [Parameter] public double? Step { get; set; }

    /// <summary>Lowest accepted value.</summary>
    [Parameter] public double? Min { get; set; }

    /// <summary>Highest accepted value.</summary>
    [Parameter] public double? Max { get; set; }

    /// <summary>Static text before the value, for example a currency symbol.</summary>
    [Parameter] public string? Prefix { get; set; }

    /// <summary>Static text after the value, for example a unit.</summary>
    [Parameter] public string? Suffix { get; set; }

    /// <summary>On-screen keyboard to request. A hint only.</summary>
    [Parameter] public PolarisNumberFieldInputMode InputMode { get; set; } = PolarisNumberFieldInputMode.Decimal;

    /// <summary>Marks the field as required.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Prevents editing.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the value but prevents editing.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Autofill hint passed through to the browser.</summary>
    [Parameter] public string? Autocomplete { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised on every keystroke.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <summary>Raised when the number is committed.</summary>
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
        attributes["step"] = PolarisAttributeValue.Format(Step);
        attributes["min"] = PolarisAttributeValue.Format(Min);
        attributes["max"] = PolarisAttributeValue.Format(Max);
        attributes["prefix"] = Prefix;
        attributes["suffix"] = Suffix;
        attributes["inputMode"] = PolarisAttributeValue.EnumValue(InputMode);
        attributes["required"] = PolarisAttributeValue.Format(Required);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["readOnly"] = PolarisAttributeValue.Format(ReadOnly);
        attributes["autocomplete"] = Autocomplete;
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);

        attributes["defaultValue"] = Value ?? DefaultValue;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Value is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = Value };
}
