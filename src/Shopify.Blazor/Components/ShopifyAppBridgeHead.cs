using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Options;

namespace Shopify.Blazor.Components;

/// <summary>
/// Renders the three head elements an embedded Shopify app requires.
/// </summary>
/// <remarks>
/// <para>
/// Place this inside <c>&lt;HeadContent&gt;</c>. Both script tags are required and both must be
/// present before the app's own components render: App Bridge reads the API key from the meta tag
/// and injects the <c>shopify</c> global, and Polaris defines the custom elements the UI is built
/// from. Loading either one lazily produces a page of unstyled, inert elements with no error.
/// </para>
/// <para>
/// App Bridge is loaded first because Polaris components take their admin behaviour from the host
/// App Bridge provides.
/// </para>
/// </remarks>
public sealed class ShopifyAppBridgeHead : ComponentBase
{
    /// <summary>The app configuration, for the API key.</summary>
    [Inject]
    public IOptions<ShopifyOptions> ShopifyOptions { get; set; } = default!;

    /// <summary>Design-system script configuration.</summary>
    [Inject]
    public IOptions<AppBridge.AppBridgeOptions> AppBridgeOptions { get; set; } = default!;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var apiKey = ShopifyOptions.Value.ApiKey;
        var appBridge = AppBridgeOptions.Value.AppBridgeScriptUrl;
        var polaris = AppBridgeOptions.Value.PolarisScriptUrl;

        builder.OpenElement(0, "meta");
        builder.AddAttribute(1, "name", "shopify-api-key");
        builder.AddAttribute(2, "content", apiKey);
        builder.CloseElement();

        builder.OpenElement(3, "script");
        builder.AddAttribute(4, "src", appBridge);
        builder.CloseElement();

        builder.OpenElement(5, "script");
        builder.AddAttribute(6, "src", polaris);
        builder.CloseElement();
    }
}
