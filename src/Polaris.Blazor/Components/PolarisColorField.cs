using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-color-field&gt;</c>.</summary>
/// <remarks>
/// The field accepts hex, RGB and HSL input but always emits hex, so the stored value is normalized
/// for you: <c>#RRGGBB</c>, or eight digits when <see cref="Alpha"/> is set.
/// </remarks>
public sealed class PolarisColorField : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-color-field";

    /// <summary>Visible field label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Placeholder shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>The colour as a hex string. Set as a DOM property after the element upgrades.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Initial colour only. Changing this after the first render has no effect.</summary>
    [Parameter] public string? DefaultValue { get; set; }

    /// <summary>Adds an opacity control and makes the emitted value eight-digit hex.</summary>
    [Parameter] public bool Alpha { get; set; }

    /// <summary>Autofill hint: <c>on</c> or <c>off</c>.</summary>
    [Parameter] public string? Autocomplete { get; set; }

    /// <summary>Form field name, used when a native form submits the field.</summary>
    [Parameter] public string? Name { get; set; }

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

    /// <summary>Raised when the colour is committed.</summary>
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
        attributes["alpha"] = PolarisAttributeValue.Format(Alpha);
        attributes["autocomplete"] = Autocomplete;
        attributes["required"] = PolarisAttributeValue.Format(Required);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["readOnly"] = PolarisAttributeValue.Format(ReadOnly);
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);

        attributes["defaultValue"] = Value ?? DefaultValue;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Value is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = Value };
}
