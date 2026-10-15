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
/// as a DOM property. It is <em>always</em> written, as an empty array when nothing is selected:
/// Polaris calls array methods on this property, and leaving it undefined throws inside the
/// component rather than merely clearing the selection.
/// </para>
/// </remarks>
public sealed class PolarisChoiceList : PolarisContentComponentBase
{
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

    /// <summary>The chosen values. Null or empty selects nothing.</summary>
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
    /// <remarks>
    /// Never left unset. Polaris reads this array with array methods, so an undefined property is an
    /// uncaught <c>TypeError: Cannot read properties of undefined (reading 'join')</c> inside the
    /// component — found by the browser verification, and invisible to a markup test.
    /// </remarks>
    protected override IReadOnlyDictionary<string, object?> Properties => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["values"] = Values ?? [],
    };
}
