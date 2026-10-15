using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-money-field&gt;</c>.</summary>
/// <remarks>
/// The value is a string, not a decimal: the element formats and displays it for the merchant's
/// locale, so parse it with the currency's own rules rather than a fixed scale. A zero-decimal
/// currency such as JPY has no fractional part at all.
/// </remarks>
public sealed class PolarisMoneyField : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-money-field";

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

    /// <summary>Lowest accepted amount.</summary>
    [Parameter] public double? Min { get; set; }

    /// <summary>Highest accepted amount.</summary>
    [Parameter] public double? Max { get; set; }

    /// <summary>
    /// Currency the amount is in: an ISO 4217 code, or <c>auto</c> to use the shop's currency.
    /// </summary>
    /// <remarks>Polaris never converts between currencies, so this only affects formatting.</remarks>
    [Parameter] public string? CurrencyCode { get; set; }

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

    /// <summary>Raised when the amount is committed.</summary>
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
        attributes["min"] = PolarisAttributeValue.Format(Min);
        attributes["max"] = PolarisAttributeValue.Format(Max);
        attributes["currencyCode"] = CurrencyCode;
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
