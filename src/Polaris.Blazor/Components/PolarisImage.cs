using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-image&gt;</c>.</summary>
/// <remarks>
/// <para>
/// The placeholder is shown while the image loads and whenever <see cref="Src"/> is absent, so a
/// missing image is a visible state rather than a broken icon.
/// </para>
/// <para>
/// With the default <see cref="PolarisImageInlineSize.Fill"/> the aspect ratio is honoured; with
/// <see cref="PolarisImageInlineSize.Auto"/> the ratio is ignored and nothing renders until the
/// image has loaded, which shows as a layout shift.
/// </para>
/// </remarks>
public sealed class PolarisImage : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-image";

    /// <summary>Image URL.</summary>
    [Parameter] public string? Src { get; set; }

    /// <summary>Candidate URLs for a responsive image, as the native <c>srcset</c> list.</summary>
    [Parameter] public string? SrcSet { get; set; }

    /// <summary>Sizes descriptor matching <see cref="SrcSet"/>, as the native <c>sizes</c> list.</summary>
    [Parameter] public string? Sizes { get; set; }

    /// <summary>Alternative description. Empty by default, which leaves the image unannounced.</summary>
    [Parameter] public string? Alt { get; set; }

    /// <summary>Ratio expressed as a number or a fraction, for example <c>1.5</c> or <c>16/9</c>.</summary>
    [Parameter] public string? AspectRatio { get; set; }

    /// <summary>Whether the image is contained or cropped to fill its box.</summary>
    [Parameter] public PolarisImageObjectFit ObjectFit { get; set; } = PolarisImageObjectFit.Contain;

    /// <summary>When the browser fetches the image.</summary>
    [Parameter] public PolarisImageLoading Loading { get; set; } = PolarisImageLoading.Eager;

    /// <summary>How assistive technology treats the image.</summary>
    [Parameter] public PolarisImageAccessibilityRole AccessibilityRole { get; set; } = PolarisImageAccessibilityRole.Img;

    /// <summary>Width behaviour. <c>Fill</c> is the component's default.</summary>
    [Parameter] public PolarisImageInlineSize InlineSize { get; set; } = PolarisImageInlineSize.Fill;

    /// <summary>Border treatment, for example <c>base</c>.</summary>
    [Parameter] public string? Border { get; set; }

    /// <summary>Border width.</summary>
    [Parameter] public string? BorderWidth { get; set; }

    /// <summary>Border style, for example <c>solid</c>.</summary>
    [Parameter] public string? BorderStyle { get; set; }

    /// <summary>Border colour.</summary>
    [Parameter] public string? BorderColor { get; set; }

    /// <summary>Corner radius, for example <c>base</c> or <c>full</c>.</summary>
    [Parameter] public string? BorderRadius { get; set; }

    /// <summary>Raised when the image loads.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnLoad { get; set; }

    /// <summary>Raised when the image fails to load, for example on a CORS or 404 failure.</summary>
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
        attributes["srcSet"] = SrcSet;
        attributes["sizes"] = Sizes;
        attributes["alt"] = Alt;
        attributes["aspectRatio"] = AspectRatio;
        attributes["objectFit"] = PolarisAttributeValue.EnumValue(ObjectFit);
        attributes["loading"] = PolarisAttributeValue.EnumValue(Loading);
        attributes["accessibilityRole"] = PolarisAttributeValue.EnumValue(AccessibilityRole);
        attributes["inlineSize"] = PolarisAttributeValue.EnumValue(InlineSize);
        attributes["border"] = Border;
        attributes["borderWidth"] = BorderWidth;
        attributes["borderStyle"] = BorderStyle;
        attributes["borderColor"] = BorderColor;
        attributes["borderRadius"] = BorderRadius;
    }
}
