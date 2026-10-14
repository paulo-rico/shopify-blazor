using Microsoft.AspNetCore.Components;
using Shopify.Blazor.AppBridge;

namespace Shopify.Blazor.Components;

/// <summary>
/// Provides App Bridge to a subtree and reports whether the page is embedded in the Admin.
/// </summary>
/// <remarks>
/// The same URL serves the embedded app and a standalone browser tab, and <c>window.shopify</c>
/// exists only inside the Admin. Every App Bridge call in this toolkit is therefore guarded, and
/// <see cref="IsEmbedded"/> is what a page uses to explain the difference to a developer instead
/// of rendering an empty shell.
/// </remarks>
public sealed class ShopifyApp : ComponentBase
{
    private bool? _embedded;

    /// <summary>The App Bridge interop service.</summary>
    [Inject]
    public IAppBridgeInterop AppBridge { get; set; } = default!;

    /// <summary>The subtree that can use App Bridge.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// True when the page is inside the Shopify Admin, where App Bridge exists; <c>null</c> while
    /// that is still being determined, which is the state during prerendering.
    /// </summary>
    public bool? IsEmbedded => _embedded;

    /// <summary>Mints a fresh session token for a call to this app's own backend.</summary>
    public ValueTask<string?> GetIdTokenAsync() => AppBridge.GetIdTokenAsync();

    /// <summary>Calls one of this app's endpoints with a freshly minted session token.</summary>
    public ValueTask<AppBridgeFetchResult<T>> FetchAsync<T>(string url, string method = "GET", object? body = null)
        => AppBridge.FetchAsync<T>(url, method, body);

    /// <summary>Shows a toast in the Admin. A no-op outside it.</summary>
    public ValueTask ToastAsync(string message, bool isError = false) => AppBridge.ShowToastAsync(message, isError);

    /// <summary>Runs work with the Admin's header progress bar shown and reliably cleared.</summary>
    public Task<T> WithLoadingAsync<T>(Func<Task<T>> action) => AppBridge.WithLoadingAsync(action);

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<ShopifyApp>>(0);
        builder.AddAttribute(1, "Value", this);
        builder.AddAttribute(2, "IsFixed", true);
        builder.AddAttribute(3, "ChildContent", ChildContent);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        var embedded = await AppBridge.IsAvailableAsync();

        if (embedded != _embedded)
        {
            _embedded = embedded;
            StateHasChanged();
        }
    }
}
