using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-text-field&gt;</c>.</summary>
public sealed class PolarisTextField : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-text-field";

    /// <summary>Visible field label. Fields are self-labelling; do not add a sibling label element.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message. When set, the field renders in its error state.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Placeholder shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// The field's value.
    /// </summary>
    /// <remarks>
    /// Written to the element as a DOM property after it upgrades. Polaris' <c>value</c> is not
    /// reflected, so an attribute-only render is unreliable once the server changes the value.
    /// </remarks>
    [Parameter] public string? Value { get; set; }

    /// <summary>Initial value only. Changing this after the first render has no effect.</summary>
    [Parameter] public string? DefaultValue { get; set; }

    /// <summary>Form field name, used when a native form submits the field.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Name of a predefined admin icon shown inside the field.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Static text before the value. Not part of the submitted value.</summary>
    [Parameter] public string? Prefix { get; set; }

    /// <summary>Static text after the value. Not part of the submitted value.</summary>
    [Parameter] public string? Suffix { get; set; }

    /// <summary>Marks the field as required.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Prevents editing.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the value but prevents editing.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Maximum accepted length. Pasting can still exceed it, so validate server-side.</summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary>Minimum accepted length.</summary>
    [Parameter] public int? MinLength { get; set; }

    /// <summary>Autofill hint passed through to the browser.</summary>
    [Parameter] public string? Autocomplete { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised on every keystroke. <c>args.Value</c> holds the current value.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <summary>Raised when the value is committed: blur or Enter.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised when the field receives focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the field loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <summary>
    /// Raised on every key press inside the field, with the key's name in
    /// <see cref="PolarisEventArgs.Key"/> — <c>ArrowDown</c>, <c>Enter</c>, <c>Escape</c>.
    /// </summary>
    /// <remarks>
    /// A text field rather than a search field is what makes this usable for Escape: a browser clears
    /// an <c>&lt;input type="search"&gt;</c> when Escape is pressed, which would overwrite whatever a
    /// handler put back. The key still is not consumed — the interop layer has no <c>preventDefault</c>.
    /// </remarks>
    [Parameter] public EventCallback<PolarisEventArgs> OnKeyDown { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Input, OnInput);
        events.On(PolarisEvents.Change, OnChange);
        events.On(PolarisEvents.Focus, OnFocus);
        events.On(PolarisEvents.Blur, OnBlur);
        events.On(PolarisEvents.KeyDown, OnKeyDown);
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
        attributes["icon"] = Icon;
        attributes["prefix"] = Prefix;
        attributes["suffix"] = Suffix;
        attributes["required"] = PolarisAttributeValue.Format(Required);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["readOnly"] = PolarisAttributeValue.Format(ReadOnly);
        attributes["maxLength"] = PolarisAttributeValue.Format(MaxLength);
        attributes["minLength"] = PolarisAttributeValue.Format(MinLength);
        attributes["autocomplete"] = Autocomplete;
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
