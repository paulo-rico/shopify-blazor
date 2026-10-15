using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-drop-zone&gt;</c>, for uploading files by drag and drop.</summary>
/// <remarks>
/// <para>
/// Polaris validates the file <em>type</em> against <see cref="Accept"/> and nothing else: size,
/// count and content are the application's problem, and the change event does not upload anything by
/// itself.
/// </para>
/// <para>
/// The element also exposes a <c>files</c> property, which is an array of browser <c>File</c>
/// objects. Those cannot cross the .NET interop boundary, so this wrapper deliberately does not
/// model it: the upload path belongs in the browser, and C# receives <see cref="Value"/> and the
/// events instead.
/// </para>
/// </remarks>
public sealed class PolarisDropZone : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-drop-zone";

    /// <summary>Visible label for the zone.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Validation message. When set, the zone renders in its error state.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Comma-separated file types the zone accepts, for example <c>.jpg,.png,.gif</c>.</summary>
    [Parameter] public string? Accept { get; set; }

    /// <summary>Allows more than one file. Without it a second drop replaces the first.</summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>
    /// The zone's submitted value.
    /// </summary>
    /// <remarks>
    /// Polaris cannot be handed files from the server: the property is read-only in the element once
    /// it has files, so the only thing this can do is reset it. Set an empty string to clear the
    /// zone.
    /// </remarks>
    [Parameter] public string? Value { get; set; }

    /// <summary>Marks the zone as required.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Prevents interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Form field name.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Hides the label visually while keeping it for screen readers.</summary>
    [Parameter] public PolarisAccessibilityVisibility? LabelAccessibilityVisibility { get; set; }

    /// <summary>Raised when files are chosen or dropped. It does not mean they were uploaded.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnChange { get; set; }

    /// <summary>Raised when the file list changes.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnInput { get; set; }

    /// <summary>Raised when files are rejected, for example because their type is not in <see cref="Accept"/>.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnDropRejected { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Change, OnChange);
        events.On(PolarisEvents.Input, OnInput);
        events.On(PolarisEvents.DropRejected, OnDropRejected);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["label"] = Label;
        attributes["error"] = Error;
        attributes["accept"] = Accept;
        attributes["multiple"] = PolarisAttributeValue.Format(Multiple);
        attributes["required"] = PolarisAttributeValue.Format(Required);
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["name"] = Name;
        attributes["labelAccessibilityVisibility"] =
            LabelAccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(LabelAccessibilityVisibility.Value);
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => Value is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = Value };
}
