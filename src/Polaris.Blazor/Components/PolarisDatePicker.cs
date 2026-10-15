using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-date-picker&gt;</c>, the calendar on its own.</summary>
/// <remarks>
/// With <see cref="PolarisDatePickerType.Range"/> the value holds two ISO dates separated by
/// <c>--</c>, for example <c>2025-05-28--2025-05-31</c>. The picker is a two-dimensional control
/// driven by a pointer, so it is awkward by keyboard or on a touch screen; a
/// <see cref="PolarisDateField"/> is usually the better choice in a form.
/// </remarks>
public sealed class PolarisDatePicker : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-date-picker";

    /// <summary>The date, or a <c>start--end</c> range. Set as a DOM property after the element upgrades.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Initial value only. Changing this after the first render has no effect.</summary>
    [Parameter] public string? DefaultValue { get; set; }

    /// <summary>Month to show, as <c>YYYY-MM</c>.</summary>
    [Parameter] public string? View { get; set; }

    /// <summary>Month shown on first render, as <c>YYYY-MM</c>.</summary>
    [Parameter] public string? DefaultView { get; set; }

    /// <summary>Whether one date or a range is chosen.</summary>
    [Parameter] public PolarisDatePickerType Type { get; set; } = PolarisDatePickerType.Single;

    /// <summary>How many months are shown at once.</summary>
    [Parameter] public PolarisDatePickerVisibleMonths VisibleMonths { get; set; } = PolarisDatePickerVisibleMonths.Auto;

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

    /// <summary>Form field name, used when a native form submits the value.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Raised on every keystroke while a date is typed.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <summary>Raised when a date is chosen.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised when the picker receives focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the picker loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <summary>Raised when the visible month changes.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnViewChange { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Input, OnInput);
        events.On(PolarisEvents.Change, OnChange);
        events.On(PolarisEvents.Focus, OnFocus);
        events.On(PolarisEvents.Blur, OnBlur);
        events.On(PolarisEvents.ViewChange, OnViewChange);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["name"] = Name;
        attributes["view"] = View;
        attributes["defaultView"] = DefaultView;
        attributes["type"] = PolarisAttributeValue.EnumValue(Type);
        attributes["visibleMonths"] = PolarisAttributeValue.EnumValue(VisibleMonths);
        attributes["allow"] = Allow;
        attributes["disallow"] = Disallow;
        attributes["allowDays"] = AllowDays;
        attributes["disallowDays"] = DisallowDays;

        attributes["defaultValue"] = Value ?? DefaultValue;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Value is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = Value };
}
