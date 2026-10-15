using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-date-field&gt;</c>, a single date with a picker.</summary>
/// <remarks>
/// <para>
/// A range is written as two ISO dates separated by <c>--</c>, for example
/// <c>2025-05-28--2025-05-31</c>. <see cref="View"/> sets which month is shown, as <c>YYYY-MM</c>.
/// </para>
/// <para>
/// The allow and disallow properties take the same date ranges as the value, and the day variants
/// take weekday names. They constrain the picker; they do not stop a typed value, so validate the
/// committed date on the server.
/// </para>
/// </remarks>
public sealed class PolarisDateField : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-date-field";

    /// <summary>Visible field label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Placeholder shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>The date, or a <c>start--end</c> range. Set as a DOM property after the element upgrades.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Initial date only. Changing this after the first render has no effect.</summary>
    [Parameter] public string? DefaultValue { get; set; }

    /// <summary>Form field name, used when a native form submits the field.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Month to show, as <c>YYYY-MM</c>.</summary>
    [Parameter] public string? View { get; set; }

    /// <summary>Month shown on first render, as <c>YYYY-MM</c>.</summary>
    [Parameter] public string? DefaultView { get; set; }

    /// <summary>
    /// Dates the merchant may choose: a comma-separated list of <c>YYYY-MM-DD</c>, <c>YYYY-MM</c>,
    /// <c>YYYY</c>, or a <c>start--end</c> range.
    /// </summary>
    /// <remarks>An omitted range end means "and later": <c>2025-01-01--</c>.</remarks>
    [Parameter] public string? Allow { get; set; }

    /// <summary>Dates the merchant may not choose, in the same syntax as <see cref="Allow"/>.</summary>
    [Parameter] public string? Disallow { get; set; }

    /// <summary>Weekdays that may be chosen, as a comma-separated list of full names such as <c>monday</c>.</summary>
    [Parameter] public string? AllowDays { get; set; }

    /// <summary>Weekdays that may not be chosen, as a comma-separated list of full names.</summary>
    [Parameter] public string? DisallowDays { get; set; }

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

    /// <summary>Raised when the date is committed.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised when the field receives focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the field loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <summary>Raised when the entered text is not a valid date.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInvalid { get; set; }

    /// <summary>Raised when the visible month changes, for example while paging through the picker.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnViewChange { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Input, OnInput);
        events.On(PolarisEvents.Change, OnChange);
        events.On(PolarisEvents.Focus, OnFocus);
        events.On(PolarisEvents.Blur, OnBlur);
        events.On(PolarisEvents.Invalid, OnInvalid);
        events.On(PolarisEvents.ViewChange, OnViewChange);
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
        attributes["view"] = View;
        attributes["defaultView"] = DefaultView;
        attributes["allow"] = Allow;
        attributes["disallow"] = Disallow;
        attributes["allowDays"] = AllowDays;
        attributes["disallowDays"] = DisallowDays;
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
