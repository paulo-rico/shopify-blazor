using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-link&gt;</c>, for navigating to another page.</summary>
/// <remarks>
/// <para>
/// A link is for navigation. For a prominent action or a form submission use a
/// <see cref="PolarisButton"/> instead: a link has no loading and no disabled state, so it cannot
/// show progress or prevent a double submit.
/// </para>
/// <para>
/// The Admin may intercept some navigation, so a link is not a reliable way to reach an arbitrary
/// external page from inside the iframe.
/// </para>
/// </remarks>
public sealed class PolarisLink : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-link";

    /// <summary>Destination URL. Without one the link renders as plain text.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Semantic colour. <see cref="PolarisTone.Critical"/> is documented; others are not.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>
    /// Browsing context to open the destination in.
    /// </summary>
    /// <remarks><c>_blank</c> makes Polaris add <c>rel="noopener noreferrer"</c> for you.</remarks>
    [Parameter] public PolarisLinkTarget Target { get; set; } = PolarisLinkTarget.Auto;

    /// <summary>Marks the destination as a download. Unreliable when the file is cross-origin.</summary>
    [Parameter] public string? Download { get; set; }

    /// <summary>Language of the linked resource, as a BCP 47 tag.</summary>
    [Parameter] public string? Lang { get; set; }

    /// <summary>Id of the element this link controls, for a declarative command.</summary>
    [Parameter] public string? CommandFor { get; set; }

    /// <summary>The command sent to <see cref="CommandFor"/>.</summary>
    [Parameter] public PolarisCommand Command { get; set; } = PolarisCommand.Auto;

    /// <summary>Id of a tooltip or popover shown when the link is hovered or focused.</summary>
    [Parameter] public string? InterestFor { get; set; }

    /// <summary>Raised on click, before the Admin decides whether to allow the navigation.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnClick { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Click, OnClick);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["href"] = Href;
        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["target"] = Href is null ? null : PolarisAttributeValue.EnumValue(Target);
        attributes["download"] = Download;
        attributes["lang"] = Lang;
        attributes["commandFor"] = CommandFor;
        attributes["command"] = CommandFor is null ? null : PolarisAttributeValue.EnumValue(Command);
        attributes["interestFor"] = InterestFor;
    }
}
