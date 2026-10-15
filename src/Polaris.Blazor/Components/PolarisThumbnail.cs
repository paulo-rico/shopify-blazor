using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-thumbnail&gt;</c>, a small preview of content, a product or media.</summary>
/// <remarks>Use an <see cref="PolarisImage"/> instead when the picture is content in its own right.</remarks>
public sealed class PolarisThumbnail : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-thumbnail";

    /// <summary>Image URL.</summary>
    [Parameter] public string? Src { get; set; }

    /// <summary>Alternative description of the image.</summary>
    [Parameter] public string? Alt { get; set; }

    /// <summary>Thumbnail size. Polaris documents this as the small through large steps.</summary>
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

        attributes["src"] = Src;
        attributes["alt"] = Alt;
        attributes["size"] = PolarisAttributeValue.EnumValue(Size);
    }
}
