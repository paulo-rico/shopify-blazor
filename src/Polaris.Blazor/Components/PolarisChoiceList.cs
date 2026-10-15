using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-choice-list&gt;</c>, for choosing one or many options.</summary>
/// <remarks>
/// <para>
/// Use this instead of a <see cref="PolarisSelect"/> when more than one option can be chosen: a
/// select is single-select by construction. The whole list is either single or multiple, never a
/// mix, and it has no search, filter or lazy loading, so a long list needs a different control.
/// </para>
/// <para>
/// <see cref="Values"/> is an array, which no attribute can carry, so it is written to the element
/// as a DOM property after it upgrades and cleared when it goes back to null. A stale array here
/// leaves options selected that the server has since deselected, and the markup shows nothing.
/// </para>
/// </remarks>
public sealed class PolarisChoiceList : PolarisContentComponentBase
{
    private static readonly string[] ValuesProperty = ["values"];

    /// <inheritdoc />
    protected override string TagName => "s-choice-list";

    /// <summary>Visible list label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the list.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Validation message.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Allows more than one choice. Without it the list is a radio group.</summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>The chosen values. Empty selects nothing; null clears the property entirely.</summary>
    [Parameter] public IReadOnlyList<string>? Values { get; set; }

    /// <summary>Prevents interaction with the whole list.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Form field name.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised when the selection is committed. <c>args.Values</c> holds the chosen values.</summary>
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
        attributes["multiple"] = PolarisAttributeValue.Format(Multiple);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["name"] = Name;
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Values is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["values"] = Values };

    /// <inheritdoc />
    protected override IEnumerable<string> ClearedProperties => Values is null ? ValuesProperty : [];
}
