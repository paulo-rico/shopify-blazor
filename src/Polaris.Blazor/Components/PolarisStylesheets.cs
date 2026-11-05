using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Polaris.Blazor.Components;

/// <summary>
/// The three stylesheets this package ships, as <c>&lt;link&gt;</c> elements for the document's head.
/// </summary>
/// <remarks>
/// <para>
/// The interop module loads the same three files, so why render them here as well? Because the module is
/// imported by JavaScript that only runs once the Blazor circuit is up, and a full page load paints long
/// before that. Measured on a deployed product editor: at 3.5 s the page had rendered its 53 disclosures
/// with the accordion's rules absent, and the stylesheet did not arrive until 6.8 s — so the first thing
/// a merchant saw was a correctly built page with no header bands, no padding and a default disclosure
/// marker. On a slower connection to the server the same window measured 15–20 seconds and read as the
/// app having lost its styling.
/// </para>
/// <para>
/// In the head, the rules are part of the first paint and there is no such window. The injection inside
/// <c>polaris-interop.js</c> stays as a fallback for an app that renders no head links, and it skips a
/// file that is already linked.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;head&gt;
///     &lt;ShopifyAppBridgeHead /&gt;
///     &lt;PolarisStylesheets /&gt;
/// &lt;/head&gt;
/// </code>
/// </example>
public sealed class PolarisStylesheets : ComponentBase
{
    private static readonly string[] Files =
    [
        "polaris-accordion.css",
        "polaris-range-slider.css",
        "polaris-grid.css",
    ];

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // A fixed list of three, so the same sequence numbers repeat: the renderer diffs by position, and
        // these links never change between renders.
        foreach (var file in Files)
        {
            var href = PolarisStaticAssets.Url(file);

            builder.OpenElement(0, "link");
            builder.AddAttribute(1, "rel", "stylesheet");
            builder.AddAttribute(2, "href", href);
            builder.AddAttribute(3, "data-polaris-stylesheet", href);
            builder.CloseElement();
        }
    }
}
