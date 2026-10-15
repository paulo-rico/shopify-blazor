using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-avatar&gt;</c>, a compact profile image or initials.</summary>
/// <remarks>
/// A cross-origin <see cref="Src"/> needs CORS headers or the image is not usable, and an
/// unsupported format falls back to <see cref="Initials"/>. More than two initials may truncate.
/// </remarks>
public sealed class PolarisAvatar : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-avatar";

    /// <summary>Up to two characters shown when there is no image.</summary>
    [Parameter] public string? Initials { get; set; }

    /// <summary>Image URL. Falls back to <see cref="Initials"/> when it fails to load.</summary>
    [Parameter] public string? Src { get; set; }

    /// <summary>Alternative description of the image.</summary>
    [Parameter] public string? Alt { get; set; }

    /// <summary>Avatar size. Polaris documents this as the small through large steps.</summary>
    [Parameter] public PolarisSize Size { get; set; } = PolarisSize.Base;

    /// <summary>Raised when the image loads.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnLoad { get; set; }

    /// <summary>Raised when the image fails to load.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnError { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Load, OnLoad);
        events.On(PolarisEvents.Error, OnError);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["initials"] = Initials;
        attributes["src"] = Src;
        attributes["alt"] = Alt;
        attributes["size"] = PolarisAttributeValue.EnumValue(Size);
    }
}
