using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-color-picker&gt;</c>, the palette with no field chrome.</summary>
/// <remarks>
/// This is the bare picker: no label, no help text and no error state. Reach for a
/// <see cref="PolarisColorField"/> when the colour is one value in a form, and use this only when
/// the picker itself is the content. It works in HSL internally and reports hex.
/// </remarks>
public sealed class PolarisColorPicker : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-color-picker";

    /// <summary>The colour as a hex string. Set as a DOM property after the element upgrades.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Initial colour only. Changing this after the first render has no effect.</summary>
    [Parameter] public string? DefaultValue { get; set; }

    /// <summary>Adds an opacity control and makes the emitted value eight-digit hex.</summary>
    [Parameter] public bool Alpha { get; set; }

    /// <summary>Form field name, used when a native form submits the value.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Raised on every change while the colour is being dragged.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <summary>Raised when the colour is committed.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Input, OnInput);
        events.On(PolarisEvents.Change, OnChange);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["name"] = Name;
        attributes["alpha"] = PolarisAttributeValue.Format(Alpha);
        attributes["defaultValue"] = Value ?? DefaultValue;
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Value is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = Value };
}
